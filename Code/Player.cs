using Sandbox.Diagnostics;

namespace SBRacer;

public class Player : Component
{
	public static Player Local { get; set; }

	[Property] public PlayerController PlayerController { get; set; }
	[Property] public Interactor Interactor { get; set; }

	[Sync( SyncFlags.FromHost )] public bool MovementLocked { get; set; }
	[Sync( SyncFlags.FromHost )] public int Money { get; set; }

	/// <summary>
	///     Is this player queued to race?
	/// </summary>
	public bool Queued => RaceGame.Instance.QueuedPlayers.Contains( this );

	/// <summary>
	///     Is this player racing?
	/// </summary>
	public bool Racing => RaceGame.Instance.RacingPlayers.Contains( this );

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( RaceGame.Instance.State != GameState.Waiting )
			return;

		if ( Input.Released( "reload" ) )
			RaceGame.Instance.QueuePlayer( this, !Queued );
	}

	[Rpc.Host]
	public void LockMovement( bool locked )
	{
		Assert.True( Networking.IsHost );

		MovementLocked = locked;
	}
}
