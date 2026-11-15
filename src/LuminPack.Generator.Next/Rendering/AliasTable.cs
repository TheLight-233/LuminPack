using System;
using System.Collections.Generic;
using System.Text;
using LuminPack.SourceGenerator.Next.Diagnostics;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Resolves helper names through the aliases the consumer leaves available.</summary>
/// <remarks>An alias is only declared when the consumer declares nothing with that name. The table is fixed and ordered, so the
/// same consumer always renders the same header and the same short names.</remarks>
internal sealed class AliasTable
{
    private static readonly (string Alias, string Target)[] Entries =
    {
        ("Unsafe", "global::System.Runtime.CompilerServices.Unsafe"),
        ("MemoryMarshal", "global::System.Runtime.InteropServices.MemoryMarshal"),
        ("MethodImpl", "global::System.Runtime.CompilerServices.MethodImplAttribute"),
        ("MethodImplOptions", "global::System.Runtime.CompilerServices.MethodImplOptions"),
        ("LuminPackSerializer", "global::LuminPack.LuminPackSerializer"),
        ("LuminPackReader", "global::LuminPack.Core.LuminPackReader"),
        ("LuminPackWriter", "global::LuminPack.Core.LuminPackWriter"),
        ("LuminPackInputPolicy", "global::LuminPack.Security.LuminPackInputPolicy"),
        ("LuminPackExceptionHelper", "global::LuminPack.Code.LuminPackExceptionHelper"),
        ("LuminPackMarshal", "global::LuminPack.Code.LuminPackMarshal"),
        ("ILuminPackOptions", "global::LuminPack.Option.ILuminPackOptions"),
        ("Preserve", "global::LuminPack.Attribute.PreserveAttribute"),
        ("LuminPackUtf16LengthOptions", "global::LuminPack.Option.LuminPackUtf16LengthOptions"),
        ("LuminPackUtf8LengthOptions", "global::LuminPack.Option.LuminPackUtf8LengthOptions"),
    };

    private readonly int _available;
    private int _used;

    private AliasTable(int available) => _available = available < 0 ? int.MaxValue : available;

    /// <summary>Creates a table for an alias mask.</summary>
    /// <param name="availableAliases">The mask of aliases the consumer leaves available, or a negative value for all.</param>
    internal static AliasTable For(int availableAliases) => new AliasTable(availableAliases);

    /// <summary>The aliases the renderer actually used, in table order, for the file header.</summary>
    internal IEnumerable<(string Alias, string Target)> Used()
    {
        for (int index = 0; index < Entries.Length; index++)
            if ((_used & (1 << index)) != 0)
                yield return Entries[index];
    }

    /// <summary>Spells a helper type through its alias when available, otherwise fully qualified.</summary>
    /// <param name="alias">The alias name, such as <c>Unsafe</c>.</param>
    /// <returns>The alias or its target.</returns>
    internal string Spell(string alias)
    {
        for (int index = 0; index < Entries.Length; index++)
            if (Entries[index].Alias == alias)
            {
                if ((_available & (1 << index)) == 0)
                    return Entries[index].Target;
                _used |= 1 << index;
                return alias;
            }
        return LuminPackExceptionHelper.ThrowArgument<string>("Unknown generated alias: " + alias, nameof(alias));
    }

    /// <summary>Writes the alias directives the renderer used.</summary>
    /// <param name="header">The file header builder.</param>
    internal void AppendUsings(StringBuilder header)
    {
        foreach (var (alias, target) in Used())
            header.Append("using ").Append(alias).Append(" = ").Append(target).Append(';').Append(Environment.NewLine);
    }
}
