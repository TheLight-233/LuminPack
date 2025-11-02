
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

[LuminPackable(generatorType: LuminPack.Enum.GeneratorType.CircleReference)]
[MemoryPackable(MemoryPack.GenerateType.CircularReference)]
public partial class Node
{
    [LuminPackOrder(0)] 
    [MemoryPackOrder(0)] 
    public string Name { get; set; } = string.Empty;

    [LuminPackOrder(1)] 
    [MemoryPackOrder(1)] 
    public Node? Parent { get; set; }

    [LuminPackOrder(2)] 
    [MemoryPackOrder(2)] 
    public List<Node> Children { get; set; } = new();
    
    public static void TestCircleReference(List<string> output)
    {
        try
        {
            // 构造循环引用：root ↔ child
            var root = new Node { Name = "root", Children = new() };
            var child = new Node { Name = "child", Parent = root, Children = new() };
            root.Children.Add(child);

            // 再加一个“孙子”，让它也指向 root，形成多重引用
            var grandChild = new Node { Name = "grand", Parent = child, Children = new() };
            child.Children.Add(grandChild);
            grandChild.Children.Add(root); // ⚠️ 真正的循环：grandChild → root

            // 序列化
            var buf = LuminPackSerializer.Serialize(root);

            // 反序列化
            var root2 = LuminPackSerializer.Deserialize<Node>(buf);

            // 验证对象身份
            bool ok =
                ReferenceEquals(root2, root2.Children[0].Parent) && // root ↔ child
                ReferenceEquals(root2, root2.Children[0].Children[0].Children[0]); // grandChild → root

            output.Add(ok ? "✓ TestCircleReference - PASSED"
                : "✗ TestCircleReference - FAILED（引用未恢复为同一对象）");
        }
        catch (Exception ex)
        {
            output.Add($"✗ TestCircleReferenceSerialization - ERROR: {ex.Message}");
            output.Add($"Stack trace: {ex.StackTrace}");
        }
    }
}

[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[ShortRunJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class SimpleBenchmark
{
    private SimpleClassBase myClass = SimpleClass.Create();
    
    private LuminBufferWriter luminBufferWriter = LuminBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private NinoArrayBufferWriter ninoBufferWriter = new NinoArrayBufferWriter(100000);
    
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
        MemoryPackSerializer.Serialize(memoryBufferWriter, myClass);
    }
    
    [Benchmark]
    public void MessagePackSerialize()
    {
        var buffer = MessagePackSerializer.Serialize(myClass);
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