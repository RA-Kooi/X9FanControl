namespace X9FanControl;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Lifetime: IHostedService
{
	private readonly ILogger<Lifetime> log;
	private readonly IHostApplicationLifetime appLifetime;

	public Lifetime(
		ILogger<Lifetime> logger,
		IHostApplicationLifetime appLifetime)
	{
		log = logger;
		this.appLifetime = appLifetime;
	}

	public Task StartAsync(CancellationToken _)
	{
		Action<Task> OnError = (_) =>
		{
			appLifetime.StopApplication();
		};

		appLifetime.ApplicationStarted.Register(() =>
		{

			log.LogInformation("Started");
		});

		appLifetime.ApplicationStopping.Register(() =>
		{
			log.LogInformation("Exiting...");

		});

		appLifetime.ApplicationStopped.Register(() =>
		{
			log.LogInformation("Done..");
		});

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken _) => Task.CompletedTask;
}
