using Sandbox.Diagnostics;

namespace SBRacer;

public sealed class RaceGame : Component, Component.INetworkListener
{
	public const float WaitingDuration = 10f;
	public const float RaceCountdownDuration = 3f;

	public static RaceGame Instance { get; set; }

	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject BuggyPrefab { get; set; }

	[Sync( SyncFlags.FromHost ), Change] public GameState State { get; private set; }

	/// <summary>
	///     How long have we been playing this map?
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public TimeSince TimeSinceGameStarted { get; set; }

	/// <summary>
	///     How long has the current state been running for?
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public TimeSince TimeSinceStateStarted { get; set; }

	[Sync( SyncFlags.FromHost )] public NetList<Player> QueuedPlayers { get; set; } = new();
	[Sync( SyncFlags.FromHost )] public NetList<Player> RacingPlayers { get; set; } = new();

	/// <summary>
	///     The race countdown is showing (3.. 2.. 1..)
	/// </summary>
	public bool IsRaceStarting => State == GameState.Racing && TimeSinceStateStarted < RaceCountdownDuration;

	public Action OnWaitingStarted { get; set; }
	public Action OnRacingStarted { get; set; }

	public bool AcceptConnection( Connection channel, ref string reason )
	{
		reason = "ily";

		return true;
	}

	/// <summary>
	///     A client is fully connected to the server. This is called on the host.
	/// </summary>
	/// <param name="connection"></param>
	public void OnActive( Connection connection )
	{
		if ( !BuggyPrefab.IsValid() )
			return;

		var spawnPoint = RaceMap.Instance.GetRandomWaitingSpawnPoint();

		var playerGo = PlayerPrefab.Clone( spawnPoint.WorldTransform );
		var player = playerGo.GetComponent<Player>();

		playerGo.NetworkSpawn( connection );
	}

	/// <summary>
	///     Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	/// <param name="connection"></param>
	public void OnDisconnected( Connection connection )
	{
		var player = connection.GetPlayer();

		// Remove players from queues when they leave.
		if ( player.IsValid() )
		{
			QueuedPlayers.Remove( player );
			RacingPlayers.Remove( player );
		}
	}

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor )
			return;

		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";

			await Task.DelayRealtimeSeconds( 0.1f );

			Networking.CreateLobby( new LobbyConfig() );
		}
	}

	protected override void OnStart()
	{
		Instance = this;

		if ( !Networking.IsHost )
			return;

		TimeSinceGameStarted = 0;
		SetState( GameState.Waiting );
	}

	private void SetState( GameState newState )
	{
		State = newState;
		TimeSinceStateStarted = 0;

		Log.Info( $"State changed to {newState}" );
	}

	/// <summary>
	///     Invoked on host and all clients whenever the game state changes
	/// </summary>
	/// <param name="oldState"></param>
	/// <param name="newState"></param>
	private void OnStateChanged( GameState oldState, GameState newState )
	{
		if ( newState == GameState.Waiting )
			OnWaitingStarted?.Invoke();

		if ( newState == GameState.Racing )
			OnRacingStarted?.Invoke();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( State == GameState.Waiting )
			TickWaitingState();

		if ( State == GameState.Racing )
			TickRacingState();
	}

	private void TickWaitingState()
	{
		// Transition from waiting state to play state
		if ( TimeSinceStateStarted < WaitingDuration )
			return;

		// Cleanup racing players from last round
		RacingPlayers.Clear();

		// All queued players are now racing players
		foreach ( var player in QueuedPlayers )
			RacingPlayers.Add( player );

		// Clear queued players
		QueuedPlayers.Clear();

		// Move players to racing spawn points.
		var spawnPoints = RacingSpawnPoint.All.Take( RacingPlayers.Count );

		for ( var p = 0; p < RacingPlayers.Count; p++ )
		{
			var player = RacingPlayers[p];
			var spawnPoint = spawnPoints.ElementAt( p );

			if ( player.Driving.IsValid() )
			{
				player.Driving.TeleportTo( spawnPoint.WorldTransform );
			}
			else
			{
				player.WorldTransform = spawnPoint.WorldTransform;
			}
		}

		SetState( GameState.Racing );
	}

	private void TickRacingState()
	{
		// Clear lap start times whilst IsRaceStarting
		if ( IsRaceStarting )
		{
			foreach ( var player in RacingPlayers )
				player.LapStartTime = Time.Now;
		}

		// Nobody is racing, revert to waiting state.
		if ( RacingPlayers.Count == 0 )
			SetState( GameState.Waiting );
	}

	[Rpc.Host]
	public void QueuePlayer( Player player, bool queued )
	{
		Assert.True( Networking.IsHost );

		if ( queued )
			QueuedPlayers.Add( player );
		else
			QueuedPlayers.Remove( player );
	}
}

public enum GameState
{
	Waiting,
	Racing
}
