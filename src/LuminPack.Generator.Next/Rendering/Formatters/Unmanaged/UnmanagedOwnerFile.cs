using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders complete unmanaged payload roots from recursive layout facts.</summary>
internal static class UnmanagedOwnerFile
{
    /// <summary>Emits packed standalone operations and their ABI-preserving support views.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Render(SharedFormatterSet set, StructModel model, GeneratedOperations operations) => SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Identity.GlobalName)), model.Identity.GlobalName, false, (w, _) => FormatterStorageEmitter.Value(w, model.StoragePlan!, "value", true), (w, _) => FormatterStorageEmitter.Value(w, model.StoragePlan!, "value", false), (w, _) =>
    {
    }, (w, _) =>
    {
    }, w => FormatterStorageEmitter.Support(w, model.StoragePlan!));
}
