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
	///     This list contains players that were racing but now aren't. This could be because they've won, or died.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public NetList<Player> RemovedPlayers { get; set; } = new();

	/// <summary>
	///     The race countdown is showing (3.. 2.. 1..)
	/// </summary>
	public bool IsRaceStarting => State == GameState.RaceCountdown;

	public Action OnWaitingStarted { get; set; }
	public Action OnRaceCountdownStarted { get; set; }
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
			RemovedPlayers.Remove( player );
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

		if ( newState == GameState.RaceCountdown )
		{
			// Cleanup racing players from last round
			RacingPlayers.Clear();
			RemovedPlayers.Clear();

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
				player.LapTimes.Clear();
				player.CurrentLap = 1;

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
		}

		if ( newState == GameState.Racing )
		{
			foreach ( var player in RacingPlayers )
			{
				// Set LapStartTime to the time of the racing state start
				player.LapStartTime = Time.Now;
			}
		}

		Log.Info( $"State changed to {newState}" );
	}

	/// <summary>
	///     Invoked on host and all clients whenever the game state changes
	/// </summary>
	/// <param name="_"></param>
	/// <param name="newState"></param>
	private void OnStateChanged( GameState _, GameState newState )
	{
		if ( newState == GameState.Waiting )
			OnWaitingStarted?.Invoke();

		if ( newState == GameState.RaceCountdown )
			OnRaceCountdownStarted?.Invoke();

		if ( newState == GameState.Racing )
			OnRacingStarted?.Invoke();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( State == GameState.Waiting )
			TickWaitingState();

		if ( State == GameState.RaceCountdown )
			TickRaceCountdownState();

		if ( State == GameState.Racing )
			TickRacingState();
	}

	private void TickWaitingState()
	{
		if ( TimeSinceStateStarted < WaitingDuration )
			return;

		if ( QueuedPlayers.Any() )
			SetState( GameState.RaceCountdown );
		else
			SetState( GameState.Waiting );
	}

	private void TickRaceCountdownState()
	{
		if ( TimeSinceStateStarted < RaceCountdownDuration )
			return;

		SetState( GameState.Racing );
	}

	private void TickRacingState()
	{
		// End game logic here, right now the below logic ends without a reason because when a player finishes the course
		// (me in singleplayer) there are no more racing players.

		// Nobody is racing, revert to waiting state.
		if ( RacingPlayers.Count == 0 )
			SetState( GameState.Waiting );
	}

	private async Task OnPlayerCompletedRace( Player player )
	{
		Assert.True( Networking.IsHost );

		if ( !player.IsValid() )
			return;

		Log.Info( $"{player.Network.Owner.DisplayName}: Race completed in {player.TotalRaceTime}" );

		RacingPlayers.Remove( player );
		RemovedPlayers.Add( player );

		// Wait a couple seconds and then teleport the player away
		await GameTask.DelaySeconds( 2f );

		player.Driving.TeleportTo( RaceMap.Instance.GetRandomWaitingSpawnPoint().WorldTransform );
	}

	/// <summary>
	///     Let all clients know a player has completed a lap
	/// </summary>
	/// <param name="player"></param>
	/// <param name="lapIndex"></param>
	/// <param name="lapTime"></param>
	[Rpc.Broadcast]
	public void OnLapCompleted( Player player, int lapIndex, float lapTime )
	{
		if ( !player.IsValid() )
			return;

		player.LapCompleted?.Invoke( lapIndex, lapTime );

		if ( !Networking.IsHost )
			return;

		var finalLapIndex = RaceMap.Instance.MaxLaps;
		var isFinalLap = lapIndex >= finalLapIndex;

		if ( isFinalLap )
		{
			_ = OnPlayerCompletedRace( player );
		}
		else
		{
			Log.Info(
				$"{player.Network.Owner.DisplayName}: Lap {lapIndex} completed in {lapTime.AsTimeFormatted( true )}" );
		}
	}

	/// <summary>
	///     Let all clients know a player has passed a checkpoint
	/// </summary>
	/// <param name="player"></param>
	/// <param name="checkpointIndex"></param>
	[Rpc.Broadcast]
	public void OnCheckpointPassed( Player player, int checkpointIndex )
	{
		if ( !player.IsValid() )
			return;

		player.CheckpointPassed?.Invoke( checkpointIndex );
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
	RaceCountdown,
	Racing
}
