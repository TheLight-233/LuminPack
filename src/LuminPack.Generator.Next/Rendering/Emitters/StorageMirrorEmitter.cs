using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes inherited Local storage levels without flattening their CLR layout.</summary>
internal static class StorageMirrorEmitter
{
    /// <summary>Emits each base-first layout declaration once inside its payload container.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="mirrors">The immutable base-first physical storage levels.</param>
    internal static void Emit(CodeWriter w, StorageMirrorModel[] mirrors)
    {
        foreach (StorageMirrorModel mirror in mirrors)
        {
            w.Blank();
            w.Line(w.Preserve);
            if (mirror.Layout != null)
                w.Line(mirror.Layout);
            w.Line("private class " + mirror.Name + (mirror.Base == null ? string.Empty : " : " + mirror.Base));
            foreach (string constraint in mirror.Constraints)
                w.Indented(() => w.Line(constraint));
            w.Open();
            BinaryEmitter.MirrorFields(w, mirror.Fields, mirror.Offsets!);
            w.Close();
        }
    }
}
