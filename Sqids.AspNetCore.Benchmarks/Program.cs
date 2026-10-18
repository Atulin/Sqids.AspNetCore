using System.Reflection;
using BenchmarkDotNet.Running;

// Run everything (default when no arguments are given):
//   dotnet run -c Release --project Sqids.AspNetCore.Benchmarks
// Run a subset:
//   dotnet run -c Release --project Sqids.AspNetCore.Benchmarks -- --filter *Encode*
BenchmarkSwitcher
	.FromAssembly(Assembly.GetExecutingAssembly())
	.Run(args.Length == 0 ? [ "--filter", "*" ] : args);
