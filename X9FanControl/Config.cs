namespace X9FanControl;

static class Config
{
	// in celsius
	public const int maxCPUTemp            = 80;
	public const int minCPUTemp            = -5;

	public const int maxHDDTemp            = 60;
	public const int minHDDTemp            =  5;
	public const int targetHDDTemp         = 45;

	public const int maxHBATemp            = 55;
	public const int minHBATemp            =  0;

	public const int taskDelay             =  5; // in seconds

	public const string ipmiSetFanSpeedPre = "raw 0x30 0x91 0x5A 0x03";
	public const string HDDZone            = "0x10";
	public const string CPUZone            = "0x11";
	public const int fanStep               = 0x8;

	public const int HDDZoneTargetRPM      = 825;
	public const int CPUZoneTargetRPM      = 800;
}
