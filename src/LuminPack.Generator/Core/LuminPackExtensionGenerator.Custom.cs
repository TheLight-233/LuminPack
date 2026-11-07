using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Builds concrete state entry points that invoke the registered custom delegates.</summary>
    /// <param name="builder">The temporary operation template builder.</param>
    /// <param name="field">The closed custom payload type.</param>
    /// <param name="metadata">Current registration settings.</param>
    private static void AppendCustomFormatterExtensions(StringBuilder builder, LuminLocalFieldData field, MetaInfo metadata)
    {
        AppendBinaryFormatterExtension(builder, field.TypeName, field,
            ((item, output) => AppendCustomOperation(output, item.TypeName, "Writer", "writer", "WriteValue", "in", metadata),
             (item, output) => AppendCustomOperation(output, item.TypeName, "Reader", "reader", "ReadValue", "ref", metadata)), metadata);
        AppendJsonFormatterExtension(builder, field.TypeName, field,
            ((item, output) => AppendCustomOperation(output, item.TypeName, "JsonWriter", "writer", "WriteValueJson", "in", metadata),
             (item, output) => AppendCustomOperation(output, item.TypeName, "JsonReader", "reader", "ReadValueJson", "ref", metadata)), metadata);
    }

    /// <summary>Invokes one custom operation without adding framing or inspecting payload fields.</summary>
    /// <param name="builder">The method body builder.</param>
    /// <param name="type">The closed payload name.</param>
    /// <param name="stateType">The operation state type suffix.</param>
    /// <param name="state">The operation state parameter.</param>
    /// <param name="callback">The registry entry field holding the operation delegate.</param>
    /// <param name="modifier">The payload reference modifier.</param>
    /// <param name="metadata">Current registration settings.</param>
    private static void AppendCustomOperation(StringBuilder builder, string type, string stateType, string state,
        string callback, string modifier, MetaInfo metadata)
    {
        string missing = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(" + type + "));";
        if (metadata.RegisterMode == LuminPackRegisterMode.Disabled)
        {
            builder.AppendLine("            " + missing);
            return;
        }
        builder.AppendLine("            if (!global::LuminPack.Code.LuminPackFormatterRegistry.TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof(" + type + ")), out var entry) || entry." + callback + " == null)");
        builder.AppendLine("            {");
        builder.AppendLine("                " + missing);
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        string callbackType = "global::LuminPack.Code.LuminPack" + stateType.Replace("Writer", "Write").Replace("Reader", "Read") + "Delegate<" + type + ">";
        builder.AppendLine("            ((" + callbackType + ")entry." + callback + ")(ref " + state + ", " + modifier + " value);");
    }
}
