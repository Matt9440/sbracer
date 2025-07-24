using Sandbox.Network;

namespace SBRacer;

public sealed class RaceGame : Component, Component.INetworkListener
{
	private const float WaitingDuration = 30f;

	public static RaceGame Instance { get; set; }

	[Property] public GameObject BuggyPrefab { get; set; }

	[Sync( SyncFlags.FromHost )] public GameState State { get; private set; }

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

		var playerBuggy = BuggyPrefab.Clone( spawnPoint.WorldTransform );
		playerBuggy.NetworkSpawn( connection );
	}

	/// <summary>
	///     Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	/// <param name="connection"></param>
	public void OnDisconnected( Connection connection ) { }

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
		if ( !IsProxy )
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
	}

	private void TickRacingState()
	{
	}
}

public enum GameState
{
	Waiting,
	Racing
}
