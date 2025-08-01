namespace X9FanControl;

using System;
using System.IO;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using LibCapNG;

class Program
{
	static string? FindProgram(string[] paths, string program)
	{
		foreach(string path in paths)
		{
			string fullPath = Path.Join(path, program);
			if(File.Exists(fullPath))
				return fullPath;
		}
		return null;
	}

	static int Main(string[] args)
	{
		cap_ng.CapngClear(CapngSelectT.CAPNG_SELECT_BOTH);

		/*
		cap_ng.CapngUpdate(
			CapngActT.CAPNG_ADD,
			CapngTypeT.CAPNG_EFFECTIVE | CapngTypeT.CAPNG_PERMITTED,
			(uint)Capabilities.FOWNER);

		cap_ng.CapngUpdate(
			CapngActT.CAPNG_ADD,
			CapngTypeT.CAPNG_EFFECTIVE | CapngTypeT.CAPNG_PERMITTED,
			(uint)Capabilities.DAC_READ_SEARCH);
		*/

		cap_ng.CapngUpdate(
			CapngActT.CAPNG_ADD,
			CapngTypeT.CAPNG_INHERITABLE | CapngTypeT.CAPNG_BOUNDING_SET,
			(uint)Capabilities.SYS_RAWIO);

		cap_ng.CapngApply(CapngSelectT.CAPNG_SELECT_BOTH);

		if(Config.HDDInitSpeed % Config.fanStep != 0)
			throw new ApplicationException("HDDInitSpeed must be a multiple of fanSpeed");

		if(Config.CPUInitSpeed % Config.fanStep != 0)
			throw new ApplicationException("CPUInitSpeed must be a multiple of fanSpeed");

		string path = Environment.GetEnvironmentVariable("PATH")!;
		string[] paths = path.Split(':', StringSplitOptions.RemoveEmptyEntries);

		string? lsScsi   = FindProgram(paths, "lsscsi");
		string? hddTemp  = FindProgram(paths, "hddtemp");
		string? lsiUtil  = FindProgram(paths, "lsiutil");
		string? ipmiTool = FindProgram(paths, "ipmitool");

		bool error = false;
		if(lsScsi == null)
		{
			Console.Error.WriteLine("Unable to find lsscsi!");
			error = true;
		}
		if(hddTemp == null)
		{
			Console.Error.WriteLine("Unable to find hddtemp");
			error = true;
		}
		if(lsiUtil == null)
		{
			Console.Error.WriteLine("Unable to find lsiutil");
			error = true;
		}
		if(ipmiTool == null)
		{
			Console.Error.WriteLine("Unable to find ipmitool");
			error = true;
		}

		if(error)
			return 1;

		Action<HostBuilderContext, IServiceCollection> lifeTime =
			(hostContext, services) =>
			{
				services.Configure<LifetimeConfig>(c =>
				{
					c.lsScsi = lsScsi!;
					c.hddTemp = hddTemp!;
					c.lsiUtil = lsiUtil!;
					c.ipmiTool = ipmiTool!;
				});
				services.AddHostedService<Lifetime>();
			};

		IHost host = new HostBuilder()
			.ConfigureLogging(logger =>
			{
				logger.AddConsole();
				logger.SetMinimumLevel(LogLevel.Debug);
			})
			.ConfigureServices(lifeTime)
			.UseConsoleLifetime()
			.Build();

		host.Run();

		return Environment.ExitCode;
	}
}
