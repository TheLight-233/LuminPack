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
    static void Main()
    {
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