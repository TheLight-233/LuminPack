
using System.Buffers;
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
using Nino.Core;


#nullable disable

/// <summary>Provides polymorphism class base functionality.</summary>
[LuminPackable]

[MemoryPackable]
[MemoryPackUnion(1, typeof(PolymorphismClass))]


[System.Text.Json.Serialization.JsonDerivedType(typeof(PolymorphismClass), "PolymorphismClass")]
[NinoType]
public abstract partial class PolymorphismClassBase
{
    /// <summary>Stores the base id.</summary>
    [LuminPackOrder(0)]
    public int BaseID;

    /// <summary>Writes  to the current output.</summary>
    public abstract void Write();
}

/// <summary>Provides polymorphism class functionality.</summary>
[MemoryPackable]

[LuminPackable]

[NinoType]
public sealed partial class PolymorphismClass : PolymorphismClassBase, Ifoo
{
    /// <summary>Stores the id.</summary>
    [LuminPackOrder(1)]
    public int Id;
    /// <summary>Stores the tag.</summary>
    [LuminPackOrder(2)]
    public bool Tag;
    /// <summary>Stores the numbers.</summary>
    [LuminPackOrder(3)]
    public int[] Numbers;
    /// <summary>Stores the numbers2.</summary>
    [LuminPackOrder(4)]
    public List<IFoo> Numbers2;
    /// <summary>Stores the map1.</summary>
    [LuminPackOrder(5)]
    public Dictionary<int, IFoo> Map1;
    /// <summary>Stores the foo.</summary>
    [LuminPackOrder(6)]
    public FooD Foo;
    /// <summary>Stores the set.</summary>
    [LuminPackOrder(7)]
    public HashSet<int> Set;

    /// <summary>Creates a sample instance for the benchmark.</summary>
    /// <returns>The result of the operation.</returns>
    public static PolymorphismClass Create()
    {
        Random random = new Random(42);
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




















[System.Text.Json.Serialization.JsonDerivedType(typeof(FooA), "FooA")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooB), "FooB")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooC), "FooC")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooD), "FooD")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooE), "FooE")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooF), "FooF")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooG), "FooG")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooH), "FooH")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooI), "FooI")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooJ), "FooJ")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooK), "FooK")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooL), "FooL")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooM), "FooM")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooN), "FooN")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooO), "FooO")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooP), "FooP")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooQ), "FooQ")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooR), "FooR")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooS), "FooS")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(FooZ), "FooZ")]
[NinoType]
public abstract partial class IFoo
{
    /// <summary>Stores the id.</summary>
    public int Id;

    /// <summary>Measures benchmark legacy union serialize for the configured benchmark input.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal virtual void __BenchmarkLegacyUnionSerialize(ref LuminPackWriter writer)
        => throw new NotSupportedException();
}

/// <summary>Provides foo a functionality.</summary>

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooA : IFoo
{
    /// <summary>Stores the card.</summary>
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooB : FooA
{
    /// <summary>Stores the card b.</summary>
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooC : FooB
{
    /// <summary>Stores the card c.</summary>
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooD : FooC
{
    /// <summary>Stores the card d.</summary>
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooE : FooD
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooF : FooE
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooH : FooF
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooG : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooI : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooJ : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooK : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooL : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooM : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooN : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooO : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooP : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooQ : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooR : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooS : FooH
{
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

[MemoryPackable]
[LuminPackable]

[NinoType]
public partial class FooZ : FooH
{
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
