using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core;

public ref partial struct LuminPackReader
{
    /// <summary>Validates and consumes a fixed-length record without materializing a string.</summary>
    /// <typeparam name="TUnit">The selected codec's encoding unit.</typeparam>
    /// <param name="maximum">The maximum UTF-16 character count.</param>
    internal void SkipVersionLengthString<TUnit>(int maximum) where TUnit : unmanaged
    {
        bool utf8 = typeof(TUnit) == typeof(byte);
        int start = Position;
        int prefix = utf8 ? 2 * sizeof(int) : sizeof(int);
        EnsureReadable(start, prefix);
        int header = LuminPackReader.Read<int>(ref Current);
        if (header == LuminPackCode.NullCollection)
        {
            AdvanceValidated(prefix);
            return;
        }
        if (header < 0)
            LuminPackExceptionHelper.ThrowInvalidStringLength(header);

        int bytes = utf8 ? header : StringSerializer.Utf16RecordPayload(header);
        int payload = start + prefix;
        EnsureReadable(payload, bytes);
        if (utf8 && bytes != 0)
        {
            int expected = LuminPackReader.Read<int>(ref Unsafe.Add(ref Current, sizeof(int)));
            int characters = StringSerializer.GetStrictUtf8CharacterCount(_buffer.Slice(payload, bytes));
            if (expected > 0 && characters != expected)
                LuminPackExceptionHelper.ThrowInvalidStringLength(expected);
            ValidateSkippedStringLength(characters, maximum);
        }
        else
        {
            ValidateSkippedStringLength(header >> 1, maximum);
        }
        AdvanceValidated(prefix + bytes);
    }

    /// <summary>Validates and consumes a terminated record without materializing a string.</summary>
    /// <typeparam name="TUnit">The selected codec's encoding unit.</typeparam>
    /// <param name="maximum">The maximum UTF-16 character count.</param>
    internal void SkipVersionTokenString<TUnit>(int maximum) where TUnit : unmanaged
    {
        int start = Position;
        ReadStringLength<TUnit, TokenStringFraming>(_buffer, ref start, out int bytes);
        bool utf8 = typeof(TUnit) == typeof(byte);
        int characters = utf8 ? StringSerializer.GetStrictUtf8CharacterCount(_buffer.Slice(start, bytes)) : bytes / sizeof(char);
        ValidateSkippedStringLength(characters, maximum);
        AdvanceValidated(bytes + (utf8 ? 1 : sizeof(char)));
    }

    /// <summary>Validates and consumes a variable-length record without materializing a string.</summary>
    /// <typeparam name="TUnit">The selected codec's encoding unit.</typeparam>
    /// <param name="maximum">The maximum UTF-16 character count.</param>
    internal void SkipVersionVarIntString<TUnit>(int maximum) where TUnit : unmanaged
    {
        var source = _buffer.Slice(Position);
        ReadVarIntRecordHeader<TUnit>(source, out int prefix, out int bytes, out int characters, out _);
        if (bytes >= 0)
        {
            if (typeof(TUnit) == typeof(byte) && StringSerializer.GetStrictUtf8CharacterCount(source.Slice(prefix, bytes)) != characters)
                LuminPackExceptionHelper.ThrowInvalidStringLength(characters);
            ValidateSkippedStringLength(characters, maximum);
        }
        AdvanceValidated(prefix + Math.Max(bytes, 0));
    }

    /// <summary>Rejects a skipped string whose validated length exceeds the receiving limit.</summary>
    /// <param name="characters">The exact UTF-16 character count.</param>
    /// <param name="maximum">The maximum permitted UTF-16 character count.</param>
    private static void ValidateSkippedStringLength(int characters, int maximum)
    {
        if (characters > maximum)
            LuminPackExceptionHelper.ThrowInvalidDataException("Untrusted input exceeded its string length limit.");
    }
}
