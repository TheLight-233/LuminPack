using System.Globalization;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

/// <summary>Removes the established assembly qualifier pattern without materializing replacement strings.</summary>
internal static class LuminPackTypeNameText
{
    /// <summary>Copies the unchanged name fragments around every complete qualifier match.</summary>
    /// <param name="source">The complete assembly-qualified name.</param>
    /// <param name="destination">Scratch with at least the source character count.</param>
    /// <param name="start">The first removal start already located by the caller.</param>
    /// <param name="end">The first removal end already located by the caller.</param>
    /// <returns>The compacted character count.</returns>
    internal static int Compact(ReadOnlySpan<char> source, Span<char> destination, int start, int end)
    {
        int written = 0;
        do
        {
            source.Slice(0, start).CopyTo(destination.Slice(written));
            written += start;
            source = source.Slice(end);
        } while (FindRemoval(source, out start, out end));
        source.CopyTo(destination.Slice(written));
        return written + source.Length;
    }

    /// <summary>Finds the next leftmost complete match of the original qualifier pattern.</summary>
    /// <param name="source">The remaining name.</param>
    /// <param name="start">Receives the first removed character.</param>
    /// <param name="end">Receives the first retained character after the match.</param>
    /// <returns>Whether a complete qualifier can be removed.</returns>
    internal static bool FindRemoval(ReadOnlySpan<char> source, out int start, out int end)
    {
        int offset = 0;
        while (offset < source.Length)
        {
            int match = source.Slice(offset).IndexOf(", Version=".AsSpan());
            if (match < 0)
                break;
            start = offset + match;
            int version = start + 10;
            if (TryVersion(source, version, out end))
                return true;
            offset = start + 1;
        }
        start = end = 0;
        return false;
    }

    /// <summary>Recognizes four nonempty Unicode digit runs separated by the original non-newline wildcards.</summary>
    /// <param name="source">The name containing the candidate.</param>
    /// <param name="offset">The first version character.</param>
    /// <param name="end">Receives the complete qualifier end.</param>
    /// <returns>Whether the version and following qualifier fields match.</returns>
    private static bool TryVersion(ReadOnlySpan<char> source, int offset, out int end)
    {
        end = 0;
        if (offset >= source.Length || !char.IsDigit(source[offset]))
            return false;
        // Even bits retain digit runs; odd bits require a digit after a wildcard.
        // Parallel states preserve greedy-regex acceptance when the wildcard is itself a digit.
        int states = 1;
        for (int index = offset + 1; index < source.Length; index++)
        {
            char value = source[index];
            if ((states & 64) != 0 && value == ',' && TrySuffix(source, index, out end))
                return true;
            states = (char.IsDigit(value) ? (states & 85) | ((states & 42) << 1) : 0) |
                (value != '\n' ? (states & 21) << 1 : 0);
            if (states == 0)
                return false;
        }
        return false;
    }

    /// <summary>Matches the culture word run and the original null or lowercase hexadecimal token alternatives.</summary>
    /// <param name="source">The name containing the candidate.</param>
    /// <param name="offset">The first comma after a complete version.</param>
    /// <param name="end">Receives the complete qualifier end.</param>
    /// <returns>Whether both remaining fields match.</returns>
    private static bool TrySuffix(ReadOnlySpan<char> source, int offset, out int end)
    {
        end = 0;
        if (!source.Slice(offset).StartsWith(", Culture=".AsSpan()))
            return false;
        int first = offset += 10;
        while (offset < source.Length && IsCultureCharacter(source[offset]))
            offset++;
        if (first == offset || !source.Slice(offset).StartsWith(", PublicKeyToken=".AsSpan()))
            return false;
        offset += 17;
        var token = source.Slice(offset);
        if (token.StartsWith("null".AsSpan()))
        {
            end = offset + 4;
            return true; }
        if (token.Length < 16)
            return false;
        for (int i = 0; i < 16; i++)
            if ((uint)(token[i] - '0') > 9 && (uint)(token[i] - 'a') > 5)
                return false;
        end = offset + 16;
        return true;
    }

    /// <summary>Recognizes the original Unicode word-character categories plus a literal hyphen.</summary>
    /// <param name="value">The UTF-16 code unit.</param>
    /// <returns>Whether the character belongs to the culture run.</returns>
    private static bool IsCultureCharacter(char value)
    {
        if (value <= 127)
            return (uint)(value - '0') <= 9 || ((uint)value | 32) - 'a' <= 25 || value is '_' or '-';
        var category = char.GetUnicodeCategory(value);
        return category is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.NonSpacingMark or
            UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
    }
}
