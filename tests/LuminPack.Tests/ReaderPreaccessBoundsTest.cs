using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that advancing fixed reads reject short windows before accessing or changing storage.</summary>
public sealed class ReaderPreaccessBoundsTest
{
    /// <summary>Checks a packed struct rejects every short logical window before changing caller storage.</summary>
    [Fact]
    public void PackedStructRejectsEveryPrefixBeforeDestinationPublication()
    {
        byte[] backing = LuminPackSerializer.Serialize(new PackedReadBoundary { Tag = 137, Value = 0x123456789ABCDEF });
        for (int length = 0; length < backing.Length; length++)
        {
            var target = new PackedReadBoundary { Tag = 71, Value = 719 };
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
            Assert.Equal((byte)71, target.Tag);
            Assert.Equal(719L, target.Value);
        }
    }
    /// <summary>Keeps complete pair destinations unchanged when either scalar projection sees a short input.</summary>
    [Fact]
    public void PairScalarsRejectEveryPrefixBeforeDestinationPublication()
    {
        var narrow = new KeyValuePair<byte, string>(137, "incoming");
        byte[] backing = LuminPackSerializer.Serialize(narrow);
        string text = new('z', 7);
        for (int length = 0; length < backing.Length; length++)
        {
            var target = new KeyValuePair<byte, string>(71, text);
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
            Assert.Equal((byte)71, target.Key);
            Assert.Same(text, target.Value);
        }
        var wide = new KeyValuePair<Guid, string>(Guid.Parse("2ED4AAC7-847C-4BB5-8EC4-E0212F6ABD91"), "incoming");
        backing = LuminPackSerializer.Serialize(wide);
        for (int length = 0; length < backing.Length; length++)
        {
            var target = new KeyValuePair<Guid, string>(Guid.Empty, text);
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
            Assert.Equal(Guid.Empty, target.Key);
            Assert.Same(text, target.Value);
        }
    }
    /// <summary>Keeps a scalar destination and cursor unchanged when only part of its storage is readable.</summary>
    [Fact]
    public unsafe void ScalarDestinationIsUnchangedBeforeFailedAdvance()
    {
        byte[] backing = [137, 0, 0, 0];
        fixed (byte* pinned = backing)
        {
            ReadOnlySpan<byte> input = backing.AsSpan(0, 1);
            var reader = new LuminPackReader(ref input);
            int destination = 719;
            Exception? failure = null;
            try { reader.Read(ref destination); }
            catch (Exception error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(719, destination);
            Assert.Equal(0, reader.Position);
        }
    }

    /// <summary>Protects large scalar storage and the 32-bit block-copy destination before an incomplete read.</summary>
    [Fact]
    public void WideScalarDestinationsAreUnchangedOnTruncatedInput()
    {
        CheckWideScalar(137L, 719L);
        CheckWideScalar(137m, 719m);
        CheckWideScalar(Guid.Parse("2ED4AAC7-847C-4BB5-8EC4-E0212F6ABD91"), Guid.Empty);
    }

    /// <summary>Checks every shortened window while keeping its complete backing allocation pinned.</summary>
    /// <typeparam name="T">The unmanaged scalar storage.</typeparam>
    /// <param name="source">The bytes present beyond the shortened input window.</param>
    /// <param name="original">The destination value that must survive rejection.</param>
    private static unsafe void CheckWideScalar<T>(T source, T original) where T : unmanaged
    {
        byte[] backing = MemoryMarshal.AsBytes(new[] { source }.AsSpan()).ToArray();
        fixed (byte* pinned = backing)
        {
            for (int length = 0; length < backing.Length; length++)
            {
                ReadOnlySpan<byte> input = backing.AsSpan(0, length);
                var reader = new LuminPackReader(ref input);
                T destination = original;
                Exception? failure = null;
                try { reader.Read(ref destination); }
                catch (Exception error) { failure = error; }
                Assert.NotNull(failure);
                Assert.Equal(original, destination);
                Assert.Equal(0, reader.Position);
            }
        }
    }

    /// <summary>Validates each complete fixed field before assigning an existing class member.</summary>
    /// <param name="length">The shortened record length including the object header.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SingleFieldIsUnchangedOnTruncatedInput(int length)
    {
        byte[] backing = LuminPackSerializer.Serialize(new ReaderPreaccessScalar { Number = 137 });
        var target = new ReaderPreaccessScalar { Number = 719 };
        var original = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
        Assert.Same(original, target);
        Assert.Equal(719, target.Number);
    }

    /// <summary>Keeps every member of an address-proof fallback batch intact until its full input is readable.</summary>
    /// <param name="length">The shortened record length including the object header.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(12)]
    public void NonadjacentBatchIsUnchangedOnTruncatedInput(int length)
    {
        byte[] backing = LuminPackSerializer.Serialize(new ReaderPreaccessGap { Number = 137, Count = 271 });
        var target = new ReaderPreaccessGap { Number = 719, Count = 981 };
        var original = target;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
        Assert.Same(original, target);
        Assert.Equal(719, target.Number);
        Assert.Equal(981, target.Count);
    }

    /// <summary>Keeps an existing nullable value until its complete unmanaged payload can be read.</summary>
    [Fact]
    public void NullableIsUnchangedOnTruncatedInput()
    {
        int? source = 137;
        byte[] backing = LuminPackSerializer.Serialize(source);
        int? target = 719;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, 2), ref target));
        Assert.Equal(719, target);
    }

    /// <summary>Validates a complete non-bulk Union field batch before changing the existing case.</summary>
    [Fact]
    public void UnionBatchIsUnchangedOnTruncatedInput()
    {
        ReaderPreaccessUnion source = new ReaderPreaccessCase { Number = 137, Count = 271 };
        byte[] backing = LuminPackSerializer.Serialize(source);
        for (int length = 1; length < backing.Length; length++)
        {
            ReaderPreaccessUnion target = new ReaderPreaccessCase { Number = 719, Count = 981 };
            var original = target;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(backing.AsSpan(0, length), ref target));
            Assert.Same(original, target);
            var retained = Assert.IsType<ReaderPreaccessCase>(target);
            Assert.Equal(719, retained.Number);
            Assert.Equal(981, retained.Count);
        }
    }
}

/// <summary>Provides one fixed field whose read cannot be protected by another member's header.</summary>
[LuminPackable]
public sealed class ReaderPreaccessScalar
{
    public int Number;
}

/// <summary>Forces the adjacent-field proof to retain the architecture-aware field-wise fallback.</summary>
[LuminPackable, StructLayout(LayoutKind.Explicit)]
public sealed class ReaderPreaccessGap
{
    [FieldOffset(0)] public int Number;
    [FieldOffset(8)] public long Count;
}

/// <summary>Exercises the Union payload's non-bulk field-batch read.</summary>
[LuminPackable, LuminPackUnion(1, typeof(ReaderPreaccessCase))]
public abstract partial class ReaderPreaccessUnion;

/// <summary>Provides two fixed members read under the original Union frame.</summary>
[LuminPackable]
public sealed partial class ReaderPreaccessCase : ReaderPreaccessUnion
{
    public int Number;
    public long Count;
}

/// <summary>Provides a packed scalar whose native padding must not weaken input range validation.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Packed)]
public struct PackedReadBoundary
{
    /// <summary>The leading semantic byte.</summary>
    public byte Tag;
    /// <summary>The aligned semantic integer.</summary>
    public long Value;
}
