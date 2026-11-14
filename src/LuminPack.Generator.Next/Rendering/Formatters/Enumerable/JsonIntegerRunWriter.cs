using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Routes supported integer spans through the complete JSON element run.</summary>
internal static class JsonIntegerRunWriter
{
    /// <summary>Checks whether an integer span can use the referenced runtime's full-run writer.</summary>
    /// <param name="set">The referenced formatter capabilities.</param>
    /// <param name="collection">The collection's source type.</param>
    /// <param name="scalar">The span element's source type.</param>
    /// <returns>Whether the exact integer overload is available.</returns>
    internal static bool Supports(SharedFormatterSet set, string collection, string scalar) =>
        (GenericFormatterReuse.Real(scalar) is "int" or "long") && set.FindFormatter(collection)?.Supports("JsonIntegerRun") == true;

    /// <summary>Emits a profile-aware call for exact framework integer storage.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The referenced formatter capabilities.</param>
    /// <param name="collection">The collection's source type.</param>
    /// <param name="scalar">The span element's source type.</param>
    /// <param name="items">The expression that produces the input span.</param>
    /// <param name="readBeforeSeparator">Whether the old traversal copies the item before writing its separator.</param>
    /// <param name="profile">The compiled JSON profile spelling.</param>
    /// <param name="writer">The active JSON writer local.</param>
    /// <returns>Whether a complete run was emitted.</returns>
    internal static bool TryEmit(CodeWriter w, SharedFormatterSet set, string collection, string scalar, string items,
        bool readBeforeSeparator, string profile = "TLuminProfile", string writer = "writer")
    {
        string type = GenericFormatterReuse.Real(scalar);
        if (!Supports(set, collection, scalar))
            return false;
        if (GenericFormatterReuse.IsUnmanagedPlaceholder(scalar))
            items = "global::System.Runtime.InteropServices.MemoryMarshal.Cast<" + scalar + ", " + type +
                ">((global::System.ReadOnlySpan<" + scalar + ">)" + items + ")";
        w.Line(writer + ".WriteIntegerRun<" + profile + ">(" + items + ", " + (readBeforeSeparator ? "true" : "false") + ");");
        return true;
    }
}
