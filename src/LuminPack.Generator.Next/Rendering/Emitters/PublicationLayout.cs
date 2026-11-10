using System;
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Resolves the helper aliases and applies the publication layout to a rendered file.</summary>
/// <remarks>The alias table is fixed and ordered, so a consumer always renders the same header and short names. The Next
/// renderer currently keeps the writer's line layout; the syntax-based wrapping and JSON name grouping remain a migration
/// task and are the passthrough methods below.</remarks>
internal static class PublicationLayout
{
    private static readonly (string Alias, string Target)[] Aliases =
    {
        ("Unsafe", "global::System.Runtime.CompilerServices.Unsafe"),
        ("MemoryMarshal", "global::System.Runtime.InteropServices.MemoryMarshal"),
        ("MethodImpl", "global::System.Runtime.CompilerServices.MethodImplAttribute"),
        ("MethodImplOptions", "global::System.Runtime.CompilerServices.MethodImplOptions"),
        ("EditorBrowsable", "global::System.ComponentModel.EditorBrowsableAttribute"),
        ("EditorBrowsableState", "global::System.ComponentModel.EditorBrowsableState"),
        ("LuminPackSerializer", "global::LuminPack.LuminPackSerializer"),
        ("LuminPackReader", "global::LuminPack.Core.LuminPackReader"),
        ("LuminPackWriter", "global::LuminPack.Core.LuminPackWriter"),
        ("LuminPackJsonReader", "global::LuminPack.Core.LuminPackJsonReader"),
        ("LuminPackJsonWriter", "global::LuminPack.Core.LuminPackJsonWriter"),
        ("LuminPackInputPolicy", "global::LuminPack.Security.LuminPackInputPolicy"),
        ("LuminPackExceptionHelper", "global::LuminPack.Code.LuminPackExceptionHelper"),
        ("LuminPackMarshal", "global::LuminPack.Code.LuminPackMarshal"),
        ("ILuminPackOptions", "global::LuminPack.Option.ILuminPackOptions"),
        ("Preserve", "global::LuminPack.Attribute.PreserveAttribute"),
        ("LuminPackDefaultOptions", "global::LuminPack.Option.LuminPackDefaultOptions"),
        ("LuminPackDeclaredOptions", "global::LuminPack.Option.LuminPackDeclaredOptions"),
        ("LuminPackUtf16LengthOptions", "global::LuminPack.Option.LuminPackUtf16LengthOptions"),
        ("LuminPackUtf8LengthOptions", "global::LuminPack.Option.LuminPackUtf8LengthOptions"),
        ("LuminPackUtf8TokenOptions", "global::LuminPack.Option.LuminPackUtf8TokenOptions"),
        ("LuminPackUtf16TokenOptions", "global::LuminPack.Option.LuminPackUtf16TokenOptions"),
        ("LuminPackUtf8VarIntOptions", "global::LuminPack.Option.LuminPackUtf8VarIntOptions"),
        ("LuminPackUtf16VarIntOptions", "global::LuminPack.Option.LuminPackUtf16VarIntOptions"),
        ("LuminPackStringEncoding", "global::LuminPack.Option.LuminPackStringEncoding"),
        ("LuminPackStringRecording", "global::LuminPack.Option.LuminPackStringRecording"),
        ("LuminPackCode", "global::LuminPack.Code.LuminPackCode"),
        ("LuminPackFormatter", "global::LuminPack.Code.LuminPackFormatter"),
        ("LuminPackFormatterTable", "global::LuminPack.Code.LuminPackFormatterTable"),
        ("LuminPackConstUtf8", "global::LuminPack.LuminPackConstUtf8"),
    };

    /// <summary>Whether the publication layout runs; development harnesses may clear it.</summary>
    internal static bool Enabled = true;

    /// <summary>The number of helper aliases; bit <c>i</c> of an alias mask selects alias <c>i</c>.</summary>
    internal static int AliasCount => Aliases.Length;

    /// <summary>The mask of a consumer that declares none of the alias names.</summary>
    internal static int AllAliases => (1 << Aliases.Length) - 1;

    /// <summary>Gets an alias name.</summary>
    internal static string AliasName(int index) => Aliases[index].Alias;

    /// <summary>Gets an alias's fully qualified target.</summary>
    internal static string AliasTarget(int index) => Aliases[index].Target;

    /// <summary>Finds an alias by its name.</summary>
    internal static int AliasIndex(string alias)
    {
        for (int index = 0; index < Aliases.Length; index++)
            if (Aliases[index].Alias == alias)
                return index;
        throw new ArgumentException(alias);
    }

    /// <summary>Applies the publication layout to a completed file.</summary>
    /// <param name="source">The complete generated source.</param>
    /// <param name="aliases">The aliases available in the consumer.</param>
    /// <returns>The source; wrapping and name grouping are not ported yet.</returns>
    internal static string Apply(string source, int aliases) => source;

    /// <summary>Spaces and wraps the lines of a file whose names and aliases are final.</summary>
    /// <param name="source">The complete presented source.</param>
    /// <returns>The source; wrapping is not ported yet.</returns>
    internal static string LayOutLines(string source) => source;
}
