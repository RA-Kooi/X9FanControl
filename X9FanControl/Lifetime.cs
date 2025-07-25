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

class Lifetime: IHostedService
{
	private readonly ILogger<Lifetime> log;
	private readonly IHostApplicationLifetime appLifetime;
	private LifetimeConfig config;
	private Task? sensorTask;

	public Lifetime(
		ILogger<Lifetime> logger,
		IHostApplicationLifetime appLifetime,
		IOptions<LifetimeConfig> options)
	{
		log = logger;
		this.appLifetime = appLifetime;
		this.config = options.Value;
		this.sensorTask = null;
	}

	public Task StartAsync(CancellationToken _)
	{
		Action<Task> OnError = (_) =>
		{
			appLifetime.StopApplication();
		};

		appLifetime.ApplicationStarted.Register(() =>
		{
			AsyncLock ipmiLock = new();

			IPMIMonitor ipmiMonitor = new(log, ipmiLock, config.ipmiTool);
			sensorTask = ipmiMonitor.Run(appLifetime.ApplicationStopping);
			sensorTask.ContinueWith(OnError, TaskContinuationOptions.OnlyOnFaulted);

			log.LogInformation("Started");
		});

		appLifetime.ApplicationStopping.Register(() =>
		{
			log.LogInformation("Exiting...");

			sensorTask!.Wait();

			// TODO: Set fans to full
		});

		appLifetime.ApplicationStopped.Register(() =>
		{
			log.LogInformation("Done..");
		});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken _) => Task.CompletedTask;
}
