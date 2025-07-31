namespace SBRacer.Components;

public sealed class Interactor : Component
{
	[Property] public float InteractReach { get; set; } = 280f;
	[Property] public float UseCooldown { get; set; } = 1f;

	public Interactable LastInteractable { get; private set; }
	public EnterExitInteractable InteractingWith { get; private set; }

	private RealTimeSince TimeSinceLastInteract { get; set; }

	/// <summary>
	///     The time since the player first pressed the use key
	/// </summary>
	public RealTimeSince TimeSinceUseStarted { get; set; }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( InteractingWith.IsValid() )
		{
			// Manual exit
			if ( Input.Released( "use" ) && InteractingWith.CanExitInteraction( Player.Local ) )
			{
				ExitInteractable();
			}

			return;
		}

		var lookDirection = Scene.Camera.WorldRotation.Forward;
		SceneTraceResult interactTrace;

		if ( !Player.Local.PlayerController.ThirdPerson )
		{
			var traceFrom = Scene.Camera.WorldPosition;
			var traceTo = traceFrom + lookDirection * InteractReach;

			interactTrace = Scene.Trace.Ray( traceFrom, traceTo ).IgnoreGameObjectHierarchy( GameObject ).Run();
		}
		else
		{
			var traceFrom = Player.Local.PlayerController.EyePosition;
			var traceTo = traceFrom + lookDirection * InteractReach;

			interactTrace = Scene.Trace.Sphere( 12f, traceFrom, traceTo ).IgnoreGameObjectHierarchy( GameObject ).Run();
		}

		// Gizmo.Draw.Line( interactTrace.StartPosition, interactTrace.EndPosition );

		var hasInteractTag = interactTrace.Tags.Contains( "interactable" );

		if ( !interactTrace.Hit || !interactTrace.GameObject.IsValid() || !hasInteractTag )
		{
			ClearLastInteractable();

			return;
		}

		var interactable = interactTrace.GameObject.Components.GetInChildrenOrSelf<Interactable>();

		var canInteract = interactable.CanInteract( Player.Local ) && TimeSinceLastInteract > UseCooldown;
		var canAltInteract = interactable.CanAltInteract( Player.Local );

		if ( !interactable.IsValid() || (!canInteract && !canAltInteract) )
		{
			ClearLastInteractable();

			return;
		}

		if ( LastInteractable != interactable )
		{
			LastInteractable?.OnHoverEnd( Player.Local );

			LastInteractable = interactable;
			LastInteractable.OnHover( Player.Local );
		}

		InteractionPrompt.RenderFor( interactable, interactTrace.HitPosition );

		if ( Input.Pressed( "use" ) )
			TimeSinceUseStarted = 0;

		if ( Input.Down( "use" ) && canAltInteract && TimeSinceUseStarted > interactable.AltInteractChargeTime )
		{
			interactable.AltInteract( Player.Local );
			Input.ReleaseAction( "use" );

			TimeSinceLastInteract = 0;

			return;
		}

		if ( Input.Released( "use" ) )
		{
			if ( !canInteract || TimeSinceUseStarted > 0.25f )
				return;

			if ( interactable is EnterExitInteractable enterExitInteractable )
			{
				InteractingWith = enterExitInteractable;
				enterExitInteractable.EnterInteract( Player.Local );
				ClearLastInteractable();
			}
			else
			{
				interactable.Interact( Player.Local );
			}

			TimeSinceLastInteract = 0;
		}
	}

	private void ClearLastInteractable()
	{
		InteractionPrompt.RenderFor( null );

		LastInteractable?.OnHoverEnd( Player.Local );
		LastInteractable = null;

		TimeSinceUseStarted = 0;
	}

	/// <summary>
	///     Exits the EnterExitInteractable we're currently interacting with.
	/// </summary>
	private void ExitInteractable()
	{
		if ( !InteractingWith.IsValid() )
			return;

		InteractingWith.ExitInteract( Player.Local );
		InteractingWith = null;
	}

	protected override void OnDisabled()
	{
		ClearLastInteractable();
	}
}
