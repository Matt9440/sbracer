namespace SBRacer.Util;

public static class ConnectionExt
{
	public static Player GetPlayer( this Connection connection )
	{
		return Game.ActiveScene.GetAll<Player>().FirstOrDefault( p => p.Network.Owner == connection );
	}
}
