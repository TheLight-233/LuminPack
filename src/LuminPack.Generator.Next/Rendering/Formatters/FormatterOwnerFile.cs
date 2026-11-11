using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Identifies the declaration container for one part of a formatter operation.</summary>
internal enum FormatterSection
{
    /// <summary>The serializer's state entry points and measurement methods.</summary>
    Entries,

    /// <summary>The nested execution roots.</summary>
    Roots,

    /// <summary>The static formatter owner and its state forwarding methods.</summary>
    Owner,

    /// <summary>Additional assembly-local extension entries.</summary>
    Extensions,
}

/// <summary>Publishes the selected formatter operations in their shared declaration containers.</summary>
internal static class FormatterOwnerFile
{
    /// <summary>Renders the execution roots and forwarders for the same operation selection.</summary>
    /// <param name="writer">The writer that tracks the file's aliases.</param>
    /// <param name="operations">The operations requested for this formatter.</param>
    /// <param name="roots">The nested roots declaration.</param>
    /// <param name="owner">The formatter owner declaration.</param>
    /// <param name="extensions">The extension container declaration, if applicable.</param>
    /// <param name="binaryWrite">Emits the binary write operation.</param>
    /// <param name="binaryRead">Emits the binary read operation.</param>
    /// <param name="jsonWrite">Emits the JSON write operation.</param>
    /// <param name="jsonRead">Emits the JSON read operation.</param>
    /// <returns>The complete formatter source.</returns>
    /// <param name="support">The optional shared support declarations.</param>
    internal static string Render(CodeWriter writer, GeneratedOperations operations, string roots, string owner,
        string? extensions, Action<FormatterSection> binaryWrite, Action<FormatterSection> binaryRead,
        Action<FormatterSection> jsonWrite, Action<FormatterSection> jsonRead, Action? support = null)
    {
        if (operations == GeneratedOperations.None)
            return string.Empty;

        writer.Line("namespace LuminPack");
        writer.Open();
        writer.Line("public static partial class LuminPackSerializer");
        writer.Open();
        Emit(FormatterSection.Entries);
        writer.Line(roots);
        writer.Open();
        Emit(FormatterSection.Roots);
        support?.Invoke();
        writer.Close();
        writer.Close();
        writer.Blank();

        writer.Line(owner);
        writer.Open();
        Emit(FormatterSection.Owner);
        writer.Close();

        if (extensions != null && (operations & (GeneratedOperations.BinaryWrite | GeneratedOperations.BinaryRead)) != 0)
        {
            writer.Blank();
            writer.Line(extensions);
            writer.Open();
            Emit(FormatterSection.Extensions);
            writer.Close();
        }

        writer.Close();
        return writer.Complete(json: false);

        void Emit(FormatterSection section)
        {
            if ((operations & GeneratedOperations.BinaryWrite) != 0)
                binaryWrite(section);
            if ((operations & GeneratedOperations.BinaryRead) != 0)
                binaryRead(section);
            if ((operations & GeneratedOperations.JsonWrite) != 0)
                jsonWrite(section);
            if ((operations & GeneratedOperations.JsonRead) != 0)
                jsonRead(section);
        }
    }
}
