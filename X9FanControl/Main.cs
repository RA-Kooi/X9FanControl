namespace X9FanControl;

using System;
using System.IO;

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

		return 0;
	}
}
