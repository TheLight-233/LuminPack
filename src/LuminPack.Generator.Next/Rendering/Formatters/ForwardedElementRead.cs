using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the checked read of a nested framework formatter.</summary>
internal static class ForwardedElementRead
{
    /// <summary>Emits the child read and its Boolean-domain validation when applicable.</summary>
    /// <param name="writer">The owner source writer.</param>
    /// <param name="type">The child collection type.</param>
    /// <param name="state">The reader expression.</param>
    /// <param name="value">The destination expression.</param>
    /// <param name="call">The concrete static read call.</param>
    /// <param name="compressed">Whether the child uses the compressed representation.</param>
    internal static void Emit(CodeWriter writer, string type, string state, string value, string call, bool compressed = false)
    {
        const string policy = "global::LuminPack.Security.LuminPackInputPolicy";
        string identity = GeneratedNames.PayloadKey(type);
        if (identity == "bool[]" && !compressed)
        {
            writer.Line(policy + ".ReadBooleans(ref " + state + ", ref " + value + ");");
            return;
        }

        writer.Line(call);
        if (identity is "bool[]" or "System.Collections.Generic.List<bool>")
            writer.Line(policy + ".CheckBooleans(" + value + ");");
    }
}
