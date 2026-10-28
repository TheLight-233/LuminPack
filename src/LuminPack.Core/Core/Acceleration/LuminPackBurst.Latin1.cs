#if NETSTANDARD2_1
using System;
using System.Threading;

namespace LuminPack.Core;

public static unsafe partial class LuminPackBurst
{
    /// <summary>Checks complete code units while keeping their owner pinned.</summary>
    /// <param name="source">The complete character range.</param><param name="latin1">Receives whether every code unit fits.</param>
    /// <returns>Whether a backend supplied the result.</returns>
    internal static bool TryIsLatin1(ReadOnlySpan<char> source, out bool latin1)
    {
        latin1 = false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.IsLatin1 == null) return false;
        if (source.IsEmpty) { latin1 = true; return true; }
        fixed (char* input = source)
            latin1 = registration.Table.IsLatin1(input, source.Length) != 0;
        return true;
    }

    /// <summary>Runs the established Latin-1 narrowing boundary over pinned complete ranges.</summary>
    /// <param name="source">The first character.</param><param name="destination">Output with room for every character.</param>
    /// <param name="length">The complete character count.</param><param name="narrowed">Whether every character narrowed; a false result can leave a prefix.</param>
    /// <returns>Whether a backend supplied the result.</returns>
    internal static bool TryNarrowLatin1(ref char source, ref byte destination, int length, out bool narrowed)
    {
        narrowed = false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.NarrowLatin1 == null) return false;
        if (length == 0) { narrowed = true; return true; }
        fixed (char* input = &source)
        fixed (byte* output = &destination)
            narrowed = registration.Table.NarrowLatin1(input, output, length) != 0;
        return true;
    }

    /// <summary>Widens a complete payload while pinning both input and unpublished output.</summary>
    /// <param name="source">The first payload byte.</param><param name="destination">The first unpublished code unit.</param>
    /// <param name="length">The complete character count.</param><returns>Whether a backend completed the operation.</returns>
    internal static bool TryWidenLatin1(ref byte source, ref char destination, int length)
    {
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.WidenLatin1 == null) return false;
        if (length == 0) return true;
        fixed (byte* input = &source)
        fixed (char* output = &destination)
            registration.Table.WidenLatin1(input, output, length);
        return true;
    }

    /// <summary>Compares complete payload and candidate windows with both owners pinned.</summary>
    /// <param name="source">The first complete payload byte.</param><param name="candidate">The same-length character range.</param>
    /// <param name="equal">Receives the comparison.</param><returns>Whether a backend supplied the result.</returns>
    internal static bool TryLatin1Equal(ref byte source, ReadOnlySpan<char> candidate, out bool equal)
    {
        equal = false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null || registration.Table.Latin1Equal == null) return false;
        if (candidate.IsEmpty) { equal = true; return true; }
        fixed (byte* input = &source)
        fixed (char* text = candidate)
            equal = registration.Table.Latin1Equal(input, text, candidate.Length) != 0;
        return true;
    }
}
#endif
