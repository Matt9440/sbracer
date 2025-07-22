namespace SBRacer.Car;

[Category( "SB Racer" ), Title( "Car Wheel" ), Icon( "adjust" )]
public class CarWheel : Component
{
	[Property] public ModelRenderer WheelModel { get; set; }
	[Property] public bool FlipWheelSpinRotation { get; set; } = false;

	private SceneTraceResult WheelTrace { get; set; }
	private float WheelRadius => WheelModel.Model.Bounds.Size.z * WheelModel.WorldScale.z / 2;
	private float WheelWidth => WheelModel.Model.Bounds.Size.y * WheelModel.WorldScale.y;
	private float CarryingMass => CarController.Local.Rigidbody.Mass / 4;

	public bool HandbrakeApplied { get; set; }
	private Vector3 WheelTraceInwardOffset { get; set; }
	private CarController Car => CarController.Local;

	protected override void OnUpdate()
	{
		DrawGizmos();
	}

	private void RunWheelTrace( float inwardOffset = 0 )
	{
		var traceDist = Car.SuspensionHeight + WheelRadius;
		var startPos = WorldPosition;
		var endPos = WorldPosition + WorldRotation.Down * traceDist;
		var inwardDirection = FlipWheelSpinRotation ? WorldRotation.Forward : WorldRotation.Backward;

		WheelTraceInwardOffset = inwardDirection * inwardOffset;
		startPos += WheelTraceInwardOffset;
		endPos += WheelTraceInwardOffset;

		// Rotate cylinder 90 degrees to align axis with axle
		WheelTrace = WheelTrace = Scene.Trace.Cylinder( WheelWidth, WheelRadius / 2, startPos, endPos )
			.Rotated( Rotation.LookAt( WorldRotation.Down, WheelModel.WorldRotation.Right ) )
			.WithoutTags( "wheel", "car" )
			.IgnoreGameObjectHierarchy( Car.GameObject )
			.Run();
	}

	/// <summary>
	///     Run a trace in the direction the wheel is facing, apply forces to stop the wheel from clipping through the wall.
	/// </summary>
	private void HandleWallCollisions()
	{
		Gizmo.Draw.IgnoreDepth = true;

		var wheelFaceDirection = FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward;
		var wallTrace = Scene.Trace
			.Ray( WheelTrace.EndPosition - WheelTraceInwardOffset,
				WheelTrace.EndPosition - WheelTraceInwardOffset + wheelFaceDirection * WheelWidth / 2 )
			.IgnoreGameObjectHierarchy( Car.GameObject )
			.Run();

		if ( wallTrace.Hit || wallTrace.StartedSolid )
		{
			Gizmo.Draw.Line( wallTrace.EndPosition, wallTrace.EndPosition + wallTrace.Normal * 100 );

			var impulseStrength = 0.8f;
			var normal = wallTrace.Normal;
			var vel = Car.Rigidbody.GetVelocityAtPoint( WorldPosition );
			var relativeNormalVel = Vector3.Dot( vel, normal );

			var speedInto = -relativeNormalVel;
			var impulse =
				normal * speedInto * Car.Rigidbody.Mass * impulseStrength;

			Car.Rigidbody.ApplyImpulseAt( WorldPosition, impulse );
		}

		Gizmo.Draw.LineThickness = 5f;
		Gizmo.Draw.Line( wallTrace.StartPosition, wallTrace.EndPosition );
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		RunWheelTrace();

		// If the cylinder trace has started solid it's probably in a wall, move it inwards towards the body of the car.
		if ( WheelTrace.StartedSolid )
		{
			var maxInwardOffset = 10f; // Maximum distance to move inward 
			var stepSize = 1f; // Incremental step size for inward movement

			// Try moving inward until not solid or max offset reached
			for ( var inwardOffset = stepSize; inwardOffset <= maxInwardOffset; inwardOffset += stepSize )
			{
				RunWheelTrace( inwardOffset );

				if ( !WheelTrace.StartedSolid )
					break;
			}
		}

		HandleWallCollisions();

		if ( !WheelTrace.Hit )
			return;

		var springDirection = WheelTrace.Normal;
		var wheelOffset = Car.SuspensionHeight - WheelTrace.Distance;

		var wheelWorldVelocity = Car.Rigidbody.GetVelocityAtPoint( WorldPosition );
		var wheelVelocity = Vector3.Dot( springDirection, wheelWorldVelocity );

		var wheelForce = wheelOffset * (Car.SuspensionStrength * CarryingMass) - wheelVelocity *
			(Car.SuspensionDamping * CarryingMass);

		wheelForce = MathF.Max( 0f, wheelForce );

		Car.Rigidbody.ApplyForceAt( WorldPosition, springDirection * wheelForce );
		BlockSliding( wheelForce );

		// Position wheel models
		if ( WheelModel.IsValid() )
		{
			WheelModel.WorldPosition = WheelTrace.EndPosition + Vector3.Up * WheelRadius / 2 + -WheelTraceInwardOffset;

			if ( HandbrakeApplied )
				return;

			var wheelVelocityModel = Car.Rigidbody.GetVelocityAtPoint( WheelModel.WorldPosition );

			WheelModel.LocalRotation *=
				Rotation.From( wheelVelocityModel.Length * Time.Delta * (FlipWheelSpinRotation ? -1f : 1f), 0, 0 );
		}
	}

	private void BlockSliding( float wheelForce )
	{
		if ( wheelForce <= 0f )
			return;

		var steeringDirection = FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward;

		var wheelVelocity = Car.Rigidbody.GetVelocityAtPoint( WorldPosition );

		var steeringVelocity = Vector3.Dot( steeringDirection, wheelVelocity );

		var gripFactor = Input.Down( "brake" ) ? 0.2f : 1f;

		var wishVelocityChange = -steeringVelocity * gripFactor;

		var wishAcceleration = wishVelocityChange / Time.Delta;

		var desiredForce = CarryingMass * wishAcceleration;

		var muLateral = 1.5f;

		var maxForceMag = muLateral * gripFactor * wheelForce;

		var appliedForce = Math.Clamp( desiredForce, -maxForceMag, maxForceMag );

		Car.Rigidbody.ApplyForceAt( WorldPosition, steeringDirection * appliedForce );
	}

	public void Accelerate( float distribution )
	{
		if ( !WheelTrace.Hit || Vector3.Dot( WheelTrace.Normal, WorldRotation.Up ) <= 0.7f )
			return;

		var carSpeed = Vector3.Dot( Car.WorldRotation.Left, Car.Rigidbody.Velocity );

		var accelerationDirection = WorldTransform.Left;
		var accelerationInput = Input.AnalogMove.x;

		if ( accelerationInput > 0 )
		{
			var normalizedSpeed = (MathF.Abs( carSpeed ) / Car.MaxSpeed).Clamp( 0, 1 );

			var availableTorque = Car.TorqueCurve.Evaluate( normalizedSpeed ) * accelerationInput * distribution;

			Car.Rigidbody.ApplyForceAt( WorldPosition,
				accelerationDirection * availableTorque * Car.Rigidbody.Mass * 100f );
		}

		if ( accelerationInput < 0 || accelerationInput == 0 )
		{
			if ( accelerationInput == 0 )
			{
				var dragCoeff = 0.15f;
				var dragForce = dragCoeff * carSpeed * Car.Rigidbody.Mass * distribution;

				Car.Rigidbody.ApplyForceAt( WorldPosition, -accelerationDirection * dragForce );

				return;
			}

			var brakeInput = MathF.Abs( accelerationInput );
			var brakeForce = brakeInput * Car.BrakeStrength * Car.Rigidbody.Mass * distribution;

			Car.Rigidbody.ApplyForceAt( WorldPosition,
				-accelerationDirection * brakeForce );
		}
	}

	public void HandBrake( float distribution )
	{
		if ( !WheelTrace.Hit || Vector3.Dot( WheelTrace.Normal, WorldRotation.Up ) <= 0.7f )
			return;

		HandbrakeApplied = true;

		var car = CarController.Local;

		var accelerationDirection = WorldTransform.Left;
		var localSpeed = Vector3.Dot( accelerationDirection, car.Rigidbody.Velocity );

		if ( MathF.Abs( localSpeed ) < 0.1f )
			return;

		var brakeDir = -accelerationDirection.Normal * MathF.Sign( localSpeed );
		var brakeForce = car.HandBrakeStrength * distribution * car.Rigidbody.Mass;

		car.Rigidbody.ApplyForceAt( WorldPosition, brakeDir * brakeForce );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 2f;

		Gizmo.Transform = global::Transform.Zero;

		/*Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.Line( WorldPosition,
			WorldPosition + (FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward) * 10f );

		Gizmo.Draw.Color = Color.Blue;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + WorldRotation.Left * 10f );

		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + WorldRotation.Up * 10f );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( WheelTrace.StartPosition, WheelTrace.EndPosition );
		Gizmo.Draw.SolidSphere( WheelTrace.EndPosition, 1f );*/

		if ( WheelModel.IsValid() )
		{
			Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.2f );
			var axleDir = WheelModel.WorldRotation.Right;

			Gizmo.Draw.SolidCylinder(
				WheelTrace.EndPosition - axleDir * WheelWidth / 2 + Vector3.Up * WheelRadius / 2,
				WheelTrace.EndPosition + axleDir * WheelWidth / 2 + Vector3.Up * WheelRadius / 2,
				WheelRadius );
		}
	}
}
