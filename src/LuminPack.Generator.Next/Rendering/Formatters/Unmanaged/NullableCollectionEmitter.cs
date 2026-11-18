using System.Globalization;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the checked nullable block transfer selected by a collection's element contract.</summary>
internal sealed class NullableCollectionEmitter
{
    private const string Core = "global::LuminPack.Code.LuminPackNullableRectangle";
    private readonly FormatterStorageModel _storage;
    private readonly string _arguments;
    private readonly string _encoding;
    private readonly string _hasTag;
    private readonly string? _readOwner;

    /// <summary>Captures source expressions shared by the read and write of a proven block.</summary>
    /// <param name="storage">The descriptor containing the modeled nullable layouts.</param>
    /// <param name="payload">The reference-free underlying type.</param>
    /// <param name="encoding">The selected Tagged or Native mode.</param>
    /// <param name="readOwner">The closed owner exposing reads to an enclosing input-policy copy.</param>
    private NullableCollectionEmitter(FormatterStorageModel storage, string payload, int encoding, string? readOwner)
    {
        _storage = storage;
        _arguments = "<" + payload + ", " + FormatterStorageEmitter.Layout(storage) + ">";
        _encoding = encoding.ToString(CultureInfo.InvariantCulture);
        _hasTag = encoding == 1 && storage.ModelSizes.Length > 1 ? "true" : "false";
        _readOwner = readOwner;
    }

    /// <summary>Selects block operations if the element contract proved a supported layout.</summary>
    /// <param name="element">The element formatter contract.</param>
    /// <param name="readOwner">The closed owner exposing reads to an enclosing input-policy copy.</param>
    /// <returns>The closed block emitter, or null for a framed element.</returns>
    internal static NullableCollectionEmitter? For(FormatterModel? element, string? readOwner = null) =>
        element is { NullableEncoding: > 0, NullableStorage: { NullablePayload: { } payload } storage }
            ? new NullableCollectionEmitter(storage, payload, element.NullableEncoding, readOwner) : null;

    /// <summary>Emits the descriptor and closed read operations accessible to the checked projection.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="read">Whether a binary read is published.</param>
    internal void Support(CodeWriter w, bool read = false)
    {
        FormatterStorageEmitter.Support(w, _storage);
        if (!read || _readOwner == null)
            return;
        const string readerType = "global::LuminPack.Core.LuminPackReader";
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void ValidateNullableCollection(scoped ref " + readerType + " reader, int length, out byte storageTag) =>");
        w.Indented(() => w.Line(Core + ".Validate" + _arguments +
            "(ref reader, length, " + _encoding + ", " + _hasTag + ", out storageTag);"));
        w.Blank();
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("internal static void ReadNullableCollection(scoped ref " + readerType +
            " reader, scoped global::System.Span<" + _storage.Type + "> items, byte storageTag) =>");
        w.Indented(() => Read(w, "reader", "items", "ReadCollection"));
        w.Blank();
    }

    /// <summary>Writes the element count and the proven storage block.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The wire writer expression.</param>
    /// <param name="items">The source span expression.</param>
    internal void WriteCollection(CodeWriter w, string writer, string items) =>
        w.Line(Core + ".WriteCollection" + _arguments + "(ref " + writer + ", " + items + ", " + _encoding + ", " + _hasTag + ");");

    /// <summary>Writes the proven storage block after its existing framing.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The wire writer expression.</param>
    /// <param name="items">The source span expression.</param>
    internal void WritePayload(CodeWriter w, string writer, string items) =>
        w.Line(Core + ".Write" + _arguments + "(ref " + writer + ", " + items + ", " + _encoding + ", " + _hasTag + ");");

    /// <summary>Validates the incoming block before destination preparation.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The wire reader expression.</param>
    /// <param name="length">The validated element count expression.</param>
    internal void Validate(CodeWriter w, string reader, string length) =>
        w.Line(_readOwner != null
            ? _readOwner + ".ValidateNullableCollection(ref " + reader + ", " + length + ", out byte storageTag);"
            : Core + ".Validate" + _arguments + "(ref " + reader + ", " + length + ", " + _encoding + ", " + _hasTag + ", out byte storageTag);");

    /// <summary>Transfers the previously validated payload into the destination.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The wire reader expression.</param>
    /// <param name="items">The destination span expression.</param>
    internal void ReadCollection(CodeWriter w, string reader, string items) =>
        w.Line(_readOwner + ".ReadNullableCollection(ref " + reader + ", " + items + ", storageTag);");

    /// <summary>Transfers the previously validated payload into a newly allocated rectangular array.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The wire reader expression.</param>
    /// <param name="items">The new array span expression.</param>
    internal void ReadPayload(CodeWriter w, string reader, string items) => Read(w, reader, items, "Read");

    /// <summary>Renders the read using the runtime operation with the destination's aliasing contract.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The wire reader expression.</param>
    /// <param name="items">The destination span expression.</param>
    /// <param name="method">The selected runtime read method.</param>
    private void Read(CodeWriter w, string reader, string items, string method) =>
        w.Line(Core + "." + method + _arguments + "(ref " + reader + ", " + items + ", " + _encoding + ", storageTag);");
}
