using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(LuminPackBenchmark.CapacityTierBenchmark).Assembly).Run(args);
