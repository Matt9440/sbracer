namespace SBRacer.Util;

public static class FloatExt
{
	public static string AsTimeFormatted( this float value, bool clampToZero = false )
	{
		var totalSeconds = (int)value;
		var minutes = totalSeconds / 60;
		var seconds = totalSeconds % 60;

		if ( value > 0 )
		{
			return $"{minutes:00}:{seconds:00}";
		}

		return "00:00";
	}
}
