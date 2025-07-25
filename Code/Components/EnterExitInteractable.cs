namespace SBRacer.Components;

/// <summary>
///     An interactable that locks the player into a "using" state (exit again with the use action)
/// </summary>
public abstract class EnterExitInteractable : Interactable
{
	public virtual string InteractionExitActionName { get; set; } = "use";
	public virtual bool RequiresThirdPerson { get; set; } = true;

	private TimeSince TimeSinceEntered { get; set; }

	public void EnterInteract( Player player )
	{
		if ( !CanInteract( player ) )
			return;

		OnInteract( player );
		TimeSinceEntered = 0;
	}

	public virtual bool CanExitInteraction( Player player )
	{
		return true;
	}

	public virtual void ExitInteract( Player player ) { }
}
