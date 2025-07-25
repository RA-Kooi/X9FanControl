using X9FanControl;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

class CPUMonitor
{
	private readonly ILogger<Lifetime> log;
	private readonly IPMIMonitor ipmiMonitor;
	private string lsiUtil;
	private int fanSpeed = Config.CPUInitSpeed;

	public CPUMonitor(
		ILogger<Lifetime> logger,
		IPMIMonitor monitor,
		string lsi)
	{
		log = logger;
		ipmiMonitor = monitor;
		lsiUtil = lsi;
	}

	public async Task Run(CancellationToken c)
	{
		int hbaCount = await HBACount();
		bool hasHBA = hbaCount > 0;

		while(!c.IsCancellationRequested)
		{
			int hbaTemp = hasHBA ? await HBATemp(hbaCount) : 0;
			SensorData sensors = ipmiMonitor.SensorData;

			int cpuTemp = sensors.CPUTemp;
			int cpuFan = sensors.CPUZoneRPM;

			log.LogDebug($"CPU: {cpuTemp}°C\nHBA: {hbaTemp}°C");

			bool aggressiveRampUp = cpuTemp > Config.maxCPUTemp;
			if(!aggressiveRampUp)
				aggressiveRampUp = hasHBA ? hbaTemp > Config.maxHBATemp : false;

			bool rampUp = cpuTemp > Config.targetCPUTemp;
			if(!rampUp)
				rampUp = hasHBA ? hbaTemp > Config.targetHBATemp : false;

			int fanDelta = cpuFan - Config.CPUZoneTargetRPM;
			rampUp = fanDelta < 0 ? true : rampUp;

			int newSpeed = fanSpeed;
			if(rampUp)
				newSpeed += Config.fanStep * (1 + 2 * aggressiveRampUp.ToInt());
			else if(fanDelta >= Config.CPUDelta)
				newSpeed -= Config.fanStep;

			newSpeed = Math.Min(0x100, newSpeed);

			if(newSpeed != fanSpeed)
			{
				fanSpeed = newSpeed;
				int writeSpeed = fanSpeed == 0x100 ? 0xFF : fanSpeed;

				string hexSpeed = $"0x{writeSpeed:X}";

				log.LogInformation(
					$"Setting CPU Zone duty cycle to {hexSpeed} "
					+ $"({(float)writeSpeed / 2.55f}%)");
			}

			await Task.Delay(Config.taskDelay * 1000);
		}
	}

	private async Task<int> HBACount()
	{
		ProcessStartInfo pInfo = new(lsiUtil);
		pInfo.RedirectStandardOutput = true;
		pInfo.ArgumentList.Add("-s");

		Process? proc = Process.Start(pInfo);
		if(proc == null)
			throw new ApplicationException("Error executing lsiutil, not root?");

		await proc.WaitForExitAsync();

		StreamReader stdout = proc.StandardOutput;
		while(!stdout.EndOfStream)
		{
			string line = stdout.ReadLine()!;
			if(!line.EndsWith("MPT Port found"))
				continue;

			string countStr = line.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries)[0];

			int count = Int32.Parse(countStr);
			return count;
		}

		return 0;
	}

	private async Task<int> HBATemp(int hbaCount)
	{
		ProcessStartInfo pInfo = new(lsiUtil);
		pInfo.RedirectStandardOutput = true;
		pInfo.ArgumentList.Add("-p");
		pInfo.ArgumentList.Add("0");
		pInfo.ArgumentList.Add("-a");
		pInfo.ArgumentList.Add("25,2,0,0");

		int maxTemp = 0;

		for(int i = 1; i <= hbaCount; ++i)
		{
			pInfo.ArgumentList[1] = $"{i}";

			Process? proc = Process.Start(pInfo);
			if(proc == null)
				throw new ApplicationException("Error executing lsiutil");

			await proc.WaitForExitAsync();

			StreamReader stdout = proc.StandardOutput;
			while(!stdout.EndOfStream)
			{
				string line = stdout.ReadLine()!;
				line = line.Trim();

				if(!line.StartsWith("IOCTemperature:"))
					continue;

				string[] parts = line.Split(
					' ',
					StringSplitOptions.RemoveEmptyEntries);

				int temp = Convert.ToInt32(parts[1], 16);
				maxTemp = Math.Max(maxTemp, temp);
			}
		}

		return maxTemp;
	}
}
