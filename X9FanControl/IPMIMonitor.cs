namespace X9FanControl;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

class SensorData
{
	public int CPUTemp;
	public int CPUZoneRPM;
	public int HDDZoneRPM;
}

class IPMIMonitor
{
	private readonly ILogger<Lifetime> log;
	private readonly AsyncLock ipmiLock;
	private string ipmiTool;
	private SensorData sensorData;

	public SensorData SensorData
	{
		get
		{
			Task<SensorData> t = ipmiLock.Lock<SensorData>(() =>
			{
				return Task.FromResult(sensorData);
			});

			t.Wait();
			return t.Result;
		}
	}

	public IPMIMonitor(
		ILogger<Lifetime> logger,
		AsyncLock ipmiLock,
		string ipmiTool)
	{
		log = logger;
		this.ipmiLock = ipmiLock;
		this.ipmiTool = ipmiTool;
		this.sensorData = new();
	}

	public async Task Run(CancellationToken c)
	{
		ProcessStartInfo pInfo = new(ipmiTool);
		pInfo.ArgumentList.Add("-c");
		pInfo.ArgumentList.Add("sensor");
		pInfo.RedirectStandardOutput = true;

		Process? pIT = null;
		await ipmiLock.Lock(async () =>
		{
			pIT = Process.Start(pInfo);
			if(pIT == null)
				throw new ApplicationException("Error executing ipmitool, not root?");

			await pIT.WaitForExitAsync();
		});

		if(pIT!.ExitCode != 0)
			throw new ApplicationException("Error executing ipmitool, not root?");

		List<string> knownFans = new(), knownCPUs = new();

		StreamReader stdout = pIT!.StandardOutput;
		while(!stdout.EndOfStream)
		{
			string line = stdout.ReadLine()!;
			string[] parts = line.Split(
				',',
				StringSplitOptions.RemoveEmptyEntries);

			if(parts[0].StartsWith("FAN") && parts[1] != "na")
				knownFans.Add(parts[0]);
			else if(parts[0].StartsWith("CPU")
					&& parts[0].EndsWith(" Temp")
					&& parts[1] != "na")
				knownCPUs.Add(parts[0]);
		}

		pInfo.ArgumentList.Clear();
		pInfo.ArgumentList.Add("-c");
		pInfo.ArgumentList.Add("sensor");
		pInfo.ArgumentList.Add("reading");

		foreach(string fan in knownFans)
			pInfo.ArgumentList.Add(fan);

		foreach(string cpu in knownCPUs)
			pInfo.ArgumentList.Add(cpu);

		List<string> hddFans = knownFans
			.Where(x => char.IsDigit(x.Last()))
			.ToList();

		List<string> cpuFans = knownFans
			.Where(x => char.IsAsciiLetter(x.Last()))
			.ToList();

		while(!c.IsCancellationRequested)
		{
			await ipmiLock.Lock(async () =>
			{
				pIT = Process.Start(pInfo);
				if(pIT == null)
					throw new ApplicationException("Error executing ipmitool");

				await pIT.WaitForExitAsync();

				int avgHdd = 0, avgCPU = 0, maxTemp = 0;

				stdout = pIT!.StandardOutput;
				while(!stdout.EndOfStream)
				{
					string line = stdout.ReadLine()!;
					string[] parts = line.Split(
						',',
						StringSplitOptions.RemoveEmptyEntries);

					bool isHdd = false, isCPU = false;
					if(hddFans.Contains(parts[0]))
						isHdd = true;
					else if(cpuFans.Contains(parts[0]))
						isCPU = true;

					int speed = Int32.Parse(parts[1]);
					avgHdd += isHdd ? speed : 0;
					avgCPU += isCPU ? speed : 0;
					maxTemp = Math.Max(maxTemp, (!isHdd && !isCPU) ? speed : 0);
				}

				sensorData.CPUTemp = maxTemp;
				sensorData.CPUZoneRPM = avgCPU / cpuFans.Count;
				sensorData.HDDZoneRPM = avgHdd / hddFans.Count;
			});

			SensorData data = this.SensorData;
			log.LogDebug($"{data.CPUTemp} {data.CPUZoneRPM} {data.HDDZoneRPM}");

			await Task.Delay(Config.taskDelay * 1000);
		}
	}
}
