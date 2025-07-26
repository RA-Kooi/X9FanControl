namespace X9FanControl;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

class LifetimeConfig
{
	public required string lsScsi, hddTemp, lsiUtil, ipmiTool;
}

class Lifetime: IHostedService, IDisposable
{
	private readonly ILogger<Lifetime> log;
	private readonly IHostApplicationLifetime appLifetime;
	private LifetimeConfig config;
	private CancellationTokenSource? cancelSource;
	private Task? sensorTask, hddTask, cpuTask;

	public Lifetime(
		ILogger<Lifetime> logger,
		IHostApplicationLifetime appLifetime,
		IOptions<LifetimeConfig> options)
	{
		log = logger;
		this.appLifetime = appLifetime;
		this.config = options.Value;
		cancelSource = null;
		sensorTask = null;
		hddTask = null;
		cpuTask = null;
	}

	public Task StartAsync(CancellationToken _)
	{
		Action<Task> OnError = (_) =>
		{
			appLifetime.StopApplication();
		};

		appLifetime.ApplicationStarted.Register(() =>
		{
			log.LogInformation("Starting...");

			Func<ProcessStartInfo, Func<string, bool>> addArgs = p =>
			{
				return x =>
				{
					p.ArgumentList.Add(x);
					return true;
				};
			};

			ProcessStartInfo pInfo = new(config.ipmiTool);
			pInfo.RedirectStandardOutput = true;
			Config.ipmiGetFanMode.All(addArgs(pInfo));

			Process? proc = Process.Start(pInfo);
			if(proc == null)
				throw new ApplicationException("Error executing ipmitool, not root?");

			proc.WaitForExit();

			if(proc.ExitCode != 0)
				throw new ApplicationException("Error executing ipmitool, not root?");

			if(proc.StandardOutput.ReadLine()!.Trim() != Config.ipmiFanModeFull)
			{
				pInfo.ArgumentList.Clear();
				Config.ipmiSetFanModeFull.All(addArgs(pInfo));

				log.LogInformation("Setting fan mode to full");

				proc = Process.Start(pInfo);
				if(proc == null)
					throw new ApplicationException("Error executing ipmitool");

				proc.WaitForExit();

				if(proc.ExitCode != 0)
					throw new ApplicationException("Error executing ipmitool");
			}

			pInfo.ArgumentList.Clear();
			Config.ipmiSetFanSpeed.All(addArgs(pInfo));
			pInfo.ArgumentList.Add("");
			pInfo.ArgumentList.Add("");

			log.LogInformation("Setting initial fan duty cycles");

			foreach(string zone in new[]{Config.HDDZone, Config.CPUZone})
			{
				int idx = Config.ipmiSetFanSpeed.Length;
				pInfo.ArgumentList[idx] = zone;

				int speed = zone == Config.HDDZone
					? Config.HDDInitSpeed
					: Config.CPUInitSpeed;

				pInfo.ArgumentList[idx + 1] = $"0x{speed:X}";

				proc = Process.Start(pInfo);
				if(proc == null)
					throw new ApplicationException("Error executing ipmitool");

				proc.WaitForExit();

				if(proc.ExitCode != 0)
					throw new ApplicationException("Error executing ipmitool");
			}

			cancelSource = CancellationTokenSource
				.CreateLinkedTokenSource(appLifetime.ApplicationStopping);

			AsyncLock ipmiLock = new();

			IPMIMonitor ipmiMonitor = new(log, ipmiLock, config.ipmiTool);
			sensorTask = ipmiMonitor.Run(cancelSource.Token);
			sensorTask.ContinueWith(OnError, TaskContinuationOptions.OnlyOnFaulted);

			// Start the other threads a bit later so they don't immediately
			// run ipmitool after or before the sensor thread.
			Thread.Sleep(Config.taskDelay * 500);

			HDDMonitor hddMonitor = new(
				log,
				ipmiMonitor,
				ipmiLock,
				config.lsScsi,
				config.hddTemp,
				config.lsiUtil,
				config.ipmiTool);

			hddTask = hddMonitor.Run(cancelSource.Token);
			hddTask.ContinueWith(OnError, TaskContinuationOptions.OnlyOnFaulted);

			CPUMonitor cpuMonitor = new(
				log,
				ipmiMonitor,
				ipmiLock,
				config.lsiUtil,
				config.ipmiTool);

			cpuTask = cpuMonitor.Run(cancelSource.Token);
			cpuTask.ContinueWith(OnError, TaskContinuationOptions.OnlyOnFaulted);

			log.LogInformation("Started");
		});

		appLifetime.ApplicationStopping.Register(() =>
		{
			log.LogInformation("Exiting...");

			sensorTask!.Wait();
			hddTask!.Wait();
			cpuTask!.Wait();
		});

		appLifetime.ApplicationStopped.Register(() =>
		{
			log.LogInformation("Setting fans to full speed...");

			ProcessStartInfo pInfo = new(config.ipmiTool);
			Config.ipmiSetFanModeFull.All(x =>
			{
				pInfo.ArgumentList.Add(x);
				return true;
			});

			Process? proc = Process.Start(pInfo);
			if(proc == null)
				log.LogCritical("Unable to execute ipmitool and set fans to full speed!");

			proc?.WaitForExit();

			log.LogInformation("Done...");
		});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken _) => Task.CompletedTask;

	public void Dispose()
	{
		cancelSource?.Dispose();
	}
}
