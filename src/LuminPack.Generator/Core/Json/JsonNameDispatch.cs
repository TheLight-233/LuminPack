using System.Collections.Generic;

namespace LuminPack.SourceGenerator;

/// <summary>Selects the existing constant-name lookup when each length has few candidates.</summary>
internal static class JsonNameDispatch
{
    /// <summary>Spells the shared by-reference hash entry for a validated property-name span.</summary>
    /// <param name="utf8">Whether the span contains UTF-8 bytes rather than UTF-16 code units.</param>
    /// <returns>The hash expression used by both generators.</returns>
    internal static string HashExpression(bool utf8)
    {
        string reference = "global::System.Runtime.InteropServices.MemoryMarshal.GetReference(name)";
        if (!utf8)
            reference = "global::System.Runtime.CompilerServices.Unsafe.As<char, byte>(ref " + reference + ")";
        return "global::LuminPack.Internal.XxHash3.Hash64(ref " + reference + ", (uint)name.Length" + (utf8 ? ")" : " * 2u)");
    }

    /// <summary>Limits the number of full-name comparisons in a length bucket for a large table.</summary>
    /// <param name="lengths">The encoded lengths, including aliases and reference metadata.</param>
    /// <returns>Whether every length bucket contains at most four candidates.</returns>
    internal static bool HasSmallBuckets(IEnumerable<int> lengths)
    {
        var counts = new Dictionary<int, int>();
        foreach (int length in lengths)
        {
            counts.TryGetValue(length, out int count);
            if (count == 4)
                return false;
            counts[length] = count + 1;
        }
        return true;
    }
}
