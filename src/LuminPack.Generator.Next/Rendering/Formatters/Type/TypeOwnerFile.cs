using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Type</c>.</summary>
/// <remarks>Both forms store the assembly-qualified name with the version, culture and key token removed, so a type resolves
/// against the defining assembly of the process that reads the payload.</remarks>
internal static class TypeOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations) =>
        SpecialOwnerFile.Render(set, operations, "Type", "global::System.Type", true, BinaryWrite, BinaryRead, JsonWrite,
            JsonRead);

    private static void BinaryWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNullObjectHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var full = value.AssemblyQualifiedName;");
        w.Line("if (string.IsNullOrEmpty(full))");
        w.Open();
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Type values without an assembly-qualified name are not supported.\");");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteObjectHeader(0);");
        w.Line("writer.WriteTypeName<" + context.Profile + ">(full);");
    }

    private static void BinaryRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var typeName = reader.Read<" + context.Profile + ">(null);");
        w.Line("if (string.IsNullOrEmpty(typeName))");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (!global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)");
        w.Open();
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowPlatformNotSupportedException(\"Type deserialization by dynamic name requires dynamic code support.\");");
        w.Line("return;");
        w.Close();
        w.Line("#endif");
        w.Line("value = global::System.Type.GetType(typeName, throwOnError: true);");
    }

    private static void JsonWrite(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (value == null)");
        w.Open();
        w.Line("writer.WriteNull<" + context.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var full = value.AssemblyQualifiedName;");
        w.Line("if (full is null)");
        w.Open();
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Type values without an assembly-qualified name are not supported.\");");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteTypeName<" + context.Profile + ">(full);");
    }

    private static void JsonRead(CodeWriter w, SpecialOwnerContext context)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var typeName = reader.Read<string, " + context.Profile + ">();");
        w.Line("#if NET8_0_OR_GREATER");
        w.Line("if (!global::System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)");
        w.Open();
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowPlatformNotSupportedException(\"Type deserialization by dynamic name requires dynamic code support.\");");
        w.Line("return;");
        w.Close();
        w.Line("#endif");
        w.Line("value = global::System.Type.GetType(typeName, throwOnError: true);");
    }
}
