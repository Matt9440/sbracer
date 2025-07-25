namespace SBRacer;

public static class InteractionPrompt
{
	public static void RenderFor( Interactable interactable, Vector3 position = default )
	{
		if ( !interactable.IsValid() )
			return;

		var camera = Game.ActiveScene.Camera;
		var hud = camera.Hud;

		var drawPosition = camera.PointToScreenPixels( position );

		// Draw item name
		var interactableName = new TextRendering.Scope( interactable.InteractionDisplayName?.ToUpper(), Color.White, 18,
			"Mogra" );
		var nameOffset = (interactableName.Measure() * 0.5f).WithY( 0 );
		hud.DrawText( interactableName, drawPosition - nameOffset );

		// Draw glyph centered below name
		var glyphSize = 26f;
		var glyphPosition = new Vector2( drawPosition.x - glyphSize * 0.5f, drawPosition.y - 32f );
		hud.DrawTexture( Input.GetGlyph( "Use", InputGlyphSize.Small, GlyphStyle.Knockout ),
			new Rect( glyphPosition.x, glyphPosition.y, glyphSize, glyphSize ) );

		// Draw alternative action if available
		var hasAlternativeAction = interactable.CanAltInteract( Player.Local );

		var progress = Player.Local.Interactor.TimeSinceUseStarted / interactable.AltInteractChargeTime;
		var isHolding = progress < 1 && Input.Down( "use" ) && Player.Local.Interactor.TimeSinceUseStarted > 0.25f;

		if ( hasAlternativeAction )
		{
			var altAction = new TextRendering.Scope( $"{interactable.AltInteractionName?.ToLower()} (hold)",
				Color.White, 12, "Poppins" );

			var altOffset = (altAction.Measure() * 0.5f).WithY( 0 );

			altAction.TextColor = isHolding ? Color.White : Color.White.WithAlpha( 0.6f );
			hud.DrawText( altAction, new Vector2( drawPosition.x, drawPosition.y + 44f ) - altOffset );

			if ( isHolding )
			{
				var barOffset = new Vector2( drawPosition.x - altAction.Measure().x / 2, drawPosition.y + 60f );

				hud.DrawRect( new Rect( barOffset.x, barOffset.y, altAction.Measure().x * progress, 1 ),
					Color.White );
			}
		}
	}
}
