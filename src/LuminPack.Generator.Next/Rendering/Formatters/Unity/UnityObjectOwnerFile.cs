using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the Unity native-owner objects <c>AnimationCurve</c>, <c>Gradient</c> and
/// <c>RectOffset</c>.</summary>
/// <remarks>The binary operations forward to the Unity runtime module, which owns the native layout and the null framing; the
/// JSON operations are not configured for native owners and throw, as the original pipeline does.</remarks>
internal static class UnityObjectOwnerFile
{
    /// <summary>Renders the owner of one Unity object type.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="shortName">The readable owner suffix, which must match the payload key.</param>
    /// <param name="typeName">The fully qualified Unity type.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations, string shortName, string typeName) =>
        SpecialOwnerFile.Render(set, operations, shortName, typeName, true,
            (w, c) => w.Line("global::LuminPack.Unity.LuminUnity.Write(ref writer, value);"),
            (w, c) => w.Line("global::LuminPack.Unity.LuminUnity.Read(ref reader, ref value);"),
            (w, c) => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Unity native-owner JSON codecs are not configured.\");"),
            (w, c) => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotSupportedException(\"Unity native-owner JSON codecs are not configured.\");"));
}
