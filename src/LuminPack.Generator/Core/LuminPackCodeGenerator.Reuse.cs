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
        if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
            sb.AppendLine("            ref var reuseLocal = ref LuminPackMarshal.As<" + typeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
        foreach (var field in data.fields)
            AppendCursorFieldRead(sb, field, Storage(field), "            ", 0, fresh: false, allowBulkCopy: !polymorphism);
        foreach (var callback in data.callBackMethods.Where(static callback => callback.Item2 == SerializeCallBackType.OnDeserialized))
            sb.AppendLine(callback.Item3 ? "            " + typeName + "." + callback.Item1 + "();" : "            value?." + callback.Item1 + "();");
        sb.AppendLine("            }");
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
