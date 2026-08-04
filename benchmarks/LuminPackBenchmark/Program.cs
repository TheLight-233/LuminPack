using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Running;
using JetBrains.Profiler.SelfApi;
using LuminPack;
using LuminPack.Option;
using LuminPack.Utility;
using LuminPackBenchmark;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--union-jit-probe")
        {
            UnionSerializationDispatchBenchmark.RunJitProbe();
            return;
        }

        if (args.Length == 1 && args[0] == "--buffer-pool-jit-probe")
        {
            BufferWriterPoolJitProbe.Run();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
    
}
