using Sandbox.Citizen;

namespace SBRacer;

public class Player : Component
{
	public static Player Local { get; set; }

	[Property] public PlayerController PlayerController { get; set; }
	[Property] public Interactor Interactor { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }

	[Sync( SyncFlags.FromHost )] public bool MovementLocked { get; set; }

	[Sync( SyncFlags.FromHost )] public int Money { get; set; }

	[Sync, Change] public CarController Driving { get; set; }

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

	public string TotalRaceTime => Racing
		? (Time.Now - LapStartTime + LapTimes.Sum()).AsTimeFormatted( true )
		: LapTimes.Sum().AsTimeFormatted( true );

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

	[Rpc.Broadcast]
	public void LockMovement( bool locked )
	{
		if ( !IsProxy )
		{
			if ( locked )
				PlayerController.WishVelocity = Vector3.Zero;

			PlayerController.UseCameraControls = !locked;
			PlayerController.UseInputControls = !locked;
			PlayerController.UseLookControls = !locked;
		}

		if ( Networking.IsHost )
			MovementLocked = locked;

		PlayerController.UseAnimatorControls = !locked;
	}

	/// <summary>
	///     Called on all clients when the Driving property changes
	/// </summary>
	/// <param name="_"></param>
	/// <param name="driving"></param>
	public void OnDrivingChanged( CarController _, CarController driving )
	{
		if ( driving.IsValid() )
		{
			AnimationHelper.Sitting = CitizenAnimationHelper.SittingStyle.Chair;
			AnimationHelper.SittingOffsetHeight = -4.304f;

			AnimationHelper.IkRightHand = driving.IkRightHand;
			AnimationHelper.IkLeftHand = driving.IkLeftHand;
			AnimationHelper.IkRightFoot = driving.IkRightFoot;

			Tags.Add( "no_collide" );
		}
		else
		{
			AnimationHelper.Sitting = CitizenAnimationHelper.SittingStyle.None;

			AnimationHelper.IkRightHand = null;
			AnimationHelper.IkLeftHand = null;
			AnimationHelper.IkRightFoot = null;

			Tags.Remove( "no_collide" );
		}
	}
}
