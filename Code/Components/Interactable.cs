namespace SBRacer.Components;

public abstract class Interactable : Component
{
	/// <summary>
	///     The color of the highlight outline when the player hovers over the interactable
	/// </summary>
	[Property]
	public virtual Color HighlightColor { get; set; } = Color.White;

	public virtual string AltInteractionName { get; set; } = "hold use";

	[Property] public virtual string InteractionDisplayName { get; set; } = "interactable";

	public virtual float AltInteractChargeTime { get; set; } = 1f;
	public virtual string InteractionActionName { get; set; } = "use";

	public void OnHover( Player player )
	{
		Highlight( true );
	}

	public void OnHoverEnd( Player player )
	{
		Highlight( false );
	}

	/// <summary>
	///     Whether the player can interact with this by pressing e
	/// </summary>
	/// <param name="pawn"></param>
	/// <returns></returns>
	public virtual bool CanInteract( Player player )
	{
		return true;
	}

	public void Interact( Player player )
	{
		if ( !CanInteract( player ) )
			return;

		OnInteract( player );
	}

	public virtual bool CanAltInteract( Player player )
	{
		return false;
	}

	public void AltInteract( Player player )
	{
		if ( !CanAltInteract( player ) )
			return;

		OnAltInteract( player );
	}

	public virtual void OnAltInteract( Player player ) { }

	protected void Highlight( bool shouldHighlight )
	{
		var highlight = Components.GetOrCreate<HighlightOutline>();

		if ( !highlight.IsValid() )
			return;

		highlight.Enabled = shouldHighlight;

		if ( !shouldHighlight )
			return;

		highlight.Color = HighlightColor;
		highlight.ObscuredColor = Color.White.WithAlpha( 0 );
		highlight.Width = 0.5f;
	}

	public virtual void OnInteract( Player pawn ) { }

	protected override void OnStart()
	{
		Tags.Add( "interactable" );

		// Clear any highlights for late joiners
		if ( IsProxy )
		{
			var highlight = Components.GetInChildren<HighlightOutline>();

			if ( highlight.IsValid() )
				highlight.Enabled = false;
		}
	}
}
