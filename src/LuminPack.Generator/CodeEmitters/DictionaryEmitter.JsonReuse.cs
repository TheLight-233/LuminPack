using System.Text;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static partial class DictionaryEmitter
{
    /// <summary>Stages JSON entries while matching values against the existing dictionary before clearing its keys.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="keyType">The closed key spelling.</param>
    /// <param name="valueType">The closed value spelling.</param>
    /// <param name="target">The dictionary local.</param>
    /// <param name="store">The original dictionary insertion statement.</param>
    /// <param name="sortedList">The exact sorted-list type when the runtime can traverse its validated storage.</param>
    /// <param name="exactSnapshot">The exact runtime type and its existing value traversal, if available.</param>
    private static void AppendExistingJsonDictionary(StringBuilder sb, string keyType, string valueType, string target, string store,
        string sortedList = null, (string Type, string Source)? exactSnapshot = null)
    {
        string pair = "global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">";
        sb.AppendLine("            if (" + target + " is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                int length = reader.CountArrayElements();");
        sb.AppendLine("                if (length == 0)");
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.ConsumeArrayEnd();");
        sb.AppendLine("                    " + target + ".Clear();");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("                int oldCount = " + target + ".Count;");
        sb.AppendLine("                var entries = global::System.Buffers.ArrayPool<" + pair + ">.Shared.Rent(global::System.Math.Max(length, oldCount));");
        sb.AppendLine("                int count = 0;");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        if (keyType is "string" or "global::System.String")
        {
            const string access = "global::LuminPack.Code.LuminPackCollectionAccess.";
            string indent = "                    ";
            if (sortedList != null)
            {
                sb.AppendLine(indent + "if (" + target + ".GetType() == typeof(" + sortedList + ") && " + access +
                    "TryGetSortedListTraversal(" + target + ", out var oldKeys, out var oldValues, out int oldVersion))");
                sb.AppendLine(indent + "{");
                sb.AppendLine(indent + "    for (int oldIndex = 0; oldIndex < oldKeys.Length; oldIndex++)");
                sb.AppendLine(indent + "    {");
                sb.AppendLine(indent + "        " + access + "CheckSortedListTraversalVersion(" + target + ", oldVersion);");
                sb.AppendLine(indent + "        entries[oldIndex] = new " + pair + "(oldKeys[oldIndex], oldValues[oldIndex]);");
                sb.AppendLine(indent + "    }");
                sb.AppendLine(indent + "    " + access + "CheckSortedListTraversalVersion(" + target + ", oldVersion);");
                sb.AppendLine(indent + "}");
                sb.AppendLine(indent + "else");
                sb.AppendLine(indent + "{");
                indent += "    ";
            }
            sb.AppendLine(indent + "int oldIndex = 0;");
            string publicSnapshot = "foreach (var entry in " + target + ") entries[oldIndex++] = entry;";
            if (exactSnapshot is { } snapshot)
            {
                sb.AppendLine(indent + "if (" + target + ".GetType() == typeof(" + snapshot.Type + "))");
                sb.AppendLine(indent + "    foreach (var entry in " + snapshot.Source + ") entries[oldIndex++] = entry;");
                sb.AppendLine(indent + "else");
                sb.AppendLine(indent + "    " + publicSnapshot);
            }
            else
                sb.AppendLine(indent + publicSnapshot);
            if (sortedList != null)
                sb.AppendLine("                    }");
        }
        sb.AppendLine("                    while (reader.Read())");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd) break;");
        AppendSkipNonArrayEntry(sb, "                        ");
        sb.AppendLine("                        " + keyType + " key = default!;");
        sb.AppendLine("                        " + valueType + " itemValue = default!;");
        AppendJsonEntryRead(sb, target + ".TryGetValue(key, out itemValue);",
            seedKey: keyType is "string" or "global::System.String" ? "key = count < oldCount ? entries[count].Key : default!;" : null);
        sb.AppendLine("                        entries[count++] = new " + pair + "(key, itemValue);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    " + target + ".Clear();");
        sb.AppendLine("                    for (int i = 0; i < count; i++)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        var key = entries[i].Key;");
        sb.AppendLine("                        var itemValue = entries[i].Value;");
        sb.AppendLine("                        " + store);
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                finally");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::System.Buffers.ArrayPool<" + pair + ">.Shared.Return(entries, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + pair + ">());");
        sb.AppendLine("                }");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
    }
}
