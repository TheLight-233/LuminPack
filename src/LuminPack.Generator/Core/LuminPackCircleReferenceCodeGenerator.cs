using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>Emits reference-tracked records: a constant slot table, a reference id and the members.</summary>
/// <remarks>A record is <c>[slot count][one width byte per slot][VarInt id][members]</c>. A width byte is a raw member's width,
/// zero for an empty slot, or 255 for a member preceded by its four-byte length. <c>250</c> refers back to an object already
/// read, and <c>255</c> is null.</remarks>
public static class LuminPackCircleReferenceCodeGenerator
{
    const int MaxParametersPerCall = 15;
    const string Memory = "global::System.Runtime.CompilerServices.Unsafe";

    /// <summary>Describes the record layout shared by the writer and reader templates.</summary>
    private sealed class Layout
    {
        public string Type = "";
        public List<LuminDataField> Fields = new();
        public int?[] Widths = Array.Empty<int?>();
        public int SlotCount;
        // The slot count and width bytes, or null when a width is known only at run time.
        public byte[] Table;
    }

    /// <summary>Collects the members in slot order with their widths.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>The record layout.</returns>
    private static Layout Describe(LuminDataInfo data)
    {
        string type = data.classFullName;
        if (!type.Contains(".") && data.classNameSpace != "<global namespace>")
            type = "global::" + data.classNameSpace + "." + data.classFullName;
        var widths = VersionMetadataEncoder.Widths(data);
        var pairs = data.fields.Select((field, index) => (field, width: widths[index])).OrderBy(pair => pair.field.Order).ToList();
        var layout = new Layout
        {
            Type = type,
            Fields = pairs.Select(pair => pair.field).ToList(),
            Widths = pairs.Select(pair => pair.width).ToArray(),
            SlotCount = data.fields.Count == 0 ? 0 : (int)data.fields.Max(field => field.Order) + 1
        };
        if (layout.SlotCount < 250 && pairs.All(pair => pair.width is int))
        {
            var table = new byte[layout.SlotCount + 1];
            table[0] = (byte)layout.SlotCount;
            foreach (var (field, width) in pairs) table[field.Order + 1] = (byte)width!.Value;
            layout.Table = table;
        }
        return layout;
    }

    /// <summary>Selects direct volatile access or the established Local storage view.</summary>
    /// <param name="field">The member being accessed.</param>
    /// <returns>The generated storage expression.</returns>
    private static string Access(LuminDataField field) => field.IsVolatile
        ? field.IsPrivate ? "global::System.Threading.Volatile.Read(ref local." + field.Identifier + ")" : "value!." + field.Identifier
        : "local." + field.Identifier;

    /// <summary>Emits a raw width byte that the JIT folds to a constant.</summary>
    /// <param name="field">A raw member.</param>
    /// <returns>The width, or 255 for a value preceded by its width.</returns>
    private static string RuntimeWidth(LuminDataField field)
        => "(byte)(" + Memory + ".SizeOf<" + field.TypeName + ">() < 255 ? " + Memory + ".SizeOf<" + field.TypeName + ">() : 255)";

    /// <summary>Emits statements for constant bytes, choosing four-byte words on 32-bit targets.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="bytes">The constant bytes.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="emit">Emits the statements for one word width.</param>
    private static void PerArchitecture(StringBuilder sb, byte[] bytes, string indent, Action<int, string> emit)
    {
        if (bytes.Length < 8)
        {
            emit(4, indent);
            return;
        }
        sb.AppendLine(indent + "if (" + Memory + ".SizeOf<nint>() == 8)");
        sb.AppendLine(indent + "{");
        emit(8, indent + "    ");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "else");
        sb.AppendLine(indent + "{");
        emit(4, indent + "    ");
        sb.AppendLine(indent + "}");
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        var layout = Describe(data);
        string type = layout.Type;

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
            sb.AppendLine(item.Item3 ? $"            {type}.{item.Item1}();" : $"            value?.{item.Item1}();");
        sb.AppendLine();

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{type}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref Unsafe.AsRef(in value));");
        // One probe registers a new object or finds the identifier of one already written.
        sb.AppendLine("            int __reference = writer.OptionState.TrackReference(value);");
        sb.AppendLine("            if (__reference < 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteObjectReferenceId((uint)~__reference);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        var table = layout.Table;
        // The constant table and the reference id join the first reserved group like an ordinary object header.
        if (table == null || !ReservedRecordEmitter.TryGenerate(sb, layout.Fields, layout.Widths, Access, (table.Length + 5).ToString(), indent =>
            {
                PerArchitecture(sb, table, indent, (widest, inner) => ReservedRecordEmitter.AppendStores(sb, table, "__cursor", inner, widest));
                sb.AppendLine(indent + "__cursor = ref " + Memory + ".Add(ref __cursor, " + table.Length + ");");
                sb.AppendLine(indent + "__cursor = ref global::LuminPack.Core.LuminPackWriter.StoreVarInt(ref __cursor, (uint)__reference);");
            }))
            AppendSequentialWrite(layout, sb);

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
            sb.AppendLine(item.Item3 ? $"            {type}.{item.Item1}();" : $"            value?.{item.Item1}();");
        sb.AppendLine();
    }

    /// <summary>Writes the record through the writer's own operations, for members of run-time width or volatile members.</summary>
    /// <param name="layout">The record layout.</param>
    /// <param name="sb">The generated operation body.</param>
    private static void AppendSequentialWrite(Layout layout, StringBuilder sb)
    {
        sb.AppendLine($"            writer.WriteObjectHeader({layout.SlotCount});");
        var slots = new string[layout.SlotCount];
        for (int order = 0; order < layout.SlotCount; order++) slots[order] = "(byte)0";
        for (int index = 0; index < layout.Fields.Count; index++)
            slots[layout.Fields[index].Order] = layout.Widths[index] is int width ? "(byte)" + width : RuntimeWidth(layout.Fields[index]);
        for (int start = 0; start < slots.Length; start += MaxParametersPerCall)
            sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(" + string.Join(", ", slots.Skip(start).Take(MaxParametersPerCall)) + "));");
        sb.AppendLine("            writer.WriteVarInt((uint)__reference);");

        var batch = new List<LuminDataField>();
        void Flush()
        {
            if (batch.Count == 0) return;
            foreach (var field in batch.Where(field => field.IsVolatile))
                sb.AppendLine($"            var {field.Name}Snapshot = {Access(field)};");
            sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(" + string.Join(", ", batch.Select(field => field.IsVolatile ? field.Name + "Snapshot" : Access(field))) + "));");
            batch.Clear();
        }
        for (int index = 0; index < layout.Fields.Count; index++)
        {
            var field = layout.Fields[index];
            if (layout.Widths[index] is int width && width != 255)
            {
                batch.Add(field);
                if (batch.Count == MaxParametersPerCall) Flush();
                continue;
            }
            Flush();
            if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
            {
                // A raw member whose width is known only at run time carries its width when it is 255 bytes or wider.
                sb.AppendLine("            {");
                if (field.IsVolatile) sb.AppendLine($"                var {field.Name}Snapshot = {Access(field)};");
                sb.AppendLine($"                if (Unsafe.SizeOf<{field.TypeName}>() >= 255) writer.WriteVersionLength(Unsafe.SizeOf<{field.TypeName}>());");
                sb.AppendLine($"                writer.Advance(writer.WriteUnmanaged({(field.IsVolatile ? field.Name + "Snapshot" : Access(field))}));");
                sb.AppendLine("            }");
                continue;
            }
            sb.AppendLine("            {");
            sb.AppendLine($"                int {field.Name}Length = writer.ReserveVersionLength();");
            sb.AppendLine($"                writer.{(field.IsCompress ? "WriteValueWithCompress" : "WriteValue")}({Access(field)});");
            sb.AppendLine($"                writer.CompleteVersionLength({field.Name}Length);");
            sb.AppendLine("            }");
        }
        Flush();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb) => GenerateDeserializeCode(data, sb, false);

    /// <summary>Appends binary deserialization statements, with a same-table path that reads into the object like an ordinary record.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="guardNestedReads">Whether inline nested records check their markers for untrusted input.</param>
    internal static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb, bool guardNestedReads)
    {
        var layout = Describe(data);
        string classGlobalName = layout.Type;
        uint memberCount = (uint)layout.SlotCount;

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value?.{item.Item1}();");
        sb.AppendLine();

        AppendSameTableRead(data, layout, sb, guardNestedReads);

        // Declare locals for every field.
        sb.AppendLine("            // Declare locals to hold the field values.");
        foreach (var field in data.localFields)
        {
            var nullable = field.IsValue ? "" : "?";
            sb.AppendLine($"            {field.TypeName}{nullable} {field.Name}Temp = default!;");
        }
        sb.AppendLine();

        sb.AppendLine("            if (!reader.TryReadObjectHead(out var count))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (count is LuminPackCode.ReferenceId)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = ({classGlobalName})reader.OptionState.GetObjectReference(reader.ReadVarIntUInt32());");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");

        // Known slot widths stay in locals; unknown trailing slots are read again from the input after the known payloads.
        sb.AppendLine("            reader.EnsureReadable(count);");
        sb.AppendLine("            ref byte __table = ref reader.PeekInput(count, out _);");
        for (int order = 0; order < memberCount; order++)
            sb.AppendLine($"            int width{order} = {order} < count ? {Memory}.Add(ref __table, {order}) : 0;");
        sb.AppendLine($"            int unknownTable = reader.Position + {memberCount};");
        sb.AppendLine("            reader.Advance(count);");
        sb.AppendLine("            uint id = reader.ReadVarIntUInt32();");
        sb.AppendLine();

        // Collect constructor arguments using default values.
        var constructorParams = new List<string>();
        if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
        {
            foreach (var param in data.SelectedConstructor.Parameters)
            {
                // Use a default value instead of reading the field.
                constructorParams.Add("default!");
            }
        }

        string constructorArgs = string.Join(", ", constructorParams);

        // Create the object with default argument values.
        if (!data.isValueType)
        {
            sb.AppendLine("            bool reuseObject = value is not null && !reader.OptionState.IsObjectReferenceRegistered(value);");
            sb.AppendLine("            if (!reuseObject)");
            sb.AppendLine("            {");
        }
        if (data.RentPoolMethod == null)
        {
            if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
            {
                sb.AppendLine($"            value = new {classGlobalName}({constructorArgs});");
            }
            else
            {
                sb.AppendLine($"            value = new {classGlobalName}();");
            }
        }
        else
        {
            // Obtain the instance from the object pool.
            if (data.RentPoolMethod.ReturnsByRef)
                sb.AppendLine($"            value = ref {classGlobalName}.{data.RentPoolMethod.Name}()!;");
            else
            {
                sb.AppendLine($"            value = {classGlobalName}.{data.RentPoolMethod.Name}();");
            }
        }


        if (!data.isValueType) sb.AppendLine("            }");
        // Register the object reference before reading nested members.
        sb.AppendLine($"            reader.OptionState.AddObjectReference(id, value!);");
        sb.AppendLine();
        if (!data.isValueType)
        {
            sb.AppendLine("            if (reuseObject)");
            sb.AppendLine("            {");
            sb.AppendLine($"            ref var reuseLocal = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
            foreach (var field in data.fields)
                sb.AppendLine($"            {field.Name}Temp = reuseLocal.{field.Identifier};");
            sb.AppendLine("            }");
        }

        // Deserialize into local variables.
        for (var order = 0; order < memberCount; order++)
        {
            var field = data.fields.FirstOrDefault(f => f.Order == order);

            if (field is not null)
            {
                sb.AppendLine($"            if (width{order} != 0)");
                sb.AppendLine("            {");
                if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
                {
                    // A raw slot records its exact width, or 255 with the width in front of a value of 255 bytes or more.
                    sb.AppendLine($"                if (width{order} != Unsafe.SizeOf<{field.TypeName}>()) reader.ValidateReferenceWidth(width{order}, Unsafe.SizeOf<{field.TypeName}>());");
                    sb.AppendLine($"                reader.EnsureReadable(reader.Position, Unsafe.SizeOf<{field.TypeName}>());");
                    sb.AppendLine($"                reader.Advance(reader.ReadUnmanaged(out {field.Name}Temp));");
                }
                else
                {
                    // A variable slot stores its payload length before the payload.
                    sb.AppendLine($"                int {field.Name}End = reader.ReadReferencePayloadLength(width{order}) + reader.GetCurrentSpanIndex();");
                    LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.AppendReusableElementRead(sb, field.TypeSymbol,
                        field.Name + "Temp", "                ", field.IsCompress);
                    sb.AppendLine($"                global::LuminPack.Core.LuminPackReader.ValidateVersionFieldEnd(reader.GetCurrentSpanIndex(), {field.Name}End);");
                }
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                sb.AppendLine($"                {field.Name}Temp = default!;");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            reader.SkipReferenceSlot(width{order});");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"            if (count > {memberCount}) reader.SkipReferenceSlots(unknownTable, count - {memberCount});");
        sb.AppendLine("            // Assign the field value.");

        // Collect public fields assigned through an object initializer.
        var initializerFields = data.fields.Where(f =>
            (data.SelectedConstructor == null ||
             !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
            !f.IsPrivate && !f.isProperty
        ).ToList();

        // Collect private fields that require separate assignments.
        var privateFields = data.fields.Where(f =>
            (data.SelectedConstructor == null ||
             !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
            (f.IsPrivate || f.isProperty)
        ).ToList();

        if (data.RentPoolMethod != null)
        {
            // Populate all fields of pooled objects without relying on constructors.
            sb.AppendLine($"            // Populate every field on the pooled instance.");

            // Assign public and private fields through the Local mirror.
            if (data.isValueType)
            {
                sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
            }
            else
            {
                sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
            }

            // Populate all fields.
            foreach (var field in data.fields)
            {
                sb.AppendLine($"            local.{field.Identifier} = {field.Name}Temp!;");
            }
        }
        else
        {
            // Assign public fields through an object initializer.
            if (initializerFields.Count > 0)
            {
                sb.AppendLine("            // Assign public fields.");
                foreach (var field in initializerFields)
                {
                    sb.AppendLine($"            value.{field.Identifier} = {field.Name}Temp!;");
                }
                sb.AppendLine();
            }

            // Assign private fields through the Local mirror.
            if (privateFields.Count > 0)
            {
                sb.AppendLine("            // Assign private fields.");
                if (data.isValueType)
                {
                    sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                }
                else
                {
                    sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                }

                foreach (var field in privateFields)
                {
                    sb.AppendLine($"            local.{field.Identifier} = {field.Name}Temp!;");
                }
            }
        }
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
    }

    /// <summary>Reads a record whose table equals the receiver's straight into the object, like an ordinary record body.</summary>
    /// <param name="data">The complete declared member contract.</param>
    /// <param name="layout">The record layout.</param>
    /// <param name="sb">The generated operation body.</param>
    /// <param name="guardNestedReads">Whether inline nested records check their markers for untrusted input.</param>
    /// <remarks>The slot count and table are compared as constant words; raw runs use the address-proven bulk copy and a variable
    /// member reads in place between its length and end check. Value types, constructor arguments, by-reference pools, volatile
    /// members and members of run-time width keep the temporary-based path, as does any other table.</remarks>
    private static void AppendSameTableRead(LuminDataInfo data, Layout layout, StringBuilder sb, bool guardNestedReads)
    {
        const string indent = "                ";
        var table = layout.Table;
        bool canConstructDirectly = data.RentPoolMethod == null && (data.SelectedConstructor == null || data.SelectedConstructor.Parameters.Count == 0);
        bool rentsInstance = data.RentPoolMethod is { ReturnsByRef: false };
        if (table == null || data.isValueType || !(canConstructDirectly || rentsInstance) ||
            layout.Fields.Select((field, index) => (field, index)).Any(pair => pair.field.IsVolatile || layout.Widths[pair.index] is not int width ||
                LuminPackCodeGenerator.IsUnmanagedFiledType(pair.field.Type) == (width == 255)))
            return;
        string type = layout.Type;
        sb.AppendLine("            ref byte __head = ref reader.PeekInput(" + table.Length + ", out bool __sameTable);");
        string match = table.Length < 8 ? ReservedRecordEmitter.Difference(table, "__head", 4) :
            "(" + Memory + ".SizeOf<nint>() == 8 ? " + ReservedRecordEmitter.Difference(table, "__head", 8) + " : " + ReservedRecordEmitter.Difference(table, "__head", 4) + ")";
        sb.AppendLine("            if (__sameTable && " + match + " == 0)");
        sb.AppendLine("            {");
        sb.AppendLine(indent + "reader.Advance(" + table.Length + ");");
        sb.AppendLine(indent + "uint __id = reader.ReadVarIntUInt32();");
        string allocation = rentsInstance ? type + "." + data.RentPoolMethod!.Name + "()" : "new " + type + "()";
        // An instance already registered in this read represents another record and is not reused. Either object is
        // registered before its members are read, so cycles resolve to it.
        sb.AppendLine(indent + "if (value is null || !reader.OptionState.ClaimReference(__id, value))");
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    value = " + allocation + ";");
        sb.AppendLine(indent + "    reader.OptionState.AddObjectReference(__id, value);");
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "var instance = value;");
        if (layout.Fields.Any(field => !LuminPackCodeGenerator.CanDeserializeDirectlyIntoConstructedObject(field)))
            sb.AppendLine(indent + "var local = LuminPackMarshal.As<" + type + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref instance);");
        string Storage(LuminDataField member) => (LuminPackCodeGenerator.CanDeserializeDirectlyIntoConstructedObject(member) ? "instance." : "local.") + member.Identifier;
        bool IsRawRun(int index) => layout.Widths[index] is int width && width != 255 && LuminPackCodeGenerator.IsUnmanagedFiledType(layout.Fields[index].Type) &&
            !layout.Fields[index].IsCompress;
        for (int index = 0; index < layout.Fields.Count; index++)
        {
            var field = layout.Fields[index];
            if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
            {
                int last = index;
                while (last + 1 < layout.Fields.Count && IsRawRun(last + 1)) last++;
                if (IsRawRun(index) &&
                    LuminPackCodeGenerator.TryGenerateCursorFixedGroup(sb, layout.Fields, index, last, Storage, indent, write: false, allowBulkCopy: true))
                {
                    index = last;
                    continue;
                }
                LuminPackCodeGenerator.AppendCursorFieldRead(sb, field, Storage(field), indent, 0, fresh: false, allowBulkCopy: true, guardNestedReads);
                continue;
            }
            sb.AppendLine(indent + "{");
            sb.AppendLine(indent + "    int end = reader.ReadVersionPayloadLength();");
            sb.AppendLine(indent + "    end += reader.Position;");
            LuminPackCodeGenerator.AppendCursorFieldRead(sb, field, Storage(field), indent + "    ", 0, fresh: false, allowBulkCopy: true, guardNestedReads);
            sb.AppendLine(indent + "    global::LuminPack.Core.LuminPackReader.ValidateVersionFieldEnd(reader.Position, end);");
            sb.AppendLine(indent + "}");
        }
        foreach (var callback in data.callBackMethods.Where(item => item.Item2 == SerializeCallBackType.OnDeserialized))
            // The caller's slot may alias a member just read, so callbacks run on the completed instance.
            sb.AppendLine(indent + (callback.Item3 ? type + "." + callback.Item1 + "();" : "instance." + callback.Item1 + "();"));
        sb.AppendLine(indent + "return;");
        sb.AppendLine("            }");
    }
}
