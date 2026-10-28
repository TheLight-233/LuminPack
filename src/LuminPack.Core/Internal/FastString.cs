#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace LuminPack.Internal;

/// <summary>Allocates uninitialized-content strings that string decoders fill completely before publishing them.</summary>
internal static class FastString
{
    /// <summary>Whether the runtime exposes the allocation entry with the signature compiled for this target.</summary>
    /// <remarks>A library asset can run on a newer runtime whose internal signature differs, so availability is probed once.</remarks>
    private static readonly bool Available = Probe();

#if NET10_0_OR_GREATER
    /// <summary>Binds the runtime's internal string allocation entry.</summary>
    /// <param name="owner">Unused static receiver required by the accessor contract.</param>
    /// <param name="length">The number of UTF-16 code units.</param>
    /// <returns>A new string whose characters the caller must overwrite.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "FastAllocateString")]
    private static extern string FastAllocateString(string? owner, nint length);
#else
    /// <summary>Binds the runtime's internal string allocation entry.</summary>
    /// <param name="owner">Unused static receiver required by the accessor contract.</param>
    /// <param name="length">The number of UTF-16 code units.</param>
    /// <returns>A new string whose characters the caller must overwrite.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "FastAllocateString")]
    private static extern string FastAllocateString(string? owner, int length);
#endif

    /// <summary>Checks the accessor once instead of failing on a runtime with another internal signature.</summary>
    /// <returns>Whether the accessor returned a string of the requested length.</returns>
    private static bool Probe()
    {
        try
        {
            return FastAllocateString(null, 1).Length == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Allocates a nonempty string that the caller fills completely before it escapes.</summary>
    /// <param name="length">The positive number of UTF-16 code units.</param>
    /// <returns>A fresh, non-interned string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Allocate(int length) => Available ? FastAllocateString(null, length) : new string('\0', length);
}
#endif
