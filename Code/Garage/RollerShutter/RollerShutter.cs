namespace SBRacer.Garage;

public class RollerShutter : Component
{
	[Property] public GameObject ShutterGameObject { get; set; }
	[Property] public BoxCollider EntranceTrigger { get; set; }
	[Property] public float OpenSpeed { get; set; } = 2f;
    
	private int VehiclesInEntranceTrigger { get; set; } = 0;
	
	private Vector3 InitialPosition { get; set; }
	private Rotation InitialRotation { get; set; }

	protected override void OnStart()
	{
		if ( !Networking.IsHost )
			return;

		EntranceTrigger.OnObjectTriggerEnter += OnEntranceTriggerEnter;
		EntranceTrigger.OnObjectTriggerExit += OnEntranceTriggerExit;

		InitialPosition = ShutterGameObject.WorldPosition;
		InitialRotation = ShutterGameObject.WorldRotation;
	}

	public void OnEntranceTriggerEnter( GameObject gameObject )
	{
		var car = gameObject.Components.GetInDescendantsOrSelf<CarController>();

		if ( !car.IsValid() )
			return;
       
		VehiclesInEntranceTrigger++;
	}

	public void OnEntranceTriggerExit( GameObject gameObject )
	{
		var car = gameObject.Components.GetInDescendantsOrSelf<CarController>();

		if ( !car.IsValid() )
			return;
       
		VehiclesInEntranceTrigger--;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		float target = VehiclesInEntranceTrigger > 0 ? 1f : 0f;

		var targetAngle = InitialRotation * (Rotation.FromRoll( 90 ) * target);
		var targetPosition = InitialPosition + InitialRotation.Down * 110 * target;
		
		ShutterGameObject.WorldRotation = ShutterGameObject.WorldRotation.LerpTo( targetAngle, Time.Delta * OpenSpeed );
		ShutterGameObject.WorldPosition = ShutterGameObject.WorldPosition.LerpTo( targetPosition, Time.Delta * OpenSpeed );
	}
}
