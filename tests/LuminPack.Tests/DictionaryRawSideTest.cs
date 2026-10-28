using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;
/// <summary>Protects independent raw dictionary members, including padding and sparse enumeration.</summary>
public class DictionaryRawSideTest
{
    /// <summary>Preserves custom comparer observations while primitive values replace their entire stored value.</summary>
    [Fact]
    public void PrimitiveReusePreservesCustomComparerCalls()
    {
        var source = new Dictionary<int, int> { [1] = 17, [2] = 23, [3] = 29 };
        var bytes = LuminPackSerializer.Serialize(source);
        var comparer = new CountingIntComparer();
        var destination = new Dictionary<int, int>(comparer) { [1] = -1, [2] = -2, [3] = -3 };
        comparer.HashCalls = comparer.EqualityCalls = 0;
        LuminPackSerializer.Deserialize(bytes, ref destination);
        Assert.Equal(6, comparer.HashCalls);
        Assert.Equal(3, comparer.EqualityCalls);
        Assert.Equal(source.OrderBy(pair => pair.Key), destination.OrderBy(pair => pair.Key));
    }

    /// <summary>Exposes the public lookup and rebuild operations performed during staged dictionary reuse.</summary>
    private sealed class CountingIntComparer : IEqualityComparer<int>
    {
        internal int HashCalls;
        internal int EqualityCalls;

        /// <summary>Counts equality checks and compares the integer keys.</summary>
        /// <param name="x">The first key.</param>
        /// <param name="y">The second key.</param>
        /// <returns>Whether the keys match.</returns>
        public bool Equals(int x, int y) { EqualityCalls++; return x == y; }

        /// <summary>Counts hashing and returns the deterministic integer hash.</summary>
        /// <param name="value">The dictionary key.</param>
        /// <returns>The unchanged key.</returns>
        public int GetHashCode(int value) { HashCalls++; return value; }
    }

    /// <summary>Compares both mixed shapes with their original per-element writer operations.</summary>
    /// <param name = "sparse">Whether deleted entries require the public enumerator fallback.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentRawMembersPreservePaddingAndWireOrder(bool sparse)
    {
        var keys = new Dictionary<RawSideStorage, string>();
        var values = new Dictionary<string, RawSideStorage>();
        for (int index = 0; index < 19; index++)
        {
            RawSideStorage raw = default;
            Unsafe.InitBlockUnaligned(ref Unsafe.As<RawSideStorage, byte>(ref raw), (byte)(index + 1), 48);
            raw.Id = index;
            raw.Amount = index * -17L;
            string text = "item-" + index;
            keys.Add(raw, text);
            values.Add(text, raw);
        }

        if (sparse)
            foreach (var pair in keys.ToArray().Where(pair => pair.Key.Id % 3 == 1))
            {
                keys.Remove(pair.Key);
                values.Remove(pair.Value);
            }

        byte[] storage = new byte[65536];
        Span<byte> span = storage;
        var state = new LuminPackWriterOptionalState();
        var writer = new LuminPackWriter(ref span, state);
        int offset = 0;
        writer.WriteCollectionHeader(keys.Count);
        foreach (var pair in keys)
        {
            writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref writer.Cursor, pair.Key));
            writer.Write(pair.Value);
        }

        byte[] keyBytes = LuminPackSerializer.Serialize(keys);
        Assert.Equal(storage.AsSpan(0, writer.Position).ToArray(), keyBytes);
        var keyCopy = LuminPackSerializer.Deserialize<Dictionary<RawSideStorage, string>>(keyBytes);
        Assert.Equal(keys.ToArray(), keyCopy.ToArray());
        Assert.Equal(keyBytes, LuminPackSerializer.Serialize(keyCopy));
        writer = new LuminPackWriter(ref span, state);
        offset = 0;
        writer.WriteCollectionHeader(values.Count);
        foreach (var pair in values)
        {
            writer.Write(pair.Key);
            writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref writer.Cursor, pair.Value));
        }

        byte[] valueBytes = LuminPackSerializer.Serialize(values);
        Assert.Equal(storage.AsSpan(0, writer.Position).ToArray(), valueBytes);
        var valueCopy = LuminPackSerializer.Deserialize<Dictionary<string, RawSideStorage>>(valueBytes);
        Assert.Equal(values.ToArray(), valueCopy.ToArray());
        Assert.Equal(valueBytes, LuminPackSerializer.Serialize(valueCopy));
    }

    /// <summary>Preserves the selected slot table when an unmanaged member has a versioned root contract.</summary>
    [Fact]
    public void SpecializedUnmanagedMembersKeepTheirSelectedFraming()
    {
        var raw = new VersionedStorage
        {
            Value = 107
        };
        var keys = new Dictionary<VersionedStorage, string>
        {
            [raw] = "versioned-key"
        };
        var values = new Dictionary<string, VersionedStorage>
        {
            ["versioned-value"] = raw
        };
        byte[] storage = new byte[1024];
        Span<byte> span = storage;
        var writer = new LuminPackWriter(ref span);
        int offset = 0;
        writer.WriteCollectionHeader(1);
        LuminPackSerializer.Serialize(ref writer, in raw);
        writer.Write("versioned-key");
        byte[] keyBytes = LuminPackSerializer.Serialize(keys);
        Assert.Equal(storage.AsSpan(0, writer.Position).ToArray(), keyBytes);
        Assert.Equal("versioned-key", LuminPackSerializer.Deserialize<Dictionary<VersionedStorage, string>>(keyBytes)[raw]);
        writer = new LuminPackWriter(ref span);
        offset = 0;
        writer.WriteCollectionHeader(1);
        writer.Write("versioned-value");
        LuminPackSerializer.Serialize(ref writer, in raw);
        byte[] valueBytes = LuminPackSerializer.Serialize(values);
        Assert.Equal(storage.AsSpan(0, writer.Position).ToArray(), valueBytes);
        Assert.Equal(107, LuminPackSerializer.Deserialize<Dictionary<string, VersionedStorage>>(valueBytes)["versioned-value"].Value);
    }

    /// <summary>Uses versioned slot framing despite having unmanaged CLR storage.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public struct VersionedStorage
    {
        /// <summary>Stores a scalar after two absent version slots.</summary>
        [LuminPackOrder(2)]
        public int Value;
    }

    /// <summary>Exposes padding whose bytes belong to the established raw value representation.</summary>
    [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct RawSideStorage
    {
        /// <summary>Stores the key identity.</summary>
        [FieldOffset(0)]
        public int Id;
        /// <summary>Stores a distant payload field.</summary>
        [FieldOffset(24)]
        public long Amount;
    }
}
