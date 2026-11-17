using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;

/// <summary>Protects bounded scalar facades independently of unchecked address-only storage kernels.</summary>
public sealed class RawScalarSpanSafetyTest
{
    /// <summary>Checks frozen and immutable dictionary interface writes with both string encodings.</summary>
    [Fact]
    public void DictionaryPairRunsKeepTheOriginalRecordSequence()
    {
        var frozen = Enumerable.Range(0, 69).ToFrozenDictionary(i => $"key-{i}",
            i => i % 7 == 0 ? null : i % 5 == 0 ? string.Empty : $"\u4F60-{i}");
        IReadOnlyDictionary<string, string?> view = frozen;
        IDictionary<string, string?> writableView = frozen;

        byte[] expected = WriteRecords<LuminPackUtf16LengthOptions>(frozen);
        Assert.Equal(expected, LuminPackSerializer.Serialize(frozen));
        Assert.Equal(expected, LuminPackSerializer.Serialize(view));
        Assert.Equal(expected, LuminPackSerializer.Serialize(writableView));

        expected = WriteRecords<LuminPackUtf8LengthOptions>(frozen);
        Assert.Equal(expected, LuminPackSerializer.Serialize<FrozenDictionary<string, string?>, LuminPackUtf8LengthOptions>(frozen));
        Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlyDictionary<string, string?>, LuminPackUtf8LengthOptions>(view));

        var immutable = frozen.ToImmutableSortedDictionary(Comparer<string>.Create((left, right) => string.CompareOrdinal(right, left)));
        IReadOnlyDictionary<string, string?> immutableView = immutable;
        Assert.Equal(WriteRecords<LuminPackUtf16LengthOptions>(immutable), LuminPackSerializer.Serialize(immutableView));
        Assert.Equal(WriteRecords<LuminPackUtf8LengthOptions>(immutable),
            LuminPackSerializer.Serialize<IReadOnlyDictionary<string, string?>, LuminPackUtf8LengthOptions>(immutableView));

        static byte[] WriteRecords<TOptions>(IReadOnlyDictionary<string, string?> dictionary) where TOptions : struct, ILuminPackOptions
        {
            byte[] destination = new byte[8192];
            Span<byte> span = destination;
            var writer = new LuminPackWriter(ref span);
            writer.WriteCollectionHeader(dictionary.Count);
            foreach (var pair in dictionary)
            {
                writer.Write<TOptions>(pair.Key);
                writer.Write<TOptions>(pair.Value);
            }
            return destination.AsSpan(0, writer.Position).ToArray();
        }
    }

    /// <summary>Checks that ordered concurrent reuse observes a key through the existing comparer before loading its value.</summary>
    [Fact]
    public void ConcurrentRawRefillKeepsComparerBeforeValueRead()
    {
        var input = new ConcurrentDictionary<uint, uint>(Enumerable.Range(0, 41)
            .Select(i => new KeyValuePair<uint, uint>((uint)i * 11, (uint)i + 200)));
        var comparer = new ObservingUIntComparer();
        ConcurrentDictionary<uint, uint>? target = new(input, comparer);
        byte[] bytes = LuminPackSerializer.Serialize(target);
        uint firstKey = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(sizeof(int)));
        comparer.ObserveEquality = (left, right) =>
        {
            if (left == firstKey && right == firstKey)
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2 * sizeof(int)), 168_430_090);
        };

        var existing = target;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(existing, target);
        Assert.Equal(168_430_090u, target![firstKey]);
    }

    /// <summary>Checks that a failed raw frozen read preserves its old result and rejects the first repeated key.</summary>
    [Fact]
    public void FrozenRawReadRejectsDuplicatesWithoutPublishing()
    {
        var original = Enumerable.Range(0, 41).ToFrozenDictionary(i => (uint)i * 11, i => (long)i * 37);
        FrozenDictionary<uint, long>? target = original;
        byte[] bytes = LuminPackSerializer.Serialize(original);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref target));
        Assert.Same(original, target);
        bytes.AsSpan(sizeof(int), sizeof(uint)).CopyTo(bytes.AsSpan(sizeof(int) + sizeof(uint) + sizeof(long)));
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(original, target);
    }

    /// <summary>Invokes the test observer from the dictionary's ordinary equality operation.</summary>
    private sealed class ObservingUIntComparer : IEqualityComparer<uint>
    {
        internal Action<uint, uint>? ObserveEquality;

        /// <inheritdoc />
        public bool Equals(uint left, uint right)
        {
            ObserveEquality?.Invoke(left, right);
            return left == right;
        }

        /// <inheritdoc />
        public int GetHashCode(uint value) => unchecked((int)value);
    }

    /// <summary>Checks counted writes from the same concurrent dictionary after removals and reinsertions.</summary>
    [Fact]
    public void ConcurrentRawPairsKeepCapturedOrder()
    {
        var dictionary = new ConcurrentDictionary<int, int>();
        for (int i = 0; i < 101; i++)
            dictionary[i * 11] = ~i;
        for (int i = 3; i < 101; i += 7)
            dictionary.TryRemove(i * 11, out _);

        int[] expected = dictionary.SelectMany(pair => new[] { pair.Key, pair.Value }).ToArray();
        byte[] bytes = LuminPackSerializer.Serialize(dictionary);
        Assert.Equal(expected.Length / 2, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(expected, MemoryMarshal.Cast<byte, int>(bytes.AsSpan(sizeof(int))).ToArray());
    }

    /// <summary>Checks both cached and uncached immutable queue runs and the concrete stack's wire order.</summary>
    [Fact]
    public void ImmutableInt32RunsKeepStackAndQueueOrder()
    {
        int[] items = Enumerable.Range(0, 4097).Select(i => unchecked(i * -1640531527)).ToArray();
        var stack = ImmutableStack.CreateRange(items);
        IImmutableStack<int> stackInterface = stack;
        int[] popOrder = items.Reverse().ToArray();
        Check(popOrder, LuminPackSerializer.Serialize(stack));
        Check(popOrder, LuminPackSerializer.Serialize(stackInterface));

        var queue = ImmutableQueue<int>.Empty;
        foreach (var item in items)
            queue = queue.Enqueue(item);
        IImmutableQueue<int> queueInterface = queue;
        Check(items, LuminPackSerializer.Serialize(queueInterface));
        int index = 0;
        foreach (var item in queue)
            Assert.Equal(items[index++], item);
        Check(items, LuminPackSerializer.Serialize(queueInterface));

        static void Check(int[] expected, byte[] bytes)
        {
            Assert.Equal(expected.Length, BinaryPrimitives.ReadInt32LittleEndian(bytes));
            Assert.Equal(expected, MemoryMarshal.Cast<byte, int>(bytes.AsSpan(sizeof(int))).ToArray());
        }
    }

    /// <summary>Checks explicit pair order across SIMD tails, removed entries and immutable tree staging.</summary>
    [Fact]
    public void CapturedRawDictionariesKeepTheirEnumerationOrder()
    {
        foreach (int count in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 101 })
        {
            var reverse = Comparer<int>.Create((left, right) => right.CompareTo(left));
            var list = new SortedList<int, int>(reverse);
            var dictionary = new Dictionary<int, int>();
            for (int i = 0; i < count; i++)
            {
                list.Add(i, unchecked(i * -1640531527));
                dictionary.Add(i, list[i]);
            }

            var dense = new ReadOnlyDictionary<int, int>(dictionary);
            Check(dense, LuminPackSerializer.Serialize(dense));
            if (count > 2)
                dictionary.Remove(count / 2);
            Check(dense, LuminPackSerializer.Serialize(dense));
            Check(list, LuminPackSerializer.Serialize(list));

            var tree = new SortedDictionary<int, int>(list, reverse);
            var immutable = dictionary.ToImmutableDictionary();
            var ordered = list.ToImmutableSortedDictionary(reverse);
            IImmutableDictionary<int, int> map = immutable;
            IImmutableDictionary<int, int> sorted = ordered;
            Check(tree, LuminPackSerializer.Serialize(tree));
            Check(immutable, LuminPackSerializer.Serialize(immutable));
            Check(ordered, LuminPackSerializer.Serialize(ordered));
            Check(map, LuminPackSerializer.Serialize(map));
            Check(sorted, LuminPackSerializer.Serialize(sorted));
            Check(immutable, LuminPackSerializer.Serialize<IReadOnlyDictionary<int, int>>(immutable));
            Check(ordered, LuminPackSerializer.Serialize<IDictionary<int, int>>(ordered));
        }

        static void Check(IReadOnlyDictionary<int, int> pairs, byte[] serialized)
        {
            byte[] expected = new byte[sizeof(int) + pairs.Count * 2 * sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, pairs.Count);
            var payload = MemoryMarshal.Cast<byte, int>(expected.AsSpan(sizeof(int)));
            int index = 0;
            foreach (var pair in pairs)
            {
                payload[index++] = pair.Key;
                payload[index++] = pair.Value;
            }
            Assert.Equal(expected, serialized);
        }
    }

    /// <summary>Checks padded pairs, exact windows, overlap rejection and unaligned vector tails.</summary>
    [Fact]
    public void PaddedTreePairsTransferOnlyWireFields()
    {
        bool lanes = IntPtr.Size == 8 && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported);
        bool wide = lanes && (Avx2.IsSupported || Avx512BW.IsSupported);
        foreach (int count in new[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 255, 256, 257, 513 })
        {
            Check(Enumerable.Range(0, count).Select(index => KeyValuePair.Create((ushort)(index * 127), (byte)(index * 7))).ToArray(), lanes);
            Check(Enumerable.Range(0, count).Select(index => KeyValuePair.Create((ulong)index * 0xAABBCCDD, unchecked((uint)(index * -62101)))).ToArray(),
                wide && count >= 256);
        }

        static void Check<TKey, TValue>(KeyValuePair<TKey, TValue>[] pairs, bool expectedTransfer)
            where TKey : unmanaged where TValue : unmanaged
        {
            int keyWidth = System.Runtime.CompilerServices.Unsafe.SizeOf<TKey>();
            int width = keyWidth + System.Runtime.CompilerServices.Unsafe.SizeOf<TValue>();
            int bytes = pairs.Length * width;
            var buffer = new byte[bytes + 2];
            buffer[0] = 0x13;
            buffer[^1] = 0x31;
            Span<byte> output = buffer.AsSpan(1, bytes);
            var writer = new LuminPackWriter(ref output);
            Assert.Equal(expectedTransfer, LuminTreeStorage.TryWritePackedPairs(ref writer, pairs, pairs.Length));
            Assert.Equal(expectedTransfer ? bytes : 0, writer.Position);
            Assert.Equal(0x13, buffer[0]);
            Assert.Equal(0x31, buffer[^1]);
            if (expectedTransfer)
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    Assert.Equal(pairs[i].Key, MemoryMarshal.Read<TKey>(output.Slice(i * width)));
                    Assert.Equal(pairs[i].Value, MemoryMarshal.Read<TValue>(output.Slice(i * width + keyWidth)));
                }
            }

            var destination = new KeyValuePair<TKey, TValue>[pairs.Length];
            ReadOnlySpan<byte> truncated = output.Slice(0, bytes - 1);
            var shortReader = new LuminPackReader(ref truncated);
            Assert.False(LuminTreeStorage.TryReadPackedPairs(ref shortReader, destination, pairs.Length));
            Assert.Equal(0, shortReader.Position);
            Assert.All(destination, pair => Assert.Equal(default, pair));

            ReadOnlySpan<byte> input = output;
            var reader = new LuminPackReader(ref input);
            Assert.Equal(expectedTransfer, LuminTreeStorage.TryReadPackedPairs(ref reader, destination, pairs.Length));
            Assert.Equal(expectedTransfer ? bytes : 0, reader.Position);
            if (expectedTransfer)
                Assert.Equal(pairs, destination);

            ReadOnlySpan<byte> alias = MemoryMarshal.AsBytes(pairs.AsSpan());
            var aliasedReader = new LuminPackReader(ref alias);
            Assert.False(LuminTreeStorage.TryReadPackedPairs(ref aliasedReader, pairs, pairs.Length));
            Assert.Equal(0, aliasedReader.Position);
        }
    }

    /// <summary>Checks both bounded pair helpers before they write into a short output window.</summary>
    [Fact]
    public void CapturedRawPairsLeaveShortOutputUntouched()
    {
        const int count = 65;
        var keys = Enumerable.Range(0, count).Select(i => i * 5L).ToArray();
        var values = Enumerable.Range(0, count).Select(i => ~i * 13L).ToArray();
        var pairs = keys.Zip(values, (key, value) => KeyValuePair.Create(key, value)).ToArray();
        var buffer = Enumerable.Repeat((byte)0xAC, count * 2 * sizeof(long)).ToArray();
        Span<byte> shortOutput = buffer.AsSpan(1);
        var writer = new LuminPackWriter(ref shortOutput);
        Assert.False(LuminFrozenConstruction.TryWriteInterleavedPairs(ref writer, keys, values, count));
        Assert.False(LuminTreeStorage.TryWritePackedPairs(ref writer, pairs, count));
        Assert.Equal(0, writer.Position);
        Assert.All(buffer, value => Assert.Equal(0xAC, value));
    }

    /// <summary>Checks generic raw paths with unsigned, narrow, wide and unequal entry widths.</summary>
    [Fact]
    public void RawCollectionRunsUseStorageWidths()
    {
        var narrow = new SortedList<byte, sbyte>();
        var medium = new SortedList<ushort, short>();
        var packed = new SortedList<RawPairKey, ulong>();
        var concurrent = new ConcurrentDictionary<uint, ulong>();
        for (int i = 0; i < 101; i++)
        {
            narrow.Add((byte)i, unchecked((sbyte)(i * -5)));
            medium.Add((ushort)(i * 307), unchecked((short)(i * -431)));
            packed.Add(new RawPairKey { Major = (uint)i, Minor = ~(uint)i }, (ulong)i * 0xF0000001);
            concurrent.TryAdd((uint)i * 7919, (ulong)i * 0xF0000001);
        }
        var wrapper = new ReadOnlyDictionary<uint, ulong>(concurrent.ToDictionary(pair => pair.Key, pair => pair.Value));
        var immutable = concurrent.ToImmutableDictionary();
        IImmutableDictionary<uint, ulong> map = immutable;
        var narrowBytes = LuminPackSerializer.Serialize(narrow);
        var mediumBytes = LuminPackSerializer.Serialize(medium);
        var packedBytes = LuminPackSerializer.Serialize(packed);
        Check(narrow, narrowBytes);
        Check(medium, mediumBytes);
        Check(packed, packedBytes);
        var frozen = packed.ToFrozenDictionary();
        Check(frozen, LuminPackSerializer.Serialize(frozen));
        Check(concurrent, LuminPackSerializer.Serialize(concurrent));
        Check(wrapper, LuminPackSerializer.Serialize(wrapper));
        Check(immutable, LuminPackSerializer.Serialize(immutable));
        Check(map, LuminPackSerializer.Serialize(map));
        SortedList<byte, sbyte>? narrowResult = null;
        SortedList<ushort, short>? mediumResult = null;
        SortedList<RawPairKey, ulong>? packedResult = null;
        Assert.Equal(narrowBytes.Length, LuminPackSerializer.Deserialize(narrowBytes, ref narrowResult));
        Assert.Equal(mediumBytes.Length, LuminPackSerializer.Deserialize(mediumBytes, ref mediumResult));
        Assert.Equal(packedBytes.Length, LuminPackSerializer.Deserialize(packedBytes, ref packedResult));
        Assert.Equal(narrow.ToArray(), narrowResult!.ToArray());
        Assert.Equal(medium.ToArray(), mediumResult!.ToArray());
        Assert.Equal(packed.ToArray(), packedResult!.ToArray());

        static void Check<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> expected, byte[] bytes)
            where TKey : unmanaged where TValue : unmanaged
        {
            Assert.Equal(expected.Count, BinaryPrimitives.ReadInt32LittleEndian(bytes));
            var payload = bytes.AsSpan(sizeof(int));
            foreach (var pair in expected)
            {
                Assert.Equal(pair.Key, MemoryMarshal.Read<TKey>(payload));
                payload = payload.Slice(Marshal.SizeOf<TKey>());
                Assert.Equal(pair.Value, MemoryMarshal.Read<TValue>(payload));
                payload = payload.Slice(Marshal.SizeOf<TValue>());
            }
            Assert.True(payload.IsEmpty);
        }
    }

    /// <summary>Checks each reuse policy when a raw pair payload reaches the end of a bounded input.</summary>
    [Fact]
    public void StagedRawPairReadsPreserveFailurePublication()
    {
        var source = new Dictionary<uint, uint>();
        for (uint i = 0; i < 101; i++)
            source.Add(i, unchecked(i * 2654435761));
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Dictionary<uint, uint>? fresh = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref fresh));
        Assert.Equal(source.ToArray(), fresh!.ToArray());

        var target = new Dictionary<uint, uint> { [2049] = 8193 };
        Dictionary<uint, uint>? reused = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref reused));
        Assert.Same(target, reused);
        Assert.Equal(8193U, Assert.Single(target).Value);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref reused));
        Assert.Same(target, reused);
        Assert.Equal(source.ToArray(), target.ToArray());

        var ordered = new SortedDictionary<uint, uint> { [2049] = 8193 };
        SortedDictionary<uint, uint>? reusedTree = ordered;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(0, bytes.Length - 1), ref reusedTree));
        Assert.Same(ordered, reusedTree);
        Assert.Empty(ordered);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref reusedTree));
        Assert.Same(ordered, reusedTree);
        Assert.Equal(source.ToArray(), ordered.ToArray());
    }

    /// <summary>Provides an ordinary user-defined unmanaged key with the complete native wire layout.</summary>
    [LuminPackable]
    public partial struct RawPairKey : IComparable<RawPairKey>
    {
        public uint Major;
        public uint Minor;

        /// <summary>Compares both unsigned fields in key order.</summary>
        /// <param name="other">The other key.</param>
        /// <returns>The comparison result.</returns>
        public readonly int CompareTo(RawPairKey other) => Major == other.Major ? Minor.CompareTo(other.Minor) : Major.CompareTo(other.Major);
    }

    /// <summary>Checks dense and sparse string set writes through the concrete and interface owners.</summary>
    [Fact]
    public void DenseStringSetsPreserveTheirReferenceOrder()
    {
        var set = Enumerable.Range(0, 101).Select(i => i % 4 == 0 ? "\u6F22\u5B57" + i : "Latin" + i).ToHashSet();
        set.Add(null!);
        set.Add(string.Empty);
        Check(set);
        set.Remove(set.First());
        Check(set);

        static void Check(HashSet<string> values)
        {
            var expected = new byte[8192];
            Span<byte> output = expected;
            var writer = new LuminPackWriter(ref output);
            writer.WriteCollectionHeader(values.Count);
            foreach (var value in values)
                writer.Write<LuminPackDefaultOptions>(value);
            byte[] bytes = output.Slice(0, writer.Position).ToArray();
            ISet<string> set = values;
            IReadOnlySet<string> readOnly = values;
            Assert.Equal(bytes, LuminPackSerializer.Serialize(values));
            Assert.Equal(bytes, LuminPackSerializer.Serialize(set));
            Assert.Equal(bytes, LuminPackSerializer.Serialize(readOnly));
        }
    }

    /// <summary>Compares generated frozen writers and unaligned reservations with explicit key/value wire order.</summary>
    [Fact]
    public void FrozenRawPairsKeepOrderAcrossVectorTails()
    {
        foreach (int count in new[] { 0, 1, 15, 16, 17, 31, 32, 33, 36, 39, 64, 65 })
        {
            var narrow = Enumerable.Range(0, count).ToFrozenDictionary(index => index * -7, index => index * 19 + 3);
            var wide = Enumerable.Range(0, count).ToFrozenDictionary(index => index * -5000000007L, index => index * 9000000011L + 5);
            Check(narrow, LuminPackSerializer.Serialize(narrow));
            Check(wide, LuminPackSerializer.Serialize(wide));
        }

        static void Check<T>(FrozenDictionary<T, T> frozen, byte[] serialized) where T : unmanaged
        {
            int width = Marshal.SizeOf<T>();
            byte[] expected = new byte[sizeof(int) + 2 * width * frozen.Count];
            BinaryPrimitives.WriteInt32LittleEndian(expected, frozen.Count);
            var pairs = MemoryMarshal.Cast<byte, T>(expected.AsSpan(sizeof(int)));
            int index = 0;
            foreach (var pair in frozen)
            {
                pairs[index++] = pair.Key;
                pairs[index++] = pair.Value;
            }
            Assert.Equal(expected, serialized);

            byte[] guarded = Enumerable.Repeat((byte)0xAC, expected.Length + 2).ToArray();
            Span<byte> output = guarded.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref output);
            LuminFrozenConstruction.WritePairs(ref writer, frozen.Keys.AsSpan(), frozen.Values.AsSpan());
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, output.ToArray());
            Assert.Equal((byte)0xAC, guarded[0]);
            Assert.Equal((byte)0xAC, guarded[^1]);
        }
    }

    /// <summary>Retains the scalar key-before-value load order for an overlapping caller-supplied span.</summary>
    /// <param name="overlapValues">Whether the values span overlaps the destination instead of the keys span.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenRawPairsKeepScalarOrderWhenInputOverlapsOutput(bool overlapValues)
    {
        const int count = 33;
        const int outputStart = 3;
        const int sourceStart = 29;
        var source = Enumerable.Range(0, count).Select(index => index + 1000).ToArray();
        var other = Enumerable.Range(0, count).Select(index => -index - 250).ToArray();
        byte[] storage = new byte[sizeof(int) + 2 * count * sizeof(int) + outputStart + 1];
        MemoryMarshal.AsBytes(source.AsSpan()).CopyTo(storage.AsSpan(sourceStart));
        byte[] expected = (byte[])storage.Clone();
        Span<int> prior = MemoryMarshal.Cast<byte, int>(expected.AsSpan(sourceStart, count * sizeof(int)));
        Span<int> packed = MemoryMarshal.Cast<byte, int>(expected.AsSpan(outputStart, (1 + 2 * count) * sizeof(int)));
        packed[0] = count;
        for (int index = 0; index < count; index++)
        {
            packed[1 + 2 * index] = overlapValues ? other[index] : prior[index];
            packed[2 + 2 * index] = overlapValues ? prior[index] : other[index];
        }

        Span<byte> output = storage.AsSpan(outputStart, (1 + 2 * count) * sizeof(int));
        Span<int> aliased = MemoryMarshal.Cast<byte, int>(storage.AsSpan(sourceStart, count * sizeof(int)));
        var writer = new LuminPackWriter(ref output);
        LuminFrozenConstruction.WritePairs<int, int>(ref writer, overlapValues ? other : aliased, overlapValues ? aliased : other);
        Assert.Equal(expected, storage);
    }

    /// <summary>Checks dense and sparse set wire order through the concrete and both set interface writers.</summary>
    [Fact]
    public void DenseRawHashSetKeepsWireOrderThroughInterfaces()
    {
        foreach (int count in new[] { 0, 15, 16, 17, 19, 23, 24, 25, 33, 100 })
        {
            var set = new HashSet<int>(count);
            for (int index = 0; index < count; index++)
                set.Add(unchecked(index * -1640531527));
            Check(set);
            if (count > 4)
            {
                set.Remove(0);
                set.Remove(unchecked(3 * -1640531527));
                Check(set);
            }
        }

        static void Check(HashSet<int> set)
        {
            byte[] expected = new byte[sizeof(int) * (set.Count + 1)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, set.Count);
            int offset = sizeof(int);
            foreach (int item in set)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset), item);
                offset += sizeof(int);
            }

            Assert.Equal(expected, LuminPackSerializer.Serialize(set));
            Assert.Equal(expected, LuminPackSerializer.Serialize<ISet<int>>(set));
            Assert.Equal(expected, LuminPackSerializer.Serialize<IReadOnlySet<int>>(set));
            byte[] guarded = Enumerable.Repeat((byte)0xD7, expected.Length + 2).ToArray();
            Span<byte> output = guarded.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref output);
            LuminPack.Code.LuminPackMarshal.WriteHashSetRaw(ref writer, set);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, output.ToArray());
            Assert.Equal((byte)0xD7, guarded[0]);
            Assert.Equal((byte)0xD7, guarded[^1]);
        }
    }

    /// <summary>Checks raw dictionary pairs across vector widths, tails, and unaligned output positions.</summary>
    [Fact]
    public void DenseRawDictionaryKeepsPackedKeyValueOrder()
    {
        for (int count = 0; count <= 19; count++)
        {
            var values = new Dictionary<int, int>(count);
            for (int index = 0; index < count; index++)
                values.Add(-index - 19, index * 787 + 41);

            byte[] expected = new byte[sizeof(int) + count * 2 * sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(expected, count);
            int offset = sizeof(int);
            foreach (var pair in values)
            {
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset), pair.Key);
                BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset + sizeof(int)), pair.Value);
                offset += 2 * sizeof(int);
            }

            Assert.Equal(expected, LuminPackSerializer.Serialize(values));
            byte[] storage = Enumerable.Repeat((byte)0xB9, expected.Length + 2).ToArray();
            Span<byte> buffer = storage.AsSpan(1, expected.Length);
            var writer = new LuminPackWriter(ref buffer);
            writer.WriteCollectionHeader(count);
            LuminPack.Code.LuminPackMarshal.WriteDenseRawPairs(ref writer, values, count);
            Assert.Equal(expected.Length, writer.Position);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal((byte)0xB9, storage[0]);
            Assert.Equal((byte)0xB9, storage[^1]);
        }
    }

    /// <summary>Preserves optional framing, compressed bytes, raw storage and exact write boundaries.</summary>
    [Fact]
    public void RawCollectionWritesKeepCompressionAndHeaderContracts()
    {
        foreach (int count in new[] { 0, 1, 17, 1024 })
        foreach (bool header in new[] { false, true })
        foreach (bool compressed in new[] { false, true })
        {
            int[] values = Enumerable.Range(0, count).Select(index => index % 7 - 3).ToArray();
            int capacity = LuminPackWriter.Size<int>(count, header, compressed);
            byte[] storage = Enumerable.Repeat((byte)0xCD, capacity + 16).ToArray();
            int written = LuminPackWriter.Write<int>(storage.AsSpan(8, capacity), values, count, header, compressed);
            Assert.InRange(written, 0, capacity);
            Assert.All(storage[..8], value => Assert.Equal((byte)0xCD, value));
            Assert.All(storage[(8 + written)..], value => Assert.Equal((byte)0xCD, value));
            int[]? restored = null;
            int consumed = LuminPackReader.Read<int>(storage.AsSpan(8, written), ref restored, count, header, compressed);
            Assert.Equal(written, consumed);
            Assert.Equal(values, restored);
            if (!compressed)
            {
                int prefix = header ? sizeof(int) : 0;
                Assert.Equal(prefix + count * sizeof(int), written);
                Assert.True(MemoryMarshal.AsBytes(values.AsSpan()).SequenceEqual(storage.AsSpan(8 + prefix, count * sizeof(int))));
            }
        }
    }

    /// <summary>Rejects every shortened input window before mutating any supported scalar width.</summary>
    [Fact]
    public void ConcreteScalarEntriesValidateTheirCompleteWindow()
    {
        Check<byte>(0x77, 0x11, static (ReadOnlySpan<byte> bytes, ref byte value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<short>(0x7777, 0x1122, static (ReadOnlySpan<byte> bytes, ref short value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<int>(0x77777777, 0x11223344, static (ReadOnlySpan<byte> bytes, ref int value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<long>(long.MaxValue, long.MinValue + 17, static (ReadOnlySpan<byte> bytes, ref long value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Guid>(Guid.Empty, new Guid("e50e71a5-4367-4ba1-98d5-a0d6e3e89a19"), static (ReadOnlySpan<byte> bytes, ref Guid value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<decimal>(decimal.MaxValue, decimal.MinValue, static (ReadOnlySpan<byte> bytes, ref decimal value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Vector4>(Vector4.Zero, new Vector4(1, -2, 3, -4), static (ReadOnlySpan<byte> bytes, ref Vector4 value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Matrix3x2>(Matrix3x2.Identity, new Matrix3x2(1, 2, 3, 4, 5, 6), static (ReadOnlySpan<byte> bytes, ref Matrix3x2 value) => LuminPackSerializer.Deserialize(bytes, ref value));
        Check<Matrix4x4>(Matrix4x4.Identity, Matrix4x4.CreateScale(137), static (ReadOnlySpan<byte> bytes, ref Matrix4x4 value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Keeps explicit profile and generic entries bounded as well as the concrete default facade.</summary>
    [Fact]
    public void ProfileAndGenericEntriesValidateBeforeAssignment()
    {
        Check<Guid>(Guid.Empty, new Guid("7f3311a7-42b2-47c1-9569-a7c6a10ad5f7"), static (ReadOnlySpan<byte> bytes, ref Guid value) => LuminPackSerializer.Deserialize<Guid, LuminPackUtf8TokenOptions>(bytes, ref value));
        Check<int>(int.MaxValue, int.MinValue, static (ReadOnlySpan<byte> bytes, ref int value) => LuminPackSerializer.Deserialize(bytes, ref value));
    }

    /// <summary>Rejects managed-reference storage before a raw collection API can copy pointer bytes.</summary>
    [Fact]
    public void RawCollectionsRejectManagedReferences()
    {
        byte[] input = new byte[IntPtr.Size];
        string[] target = ["sentinel"];
        object original = target;
        Assert.ThrowsAny<Exception>(() => LuminPackReader.Read(input, ref target, 1, false, false));
        Assert.Same(original, target);
        Assert.Equal("sentinel", target[0]);
        byte[] output = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        Assert.ThrowsAny<Exception>(() => LuminPackWriter.Write<string>(output, target.AsSpan(), 1, false, false));
        Assert.All(output, value => Assert.Equal((byte)0xA5, value));
        WithReference[] nested = [new() { Value = original }];
        Assert.ThrowsAny<Exception>(() => LuminPackReader.Read(input, ref nested, 1, false, false));
        Assert.Same(original, nested[0].Value);
    }

    /// <summary>Represents a value type whose raw bytes include a managed object reference.</summary>
    private struct WithReference
    {
        internal object? Value;
    }

    /// <summary>Reads one concrete generated bounded entry.</summary>
    /// <typeparam name="T">The raw scalar storage type.</typeparam>
    /// <param name="bytes">The retained input window.</param>
    /// <param name="value">The destination storage.</param>
    /// <returns>The complete consumed record width.</returns>
    private delegate int ReadScalar<T>(ReadOnlySpan<byte> bytes, ref T value);

    /// <summary>Places complete backing storage behind truncated windows and checks exact destination bytes.</summary>
    /// <typeparam name="T">The reference-free scalar storage type.</typeparam>
    /// <param name="initial">The destination before the read.</param>
    /// <param name="expected">The independently formed complete wire value.</param>
    /// <param name="read">The concrete facade selected by the fixture.</param>
    private static void Check<T>(T initial, T expected, ReadScalar<T> read) where T : unmanaged
    {
        byte[] wire = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref expected, 1)).ToArray();
        byte[] original = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref initial, 1)).ToArray();
        byte[] backing = new byte[wire.Length + 17];
        wire.CopyTo(backing, 0);
        for (int length = 0; length < wire.Length; length++)
        {
            T target = initial;
            Assert.ThrowsAny<Exception>(() => read(backing.AsSpan(0, length), ref target));
            Assert.Equal(original, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref target, 1)).ToArray());
        }
        T complete = initial;
        Assert.Equal(wire.Length, read(backing, ref complete));
        Assert.Equal(wire, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref complete, 1)).ToArray());
    }
}
