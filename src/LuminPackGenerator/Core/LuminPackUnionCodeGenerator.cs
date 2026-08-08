using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>
/// Emits the union root operations. Member dispatch itself is generated as partial virtual
/// slots by <see cref="LuminPackUnionDispatchCodeGenerator"/>; no parser or runtime registry
/// participates in the normal path.
/// </summary>
public static class LuminPackUnionCodeGenerator
{
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                ref int offset = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
            sb.AppendLine("                offset += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionSerialize_{suffix}(ref writer);");
        sb.AppendLine();

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;
        ushort maxTag = data.UnionMembers.Count == 0 ? (ushort)0 : data.UnionMembers.Max(static member => member.Id);

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        sb.AppendLine(maxTag < 250 && !data.IsWideTag
            ? "            if (!reader.TryPeekUnionHeader(out var tag))"
            : "            if (!reader.TryPeekWideUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        foreach (LuminUnionMemberInfo member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            sb.AppendLine($"                case {member.Id}:");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {memberType} member = default!;");
            sb.AppendLine("                    reader.ReadPolymorphismValue(ref member);");
            sb.AppendLine(member.Type.IsValueType
                ? $"                    value = ({classGlobalName})(object)member;"
                : $"                    value = LuminPackMarshal.As<{memberType}, {classGlobalName}>(ref member!);");
            sb.AppendLine("                    break;");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                default:");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({classGlobalName}));");
        sb.AppendLine("                    break;");
        sb.AppendLine("            }");

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    public static void GenerateCalculateOffsetCode(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionCalculateOffset_{suffix}(ref evaluator);");
        sb.AppendLine();
    }

    public static void GenerateSerializeJsonCode(
        LuminDataInfo data,
        StringBuilder sb,
        string classGlobalName)
    {
        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        string suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(data.TypeSymbol.OriginalDefinition);
        sb.AppendLine($"            value.__LuminPackUnionSerializeJson_{suffix}(ref writer);");
        sb.AppendLine();

        foreach (var item in data.callBackMethods.Where(
                     static callback => callback.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    private static string GetMemberType(LuminDataInfo data, LuminUnionMemberInfo member)
    {
        if (!member.Type.IsUnboundGenericType)
        {
            return FormatterTypeName.Get(member.Type);
        }

        string prefix = member.Type.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : "global::" + member.Type.ContainingNamespace.ToDisplayString() + ".";
        return prefix + member.Type.Name + "<" + string.Join(", ", data.GenericParameters) + ">";
    }
}
