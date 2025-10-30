
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using LuminPack.Utility;
using GeneratorType = LuminPack.Enum.GeneratorType;

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
[MemoryPackUnion(1, typeof(PolymorphismClass))]
[MessagePackObject]
[Union(1, typeof(PolymorphismClass))]
public abstract partial class PolymorphismClassBase
{
    [Key(10)]
    [LuminPackOrder(0)]
    public int BaseID;

    public abstract void Write();
}

[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public sealed partial class PolymorphismClass : PolymorphismClassBase, Ifoo
{
    [Key(0)]
    [LuminPackOrder(1)] 
    public int Id;
    [Key(1)]
    [LuminPackOrder(2)] 
    public bool Tag;
    [Key(8)] 
    [LuminPackOrder(3)] 
    public int[] Numbers;
    [Key(3)] 
    [LuminPackOrder(4)] 
    public List<IFoo> Numbers2;
    [Key(4)] 
    [LuminPackOrder(5)] 
    public Dictionary<int, IFoo> Map1;
    [Key(5)]
    [LuminPackOrder(6)] 
    public FooD Foo;
    [Key(6)] 
    [LuminPackOrder(7)] 
    public HashSet<int> Set;

    public static PolymorphismClass Create()
    {
        Random random = new Random();
        return new PolymorphismClass
        {
            Id = random.Next(),
            Tag = random.Next() % 2 == 0,
            Numbers = Enumerable.Range(0, 100).Select(_ => random.Next()).ToArray(),
            Numbers2 = new(),
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
            Set = new HashSet<int>(Enumerable.Range(0, 100).Select(_ => random.Next()).ToList())
            
        };
    }
    
    public override void Write() => Console.WriteLine("Yeah！！！");
}

[NinoType]
[MemoryPackable]
[MemoryPackUnion(1, typeof(FooA))]
[MemoryPackUnion(2, typeof(FooB))]
[MemoryPackUnion(3, typeof(FooC))]
[MemoryPackUnion(4, typeof(FooD))]
[MemoryPackUnion(5, typeof(FooE))]
[MemoryPackUnion(6, typeof(FooF))]
[MemoryPackUnion(7, typeof(FooG))]
[MemoryPackUnion(8, typeof(FooH))]
[MemoryPackUnion(9, typeof(FooI))]
[MemoryPackUnion(10, typeof(FooJ))]
[MemoryPackUnion(11, typeof(FooK))]
[MemoryPackUnion(12, typeof(FooL))]
[MemoryPackUnion(13, typeof(FooM))]
[MemoryPackUnion(14, typeof(FooN))]
[MemoryPackUnion(15, typeof(FooO))]
[MemoryPackUnion(16, typeof(FooP))]
[MemoryPackUnion(17, typeof(FooQ))]
[MemoryPackUnion(18, typeof(FooR))]
[MemoryPackUnion(19, typeof(FooS))]
[MemoryPackUnion(20, typeof(FooZ))]
[LuminPackable]
[Union(1, typeof(FooA))]
[Union(2, typeof(FooB))]
[Union(3, typeof(FooC))]
[Union(4, typeof(FooD))]
[Union(5, typeof(FooE))]
[Union(6, typeof(FooF))]
[Union(7, typeof(FooG))]
[Union(8, typeof(FooH))]
[Union(9, typeof(FooI))]
[Union(10, typeof(FooJ))]
[Union(11, typeof(FooK))]
[Union(12, typeof(FooL))]
[Union(13, typeof(FooM))]
[Union(14, typeof(FooN))]
[Union(15, typeof(FooO))]
[Union(16, typeof(FooP))]
[Union(17, typeof(FooQ))]
[Union(18, typeof(FooR))]
[Union(19, typeof(FooS))]
[Union(20, typeof(FooZ))]
public abstract partial class IFoo
{
    [Key(0)] public int Id;
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooA : IFoo
{
    [Key(1)] 
    public Transform Card;
    

    public static FooA Create()
    {
        return new FooA()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooB : FooA
{
    [Key(2)] 
    public Transform CardB;
    
    public static FooB Create()
    {
        return new FooB()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooC : FooB
{
    [Key(3)] 
    public Transform CardC;
    
    public static FooC Create()
    {
        return new FooC()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooD : FooC
{
    [Key(4)] 
    public Transform CardD;

    public static FooD Create()
    {
        return new FooD()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooE : FooD
{
    public static FooE Create()
    {
        return new FooE()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooF : FooE
{
    public static FooF Create()
    {
        return new FooF()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooH : FooF
{
    public static FooH Create()
    {
        return new FooH()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooG : FooH
{
    public static FooG Create()
    {
        return new FooG()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooI : FooH
{
    public static FooI Create()
    {
        return new FooI()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooJ : FooH
{
    public static FooJ Create()
    {
        return new FooJ()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooK : FooH
{
    public static FooK Create()
    {
        return new FooK()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooL : FooH
{
    public static FooL Create()
    {
        return new FooL()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooM : FooH
{
    public static FooM Create()
    {
        return new FooM()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooN : FooH
{
    public static FooN Create()
    {
        return new FooN()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooO : FooH
{
    public static FooO Create()
    {
        return new FooO()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooP : FooH
{
    public static FooP Create()
    {
        return new FooP()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooQ : FooH
{
    public static FooQ Create()
    {
        return new FooQ()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooR : FooH
{
    public static FooR Create()
    {
        return new FooR()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooS : FooH
{
    public static FooS Create()
    {
        return new FooS()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public partial class FooZ : FooH
{
    public static FooZ Create()
    {
        return new FooZ()
        {
            Id = 1000,
            Card = new Transform {x = 1, y = 2, z = 3},
            CardB = new Transform {x = 1, y = 2, z = 3},
            CardC = new Transform {x = 1, y = 2, z = 3},
            CardD = new Transform {x = 1, y = 2, z = 3},
        };
    }
}

public interface Ifoo
{
    public void Write();
}


[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public partial class Transform
{
    [Key(0)] public float x;
    [Key(1)] public float y;
    [Key(2)] public float z;
    
    
    public Transform()
    {
    }

    [LuminPackConstructor]
    [MemoryPackConstructor]
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
[ShortRunJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
public class PolymorphismBenchmark
{
    private PolymorphismClassBase myClass = PolymorphismClass.Create();
    
    private ReusableLinkedArrayBufferWriter luminBufferWriter = ReusableLinkedArrayBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private NinoArrayBufferWriter ninoBufferWriter = new NinoArrayBufferWriter(100000);
    
    private byte[] buffer1;
    private byte[] buffer2;
    private byte[] buffer3;
    private byte[] buffer4;

    private LuminPackSerializerOption _option = new LuminPackSerializerOption()
    {
        StringEncoding = LuminPackStringEncoding.UTF16,
        StringRecording = LuminPackStringRecording.Length
    };
    
    [GlobalSetup]
    public void SetUp()
    {
        for (int i = 0; i < 100; i++)
        {
            switch (i % 20)
            {
                case 1 : ((PolymorphismClass)myClass).Numbers2.Add(FooA.Create()); break;
                case 2 : ((PolymorphismClass)myClass).Numbers2.Add(FooB.Create()); break;
                case 3 : ((PolymorphismClass)myClass).Numbers2.Add(FooC.Create()); break;
                case 4 : ((PolymorphismClass)myClass).Numbers2.Add(FooD.Create()); break;
                case 5 : ((PolymorphismClass)myClass).Numbers2.Add(FooE.Create()); break;
                case 6 : ((PolymorphismClass)myClass).Numbers2.Add(FooF.Create()); break;
                case 7 : ((PolymorphismClass)myClass).Numbers2.Add(FooG.Create()); break;
                case 8 : ((PolymorphismClass)myClass).Numbers2.Add(FooH.Create()); break;
                case 9 : ((PolymorphismClass)myClass).Numbers2.Add(FooI.Create()); break;
                case 10 : ((PolymorphismClass)myClass).Numbers2.Add(FooJ.Create()); break;
                case 11 : ((PolymorphismClass)myClass).Numbers2.Add(FooK.Create()); break;
                case 12 : ((PolymorphismClass)myClass).Numbers2.Add(FooL.Create()); break;
                case 13 : ((PolymorphismClass)myClass).Numbers2.Add(FooM.Create()); break;
                case 14 : ((PolymorphismClass)myClass).Numbers2.Add(FooN.Create()); break;
                case 15 : ((PolymorphismClass)myClass).Numbers2.Add(FooO.Create()); break;
                case 16 : ((PolymorphismClass)myClass).Numbers2.Add(FooP.Create()); break;
                case 17 : ((PolymorphismClass)myClass).Numbers2.Add(FooQ.Create()); break;
                case 18 : ((PolymorphismClass)myClass).Numbers2.Add(FooR.Create()); break;
                case 19 : ((PolymorphismClass)myClass).Numbers2.Add(FooS.Create()); break;
                case 20 : ((PolymorphismClass)myClass).Numbers2.Add(FooZ.Create()); break;
            }
        }
        
        buffer1 = LuminPackSerializer.Serialize(myClass, _option);
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
        LuminPackSerializer.Serialize(myClass, luminBufferWriter, _option);
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
        var res = LuminPackSerializer.Deserialize<PolymorphismClassBase>(buffer1, _option);
    }
    
    [Benchmark]
    public void MemoryPackDeserialize()
    {
        var res = MemoryPackSerializer.Deserialize<PolymorphismClassBase>(buffer2);
    }
    
    [Benchmark]
    public void MessagePackDeserialize()
    {
        var res = MessagePackSerializer.Deserialize<PolymorphismClassBase>(buffer3);
    }
    
    [Benchmark]
    public void NinoDeserialize()
    {
        var res = NinoDeserializer.Deserialize<PolymorphismClassBase>(buffer4);
    }
}