using System.Collections.Immutable;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks which containers write a layout tag for structs whose native layout differs between processes.</summary>
[Collection(nameof(RuntimeStateCollection))]
public class LayoutTagScopeTest
{
    /// <summary>Checks that a variable-layout struct gets a tagged payload in contiguous array, List and Memory records.</summary>
    [Fact]
    public void VariableLayoutStructsAreTaggedInContiguousRecords()
    {
        var values = new[] { new UnmarkedVariant { A = 1, B = 100 }, new UnmarkedVariant { A = 2, B = -200 } };
        byte[] array = LuminPackSerializer.Serialize(values);
        Assert.Equal(values.Length, BitConverter.ToInt32(array, 0));
        Assert.Contains(array[4], new byte[] { 1, 2 });
        Assert.Equal(4 + 1 + values.Length * (array[4] == 1 ? 16 : 12), array.Length);
        Assert.Equal(array.Length, LuminPackSerializer.Sizeof(values));
        Assert.Equal(array, LuminPackSerializer.Serialize(values.ToList()));
        Assert.Equal(array, LuminPackSerializer.Serialize(new Memory<UnmarkedVariant>(values)));
        Assert.Equal(array, LuminPackSerializer.Serialize(new ArraySegment<UnmarkedVariant>(values)));
        Assert.Equal(values, ConcreteReads.Read<UnmarkedVariant[]>(array, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        Assert.Equal(values, ConcreteReads.Read<List<UnmarkedVariant>>(array, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        Assert.Equal(values, ConcreteReads.Read<Memory<UnmarkedVariant>>(array, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)).ToArray());
        var reusedList = new List<UnmarkedVariant> { default, default, default };
        LuminPackSerializer.Deserialize(array, ref reusedList);
        Assert.Equal(values, reusedList);
        var reusedArray = new UnmarkedVariant[2];
        var original = reusedArray;
        LuminPackSerializer.Deserialize(array, ref reusedArray);
        Assert.Same(original, reusedArray);
        Assert.Equal(values, reusedArray);
    }

    /// <summary>Checks that element-wise and member contexts round-trip the same struct through its packed single-value form.</summary>
    [Fact]
    public void ElementWiseFamiliesUseThePackedSingleValueForm()
    {
        var values = new[] { new UnmarkedVariant { A = 1, B = 100 }, new UnmarkedVariant { A = 2, B = -200 } };
        var immutable = values.ToImmutableArray();
        Assert.Equal(values, ConcreteReads.Read<ImmutableArray<UnmarkedVariant>>(LuminPackSerializer.Serialize(immutable), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var queue = new Queue<UnmarkedVariant>(values);
        Assert.Equal(values, ConcreteReads.Read<Queue<UnmarkedVariant>>(LuminPackSerializer.Serialize(queue), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var dictionary = new Dictionary<int, UnmarkedVariant> { [1] = values[0], [2] = values[1] };
        Assert.Equal(dictionary, ConcreteReads.Read<Dictionary<int, UnmarkedVariant>>(LuminPackSerializer.Serialize(dictionary), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var holder = new VariantHolder { Single = values[0], Items = values, Items2 = values.ToList() };
        var restored = ConcreteReads.Read<VariantHolder>(LuminPackSerializer.Serialize(holder), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Assert.Equal(holder.Single, restored.Single);
        Assert.Equal(holder.Items, restored.Items);
        Assert.Equal(holder.Items2, restored.Items2);
    }

    /// <summary>Checks that structs without any attribute get implicit formatters: packed values, tagged contiguous payloads, native when stable.</summary>
    [Fact]
    public void StructsWithoutAttributesGetImplicitFormatters()
    {
        var variant = new ImplicitVariant { A = 5, B = 99 };
        byte[] single = LuminPackSerializer.Serialize(variant);
        Assert.Equal(new[] { variant.A }.Concat(BitConverter.GetBytes(variant.B)).ToArray(), single);
        Assert.Equal(single.Length, LuminPackSerializer.Sizeof(variant));
        Assert.Equal(variant, ConcreteReads.Read<ImplicitVariant>(single, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));

        var variants = new[] { variant, new ImplicitVariant { A = 6, B = -1 } };
        byte[] array = LuminPackSerializer.Serialize(variants);
        Assert.Contains(array[4], new byte[] { 1, 2 });
        Assert.Equal(4 + 1 + variants.Length * (array[4] == 1 ? 16 : 12), array.Length);
        Assert.Equal(variants, ConcreteReads.Read<ImplicitVariant[]>(array, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var listBytes = LuminPackSerializer.Serialize(variants.ToList());
        Assert.Equal(array, listBytes);

        var stable = new[] { new ImplicitStable { A = 1, B = 2 }, new ImplicitStable { A = 3, B = 4 } };
        byte[] native = LuminPackSerializer.Serialize(stable);
        Assert.Equal(4 + stable.Length * 8, native.Length);
        Assert.Equal(stable, ConcreteReads.Read<ImplicitStable[]>(native, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));

        var holder = new ImplicitHolder
        {
            Single = variant,
            Items = variants,
            List = variants.ToList(),
            Map = new Dictionary<int, ImplicitVariant> { [1] = variant },
            Stable = stable.ToList(),
            StableMap = new Dictionary<int, ImplicitStable> { [7] = stable[1] }
        };
        var restored = ConcreteReads.Read<ImplicitHolder>(LuminPackSerializer.Serialize(holder), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v));
        Assert.Equal(holder.Single, restored.Single);
        Assert.Equal(holder.Items, restored.Items);
        Assert.Equal(holder.List, restored.List);
        Assert.Equal(holder.Map, restored.Map);
        Assert.Equal(holder.Stable, restored.Stable);
        Assert.Equal(holder.StableMap, restored.StableMap);
    }

    /// <summary>Checks that a struct whose layout is identical in every model keeps its headerless native copy.</summary>
    [Fact]
    public void StableLayoutsKeepHeaderlessNativeCopies()
    {
        var values = new[] { new StableLayout { A = 1, B = 2, C = 3 }, new StableLayout { A = 4, B = 5, C = 6 } };
        byte[] bytes = LuminPackSerializer.Serialize(values);
        Assert.Equal(4 + values.Length * 12, bytes.Length);
        Assert.Equal(values, ConcreteReads.Read<StableLayout[]>(bytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        // Trailing padding that every model shares stays part of the native element.
        var nullable = new int?[] { 1, null };
        Assert.Equal(4 + 2 * 8, LuminPackSerializer.Serialize(nullable).Length);
        // A layout that differs between models does carry the tag, for BCL generic structs as well.
        byte[] longs = LuminPackSerializer.Serialize(new long?[] { 1, null });
        Assert.Equal(4 + 1 + 2 * (longs[4] == 1 ? 16 : 12), longs.Length);
        Assert.Equal(new long?[] { 1, null }, ConcreteReads.Read<long?[]>(longs, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var pairs = new[] { new KeyValuePair<int, long>(1, 2), new KeyValuePair<int, long>(3, 4) };
        byte[] pairBytes = LuminPackSerializer.Serialize(pairs);
        Assert.Equal(4 + 1 + 2 * (pairBytes[4] == 1 ? 16 : 12), pairBytes.Length);
        Assert.Equal(pairs, ConcreteReads.Read<KeyValuePair<int, long>[]>(pairBytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }

    /// <summary>Checks that ValueTuple layouts, which vary with the runtime's automatic field order, are tagged or stay stable as appropriate.</summary>
    [Fact]
    public void ValueTupleOrderIsTaggedOnlyWhenModelsDiffer()
    {
        // (int, byte) places the wide member first under every model, so it keeps the headerless copy.
        var stable = new[] { (1, (byte)2), (3, (byte)4) };
        Assert.Equal(4 + 2 * 8, LuminPackSerializer.Serialize(stable).Length);
        // (byte, int) is ordered differently by the declared and the automatic models, so it is tagged.
        var variant = new[] { ((byte)1, 2), ((byte)3, 4) };
        byte[] bytes = LuminPackSerializer.Serialize(variant);
        Assert.Equal(4 + 1 + 2 * 8, bytes.Length);
        Assert.Equal(variant, ConcreteReads.Read<(byte, int)[]>(bytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        // A payload from a runtime that keeps declaration order (model 1) decodes here too.
        byte[] declared = BitConverter.GetBytes(2).Append((byte)1)
            .Concat(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0 }.Take(1).Concat(new byte[3]).Concat(BitConverter.GetBytes(2)))
            .Concat(new byte[] { 3, 0, 0, 0 }.Take(1).Concat(new byte[3]).Concat(BitConverter.GetBytes(4))).ToArray();
        Assert.Equal(variant, ConcreteReads.Read<(byte, int)[]>(declared, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }
}

/// <summary>A struct without the portable marker whose alignment differs between 32-bit and 64-bit processes.</summary>
[LuminPack.Attribute.LuminPackable]
public partial struct UnmarkedVariant : IEquatable<UnmarkedVariant>
{
    /// <summary>The narrow member preceding padding.</summary>
    public byte A;

    /// <summary>The wide member.</summary>
    public long B;

    /// <summary>Compares the members.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>Whether both members are equal.</returns>
    public bool Equals(UnmarkedVariant other) => A == other.A && B == other.B;
}

/// <summary>A struct with the same layout in every model.</summary>
[LuminPack.Attribute.LuminPackable]
public partial struct StableLayout : IEquatable<StableLayout>
{
    /// <summary>The first member.</summary>
    public int A;

    /// <summary>The second member.</summary>
    public int B;

    /// <summary>The third member.</summary>
    public int C;

    /// <summary>Compares the members.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>Whether all members are equal.</returns>
    public bool Equals(StableLayout other) => A == other.A && B == other.B && C == other.C;
}

/// <summary>An object holding variable-layout structs as members.</summary>
[LuminPack.Attribute.LuminPackable]
public sealed partial class VariantHolder
{
    /// <summary>A single member value.</summary>
    public UnmarkedVariant Single;

    /// <summary>An array member.</summary>
    public UnmarkedVariant[]? Items;

    /// <summary>A List member.</summary>
    public List<UnmarkedVariant>? Items2;
}

/// <summary>A struct without attributes whose alignment differs between 32-bit and 64-bit processes.</summary>
public struct ImplicitVariant : IEquatable<ImplicitVariant>
{
    /// <summary>The narrow member preceding padding.</summary>
    public byte A;

    /// <summary>The wide member.</summary>
    public long B;

    /// <summary>Compares the members.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>Whether both members are equal.</returns>
    public bool Equals(ImplicitVariant other) => A == other.A && B == other.B;
}

/// <summary>A struct without attributes whose layout is identical in every model.</summary>
public struct ImplicitStable : IEquatable<ImplicitStable>
{
    /// <summary>The first member.</summary>
    public int A;

    /// <summary>The second member.</summary>
    public int B;

    /// <summary>Compares the members.</summary>
    /// <param name="other">The other value.</param>
    /// <returns>Whether both members are equal.</returns>
    public bool Equals(ImplicitStable other) => A == other.A && B == other.B;
}

/// <summary>An object holding structs without attributes in every container shape.</summary>
[LuminPack.Attribute.LuminPackable]
public sealed partial class ImplicitHolder
{
    /// <summary>A single member.</summary>
    public ImplicitVariant Single;

    /// <summary>An array member.</summary>
    public ImplicitVariant[]? Items;

    /// <summary>A List member.</summary>
    public List<ImplicitVariant>? List;

    /// <summary>A dictionary member.</summary>
    public Dictionary<int, ImplicitVariant>? Map;

    /// <summary>A List member of a stable layout.</summary>
    public List<ImplicitStable>? Stable;

    /// <summary>A dictionary member of a stable layout.</summary>
    public Dictionary<int, ImplicitStable>? StableMap;
}
