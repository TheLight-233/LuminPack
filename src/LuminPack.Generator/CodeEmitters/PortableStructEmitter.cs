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

    /// <summary>Names the generated offset description consumed by the vectorized transfer.</summary>
    /// <param name="plan">The aggregate storage plan.</param>
    /// <returns>A collision-free name beside the storage view.</returns>
    private static string LayoutName(PortableStructPlan plan)
        => "LocalPortableLayout" + LuminPackLocalLayoutGenerator.GetLocalDeclarationName(plan.Type);

    /// <summary>Recognizes storage that must keep its field-wise volatile accesses.</summary>
    /// <param name="plan">The storage plan.</param>
    /// <returns>Whether any expanded field is volatile.</returns>
    private static bool HasVolatile(PortableStructPlan plan)
    {
        if (plan.Fields == null) return false;
        for (int index = 0; index < plan.Fields.Length; index++)
            if (plan.Fields[index].IsVolatile || HasVolatile(plan.Children[index])) return true;
        return false;
    }

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
            if (plan.Type is INamedTypeSymbol { IsTupleType: true })
            {
                // A tagged tuple has no view of its own, but its payload helper still needs the layout models.
                if (plan.Tagged && !HasVolatile(plan)) AppendLayout(plan);
                foreach (var child in plan.Children) AppendView(child);
                return;
            }
            StorageLayoutInfo.Get((INamedTypeSymbol)plan.Type).AppendAttribute(sb, "        ");
            sb.AppendLine("        private struct " + ViewName(plan));
            sb.AppendLine("        {");
            for (int index = 0; index < plan.Fields.Length; index++)
            {
                int offset = plan.Fields[index].Offset;
                if (offset >= 0) sb.AppendLine("            [global::System.Runtime.InteropServices.FieldOffset(" + offset + ")]");
                sb.AppendLine("            internal " + (plan.Fields[index].IsVolatile ? "volatile " : "") + FormatterTypeName.Get(plan.Fields[index].Type) + " F" + index + ";");
            }
            sb.AppendLine("        }");
            if (!HasVolatile(plan)) AppendLayout(plan);
            foreach (var child in plan.Children) AppendView(child);
        }
        // Offsets come from the running process's field addresses, so the description holds on every architecture and pack.
        void AppendLayout(PortableStructPlan plan)
        {
            string type = FormatterTypeName.Get(plan.Type);
            sb.AppendLine("        private readonly struct " + LayoutName(plan) + " : global::LuminPack.Utility." +
                (plan.Tagged ? "ILuminStorageLayout" : "ILuminPortableLayout"));
            sb.AppendLine("        {");
            sb.AppendLine("            public int WireSize => " + plan.WireSize + ";");
            sb.AppendLine("            public void Describe(global::System.Span<int> storageOffsets)");
            sb.AppendLine("            {");
            sb.AppendLine("                " + type + " __probe = default;");
            sb.AppendLine("                ref byte __origin = ref global::System.Runtime.CompilerServices.Unsafe.As<" + type + ", byte>(ref __probe);");
            sb.AppendLine("                int __next = 0;");
            int identity = 0;
            Describe(plan, "__probe", type);
            sb.AppendLine("            }");
            if (plan.Tagged) AppendModels(sb, plan);
            sb.AppendLine("        }");
            void Describe(PortableStructPlan current, string expression, string currentType)
            {
                if (current.Fields != null)
                {
                    if (current.Type is INamedTypeSymbol { IsTupleType: true })
                    {
                        for (int field = 0; field < current.Fields.Length; field++)
                            Describe(current.Children[field], expression + "." + current.Fields[field].Name, FormatterTypeName.Get(current.Fields[field].Type));
                        return;
                    }
                    string local = "__view" + identity++;
                    sb.AppendLine("                ref var " + local + " = ref LuminPackMarshal.As<" + currentType + ", " + ViewName(current) + ">(ref " + expression + ");");
                    for (int field = 0; field < current.Fields.Length; field++)
                        Describe(current.Children[field], local + ".F" + field, FormatterTypeName.Get(current.Fields[field].Type));
                    return;
                }
                string offset = "__offset" + identity++;
                sb.AppendLine("                int " + offset + " = (int)global::System.Runtime.CompilerServices.Unsafe.ByteOffset(ref __origin, ref global::System.Runtime.CompilerServices.Unsafe.As<" + currentType + ", byte>(ref " + expression + "));");
                sb.AppendLine("                for (int __byte = 0; __byte < " + current.WireSize + "; __byte++) storageOffsets[__next++] = " + offset + " + __byte;");
            }
        }
    }

    /// <summary>Emits the generated layout models that a tagged payload names with its one-byte tag.</summary>
    /// <param name="sb">The layout declaration being generated.</param>
    /// <param name="plan">The tagged storage plan.</param>
    private static void AppendModels(StringBuilder sb, PortableStructPlan plan)
    {
        sb.AppendLine("            public int ModelCount => " + plan.Models.Length + ";");
        sb.AppendLine("            public int ModelSize(int tag)");
        sb.AppendLine("            {");
        for (int model = 0; model < plan.Models.Length; model++)
            sb.AppendLine("                if (tag == " + (model + 1) + ") return " + plan.Models[model].Size + ";");
        sb.AppendLine("                return 0;");
        sb.AppendLine("            }");
        sb.AppendLine("            public void DescribeModel(int tag, global::System.Span<int> storageOffsets)");
        sb.AppendLine("            {");
        for (int model = 0; model < plan.Models.Length; model++)
        {
            var offsets = plan.Models[model].Offsets;
            sb.AppendLine("                if (tag == " + (model + 1) + ")");
            sb.AppendLine("                {");
            int start = 0;
            for (int index = 1; index <= offsets.Length; index++)
            {
                if (index < offsets.Length && offsets[index] == offsets[index - 1] + 1) continue;
                sb.AppendLine("                    for (int __i = 0; __i < " + (index - start) + "; __i++) storageOffsets[" + start + " + __i] = " + offsets[start] + " + __i;");
                start = index;
            }
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("            }");
    }

    /// <summary>Names the runtime tag helper of a tagged payload.</summary>
    /// <param name="plan">The tagged storage plan.</param>
    /// <returns>The closed helper type.</returns>
    private static string TagHelper(PortableStructPlan plan)
        => "global::LuminPack.Utility.LuminStorageTag<" + FormatterTypeName.Get(plan.Type) + ", " + LayoutName(plan) + ">";

    /// <summary>Spells the bytes a contiguous payload of <paramref name="count"/> elements occupies, including a layout tag.</summary>
    /// <param name="plan">The storage plan.</param>
    /// <param name="count">An expression for the element count.</param>
    /// <returns>An int expression; packed payloads are count times the packed width, tagged payloads add one tag byte when nonempty.</returns>
    internal static string PayloadSize(PortableStructPlan plan, string count)
        => plan.Tagged ? "(" + count + " == 0 ? 0 : 1 + " + count + " * " + TagHelper(plan) + ".Width)" : count + " * " + plan.WireSize;

    /// <summary>Spells the fewest bytes a contiguous payload of <paramref name="count"/> elements can occupy.</summary>
    /// <param name="plan">The storage plan.</param>
    /// <param name="count">An expression for the element count.</param>
    /// <returns>An int expression valid for any tag, used to reject forged counts before reading the tag.</returns>
    internal static string PayloadMinimum(PortableStructPlan plan, string count)
        => plan.Tagged ? "(" + count + " == 0 ? 0 : 1 + " + count + " * " + plan.Models.Min(model => model.Size) + ")" : count + " * " + plan.WireSize;

    /// <summary>Validates the selected tagged payload before any count-based allocation or reuse mutation.</summary>
    /// <param name="sb">The generated reader statements.</param>
    /// <param name="plan">The collection element layout.</param>
    /// <param name="count">The stable element count expression.</param>
    /// <param name="index">The payload cursor expression, immediately before its optional tag.</param>
    internal static void AppendReadValidation(StringBuilder sb, PortableStructPlan plan, string count, string index)
    {
        if (!plan.Tagged) return;
        sb.AppendLine("            if (" + count + " != 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.EnsureCollectionPayload(" + index + ", 1);");
        sb.AppendLine("                int __validatedWidth = " + TagHelper(plan) + ".WidthOf(reader.GetSpanReference(" + index + "));");
        sb.AppendLine("                reader.EnsureCollectionPayload(" + index + " + 1, checked(" + count + " * __validatedWidth));");
        sb.AppendLine("            }");
    }

    /// <summary>Validates one complete read range before its fixed transfer and advances the existing state cursor.</summary>
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
        if (!write)
        {
            sb.AppendLine("                reader.EnsureReadable(__portableIndex, " + plan.WireSize + ");");
        }
        sb.AppendLine("                ref byte __portableWire = ref " + state + ".GetSpanReference(__portableIndex);");
        sb.AppendLine("                if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        sb.AppendLine("                {");
        AppendTransfer(sb, plan, value, sourceType, "__portableWire", "                    ", write, true);
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        AppendTransfer(sb, plan, value, sourceType, "__portableWire", "                    ", write, false);
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
            sb.AppendLine("            writer.EnsureAdditionalCapacity(checked(4 + " + PayloadSize(plan, "length") + "));");
            sb.AppendLine("            writer.WriteCollectionHeader(ref index, length);");
            sb.AppendLine("            writer.Advance(4);");
        }
        else
        {
            sb.AppendLine("            reader.EnsureCollectionPayload(index, 4);");
            sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out int length" + (plan.WireSize == 0 ? ", 0" : "") + "))");
            sb.AppendLine("            {");
            sb.AppendLine(memoryType == null ? "                value = null;" : "                value = default;");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine("            reader.EnsureCollectionPayload(index, checked(4 + " + PayloadMinimum(plan, "length") + "));");
            AppendReadValidation(sb, plan, "length", "index + 4");
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
        AppendPayload(sb, plan, type, "global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span)", "length", write);
        if (write) sb.AppendLine("            writer.CheckBuffer();");
        else if (memoryType != null) sb.AppendLine("            value = new " + memoryType + "<" + type + ">(__portableArray);");
        return true;
    }

    /// <summary>Emits a contiguous fixed-width payload after its caller reserves or validates the complete container.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="plan">The element storage plan.</param>
    /// <param name="type">The declared element type.</param>
    /// <param name="first">A reference expression to the first element.</param>
    /// <param name="length">The stable element count.</param>
    /// <param name="write">Whether to write rather than read.</param>
    /// <remarks>
    /// Leading elements use the vectorized transfer when the storage has no volatile field; it reports how many it moved and
    /// the established field-wise loops finish the rest from that element, with 32-bit execution keeping its block-copy cores.
    /// </remarks>
    internal static void AppendPayload(StringBuilder sb, PortableStructPlan plan, string type, string first, string length, bool write)
    {
        string state = write ? "writer" : "reader";
        sb.AppendLine("            int __portableIndex = " + state + ".GetCurrentSpanIndex();");
        sb.AppendLine("            ref byte __portableWire = ref " + state + ".GetSpanReference(__portableIndex);");
        sb.AppendLine("            ref " + type + " __portableFirst = ref " + first + ";");
        if (plan.Tagged)
        {
            // The tag names the native model of the following elements; an empty payload carries neither.
            string helper = TagHelper(plan);
            sb.AppendLine("            if (" + length + " != 0)");
            sb.AppendLine("            {");
            if (write)
            {
                sb.AppendLine("                __portableWire = " + helper + ".Tag;");
                sb.AppendLine("                " + helper + ".Pack(ref __portableFirst, " + length + ", ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __portableWire, 1));");
                sb.AppendLine("                writer.Advance(checked(1 + " + length + " * " + helper + ".Width));");
            }
            else
            {
                sb.AppendLine("                reader.EnsureCollectionPayload(__portableIndex, 1);");
                sb.AppendLine("                byte __portableTag = __portableWire;");
                sb.AppendLine("                int __portableWidth = " + helper + ".WidthOf(__portableTag);");
                sb.AppendLine("                reader.EnsureCollectionPayload(__portableIndex + 1, checked(" + length + " * __portableWidth));");
                sb.AppendLine("                " + helper + ".Unpack(__portableTag, ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __portableWire, 1), " + length + ", ref __portableFirst);");
                sb.AppendLine("                reader.Advance(checked(1 + " + length + " * __portableWidth));");
            }
            sb.AppendLine("            }");
            return;
        }
        if (HasVolatile(plan))
            sb.AppendLine("            int __portableStart = 0;");
        else
        {
            string transfer = "global::LuminPack.Utility.LuminPortable<" + type + ", " + LayoutName(plan) + ">";
            sb.AppendLine("            int __portableStart = " + (write
                ? transfer + ".Pack(ref __portableFirst, " + length + ", ref __portableWire);"
                : transfer + ".Unpack(ref __portableWire, " + length + ", ref __portableFirst);"));
            sb.AppendLine("            __portableWire = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __portableWire, (nint)__portableStart * " + plan.WireSize + ");");
        }
        sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        AppendLoop(true);
        sb.AppendLine("            else");
        AppendLoop(false);
        sb.AppendLine("            " + state + ".Advance(checked(" + length + " * " + plan.WireSize + "));");
        // The architecture decision stays outside the generated element loop.
        void AppendLoop(bool typed)
        {
            sb.AppendLine("            {");
            sb.AppendLine("                for (int __portableItem = __portableStart; __portableItem < " + length + "; __portableItem++)");
            sb.AppendLine("                {");
            AppendTransfer(sb, plan, "global::System.Runtime.CompilerServices.Unsafe.Add(ref __portableFirst, __portableItem)", type, "__portableWire", "                    ", write, typed);
            sb.AppendLine("                    __portableWire = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __portableWire, " + plan.WireSize + ");");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Recursively emits scalar field operations; nested aggregates are never copied as blocks.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="plan">The complete recursive storage plan.</param>
    /// <param name="value">The value storage expression.</param>
    /// <param name="sourceType">The declared storage type.</param>
    /// <param name="target">A reference to the element's first wire byte.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="write">Whether to write rather than read.</param>
    /// <param name="typed">Whether the architecture permits typed unaligned access.</param>
    private static void AppendTransfer(StringBuilder sb, PortableStructPlan plan, string value, string sourceType,
        string target, string indent, bool write, bool typed)
    {
        int position = 0, identity = 0;
        Visit(plan, value, sourceType, false);
        // Each view aliases existing CLR storage, retaining GC-tracked references and the original ABI.
        void Visit(PortableStructPlan current, string expression, string type, bool isVolatile)
        {
            if (current.Fields != null)
            {
                if (current.Type is INamedTypeSymbol { IsTupleType: true })
                {
                    for (int field = 0; field < current.Fields.Length; field++)
                        Visit(current.Children[field], expression + "." + current.Fields[field].Name, FormatterTypeName.Get(current.Fields[field].Type), current.Fields[field].IsVolatile);
                    return;
                }
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
            string address = "global::System.Runtime.CompilerServices.Unsafe.Add(ref " + target + ", " + position + ")";
            if (typed)
                sb.AppendLine(indent + (write
                    ? "global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned(ref " + address + ", " + expression + ");"
                    : expression + " = global::System.Runtime.CompilerServices.Unsafe.ReadUnaligned<" + type + ">(ref " + address + ");"));
            else
            {
                // 32-bit execution keeps the established block-copy scalar cores.
                sb.AppendLine(indent + (write
                    ? "global::LuminPack.Core.LuminPackWriter.Write(ref " + address + ", " + expression + ");"
                    : isVolatile ? "global::LuminPack.Core.LuminPackReader.Read(ref " + address + ", out " + type + " " + offset + "Value);"
                    : "global::LuminPack.Core.LuminPackReader.Read(ref " + address + ", out " + expression + ");"));
                if (isVolatile && !write) sb.AppendLine(indent + expression + " = " + offset + "Value;");
            }
            position += current.WireSize;
        }
    }
}
