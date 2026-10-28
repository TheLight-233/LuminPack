
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using LuminPack;
using LuminPack.Core;
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

/// <summary>Provides polymorphism class base functionality.</summary>
[LuminPackable]
[NinoType]
[MemoryPackable]
[MemoryPackUnion(1, typeof(PolymorphismClass))]
[MessagePackObject]
[Union(1, typeof(PolymorphismClass))]
public abstract partial class PolymorphismClassBase
{
    /// <summary>Stores the base id.</summary>
    [Key(10)]
    [LuminPackOrder(0)]
    public int BaseID;

    /// <summary>Writes  to the current output.</summary>
    public abstract void Write();
}

/// <summary>Provides polymorphism class functionality.</summary>
[MemoryPackable]
[MessagePackObject]
[LuminPackable]
[NinoType]
public sealed partial class PolymorphismClass : PolymorphismClassBase, Ifoo
{
    /// <summary>Stores the id.</summary>
    [Key(0)]
    [LuminPackOrder(1)]
    public int Id;
    /// <summary>Stores the tag.</summary>
    [Key(1)]
    [LuminPackOrder(2)]
    public bool Tag;
    /// <summary>Stores the numbers.</summary>
    [Key(8)]
    [LuminPackOrder(3)]
    public int[] Numbers;
    /// <summary>Stores the numbers2.</summary>
    [Key(3)]
    [LuminPackOrder(4)]
    public List<IFoo> Numbers2;
    /// <summary>Stores the map1.</summary>
    [Key(4)]
    [LuminPackOrder(5)]
    public Dictionary<int, IFoo> Map1;
    /// <summary>Stores the foo.</summary>
    [Key(5)]
    [LuminPackOrder(6)]
    public FooD Foo;
    /// <summary>Stores the set.</summary>
    [Key(6)]
    [LuminPackOrder(7)]
    public HashSet<int> Set;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static PolymorphismClass Create()
    {
        Random random = new Random(12345);
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

    /// <summary>Writes  to the current output.</summary>
    public override void Write() => Console.WriteLine("Yeah！！！");
}


/// <summary>Provides i foo functionality.</summary>
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
[LuminPackUnion(15, typeof(FooO))]
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
    /// <summary>Stores the id.</summary>
    [Key(0)] public int Id;

    /// <summary>Measures benchmark legacy union serialize for the configured benchmark input.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal virtual void __BenchmarkLegacyUnionSerialize(ref LuminPackWriter writer)
        => throw new NotSupportedException();
}

/// <summary>Provides foo a functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooA : IFoo
{

    /// <summary>Stores the card.</summary>
    [Key(1)]
    public Transform Card;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooA Create()
    {
        return new FooA()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo b functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooB : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;

    /// <summary>Stores the card b.</summary>
    [Key(2)]
    public Transform CardB;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooB Create()
    {
        return new FooB()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo c functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooC : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;

    /// <summary>Stores the card c.</summary>
    [Key(3)]
    public Transform CardC;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooC Create()
    {
        return new FooC()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo d functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooD : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;

    /// <summary>Stores the card d.</summary>
    [Key(4)]
    public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooD Create()
    {
        return new FooD()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo e functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooE : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooE Create()
    {
        return new FooE()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo f functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooF : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooF Create()
    {
        return new FooF()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo h functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooH : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooH Create()
    {
        return new FooH()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo g functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooG : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooG Create()
    {
        return new FooG()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo i functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooI : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooI Create()
    {
        return new FooI()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo j functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooJ : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooJ Create()
    {
        return new FooJ()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo k functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooK : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooK Create()
    {
        return new FooK()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo l functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooL : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooL Create()
    {
        return new FooL()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo m functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooM : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooM Create()
    {
        return new FooM()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo n functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooN : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooN Create()
    {
        return new FooN()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo o functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooO : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooO Create()
    {
        return new FooO()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo p functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooP : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooP Create()
    {
        return new FooP()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo q functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooQ : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooQ Create()
    {
        return new FooQ()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo r functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooR : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooR Create()
    {
        return new FooR()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo s functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooS : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooS Create()
    {
        return new FooS()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides foo z functionality.</summary>
[NinoType]
[MemoryPackable]
[LuminPackable]
[MessagePackObject]
public sealed partial class FooZ : IFoo
{
    /// <summary>Stores the Card value.</summary>
    [Key(1)] public Transform Card;
    /// <summary>Stores the CardB value.</summary>
    [Key(2)] public Transform CardB;
    /// <summary>Stores the CardC value.</summary>
    [Key(3)] public Transform CardC;
    /// <summary>Stores the CardD value.</summary>
    [Key(4)] public Transform CardD;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static FooZ Create()
    {
        return new FooZ()
        {
            Id = 1000,
            Card = new Transform { x = 1, y = 2, z = 3 },
            CardB = new Transform { x = 1, y = 2, z = 3 },
            CardC = new Transform { x = 1, y = 2, z = 3 },
            CardD = new Transform { x = 1, y = 2, z = 3 },
        };
    }
}

/// <summary>Provides ifoo functionality.</summary>
public interface Ifoo
{
    /// <summary>Writes  to the current output.</summary>
    public void Write();
}


/// <summary>Provides polymorphism benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 10)]
[MemoryDiagnoser]
[GcServer]
[MarkdownExporterAttribute.GitHub]
[Config(typeof(PolymorphismDisassemblyConfig))]
public class PolymorphismBenchmark
{
    private PolymorphismClassBase myClass = PolymorphismClass.Create();

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
        for (int i = 0; i < 1000; i++)
        {
            switch (i % 20)
            {
                case 0: ((PolymorphismClass)myClass).Numbers2.Add(FooA.Create()); break;
                case 1: ((PolymorphismClass)myClass).Numbers2.Add(FooB.Create()); break;
                case 2: ((PolymorphismClass)myClass).Numbers2.Add(FooC.Create()); break;
                case 3: ((PolymorphismClass)myClass).Numbers2.Add(FooD.Create()); break;
                case 4: ((PolymorphismClass)myClass).Numbers2.Add(FooE.Create()); break;
                case 5: ((PolymorphismClass)myClass).Numbers2.Add(FooF.Create()); break;
                case 6: ((PolymorphismClass)myClass).Numbers2.Add(FooG.Create()); break;
                case 7: ((PolymorphismClass)myClass).Numbers2.Add(FooH.Create()); break;
                case 8: ((PolymorphismClass)myClass).Numbers2.Add(FooI.Create()); break;
                case 9: ((PolymorphismClass)myClass).Numbers2.Add(FooJ.Create()); break;
                case 10: ((PolymorphismClass)myClass).Numbers2.Add(FooK.Create()); break;
                case 11: ((PolymorphismClass)myClass).Numbers2.Add(FooL.Create()); break;
                case 12: ((PolymorphismClass)myClass).Numbers2.Add(FooM.Create()); break;
                case 13: ((PolymorphismClass)myClass).Numbers2.Add(FooN.Create()); break;
                case 14: ((PolymorphismClass)myClass).Numbers2.Add(FooO.Create()); break;
                case 15: ((PolymorphismClass)myClass).Numbers2.Add(FooP.Create()); break;
                case 16: ((PolymorphismClass)myClass).Numbers2.Add(FooQ.Create()); break;
                case 17: ((PolymorphismClass)myClass).Numbers2.Add(FooR.Create()); break;
                case 18: ((PolymorphismClass)myClass).Numbers2.Add(FooS.Create()); break;
                case 19: ((PolymorphismClass)myClass).Numbers2.Add(FooZ.Create()); break;
            }
        }

        buffer1 = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(myClass);
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
        LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(myClass, luminBufferWriter);
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
        var res = LuminPackSerializer.Deserialize<PolymorphismClassBase, LuminPackUtf16LengthOptions>(buffer1);
    }

    /// <summary>Measures memory pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MemoryPackDeserialize()
    {
        var res = MemoryPackSerializer.Deserialize<PolymorphismClassBase>(buffer2);
    }

    /// <summary>Measures message pack deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void MessagePackDeserialize()
    {
        var res = MessagePackSerializer.Deserialize<PolymorphismClassBase>(buffer3);
    }

    /// <summary>Measures nino deserialize for the configured benchmark input.</summary>
    [Benchmark]
    public void NinoDeserialize()
    {
        var res = NinoDeserializer.Deserialize<PolymorphismClassBase>(buffer4);
    }
}

/// <summary>Enables native disassembly on the hosts supported by the installed BenchmarkDotNet version.</summary>
public sealed class PolymorphismDisassemblyConfig : ManualConfig
{
    /// <summary>Keeps timing and allocation benchmarks available on macOS without an unsupported diagnoser.</summary>
    public PolymorphismDisassemblyConfig()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                maxDepth: 8, printSource: true, printInstructionAddresses: true)));
    }
}
