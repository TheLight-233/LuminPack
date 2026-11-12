using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary and JSON formatter owner of <c>System.Globalization.CultureInfo</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's culture roots and extension methods. The binary and JSON
/// roots resolve names through the runtime cache when the referenced assembly exposes it, otherwise through the framework
/// culture lookup.</remarks>
internal static class CultureInfoOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        const string writer = "LuminPackWriter";
        const string reader = "LuminPackReader";
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string code = w.A("LuminPackCode");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;
        string cache = "global::LuminPack.Code.LuminPackCultureInfoCache";
        string resolve = set.CultureInfoReuse ? cache + ".ResolveCulture" : "global::System.Globalization.CultureInfo.GetCultureInfo";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class LuminRoots_CultureInfo", "internal static partial class Binary_" + extension + "_CultureInfo",
            null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Entries:
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_CultureInfo(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in global::System.Globalization.CultureInfo input)");
                    w.Line("        => LuminRoots_CultureInfo.RootWrite<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Serialize_CultureInfo<TLuminProfile>(");
                    w.Line("    scoped ref " + writer + " writer,");
                    w.Line("    scoped in global::System.Globalization.CultureInfo input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_CultureInfo.RootWrite<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_CultureInfo(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("        => MeasureRecord_CultureInfo<" + utf16 + ">(ref evaluator, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void MeasureRecord_CultureInfo<TLuminProfile>(");
                    w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("evaluator.Add(1);");
                    w.Line("if (value is null)");
                    w.Line("    return;");
                    w.Line("var text = value.Name;");
                    w.Line("evaluator.Add(evaluator.GetStringLength<TLuminProfile>(ref text));");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWrite<TLuminProfile>(scoped ref " + writer + " writer, global::System.Globalization.CultureInfo input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value == null)");
                    w.Open();
                    w.Line("writer.Cursor = " + code + ".NullObject;");
                    w.Line("writer.Advance(1);");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.Cursor = unchecked((byte)(0));");
                    w.Line("writer.Advance(1);");
                    w.Line("writer.Write<TLuminProfile>(value.Name);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + writer + " state,");
                    w.Line("    in global::System.Globalization.CultureInfo value)");
                    w.Line("        => " + serializer + ".LuminRoots_CultureInfo.RootWrite<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in global::System.Globalization.CultureInfo value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_CultureInfo.RootWrite<TOptions>(ref state, value);");
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
                    w.Line("internal static void Deserialize_CultureInfo(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("        => LuminRoots_CultureInfo.RootRead<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void Deserialize_CultureInfo<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_CultureInfo.RootRead<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootRead<TLuminProfile>(");
                    w.Line("    scoped ref " + reader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (!reader.TryReadObjectHead())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    if (set.CultureInfoReuse)
                    {
                        w.Line("var name = value?.GetType() == typeof(global::System.Globalization.CultureInfo) && value.IsReadOnly");
                        w.Line("    ? value.Name");
                        w.Line("    : " + cache + ".Name;");
                    }
                    else
                        w.Line("var name = value?.GetType() == typeof(global::System.Globalization.CultureInfo) && value.IsReadOnly ? value.Name : null;");
                    w.Line("var str = reader.Read<TLuminProfile>(name);");
                    w.Line("value = " + resolve + "(str ?? string.Empty);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(");
                    w.Line("    this ref " + reader + " state,");
                    w.Line("    ref global::System.Globalization.CultureInfo value)");
                    w.Line("        => " + serializer + ".LuminRoots_CultureInfo.RootRead<" + utf16 + ">(ref state, ref value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_CultureInfo.RootRead<TOptions>(ref state, ref value);");
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
                    w.Line("internal static void SerializeJson_CultureInfo(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in global::System.Globalization.CultureInfo input)");
                    w.Line("        => LuminRoots_CultureInfo.RootWriteJson<" + utf16 + ">(ref writer, input);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void SerializeJson_CultureInfo<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    scoped in global::System.Globalization.CultureInfo input)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_CultureInfo.RootWriteJson<TLuminProfile>(ref writer, input);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonWriter + " writer,");
                    w.Line("    global::System.Globalization.CultureInfo input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value == null)");
                    w.Open();
                    w.Line("writer.WriteNull<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.Write<TLuminProfile>(value.Name);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Write(");
                    w.Line("    this ref " + jsonWriter + " state,");
                    w.Line("    in global::System.Globalization.CultureInfo value)");
                    w.Line("        => " + serializer + ".LuminRoots_CultureInfo.RootWriteJson<" + utf16 + ">(ref state, value);");
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in global::System.Globalization.CultureInfo value)");
                    w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_CultureInfo.RootWriteJson<TOptions>(ref state, value);");
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
                    w.Line("internal static void DeserializeJson_CultureInfo(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("        => LuminRoots_CultureInfo.RootReadJson<" + utf16 + ">(ref reader, ref value);");
                    w.Blank();
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void DeserializeJson_CultureInfo<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_CultureInfo.RootReadJson<TLuminProfile>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Roots:
                {
                    w.Line(preserve);
                    w.Line(inline);
                    w.Line("internal static void RootReadJson<TLuminProfile>(");
                    w.Line("    scoped ref " + jsonReader + " reader,");
                    w.Line("    scoped ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    if (set.CultureInfoReuse)
                    {
                        w.Line("string str = " + cache + ".Name!;");
                        w.Line("if (str is not null && reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.String)");
                        w.Line("    reader.Read<TLuminProfile>(ref str);");
                        w.Line("else");
                        w.Line("    str = reader.Read<string, TLuminProfile>();");
                    }
                    else
                        w.Line("var str = reader.Read<string, TLuminProfile>();");
                    w.Line("value = " + resolve + "(str ?? string.Empty);");
                    w.Close();
                    w.Blank();
                    break;
                }
                case FormatterSection.Owner:
                    w.Line(inline);
                    w.Line("internal static void Read(this ref " + jsonReader + " state, ref global::System.Globalization.CultureInfo value)");
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_CultureInfo.RootReadJson<" + utf16 + ">(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    w.Line(inline);
                    w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref global::System.Globalization.CultureInfo value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
                    w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(serializer + ".LuminRoots_CultureInfo.RootReadJson<TOptions>(ref state, ref value);");
                    w.Line("state.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }
    }
}
