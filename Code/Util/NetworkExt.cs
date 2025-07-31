namespace SBRacer.Util;

public static class NetworkExt
{
	/// <summary>
	///     Take ownership of this GameObject and all of its descendants
	/// </summary>
	/// <param name="accessor"></param>
	public static void TakeOwnershipRecursive( this GameObject.NetworkAccessor accessor )
	{
		var rootGo = accessor.RootGameObject;
		var allObjects = rootGo.GetAllObjects( true );

		rootGo.Network.TakeOwnership();

		foreach ( var descendantGo in allObjects )
		{
			if ( descendantGo.NetworkMode != NetworkMode.Object )
				continue;

			descendantGo.Network.TakeOwnership();
		}
	}

	/// <summary>
	///     Assign ownership of this GameObject and all of its descendants
	/// </summary>
	/// <param name="accessor"></param>
	/// <param name="connection"></param>
	public static void AssignOwnershipRecursive( this GameObject.NetworkAccessor accessor, Connection connection )
	{
		var rootGo = accessor.RootGameObject;
		var allObjects = rootGo.GetAllObjects( true );

		rootGo.Network.AssignOwnership( connection );

		foreach ( var descendantGo in allObjects )
		{
			if ( descendantGo.NetworkMode != NetworkMode.Object )
				continue;

			descendantGo.Network.AssignOwnership( connection );
		}
	}

	/// <summary>
	///     Drop ownership of this GameObject and all of its descendants
	/// </summary>
	/// <param name="accessor"></param>
	public static void DropOwnershipRecursive( this GameObject.NetworkAccessor accessor )
	{
		var rootGo = accessor.RootGameObject;
		var allObjects = rootGo.GetAllObjects( true );

		rootGo.Network.DropOwnership();

		foreach ( var descendantGo in allObjects )
		{
			if ( descendantGo.NetworkMode != NetworkMode.Object )
				continue;

			descendantGo.Network.DropOwnership();
		}
	}
}
