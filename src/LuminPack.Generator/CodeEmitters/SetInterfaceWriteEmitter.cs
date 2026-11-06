using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Uses concrete public set enumerators without replacing their mutation or ordering rules.</summary>
internal static class SetInterfaceWriteEmitter
{
    /// <summary>Writes exact known set implementations or the original interface traversal.</summary>
    /// <param name="field">The set interface contract and referenced-framework capabilities.</param>
    /// <param name="sb">The generated operation.</param>
    /// <param name="json">Whether the caller has already written JSON array framing.</param>
    internal static void AppendElements(LuminLocalFieldData field, StringBuilder sb, bool json)
    {
        string element = CodeEmitterRegistry.GetFirstGeneric(field.TypeName);
        string runtimeElement = InterfaceCollectionEmitterHelper.RuntimeName(EmitterTypeTraits.GetFirstTypeArgument(field), element);
        string[] implementations = json ? new[] { "HashSet", "SortedSet" } : new[] { "SortedSet" };
        for (int i = 0; i < implementations.Length; i++)
        {
            string concrete = "global::System.Collections.Generic." + implementations[i] + "<" + element + ">";
            string runtime = "global::System.Collections.Generic." + implementations[i] + "<" + runtimeElement + ">";
            sb.AppendLine("            " + (i == 0 ? "if" : "else if") + " (value.GetType() == typeof(" + runtime + "))");
            sb.AppendLine("                WriteSetElements" + i + "(ref writer, global::System.Runtime.CompilerServices.Unsafe.As<" + concrete + ">(value));");
        }
        if (field.CanEnumerateFrozenSet)
        {
            sb.AppendLine("            else if (value is global::System.Collections.Frozen.FrozenSet<" + element + "> frozen)");
            if (json) sb.AppendLine("                WriteFrozenElements(ref writer, frozen);");
            else
            {
                sb.AppendLine("            {");
                sb.AppendLine("                if (frozen.Count == 0)");
                sb.AppendLine("                {");
                sb.AppendLine("                    int position = writer.BeginCollection();");
                sb.AppendLine("                    writer.EndCollection(position, 0);");
                sb.AppendLine("                    writer.CheckBuffer();");
                sb.AppendLine("                }");
                sb.AppendLine("                else WriteFrozenElements(ref writer, frozen);");
                sb.AppendLine("            }");
            }
        }
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        AppendLoop(field, sb, "value", json, "                ");
        sb.AppendLine("            }");
        for (int i = 0; i < implementations.Length; i++)
            AppendFunction(field, sb, "WriteSetElements" + i, "global::System.Collections.Generic." + implementations[i] + "<" + element + ">", json);
        if (field.CanEnumerateFrozenSet)
            AppendFunction(field, sb, "WriteFrozenElements", "global::System.Collections.Frozen.FrozenSet<" + element + ">", json);
    }

    /// <summary>Declares a concrete traversal outside the interface caller's inline budget.</summary>
    /// <param name="field">The element formatter and capabilities.</param>
    /// <param name="sb">The generated operation.</param>
    /// <param name="name">The local operation name.</param>
    /// <param name="type">The concrete set type.</param>
    /// <param name="json">Whether to retain JSON element framing.</param>
    private static void AppendFunction(LuminLocalFieldData field, StringBuilder sb, string name, string type, bool json)
    {
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static void " + name + "(ref global::LuminPack.Core." + (json ? "LuminPackJsonWriter" : "LuminPackWriter") + " writer, " + type + " source)");
        sb.AppendLine("            {");
        AppendLoop(field, sb, "source", json, "                ", !json && type.StartsWith("global::System.Collections.Generic.SortedSet<", System.StringComparison.Ordinal));
        sb.AppendLine("            }");
    }

    /// <summary>Retains public enumeration, scalar formatting, checks and count publication.</summary>
    /// <param name="field">The element formatter and capabilities.</param>
    /// <param name="sb">The generated operation.</param>
    /// <param name="source">The enumerated expression.</param>
    /// <param name="json">Whether the caller owns JSON framing.</param>
    /// <param name="indent">The enclosing indentation.</param>
    /// <param name="skipKnownEmpty">Whether the concrete empty set can avoid allocating its traversal stack.</param>
    private static void AppendLoop(LuminLocalFieldData field, StringBuilder sb, string source, bool json, string indent, bool skipKnownEmpty = false)
    {
        if (!json)
        {
            CollectionEmitterKit.AppendCountedWrite(sb, "var item", source, new[] { "var v = item;", "writer.WriteValue(in v);" }, indent, EmitterTypeTraits.GetFirstTypeArgument(field), skipKnownEmpty);
            return;
        }
        sb.AppendLine(indent + "bool isFirst = true;");
        sb.AppendLine(indent + "foreach (var item in " + source + ")");
        sb.AppendLine(indent + "{");
        JsonCollectionElementEmitter.Append(sb, "item", indent + "    ");
        sb.AppendLine(indent + "}");
    }
}
