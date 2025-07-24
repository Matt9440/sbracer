namespace SBRacer.MapEntities;

public class RaceMap : Component
{
	public static RaceMap Instance { get; set; }

	[Property] public string MapName { get; set; } = "SBRacer Map";

	[Property] private MapType Type { get; set; } = MapType.Race;

	/// <summary>
	///     How many laps should the players race for?
	/// </summary>
	[Property, ShowIf( nameof(Type), MapType.Race )]
	public int MaxLaps { get; set; } = 1;

	protected override void OnAwake()
	{
		Instance = this;
	}

	public WaitingSpawnPoint GetRandomWaitingSpawnPoint()
	{
		var waitingSpawns = Scene.GetAll<WaitingSpawnPoint>()?.ToList();

		if ( waitingSpawns is null || waitingSpawns.Count == 0 )
		{
			Log.Error( "Unable to find any waiting spawn points!" );
			return null;
		}

		var randomSpawn = Game.Random.FromList( waitingSpawns );

		if ( !randomSpawn.IsValid() )
		{
			Log.Error( "GetRandomWaitingSpawn returned an invalid spawn point." );
			return null;
		}

		return randomSpawn;
	}

	public RacingSpawnPoint GetRandomRacingSpawnPoint()
	{
		var racingSpawns = Scene.GetAll<RacingSpawnPoint>()?.ToList();

		if ( racingSpawns is null || racingSpawns.Count == 0 )
		{
			Log.Error( "Unable to find any racing spawn points!" );
			return null;
		}

		var randomSpawn = Game.Random.FromList( racingSpawns );

		if ( !randomSpawn.IsValid() )
		{
			Log.Error( "GetRandomRacingSpawn returned an invalid spawn point." );
			return null;
		}

		return randomSpawn;
	}
}

public enum MapType
{
	Race,
	DemolitionDerby
}
