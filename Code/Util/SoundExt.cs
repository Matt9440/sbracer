namespace SBRacer.Util;

public static class SoundExt
{
	public static async Task<SoundHandle> PlayAfter( this SoundEvent sound, float delay, string targetMixer = "" )
	{
		await GameTask.DelaySeconds( delay );

		return Play( sound, targetMixer );
	}

	public static async Task<SoundHandle> PlayAfter( this SoundEvent sound, float delay, Vector3 position,
		string targetMixer = "" )
	{
		await GameTask.DelaySeconds( delay );

		return PlayFrom( sound, position, targetMixer );
	}

	public static async Task<SoundHandle> PlayAfter( this SoundEvent sound, float delay, GameObject parent,
		string targetMixer = "" )
	{
		await GameTask.DelaySeconds( delay );

		return PlayFrom( sound, parent, targetMixer );
	}

	public static SoundHandle Play( this SoundEvent sound, string targetMixer = "" )
	{
		if ( !sound.IsValid() )
		{
			return null;
		}

		var playingSound = Sound.Play( sound );

		var isTargetingMixer = !string.IsNullOrEmpty( targetMixer );

		if ( !isTargetingMixer )
		{
			return playingSound;
		}

		var mixer = Mixer.FindMixerByName( targetMixer );

		if ( mixer is null )
		{
			return playingSound;
		}

		playingSound.TargetMixer = mixer;

		return playingSound;
	}

	/// <summary>
	///     Plays a sound from a world position
	/// </summary>
	/// <param name="sound"></param>
	/// <param name="position"></param>
	/// <param name="targetMixer"></param>
	/// <returns></returns>
	public static SoundHandle PlayFrom( this SoundEvent sound, Vector3 position, string targetMixer = "" )
	{
		if ( !sound.IsValid() )
		{
			return null;
		}

		var playingSound = Sound.Play( sound, position );

		var isTargetingMixer = !string.IsNullOrEmpty( targetMixer );

		if ( !isTargetingMixer )
		{
			return playingSound;
		}

		var mixer = Mixer.FindMixerByName( targetMixer );

		if ( mixer is null )
		{
			return playingSound;
		}

		playingSound.TargetMixer = mixer;

		return playingSound;
	}

	/// <summary>
	///     Plays a sound that follows its parent
	/// </summary>
	/// <param name="sound"></param>
	/// <param name="parent"></param>
	/// <param name="targetMixer"></param>
	/// <returns></returns>
	public static SoundHandle PlayFrom( this SoundEvent sound, GameObject parent, string targetMixer = "" )
	{
		if ( !sound.IsValid() )
		{
			return null;
		}

		var playingSound = Sound.Play( sound, parent.WorldPosition );
		playingSound.Parent = parent;
		playingSound.FollowParent = true;

		var isTargetingMixer = !string.IsNullOrEmpty( targetMixer );

		if ( !isTargetingMixer )
		{
			return playingSound;
		}

		var mixer = Mixer.FindMixerByName( targetMixer );

		if ( mixer is null )
		{
			return playingSound;
		}

		playingSound.TargetMixer = mixer;

		return playingSound;
	}

	[Rpc.Broadcast]
	public static void Broadcast( this SoundEvent sound, string targetMixer = "" )
	{
		sound.Play( targetMixer );
	}

	[Rpc.Broadcast]
	public static void BroadcastFrom( this SoundEvent sound, Vector3 position, string targetMixer = "" )
	{
		sound.PlayFrom( position, targetMixer );
	}

	[Rpc.Broadcast]
	public static void BroadcastFrom( this SoundEvent sound, GameObject parent, string targetMixer = "" )
	{
		sound.PlayFrom( parent, targetMixer );
	}
}
