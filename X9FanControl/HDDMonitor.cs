namespace X9FanControl;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

class HDDMonitor
{
	private readonly ILogger<Lifetime> log;
	private readonly IPMIMonitor ipmiMonitor;
	private readonly AsyncLock ipmiLock;
	private string lsScsi, hddTemp, lsiUtil, ipmiTool;

	public HDDMonitor(
		ILogger<Lifetime> logger,
		IPMIMonitor monitor,
		AsyncLock ipmiLock,
		string lsScsi,
		string hddTemp,
		string lsiUtil,
		string ipmiTool)
	{
		log = logger;
		ipmiMonitor = monitor;
		this.ipmiLock = ipmiLock;
		this.lsiUtil = lsiUtil;
		this.hddTemp = hddTemp;
		this.lsScsi = lsScsi;
		this.ipmiTool = ipmiTool;
	}

	public async Task Run(CancellationToken c)
	{
		int fanSpeed = Config.HDDInitSpeed;
		int lastTemp = -200;

		while(!c.IsCancellationRequested)
		{
			List<string> HDDs = await DiscoverHDDs();
			List<int> temps = await HDDTemps(HDDs);

			HDDs.Zip(temps).All(t =>
			{
				log.LogDebug($"{t.First}: {t.Second}°C");
				return true;
			});

			SensorData sensors = ipmiMonitor.SensorData;
			int hddTemp = temps.Max();
			int hddFan = sensors.HDDZoneRPM;

			bool isRising = hddTemp > lastTemp;
			bool aggressiveRampUp = hddTemp > Config.maxHDDTemp;
			bool rampUp = hddTemp > Config.targetHDDTemp
				&& (isRising || aggressiveRampUp);

			int fanDelta = hddFan - Config.HDDZoneTargetRPM;
			rampUp = fanDelta < 0 ? true : rampUp;

			int newSpeed = fanSpeed;
			if(rampUp)
				newSpeed += Config.fanStep * (1 + 2 * aggressiveRampUp.ToInt());
			else if(fanDelta >= Config.HDDDelta)
				newSpeed -= Config.fanStep;

			newSpeed = Math.Clamp(newSpeed, 0x0, 0x100);

			if(newSpeed != fanSpeed)
			{
				fanSpeed = newSpeed;
				int writeSpeed = fanSpeed == 0x100 ? 0xFF : fanSpeed;

				string hexSpeed = $"0x{writeSpeed:X}";

				log.LogInformation(
					$"Setting HDD Zone duty cycle to {hexSpeed} "
					+ $"({(float)writeSpeed / 2.55f}%)");

				await ipmiLock.Lock(async () =>
				{
					ProcessStartInfo pInfo = new(ipmiTool);
					Config.ipmiSetFanSpeed.All(x =>
					{
						pInfo.ArgumentList.Add(x);
						return true;
					});
					pInfo.ArgumentList.Add(Config.HDDZone);
					pInfo.ArgumentList.Add($"0x{writeSpeed:X}");

					Process? proc = Process.Start(pInfo);
					if(proc == null)
						throw new ApplicationException("Error executing ipmiTool");

					await proc.WaitForExitAsync();

					if(proc.ExitCode != 0)
						throw new ApplicationException("Error executing ipmiTool");
				});
			}

			lastTemp = hddTemp;

			await Task.Delay(Config.taskDelay * 1000);
		}
	}

	private async Task<List<string>> DiscoverHDDs()
	{
		ProcessStartInfo pInfo = new();
		pInfo.FileName = lsiUtil;
		pInfo.Arguments = "-s";
		pInfo.RedirectStandardOutput = true;

		Process? proc = Process.Start(pInfo);
		if(proc == null)
			throw new ApplicationException("Error executing lsiutil, not root?");

		await proc.WaitForExitAsync();

		if(proc!.ExitCode != 0)
			throw new ApplicationException("Error executing lsiutil, not root?");

		List<string> disks = new(), SASAddrs = new();

		StreamReader stdio = proc.StandardOutput;
		while(!stdio.EndOfStream)
		{
			string line = stdio.ReadLine()!;
			if(!line.Contains("Disk"))
				continue;

			string[] parts = line.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries);

			int idx = parts.Length - 2;
			if(idx <= 6)
				throw new ApplicationException("Unable to parse output");

			SASAddrs.Add(parts[idx]);
		}

		pInfo.FileName = lsScsi;
		pInfo.Arguments = "-t";

		proc = Process.Start(pInfo);
		if(proc == null)
			throw new ApplicationException("Error executing lsscsi");

		await proc.WaitForExitAsync();

		stdio = proc.StandardOutput;
		while(!stdio.EndOfStream)
		{
			string line = stdio.ReadLine()!;
			if(!SASAddrs.Where(a => line.Contains(a)).Any())
				continue;

			string[] parts = line.Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries);

			disks.Add(parts[3]);
		}

		return disks;
	}

	private async Task<List<int>> HDDTemps(List<string> hdds)
	{
		ProcessStartInfo pInfo = new(hddTemp);
		pInfo.RedirectStandardOutput = true;
		hdds.ForEach(x => pInfo.ArgumentList.Add(x));

		Process? pHT = Process.Start(pInfo);
		if(pHT == null)
			throw new ApplicationException("Error executing hddtemp");

		await pHT.WaitForExitAsync();

		List<int> temps = new(hdds.Count);

		StreamReader stdout = pHT.StandardOutput;
		while(!stdout.EndOfStream)
		{
			string line = stdout.ReadLine()!;
			string tempStr = line
				.Split(' ', StringSplitOptions.RemoveEmptyEntries)
				.Last()
				.Split('°')[0];

			int temp = Int32.Parse(tempStr);
			temps.Add(temp);
		}

		return temps;
	}
}
