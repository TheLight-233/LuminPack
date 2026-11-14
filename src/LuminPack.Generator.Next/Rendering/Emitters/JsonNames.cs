namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Spells JSON name-table identifiers and computes the hashes their dispatch uses.</summary>
/// <remarks>The JSON emitter groups names by length for small tables or sparse length buckets, and by XXH3 otherwise. Both the member suffix
/// and the hash are part of the generated lookup, so they must remain identical across generators.</remarks>
internal static class JsonNames
{
    /// <summary>Spells a member's table suffix, keeping a leading <c>U_</c> from colliding with the UTF-8 prefix.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>The table suffix.</returns>
    internal static string MemberSuffix(string name) => name.TrimStart('U').StartsWith("_", System.StringComparison.Ordinal) ||
        // A field cannot share the name of its name-table class.
        name is "Utf8" or "Utf16" or "Utf8Head" or "Utf16Head" ? "U" + name : name;

    /// <summary>Computes the XxHash3 of a UTF-8 name.</summary>
    /// <param name="bytes">The encoded name.</param>
    /// <returns>The 64-bit hash.</returns>
    internal static ulong ComputeUtf8BytesHash(byte[] bytes)
    {
        return global::LuminPack.Internal.XxHash3Algorithm.Compute(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(new System.ReadOnlySpan<byte>(bytes)), (uint)bytes.Length);
    }

    /// <summary>Computes the XxHash3 of a UTF-16 name's bytes.</summary>
    /// <param name="text">The name.</param>
    /// <returns>The 64-bit hash.</returns>
    internal static ulong ComputeUtf16Hash(string text)
    {
        return global::LuminPack.Internal.XxHash3Algorithm.Compute(
            ref System.Runtime.CompilerServices.Unsafe.As<char, byte>(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(System.MemoryExtensions.AsSpan(text))),
            (uint)text.Length * sizeof(char));
    }
}
