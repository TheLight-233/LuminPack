using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Forwards native-owner Unity types to the optional module without inspecting or serializing their private native handles.</summary>
public static class UnityObjectEmitter
{
    /// <summary>Invokes the external complete binary write.</summary>
    /// <param name="data">The selected native-owner contract.</param><param name="builder">Generated source.</param>
    public static void Write(LuminLocalFieldData data, StringBuilder builder)
        => builder.AppendLine("            global::LuminPack.Unity.LuminUnity.Write(ref writer, value);");

    /// <summary>Invokes the external complete binary read, preserving its owner reuse and cursor publication.</summary>
    /// <param name="data">The selected native-owner contract.</param><param name="builder">Generated source.</param>
    public static void Read(LuminLocalFieldData data, StringBuilder builder)
        => builder.AppendLine("            global::LuminPack.Unity.LuminUnity.Read(ref reader, ref value);");

    /// <summary>Invokes the external exact size operation.</summary>
    /// <param name="data">The selected native-owner contract.</param><param name="builder">Generated source.</param>
    public static void Size(LuminLocalFieldData data, StringBuilder builder)
        => builder.AppendLine("            global::LuminPack.Unity.LuminUnity.Size(ref evaluator, value);");

    /// <summary>Rejects unavailable JSON rather than falling back to native object storage.</summary>
    /// <param name="data">The selected native-owner contract.</param><param name="builder">Generated source.</param>
    public static void UnsupportedJson(LuminLocalFieldData data, StringBuilder builder)
        => builder.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Unity native-owner JSON codecs are not configured.\");");
}
