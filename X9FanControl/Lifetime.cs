namespace X9FanControl;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

class Lifetime: IHostedService
{
	private bool stop;
	private readonly IHostApplicationLifetime appLifetime;
	private Task? mainLoop;

	public Lifetime(IHostApplicationLifetime appLifetime)
	{
		this.stop = false;
		this.appLifetime = appLifetime;
		this.mainLoop = null;
	}

	public Task StartAsync(CancellationToken _)
	{
		mainLoop = Task.Run(async() =>
		{
			while(!stop)
			{
				Console.WriteLine("Sleeping...");
				await Task.Delay(5000);
			}
		});

		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken _)
	{
		Console.WriteLine("Exiting...");

		stop = true;
		await mainLoop!;
	}
}
