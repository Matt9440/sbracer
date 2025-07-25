namespace SBRacer.MapEntities;

public class RacingSpawnPoint : Component
{
	public static List<RacingSpawnPoint> All => Game.ActiveScene.GetAll<RacingSpawnPoint>().ToList();

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var spawnpointModel = Model.Load( "models/buggy/buggy.vmdl" );

		Gizmo.Hitbox.Model( spawnpointModel );
		Gizmo.Draw.Color = Color.Orange.WithAlpha( Gizmo.IsHovered || Gizmo.IsSelected ? 0.7f : 0.5f );
		var so = Gizmo.Draw.Model( spawnpointModel );

		if ( so.IsValid() )
			so.Flags.CastShadows = true;
	}
}
