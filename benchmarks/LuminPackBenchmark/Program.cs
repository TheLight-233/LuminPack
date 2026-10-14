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
        if (args.Length == 2 && args[0] == "--binary-paired-probe")
        {
            BinaryPairedProbe.Run(args[1]);
            return;
        }

        if (args.Length == 1 && args[0] == "--fixed-field-assembly-probe")
        {
            CharacterAssemblyProbe.RunFixedFields();
            return;
        }

        if (args.Length == 1 && args[0] == "--character-assembly-probe")
        {
            CharacterAssemblyProbe.Run();
            return;
        }

        if (args.Length == 1 && args[0] == "--verify-optimization-fixtures")
        {
            new CharacterFixedFieldsRatioBenchmark().Setup();
            new CharacterBinaryRatioBenchmark().Setup();
            new CharacterJsonRatioBenchmark().Setup();
            foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
            {
                var benchmark = new BinaryOptimizationBenchmark { Encoding = encoding };
                try { benchmark.Setup(); }
                finally { benchmark.Cleanup(); }
            }
            foreach (var shape in new[] { "Reversed", "Indented", "Utf16" })
                new JsonInputShapeOptimizationBenchmark { Shape = shape }.Setup();
            new SerializerComparisonBenchmark().Setup();
            var graph = new ContinuationOptimizationBenchmark();
            try { graph.Setup(); }
            finally { graph.Cleanup(); }
            var medium = new MediumStringOptimizationBenchmark();
            try { medium.Setup(); }
            finally { medium.Cleanup(); }
            Console.WriteLine("All optimization fixtures preserve their full object graphs.");
            return;
        }

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
