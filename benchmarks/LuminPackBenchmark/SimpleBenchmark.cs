
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
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

[LuminPackable]
[NinoType]
[MemoryPackable]
[MemoryPackUnion(1, typeof(SimpleClass))]
[MessagePackObject]
[Union(1, typeof(SimpleClass))]
public abstract partial class SimpleClassBase
{
    [Key(10)]
    public int BaseID;

    public abstract void Write();
}


[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public sealed partial class SimpleClass : SimpleClassBase, Ifoo
{
    [Key(0)] public int Id;
    [Key(1)] public bool Tag;
    [Key(2)] public int[] Numbers;
    [Key(3)] public List<Transform> Numbers2;
    [Key(4)] public Dictionary<int, IFoo> Map1;
    [Key(5)] public FooD Foo;

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

    public static SimpleClass Instance = new();
    
    //[LuminPackPoolRent]
    public static SimpleClass Rent()
    {
        return Instance;
    }
    
    //[LuminPackOnSerializing]
    public static void OnSerializing()
    {
        Console.WriteLine("OnSerializing");
    }
    
    //[LuminPackOnSerialized]
    public void OnSerialized()
    {
        Console.WriteLine("OnSerialized");
    }
    
    //[LuminPackOnDeserialized]
    public void OnDeserialized()
    {
        Console.WriteLine("OnDeserialized");
    }
    
    //[LuminPackOnDeserializing]
    public static void OnDeserializing()
    {
        Console.WriteLine("OnDeserializing");
    }
    
    
    
    public override void Write() => Console.WriteLine("Yeah！！！");
}


[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public partial struct Transform
{
    [Key(0)] public float x;
    [Key(1)] public float y;
    [Key(2)] public float z;
    
    public Transform()
    {
    }

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

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[GcServer]
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

    [GlobalSetup]
    public void SetUp()
    {
        buffer1 = LuminPackSerializer.Serialize(myClass);
        buffer2 = MemoryPackSerializer.Serialize(myClass);
        buffer3 = MessagePackSerializer.Serialize(myClass);
        buffer4 = NinoSerializer.Serialize(myClass);
    }

    [GlobalCleanup]
    public void CleanUp()
    {
        luminBufferWriter.Dispose();
    }

    [Benchmark]
    public void LuminPackSerialize()
    {
        LuminPackSerializer.Serialize(myClass, luminBufferWriter);
    }
    
    [Benchmark]
    public void MemoryPackSerialize()
    {
        memoryBufferWriter.Reset();
        MemoryPackSerializer.Serialize(memoryBufferWriter, myClass);
    }
    
    [Benchmark]
    public void MessagePackSerialize()
    {
        messageArrayBufferWriter.ResetWrittenCount();
        MessagePackSerializer.Serialize(messageArrayBufferWriter, myClass);
    }
    
    [Benchmark]
    public void NinoSerialize()
    {
        ninoBufferWriter.ResetWrittenCount();
        NinoSerializer.Serialize(myClass, ninoBufferWriter);
    }
    
    [Benchmark]
    public void LuminPackDeserialize()
    {
        var res = LuminPackSerializer.Deserialize<SimpleClassBase>(buffer1);
    }
    
    [Benchmark]
    public void MemoryPackDeserialize()
    {
        var res = MemoryPackSerializer.Deserialize<SimpleClassBase>(buffer2);
    }
    
    [Benchmark]
    public void MessagePackDeserialize()
    {
        var res = MessagePackSerializer.Deserialize<SimpleClassBase>(buffer3);
    }
    
    [Benchmark]
    public void NinoDeserialize()
    {
        var res = NinoDeserializer.Deserialize<SimpleClassBase>(buffer4);
    }
}