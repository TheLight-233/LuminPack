using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{
    /// <summary>Adds an isolated existing-instance branch without expanding it into the fresh allocation path.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="data">The canonical object metadata.</param>
    /// <param name="typeName">The concrete runtime type.</param>
    /// <param name="polymorphism">Whether the Union reader already consumed the object framing.</param>
    private static void AppendExistingObjectRead(StringBuilder sb, LuminDataInfo data, string typeName, bool polymorphism)
    {
        // Approved 2026-10-19: read ordinary root members straight into their existing storage instead of
        // staging locals and committing after the parse. The NoInlining attribute on ReadExisting is
        // load-bearing (removing it regresses reuse 0.3-13.4 %) and must stay.
        string Storage(LuminDataField field) => CanDeserializeDirectlyIntoConstructedObject(field) ? "value!." + field.Identifier : "reuseLocal." + field.Identifier;
        string condition = data.isValueType ? string.Join(" || ", ReusableMemberConditions(data.fields, field =>
            CanDeserializeDirectlyIntoConstructedObject(field) ? "value." + field.Identifier :
                "LuminPackMarshal.As<" + typeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value)." + field.Identifier)) : "value is not null";
        sb.AppendLine("            if (" + condition + ")");
        sb.AppendLine("            {");
        // A reference root never changes identity here. Avoid exposing fresh caller slots by address.
        sb.AppendLine("                ReadExisting(ref reader, " + (data.isValueType ? "ref " : "") + "value);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static void ReadExisting(ref global::LuminPack.Core.LuminPackReader reader, " + (data.isValueType ? "ref " : "") + typeName + (data.isValueType ? "" : "?") + " value)");
        sb.AppendLine("            {");
        sb.AppendLine("            ref int reuseOffset = ref reader.GetCurrentSpanOffset();");
        if (!polymorphism)
        {
            sb.AppendLine("            reuseOffset++;");
        }
        if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
            sb.AppendLine("            ref var reuseLocal = ref LuminPackMarshal.As<" + typeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
        for (int i = 0; i < data.fields.Count; i++)
        {
            var field = data.fields[i];
            int end = IsMergeableField(field) ? System.Math.Min(FindNextMergeableField(data, i), i + 14) : i;
            if (end > i)
            {
                if (!TryGenerateFixedUnmanagedRead(sb, data.fields, i, end, Storage, "reuseOffset", "            ", copyToStorage: false))
                {
                    sb.AppendLine("            reuseOffset += reader.ReadUnmanaged(ref reuseOffset, " +
                        string.Join(", ", data.fields.Skip(i).Take(end - i + 1).Select(member => "out " + Storage(member))) + ");");
                }
                i = end;
                continue;
            }
            GenerateReusableFieldRead(sb, field, Storage(field), "reuseOffset", 3, 0, "_", "");
        }
        foreach (var callback in data.callBackMethods.Where(static callback => callback.Item2 == SerializeCallBackType.OnDeserialized))
            sb.AppendLine(callback.Item3 ? "            " + typeName + "." + callback.Item1 + "();" : "            value?." + callback.Item1 + "();");
        sb.AppendLine("            }");
    }

    /// <summary>Reads one original inline field using its existing storage as the recursive seed.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="field">The canonical field contract.</param>
    /// <param name="target">The mutable local or storage expression.</param>
    /// <param name="offset">The original absolute input cursor.</param>
    /// <param name="indent">The indentation level.</param>
    /// <param name="depth">The inline nesting depth.</param>
    /// <param name="suffix">The unique collection name prefix.</param>
    /// <param name="parent">The original nested accessor name prefix.</param>
    private static void GenerateReusableFieldRead(StringBuilder sb, LuminDataField field, string target, string offset, int indent, int depth, string suffix, string parent)
    {
        using var scope = new CompiledOptionFieldScope(sb, field.Options);
        string spaces = new string(' ', indent * 4);
        if (field.Type is LuminFiledType.List or LuminFiledType.Array)
        {
            GenerateReusableCollectionRead(sb, field, target, offset, indent, depth, suffix, parent);
            return;
        }
        bool nullableObject = field.Type == LuminFiledType.Class;
        if (nullableObject)
        {
            sb.AppendLine(spaces + "if (reader.PeekIsNullObject(ref " + offset + ")) { " + target + " = default!; " + offset + "++; }");
            sb.AppendLine(spaces + "else");
            sb.AppendLine(spaces + "{");
        }
        GenerateDeserializeCode(sb, field, target, "span", offset, indent, depth, multList: suffix,
            parentName: parent, reuseExistingValue: true);
        if (field.Type is not (LuminFiledType.String or LuminFiledType.Class or LuminFiledType.Struct or LuminFiledType.Other))
            sb.AppendLine(spaces + offset + " += " + GetFieldLength(field, depth, suffix, "reader") + ";");
        if (nullableObject) sb.AppendLine(spaces + "}");
    }

    /// <summary>Reads an inline collection while retaining its storage and existing element roots.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="field">The original collection contract.</param>
    /// <param name="target">The reusable collection local.</param>
    /// <param name="offset">The shared absolute input cursor.</param>
    /// <param name="indent">The indentation level.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="suffix">The unique collection name prefix.</param>
    /// <param name="parent">The nested accessor name prefix.</param>
    private static void GenerateReusableCollectionRead(StringBuilder sb, LuminDataField field, string target, string offset, int indent, int depth, string suffix, string parent)
    {
        string spaces = new string(' ', indent * 4);
        string name = parent + field.Name + "Reuse" + suffix + depth;
        string elementType = GetFullGenericTypeName(field.GenericType, field.ClassName, field.ClassGenericType);
        var element = new LuminDataField
        {
            Name = field.Name, Type = ConvertGenericsToFieldType(field.GenericType.FirstOrDefault()),
            GenericType = field.GenericType.Skip(1).ToList(), ClassName = field.ClassName,
            ClassFields = field.ClassFields, ClassGenericType = field.ClassGenericType,
            SelectedConstructor = field.SelectedConstructor, RentPoolMethod = field.RentPoolMethod,
            FullTypeName = field.ClassName.TrimEnd('?'), IsCompress = field.IsCompress,
            TypeSymbol = field.TypeSymbol is IArrayTypeSymbol array ? array.ElementType :
                (field.TypeSymbol as INamedTypeSymbol)?.TypeArguments.FirstOrDefault()
        };
        bool raw = !IsReferenceGenericType(field.GenericType.FirstOrDefault()) || IsPureValueTypeStruct(element);
        if (raw)
        {
            if (field.Type == LuminFiledType.Array)
            {
                sb.AppendLine(spaces + "reader." + (field.IsCompress ? "DangerousReadUnmanagedArrayWithCompress" : "DangerousReadUnmanagedArray") + "(ref " + offset + ", ref " + target + "!, out var " + name + "Bytes);");
                sb.AppendLine(spaces + offset + " += " + name + "Bytes;");
                return;
            }
        }
        sb.AppendLine(spaces + "if (!reader.TryReadCollectionHead(ref " + offset + ", out int " + name + "Count))");
        sb.AppendLine(spaces + "{ " + target + " = default!; " + offset + " += 4; }");
        sb.AppendLine(spaces + "else");
        sb.AppendLine(spaces + "{");
        if (field.Type == LuminFiledType.List)
        {
            sb.AppendLine(spaces + target + " ??= new global::System.Collections.Generic.List<" + elementType + ">(" + name + "Count);");
            sb.AppendLine(spaces + "var " + name + "Span = LuminPackMarshal.GetListSpan(" + target + ", " + name + "Count);");
        }
        else
        {
            sb.AppendLine(spaces + "if (" + target + " is null || " + target + ".Length != " + name + "Count || " + target + ".GetType() != typeof(" + elementType + "[]))");
            sb.AppendLine(spaces + "    " + target + " = " + name + "Count == 0 ? global::System.Array.Empty<" + elementType + ">() : LuminPackMarshal.AllocateUninitializedArray<" + elementType + ">(" + name + "Count);");
            sb.AppendLine(spaces + "var " + name + "Span = " + target + ".AsSpan();");
        }
        if (raw)
        {
            sb.AppendLine(spaces + "reader." + (field.IsCompress ? "DangerousReadUnmanagedSpanWithCompress" : "DangerousReadUnmanagedSpan") + "(ref " + offset + ", ref " + name + "Span, out var " + name + "Bytes);");
            sb.AppendLine(spaces + offset + " += " + name + "Bytes;");
        }
        else
        {
            sb.AppendLine(spaces + offset + " += 4;");
            sb.AppendLine(spaces + "for (int " + name + "Index = 0; " + name + "Index < " + name + "Count; " + name + "Index++)");
            sb.AppendLine(spaces + "{");
            GenerateReusableFieldRead(sb, element, name + "Span[" + name + "Index]", offset, indent + 1, depth + 1, suffix + "_", parent + field.Name);
            sb.AppendLine(spaces + "}");
        }
        sb.AppendLine(spaces + "}");
    }

    /// <summary>Gets the original public storage or Local-plus-As accessor for one inline member.</summary>
    /// <param name="owner">The inline owner contract.</param>
    /// <param name="member">The selected member.</param>
    /// <param name="target">The owner instance expression.</param>
    /// <returns>The same mutable storage used by ordinary inline reads.</returns>
    private static string NestedStorage(LuminDataField owner, LuminDataField member, string target)
        => member.IsPrivate ? "Get" + owner.Name + member.Name + "(" + target + ")" : target + "." + member.Identifier;

    /// <summary>Checks whether a managed value root contains any statically known reusable reference storage.</summary>
    /// <param name="fields">The canonical inline field contracts.</param>
    /// <returns>Whether a reference can seed the existing-instance branch.</returns>
    private static bool HasReusableMembers(IEnumerable<LuminDataField> fields)
        => fields.Any(field => field.FieldType == LuminDataType.Reference || field.TypeSymbol?.IsReferenceType == true ||
            field.TypeSymbol is ITypeParameterSymbol { IsValueType: false } ||
            field.Type == LuminFiledType.Struct && HasReusableMembers(field.ClassFields));

    /// <summary>Finds reference storage inside managed struct layers without boxing or invoking user equality.</summary>
    /// <param name="fields">The canonical inline members.</param>
    /// <param name="storage">The original storage accessor for each member.</param>
    /// <returns>Null tests on the actual reference slots available for reuse.</returns>
    private static IEnumerable<string> ReusableMemberConditions(IEnumerable<LuminDataField> fields, System.Func<LuminDataField, string> storage)
    {
        foreach (var field in fields)
        {
            string target = storage(field);
            if (field.TypeSymbol is ITypeParameterSymbol { IsValueType: false })
                yield return "(!typeof(" + field.TypeSymbol.ToDisplayString() + ").IsValueType && (object?)" + target + " is not null)";
            else if (field.FieldType == LuminDataType.Reference || field.TypeSymbol?.IsReferenceType == true)
                yield return target + " is not null";
            else if (field.Type == LuminFiledType.Struct)
                foreach (string condition in ReusableMemberConditions(field.ClassFields, member => NestedStorage(field, member, target)))
                    yield return condition;
        }
    }

    /// <summary>Constructs only missing inline instances, then commits every selected member to its original storage.</summary>
    /// <param name="sb">The generated operation builder.</param>
    /// <param name="field">The inline class or managed struct contract.</param>
    /// <param name="target">The reusable owner local.</param>
    /// <param name="parent">The temporary name prefix.</param>
    /// <param name="suffix">The temporary depth suffix.</param>
    /// <param name="spaces">The statement indentation.</param>
    private static void AppendReusableNestedCommit(StringBuilder sb, LuminDataField field, string target, string parent, string suffix, string spaces)
    {
        if (field.Type == LuminFiledType.Class) sb.AppendLine(spaces + "if (" + target + " is null)");
        sb.AppendLine(spaces + "{");
        string arguments = field.SelectedConstructor == null ? "" : string.Join(", ", field.SelectedConstructor.Parameters.Select(parameter =>
            field.ClassFields.Any(member => member.Name == parameter.MatchingFieldName) ? parent + field.Name + parameter.MatchingFieldName + "Temp" + suffix + "!" : "default!"));
        string allocation = field.RentPoolMethod == null ? "new " + field.FullTypeName + "(" + arguments + ")" : field.FullTypeName + "." + field.RentPoolMethod.Name + "()";
        if (field.RentPoolMethod?.ReturnsByRef == true)
        {
            string rented = parent + field.Name + "Rented" + suffix;
            sb.AppendLine(spaces + "ref var " + rented + " = ref " + allocation + ";");
            sb.AppendLine(spaces + target + " = " + rented + ";");
        }
        else sb.AppendLine(spaces + target + " = " + allocation + ";");
        sb.AppendLine(spaces + "}");
        foreach (var member in field.ClassFields)
            sb.AppendLine(spaces + NestedStorage(field, member, target) + " = " + parent + field.Name + member.Name + "Temp" + suffix + "!;");
    }
}
