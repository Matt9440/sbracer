using Sandbox.Citizen;
using Sandbox.Diagnostics;

namespace SBRacer;

public class Player : Component
{
	public static Player Local { get; set; }

	[Property] public PlayerController PlayerController { get; set; }
	[Property] public Interactor Interactor { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }

	[Sync( SyncFlags.FromHost ), Change( "OnMovementLocked" )]
	public bool MovementLocked { get; set; }

	[Sync( SyncFlags.FromHost )] public int Money { get; set; }

	[Sync] public CarController Driving { get; set; }

	[Sync( SyncFlags.FromHost )] public int NextCheckpointIndex { get; set; }
	[Sync( SyncFlags.FromHost )] public int CurrentLap { get; set; } = 1;
	[Sync( SyncFlags.FromHost )] public float LapStartTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float LastCheckpointTime { get; set; }
	[Sync( SyncFlags.FromHost )] public NetList<float> LapTimes { get; set; } = new();

	/// <summary>
	///     Is this player queued to race?
	/// </summary>
	public bool Queued => RaceGame.Instance.QueuedPlayers.Contains( this );

	/// <summary>
	///     Is this player racing?
	/// </summary>
	public bool Racing => RaceGame.Instance.RacingPlayers.Contains( this );

	public Action<int, float> LapCompleted { get; set; } // Lap Count, Lap Time
	public Action<int> CheckpointPassed { get; set; } // Checkpoint Index

	public string CurrentLapTime => (Time.Now - LapStartTime).AsTimeFormatted( true );

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;

		LapCompleted += OnLapCompleted;
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

	public void OnLapCompleted( int lapIndex, float lapTime )
	{
		if ( IsProxy )
			return;

		var finalLapIndex = RaceMap.Instance.MaxLaps;
		var isFinalLap = lapIndex >= finalLapIndex;

		if ( isFinalLap )
		{
			Log.Info( $"Race completed in {LapTimes.Sum().AsTimeFormatted( true )}" );
		}
		else
		{
			Log.Info( $"Lap {lapIndex} completed in {lapTime.AsTimeFormatted( true )}" );
		}
	}

	[Rpc.Host]
	public void LockMovement( bool locked )
	{
		Assert.True( Networking.IsHost );

		MovementLocked = locked;
	}

	/// <summary>
	///     Called on all clients as a result of changes to MovementLocked
	/// </summary>
	/// <param name="wasLocked"></param>
	/// <param name="isLocked"></param>
	public void OnMovementLocked( bool wasLocked, bool isLocked )
	{
		if ( IsProxy )
			return;

		if ( isLocked )
			PlayerController.WishVelocity = Vector3.Zero;

		PlayerController.UseCameraControls = !isLocked;
		PlayerController.UseInputControls = !isLocked;
		PlayerController.UseLookControls = !isLocked;
		PlayerController.UseAnimatorControls = !isLocked;
	}
}
