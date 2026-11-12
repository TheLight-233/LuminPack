using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary formatter owner of <c>string</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's string roots and extension methods.</remarks>
internal static class StringOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        string writer = w.A("LuminPackWriter");
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;

        return FormatterOwnerFile.Render(w, operations, "internal static partial class LuminRoots_String", "internal static partial class Binary_" + extension + "_String",
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_String(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in string input) => LuminRoots_String.RootWrite<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_String<TLuminProfile>(scoped ref " + writer + " writer, scoped in string input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_String.RootWrite<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_String(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref string value) => MeasureRecord_String<" + utf16 + ">(ref evaluator, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_String<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref string value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => evaluator.Add(evaluator.GetStringLength<TLuminProfile>(ref value));");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWrite<TLuminProfile>(scoped ref " + writer + " writer, string input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("writer.Write<TLuminProfile>(value);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in string value) => " + serializer + ".LuminRoots_String.RootWrite<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in string value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_String.RootWrite<TOptions>(ref state, value);");
                    w.Blank();
                    break;
            }
        }

        void EmitBinaryRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_String(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref string value) => Deserialize_String<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_String<TLuminProfile>(scoped ref " + reader + " reader, scoped ref string value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => reader.Read<TLuminProfile>(ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref string value) => " + serializer + ".Deserialize_String<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref string value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".Deserialize_String<TOptions>(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_String(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in string input) => LuminRoots_String.RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_String<TLuminProfile>(scoped ref " + jsonWriter + " writer, scoped in string input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_String.RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(scoped ref " + jsonWriter + " writer, string input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("writer.Write<TLuminProfile>(value);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in string value) => " + serializer + ".LuminRoots_String.RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in string value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_String.RootWriteJson<TOptions>(ref state, value);");
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
                    w.Line("internal static void DeserializeJson_String(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref string value) => LuminRoots_String.RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_String<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref string value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_String.RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootReadJson<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref string value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.Null)");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("if (reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.String)");
                    w.Line("    " + exception + ".ThrowInvalidOperationException(\"Expected string token\");");
                    w.Line("reader.Read<TLuminProfile>(ref value);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref string value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_String.RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref string value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_String.RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
