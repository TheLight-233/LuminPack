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
            if (json && field.CanTraverseUncachedImmutableQueue && type == "global::System.Collections.Immutable.ImmutableQueue")
                AppendQueueLoop(field, sb, "value", "            ");
            else
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
            else if (json && field.CanTraverseUncachedImmutableQueue && implementations[i] == "ImmutableQueue")
                AppendQueueLoop(field, sb, "source", "                ");
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
    /// <param name="declareFirst">Whether to start a delimiter state instead of continuing the preceding queue stack.</param>
    private static void AppendLoop(LuminLocalFieldData field, StringBuilder sb, string collection, string indent, bool json, bool dictionary, bool isolateInteger = false, bool declareFirst = true)
    {
        if (json && !dictionary && declareFirst) sb.AppendLine(indent + "bool isFirst = true;");
        sb.AppendLine(indent + "foreach (var item in " + collection + ")");
        sb.AppendLine(indent + "{");
        if (!json) sb.AppendLine(indent + "    writer.CheckBuffer();");
        if (json && dictionary)
            DictionaryEmitter.AppendJsonDictionaryEntrySerialize(field, sb, "item.Key", "item.Value", indent + "    ", fused: false, isolateInteger: isolateInteger);
        else if (dictionary)
        {
            sb.AppendLine(indent + "    var key = item.Key;");
            sb.AppendLine(indent + "    writer.WriteValue(in key);");
            sb.AppendLine(indent + "    var itemValue = item.Value;");
            sb.AppendLine(indent + "    writer.WriteValue(in itemValue);");
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

    /// <summary>Writes a concrete queue while retaining cached stacks and staging only an uncached back stack.</summary>
    /// <param name="field">The immutable queue contract and compiled element operation.</param>
    /// <param name="sb">The generated method body.</param>
    /// <param name="collection">The exact framework queue expression.</param>
    /// <param name="indent">The enclosing indentation.</param>
    private static void AppendQueueLoop(LuminLocalFieldData field, StringBuilder sb, string collection, string indent)
    {
        string type = GetFirstGeneric(field.TypeName);
        sb.AppendLine(indent + "bool isFirst = true;");
        sb.AppendLine(indent + "if (global::LuminPack.Code.LuminPackCollectionAccess.TryGetImmutableQueueTraversal(" + collection + ", out var forwards, out var backwards, out bool reverse))");
        sb.AppendLine(indent + "{");
        AppendLoop(field, sb, "forwards!", indent + "    ", true, false, true, false);
        sb.AppendLine(indent + "    if (reverse) WriteReversed(ref writer, backwards!, ref isFirst);");
        sb.AppendLine(indent + "    else");
        sb.AppendLine(indent + "    {");
        AppendLoop(field, sb, "backwards!", indent + "        ", true, false, true, false);
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "else");
        sb.AppendLine(indent + "{");
        AppendLoop(field, sb, collection, indent + "    ", true, false, true, false);
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine(indent + "static void WriteReversed(ref global::LuminPack.Core.LuminPackJsonWriter writer, global::System.Collections.Immutable.ImmutableStack<" + type + "> stack, ref bool isFirst)");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    var items = global::LuminPack.Code.LuminPackCollectionAccess.RentImmutableQueueReversal(stack, out int count);");
        sb.AppendLine(indent + "    try");
        sb.AppendLine(indent + "    {");
        AppendLoop(field, sb, "new global::System.ReadOnlySpan<" + type + ">(items, 0, count)", indent + "        ", true, false, true, false);
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    finally { global::System.Buffers.ArrayPool<" + type + ">.Shared.Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + type + ">()); }");
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
