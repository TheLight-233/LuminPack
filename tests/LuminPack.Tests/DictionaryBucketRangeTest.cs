using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace LuminPackUnitTest;
/// <summary>Protects bucket placement and duplicate rejection around the unsigned remainder shortcut.</summary>
public class DictionaryBucketRangeTest
{
    /// <summary>Checks that batched dictionary writes exclude entry alignment gaps but preserve raw value padding.</summary>
    [Fact]
    public void DensePairsRetainIndependentStorageWidths()
    {
        var mixed = new Dictionary<byte, long>
        {
            [1] = long.MaxValue,
            [2] = long.MinValue,
            [3] = 71
        };
        byte[] mixedBytes = LuminPackSerializer.Serialize(mixed);
        VerifyPairBytes(mixed, mixedBytes);
        Assert.Equal(mixed.ToArray(), LuminPackSerializer.Deserialize<Dictionary<byte, long>>(mixedBytes).ToArray());
        for (int prefix = 0; prefix < mixedBytes.Length; prefix++)
        {
            byte[] truncated = mixedBytes.AsSpan(0, prefix).ToArray();
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<Dictionary<byte, long>>(truncated));
        }

        PaddedPairValue raw = default;
        Unsafe.InitBlockUnaligned(ref Unsafe.As<PaddedPairValue, byte>(ref raw), 0xA5, 16);
        raw.Tag = 3;
        raw.Number = -987654321;
        var padded = new Dictionary<int, PaddedPairValue>
        {
            [11] = raw,
            [13] = raw
        };
        byte[] paddedBytes = LuminPackSerializer.Serialize(padded);
        VerifyPairBytes(padded, paddedBytes);
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, PaddedPairValue>>(paddedBytes);
        Assert.Equal(paddedBytes, LuminPackSerializer.Serialize(copy));
        mixed.Remove(2);
        VerifyPairBytes(mixed, LuminPackSerializer.Serialize(mixed));
        padded.Remove(11);
        VerifyPairBytes(padded, LuminPackSerializer.Serialize(padded));
    }

    /// <summary>Builds expected bytes through the original independent key and value cores.</summary>
    /// <typeparam name = "TKey">The raw key storage.</typeparam>
    /// <typeparam name = "TValue">The raw value storage.</typeparam>
    /// <param name = "source">The dictionary in its existing enumeration order.</param>
    /// <param name = "actual">The generated writer's output.</param>
    private static void VerifyPairBytes<TKey, TValue>(Dictionary<TKey, TValue> source, byte[] actual)
        where TKey : unmanaged where TValue : unmanaged
    {
        Span<byte> span = new byte[1024];
        var writer = new LuminPackWriter(ref span);
        int index = 0;
        writer.WriteCollectionHeader(source.Count);
        foreach (var pair in source)
        {
            index = writer.Position;
            writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), pair.Key));
            index = writer.Position;
            writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)index), pair.Value));
        }

        Assert.Equal(span[..writer.Position].ToArray(), actual);
    }

    /// <summary>Exposes explicit padding that must remain part of an unmanaged value's representation.</summary>
    [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PaddedPairValue
    {
        [FieldOffset(0)]
        public byte Tag;
        [FieldOffset(8)]
        public long Number;
    }

    /// <summary>Checks small hashes, boundary hashes, negative hashes, and collision chains against normal dictionary lookup.</summary>
    [Fact]
    public void SmallAndLargeHashesPreserveDictionaryLookups()
    {
        var source = new Dictionary<int, int>();
        foreach (int key in new[]
        {
            0,
            1,
            2,
            3,
            16,
            17,
            18,
            34,
            51,
            1009,
            int.MaxValue,
            -1,
            int.MinValue
        }

        )
            source.Add(key, unchecked(key * 31 + 7));
        var copy = LuminPackSerializer.Deserialize<Dictionary<int, int>>(LuminPackSerializer.Serialize(source));
        Assert.Equal(source.Count, copy.Count);
        foreach (var pair in source)
            Assert.Equal(pair.Value, copy[pair.Key]);
        copy.Add(91, 857);
        Assert.Equal(857, copy[91]);
        Assert.True(copy.Remove(17));
        Assert.False(copy.ContainsKey(17));
    }

    /// <summary>Checks that colliding and repeated small hashes retain the normal duplicate-key failure.</summary>
    [Fact]
    public void DuplicateSmallHashesStillRejectDuplicateKeys()
    {
        byte[] payload = new byte[20];
        BitConverter.TryWriteBytes(payload.AsSpan(0), 2);
        BitConverter.TryWriteBytes(payload.AsSpan(4), 1);
        BitConverter.TryWriteBytes(payload.AsSpan(8), 11);
        BitConverter.TryWriteBytes(payload.AsSpan(12), 1);
        BitConverter.TryWriteBytes(payload.AsSpan(16), 13);
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<int, int>>(payload));
    }
    /// <summary>Preserves immutable keys, enumeration version effects and staged failure publication for scalar refills.</summary>
    [Fact]
    public void StringScalarRefillsRetainKeysAndCheckedFailurePublication()
    {
        CheckStringScalarRefill<LuminPack.Option.LuminPackUtf16LengthOptions>();
        CheckStringScalarRefill<LuminPack.Option.LuminPackUtf8LengthOptions>();
        CheckStringScalarRefill<LuminPack.Option.LuminPackUtf16TokenOptions>();
        CheckStringScalarRefill<LuminPack.Option.LuminPackUtf8TokenOptions>();
    }

    /// <summary>Exercises complete generated refills, changed keys, comparer exclusions, duplicates and every truncated prefix.</summary>
    /// <typeparam name="TOptions">The original complete binary string profile.</typeparam>
    private static void CheckStringScalarRefill<TOptions>() where TOptions : struct, LuminPack.Option.ILuminPackOptions
    {
        string[] keys = [new("first_key".ToCharArray()), new("key_\u4E2D".ToCharArray()), new("key_\uD83D\uDE03".ToCharArray())];
        var source = keys.Select((key, index) => new KeyValuePair<string, long>(key, index + 71)).ToDictionary();
        var target = keys.ToDictionary(key => key, key => 0L);
        var original = target;
        byte[] payload = LuminPackSerializer.Serialize<Dictionary<string, long>, TOptions>(source);
        var enumerator = target.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(payload.Length, LuminPackSerializer.Deserialize<Dictionary<string, long>, TOptions>(payload, ref target));
        Assert.Same(original, target);
        Assert.Equal(source.ToArray(), target.ToArray());
        for (int index = 0; index < keys.Length; index++) Assert.Same(keys[index], target.Keys.ElementAt(index));
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        target.Add("later", 811);
        Assert.True(target.Remove("later"));
        target.TrimExcess();
        Assert.Equal(source.ToArray(), target.ToArray());

        foreach (var changed in new[]
        {
            source.Reverse().ToDictionary(),
            source.Select(pair => new KeyValuePair<string, long>("changed_" + pair.Key, pair.Value + 1)).ToDictionary()
        })
        {
            byte[] changedPayload = LuminPackSerializer.Serialize<Dictionary<string, long>, TOptions>(changed);
            Assert.Equal(changedPayload.Length, LuminPackSerializer.Deserialize<Dictionary<string, long>, TOptions>(changedPayload, ref target));
            Assert.Same(original, target);
            Assert.Equal(changed.ToArray(), target.ToArray());
        }

        var custom = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["FIRST_KEY"] = -1 };
        var comparer = custom.Comparer;
        LuminPackSerializer.Deserialize<Dictionary<string, long>, TOptions>(payload, ref custom);
        Assert.Same(comparer, custom.Comparer);
        Assert.Equal(source.ToArray(), custom.ToArray());

        var incomplete = keys.ToDictionary(key => key, key => -11L);
        var before = incomplete.ToArray();
        for (int width = 0; width < payload.Length; width++)
        {
            byte[] truncated = payload.AsSpan(0, width).ToArray();
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<Dictionary<string, long>, TOptions>(truncated, ref incomplete));
            Assert.Equal(before, incomplete.ToArray());
        }

        var duplicateTarget = new Dictionary<string, long> { ["first"] = -1, ["second"] = -2 };
        byte[] duplicate = DuplicateStringScalarPayload<TOptions>();
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<string, long>, TOptions>(duplicate, ref duplicateTarget));
        Assert.Single(duplicateTarget);
        Assert.Equal(71, duplicateTarget["first"]);
    }

    /// <summary>Builds a literal duplicate-key record through the existing complete scalar/string kernels.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    /// <returns>A two-entry dictionary record with duplicate keys.</returns>
    private static unsafe byte[] DuplicateStringScalarPayload<TOptions>() where TOptions : struct, LuminPack.Option.ILuminPackOptions
    {
        byte[] storage = new byte[256];
        fixed (byte* pinned = storage)
        {
            Span<byte> output = new Span<byte>(pinned, storage.Length);
            var writer = new LuminPack.Core.LuminPackWriter(ref output);
            writer.Write(2);
            writer.Write<TOptions>("first"); writer.Write(71L);
            writer.Write<TOptions>("first"); writer.Write(73L);
            return output[..writer.CurrentIndex].ToArray();
        }
    }
    /// <summary>Retains public Add collision randomization after a capacity-only rehash creates a long bucket chain.</summary>
    [Fact]
    public void StableStringKeysRetainCapacityGrowthCollisionPolicy()
    {
        if (IntPtr.Size != 8) return;
        int capacity = new Dictionary<string, long>().EnsureCapacity(1600);
        var seed = new Dictionary<string, long>(211);
        var comparer = LuminPack.Code.LuminPackMarshal.GetDictionaryView(seed)._comparer;
        if (comparer is null || comparer.GetType() == EqualityComparer<string>.Default.GetType()) return;
        var keys = new List<string>();
        for (int value = 0; keys.Count < 102; value++)
        {
            string key = "collision_" + value;
            if (unchecked((uint)comparer.GetHashCode(key)) % (uint)capacity == 0) keys.Add(key);
        }
        var destination = new Dictionary<string, long>(211);
        foreach (string key in keys) destination.Add(key, -1);
        destination.EnsureCapacity(capacity);
        var source = keys.Select((key, index) => new KeyValuePair<string, long>(key, index + 71)).ToDictionary();
        var expected = new Dictionary<string, long>(destination);
        expected.EnsureCapacity(capacity);
        expected.Clear();
        foreach (var pair in source) expected.Add(pair.Key, pair.Value);
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref destination));
        Assert.Equal(expected.ToArray(), destination.ToArray());
        Assert.Equal(LuminPack.Code.LuminPackMarshal.GetDictionaryView(expected)._comparer?.GetType(),
            LuminPack.Code.LuminPackMarshal.GetDictionaryView(destination)._comparer?.GetType());
        destination.Add("later", 29);
        Assert.True(destination.Remove("later"));
        destination.TrimExcess();
        Assert.Equal(source.ToArray(), destination.ToArray());
    }

}
