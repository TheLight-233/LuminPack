#if NET9_0_OR_GREATER
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;

namespace LuminPack.Core;

public ref partial struct LuminPackReader
{
    /// <summary>Looks up a validated fixed UTF-16 string record without allocating an existing key.</summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="lookup">The existing dictionary's alternate lookup.</param>
    /// <param name="found">Whether the existing key matched.</param>
    /// <param name="value">The existing value when the key matched.</param>
    /// <returns>The matching key or a newly decoded key.</returns>
    internal string ReadConcurrentDictionaryKey<TValue>(
        ConcurrentDictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> lookup, out bool found, out TValue value)
    {
        ref byte source = ref Current;
        int remaining = ValidateCurrentCollectionPayload(ref source, sizeof(int));
        int header = Read<int>(ref source);
        if (header <= 1)
        {
            string? key = Read<LuminPackUtf16LengthOptions>((string?)null);
            if (key is null)
                LuminPackExceptionHelper.ThrowArgumentNullException("key");

            return LookupConcurrentDictionaryKey(lookup, key.AsSpan(), out found, out value);
        }

        bool latin1 = (header & 1) != 0;
        int bytes = latin1 ? header >> 1 : header;
        if (bytes > remaining)
            ThrowPayloadOutOfRange(_buffer.Length - remaining, bytes);
        var payload = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref source, sizeof(int)), bytes);
        AdvanceValidated(sizeof(int) + bytes);
        if (!latin1)
            return LookupConcurrentDictionaryKey(lookup, MemoryMarshal.Cast<byte, char>(payload), out found, out value);

        return LookupLatin1ConcurrentDictionaryKey(lookup, payload, out found, out value);
    }

    /// <summary>Widens the validated compact key into bounded or pooled temporary storage.</summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="lookup">The dictionary's alternate lookup.</param>
    /// <param name="payload">The compact key bytes.</param>
    /// <param name="found">Whether the key matched.</param>
    /// <param name="value">The existing value when the key matched.</param>
    /// <returns>The matching key or a newly decoded key.</returns>
    private static string LookupLatin1ConcurrentDictionaryKey<TValue>(
        ConcurrentDictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> lookup, ReadOnlySpan<byte> payload,
        out bool found, out TValue value)
    {
        const int stackLimit = 128;
        if (payload.Length <= stackLimit)
        {
            Span<char> text = stackalloc char[stackLimit];
            text = text[..payload.Length];
            Encoding.Latin1.GetChars(payload, text);
            return LookupConcurrentDictionaryKey(lookup, text, out found, out value);
        }

        char[] rented = System.Buffers.ArrayPool<char>.Shared.Rent(payload.Length);
        try
        {
            Span<char> text = rented.AsSpan(0, payload.Length);
            Encoding.Latin1.GetChars(payload, text);
            return LookupConcurrentDictionaryKey(lookup, text, out found, out value);
        }
        finally
        {
            System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Materializes a key only when the span lookup misses.</summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="lookup">The dictionary's alternate lookup.</param>
    /// <param name="text">The complete key characters.</param>
    /// <param name="found">Whether the key matched.</param>
    /// <param name="value">The existing value when the key matched.</param>
    /// <returns>The matching key or a newly decoded key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string LookupConcurrentDictionaryKey<TValue>(
        ConcurrentDictionary<string, TValue>.AlternateLookup<ReadOnlySpan<char>> lookup, ReadOnlySpan<char> text,
        out bool found, out TValue value)
    {
        found = lookup.TryGetValue(text, out string? key, out value!);
        return key ?? new string(text);
    }
}
#endif
