namespace SBRacer.Car;

[Category( "SB Racer" ), Title( "Orbit Camera" ), Icon( "camera_alt" )]
public sealed class OrbitCamera : Component
{
	private Angles _lookDir;
	[Group( "Following" ), Property] public float Distance { get; set; } = 256f;
	[Group( "Following" ), Property] public float Height { get; set; } = 32f;

	[Group( "Sensitivity" ), Property] public float LookSensitivity { get; set; } = 5.0f;
	[Group( "Sensitivity" ), Property] public float PositionSensitivity { get; set; } = 5.0f;
	[Group( "Sensitivity" ), Property] public float RotationSensitivity { get; set; } = 5.0f;

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor )
		{
			return;
		}

		if ( !CarController.Local.IsValid() )
		{
			return;
		}

		var target = CarController.Local.CameraLookAt;

		// Analog look
		var analogLook = Input.AnalogLook;

		if ( Input.UsingController )
		{
			var theta = MathF.Atan2( -analogLook.yaw, analogLook.pitch );
			var angleDeg = theta.RadianToDegree();
			var targetDir = new Angles( 0, angleDeg, 0 );
			_lookDir = _lookDir.LerpTo( targetDir, LookSensitivity * Time.Delta );
		}
		else
		{
			_lookDir += analogLook;
		}


		// Follow camera
		var offset = _lookDir.ToRotation().Backward * Distance * target.Transform.World.Rotation;
		offset += Vector3.Up * Height;

		var targetPosition = target.WorldPosition + offset;
		var targetRotation = Rotation.LookAt( target.WorldPosition - targetPosition ).Angles();

		WorldPosition = Vector3.Lerp( WorldPosition, targetPosition, PositionSensitivity * Time.Delta );
		WorldRotation = Rotation.Lerp( WorldRotation, targetRotation, RotationSensitivity * Time.Delta );
	}
}
