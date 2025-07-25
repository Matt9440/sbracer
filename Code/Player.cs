namespace SBRacer;

public class Player : Component
{
	public static Player Local { get; set; }

	[Sync( SyncFlags.FromHost )] public int Money { get; set; }

	public bool Queued => RaceGame.Instance.QueuedPlayers.Contains( this );
	public bool Racing => RaceGame.Instance.RacingPlayers.Contains( this );

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;
	}
}
