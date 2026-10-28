using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides type emitter functionality.</summary>
public static class TypeEmitter
{
    /// <summary>Appends binary writing statements for type values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ");
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            var full = value.AssemblyQualifiedName;");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(full))");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Type values without an assembly-qualified name are not supported.\");");
        sb.AppendLine("                ");
        sb.AppendLine("                ");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            var shortName = global::System.Text.RegularExpressions.Regex.Replace(full, @\", Version=\\d+.\\d+.\\d+.\\d+, Culture=[\\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})\", \"\");");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.WriteObjectHeader(0);");
        sb.AppendLine("            writer.WriteStringRecord(shortName);");
        sb.AppendLine("            ");
        sb.AppendLine("            ");
    }

    /// <summary>Appends binary reading statements for type values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (!reader.TryReadObjectHead())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            var typeName = reader.ReadStringAndAdvance(ref index);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(typeName))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        AppendDynamicTypeGuard(sb);
        sb.AppendLine("            value = global::System.Type.GetType(typeName, throwOnError: true);");
    }

    /// <summary>Appends JSON writing statements for type values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var full = value.AssemblyQualifiedName;");
        sb.AppendLine();
        sb.AppendLine("            if (full is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Type values without an assembly-qualified name are not supported.\");");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var shortName = global::System.Text.RegularExpressions.Regex.Replace(full, @\", Version=\\d+.\\d+.\\d+.\\d+, Culture=[\\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})\", \"\");");
        sb.AppendLine("            writer.WriteString(shortName);");
    }

    /// <summary>Appends JSON reading statements for type values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var typeName = reader.ReadString();");
        AppendDynamicTypeGuard(sb);
        sb.AppendLine("            value = global::System.Type.GetType(typeName, throwOnError: true);");
    }

    /// <summary>Rejects name-based resolution on runtimes without dynamic code support.</summary>
    /// <param name="sb">The builder receiving the formatter statements.</param>
    private static void AppendDynamicTypeGuard(StringBuilder sb)
    {
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)");
        sb.AppendLine("            {");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowPlatformNotSupportedException(\"Type deserialization by dynamic name requires dynamic code support.\");");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("#endif");
    }
}
