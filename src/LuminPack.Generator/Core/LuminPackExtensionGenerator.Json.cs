using System;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{

    /// <summary>Builds the json formatter extension source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void AppendJsonFormatterExtension(StringBuilder sb, string typeName, LuminLocalFieldData field,
        (Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter,
        MetaInfo metaInfo)
    {
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void WriteValue(ref this global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + typeName + " value)"
            : "        public static void WriteValue(ref this global::LuminPack.Core.LuminPackJsonWriter writer, in " + typeName + " value)");
        sb.AppendLine("        {");
        formatter.Serialize(field, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void ReadValue(ref this global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + " value)"
            : "        public static void ReadValue(ref this global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + " value)");
        sb.AppendLine("        {");
        formatter.Deserialize(field, sb);
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    /// <summary>Builds the root json extensions source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="typeName">The C# type name to use in generated source.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateRootJsonExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
    {
        if (data.isUnion)
        {
            if (data.isValueType)
            {
                GenerateUnionJsonExtensionsStruct(sb, data, typeName, metaInfo);
            }
            else
            {
                GenerateUnionJsonExtensions(sb, data, typeName, metaInfo);
            }
            return;
        }

        string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
        string helperName = "Json_" + SanitizeAssemblyName(typeName);
        string helperTypeName = helperName + genericParameters;
        string nullable = data.isValueType ? string.Empty : "?";
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + typeName + nullable + " value)"
            : "        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, in " + typeName + nullable + " value)");
        AppendGenericConstraints(sb, data);
        if (data.generatorType == GeneratorType.CircleReference)
        {
            // The existing circle-reference algorithm needs a writable reference for private
            // field access. Keep the public extension readonly and pass a local copy through.
            sb.AppendLine("        {");
            sb.AppendLine("            var writableValue = value;");
            sb.AppendLine("            " + helperTypeName + ".WriteValue(ref writer, ref writableValue);");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine("            => " + helperTypeName + ".WriteValue(ref writer, in value);");
        }
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8
            ? "        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullable + " value)"
            : "        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullable + " value)");
        AppendGenericConstraints(sb, data);
        sb.AppendLine("            => " + helperTypeName + ".ReadValue(ref reader, ref value);");
        sb.AppendLine();
        // The helper owns this type's shared JSON property-name cache.  Concrete JSON entries
        // reference these members directly (instead of copying them per root), so the helper and
        // its cache members are emitted as assembly-internal rather than private.
        sb.AppendLine("        internal static class " + helperTypeName);
        AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        if (data.generatorType == GeneratorType.CircleReference)
        {
            LuminPackJsonCodeGenerator.GenerateStaticUtf8Fields(sb, data);
            LuminPackJsonCircleReferenceCodeGenerator.GenerateStaticUtf8FieldsForCircleReference(sb);
            var circleMethods = new StringBuilder();
            LuminPackJsonCircleReferenceCodeGenerator.GenerateJsonSerializeWithCircleReference(circleMethods, data, typeName, metaInfo);
            circleMethods.AppendLine();
            LuminPackJsonCircleReferenceCodeGenerator.GenerateJsonDeserializeWithCircleReference(circleMethods, data, typeName, metaInfo);
            string nullableSuffix = data.isValueType ? string.Empty : "?";
            string methods = circleMethods.ToString()
                .Replace("public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + typeName + nullableSuffix + " value)",
                "public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + typeName + nullableSuffix + " value)")
                .Replace("public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + typeName + nullableSuffix + " value)",
                "public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + typeName + nullableSuffix + " value)")
                .Replace("public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullableSuffix + " value)",
                "public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullableSuffix + " value)")
                .Replace("public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullableSuffix + " value)",
                "public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullableSuffix + " value)");
            sb.Append(methods);
        }
        else
        {
            LuminPackJsonCodeGenerator.GenerateStaticUtf8Fields(sb, data);
            LuminPackJsonCodeGenerator.GenerateJsonSerialize(sb, data, typeName, metaInfo);
            sb.AppendLine();
            LuminPackJsonCodeGenerator.GenerateJsonDeserialize(sb, data, typeName, metaInfo);
        }
        sb.AppendLine("        }");
        sb.AppendLine();
    }

}
