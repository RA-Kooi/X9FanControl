namespace BindGen;

using System.IO;

using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

class LibCap: ILibrary
{
	public void Setup(Driver driver)
	{
		DriverOptions options = driver.Options;
		options.GeneratorKind = GeneratorKind.CSharp;
		options.Verbose = true;
		options.Quiet = false;

		string cwd = Directory.GetCurrentDirectory();

		Module module = options.AddModule("LibCapNG");
		module.IncludeDirs.Add(cwd);
		module.Headers.Add("cap-ng.h");
		module.LibraryDirs.Add(@"/usr/lib");
		module.Libraries.Add("libcap-ng.so");
	}

	public void SetupPasses(Driver driver)
	{
	}

	public void Preprocess(Driver driver, ASTContext ctx)
	{
	}

	public void Postprocess(Driver driver, ASTContext ctx)
	{
	}
}
