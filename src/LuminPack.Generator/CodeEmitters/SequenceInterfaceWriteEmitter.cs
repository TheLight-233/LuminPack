using System.Collections.Generic;
using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Preserves public sequence traversal while keeping concrete enumerators unboxed.</summary>
internal static class SequenceInterfaceWriteEmitter
{
    /// <summary>Recognizes the five sequence interfaces with the shared collection representation.</summary>
    /// <param name="name">The canonical collection name without type arguments.</param>
    /// <returns>Whether the collection is a supported sequence interface.</returns>
    internal static bool IsInterface(string name) => name is "global::System.Collections.Generic.IEnumerable" or
        "global::System.Collections.Generic.ICollection" or "global::System.Collections.Generic.IList" or
        "global::System.Collections.Generic.IReadOnlyCollection" or "global::System.Collections.Generic.IReadOnlyList";

    /// <summary>Recognizes framework wrappers whose public enumeration delegates to their backing list.</summary>
    /// <param name="name">The canonical collection name without type arguments.</param>
    /// <returns>Whether the collection is a supported wrapper.</returns>
    internal static bool IsWrapper(string name) => name is "global::System.Collections.ObjectModel.Collection" or
        "global::System.Collections.ObjectModel.ObservableCollection" or "global::System.Collections.ObjectModel.ReadOnlyCollection" or
        "global::System.Collections.ObjectModel.ReadOnlyObservableCollection";

    /// <summary>Emits concrete loops and the original fallback, retaining the caller's representation and publication.</summary>
    /// <param name="field">The sequence and element contracts.</param>
    /// <param name="sb">The operation receiving generated statements.</param>
    /// <param name="json">Whether to use JSON element framing.</param>
    /// <param name="fixedHeader">Whether the binary wrapper already published its original header.</param>
    /// <param name="storageExpression">An optional already-classified grouping storage probe, excluding unrelated concrete sequence tests.</param>
    internal static void Append(LuminLocalFieldData field, StringBuilder sb, bool json, bool fixedHeader = false, string storageExpression = null)
    {
        string element = CodeEmitterRegistry.GetFirstGeneric(field.TypeName);
        string runtime = InterfaceCollectionEmitterHelper.RuntimeName(EmitterTypeTraits.GetFirstTypeArgument(field), element);
        string name = field.TypeName.Substring(0, field.TypeName.IndexOf('<'));
        bool counted = !json && !fixedHeader;
        if (fixedHeader)
        {
            sb.AppendLine("            if (__wrapperCount != 0 && global::LuminPack.Code.LuminPackCollectionAccess.GetSequenceStorage<" + element + ">(value) is " + element + "[] sequenceArray)");
            sb.AppendLine("            {");
            AppendLoop(field, sb, "sequenceArray", json: false, fixedHeader: true);
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            AppendLoop(field, sb, "value", json: false, fixedHeader: true);
            sb.AppendLine("            }");
            return;
        }
        if (counted)
        {
            sb.AppendLine("            int sequenceStart = writer.BeginCollection();");
            sb.AppendLine("            int sequenceCount = 0;");
        }
        var types = new List<string>();
        if (IsInterface(name) && storageExpression == null)
        {
            if (json)
            {
                types.Add("List");
                types.Add("Array"); }
            if (name is "global::System.Collections.Generic.IEnumerable" or "global::System.Collections.Generic.IReadOnlyCollection")
            {
                types.Add("Queue");
                types.Add("Stack");
                types.Add("LinkedList"); }
            else if (name == "global::System.Collections.Generic.ICollection")
                types.Add("LinkedList");
        }
        string Concrete(string kind, string item) => kind == "Array" ? item + "[]" : "global::System.Collections.Generic." + kind + "<" + item + ">";
        for (int i = 0; i < types.Count; i++)
        {
            sb.AppendLine("            " + (i == 0 ? "if" : "else if") + " (value.GetType() == typeof(" + Concrete(types[i], runtime) + "))");
            sb.AppendLine("            {");
            sb.AppendLine("                var source = global::System.Runtime.CompilerServices.Unsafe.As<" + Concrete(types[i], element) + ">(value);");
            if (counted)
            {
                sb.AppendLine("                if (source.Count != 0) sequenceCount = WriteSequence" + i + "(ref writer, source);");
            }
            else
                sb.AppendLine("                WriteSequence" + i + "(ref writer, source);");
            sb.AppendLine("            }");
        }
        if (field.CanGetSequenceStorage)
        {
            string storage = storageExpression ?? "global::LuminPack.Code.LuminPackCollectionAccess.GetSequenceStorage<" + element + ">(value)";
            sb.AppendLine("            " + (types.Count == 0 ? "if" : "else if") + " (" + storage + " is { } sequenceStorage)");
            sb.AppendLine("            {");
            foreach (string kind in new[] { "List", "Array" })
            {
                sb.AppendLine(kind == "List" ? "                if (sequenceStorage.GetType() == typeof(" + Concrete(kind, runtime) + "))" : "                else");
                sb.AppendLine("                {");
                sb.AppendLine("                    var source = global::System.Runtime.CompilerServices.Unsafe.As<" + Concrete(kind, element) + ">(sequenceStorage);");
                if (counted)
                {
                    sb.AppendLine("                    if (source." + (kind == "List" ? "Count" : "Length") + " != 0) sequenceCount = WriteSequence" + kind + "(ref writer, source);");
                }
                else
                    sb.AppendLine("                    " + (storageExpression != null ? "if (source." + (kind == "List" ? "Count" : "Length") + " != 0) " : "") + "WriteSequence" + kind + "(ref writer, source);");
                sb.AppendLine("                }");
            }
            sb.AppendLine("            }");
        }
        if (types.Count != 0 || field.CanGetSequenceStorage)
            sb.AppendLine("            else");
        sb.AppendLine("            {");
        if (counted)
            AppendCountedLoop(field, sb, "value", "sequenceCount");
        else
            AppendLoop(field, sb, "value", json, fixedHeader);
        sb.AppendLine("            }");
        if (counted)
        {
            sb.AppendLine("            writer.EndCollection(sequenceStart, sequenceCount);");
            sb.AppendLine("            writer.CheckBuffer();");
        }
        for (int i = 0; i < types.Count; i++)
            AppendFunction(field, sb, "WriteSequence" + i, Concrete(types[i], element), json, fixedHeader);
        if (field.CanGetSequenceStorage)
        {
            AppendFunction(field, sb, "WriteSequenceList", Concrete("List", element), json, fixedHeader);
            AppendFunction(field, sb, "WriteSequenceArray", Concrete("Array", element), json, fixedHeader);
        }
    }

    /// <summary>Separates concrete iterator state from the dispatcher's inline budget.</summary>
    /// <param name="field">The element contract.</param>
    /// <param name="sb">The operation receiving the helper.</param>
    /// <param name="name">The generated local function name.</param>
    /// <param name="type">The concrete sequence type.</param>
    /// <param name="json">Whether the loop writes JSON.</param>
    /// <param name="fixedHeader">Whether binary framing belongs to the caller.</param>
    private static void AppendFunction(LuminLocalFieldData field, StringBuilder sb, string name, string type, bool json, bool fixedHeader)
    {
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        bool counted = !json && !fixedHeader;
        sb.AppendLine("            static " + (counted ? "int" : "void") + " " + name + "(ref global::LuminPack.Core." + (json ? "LuminPackJsonWriter" : "LuminPackWriter") + " writer, " + type + " source)");
        sb.AppendLine("            {");
        if (counted)
        {
            sb.AppendLine("                int count = 0;");
            AppendCountedLoop(field, sb, "source", "count");
            sb.AppendLine("                return count;");
        }
        else
            AppendLoop(field, sb, "source", json, fixedHeader);
        sb.AppendLine("            }");
    }

    /// <summary>Returns the observed count while the caller retains the original header and state publication boundary.</summary>
    /// <param name="field">The element contract.</param>
    /// <param name="sb">The generated statements.</param>
    /// <param name="source">The public sequence.</param>
    /// <param name="count">The running count, initialized by the caller.</param>
    private static void AppendCountedLoop(LuminLocalFieldData field, StringBuilder sb, string source, string count)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(field);
        string type = CodeEmitterRegistry.GetFirstGeneric(field.TypeName);
        sb.AppendLine("                foreach (var item in " + source + ")");
        sb.AppendLine("                {");
        if (!CollectionEmitterKit.ChecksItself(element))
            sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    " + count + "++;");
        sb.AppendLine("                    var v = item;");
        sb.AppendLine("                    " + CollectionEmitterKit.ContiguousElementWrite(element, type, "v"));
        sb.AppendLine("                }");
    }

    /// <summary>Keeps the original scalar operation, callback ordering and header publication.</summary>
    /// <param name="field">The sequence element contract.</param>
    /// <param name="sb">The generated statements.</param>
    /// <param name="source">The enumerated value.</param>
    /// <param name="json">Whether JSON element framing is needed.</param>
    /// <param name="fixedHeader">Whether the caller already owns binary framing.</param>
    private static void AppendLoop(LuminLocalFieldData field, StringBuilder sb, string source, bool json, bool fixedHeader)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(field);
        string type = CodeEmitterRegistry.GetFirstGeneric(field.TypeName);
        if (json)
            sb.AppendLine("                bool isFirst = true;");
        sb.AppendLine("                foreach (var item in " + source + ")");
        sb.AppendLine("                {");
        if (json)
            JsonCollectionElementEmitter.Append(sb, "item", "                    ");
        else
        {
            sb.AppendLine("                    writer.CheckBuffer();");
            sb.AppendLine("                    var temp = item;");
            sb.AppendLine("                    " + CollectionEmitterKit.ContiguousElementWrite(element, type, "temp"));
        }
        sb.AppendLine("                }");
    }
}
