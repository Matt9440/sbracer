namespace SBRacer.Garage;

public class CarPlatform : Component
{
	[Property] public Rigidbody PlatformRigidbody { get; set; }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		var input = Input.AnalogMove;
		PlatformRigidbody.AngularVelocity = new(0,0, -input.y * 1);
	}
}
