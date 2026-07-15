using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Enum;
using LuminPack.Utility;
using MemoryPack;
using MessagePack;
using Nino.Core;

namespace LuminPackBenchmark;


[MemoryPackable]
[MessagePackObject]
[LuminPackable]
public partial class CompressTest
{
    [LuminPackOrder(0)]
    [Key(0)] public int Id;
    [LuminPackOrder(1)]
    [Key(1)] public bool Tag;
    [LuminPackOrder(2)]
    [LuminPackCompress]
    [BrotliFormatter]
    [LuminPackableObject]
    [Key(2)] public byte[] Numbers;
    [LuminPackCompress]
    [LuminPackOrder(3)]
    [Key(3)] public List<Transform> Numbers2;
    [LuminPackOrder(4)]
    [Key(4)] public Dictionary<int, IFoo> Map1;
    [LuminPackOrder(5)]
    [Key(5)] public FooD Foo;
    
    public static CompressTest Create()
    {
        Random random = new Random();
        return new CompressTest
        {
            Id = 10000,
            Tag = random.Next() % 2 == 0,
            Numbers = LuminPackSerializer.Serialize(SimpleClass.Create()),
            Numbers2 = Enumerable.Range(0, 100).Select(x => new Transform(x, x, x)).ToList(),
            Map1 = new Dictionary<int, IFoo>()
            {
                {1, FooA.Create()},
                {2, FooB.Create()},
                {3, FooC.Create()},
                {4, FooD.Create()},
                {5, FooA.Create()},
                {6, FooB.Create()},
                {7, FooC.Create()},
                {8, FooD.Create()},
                {9, FooA.Create()},
                {10, FooB.Create()},
                {11, FooC.Create()},
                {12, FooD.Create()},
            },
            Foo = new FooD()
            {
                Card = new Transform(random.Next(), random.Next(), random.Next()),
                CardB = new Transform(random.Next(), random.Next(), random.Next()),
                CardC = new Transform(random.Next(), random.Next(), random.Next()),
                CardD = new Transform(random.Next(), random.Next(), random.Next()),
            },
        };
    }
}

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class SerializeCompressBenchmark
{
    public CompressTest data = CompressTest.Create();
    
    public MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
    
    private LuminBufferWriter luminBufferWriter = LuminBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private ArrayBufferWriter<byte> messageArrayBufferWriter = new ArrayBufferWriter<byte>(100000);
    
    private byte[] buffer1;
    private byte[] buffer2;
    private byte[] buffer3;

    [GlobalSetup]
    public void SetUp()
    {
        buffer1 = LuminPackSerializer.Serialize(data);
        buffer2 = MemoryPackSerializer.Serialize(data);
        buffer3 = MessagePackSerializer.Serialize(data);
    }
    
    [GlobalCleanup]
    public void CleanUp()
    {
        luminBufferWriter.Dispose();
    }
    
    [Benchmark(Baseline = true)]
    public void LuminPackSerialize()
    {
        LuminPackSerializer.Serialize(data);
    }
    
    [Benchmark]
    public void MemoryPackSerialize()
    {
        MemoryPackSerializer.Serialize(data);
    }
    
    [Benchmark]
    public void MessagePackSerialize()
    {
        MessagePackSerializer.Serialize(data, options);
    }
    
    [Benchmark]
    public void LuminPackDeserialize()
    {
        LuminPackSerializer.Deserialize<CompressTest>(buffer1);
    }

    [Benchmark]
    public void MemoryPackDeserialize()
    {
        MemoryPackSerializer.Deserialize<CompressTest>(buffer2);
    }

    [Benchmark]
    public void MessagePackDeserialize()
    {
        MessagePackSerializer.Deserialize<CompressTest>(buffer3);
    }
}