namespace X9FanControl;

static class Config
{
	// in celsius
	public const int maxCPUTemp            = 80;
	public const int targetCPUTemp         = 50;

	public const int maxHDDTemp            = 60;
	public const int targetHDDTemp         = 40;

	public const int maxHBATemp            = 60;
	public const int targetHBATemp         = 53;

	public const int taskDelay             =  5; // in seconds

	public const int fanStep               = 0x8;

	public const int HDDInitSpeed          =  64; // duty cycle passed to ipmitool
	public const int HDDZoneTargetRPM      = 825;
	public const int HDDDelta              = 100;

	public const int CPUInitSpeed          =  96; // duty cycle passed to ipmitool
	public const int CPUZoneTargetRPM      = 750;
	public const int CPUDelta              =  25;

	public const string HDDZone            = "0x10";
	public const string CPUZone            = "0x11";
	public static readonly string[] ipmiSetFanSpeed  =
	{
		"raw",
		"0x30",
		"0x91",
		"0x5A",
		"0x03"
	};

	public static readonly string[] ipmiGetFanMode =
	{
		"raw",
		"0x30",
		"0x45",
		"0x00"
	};

	public const string ipmiFanModeFull = "01";

	public static readonly string[] ipmiSetFanModeFull =
	{
		"raw",
		"0x30",
		"0x45",
		"0x01",
		"0x01"
	};
}
