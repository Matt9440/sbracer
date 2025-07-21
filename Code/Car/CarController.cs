namespace SBRacer.Car;

[Category( "SB Racer" ), Title( "Car Controller" ), Icon( "toys" )]
public class CarController : Component
{
	public static CarController Local { get; set; }

	[Property, Category( "References" )] public Rigidbody Rigidbody { get; set; }
	[Property, Category( "References" )] public CarWheel FrontLeftWheel { get; set; }
	[Property, Category( "References" )] public CarWheel FrontRightWheel { get; set; }
	[Property, Category( "References" )] public CarWheel BackLeftWheel { get; set; }
	[Property, Category( "References" )] public CarWheel BackRightWheel { get; set; }
	[Property, Category( "References" )] public GameObject CameraLookAt { get; set; }

	[Property, Category( "Movement" )] public DriveType DriveType { get; set; } = DriveType.FrontWheelDrive;
	[Property, Category( "Movement" )] public float MaxSpeed { get; set; } = 100f;
	[Property, Category( "Movement" )] public Curve TorqueCurve { get; set; }
	[Property, Category( "Movement" )] public float SteeringSpeed { get; set; } = 120f; // Max degrees per second
	[Property, Category( "Movement" )] public Curve SteeringEffectivenessCurve { get; set; }
	[Property, Category( "Movement" )] public float BrakeStrength { get; set; } = 50f;
	[Property, Category( "Movement" )] public float HandBrakeStrength { get; set; } = 100f;

	[Property, Category( "Suspension" )] public float SuspensionHeight { get; set; } = 30f;
	[Property, Category( "Suspension" )] public float SuspensionDamping { get; set; } = 2500f;
	[Property, Category( "Suspension" )] public float SuspensionStrength { get; set; } = 20000f;

	/// <summary>
	///     The length between a front wheel and a back wheel
	/// </summary>
	private float WheelBaseLength => (FrontLeftWheel.WorldPosition - BackLeftWheel.WorldPosition).Length;

	/// <summary>
	///     The distance between opposite wheels
	/// </summary>
	private float TrackWidthLength => (FrontLeftWheel.WorldPosition - FrontRightWheel.WorldPosition).Length;

	[Property] public float MaxSteerAngle { get; set; } = 30f;

	protected override void OnStart()
	{
		base.OnStart();

		if ( !IsProxy )
			Local = this;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		Steer();

		switch ( DriveType )
		{
			case DriveType.FrontWheelDrive:
				FrontLeftWheel.Accelerate( 0.5f );
				FrontRightWheel.Accelerate( 0.5f );
				break;
			case DriveType.RearWheelDrive:
				BackLeftWheel.Accelerate( 0.5f );
				BackRightWheel.Accelerate( 0.5f );
				break;
			case DriveType.FourWheelDrive:
				FrontLeftWheel.Accelerate( 0.15f );
				FrontRightWheel.Accelerate( 0.15f );
				BackLeftWheel.Accelerate( 0.35f );
				BackRightWheel.Accelerate( 0.35f );
				break;
		}

		if ( Input.Down( "brake" ) )
		{
			BackLeftWheel.HandBrake( 0.5f );
			BackRightWheel.HandBrake( 0.5f );
		}
		else
		{
			BackLeftWheel.HandbrakeApplied = false;
			BackRightWheel.HandbrakeApplied = false;
		}
	}

	private void Steer()
	{
		if ( IsProxy )
			return;

		// Ackermann formula (inside wheel turns more than outside wheel)
		var phi = Input.AnalogMove.y * MaxSteerAngle;

		// Use absolutes to calculate magnitudes, then apply sign
		var absPhi = MathF.Abs( phi );
		var absPhiRad = absPhi.DegreeToRadian();
		var sinAbs = MathF.Sin( absPhiRad );
		var cosAbs = MathF.Cos( absPhiRad );

		var numerAbs = 2 * WheelBaseLength * sinAbs;
		var innerDenom = 2 * WheelBaseLength * cosAbs - TrackWidthLength * sinAbs;
		var outerDenom = 2 * WheelBaseLength * cosAbs + TrackWidthLength * sinAbs;

		var innerAbs = MathF.Atan( numerAbs / innerDenom ).RadianToDegree();
		var outerAbs = MathF.Atan( numerAbs / outerDenom ).RadianToDegree();

		var sign = MathF.Sign( phi );
		var innerAngle = innerAbs * sign;
		var outerAngle = outerAbs * sign;

		// Determine targets: assuming phi > 0 is left turn (positive yaw = left)
		// Inner wheel is left for left turn, right for right turn
		var targetLeftYaw = phi >= 0 ? innerAngle : outerAngle;
		var targetRightYaw = phi >= 0 ? outerAngle : innerAngle;

		// Calculate effective steering speed based on current velocity
		var speedFactor = (Rigidbody.Velocity.Length / MaxSpeed).Clamp( 0f, 1f );
		var effectiveness = SteeringEffectivenessCurve.Evaluate( speedFactor );

		var effectiveSteeringSpeed = SteeringSpeed * effectiveness;

		// Smoothly interpolate towards targets
		var currentLeftYaw = FrontLeftWheel.LocalRotation.Yaw();
		var newLeftYaw = currentLeftYaw.Approach( targetLeftYaw, effectiveSteeringSpeed * Time.Delta );
		FrontLeftWheel.LocalRotation = Rotation.FromYaw( newLeftYaw );

		var currentRightYaw = FrontRightWheel.LocalRotation.Yaw();
		var newRightYaw = currentRightYaw.Approach( targetRightYaw, effectiveSteeringSpeed * Time.Delta );
		FrontRightWheel.LocalRotation = Rotation.FromYaw( newRightYaw );
	}
}

public enum DriveType
{
	FrontWheelDrive,
	RearWheelDrive,
	FourWheelDrive
}
