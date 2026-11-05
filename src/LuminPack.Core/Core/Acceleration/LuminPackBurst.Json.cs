#if NETSTANDARD2_1
using System;
using System.Threading;

namespace LuminPack.Core;
public static unsafe partial class LuminPackBurst
{
    /// <summary>Runs an optional read-only sizing hint over a pinned complete window; short inputs keep the managed scanner.</summary>
    /// <param name = "source">The complete remaining input.</param><param name = "utf8">Whether bytes encode UTF-8.</param>
    /// <param name = "entries">Whether array members are flat scalar/string pairs.</param><param name = "count">The sizing hint.</param>
    /// <returns>Whether a registered backend returned a bounded sizing result.</returns>
    internal static bool TryJsonArrayHint(ReadOnlySpan<byte> source, bool utf8, bool entries, out int count)
    {
        count = -1;
        if (source.Length < 128)
            return false;
        var registration = Volatile.Read(ref _registration);
        if (registration is null)
            return false;
        uint flags = entries ? 1U : 0U;
        int units = utf8 ? source.Length : source.Length / 2;
        if (utf8)
        {
            if (registration.Table.JsonArrayHintUtf8 == null)
                return false;
            fixed (byte* input = source)
                count = registration.Table.JsonArrayHintUtf8(input, units, flags);
        }
        else
        {
            if ((source.Length & 1) != 0 || registration.Table.JsonArrayHintUtf16 == null)
                return false;
            fixed (byte* input = source)
                count = registration.Table.JsonArrayHintUtf16((char*)input, units, flags);
        }

        return count >= -1 && count <= units;
    }
}
#endif
