namespace SBRacer.Car;

[Category( "SB Racer" ), Title( "Car Controller" ), Icon( "toys" )]
public class CarController : EnterExitInteractable
{
	[Property, Category( "References" )] public Rigidbody Rigidbody { get; set; }
	[Property, Category( "References" )] public GameObject SteeringWheel { get; set; }
	[Property, Category( "References" )] public CarWheel FrontLeftWheel { get; set; }
	[Property, Category( "References" )] public CarWheel FrontRightWheel { get; set; }
	[Property, Category( "References" )] public CarWheel BackLeftWheel { get; set; }
	[Property, Category( "References" )] public CarWheel BackRightWheel { get; set; }
	[Property, Category( "References" )] public GameObject CameraLookAt { get; set; }
	[Property, Category( "References" )] public GameObject SeatedTransform { get; set; }
	[Property, Category( "References" )] public GameObject IkRightHand { get; set; }
	[Property, Category( "References" )] public GameObject IkLeftHand { get; set; }
	[Property, Category( "References" )] public GameObject IkRightFoot { get; set; }

	[Property, Category( "Movement" )] public DriveType DriveType { get; set; } = DriveType.FrontWheelDrive;
	[Property, Category( "Movement" )] public float MaxSpeed { get; set; } = 100f;
	[Property, Category( "Movement" )] public Curve TorqueCurve { get; set; }
	[Property, Category( "Movement" )] public float SteeringSpeed { get; set; } = 120f; // Max degrees per second
	[Property, Category( "Movement" )] public Curve SteeringEffectivenessCurve { get; set; }
	[Property, Category( "Movement" )] public float BrakeStrength { get; set; } = 50f;
	[Property, Category( "Movement" )] public float HandBrakeStrength { get; set; } = 100f;
	[Property, Category( "Movement" )] public float HandBrakeGripFactor { get; set; } = 0.3f;

	[Property, Category( "Suspension" )] public float SuspensionHeight { get; set; } = 30f;
	[Property, Category( "Suspension" )] public float SuspensionDamping { get; set; } = 2500f;
	[Property, Category( "Suspension" )] public float SuspensionStrength { get; set; } = 20000f;

	[Property, Category( "Gearing" )]
	public float[] GearRatios { get; set; } = { 3.5f, 2.5f, 1.8f, 1.2f, 0.8f }; // Gear 1 to 5

	[Property, Category( "Gearing" )]
	public float ReverseGearRatio { get; set; } =
		5.0f; // High torque for reverse, positive value (direction handled separately)

	[Property, Category( "Gearing" )] public float FinalDriveRatio { get; set; } = 4.0f;
	[Property, Category( "Gearing" )] public float MaxRpm { get; set; } = 6000f;
	[Property, Category( "Gearing" )] public float ShiftUpRpm { get; set; } = 4000f;
	[Property, Category( "Gearing" )] public float ShiftDownRpm { get; set; } = 2500f;

	[Property, Category( "Sounds" )] public SoundEvent StartSound { get; set; }
	[Property, Category( "Sounds" )] public SoundEvent StopSound { get; set; }
	[Property, Category( "Sounds" )] public SoundEvent EngineSound { get; set; }
	[Property, Category( "Sounds" )] public SoundEvent SkidSound { get; set; }
	[Property, Category( "Sounds" )] public SoundEvent BrakeSound { get; set; }

	public float CurrentRpm { get; private set; }
	public int CurrentGear { get; private set; } = 1;

	public float CurrentGearRatio =>
		CurrentGear >= 1 && CurrentGear <= GearRatios.Length ? GearRatios[CurrentGear - 1] :
		CurrentGear == -1 ? ReverseGearRatio : 0f;

	public float DisplaySpeed => MathF.Max( 0, Rigidbody.Velocity.Length * 0.05681818181f );

	/// <summary>
	///     The length between a front wheel and a back wheel
	/// </summary>
	private float WheelBaseLength => (FrontLeftWheel.WorldPosition - BackLeftWheel.WorldPosition).Length;

	/// <summary>
	///     The distance between opposite wheels
	/// </summary>
	private float TrackWidthLength => (FrontLeftWheel.WorldPosition - FrontRightWheel.WorldPosition).Length;

	[Property] public float MaxSteerAngle { get; set; } = 30f;

	[Sync( SyncFlags.FromHost )] public Player Owner { get; private set; }
	[Sync] public Player DrivenBy { get; private set; }

	private SoundHandle BrakeHandle { get; set; }
	private SoundHandle EngineHandle { get; set; }
	private SoundHandle SkidHandle { get; set; }
	private float SoundNormalizedRpm { get; set; }

	public override string InteractionDisplayName =>
		Owner.IsValid() ? $"Drive {Owner.Network.Owner.DisplayName}'s car" : "Drive";

	public override bool CanAltInteract( Player player )
	{
		return false;
	}

	public override bool CanExitInteraction( Player player )
	{
		return !player.Racing;
	}

	public override bool CanInteract( Player player )
	{
		return !DrivenBy.IsValid();
	}

	public override void OnInteract( Player player )
	{
		// Give network ownership of this car to the interactor
		GameObject.Network.TakeOwnershipRecursive();
		DrivenBy = player;

		player.LockMovement( true );
		player.Driving = this;

		player.GameObject.Parent = SeatedTransform;
		player.WorldTransform = SeatedTransform.WorldTransform;
		player.PlayerController.Renderer.LocalRotation = Rotation.Identity;

		if ( StartSound.IsValid() )
			StartSound.BroadcastFrom( GameObject, "Vehicles" );
	}

	public override void ExitInteract( Player player )
	{
		DrivenBy = null;

		player.Driving = null;
		player.LockMovement( false );

		player.GameObject.Parent = null;
		player.WorldTransform = FindSafeExitPoint();

		if ( StopSound.IsValid() )
			StopSound.BroadcastFrom( GameObject, "Vehicles" );

		EngineHandle?.Stop();
		EngineHandle?.Dispose();
		EngineHandle = null;
		SkidHandle?.Stop();
		SkidHandle?.Dispose();
		SkidHandle = null;
		BrakeHandle?.Stop();
		BrakeHandle?.Dispose();
		BrakeHandle = null;

		if ( Owner.IsValid() )
			GameObject.Network.AssignOwnershipRecursive( Owner.Network.Owner );
		else
			GameObject.Network.DropOwnershipRecursive();
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( Networking.IsHost )
			Owner = Network.Owner.GetPlayer();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy || !DrivenBy.IsValid() )
			return;

		// Make driver look at camera 
		var eyePos = DrivenBy.AnimationHelper.EyeWorldTransform.Position;

		var dir = (Scene.Camera.WorldPosition - eyePos).Normal;
		var dotProduct = Vector3.Dot( dir, WorldRotation.Forward );

		if ( dotProduct > -0.4f )
			DrivenBy.AnimationHelper.WithLook( dir );
		else
			DrivenBy.AnimationHelper.WithLook( WorldRotation.Forward );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( IsProxy )
			return;

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

		if ( !DrivenBy.IsValid() || (Player.Local.Racing && RaceGame.Instance.IsRaceStarting) )
		{
			EngineHandle?.Stop();
			SkidHandle?.Stop();
			BrakeHandle?.Stop();

			return;
		}

		var accelerationInput = Input.AnalogMove.x;

		UpdateGearing();

		if ( Input.Down( "Jump" ) )
		{
			BackLeftWheel.HandBrake( 0.5f );
			BackRightWheel.HandBrake( 0.5f );
		}
		else
		{
			BackLeftWheel.HandbrakeApplied = false;
			BackRightWheel.HandbrakeApplied = false;
		}

		Steer();

		// Handle sounds
		HandleSounds( accelerationInput );

		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.ScreenText( $"Gear {CurrentGear}, rpm {CurrentRpm:F0}, Speed {DisplaySpeed:F0} mph",
			Scene.Camera.PointToScreenPixels( WorldPosition ) );
	}

	private void HandleSounds( float accelerationInput )
	{
		// Engine sound
		if ( EngineSound.IsValid() )
		{
			if ( !EngineHandle.IsValid() || EngineHandle.IsStopped )
				EngineHandle = EngineSound.PlayFrom( GameObject, "Vehicles" );

			if ( EngineHandle.IsValid() )
			{
				var targetNormalizedRpm = MathF.Abs( accelerationInput ) > 0 ? CurrentRpm / MaxRpm : 0.1f;

				SoundNormalizedRpm = SoundNormalizedRpm.Approach( targetNormalizedRpm, 5f * Time.Delta );
				EngineHandle.Pitch = 0.4f + SoundNormalizedRpm * 1f;
				EngineHandle.Volume = 0.2f + MathF.Abs( accelerationInput ) * 0.5f;
			}
		}

		// Skid sound for handbrake
		var velocityLength = Rigidbody.Velocity.Length;

		if ( Input.Down( "Jump" ) && velocityLength > 10f && SkidSound != null )
		{
			if ( !SkidHandle.IsValid() || SkidHandle.IsStopped )
				SkidHandle = SkidSound.PlayFrom( GameObject, "Vehicles" );

			if ( SkidHandle.IsValid() )
				SkidHandle.Volume = (velocityLength / MaxSpeed).Clamp( 0f, 1f );
		}
		else
		{
			SkidHandle?.Stop();
			SkidHandle?.Dispose();
			SkidHandle = null;
		}

		// Brake sound
		if ( accelerationInput < 0 && velocityLength > 10f && BrakeSound != null )
		{
			if ( !BrakeHandle.IsValid() || BrakeHandle.IsStopped )
				BrakeHandle = BrakeSound.PlayFrom( GameObject, "Vehicles" );

			if ( BrakeHandle.IsValid() )
				BrakeHandle.Volume = MathF.Abs( accelerationInput ) * (velocityLength / MaxSpeed).Clamp( 0f, 1f );
		}
		else
		{
			BrakeHandle?.Stop();
			BrakeHandle?.Dispose();
			BrakeHandle = null;
		}
	}

	private float AverageDrivenWheelRadius()
	{
		var totalRadius = 0f;
		var count = 0;

		switch ( DriveType )
		{
			case DriveType.FrontWheelDrive:
				totalRadius += FrontLeftWheel.WheelRadius + FrontRightWheel.WheelRadius;
				count += 2;
				break;
			case DriveType.RearWheelDrive:
				totalRadius += BackLeftWheel.WheelRadius + BackRightWheel.WheelRadius;
				count += 2;
				break;
			case DriveType.FourWheelDrive:
				totalRadius += FrontLeftWheel.WheelRadius + FrontRightWheel.WheelRadius + BackLeftWheel.WheelRadius +
				               BackRightWheel.WheelRadius;
				count += 4;
				break;
		}

		return count > 0 ? totalRadius / count : BackLeftWheel.WheelRadius; // Fallback
	}

	private void UpdateGearing()
	{
		var carSpeed = Vector3.Dot( WorldRotation.Forward, Rigidbody.Velocity );

		var avgWheelRadius = AverageDrivenWheelRadius();
		var wheelCircumference = MathF.PI * avgWheelRadius;
		var wheelRps = MathF.Abs( carSpeed ) / wheelCircumference; // Use Abs for RPM in reverse

		CurrentRpm =
			wheelRps * MathF.Abs( CurrentGearRatio ) * FinalDriveRatio * 60; // Use Abs(ratio) to avoid negative RPM
		CurrentRpm = Math.Clamp( CurrentRpm, 0, MaxRpm );

		var accelerationInput = Input.AnalogMove.x;
		var oldGear = CurrentGear;

		if ( accelerationInput > 0 && CurrentGear > 0 )
		{
			if ( CurrentRpm > ShiftUpRpm && CurrentGear < GearRatios.Length )
			{
				CurrentGear++;
			}
			else if ( CurrentRpm < ShiftDownRpm && CurrentGear > 1 )
			{
				CurrentGear--;
			}
		}
		else if ( accelerationInput < 0 ) // Engage reverse
		{
			CurrentGear = -1;
		}
		else if ( CurrentGear == -1 && accelerationInput >= 0 ) // Exit reverse on forward input
		{
			CurrentGear = 1; // Back to first gear
		}
	}

	private void Steer()
	{
		if ( IsProxy )
			return;

		// Ackermann formula (inside wheel turns more than outside wheel)
		var phi = Input.AnalogMove.y * MaxSteerAngle;

		// Clamp phi to ensure input doesn't exceed MaxSteerAngle
		phi = phi.Clamp( -MaxSteerAngle, MaxSteerAngle );

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

		// Rotate steering wheel (roll around Z-axis)
		if ( SteeringWheel.IsValid() )
		{
			var maxSteeringWheelRoll =
				MaxSteerAngle + 30; // Max roll matches wheel limits (e.g., 30 * 10 = 300 degrees)

			var targetSteeringWheelRoll = -phi * 10f; // Multiply by 10 for visual effect, invert for natural rotation
			targetSteeringWheelRoll = targetSteeringWheelRoll.Clamp( -maxSteeringWheelRoll, maxSteeringWheelRoll );

			var currentSteeringWheelRoll = SteeringWheel.LocalRotation.Roll();
			var steeringWheelSpeed = effectiveSteeringSpeed * 2f; // Double the speed for faster rotation
			var newSteeringWheelRoll =
				currentSteeringWheelRoll.Approach( targetSteeringWheelRoll, steeringWheelSpeed * Time.Delta );

			SteeringWheel.LocalRotation = Rotation.FromPitch( SteeringWheel.LocalRotation.Pitch() ) *
			                              Rotation.FromRoll( newSteeringWheelRoll );
		}
	}

	protected Transform FindSafeExitPoint()
	{
		var playerBounds = Player.Local.PlayerController.Renderer.Bounds;

		var testDistance = 48f;
		var testPositions = new[]
		{
			WorldPosition + WorldRotation.Left * testDistance, WorldPosition + WorldRotation.Right * testDistance,
			WorldPosition + WorldRotation.Forward * testDistance,
			WorldPosition + WorldRotation.Backward * testDistance, WorldPosition + Vector3.Up * testDistance
		};

		foreach ( var testPos in testPositions )
		{
			var testTransform = new Transform( testPos + Vector3.Up * 12f, WorldRotation );

			// Check this position isn't obstructed
			var trace = Scene.Trace.Box( playerBounds, testTransform.Position, testTransform.Position )
				.WithoutTags( "player", "trigger" )
				.Run();

			if ( !trace.Hit )
			{
				// Check there's ground beneath
				var groundTrace = Scene.Trace
					.Ray( testTransform.Position, testTransform.Position + Vector3.Down * 100f )
					.WithoutTags( "player", "trigger" )
					.Run();

				// Adjust to ground level
				if ( groundTrace.Hit )
					return testTransform.WithPosition( groundTrace.HitPosition + Vector3.Up * 2f );
			}
		}

		// Fallback to original position if all else fails
		return WorldTransform.WithPosition( WorldPosition + Vector3.Up * 12f + WorldRotation.Left * 32f );
	}

	/// <summary>
	///     Clear velocity and teleport vehicle
	/// </summary>
	/// <param name="transform"></param>
	[Rpc.Owner]
	public void TeleportTo( Transform transform )
	{
		Rigidbody.Velocity = Vector3.Zero;
		WorldTransform = transform;
		Network.ClearInterpolation();

		if ( StartSound.IsValid() )
			StartSound.BroadcastFrom( GameObject, "Vehicles" );
	}
}

public enum DriveType
{
	FrontWheelDrive,
	RearWheelDrive,
	FourWheelDrive
}
