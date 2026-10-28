using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Evaluates complete BCL wrapper records using the same framing as their binary emitters.</summary>
internal static class SpecialValueEvaluatorEmitter
{
    /// <summary>Appends the exact wrapper header and selected child contribution.</summary>
    /// <param name="data">The closed BCL value metadata.</param>
    /// <param name="sb">The generated operation body.</param>
    internal static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        if (TupleEmitter.IsTuple(data.TypeName) && EmitterTypeTraits.CanUseUnmanagedSerialization(data.TypeSymbol))
        {
            UnmanagedEmitter.GenerateCalculateOffsetCode(data, sb);
            return;
        }
        bool valueTuple = data.TypeName.StartsWith("global::System.ValueTuple<", System.StringComparison.Ordinal);
        if (!valueTuple)
        {
            int header = data.TypeName == "global::System.Text.StringBuilder" ? 4 : 1;
            sb.AppendLine("            evaluator += " + header + ";");
            sb.AppendLine("            if (value is null) return;");
        }
        if (TupleEmitter.IsTuple(data.TypeName))
        {
            int count = CodeEmitterRegistry.GetGenericArguments(data.TypeName).Length;
            for (int index = 1; index <= count; index++)
            {
                sb.AppendLine("            var item" + index + " = value." + (index == 8 ? "Rest" : "Item" + index) + ";");
                sb.AppendLine("            evaluator.CalculateOffset(ref item" + index + ");");
            }
            return;
        }
        if (data.TypeName.StartsWith("global::System.Lazy<", System.StringComparison.Ordinal))
        {
            sb.AppendLine("            var item = value.Value;");
            sb.AppendLine("            evaluator.CalculateOffset(ref item);");
            return;
        }
        switch (data.TypeName)
        {
            case "global::System.Version":
                sb.AppendLine("            evaluator += 16;");
                return;
            case "global::System.Text.StringBuilder":
                sb.AppendLine("            evaluator += checked(value.Length * sizeof(char));");
                return;
            case "global::System.Uri":
                sb.AppendLine("            var text = value.OriginalString;");
                break;
            case "global::System.Globalization.CultureInfo":
                sb.AppendLine("            var text = value.Name;");
                break;
            case "global::System.TimeZoneInfo":
                sb.AppendLine("            var text = global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value);");
                break;
            case "global::System.Type":
                sb.AppendLine("            var full = value.AssemblyQualifiedName;");
                sb.AppendLine("            if (string.IsNullOrEmpty(full)) global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Type values without an assembly-qualified name are not supported.\");");
                sb.AppendLine("            var text = global::System.Text.RegularExpressions.Regex.Replace(full, @\", Version=\\d+.\\d+.\\d+.\\d+, Culture=[\\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})\", \"\");");
                break;
            default:
                return;
        }
        sb.AppendLine("            evaluator += evaluator.GetStringLength(ref text);");
    }
}
