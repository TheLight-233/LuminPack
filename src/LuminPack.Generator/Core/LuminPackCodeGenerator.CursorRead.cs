using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{
    /// <summary>Reads one raw fixed field at the cursor and advances once, without relying on helper inlining.</summary>
    /// <param name="builder">The operation receiving the statements.</param>
    /// <param name="field">The raw unmanaged field contract.</param>
    /// <param name="target">The writable field storage.</param>
    /// <param name="indent">The statement indentation.</param>
    private static void AppendFixedFieldRead(StringBuilder builder, LuminDataField field, string target, string indent)
    {
        // The checked Advance validates the complete field range before the cursor moves; the load then reads the
        // captured position, so one range check covers both.
        builder.AppendLine(indent + "{");
        builder.AppendLine(indent + "    ref byte fixedCursor = ref reader.GetCurrentSpanReference();");
        builder.AppendLine(indent + "    reader.Advance(" + GetUnmanagedFieldSize(field) + ");");
        builder.AppendLine(indent + "    " + FixedRead("fixedCursor", target, GetGeneratedTypeName(field)));
        builder.AppendLine(indent + "}");
    }

    /// <summary>Reads one complete field while preserving its selected inline or formatter contract.</summary>
    /// <param name="builder">The operation receiving the field statements.</param>
    /// <param name="field">The analyzed field contract.</param>
    /// <param name="target">The writable field storage.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="depth">The finite analyzed inline depth.</param>
    /// <param name="fresh">Whether the destination has no reusable storage.</param>
    /// <param name="allowBulkCopy">Whether nested fixed groups may use the address-proven storage copy; Union payloads keep their cursor-offset form.</param>
    private static void AppendCursorFieldRead(StringBuilder builder, LuminDataField field, string target, string indent, int depth, bool fresh,
        bool allowBulkCopy = true)
    {
        using var scope = new CompiledOptionFieldScope(builder, field.Options,
            field.TypeSymbol == null ? null : FormatterTypeName.Get(field.TypeSymbol));
        if (PortableStructPlan.TryCreate(field.TypeSymbol) != null || PortableStructPlan.RequiresClosedDispatch(field.TypeSymbol))
        {
            builder.AppendLine(indent + "reader.ReadValue(ref " + target + ");");
            return;
        }
        if (IsPureValueTypeStruct(field))
        {
            AppendFixedFieldRead(builder, field, target, indent);
            return;
        }
        if (field.Type == LuminFiledType.String)
        {
            if (fresh || field.IsVolatile)
                builder.AppendLine(indent + target + " = reader.ReadStringRecord((string?)null)!;");
            else
            {
                builder.AppendLine(indent + "{");
                builder.AppendLine(indent + "    var currentString = " + target + ";");
                builder.AppendLine(indent + "    var decodedString = reader.ReadStringRecord(currentString);");
                builder.AppendLine(indent + "    if (!global::System.Object.ReferenceEquals(currentString, decodedString)) " + target + " = decodedString!;");
                builder.AppendLine(indent + "}");
            }
            return;
        }
        if (!field.IsCompress && IsMergeableField(field))
        {
            if (field.IsVolatile)
            {
                builder.AppendLine(indent + "{");
                builder.AppendLine(indent + "    var scalar = reader.Read<" + GetFieldLocalVariableType(field) + ">();");
                builder.AppendLine(indent + "    " + target + " = scalar;");
                builder.AppendLine(indent + "}");
            }
            else AppendFixedFieldRead(builder, field, target, indent);
            return;
        }
        if (field.Type is LuminFiledType.Class or LuminFiledType.Struct && field.ClassFields.Count != 0)
        {
            builder.AppendLine(indent + "{");
            if (field.Type == LuminFiledType.Class)
            {
                builder.AppendLine(indent + "    if (reader.Read<byte>() == 255) " + target + " = default!;");
                builder.AppendLine(indent + "    else");
            }
            else builder.AppendLine(indent + "    reader.Advance(1);");
            builder.AppendLine(indent + "    {");
            string spaces = indent + "        ";
            string name = "inlineRead" + depth;
            if (CanDeserializeDirectlyIntoNestedObject(field))
            {
                // A pooled member rents a missing instance and then reads into it like an existing one, even on a fresh read.
                bool rents = field.RentPoolMethod != null && !string.IsNullOrWhiteSpace(field.FullTypeName);
                string allocation = rents
                    ? (fresh ? "" : target + " ?? ") + field.FullTypeName + "." + field.RentPoolMethod!.Name + "()"
                    : string.IsNullOrWhiteSpace(field.FullTypeName)
                        ? target + (fresh ? " = new()" : " ??= new()")
                        : (fresh ? "" : target + " ?? ") + "new " + field.FullTypeName + "()";
                if (rents) fresh = false;
                builder.AppendLine(spaces + "var " + name + " = " + allocation + ";");
                var members = field.ClassFields;
                string Storage(LuminDataField member) => name + "." + member.Identifier;
                for (int index = 0; index < members.Count; index++)
                {
                    // Adjacent fixed members of the constructed nested object use the same proven group as root members.
                    int last = index;
                    if (!members[index].IsCompress && IsMergeableField(members[index]))
                        while (last + 1 < members.Count && !members[last + 1].IsCompress && IsMergeableField(members[last + 1])) last++;
                    if (TryGenerateCursorFixedGroup(builder, members, index, last, Storage, spaces, write: false, allowBulkCopy))
                        index = last;
                    else AppendCursorFieldRead(builder, members[index], Storage(members[index]), spaces, depth + 1, fresh, allowBulkCopy);
                }
                builder.AppendLine(spaces + target + " = " + name + ";");
            }
            else
            {
                string suffix = "_" + depth;
                foreach (var member in field.ClassFields)
                {
                    string initial = fresh ? "default!" : (field.Type == LuminFiledType.Class ? target + " is null ? default! : " : "") + NestedStorage(field, member, target);
                    builder.AppendLine(spaces + GetFieldLocalVariableType(member) + (member.FieldType == LuminDataType.Reference ? "?" : "") + " " + name + field.Name + member.Name + "Temp" + suffix + " = " + initial + ";");
                }
                foreach (var member in field.ClassFields)
                    AppendCursorFieldRead(builder, member, name + field.Name + member.Name + "Temp" + suffix, spaces, depth + 1, fresh, allowBulkCopy);
                AppendReusableNestedCommit(builder, field, target, name, suffix, spaces);
            }
            builder.AppendLine(indent + "    }");
            builder.AppendLine(indent + "}");
            return;
        }
        if (!fresh)
            EmitterTypeTraits.AppendReusableElementRead(builder, field.TypeSymbol, target, indent, field.IsCompress);
        else
            builder.AppendLine(indent + "reader." + (field.IsCompress ? "ReadValueWithCompress" : field.TypeSymbol != null && !ContainsTypeParameter(field.TypeSymbol) && (IsExactFreshCollectionType(field) || IsExactManagedArrayType(field)) ? "ReadFreshValue" : "ReadValue") + "(ref " + target + "!);");
    }
}
