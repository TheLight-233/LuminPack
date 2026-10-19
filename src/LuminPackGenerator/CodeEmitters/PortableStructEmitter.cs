using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Emits padding-free transfers while retaining the existing Local/As storage mechanism.</summary>
internal static class PortableStructEmitter
{
    /// <summary>Names an ABI-preserving storage view independently of serialized member filters.</summary>
    /// <param name="plan">The aggregate storage plan.</param>
    /// <returns>A collision-free name in the existing generated extension container.</returns>
    private static string ViewName(PortableStructPlan plan)
        => "LocalPortable" + LuminPackLocalLayoutGenerator.GetLocalDeclarationName(plan.Type);

    /// <summary>Appends all opted-in storage views reachable from one operation's type graph.</summary>
    /// <param name="sb">The operation container.</param>
    /// <param name="type">The operation type.</param>
    internal static void AppendViews(StringBuilder sb, ITypeSymbol type)
    {
        if (sb.ToString().IndexOf("LocalPortable", System.StringComparison.Ordinal) < 0) return;
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var emitted = new HashSet<string>();
        Visit(type);
        // Visit symbols once; unmanaged storage graphs cannot contain cycles.
        void Visit(ITypeSymbol current)
        {
            if (current == null || !visited.Add(current)) return;
            var plan = PortableStructPlan.TryCreate(current);
            if (plan != null) { AppendView(plan); return; }
            if (current is IArrayTypeSymbol array) Visit(array.ElementType);
            if (current is not INamedTypeSymbol named) return;
            foreach (var argument in named.TypeArguments) Visit(argument);
        }
        // Mirror the actual ABI, including Pack/Size and explicit offsets; only the wire is packed.
        void AppendView(PortableStructPlan plan)
        {
            if (plan.Fields == null || !emitted.Add(ViewName(plan))) return;
            var layout = plan.Type.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() ==
                "System.Runtime.InteropServices.StructLayoutAttribute");
            if (layout == null) sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Sequential)]");
            else
            {
                sb.Append("        [global::System.Runtime.InteropServices.StructLayout((global::System.Runtime.InteropServices.LayoutKind)" + layout.ConstructorArguments[0].Value);
                foreach (var argument in layout.NamedArguments)
                    sb.Append(", " + argument.Key + " = " + (argument.Key == "CharSet" ? "(global::System.Runtime.InteropServices.CharSet)" : "") + argument.Value.Value);
                sb.AppendLine(")]");
            }
            sb.AppendLine("        private struct " + ViewName(plan));
            sb.AppendLine("        {");
            for (int index = 0; index < plan.Fields.Length; index++)
            {
                var offset = plan.Fields[index].GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() ==
                    "System.Runtime.InteropServices.FieldOffsetAttribute");
                if (offset != null) sb.AppendLine("            [global::System.Runtime.InteropServices.FieldOffset(" + offset.ConstructorArguments[0].Value + ")]");
                sb.AppendLine("            internal " + (plan.Fields[index].IsVolatile ? "volatile " : "") + FormatterTypeName.Get(plan.Fields[index].Type) + " F" + index + ";");
            }
            sb.AppendLine("        }");
            foreach (var child in plan.Children) AppendView(child);
        }
    }

    /// <summary>Emits one unchecked fixed transfer and advances its existing state cursor.</summary>
    /// <param name="sb">The statements being generated.</param>
    /// <param name="plan">The packed storage plan.</param>
    /// <param name="value">The caller's value storage.</param>
    /// <param name="sourceType">The declared type of that storage.</param>
    /// <param name="write">Whether to write rather than read.</param>
    internal static void AppendValue(StringBuilder sb, PortableStructPlan plan, string value, string sourceType, bool write)
    {
        string state = write ? "writer" : "reader";
        sb.AppendLine("            {");
        sb.AppendLine("                int __portableIndex = " + state + ".GetCurrentSpanIndex();");
        if (!write) sb.AppendLine("                reader.EnsureReadable(__portableIndex, " + plan.WireSize + ");");
        sb.AppendLine("                if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        sb.AppendLine("                {");
        AppendTransfer(sb, plan, value, sourceType, "__portableIndex", "                    ", write, true);
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        AppendTransfer(sb, plan, value, sourceType, "__portableIndex", "                    ", write, false);
        sb.AppendLine("                }");
        sb.AppendLine("                " + state + ".Advance(" + plan.WireSize + ");");
        sb.AppendLine("            }");
    }

    /// <summary>Writes or reads a counted array/List directly through its contiguous element storage.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="element">The actual element symbol.</param>
    /// <param name="list">Whether the container is a List.</param>
    /// <param name="write">Whether to write rather than read.</param>
    /// <param name="memoryType">An optional supported contiguous memory container name.</param>
    /// <param name="freshList">Whether the existing operation owns a newly constructed list.</param>
    /// <returns>Whether the portable element path was emitted.</returns>
    internal static bool TryAppendCollection(StringBuilder sb, ITypeSymbol element, bool list, bool write, string memoryType = null, bool freshList = false)
    {
        if (memoryType != null && memoryType is not ("global::System.Memory" or "global::System.ReadOnlyMemory" or "global::System.ArraySegment")) return false;
        var plan = PortableStructPlan.TryCreate(element);
        if (plan == null) return false;
        string type = FormatterTypeName.Get(element);
        string state = write ? "writer" : "reader";
        sb.AppendLine("            ref int index = ref " + state + ".GetCurrentSpanOffset();");
        if (write)
        {
            if (memoryType == null || memoryType == "global::System.ArraySegment")
            {
                sb.AppendLine(memoryType == null ? "            if (value is null)" : "            if (value.Array is null)");
                sb.AppendLine("            {");
                sb.AppendLine("                writer.EnsureAdditionalCapacity(4);");
                sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
                sb.AppendLine("                writer.Advance(4);");
                sb.AppendLine("                return;");
                sb.AppendLine("            }");
            }
            sb.AppendLine(list ? "            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value));" :
                memoryType is "global::System.Memory" or "global::System.ReadOnlyMemory" ? "            var span = value.Span;" : "            var span = value.AsSpan();");
            sb.AppendLine("            int length = span.Length;");
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(4 + length * " + plan.WireSize + "));");
            sb.AppendLine("            writer.WriteCollectionHeader(ref index, length);");
            sb.AppendLine("            writer.Advance(4);");
        }
        else
        {
            sb.AppendLine("            reader.EnsureCollectionPayload(index, 4);");
            sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out int length))");
            sb.AppendLine("            {");
            sb.AppendLine(memoryType == null ? "                value = null;" : "                value = default;");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            reader.EnsureCollectionPayload(index, checked(4 + length * " + plan.WireSize + "));");
            sb.AppendLine("            reader.Advance(4);");
            if (memoryType != null)
            {
                sb.AppendLine("            var __portableArray = new " + type + "[length];");
                sb.AppendLine("            var span = __portableArray.AsSpan();");
            }
            else if (list)
            {
                if (freshList)
                {
                    sb.AppendLine("            var __portableArray = reader.CreateFreshListArray<" + type + ">(length, out value);");
                    sb.AppendLine("            var span = __portableArray.AsSpan();");
                }
                else
                {
                    sb.AppendLine("            if (value is null) value = new global::System.Collections.Generic.List<" + type + ">(length);");
                    sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
                }
            }
            else
            {
                sb.AppendLine("            if (value is null || value.Length != length) value = LuminPackMarshal.AllocateUninitializedArray<" + type + ">(length);");
                sb.AppendLine("            var span = value.AsSpan();");
            }
        }
        AppendPayload(sb, plan, type, "span[__portableItem]", "length", write);
        if (write) sb.AppendLine("            writer.CheckBuffer();");
        else if (memoryType != null) sb.AppendLine("            value = new " + memoryType + "<" + type + ">(__portableArray);");
        return true;
    }

    /// <summary>Emits an indexed fixed-width payload after its caller reserves or validates the complete container.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="plan">The element storage plan.</param>
    /// <param name="type">The declared element type.</param>
    /// <param name="element">An element expression indexed by __portableItem.</param>
    /// <param name="length">The stable element count.</param>
    /// <param name="write">Whether to write rather than read.</param>
    internal static void AppendPayload(StringBuilder sb, PortableStructPlan plan, string type, string element, string length, bool write)
    {
        sb.AppendLine("            int __portableIndex = " + (write ? "writer" : "reader") + ".GetCurrentSpanIndex();");
        sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        AppendLoop(true);
        sb.AppendLine("            else");
        AppendLoop(false);
        sb.AppendLine("            " + (write ? "writer" : "reader") + ".Advance(checked(" + length + " * " + plan.WireSize + "));");
        // The architecture decision stays outside the generated element loop.
        void AppendLoop(bool typed)
        {
            sb.AppendLine("            {");
            sb.AppendLine("                for (int __portableItem = 0; __portableItem < " + length + "; __portableItem++)");
            sb.AppendLine("                {");
            AppendTransfer(sb, plan, element, type, "__portableIndex", "                    ", write, typed);
            sb.AppendLine("                    __portableIndex += " + plan.WireSize + ";");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Recursively emits scalar field operations; nested aggregates are never copied as blocks.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="plan">The complete recursive storage plan.</param>
    /// <param name="value">The value storage expression.</param>
    /// <param name="sourceType">The declared storage type.</param>
    /// <param name="index">The absolute payload cursor.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="write">Whether to write rather than read.</param>
    /// <param name="typed">Whether the architecture permits typed unaligned access.</param>
    private static void AppendTransfer(StringBuilder sb, PortableStructPlan plan, string value, string sourceType,
        string index, string indent, bool write, bool typed)
    {
        int position = 0, identity = 0;
        Visit(plan, value, sourceType, false);
        // Each view aliases existing CLR storage, retaining GC-tracked references and the original ABI.
        void Visit(PortableStructPlan current, string expression, string type, bool isVolatile)
        {
            if (current.Fields != null)
            {
                string local = "__portableStorage" + identity++;
                sb.AppendLine(indent + "ref var " + local + " = ref LuminPackMarshal.As<" + type + ", " + ViewName(current) +
                    ">(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in " + expression + "));");
                for (int field = 0; field < current.Fields.Length; field++)
                    Visit(current.Children[field], local + ".F" + field, FormatterTypeName.Get(current.Fields[field].Type), current.Fields[field].IsVolatile);
                return;
            }
            string offset = "__portableField" + identity++;
            if (isVolatile && write)
            {
                sb.AppendLine(indent + "var " + offset + "Snapshot = " + expression + ";");
                expression = offset + "Snapshot";
            }
            if (typed)
            {
                string address = (write ? "writer" : "reader") + ".GetSpanReference(" + index + " + " + position + ")";
                sb.AppendLine(indent + (write
                    ? "global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref " + address + ", " + expression + ");"
                    : expression + " = global::System.Runtime.CompilerServices.Unsafe.ReadUnaligned<" + type + ">(ref " + address + ");"));
            }
            else
            {
                sb.AppendLine(indent + "int " + offset + " = " + index + " + " + position + ";");
                sb.AppendLine(indent + (write
                    ? "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + expression + ");"
                    : isVolatile ? "reader.ReadUnmanagedWithoutSizeReturn<" + type + ">(ref " + offset + ", out var " + offset + "Value);"
                    : "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + expression + ");"));
                if (isVolatile && !write) sb.AppendLine(indent + expression + " = " + offset + "Value;");
            }
            position += current.WireSize;
        }
    }
}
