using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Builds concrete state entry points that invoke the registered custom method pointers.</summary>
    /// <param name="builder">The temporary operation template builder.</param>
    /// <param name="field">The closed custom payload type.</param>
    /// <param name="metadata">Current unsafe and registration settings.</param>
    private static void AppendCustomFormatterExtensions(StringBuilder builder, LuminLocalFieldData field, MetaInfo metadata)
    {
        AppendBinaryFormatterExtension(builder, field.TypeName, field,
            ((item, output) => AppendCustomOperation(output, item.TypeName, "Writer", "writer", "WriteValue", "in", metadata),
             (item, output) => AppendCustomOperation(output, item.TypeName, "Reader", "reader", "ReadValue", "ref", metadata)), metadata);
        AppendJsonFormatterExtension(builder, field.TypeName, field,
            ((item, output) => AppendCustomOperation(output, item.TypeName, "JsonWriter", "writer", "WriteValueJson", "in", metadata),
             (item, output) => AppendCustomOperation(output, item.TypeName, "JsonReader", "reader", "ReadValueJson", "ref", metadata)), metadata);
        // Custom types register no width method, so a direct-record width never reaches them.
        AppendCalculateFormatterExtension(builder, field.TypeName, field,
            (_, output) => output.AppendLine("            " + UnsupportedWidth), metadata);
    }

    /// <summary>Invokes one custom operation without adding framing or inspecting payload fields.</summary>
    /// <param name="builder">The method body builder.</param>
    /// <param name="type">The closed payload name.</param>
    /// <param name="stateType">The operation state type suffix.</param>
    /// <param name="state">The operation state parameter.</param>
    /// <param name="pointer">The registry entry field holding the method address.</param>
    /// <param name="modifier">The payload reference modifier.</param>
    /// <param name="metadata">Current unsafe and registration settings.</param>
    private static void AppendCustomOperation(StringBuilder builder, string type, string stateType, string state,
        string pointer, string modifier, MetaInfo metadata)
    {
        string missing = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(" + type + "));";
        if (!metadata.AllowUnsafe || metadata.RegisterMode == LuminPackRegisterMode.Disabled)
        {
            builder.AppendLine("            " + missing);
            return;
        }
        builder.AppendLine("            if (!global::LuminPack.Code.LuminPackFormatterRegistry.TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof(" + type + ")), out var entry) || entry." + pointer + " == 0)");
        builder.AppendLine("            {");
        builder.AppendLine("                " + missing);
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        builder.AppendLine("            unsafe { ((delegate*<ref global::LuminPack.Core.LuminPack" + stateType + ", " + modifier + " " + type + ", void>)entry." + pointer + ")(ref " + state + ", " + modifier + " value); }");
    }
}
