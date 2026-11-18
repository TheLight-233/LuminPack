using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders complete unmanaged payload roots from recursive layout facts.</summary>
internal static class UnmanagedOwnerFile
{
    /// <summary>Identifies compact, exact standalone layouts that can publish stores over reserved output.</summary>
    /// <param name="model">The existing portable storage model.</param>
    /// <returns>Whether the root can use its packed transfer without a writer.</returns>
    internal static bool CanWriteFixedRoot(StructModel model) =>
        model is { Storage: StructStorage.PortableUnmanaged, Rejection: null, Versioned: false, Callbacks: false,
            StoragePlan: { WireSize: > 0, Volatile: false } layout } && !model.Identity.IsGeneric &&
        DirectInlining.PortableStorageWrite(layout);

    /// <summary>Emits packed standalone operations and their ABI-preserving support views.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Render(SharedFormatterSet set, StructModel model, GeneratedOperations operations) => SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Identity.GlobalName)), model.Identity.GlobalName, false, (w, _) => FormatterStorageEmitter.Value(w, model.StoragePlan!, "value", true), (w, _) => FormatterStorageEmitter.Value(w, model.StoragePlan!, "value", false), (w, _) =>
    {
    }, (w, _) =>
    {
    }, w =>
    {
        FormatterStorageEmitter.Support(w, model.StoragePlan!);
        if ((operations & GeneratedOperations.BinaryWrite) == 0 || !CanWriteFixedRoot(model))
            return;
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void WriteFixedStorage(ref byte destination, scoped in " + model.Identity.GlobalName + " input)");
        w.Open();
        w.Line("var value = input;");
        FormatterStorageEmitter.Transfer(w, model.StoragePlan!, "value", "destination", write: true);
        w.Close();
    });
}
