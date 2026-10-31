
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
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
[MedianColumn, MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByJob)]
[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[Config(typeof(PolymorphismDisassemblyConfig))]
public class PolymorphismBenchmark
{
    /// <summary>The number of distinct random case sequences rotated between invocations.</summary>
    private const int ListCount = 16;

    /// <summary>The element count of each sequence.</summary>
    private const int ListLength = 1000;

    private LuminBufferWriter luminBufferWriter = LuminBufferWriterPool.Rent();
    private MemoryPack.Internal.ReusableLinkedArrayBufferWriter memoryBufferWriter = MemoryPack.Internal.ReusableLinkedArrayBufferWriterPool.Rent();
    private NinoArrayBufferWriter ninoBufferWriter = new NinoArrayBufferWriter(100000);
    private ArrayBufferWriter<byte> messageArrayBufferWriter = new ArrayBufferWriter<byte>(100000);

    private List<IFoo>[] lists;
    private byte[][] luminPayloads;
    private byte[][] memoryPayloads;
    private byte[][] messagePayloads;
    private byte[][] ninoPayloads;
    private int next;

    // The first sequence's payloads, compared by --verify-fixtures.
    private byte[] buffer1;
    private byte[] buffer2;
    private byte[] buffer3;
    private byte[] buffer4;

    /// <summary>Builds distinct uniformly random sequences over all twenty IFoo cases with fixed seeds.</summary>
    /// <remarks>Rotating sequences keeps the branch predictor from memorizing one replayed order.</remarks>
    [GlobalSetup]
    public void SetUp()
    {
        lists = new List<IFoo>[ListCount];
        luminPayloads = new byte[ListCount][];
        memoryPayloads = new byte[ListCount][];
        messagePayloads = new byte[ListCount][];
        ninoPayloads = new byte[ListCount][];
        for (int list = 0; list < ListCount; list++)
        {
            var random = new Random(20261031 + list);
            var items = new List<IFoo>(ListLength);
            for (int index = 0; index < ListLength; index++) items.Add(CreateFoo(random.Next(20)));
            lists[list] = items;
            luminPayloads[list] = LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(items);
            memoryPayloads[list] = MemoryPackSerializer.Serialize(items);
            messagePayloads[list] = MessagePackSerializer.Serialize(items);
            ninoPayloads[list] = NinoSerializer.Serialize(items);
        }
        buffer1 = luminPayloads[0];
        buffer2 = memoryPayloads[0];
        buffer3 = messagePayloads[0];
        buffer4 = ninoPayloads[0];
        next = 0;
    }

    /// <summary>Creates one sample instance of a case.</summary>
    /// <param name="kind">The case index from 0 to 19.</param>
    /// <returns>The sample instance.</returns>
    private static IFoo CreateFoo(int kind) => kind switch
    {
        0 => FooA.Create(), 1 => FooB.Create(), 2 => FooC.Create(), 3 => FooD.Create(), 4 => FooE.Create(),
        5 => FooF.Create(), 6 => FooG.Create(), 7 => FooH.Create(), 8 => FooI.Create(), 9 => FooJ.Create(),
        10 => FooK.Create(), 11 => FooL.Create(), 12 => FooM.Create(), 13 => FooN.Create(), 14 => FooO.Create(),
        15 => FooP.Create(), 16 => FooQ.Create(), 17 => FooR.Create(), 18 => FooS.Create(), _ => FooZ.Create(),
    };

    /// <summary>Selects the next sequence.</summary>
    /// <returns>The sequence index.</returns>
    private int Next() => next = (next + 1) & (ListCount - 1);

    /// <summary>Returns the pooled LuminPack buffer.</summary>
    [GlobalCleanup]
    public void CleanUp()
    {
        luminBufferWriter.Dispose();
    }

    /// <summary>Measures LuminPack writing a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public void LuminPackSerialize()
    {
        luminBufferWriter.ResetCore();
        luminBufferWriter.WriterState.ResetOperationState();
        LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>(lists[Next()], luminBufferWriter);
    }

    /// <summary>Measures MemoryPack writing a random List of every IFoo case.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize")]
    public void MemoryPackSerialize()
    {
        memoryBufferWriter.Reset();
        MemoryPackSerializer.Serialize(memoryBufferWriter, lists[Next()]);
    }

    /// <summary>Measures MessagePack writing a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public void MessagePackSerialize()
    {
        messageArrayBufferWriter.ResetWrittenCount();
        MessagePackSerializer.Serialize(messageArrayBufferWriter, lists[Next()]);
    }

    /// <summary>Measures Nino writing a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Serialize")]
    public void NinoSerialize()
    {
        ninoBufferWriter.ResetWrittenCount();
        NinoSerializer.Serialize(lists[Next()], ninoBufferWriter);
    }

    /// <summary>Measures LuminPack reading a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public void LuminPackDeserialize()
    {
        var res = LuminPackSerializer.Deserialize<List<IFoo>, LuminPackUtf16LengthOptions>(luminPayloads[Next()]);
    }

    /// <summary>Measures MemoryPack reading a random List of every IFoo case.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize")]
    public void MemoryPackDeserialize()
    {
        var res = MemoryPackSerializer.Deserialize<List<IFoo>>(memoryPayloads[Next()]);
    }

    /// <summary>Measures MessagePack reading a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public void MessagePackDeserialize()
    {
        var res = MessagePackSerializer.Deserialize<List<IFoo>>(messagePayloads[Next()]);
    }

    /// <summary>Measures Nino reading a random List of every IFoo case.</summary>
    [Benchmark]
    [BenchmarkCategory("Deserialize")]
    public void NinoDeserialize()
    {
        var res = NinoDeserializer.Deserialize<List<IFoo>>(ninoPayloads[Next()]);
    }
}

/// <summary>Enables native disassembly on the hosts supported by the installed BenchmarkDotNet version.</summary>
public sealed class PolymorphismDisassemblyConfig : BenchmarkConfig
{
    /// <summary>Keeps timing and allocation benchmarks available on macOS without an unsupported diagnoser.</summary>
    public PolymorphismDisassemblyConfig()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(
                maxDepth: 8, printSource: true, printInstructionAddresses: true)));
    }
}
