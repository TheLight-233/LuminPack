using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
#if NET8_0_OR_GREATER
    /// <summary>Writes a modern bit array into a completely reserved binary record.</summary>
    /// <param name="destination">The start of the count header followed by the full word-rounded payload.</param>
    /// <param name="value">The captured bit array, including null.</param>
    /// <param name="bitCount">The captured length, or the null collection marker.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteReservedBitArray(ref byte destination, BitArray? value, int bitCount)
    {
        if (value is null)
        {
            LuminPackWriter.Write(ref destination, LuminPackCode.NullCollection);
            return;
        }

        int bytes = (int)(((uint)bitCount + 31) >> 5) * sizeof(int);
        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        var payload = MemoryMarshal.AsBytes(view.m_array.AsSpan()).Slice(0, bytes);
        LuminPackWriter.WritePayload(ref destination, ref MemoryMarshal.GetReference(payload), bitCount, bytes);
    }
#endif

    /// <summary>Writes validated bit storage directly, preserving the portable CopyTo padding convention.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="value">The nonnull framework bit array.</param>
    /// <returns>Whether the supported storage was written.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryWriteBitArrayPayload(ref LuminPackWriter writer, BitArray value)
    {
        int length = value.Length;
        if (length == 0)
        {
            writer.Write<byte>(ReadOnlySpan<byte>.Empty, 0);
            return true; }
        if (!BitPayloadStorage.Available)
            return false;
        Span<byte> bytes = BitPayloadStorage.Bytes(value);
        int payload = (int)(((long)length + 31) >> 5) * 4;
        // Retained excess capacity keeps the framework CopyTo path and its exception behavior.
        if (bytes.Length != payload)
            return false;
        int extra = length & 31;
        int tail = extra != 0 && BitPayloadStorage.MaskTail
            ? MemoryMarshal.Read<int>(bytes.Slice(payload - 4)) & (int)((1u << extra) - 1) : 0;
        writer.Write<byte>(bytes, length);
        if (extra != 0 && BitPayloadStorage.MaskTail)
            LuminPackWriter.Write(ref Unsafe.Add(ref writer.CurrentReference, -4), tail);
        return true;
    }

    /// <summary>Creates final bit-array storage when its actual runtime layout has been validated.</summary>
    /// <param name="length">The validated nonnegative bit count.</param>
    /// <param name="value">Receives an unpublished fresh result, or null when the layout is unsupported.</param>
    /// <param name="storage">Receives the complete word-rounded writable payload.</param>
    /// <returns>Whether the final storage can be filled directly.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool TryCreateBitArrayPayload(int length, out BitArray? value, out Span<byte> storage)
    {
        value = null;
        storage = default;
        if (length <= 0 || length > int.MaxValue - 31 || !BitPayloadStorage.Available)
            return false;
        var result = new BitArray((length + 31) & ~31);
        storage = BitPayloadStorage.Bytes(result);
        if (storage.Length != ((length + 31) >> 5) * 4)
            return false;
        value = result;
        return true;
    }

    /// <summary>Checks both supported backing-array representations before accessing their payload.</summary>
    private static class BitPayloadStorage
    {
        internal static bool MaskTail;
        internal static readonly bool Available = Check();

        /// <summary>Reads the located reference as an object before checking its exact array representation.</summary>
        /// <param name="value">The framework bit-array owner.</param>
        /// <returns>The byte projection of its actual primitive array, or an empty span for an unknown representation.</returns>
        internal static Span<byte> Bytes(BitArray value)
        {
            object array = LuminFieldLayout.Field<object>(value, LuminCollectionLayouts.BitArrayArray);
            if (array is int[] words)
                return MemoryMarshal.AsBytes(words.AsSpan());
            return array is byte[] bytes ? bytes.AsSpan() : default;
        }

        /// <summary>Checks rounded storage lengths, bit order, writable access and the public shrinking operation.</summary>
        /// <returns>Whether public constructors and methods agree with the located payload.</returns>
        private static bool Check()
        {
            try
            {
                if (LuminCollectionLayouts.BitArrayArray < 0)
                    return false;
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var field = typeof(BitArray).GetField("_array", flags) ?? typeof(BitArray).GetField("m_array", flags);
                if (field is null || field.FieldType != typeof(int[]) && field.FieldType != typeof(byte[]))
                    return false;
                bool? masksTail = null;
                foreach (int length in new[] { 1, 7, 8, 31, 32, 33, 65, 257 })
                {
                    int rounded = (length + 31) & ~31;
                    var value = new BitArray(rounded);
                    Span<byte> bytes = Bytes(value);
                    if (bytes.Length != rounded / 8)
                        return false;
                    bytes.Fill(0xA5);
                    for (int bit = 0; bit < rounded; bit++)
                        if (value[bit] != ((0xA5 & (1 << (bit & 7))) != 0))
                            return false;
                    value.Length = length;
                    var expected = new BitArray(new int[(length + 31) >> 5]);
                    for (int bit = 0; bit < rounded; bit++)
                        expected[bit] = (0xA5 & (1 << (bit & 7))) != 0;
                    expected.Length = length;
                    var actualWords = new int[(length + 31) >> 5];
                    var expectedWords = new int[actualWords.Length];
                    value.CopyTo(actualWords, 0);
                    expected.CopyTo(expectedWords, 0);
                    if (!actualWords.AsSpan().SequenceEqual(expectedWords))
                        return false;
                    bytes = Bytes(value);
                    bytes.Fill(0xFF);
                    value.CopyTo(actualWords, 0);
                    int extra = length & 31;
                    if (extra != 0)
                    {
                        bool masked = actualWords[actualWords.Length - 1] == (int)((1u << extra) - 1);
                        if (!masked && actualWords[actualWords.Length - 1] != -1 || masksTail.HasValue && masksTail.Value != masked)
                            return false;
                        masksTail = masked;
                    }
                }
                MaskTail = masksTail == true;
                return true;
            }
            catch
            {
                return false; }
        }
    }
}
