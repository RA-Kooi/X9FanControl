namespace X9FanControl;

using System;
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
				config.lsScsi,
				config.hddTemp,
				config.lsiUtil);

			hddTask = hddMonitor.Run(cancelSource.Token);
			hddTask.ContinueWith(OnError, TaskContinuationOptions.OnlyOnFaulted);

			CPUMonitor cpuMonitor = new(
				log,
				ipmiMonitor,
				config.lsiUtil);

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

			// TODO: Set fans to full
		});

		appLifetime.ApplicationStopped.Register(() =>
		{
			log.LogInformation("Done..");
		});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken _) => Task.CompletedTask;

	public void Dispose()
	{
		cancelSource?.Dispose();
	}
}
