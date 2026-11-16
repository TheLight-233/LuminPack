#if NET8_0_OR_GREATER
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Option;

namespace LuminPack.Core;

public ref partial struct LuminPackJsonReader
{
    /// <summary>Checks the entry state of a generated integer run with the default JSON profile.</summary>
    /// <param name="first">Whether the first field starts immediately after the opening brace.</param>
    /// <param name="remaining">The validated width from the reader cursor to the window end.</param>
    /// <returns>Whether the generated reader may attempt the run.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryBeginGeneratedInt32Run(bool first, out int remaining) =>
        TryBeginGeneratedInt32Run<LuminPackDefaultOptions>(first, out remaining);

    /// <summary>Checks the entry state and bounds for a generated integer run.</summary>
    /// <typeparam name="TOptions">The closed document profile.</typeparam>
    /// <param name="first">Whether the first field starts immediately after the opening brace.</param>
    /// <param name="remaining">The validated width from the reader cursor to the window end.</param>
    /// <returns>Whether the generated reader may attempt the run.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryBeginGeneratedInt32Run<TOptions>(bool first, out int remaining) where TOptions : struct, ILuminPackOptions
    {
        remaining = 0;
        if (!LuminPackJsonWriter.HasConstantGeneratedPropertyEncoding<TOptions>())
            return false;
        if (default(TOptions).ValidateJsonSyntax &&
            (!IsCurrentContainerObject() || (first ? !IsNameState(GrammarState) : GrammarState != JsonStateAfterValue)))
            return false;

        long width = (long)Remaining;
        if ((ulong)width > (uint)Buffer.Length)
            return false;
        remaining = (int)width;
        return true;
    }

    /// <summary>Matches a complete generated property head followed by a bounded decimal Int32.</summary>
    /// <param name="cursor">The first byte of the caller's validated remaining range.</param>
    /// <param name="remaining">The number of bytes the caller validated before starting the run.</param>
    /// <param name="utf8Head">The complete UTF-8 head, including the expected separator when present.</param>
    /// <param name="utf16Head">The complete UTF-16 head, including the expected separator when present.</param>
    /// <param name="utf8">Whether the document uses UTF-8.</param>
    /// <param name="value">The parsed integer when a complete field matches.</param>
    /// <returns>The number of bytes to consume, or zero to resume at this field with the ordinary reader.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadGeneratedInt32(scoped ref byte cursor, int remaining, ReadOnlySpan<byte> utf8Head,
        ReadOnlySpan<char> utf16Head, bool utf8, out int value)
    {
        value = 0;
        ReadOnlySpan<byte> head = utf8 ? utf8Head : MemoryMarshal.AsBytes(utf16Head);
        int step = utf8 ? 1 : sizeof(char);
        if (remaining < step || head.Length > remaining - step ||
            !MemoryMarshal.CreateReadOnlySpan(ref cursor, head.Length).SequenceEqual(head))
            return 0;

        int offset = head.Length;
        int code = GeneratedInt32CodeUnit(ref cursor, offset, utf8);
        bool negative = code == '-';
        if (negative)
        {
            offset += step;
            if (offset > remaining - step)
                return 0;
            code = GeneratedInt32CodeUnit(ref cursor, offset, utf8);
        }

        uint first = (uint)(code - '0');
        if (first > 9)
            return 0;
        ulong magnitude = first;
        int digits = 1;
        offset += step;
        while (offset <= remaining - step)
        {
            code = GeneratedInt32CodeUnit(ref cursor, offset, utf8);
            uint digit = (uint)(code - '0');
            if (digit > 9)
            {
                if (code is not (' ' or '\t' or '\r' or '\n' or ',' or ']' or '}'))
                    return 0;
                break;
            }
            if (first == 0 || digits == 10)
                return 0;
            magnitude = magnitude * 10 + digit;
            offset += step;
            digits++;
        }
        if (magnitude > (negative ? 2147483648UL : 2147483647UL))
            return 0;

        value = negative ? unchecked(-(int)magnitude) : (int)magnitude;
        return offset;
    }

    /// <summary>Loads one code unit from a range the generated scanner already checked.</summary>
    /// <param name="cursor">The first byte of the remaining range.</param>
    /// <param name="offset">The validated byte offset.</param>
    /// <param name="utf8">Whether to load one byte or one UTF-16 code unit.</param>
    /// <returns>The numeric code unit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GeneratedInt32CodeUnit(ref byte cursor, int offset, bool utf8)
    {
        ref byte source = ref Unsafe.Add(ref cursor, offset);
        return utf8 ? source : Unsafe.ReadUnaligned<ushort>(ref source);
    }

    /// <summary>Publishes a generated integer run using the default JSON profile.</summary>
    /// <param name="count">The bytes consumed by fully matched fields.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteGeneratedInt32Run(int count) => CompleteGeneratedInt32Run<LuminPackDefaultOptions>(count);

    /// <summary>Advances to the first unconsumed byte and restores the token state of an ordinary integer read.</summary>
    /// <typeparam name="TOptions">The closed document profile.</typeparam>
    /// <param name="count">The bytes consumed by fully matched fields.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CompleteGeneratedInt32Run<TOptions>(int count) where TOptions : struct, ILuminPackOptions
    {
        if (count == 0)
            return;
        Advance(count);
        if (default(TOptions).ValidateJsonSyntax)
            GrammarState = JsonStateAfterValue;
        CompleteValue();
    }
}
#endif
