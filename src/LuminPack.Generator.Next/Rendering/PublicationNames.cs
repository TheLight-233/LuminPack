using System.Text;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Names generated files after the namespace and name of the declaration they serve.</summary>
/// <remarks>A type file is <c>LuminPackGen.Namespace.Name.g.cs</c>. Generic arguments are not part of the name, so every
/// construction of one generic definition, such as <c>Dictionary&lt;int, string&gt;</c> and <c>Dictionary&lt;long, string&gt;</c>,
/// shares <c>LuminPackGen.System.Collections.Generic.Dictionary`2.g.cs</c>, and the publisher joins them into one file. A
/// second kind of file for the same declaration in the same generator carries a short role before the extension.</remarks>
internal static class PublicationNames
{
    internal const string Prefix = "LuminPackGen.";

    /// <summary>Names the file of one declaration.</summary>
    /// <param name="identity">The canonical type identity.</param>
    /// <param name="role">An optional role, such as <c>Api</c> or <c>Layout</c>.</param>
    /// <returns>The hint name.</returns>
    internal static string Type(string identity, string? role = null)
        => Prefix + Mangle(identity) + (role == null ? string.Empty : "." + role) + ".g.cs";

    /// <summary>Names an assembly-wide file.</summary>
    /// <param name="name">The file's role.</param>
    /// <returns>The hint name.</returns>
    internal static string Shared(string name) => Prefix + name + ".g.cs";

    /// <summary>Spells a type identity as its namespace and name with the generic arity in place of the arguments.</summary>
    /// <param name="identity">The canonical type identity, for example <c>global::System.Collections.Generic.List&lt;int&gt;</c>.</param>
    /// <returns>The file-safe name, for example <c>System.Collections.Generic.List`1</c>.</returns>
    internal static string Mangle(string identity)
    {
        string text = identity.Replace("global::", string.Empty).Replace(" ", string.Empty);
        var name = new StringBuilder(text.Length);
        int depth = 0, arguments = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character is '<' or '(')
            {
                if (depth++ == 0)
                {
                    arguments = 1;
                    if (character == '(')
                        name.Append("ValueTuple");
                }
                continue;
            }
            if (character is '>' or ')')
            {
                if (--depth == 0)
                    name.Append('`').Append(arguments);
                continue;
            }
            if (depth != 0)
            {
                if (character == ',' && depth == 1)
                    arguments++;
                continue;
            }
            if (character == '[')
            {
                int rank = 1;
                for (index++; index < text.Length && text[index] != ']'; index++)
                    if (text[index] == ',')
                        rank++;
                name.Append("Array");
                if (rank > 1)
                    name.Append(rank);
                continue;
            }
            if (character == '?')
                name.Append("Nullable");
            else if (character is '.' or '+')
                name.Append('.');
            else if (char.IsLetterOrDigit(character) || character == '_')
                name.Append(character);
            else
                name.Append('_');
        }
        return name.Length == 0 ? "Declaration" : name.ToString();
    }
}
