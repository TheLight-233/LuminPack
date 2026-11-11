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
        return WithEntries(writer.Complete(json: false));

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

    private static readonly System.Text.RegularExpressions.Regex RootSignature = new(
        @"internal static void (RootWrite|RootRead|RootWriteJson|RootReadJson)(<\w+>)?\(\s*scoped ref [\w.:]+ (?:writer|reader),\s*(?:scoped )?(in|ref) (.+?) (?:value|input)\)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex RootsDeclaration = new(
        @"^( *)internal static partial class LuminRoots_(\w+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline |
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Adds the serializer's state entries to an owner whose template publishes only its roots.</summary>
    /// <param name="source">The rendered owner file.</param>
    /// <returns>The file with <c>Serialize_</c>, <c>Deserialize_</c>, <c>SerializeJson_</c> and <c>DeserializeJson_</c> entries
    /// beside the roots, or the unchanged file when its template already declares entries.</returns>
    /// <remarks>Every published root has these entries in the original generator: the profile-free entry runs the default
    /// profile and the generic entry forwards its own. Registrations and tests bind them by name.</remarks>
    private static string WithEntries(string source)
    {
        System.Text.RegularExpressions.Match roots = RootsDeclaration.Match(source);
        if (!roots.Success || source.Contains(" Serialize_") || source.Contains(" Deserialize_") || source.Contains(" SerializeJson_") ||
            source.Contains(" DeserializeJson_"))
            return source;
        string indent = roots.Groups[1].Value, name = roots.Groups[2].Value;
        var entries = new System.Text.StringBuilder();
        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (System.Text.RegularExpressions.Match root in RootSignature.Matches(source))
        {
            string operation = root.Groups[1].Value;
            if (!seen.Add(operation))
                continue;
            bool generic = root.Groups[2].Success;
            string type = root.Groups[4].Value;
            bool write = operation is "RootWrite" or "RootWriteJson";
            bool json = operation.EndsWith("Json", StringComparison.Ordinal);
            string state = "global::LuminPack.Core.LuminPack" + (json ? "Json" : string.Empty) + (write ? "Writer" : "Reader");
            string entry = (write ? "Serialize" : "Deserialize") + (json ? "Json" : string.Empty) + "_" + name;
            string parameters = "scoped ref " + state + (write ? " writer, scoped in " : " reader, scoped ref ") + type +
                (write ? " input)" : " value)");
            string arguments = write ? "(ref writer, in input)" : "(ref reader, ref value)";
            const string Preserve = "[global::LuminPack.Attribute.PreserveAttribute]";
            const string Inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
            entries.Append(indent).Append(Preserve).Append('\n').Append(indent).Append(Inline).Append('\n');
            entries.Append(indent).Append("internal static void ").Append(entry).Append('(').Append(parameters).Append('\n');
            entries.Append(indent).Append("    => LuminRoots_").Append(name).Append('.').Append(operation)
                .Append(generic ? "<global::LuminPack.Option.LuminPackUtf16LengthOptions>" : string.Empty).Append(arguments).Append(";\n\n");
            entries.Append(indent).Append(Preserve).Append('\n').Append(indent).Append(Inline).Append('\n');
            entries.Append(indent).Append("internal static void ").Append(entry).Append("<TLuminProfile>(").Append(parameters).Append('\n');
            entries.Append(indent).Append("    where TLuminProfile : struct, global::LuminPack.Option.ILuminPackOptions\n");
            entries.Append(indent).Append("    => LuminRoots_").Append(name).Append('.').Append(operation)
                .Append(generic ? "<TLuminProfile>" : string.Empty).Append(arguments).Append(";\n\n");
        }
        return entries.Length == 0 ? source : source.Insert(roots.Index, entries.ToString());
    }
}
