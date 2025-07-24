namespace X9FanControl;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Lifetime: IHostedService
{
	private bool stop;
	private readonly ILogger<Lifetime> log;
	private readonly IHostApplicationLifetime appLifetime;
	private Task? mainLoop;

	public Lifetime(
		ILogger<Lifetime> logger,
		IHostApplicationLifetime appLifetime)
	{
		this.stop = false;
		log = logger;
		this.appLifetime = appLifetime;
		this.mainLoop = null;
	}

	public Task StartAsync(CancellationToken _)
	{
		mainLoop = Task.Run(async() =>
		{
			while(!stop)
			{
				log.LogInformation("Sleeping...");
				await Task.Delay(5000);
			}
		});

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken _)
	{
		log.LogInformation("Exiting...");

		stop = true;
		await mainLoop!;
	}
}
