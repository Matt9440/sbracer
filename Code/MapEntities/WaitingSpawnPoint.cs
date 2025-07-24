namespace SBRacer.MapEntities;

public class WaitingSpawnPoint : Component
{
	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var spawnpointModel = Model.Load( "models/editor/spawnpoint.vmdl" );

		Gizmo.Hitbox.Model( spawnpointModel );
		Gizmo.Draw.Color = Color.Orange.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );
		var so = Gizmo.Draw.Model( spawnpointModel );

		if ( so.IsValid() )
			so.Flags.CastShadows = true;
	}
}
