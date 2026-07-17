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
        if (args.Length > 0 && args[0].Equals("objectpool-quick", StringComparison.OrdinalIgnoreCase))
        {
            ObjectPoolBenchmark.RunQuick();
            return;
        }

        if (args.Length > 0 && args[0].Equals("objectpool", StringComparison.OrdinalIgnoreCase))
        {
            BenchmarkRunner.Run<ObjectPoolBenchmark>();
            return;
        }

        BenchmarkRunner.Run<SerializeBenchmark>();
        LuminPackSerializerOption luminPackSerializerOption = new LuminPackSerializerOption()
        {
            StringEncoding = LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Token
        };
        Console.WriteLine(LuminPackSerializer.Sizeof(CharacterSaveData.Create()));
        var buffer = LuminPackSerializer.Serialize(CharacterSaveData.Create(), luminPackSerializerOption);
        var result = LuminPackSerializer.Deserialize<CharacterSaveData>(buffer, luminPackSerializerOption);
    }
    
}
