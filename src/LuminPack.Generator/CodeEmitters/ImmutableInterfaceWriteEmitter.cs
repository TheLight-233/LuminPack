using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Keeps immutable traversal state outside callers and reuses checked hash-tree copies.</summary>
internal static class ImmutableInterfaceWriteEmitter
{
    /// <summary>Writes elements through exact framework implementations or the original interface enumerator.</summary>
    /// <param name="field">The complete interface or concrete collection contract.</param>
    /// <param name="sb">The generated operation body.</param>
    /// <param name="json">Whether to retain JSON element framing instead of binary checks.</param>
    /// <param name="dictionary">Whether elements are key/value pairs.</param>
    internal static void AppendElements(LuminLocalFieldData field, StringBuilder sb, bool json, bool dictionary)
    {
        int generic = field.TypeName.IndexOf('<');
        string type = generic < 0 ? field.TypeName : field.TypeName.Substring(0, generic);
        bool direct = type is "global::System.Collections.Immutable.ImmutableHashSet" or "global::System.Collections.Immutable.ImmutableDictionary";
        string[] implementations = direct ? new[] { type.Substring(type.LastIndexOf('.') + 1) } : GetImplementations(type);
        if (implementations.Length == 0)
        {
            AppendLoop(field, sb, "value", "            ", json, dictionary, type is "global::System.Collections.Immutable.ImmutableArray" or "global::System.Collections.Immutable.ImmutableQueue" or "global::System.Collections.Immutable.ImmutableStack");
            return;
        }
        string arguments = GetFirstGeneric(field.TypeName);
        string runtimeArguments = InterfaceCollectionEmitterHelper.RuntimeName(EmitterTypeTraits.GetFirstTypeArgument(field), arguments);
        if (dictionary)
        {
            string second = GetSecondGeneric(field.TypeName);
            arguments += ", " + second;
            runtimeArguments += ", " + InterfaceCollectionEmitterHelper.RuntimeName(
                field.TypeSymbol is INamedTypeSymbol named ? named.TypeArguments[1] : null, second);
        }
        for (int i = 0; i < implementations.Length; i++)
        {
            string concrete = "global::System.Collections.Immutable." + implementations[i] + "<" + arguments + ">";
            string runtime = "global::System.Collections.Immutable." + implementations[i] + "<" + runtimeArguments + ">";
            string cast = implementations[i] == "ImmutableArray" ? "(" + concrete + ")value" : "global::System.Runtime.CompilerServices.Unsafe.As<" + concrete + ">(value)";
            if (!direct) sb.AppendLine("            " + (i == 0 ? "if" : "else if") + " (value.GetType() == typeof(" + runtime + "))");
            sb.AppendLine("                WriteKnownElements" + i + "(ref writer, " + (direct ? "value" : cast) + ");");
        }
        if (!direct)
        {
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            AppendLoop(field, sb, "value", "                ", json, dictionary);
            sb.AppendLine("            }");
        }
        for (int i = 0; i < implementations.Length; i++)
        {
            string concrete = "global::System.Collections.Immutable." + implementations[i] + "<" + arguments + ">";
            sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
            sb.AppendLine("            static void WriteKnownElements" + i + "(ref global::LuminPack.Core." + (json ? "LuminPackJsonWriter" : "LuminPackWriter") + " writer, " + concrete + " source)");
            sb.AppendLine("            {");
            if (implementations[i] is "ImmutableHashSet" or "ImmutableDictionary")
                AppendSnapshot(field, sb, arguments, json, dictionary);
            else
                AppendLoop(field, sb, "source", "                ", json, dictionary, implementations[i] is "ImmutableArray" or "ImmutableQueue" or "ImmutableStack");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Lists framework implementations whose concrete traversal preserves the interface order.</summary>
    /// <param name="type">The canonical interface type without generic arguments.</param>
    /// <returns>The exact implementation names, or an empty array for concrete or unrelated contracts.</returns>
    private static string[] GetImplementations(string type) => type switch
    {
        "global::System.Collections.Immutable.IImmutableList" => new[] { "ImmutableList", "ImmutableArray" },
        "global::System.Collections.Immutable.IImmutableSet" => new[] { "ImmutableHashSet", "ImmutableSortedSet" },
        "global::System.Collections.Immutable.IImmutableDictionary" => new[] { "ImmutableDictionary", "ImmutableSortedDictionary" },
        "global::System.Collections.Immutable.IImmutableQueue" => new[] { "ImmutableQueue" },
        "global::System.Collections.Immutable.IImmutableStack" => new[] { "ImmutableStack" },
        _ => System.Array.Empty<string>()
    };

    /// <summary>Emits the established traversal, element framing and per-element capacity checks.</summary>
    /// <param name="field">The collection contract and runtime capabilities.</param>
    /// <param name="sb">The generated body.</param>
    /// <param name="collection">The concrete or interface expression being enumerated.</param>
    /// <param name="indent">The enclosing statement indentation.</param>
    /// <param name="json">Whether the writer uses JSON framing.</param>
    /// <param name="dictionary">Whether the element exposes a key and value.</param>
    /// <param name="isolateInteger">Whether this linear or staged loop benefits from isolating integer formatting.</param>
    private static void AppendLoop(LuminLocalFieldData field, StringBuilder sb, string collection, string indent, bool json, bool dictionary, bool isolateInteger = false)
    {
        if (json && !dictionary) sb.AppendLine(indent + "bool isFirst = true;");
        sb.AppendLine(indent + "foreach (var item in " + collection + ")");
        sb.AppendLine(indent + "{");
        if (!json) sb.AppendLine(indent + "    writer.CheckBuffer();");
        else if (dictionary) sb.AppendLine(indent + "    writer.WriteArrayStart();");
        if (dictionary)
        {
            sb.AppendLine(indent + "    var key = item.Key;");
            sb.AppendLine(indent + "    writer.WriteValue(in key);");
            sb.AppendLine(indent + "    var itemValue = item.Value;");
            sb.AppendLine(indent + "    writer.WriteValue(in itemValue);");
            if (json) sb.AppendLine(indent + "    writer.WriteArrayEnd();");
        }
        else if (json)
            JsonCollectionElementEmitter.Append(field, sb, "item", indent + "    ", isolateInteger);
        else
        {
            sb.AppendLine(indent + "    var temp = item;");
            sb.AppendLine(indent + "    writer.WriteValue(in temp);");
        }
        sb.AppendLine(indent + "}");
    }

    /// <summary>Uses the existing checked hash-tree copy to avoid abandoned BCL collision-enumerator leases.</summary>
    /// <param name="field">The collection contract and runtime capabilities.</param>
    /// <param name="sb">The generated concrete writer body.</param>
    /// <param name="arguments">The collection's element or key/value type arguments.</param>
    /// <param name="json">Whether to retain JSON framing.</param>
    /// <param name="dictionary">Whether the copied items are key/value pairs.</param>
    private static void AppendSnapshot(LuminLocalFieldData field, StringBuilder sb, string arguments, bool json, bool dictionary)
    {
        string item = dictionary ? "global::System.Collections.Generic.KeyValuePair<" + arguments + ">" : arguments;
        sb.AppendLine("                int count = source.Count;");
        sb.AppendLine("                if (count == 0) return;");
        sb.AppendLine("                var items = global::System.Buffers.ArrayPool<" + item + ">.Shared.Rent(count);");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::LuminPack.Utility.LuminTreeStorage.CopyTo(source, new global::System.Span<" + item + ">(items, 0, count));");
        AppendLoop(field, sb, "new global::System.ReadOnlySpan<" + item + ">(items, 0, count)", "                    ", json, dictionary, isolateInteger: true);
        sb.AppendLine("                }");
        sb.AppendLine("                finally { global::System.Buffers.ArrayPool<" + item + ">.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + item + ">()); }");
    }
}
