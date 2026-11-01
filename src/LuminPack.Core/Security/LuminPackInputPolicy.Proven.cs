using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    /// <summary>Opens the unchanged frame within a proven scalar/string resource bound before generated validation.</summary>
    /// <param name="input">The stable original input window.</param>
    /// <param name="schema">The immutable contract and quota proof.</param>
    /// <param name="members">The compiler's exact member count.</param>
    /// <param name="payload">The borrowed payload awaiting generated structural and domain guards.</param>
    /// <param name="consumed">The complete original frame width.</param>
    /// <returns>Whether framing and all input-size resource bounds fit the original policy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryOpenProvenFrame(ReadOnlySpan<byte> input, LuminPackSafeSchema schema, int members,
        out ReadOnlySpan<byte> payload, out int consumed)
    {
        payload = default; consumed = 0;
        if (!BitConverter.IsLittleEndian || input.Length < HeaderSize || members < 0 || members != schema.ProvenObjectMembers) return false;
        int length = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(12, 4));
        if (length < 1 || length > input.Length - HeaderSize ||
            length != BinaryPrimitives.ReadInt32LittleEndian(input.Slice(16, 4)) ||
            BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(8, 4)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(20, 4)) != 0 ||
            !input.Slice(0, 8).SequenceEqual(Magic) || !input.Slice(24, 32).SequenceEqual(schema.Fingerprint.AsSpan(0, 32))) return false;
        var value = input.Slice(HeaderSize, length);
        if (value[0] == 255)
        {
            if (length != 1 || !schema.FixedNullAllowed) return false;
        }
        else if (value[0] != members || length > schema.ProvenPayloadLimit) return false;
        payload = value; consumed = HeaderSize + length;
        return true;
    }

    /// <summary>Checks one declared string record without allocation before the original decoding kernel runs.</summary>
    /// <param name="input">The already bounded complete payload.</param>
    /// <param name="position">The checked field cursor, advanced only after a valid complete record.</param>
    /// <param name="mode">The compiler's string encoding and recording mode.</param>
    /// <returns>Whether the string header, range and Unicode domain are valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySkipProvenString(ReadOnlySpan<byte> input, ref int position, int mode)
    {
        if ((uint)position > (uint)input.Length) return false;
        if (mode != 3) return TrySkipOtherProvenString(input, ref position, mode);
        if (input.Length - position < 4) return false;
        int header = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(position, 4));
        int start = position + 4;
        if (header == -1) { position = start; return true; }
        if (header < 0) return false;
        bool latin = (header & 1) != 0;
        int bytes = latin ? header >> 1 : header;
        if (bytes > input.Length - start || !latin && !TryProvenUnicode(input.Slice(start, bytes))) return false;
        position = start + bytes;
        return true;
    }

    /// <summary>Checks the token, UTF-8 and canonical variable-length string profiles outside the fixed UTF-16 path.</summary>
    /// <param name="input">The complete bounded payload.</param>
    /// <param name="position">The current record boundary.</param>
    /// <param name="mode">The original compiler-selected profile.</param>
    /// <returns>Whether one complete original string record was validated.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySkipOtherProvenString(ReadOnlySpan<byte> input, ref int position, int mode)
    {
        bool utf8 = mode is 0 or 1 or 4;
        int at = position, bytes, characters = -1, terminator = 0;
        bool latin = false;
        if (mode is 0 or 2)
        {
            var remaining = input.Slice(at);
            int units = utf8 ? remaining.IndexOf((byte)0) :
                MemoryMarshal.Cast<byte, ushort>(remaining.Slice(0, remaining.Length & ~1)).IndexOf((ushort)0);
            if (units < 0) return false;
            bytes = units * (utf8 ? 1 : 2); terminator = utf8 ? 1 : 2;
        }
        else
        {
            int header;
            if (mode is 4 or 5)
            {
                if (!TryProvenHeader(input, ref at, out header) || utf8 && !TryProvenHeader(input, ref at, out characters)) return false;
            }
            else if (mode == 1)
            {
                if (input.Length - at < 8) return false;
                header = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(at, 4));
                characters = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(at + 4, 4)); at += 8;
            }
            else return false;
            if (header == -1)
            {
                if (utf8 && characters != -1) return false;
                position = at; return true;
            }
            if (header < 0 || utf8 && characters < 0) return false;
            latin = !utf8 && (header & 1) != 0;
            bytes = utf8 || !latin ? header : header >> 1;
        }
        if (bytes > input.Length - at - terminator) return false;
        var text = input.Slice(at, bytes);
        if (utf8)
        {
            try
            {
                int actual = InputUtf8.GetCharCount(text);
                if (mode != 0 && actual != characters) return false;
            }
            catch (DecoderFallbackException) { return false; }
        }
        else if (!latin && !TryProvenUnicode(text)) return false;
        position = at + bytes + terminator;
        return true;
    }

    /// <summary>Checks a canonical biased base-128 header without publishing a partial cursor.</summary>
    /// <param name="input">The complete bounded payload.</param>
    /// <param name="position">The current header boundary.</param>
    /// <param name="header">The original nullable length.</param>
    /// <returns>Whether a complete canonical header was present.</returns>
    private static bool TryProvenHeader(ReadOnlySpan<byte> input, ref int position, out int header)
    {
        uint value = 0; int at = position; header = 0;
        for (int shift = 0; shift <= 28; shift += 7)
        {
            if ((uint)at >= (uint)input.Length) return false;
            uint next = input[at++];
            if (shift == 28 && next > 8) return false;
            value |= (next & 127) << shift;
            if (next < 128)
            {
                if (shift != 0 && next == 0 || value > 0x80000000u) return false;
                header = unchecked((int)(value - 1)); position = at; return true;
            }
        }
        return false;
    }

    /// <summary>Checks complete UTF-16 code units with bounded framework scanning and a surrogate fallback.</summary>
    /// <param name="bytes">The already bounded little-endian text payload.</param>
    /// <returns>Whether every surrogate belongs to a complete valid pair.</returns>
    private static bool TryProvenUnicode(ReadOnlySpan<byte> bytes)
    {
        if ((bytes.Length & 1) != 0) return false;
#if NET8_0_OR_GREATER
        if (MemoryMarshal.Cast<byte, char>(bytes).IndexOfAnyInRange('\uD800', '\uDFFF') < 0) return true;
#endif
        for (int at = 0; at < bytes.Length; at += 2)
        {
            ushort current = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at, 2));
            if (current is >= 0xD800 and <= 0xDBFF)
            {
                at += 2;
                if (at >= bytes.Length) return false;
                ushort next = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at, 2));
                if (next is < 0xDC00 or > 0xDFFF) return false;
            }
            else if (current is >= 0xDC00 and <= 0xDFFF) return false;
        }
        return true;
    }
}
