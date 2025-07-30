namespace SBRacer.MapEntities;

public class Checkpoint : Component, Component.ITriggerListener
{
	[Property] public int CheckpointIndex { get; set; } = 0;

	[Property, RequireComponent] public BoxCollider CheckpointTrigger { get; set; }

	[Property] public bool IsFinishLine { get; set; } = false;

	[Property] public GameObject NextCheckpoint { get; set; }

	/// <summary>
	///     Triggers checkpoint logic on the server only
	/// </summary>
	/// <param name="other"></param>
	public void OnTriggerEnter( Collider other )
	{
		if ( !Networking.IsHost || RaceGame.Instance.State != GameState.Racing )
			return;

		var triggeredByGo = other.GameObject;

		if ( !triggeredByGo.IsValid() )
			return;

		var hasVehicleTag = triggeredByGo.Tags.Has( "vehicle" );

		if ( !hasVehicleTag )
			return;

		var player = triggeredByGo.Components.GetInDescendantsOrSelf<Player>();

		if ( !player.IsValid() )
			return;

		HandleCheckpoint( player );
	}

	protected override void OnStart()
	{
		CheckpointTrigger.IsTrigger = true;
	}

	private void HandleCheckpoint( Player player )
	{
		if ( player.NextCheckpointIndex == CheckpointIndex )
		{
			// Record checkpoint time
			var checkpointTime = Time.Now;
			player.LastCheckpointTime = checkpointTime;

			// Increment checkpoint index
			player.NextCheckpointIndex++;

			RaceGame.Instance.OnCheckpointPassed( player, CheckpointIndex );

			if ( IsFinishLine )
			{
				// Calculate lap time
				var lapTime = checkpointTime - player.LapStartTime;

				player.LapTimes.Add( lapTime );
				player.CurrentLap++;
				player.LapStartTime = checkpointTime; // Reset for next lap
				player.NextCheckpointIndex = 0; // Reset to first checkpoint

				RaceGame.Instance.OnLapCompleted( player, player.CurrentLap - 1, lapTime );
			}
		}
		else
		{
			// Player is travelling the wrong way, tell them?
			Log.Info( "Wrong way!" );
		}
	}

	protected override void OnUpdate()
	{
		//base.OnUpdate();
		Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
		Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( CheckpointTrigger.WorldPosition, CheckpointTrigger.Scale ) );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
		Gizmo.Draw.SolidBox( BBox.FromPositionAndSize( CheckpointTrigger.WorldPosition, CheckpointTrigger.Scale ) );
	}
}
