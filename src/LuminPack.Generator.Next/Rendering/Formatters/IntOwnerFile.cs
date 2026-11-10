using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared binary formatter owner of <c>int</c>.</summary>
/// <remarks>The members mirror the legacy formatter generator's scalar roots and extension methods. The array and list JSON
/// roots transfer their elements through this owner, so it is published with either of them.</remarks>
internal static class IntOwnerFile
{
    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set)
    {
        var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        string writer = w.A("LuminPackWriter");
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string exception = w.A("LuminPackExceptionHelper");
        string options = w.A("ILuminPackOptions");
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string memory = w.A("Unsafe");
        string preserve = w.Preserve;
        string inline = w.MethodImpl("AggressiveInlining");
        string extension = set.Extension!;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_Int(");
        w.Line("    scoped ref " + writer + " writer,");
        w.Line("    scoped in int input) => LuminRoots_Int.RootWrite(ref writer, in input);");
        w.Blank();

        w.Line("internal static partial class LuminRoots_Int");
        w.Open();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite(scoped ref " + writer + " writer, scoped in int input)");
        w.Open();
        w.Line("var value = input;");
        w.Line("writer.Write(in value);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootWriteJson<TLuminProfile>(scoped ref " + jsonWriter + " writer, scoped in int input)");
        w.Line("    where TLuminProfile : struct, " + options);
        w.Open();
        w.Line("var value = input;");
        w.Line("writer.Write<TLuminProfile>(value);");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void RootReadJson<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref int value)");
        w.Line("    where TLuminProfile : struct, " + options + " => value = reader.Read<int, TLuminProfile>();");
        w.Close();
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_Int<TLuminProfile>(scoped ref " + writer + " writer, scoped in int input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_Int.RootWrite(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_Int(scoped ref " + reader + " reader, scoped ref int value) => reader.Read(ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_Int<TLuminProfile>(scoped ref " + reader + " reader, scoped ref int value)");
        w.Line("    where TLuminProfile : struct, " + options + " => Deserialize_Int(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_Int(");
        w.Line("    scoped ref " + jsonWriter + " writer,");
        w.Line("    scoped in int input) => LuminRoots_Int.RootWriteJson<" + utf16 + ">(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_Int<TLuminProfile>(scoped ref " + jsonWriter + " writer, scoped in int input)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_Int.RootWriteJson<TLuminProfile>(ref writer, in input);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_Int(");
        w.Line("    scoped ref " + jsonReader + " reader,");
        w.Line("    scoped ref int value) => LuminRoots_Int.RootReadJson<" + utf16 + ">(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_Int<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref int value)");
        w.Line("    where TLuminProfile : struct, " + options + " => LuminRoots_Int.RootReadJson<TLuminProfile>(ref reader, ref value);");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_Int(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref int value) => evaluator.Add(" + memory + ".SizeOf<int>());");
        w.Blank();

        w.Line(preserve);
        w.Line(inline);
        w.Line("internal static void MeasureRecord_Int<TLuminProfile>(");
        w.Line("    scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator,");
        w.Line("    scoped ref int value)");
        w.Line("    where TLuminProfile : struct, " + options + " => MeasureRecord_Int(ref evaluator, ref value);");
        w.Close();
        w.Blank();

        w.Line("internal static partial class Binary_" + extension + "_Int");
        w.Open();
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + writer + " state,");
        w.Line("    in int value) => " + serializer + ".LuminRoots_Int.RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in int value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_Int.RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(");
        w.Line("    this ref " + reader + " state,");
        w.Line("    ref int value) => " + serializer + ".Deserialize_Int(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref int value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".Deserialize_Int(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Write(");
        w.Line("    this ref " + jsonWriter + " state,");
        w.Line("    in int value) => " + serializer + ".LuminRoots_Int.RootWriteJson<" + utf16 + ">(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + jsonWriter + " state, in int value)");
        w.Line("    where TOptions : struct, " + options + " => " + serializer + ".LuminRoots_Int.RootWriteJson<TOptions>(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Read(this ref " + jsonReader + " state, ref int value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<" + utf16 + ">())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_Int.RootReadJson<" + utf16 + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + jsonReader + " state, ref int value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("if (state.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Line("    " + exception + ".ThrowFormatException(\"JSON input does not contain a value.\");");
        w.Line(serializer + ".LuminRoots_Int.RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Close();
        w.Close();

        return w.Complete(json: false);
    }
}
