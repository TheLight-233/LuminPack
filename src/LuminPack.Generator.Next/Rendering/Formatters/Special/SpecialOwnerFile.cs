using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>The aliases a framework formatter body may reference.</summary>
internal readonly struct SpecialOwnerContext
{
    private readonly FormatterModel? _contract;
    /// <summary>Checks a runtime capability captured during analysis.</summary>
    /// <param name="capability">The capability supplied to <c>Supports</c>.</param>
    /// <returns>Whether the input satisfies the supports check.</returns>
    internal bool Supports(string capability) => _contract?.Supports(capability) == true;

    /// <summary>The profile type parameter in scope.</summary>
    internal readonly string Profile;

    /// <summary>The JSON writer alias.</summary>
    internal readonly string JsonWriter;

    /// <summary>The JSON reader alias.</summary>
    internal readonly string JsonReader;

    /// <summary>Captures the current aliases.</summary>
    /// <param name="profile">The compiled serialization profile or its generated type name.</param>
    /// <param name="jsonWriter">The JSON writer supplied to <c>SpecialOwnerContext</c>.</param>
    /// <param name="jsonReader">The JSON reader supplied to <c>SpecialOwnerContext</c>.</param>
    /// <param name="contract">The contract supplied to <c>SpecialOwnerContext</c>.</param>
    internal SpecialOwnerContext(string profile, string jsonWriter, string jsonReader, FormatterModel? contract = null)
    {
        _contract = contract;
        JsonReader = jsonReader;
        JsonWriter = jsonWriter;
        Profile = profile;
    }
}

/// <summary>Renders the shared formatter file of one framework type.</summary>
/// <remarks>The binary and JSON roots carry the type-specific body; the entries, owner class and forwarding methods follow
/// the same shape as the culture-info owner so every framework type is published and called the same way.</remarks>
internal static class SpecialOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="shortName">The readable owner suffix, which must match the payload key.</param>
    /// <param name="typeName">The fully qualified framework type.</param>
    /// <param name="referenceType">Whether the type is a reference type that frames a null record itself.</param>
    /// <param name="binaryWrite">Emits the binary write root body.</param>
    /// <param name="binaryRead">Emits the binary read root body.</param>
    /// <param name="jsonWrite">Emits the JSON write root body.</param>
    /// <param name="jsonRead">Emits the JSON read root body.</param>
    /// <returns>The complete source.</returns>
    /// <param name="support">The optional shared support declarations.</param>
    /// <param name="compressedEntries">The compressed entries used by this operation.</param>
    /// <param name="outlineJson">Whether JSON roots are kept out of their callers.</param>
    /// <param name="outlineBinaryWrite">Whether the binary write root is kept out of its callers.</param>
    /// <param name="binaryWriteEntries">The optional measurement declarations that accompany the write entries.</param>
    /// <param name="jsonWriteRun">Whether the JSON writer only frames a complete out-of-line element run.</param>
    /// <param name="optimizedRoots">Whether the outlined roots traverse elements and request full optimization where the
    /// runtime supports it.</param>
    /// <param name="outlineBinaryRead">Whether the binary read root keeps an independent inlining budget.</param>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations, string shortName, string typeName,
        bool referenceType, Action<CodeWriter, SpecialOwnerContext> binaryWrite, Action<CodeWriter, SpecialOwnerContext> binaryRead,
        Action<CodeWriter, SpecialOwnerContext> jsonWrite, Action<CodeWriter, SpecialOwnerContext> jsonRead, Action<CodeWriter>? support = null,
        bool compressedEntries = false, bool outlineJson = false, bool outlineBinaryWrite = false,
        Action<CodeWriter>? binaryWriteEntries = null, bool jsonWriteRun = false, bool optimizedRoots = false, bool outlineBinaryRead = false)
    {
        using var w = new CodeWriter(set.AvailableAliases) { Scoped = set.Options == null || set.Options.CSharpVersion >= 1100 };
        string serializer = w.A("LuminPackSerializer");
        const string writer = "LuminPackWriter";
        const string reader = "LuminPackReader";
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string roots = "LuminRoots_" + shortName;
        // Only element traversals opt in; scalar framework types keep the JIT's tiering.
        bool optimized = optimizedRoots && set.OptimizedRoots;

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots,
            "internal static partial class Binary_" + extension + "_" + shortName, null, EmitBinaryWrite, EmitBinaryRead,
            EmitJsonWrite, EmitJsonRead, support == null ? null : () => support(w));

        string Input() => referenceType ? typeName + " input" : "in " + typeName + " input";

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    if (compressedEntries)
                    {
                        w.Line("internal static void SerializeWithCompress<TLuminProfile>(scoped ref " + writer + " writer, scoped in " + typeName + " input)");
                        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite<TLuminProfile>(ref writer, input);");
                        w.Blank();
                    }
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + shortName + "(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + typeName + " input)");
                    w.Line("        => " + roots + ".RootWrite<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_" + shortName + "<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in " + typeName + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWrite<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    binaryWriteEntries?.Invoke(w);
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(outlineBinaryWrite ? w.MethodImpl("NoInlining", optimized) : inline);
                    w.Line("internal static void RootWrite<TLuminProfile>(scoped ref " + writer + " writer, " + Input() + ")");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    binaryWrite(w, new SpecialOwnerContext("TLuminProfile", jsonWriter, jsonReader, set.FindFormatter(typeName)));
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in " + typeName + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWrite<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + typeName + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWrite<TOptions>(ref state, value);");
                    w.Blank();
                    break;
            }
        }

        void EmitBinaryRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    if (compressedEntries)
                    {
                        w.Line("internal static void DeserializeWithCompress<TLuminProfile>(scoped ref " + reader + " reader, scoped ref " + typeName + " value)");
                        w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead<TLuminProfile>(ref reader, ref value);");
                        w.Blank();
                    }
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + shortName + "(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("        => " + roots + ".RootRead<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_" + shortName + "<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootRead<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(outlineBinaryRead ? w.MethodImpl("NoInlining") : inline);
                    w.Line("internal static void RootRead<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    binaryRead(w, new SpecialOwnerContext("TLuminProfile", jsonWriter, jsonReader, set.FindFormatter(typeName)));
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref " + typeName + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootRead<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + typeName + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootRead<TOptions>(ref state, ref value);");
                    w.Blank();
                    break;
            }
        }

        void EmitJsonWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + shortName + "(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + typeName + " input)");
                    w.Line("        => " + roots + ".RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_" + shortName + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in " + typeName + " input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    if (!jsonWriteRun)
                        w.Line(outlineJson ? w.MethodImpl("NoInlining", optimized) : inline);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    " + Input() + ")");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    jsonWrite(w, new SpecialOwnerContext("TLuminProfile", jsonWriter, jsonReader, set.FindFormatter(typeName)));
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in " + typeName + " value)");
                    w.Line("        => " + serializer + "." + roots + ".RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in " + typeName + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + "." + roots + ".RootWriteJson<TOptions>(ref state, value);");
                    w.Blank();
                    break;
            }
        }

        void EmitJsonRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + shortName + "(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("        => " + roots + ".RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_" + shortName + "<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => " + roots + ".RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line(outlineJson ? w.MethodImpl("NoInlining", optimized) : inline);
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref " + typeName + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    jsonRead(w, new SpecialOwnerContext("TLuminProfile", jsonWriter, jsonReader, set.FindFormatter(typeName)));
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref " + typeName + " value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + "." + roots + ".RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref " + typeName + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + "." + roots + ".RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
