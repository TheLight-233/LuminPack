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
        if (args.Length == 1 && args[0] == "--buffer-pool-jit-probe")
        {
            BufferWriterPoolJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-jit-probe")
        {
            CharacterSaveDataJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-concrete-jit-probe")
        {
            CharacterSaveDataConcreteJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--dictionary-bucket-jit-probe")
        {
            DictionaryBucketJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-json-jit-probe")
        {
            CharacterSaveDataJsonJitProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--json-initializer-jit-probe")
        {
            CharacterSaveDataJsonJitProbe.RunInitializerControl();
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

}
