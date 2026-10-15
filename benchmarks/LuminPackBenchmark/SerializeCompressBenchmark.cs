using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using MemoryPack;
using MessagePack;

namespace LuminPackBenchmark;


/// <summary>Provides compress test functionality.</summary>
[MemoryPackable]
[MessagePackObject]
[LuminPackable]
public partial class CompressTest
{
    /// <summary>Stores the id.</summary>
    [LuminPackOrder(0)]
    [Key(0)] public int Id;
    /// <summary>Stores the tag.</summary>
    [LuminPackOrder(1)]
    [Key(1)] public bool Tag;
    /// <summary>Stores the numbers.</summary>
    [LuminPackOrder(2)]
    [LuminPackCompress]
    [BrotliFormatter]
    [LuminPackableObject]
    [Key(2)] public byte[] Numbers;
    /// <summary>Stores the numbers2.</summary>
    [LuminPackCompress]
    [LuminPackOrder(3)]
    [Key(3)] public List<Transform> Numbers2;
    /// <summary>Stores the map1.</summary>
    [LuminPackOrder(4)]
    [Key(4)] public Dictionary<int, IFoo> Map1;
    /// <summary>Stores the foo.</summary>
    [LuminPackOrder(5)]
    [Key(5)] public FooD Foo;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
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

/// <summary>Provides serialize compress benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class SerializeCompressBenchmark
{
    /// <summary>Stores the data.</summary>
    public CompressTest data = CompressTest.Create();

    /// <summary>Stores the options.</summary>
    public MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

    private LuminBufferWriter luminBufferWriter = LuminBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private ArrayBufferWriter<byte> messageArrayBufferWriter = new ArrayBufferWriter<byte>(100000);

    private byte[] buffer1;
    private byte[] buffer2;
    private byte[] buffer3;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void SetUp()
    {
        buffer1 = LuminPackSerializer.Serialize(data);
        buffer2 = MemoryPackSerializer.Serialize(data);
        buffer3 = MessagePackSerializer.Serialize(data);
    }

    /// <summary>Measures clean up for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void CleanUp()
    {
        luminBufferWriter.Dispose();
    }

    /// <summary>Measures lumin pack serialize for the configured benchmark input.</summary>
    [Benchmark(Baseline = true)]
    public void LuminPackSerialize()
    {
        LuminPackSerializer.Serialize(data);
    }

    /// <summary>Measures memory pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackSerialize()
    {
        MemoryPackSerializer.Serialize(data);
    }

    /// <summary>Measures message pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackSerialize()
    {
        MessagePackSerializer.Serialize(data, options);
    }

    /// <summary>Measures lumin pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void LuminPackDeserialize()
    {
        LuminPackSerializer.Deserialize<CompressTest>(buffer1);
    }

    /// <summary>Measures memory pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackDeserialize()
    {
        MemoryPackSerializer.Deserialize<CompressTest>(buffer2);
    }

    /// <summary>Measures message pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackDeserialize()
    {
        MessagePackSerializer.Deserialize<CompressTest>(buffer3);
    }
}
