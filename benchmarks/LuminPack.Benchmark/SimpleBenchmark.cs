
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Utility;

namespace LuminPackBenchmark;

using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Attribute;
using MemoryPack;
using MessagePack;
using Nino.Core;


#nullable disable

/// <summary>Provides simple class base functionality.</summary>
[LuminPackable]
[NinoType]
[MemoryPackable]
[MemoryPackUnion(1, typeof(SimpleClass))]
[MessagePackObject]
[Union(1, typeof(SimpleClass))]
public abstract partial class SimpleClassBase
{
    /// <summary>Stores the base id.</summary>
    [Key(10)]
    public int BaseID;

    /// <summary>Writes  to the current output.</summary>
    public abstract void Write();
}


/// <summary>Provides simple class functionality.</summary>
[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public sealed partial class SimpleClass : SimpleClassBase, Ifoo
{
    /// <summary>Stores the id.</summary>
    [Key(0)] public int Id;
    /// <summary>Stores the tag.</summary>
    [Key(1)] public bool Tag;
    /// <summary>Stores the numbers.</summary>
    [Key(2)] public int[] Numbers;
    /// <summary>Stores the numbers2.</summary>
    [Key(3)] public List<Transform> Numbers2;
    /// <summary>Stores the map1.</summary>
    [Key(4)] public Dictionary<int, IFoo> Map1;
    /// <summary>Stores the foo.</summary>
    [Key(5)] public FooD Foo;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static SimpleClass Create()
    {
        Random random = new Random();
        return new SimpleClass
        {
            Id = random.Next(),
            Tag = random.Next() % 2 == 0,
            Numbers = Enumerable.Range(0, 100).Select(_ => random.Next()).ToArray(),
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

    /// <summary>Stores the instance.</summary>
    public static SimpleClass Instance = new();

    //[LuminPackPoolRent]
    /// <summary>Obtains a reusable instance from the pool, creating one when needed.</summary>
    /// <returns>The result of the operation.</returns>
    public static SimpleClass Rent()
    {
        return Instance;
    }

    //[LuminPackOnSerializing]
    /// <summary>Measures on serializing for the configured benchmark input.</summary>
    public static void OnSerializing()
    {
        Console.WriteLine("OnSerializing");
    }

    //[LuminPackOnSerialized]
    /// <summary>Measures on serialized for the configured benchmark input.</summary>
    public void OnSerialized()
    {
        Console.WriteLine("OnSerialized");
    }

    //[LuminPackOnDeserialized]
    /// <summary>Measures on deserialized for the configured benchmark input.</summary>
    public void OnDeserialized()
    {
        Console.WriteLine("OnDeserialized");
    }

    //[LuminPackOnDeserializing]
    /// <summary>Measures on deserializing for the configured benchmark input.</summary>
    public static void OnDeserializing()
    {
        Console.WriteLine("OnDeserializing");
    }



    /// <summary>Writes  to the current output.</summary>
    public override void Write() => Console.WriteLine("Yeah！！！");
}


/// <summary>Provides transform functionality.</summary>
[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
[LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct Transform
{
    /// <summary>Stores the x.</summary>
    [Key(0)] public float x;
    /// <summary>Stores the y.</summary>
    [Key(1)] public float y;
    /// <summary>Stores the z.</summary>
    [Key(2)] public float z;

    /// <summary>Initializes a new instance of <c>Transform</c>.</summary>
    public Transform()
    {
    }

    /// <summary>Initializes a new instance of <c>Transform</c>.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <param name="y">The y used by this operation.</param>
    /// <param name="z">The z used by this operation.</param>
    [LuminPackConstructor]
    //[MemoryPackConstructor]
    [NinoConstructor]
    public Transform(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

}

/// <summary>Provides simple benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 3, warmupCount: 6, iterationCount: 15)]
[MemoryDiagnoser]
[GcServer(true)]
public class SimpleBenchmark
{
    private SimpleClassBase myClass = SimpleClass.Create();

    private LuminBufferWriter luminBufferWriter = LuminBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private NinoArrayBufferWriter ninoBufferWriter = new NinoArrayBufferWriter(100000);
    private ArrayBufferWriter<byte> messageArrayBufferWriter = new ArrayBufferWriter<byte>(100000);

    private byte[] buffer1;
    private byte[] buffer2;
    private byte[] buffer3;
    private byte[] buffer4;

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void SetUp()
    {
        buffer1 = LuminPackSerializer.Serialize(myClass);
        buffer2 = MemoryPackSerializer.Serialize(myClass);
        buffer3 = MessagePackSerializer.Serialize(myClass);
        buffer4 = NinoSerializer.Serialize(myClass);
    }

    /// <summary>Measures clean up for the configured benchmark input.</summary>
    [GlobalCleanup]
    public void CleanUp()
    {
        luminBufferWriter.Dispose();
    }

    /// <summary>Measures lumin pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void LuminPackSerialize()
    {
        luminBufferWriter.ResetCore();
        luminBufferWriter.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize(myClass, luminBufferWriter);
    }

    /// <summary>Measures memory pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackSerialize()
    {
        memoryBufferWriter.Reset();
        MemoryPackSerializer.Serialize(memoryBufferWriter, myClass);
    }

    /// <summary>Measures message pack serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackSerialize()
    {
        messageArrayBufferWriter.ResetWrittenCount();
        MessagePackSerializer.Serialize(messageArrayBufferWriter, myClass);
    }

    /// <summary>Measures nino serialize for the configured benchmark input.</summary>
    [Benchmark]
    public void NinoSerialize()
    {
        ninoBufferWriter.ResetWrittenCount();
        NinoSerializer.Serialize(myClass, ninoBufferWriter);
    }

    /// <summary>Measures lumin pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void LuminPackDeserialize()
    {
        SimpleClassBase res = default;
        LuminPackSerializer.Deserialize(buffer1, ref res);
    }

    /// <summary>Measures memory pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackDeserialize()
    {
        var res = MemoryPackSerializer.Deserialize<SimpleClassBase>(buffer2);
    }

    /// <summary>Measures message pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackDeserialize()
    {
        var res = MessagePackSerializer.Deserialize<SimpleClassBase>(buffer3);
    }

    /// <summary>Measures nino deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void NinoDeserialize()
    {
        var res = NinoDeserializer.Deserialize<SimpleClassBase>(buffer4);
    }
}
