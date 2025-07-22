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

	protected override void OnUpdate()
	{
		DrawGizmos();
	}

	private void RunWheelTrace( float inwardOffset = 0 )
	{
		var car = CarController.Local;

		var traceDist = car.SuspensionHeight + WheelRadius;
		var startPos = WorldPosition;
		var endPos = WorldPosition + WorldRotation.Down * traceDist;
		var inwardDirection = FlipWheelSpinRotation ? WorldRotation.Forward : WorldRotation.Backward;

		WheelTraceInwardOffset = inwardDirection * inwardOffset;
		startPos += WheelTraceInwardOffset;
		endPos += WheelTraceInwardOffset;

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineThickness = 10f;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + inwardDirection * 30 );

		// Rotate cylinder 90 degrees to align axis with axle
		WheelTrace = WheelTrace = Scene.Trace.Cylinder( WheelWidth, WheelRadius / 2, startPos, endPos )
			.Rotated( Rotation.LookAt( WorldRotation.Down, WheelModel.WorldRotation.Right ) )
			.WithoutTags( "wheel", "car" )
			.IgnoreGameObjectHierarchy( car.GameObject )
			.Run();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		var car = CarController.Local;

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

		if ( !WheelTrace.Hit )
			return;

		var springDirection = WheelTrace.Normal;
		var wheelOffset = car.SuspensionHeight - WheelTrace.Distance;

		var wheelWorldVelocity = car.Rigidbody.GetVelocityAtPoint( WorldPosition );
		var wheelVelocity = Vector3.Dot( springDirection, wheelWorldVelocity );

		var wheelForce = wheelOffset * (car.SuspensionStrength * CarryingMass) - wheelVelocity *
			(car.SuspensionDamping * CarryingMass);

		wheelForce = MathF.Max( 0f, wheelForce );

		car.Rigidbody.ApplyForceAt( WorldPosition, springDirection * wheelForce );
		BlockSliding( wheelForce );

		// Position wheel models
		if ( WheelModel.IsValid() )
		{
			WheelModel.WorldPosition = WheelTrace.EndPosition + Vector3.Up * WheelRadius / 2 + -WheelTraceInwardOffset;

			if ( HandbrakeApplied )
				return;

			var wheelVelocityModel = car.Rigidbody.GetVelocityAtPoint( WheelModel.WorldPosition );

			WheelModel.LocalRotation *=
				Rotation.From( wheelVelocityModel.Length * Time.Delta * (FlipWheelSpinRotation ? -1f : 1f), 0, 0 );
		}
	}

	private void BlockSliding( float wheelForce )
	{
		if ( wheelForce <= 0f )
			return;

		var car = CarController.Local;

		var steeringDirection = FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward;

		var wheelVelocity = car.Rigidbody.GetVelocityAtPoint( WorldPosition );

		var steeringVelocity = Vector3.Dot( steeringDirection, wheelVelocity );

		var gripFactor = Input.Down( "brake" ) ? 0.2f : 1f;

		var wishVelocityChange = -steeringVelocity * gripFactor;

		var wishAcceleration = wishVelocityChange / Time.Delta;

		var desiredForce = CarryingMass * wishAcceleration;

		var muLateral = 1.5f;

		var maxForceMag = muLateral * gripFactor * wheelForce;

		var appliedForce = Math.Clamp( desiredForce, -maxForceMag, maxForceMag );

		car.Rigidbody.ApplyForceAt( WorldPosition, steeringDirection * appliedForce );
	}

	public void Accelerate( float distribution )
	{
		if ( !WheelTrace.Hit || Vector3.Dot( WheelTrace.Normal, WorldRotation.Up ) <= 0.7f )
			return;

		var car = CarController.Local;
		var carSpeed = Vector3.Dot( car.WorldRotation.Left, car.Rigidbody.Velocity );

		var accelerationDirection = WorldTransform.Left;
		var accelerationInput = Input.AnalogMove.x;

		if ( accelerationInput > 0 )
		{
			var normalizedSpeed = (MathF.Abs( carSpeed ) / car.MaxSpeed).Clamp( 0, 1 );

			var availableTorque = car.TorqueCurve.Evaluate( normalizedSpeed ) * accelerationInput * distribution;

			car.Rigidbody.ApplyForceAt( WorldPosition,
				accelerationDirection * availableTorque * car.Rigidbody.Mass * 100f );
		}

		if ( accelerationInput < 0 || accelerationInput == 0 )
		{
			if ( accelerationInput == 0 )
			{
				var dragCoeff = 0.15f;
				var dragForce = dragCoeff * carSpeed * car.Rigidbody.Mass * distribution;

				car.Rigidbody.ApplyForceAt( WorldPosition, -accelerationDirection * dragForce );

				return;
			}

			var brakeInput = MathF.Abs( accelerationInput );
			var brakeForce = brakeInput * car.BrakeStrength * car.Rigidbody.Mass * distribution;

			car.Rigidbody.ApplyForceAt( WorldPosition,
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
