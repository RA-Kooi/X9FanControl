namespace X9FanControl;

public static class BoolExtensions
{
	public static int ToInt(this bool value)
	{
		return value ? 1 : 0;
	}
}
