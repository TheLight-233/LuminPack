using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{
    /// <summary>Writes ordinary closed fields as consecutive records at a cursor held in a local.</summary>
    /// <param name="data">The canonical object and storage contract.</param>
    /// <param name="builder">The existing operation builder after null handling and storage selection.</param>
    /// <param name="polymorphism">Whether the caller already wrote the Union framing.</param>
    /// <remarks>Fixed fields, strings, raw arrays and lists and raw-pair dictionaries are bounded records the writer stores at
    /// <c>__cursor</c>, a local no call can address. Consecutive bounded records of one profile read each member once, reserve
    /// their summed bound with one check and store without further checks; a lone record checks itself. Bounded-entry
    /// dictionaries check each entry in their member kernel. Every other member publishes the cursor, runs its original
    /// operation and resumes; consecutive such members share one publication. A type without a variable record keeps its
    /// original fixed writes, which fixed Union cases project onto their local index.</remarks>
    private static void GenerateCursorObjectWrite(LuminDataInfo data, StringBuilder builder, bool polymorphism)
    {
        const string indent = "            ";
        string Storage(LuminDataField member) => (member.IsPrivate || member.isProperty ? "local." : "value.") + member.Identifier;
        var fields = data.fields;
        if (!fields.Any(field => ReservedKind(field) > 1))
        {
            if (polymorphism && fields.Count != 0 && fields.All(field => !field.IsCompress && IsMergeableField(field)))
            {
                // The ordinary element check leaves at least 4 KiB: one eighth of the runtime's
                // 32 KiB initial buffer. Account for the three-byte maximum Union header before
                // deciding whether the fixed payload needs a complete reservation. Small constant
                // widths fold this branch away.
                string width = string.Join(" + ", fields.Select(GetUnmanagedFieldSize));
                builder.AppendLine(indent + "if (checked(3 + " + width + ") > 4096) writer.EnsureAdditionalCapacity(checked(" + width + "));");
            }
            if (!polymorphism) builder.AppendLine(indent + "writer.Write((byte)" + fields.Count + ");");
            for (int index = 0; index < fields.Count; index++)
                index = AppendOriginalMembers(builder, fields, index, fields.Count - 1, Storage);
            return;
        }
        builder.AppendLine(indent + "ref byte __cursor = ref writer.GetCurrentSpanReference();");
        bool header = !polymorphism;
        bool current = true;
        for (int index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            int kind = ReservedKind(field);
            if (kind is 0 or 6)
            {
                if (header) builder.AppendLine(indent + "__cursor = ref writer.WriteRecord(ref __cursor, (byte)" + fields.Count + ");");
                header = false;
                if (kind == 6)
                {
                    if (!current) builder.AppendLine(indent + "__cursor = ref writer.GetCurrentSpanReference();");
                    current = true;
                    using var entries = new CompiledOptionFieldScope(builder, field.Options);
                    AppendRecordEntries(builder, field, index, Storage, indent);
                    continue;
                }
                if (current) builder.AppendLine(indent + "writer.CommitRecords(ref __cursor);");
                current = false;
                index = AppendOriginalMembers(builder, fields, index, index, Storage);
                continue;
            }
            if (!current) builder.AppendLine(indent + "__cursor = ref writer.GetCurrentSpanReference();");
            current = true;
            int last = index;
            while (last + 1 < fields.Count && ReservedKind(fields[last + 1]) is int next && next is > 0 and < 6 && ScopeKey(fields[last + 1]) == ScopeKey(field)) last++;
            AppendRecordGroup(builder, fields, index, last, header ? fields.Count : -1, Storage, indent);
            header = false;
            index = last;
        }
        if (header) builder.AppendLine(indent + "__cursor = ref writer.WriteRecord(ref __cursor, (byte)" + fields.Count + ");");
        if (current) builder.AppendLine(indent + "writer.CommitRecords(ref __cursor);");
    }

    /// <summary>Identifies the compiled string profile a member's operations are specialized with.</summary>
    /// <param name="field">The wire field.</param>
    /// <returns>The declared profile type, or empty for the enclosing profile.</returns>
    private static string ScopeKey(LuminDataField field) => field.Options?.IsDeclared == true ? field.Options.ScopeTypeName : string.Empty;

    /// <summary>Writes consecutive bounded records of one profile, reserving their summed bound once when there are several.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="fields">The ordered wire fields.</param>
    /// <param name="first">The first member.</param>
    /// <param name="last">The last member.</param>
    /// <param name="header">The member count header to write first, or -1.</param>
    /// <param name="Storage">The field storage expression.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <remarks>Variable members are read once into locals, so their bounds and their stores use the same strings, array and
    /// list snapshots and dictionary counts, and concurrent mutation cannot make a store leave the reserved room.</remarks>
    private static void AppendRecordGroup(StringBuilder builder, List<LuminDataField> fields, int first, int last, int header, Func<LuminDataField, string> Storage, string indent)
    {
        const string writerType = "global::LuminPack.Core.LuminPackWriter";
        using var scope = new CompiledOptionFieldScope(builder, fields[first].Options);
        if (first == last && header < 0)
        {
            // A lone record checks itself.
            var field = fields[first];
            int kind = ReservedKind(field);
            if (kind == 1) builder.AppendLine(indent + "__cursor = ref writer.WriteRecord(ref __cursor, " + Storage(field) + ");");
            else if (kind == 2) builder.AppendLine(indent + "__cursor = ref writer.WriteRecordString(ref __cursor, " + Storage(field) + ");");
            else if (kind is 3 or 4) builder.AppendLine(indent + "__cursor = ref writer.WriteRecordSpan(ref __cursor, " + Storage(field) + ");");
            else builder.AppendLine(indent + "__cursor = ref writer.WriteRecordPairs(ref __cursor, " + Storage(field) + ");");
            return;
        }
        builder.AppendLine(indent + "{");
        var terms = new List<string>();
        if (header >= 0) terms.Add("1");
        for (int index = first; index <= last; index++)
        {
            var field = fields[index];
            int kind = ReservedKind(field);
            string name = "__m" + index;
            if (kind == 1) terms.Add(GetUnmanagedFieldSize(field));
            else if (kind == 2)
            {
                builder.AppendLine(indent + "    string " + name + " = " + Storage(field) + ";");
                terms.Add("writer.RecordBound(" + name + ")");
            }
            else
            {
                builder.AppendLine(indent + "    var " + name + " = " + writerType + ".Capture(" + Storage(field) + ");");
                terms.Add(name + ".Bound");
            }
        }
        builder.AppendLine(indent + "    __cursor = ref writer.ReserveRecord(ref __cursor, checked(" + string.Join(" + ", terms) + "));");
        if (header >= 0) builder.AppendLine(indent + "    __cursor = ref " + writerType + ".WriteReserved(ref __cursor, (byte)" + header + ");");
        for (int index = first; index <= last; index++)
        {
            var field = fields[index];
            int kind = ReservedKind(field);
            string name = "__m" + index;
            if (kind == 1)
            {
                int end = index;
                while (end + 1 <= last && ReservedKind(fields[end + 1]) == 1) end++;
                AppendReservedFixedGroup(builder, fields, index, end, Storage, indent + "    ");
                index = end;
            }
            else if (kind == 2) builder.AppendLine(indent + "    __cursor = ref writer.WriteReservedRecordString(ref __cursor, " + name + ");");
            else if (kind is 3 or 4) builder.AppendLine(indent + "    __cursor = ref " + writerType + ".WriteReservedSpanRecord(ref __cursor, " + name + ");");
            else builder.AppendLine(indent + "    __cursor = ref " + writerType + ".WriteReserved(ref __cursor, " + name + ");");
        }
        builder.AppendLine(indent + "}");
    }

    /// <summary>Stores consecutive fixed fields into reserved room, copying a proven contiguous block or each field.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="fields">The ordered wire fields.</param>
    /// <param name="first">The first fixed field.</param>
    /// <param name="last">The last fixed field.</param>
    /// <param name="Storage">The field storage expression.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <remarks>The block copy requires the actual field addresses to match the packed offsets on a 64-bit target; otherwise
    /// each field is stored at its packed offset through the architecture-aware fixed store.</remarks>
    private static void AppendReservedFixedGroup(StringBuilder builder, List<LuminDataField> fields, int first, int last, Func<LuminDataField, string> Storage, string indent)
    {
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        const string writerType = "global::LuminPack.Core.LuminPackWriter";
        if (first == last)
        {
            builder.AppendLine(indent + "__cursor = ref " + writerType + ".WriteReserved(ref __cursor, " + Storage(fields[first]) + ");");
            return;
        }
        string suffix = first.ToString();
        string Reference(LuminDataField member) => unsafeType + ".AsRef(in " + Storage(member) + ")";
        string length = GetUnmanagedFieldSize(fields[first]);
        var proofs = new List<string> { unsafeType + ".SizeOf<global::System.IntPtr>() == 8" };
        for (int member = first + 1; member <= last; member++)
        {
            proofs.Add(unsafeType + ".ByteOffset(ref __fixed" + suffix + ", ref " + unsafeType + ".As<" + GetGeneratedTypeName(fields[member]) + ", byte>(ref " + Reference(fields[member]) + ")) == (nint)(" + length + ")");
            length = AddUnmanagedFieldSize(length, GetUnmanagedFieldSize(fields[member]));
        }
        builder.AppendLine(indent + "ref byte __fixed" + suffix + " = ref " + unsafeType + ".As<" + GetGeneratedTypeName(fields[first]) + ", byte>(ref " + Reference(fields[first]) + ");");
        builder.AppendLine(indent + "if (" + string.Join(" && ", proofs) + ")");
        builder.AppendLine(indent + "    __cursor = ref " + writerType + ".WriteReservedBlock(ref __cursor, ref __fixed" + suffix + ", " + length + ");");
        builder.AppendLine(indent + "else");
        builder.AppendLine(indent + "{");
        for (int member = first; member <= last; member++)
            builder.AppendLine(indent + "    __cursor = ref " + writerType + ".WriteReserved(ref __cursor, " + Storage(fields[member]) + ");");
        builder.AppendLine(indent + "}");
    }

    /// <summary>Writes a bounded-entry dictionary through its member kernel, or its original operation when the kernel declines.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="field">The dictionary member.</param>
    /// <param name="index">The member index, used for unique names.</param>
    /// <param name="Storage">The field storage expression.</param>
    /// <param name="indent">The statement indentation.</param>
    private static void AppendRecordEntries(StringBuilder builder, LuminDataField field, int index, Func<LuminDataField, string> Storage, string indent)
    {
        string name = "__entries" + index;
        builder.AppendLine(indent + "{");
        builder.AppendLine(indent + "    var " + name + " = " + Storage(field) + ";");
        builder.AppendLine(indent + "    int " + name + "Width = writer.UsesUtf16LengthRecords() ? " + name + "Kernel(ref __cursor, writer.RecordRoom(ref __cursor), " + name + ") : -1;");
        builder.AppendLine(indent + "    if (" + name + "Width >= 0)");
        builder.AppendLine(indent + "        __cursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __cursor, (nint)(uint)" + name + "Width);");
        builder.AppendLine(indent + "    else");
        builder.AppendLine(indent + "    {");
        builder.AppendLine(indent + "        writer.CommitRecords(ref __cursor);");
        builder.AppendLine(indent + "        " + name + "Fallback(ref writer, " + name + ");");
        builder.AppendLine(indent + "        __cursor = ref writer.GetCurrentSpanReference();");
        builder.AppendLine(indent + "    }");
        AppendFallbackFunction(builder, field, name + "Fallback", indent + "    ");
        AppendEntryKernel(builder, field, name + "Kernel", indent + "    ");
        builder.AppendLine(indent + "}");
    }

    /// <summary>Emits a member's original operation as a static local function outside the hot path.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="field">The member.</param>
    /// <param name="name">The local function name.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <remarks>A declined kernel is rare, so its complete original operation stays out of the enclosing root, whose inlining
    /// and local-tracking budget then covers only the hot path.</remarks>
    private static void AppendFallbackFunction(StringBuilder builder, LuminDataField field, string name, string indent)
    {
        string type = field.TypeSymbol.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        builder.AppendLine(indent + "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        builder.AppendLine(indent + "static void " + name + "(ref global::LuminPack.Core.LuminPackWriter writer, " + type + " fallbackValue)");
        builder.AppendLine(indent + "{");
        var original = new StringBuilder();
        AppendCursorField(original, field, "fallbackValue", "", 0);
        foreach (var line in original.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            if (line.Length != 0) builder.AppendLine(indent + "    " + line.TrimStart());
        builder.AppendLine(indent + "}");
    }

    /// <summary>Emits one original member operation, merging adjacent fixed fields up to a limit.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="fields">The ordered wire fields.</param>
    /// <param name="index">The first field to emit.</param>
    /// <param name="limit">The last field this operation may merge.</param>
    /// <param name="Storage">The field storage expression.</param>
    /// <returns>The last field emitted.</returns>
    private static int AppendOriginalMembers(StringBuilder builder, List<LuminDataField> fields, int index, int limit, Func<LuminDataField, string> Storage)
    {
        {
            var field = fields[index];
            if (!field.IsCompress && IsMergeableField(field))
            {
                int last = index;
                while (last + 1 <= limit && !field.IsVolatile &&
                    !fields[last + 1].IsVolatile && !fields[last + 1].IsCompress && IsMergeableField(fields[last + 1])) last++;
                if (field.IsVolatile)
                {
                    builder.AppendLine("            var volatileValue" + index + " = " + Storage(field) + ";");
                    builder.AppendLine("            global::LuminPack.Core.LuminPackWriter.Write(ref writer, volatileValue" + index + ");");
                }
                else if (!TryGenerateCursorFixedGroup(builder, fields, index, last, Storage, "            ", write: true))
                    foreach (var member in fields.Skip(index).Take(last - index + 1))
                        builder.AppendLine("            global::LuminPack.Core.LuminPackWriter.Write(ref writer, " + Storage(member) + ");");
                return last;
            }
            AppendCursorField(builder, field, Storage(field), "            ", 0);
            return index;
        }
    }

    /// <summary>Classifies a member whose complete record has an upper bound computable from one read of its storage.</summary>
    /// <param name="field">The wire field.</param>
    /// <returns>0 when unbounded, 1 for fixed raw storage, 2 for a string, 3 for a raw array and 4 for a raw list.</returns>
    private static int ReservedKind(LuminDataField field)
    {
        if (field.IsCompress || field.IsVolatile || field.TypeSymbol == null ||
            PortableStructPlan.TryCreate(field.TypeSymbol) != null || PortableStructPlan.RequiresClosedDispatch(field.TypeSymbol)) return 0;
        if (IsMergeableField(field)) return 1;
        if (field.Type == LuminFiledType.String && field.TypeSymbol.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String) return 2;
        if (field.TypeSymbol is Microsoft.CodeAnalysis.IArrayTypeSymbol { Rank: 1 } array && IsReservedElement(array.ElementType)) return 3;
        if (field.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } list &&
            list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" && IsReservedElement(list.TypeArguments[0])) return 4;
        if (field.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 2 } dictionary &&
            dictionary.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>")
        {
            if (IsReservedElement(dictionary.TypeArguments[0]) && IsReservedElement(dictionary.TypeArguments[1])) return 5;
            // Raw and string members bound each entry from one read, so the dictionary is an open record checked entry by entry.
            if (IsEntryMember(dictionary.TypeArguments[0]) && IsEntryMember(dictionary.TypeArguments[1])) return 6;
        }
        return 0;
    }

    /// <summary>Accepts a dictionary key or value whose record has a bound computable from one read.</summary>
    /// <param name="member">The key or value type.</param>
    /// <returns>Whether it is a string or raw element storage.</returns>
    private static bool IsEntryMember(Microsoft.CodeAnalysis.ITypeSymbol member)
        => member.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String || IsReservedElement(member);

    /// <summary>Emits a member-specific static kernel that writes a dense dictionary of raw and string entries into given room.</summary>
    /// <param name="builder">The operation builder.</param>
    /// <param name="field">A bounded-entry dictionary member.</param>
    /// <param name="name">The local function name.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <remarks>The kernel returns the written width, or -1 before any store that would leave the room, for non-dense storage and
    /// on 32-bit targets; the caller then takes the member's original operation, which rewrites every byte the kernel stored.
    /// Each string is read once for its bound and its store, and the dictionary version, count and free count are checked
    /// after the entries as the dense path checks them. Only the UTF-16 length profile calls it.</remarks>
    private static void AppendEntryKernel(StringBuilder builder, LuminDataField field, string name, string indent)
    {
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        var dictionary = (Microsoft.CodeAnalysis.INamedTypeSymbol)field.TypeSymbol;
        string type = dictionary.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        bool textKey = dictionary.TypeArguments[0].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        bool textValue = dictionary.TypeArguments[1].SpecialType == Microsoft.CodeAnalysis.SpecialType.System_String;
        string keyType = dictionary.TypeArguments[0].ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        string valueType = dictionary.TypeArguments[1].ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
        string Bound(bool text, string member, string local) => text
            ? "(" + local + " is null ? 0 : (long)" + local + ".Length * sizeof(char)) + sizeof(int)"
            : unsafeType + ".SizeOf<" + member + ">()";
        void Store(bool text, string member, string local, string source)
        {
            builder.AppendLine(indent + "        " + (text
                ? "at += global::LuminPack.Core.LuminPackWriter.WriteReservedUtf16Record(ref " + unsafeType + ".Add(ref destination, (nint)(uint)at), " + local + ");"
                : "global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref " + unsafeType + ".Add(ref destination, (nint)(uint)at), ref " + unsafeType + ".As<" + member + ", byte>(ref " + source + "), (uint)" + unsafeType + ".SizeOf<" + member + ">()); at += " + unsafeType + ".SizeOf<" + member + ">();"));
        }
        builder.AppendLine(indent + "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        builder.AppendLine(indent + "static int " + name + "(ref byte destination, int room, " + type + " dictionary)");
        builder.AppendLine(indent + "{");
        builder.AppendLine(indent + "    if (room < sizeof(int)) return -1;");
        builder.AppendLine(indent + "    if (dictionary is null) { global::LuminPack.Core.LuminPackWriter.Write<int>(ref destination, -1); return sizeof(int); }");
        builder.AppendLine(indent + "    if (" + unsafeType + ".SizeOf<global::System.IntPtr>() != 8) return -1;");
        builder.AppendLine(indent + "    var view = LuminPackMarshal.GetDictionaryView(dictionary);");
        builder.AppendLine(indent + "    int count = view._count;");
        builder.AppendLine(indent + "    var entries = view._entries;");
        builder.AppendLine(indent + "    if (view._freeCount != 0 || (entries is null ? count != 0 : (uint)count > (uint)entries.Length)) return -1;");
        builder.AppendLine(indent + "    int version = view._version;");
        builder.AppendLine(indent + "    global::LuminPack.Core.LuminPackWriter.Write<int>(ref destination, count);");
        builder.AppendLine(indent + "    int at = sizeof(int);");
        builder.AppendLine(indent + "    for (int i = 0; i < count; i++)");
        builder.AppendLine(indent + "    {");
        builder.AppendLine(indent + "        ref var entry = ref " + unsafeType + ".Add(ref LuminPackMarshal.GetNotNullArrayReference(entries!), (nint)(uint)i);");
        if (textKey) builder.AppendLine(indent + "        string key = entry.Key;");
        if (textValue) builder.AppendLine(indent + "        string item = entry.Value;");
        builder.AppendLine(indent + "        if (at + " + Bound(textKey, keyType, "key") + " + " + Bound(textValue, valueType, "item") + " > room) return -1;");
        Store(textKey, keyType, "key", "entry.Key");
        Store(textValue, valueType, "item", "entry.Value");
        builder.AppendLine(indent + "    }");
        builder.AppendLine(indent + "    if (view._version != version || view._count != count || view._freeCount != 0)");
        builder.AppendLine(indent + "        global::LuminPack.Code.LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();");
        builder.AppendLine(indent + "    return at;");
        builder.AppendLine(indent + "}");
    }

    /// <summary>Spells a dictionary member's key and value types.</summary>
    /// <param name="field">A raw-pair dictionary member.</param>
    /// <returns>The fully qualified key and value type arguments.</returns>
    private static string PairTypes(LuminDataField field)
    {
        var dictionary = (Microsoft.CodeAnalysis.INamedTypeSymbol)field.TypeSymbol;
        return dictionary.TypeArguments[0].ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat) + ", " +
               dictionary.TypeArguments[1].ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>Accepts only elements the raw collection path copies as their native storage.</summary>
    /// <param name="element">The element type.</param>
    /// <returns>Whether the element is closed, raw, unmanaged and not Nullable.</returns>
    private static bool IsReservedElement(Microsoft.CodeAnalysis.ITypeSymbol element)
        => element is not Microsoft.CodeAnalysis.ITypeParameterSymbol &&
           element.OriginalDefinition.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_Nullable_T &&
           PortableStructPlan.TryCreate(element) == null && !PortableStructPlan.RequiresClosedDispatch(element) &&
           EmitterTypeTraits.TryGetIsUnmanaged(element, out bool unmanaged) && unmanaged && EmitterTypeTraits.FormatterIsRawStorage(element);

    /// <summary>Retains ordinary inline member contracts, including unmarked private nested storage.</summary>
    /// <param name="builder">The current operation body.</param>
    /// <param name="field">The selected inline field contract.</param>
    /// <param name="expression">The field storage or existing private-storage accessor.</param>
    /// <param name="indent">The current statement indentation.</param>
    /// <param name="depth">The finite analyzed inline depth.</param>
    private static void AppendCursorField(StringBuilder builder, LuminDataField field, string expression, string indent, int depth)
    {
        using var scope = new CompiledOptionFieldScope(builder, field.Options,
            field.TypeSymbol == null ? null : FormatterTypeName.Get(field.TypeSymbol));
        if (PortableStructPlan.TryCreate(field.TypeSymbol) != null || PortableStructPlan.RequiresClosedDispatch(field.TypeSymbol))
        {
            builder.AppendLine(indent + "writer.WriteValue(" + expression + ");");
            return;
        }
        if (IsPureValueTypeStruct(field))
        {
            builder.AppendLine(indent + "global::LuminPack.Core.LuminPackWriter.Write(ref writer, " + expression + ");");
            return;
        }
        if (field.Type == LuminFiledType.String)
        {
            builder.AppendLine(indent + "writer.WriteStringRecord(" + expression + ");");
            return;
        }
        if (field.Type is LuminFiledType.Class or LuminFiledType.Struct && field.ClassFields.Count != 0 && !IsMergeableField(field))
        {
            builder.AppendLine(indent + "{");
            string value = "inlineValue" + depth;
            builder.AppendLine(indent + "    var " + value + " = " + expression + ";");
            if (field.Type == LuminFiledType.Class)
            {
                builder.AppendLine(indent + "    if (" + value + " is null) writer.Write((byte)255);");
                builder.AppendLine(indent + "    else");
            }
            builder.AppendLine(indent + "    {");
            builder.AppendLine(indent + "        writer.Write((byte)" + field.ClassFields.Count + ");");
            var members = field.ClassFields;
            string Storage(LuminDataField member) => member.IsPrivate ? "Get" + field.Name + member.Name + "(" + value + ")" : value + "." + member.Identifier;
            bool Groupable(LuminDataField member) => !member.IsPrivate && !member.isProperty && !member.IsVolatile && !member.IsCompress && IsMergeableField(member);
            for (int index = 0; index < members.Count; index++)
            {
                var member = members[index];
                // Adjacent public fixed members use the same proven group and fallback as root members.
                int last = index;
                if (Groupable(member))
                    while (last + 1 < members.Count && Groupable(members[last + 1])) last++;
                if (TryGenerateCursorFixedGroup(builder, members, index, last, Storage, indent + "        ", write: true))
                    index = last;
                else if (IsMergeableField(member)) builder.AppendLine(indent + "        global::LuminPack.Core.LuminPackWriter.Write(ref writer, " + Storage(member) + ");");
                else AppendCursorField(builder, member, Storage(member), indent + "        ", depth + 1);
            }
            builder.AppendLine(indent + "    }");
            builder.AppendLine(indent + "}");
            return;
        }
        builder.AppendLine(indent + "writer." + (field.IsCompress ? "WriteValueWithCompress" : "WriteValue") + "(" + expression + ");");
    }
}
