namespace SBRacer.Car;

[Category( "SB Racer" ), Title( "Car Wheel" ), Icon( "adjust" )]
public class CarWheel : Component
{
	[Property] public ModelRenderer WheelModel { get; set; }
	[Property] public bool FlipWheelSpinRotation { get; set; } = false;

	// https://www.youtube.com/watch?v=CdPYlj5uZeI

	private SceneTraceResult WheelTrace { get; set; }
	private float WheelHeight => WheelModel.Model.Bounds.Size.z * WheelModel.WorldScale.z / 2;
	private float CarryingMass => CarController.Local.Rigidbody.Mass / 4;

	public bool HandbrakeApplied { get; set; }

	protected override void OnUpdate()
	{
		DrawGizmos();
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		var car = CarController.Local;

		WheelTrace = Scene.Trace.Ray( WorldPosition, WorldPosition + WorldTransform.Down * WheelHeight )
			.Run();
		// Log.Info( WheelTrace.GameObject );

		if ( !WheelTrace.Hit )
			return;

		var wheelForce = 0f; // New: Store for use as normal in friction

		// Suspension
		// Spring = (Offset x Strength) - (Velocity x Damping)
		{
			// World space direction of the spring force
			var springDirection = WorldRotation.Up;

			// Calculate wheel offset from the raycast
			var wheelOffset = car.SuspensionHeight + WheelHeight / 2 - WheelTrace.Distance;

			// World space velocity of this tyre
			var wheelWorldVelocity = car.Rigidbody.GetVelocityAtPoint( WorldPosition );

			// Calculate velocity along the spring direction, springDir is a normal so this returns the magnitude 
			// of wheelWorldVelocity projected onto springDirection
			var wheelVelocity = Vector3.Dot( springDirection, wheelWorldVelocity );

			// Calculate the magnitude of the dampened spring force
			wheelForce = wheelOffset * (car.SuspensionStrength * CarryingMass) - wheelVelocity *
				(car.SuspensionDamping * CarryingMass);

			// New: Clamp to prevent negative forces (suspension doesn't pull down)
			wheelForce = MathF.Max( 0f, wheelForce );

			car.Rigidbody.ApplyForceAt( WorldPosition, springDirection * wheelForce );
		}

		BlockSliding( wheelForce );

		// Position wheel models
		if ( WheelModel.IsValid() )
		{
			WheelModel.WorldPosition = WorldPosition.WithZ( WheelTrace.EndPosition.z + WheelHeight );

			// Don't spin wheels if the handbrake is applied.
			if ( HandbrakeApplied )
				return;

			var wheelVelocity = car.Rigidbody.GetVelocityAtPoint( WorldPosition );

			WheelModel.LocalRotation *=
				Rotation.From( wheelVelocity.Length * Time.Delta * (FlipWheelSpinRotation ? -1f : 1f), 0, 0 );
		}
	}

	private void BlockSliding( float wheelForce )
	{
		if ( !WheelTrace.Hit || wheelForce <= 0f ) // New: No friction without compression
			return;

		// Steering
		// Force = Mass x Acceleration
		// Acceleration = Change in Velocity / Time

		var car = CarController.Local;

		// World space direction of the spring force
		var steeringDirection = FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward;

		// World space velocity of the suspension
		var wheelVelocity = car.Rigidbody.GetVelocityAtPoint( WorldPosition );

		// What is the tyres velocity in the steering direction?
		// Steering direction is a normal, so this will return the magnitude of wheelVelocity projected
		// onto steeringDirection
		var steeringVelocity = Vector3.Dot( steeringDirection, wheelVelocity );

		// Reduce grip during handbrake for sliding (only affects this wheel if handbraking)
		var gripFactor = Input.Down( "brake" ) ? 0.2f : 1f; // Low grip = more slide

		var wishVelocityChange = -steeringVelocity * gripFactor;

		// Turn change in velocity into acceleration
		// This will produce the acceleration necessary to change the velocity by wishVelocityChange in 1 physics
		// step.
		var wishAcceleration = wishVelocityChange / Time.Delta;

		// Force = Mass * Acceleration, multiply by the mass of the tyre and apply as a force.
		var desiredForce = CarryingMass * wishAcceleration;

		// New: Clamp to friction limit (realistic max lateral force = mu * normal; here mu ≈ gripFactor, but tunable)
		var muLateral =
			1.5f; // Tune: Base tire friction coeff (1.0-1.5 for dry asphalt; multiply by gripFactor for reductions)
		var maxForceMag = muLateral * gripFactor * wheelForce;
		var appliedForce = Math.Clamp( desiredForce, -maxForceMag, maxForceMag );

		car.Rigidbody.ApplyForceAt( WorldPosition, steeringDirection * appliedForce );
	}

	public void Accelerate( float distribution )
	{
		// Acceleration / braking
		if ( !WheelTrace.Hit )
			return;

		var car = CarController.Local;
		var carSpeed = Vector3.Dot( car.WorldRotation.Left, car.Rigidbody.Velocity );

		var accelerationDirection = WorldTransform.Left;
		var accelerationInput = Input.AnalogMove.x;

		if ( accelerationInput > 0 )
		{
			var normalizedSpeed = (MathF.Abs( carSpeed ) / car.MaxSpeed).Clamp( 0, 1 );

			// Evaluate torque curve to figure out how much torque to apply
			var availableTorque = car.TorqueCurve.Evaluate( normalizedSpeed ) * accelerationInput * distribution;

			car.Rigidbody.ApplyForceAt( WorldPosition,
				accelerationDirection * availableTorque * car.Rigidbody.Mass * 100f );
		}

		// Braking / air resistance
		if ( accelerationInput < 0 || accelerationInput == 0 )
		{
			if ( accelerationInput == 0 )
			{
				var dragCoeff = 0.15f;
				var dragForce = dragCoeff * carSpeed * car.Rigidbody.Mass *
				                distribution;

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
		if ( !WheelTrace.Hit )
			return;

		HandbrakeApplied = true;

		var car = CarController.Local;

		var accelerationDirection = WorldTransform.Left;
		var localSpeed = Vector3.Dot( accelerationDirection, car.Rigidbody.Velocity );

		if ( MathF.Abs( localSpeed ) < 0.1f )
			return; // No force if stopped

		// Apply strong opposing force to simulate locking
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

		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.Line( WorldPosition,
			WorldPosition + (FlipWheelSpinRotation ? WorldRotation.Backward : WorldRotation.Forward) * 10f );

		Gizmo.Draw.Color = Color.Blue;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + WorldRotation.Left * 10f );

		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.Line( WorldPosition, WorldPosition + WorldRotation.Up * 10f );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( WheelTrace.StartPosition, WheelTrace.EndPosition );
		Gizmo.Draw.SolidSphere( WheelTrace.EndPosition, 1f );

		//Gizmo.Draw.ScreenText( $"{CarController.Local.}" );
	}
}
