using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the object form of a JSON dictionary with string keys.</summary>
internal static class DictionaryJsonPropertyReader
{
    /// <summary>Consumes the opening delimiter and selects the first property or the empty object's terminator.</summary>
    /// <param name="w">The generated source writer.</param>
    internal static void SelectFirst(CodeWriter w)
    {
        w.Line("reader.TryConsumeObjectStart<TLuminProfile>();");
        ReadNext(w);
    }

    /// <summary>Reads each property through the existing key and value owners before storing it.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="keyOwner">The JSON string owner.</param>
    /// <param name="valueOwner">The JSON value owner.</param>
    /// <param name="valueType">The emitted type of one value.</param>
    /// <param name="jsonReader">The emitted name of the JSON reader.</param>
    /// <param name="store">The statements that store the decoded key and value.</param>
    /// <param name="seedKey">An optional expression providing a string to reuse.</param>
    /// <param name="seedValue">An optional statement seeding the value from the existing destination.</param>
    /// <param name="beforeStore">An optional emitter preparing storage for the decoded entry.</param>
    /// <param name="firstPropertySelected">Whether the first property token has already been selected.</param>
    internal static void Emit(CodeWriter w, string keyOwner, string valueOwner, string valueType, string jsonReader,
        string store, string? seedKey = null, string? seedValue = null, System.Action<CodeWriter>? beforeStore = null,
        bool firstPropertySelected = false)
    {
        const string exception = "global::LuminPack.Code.LuminPackExceptionHelper";
        string tokens = jsonReader + ".JsonTokenType.";
        if (!firstPropertySelected)
            SelectFirst(w);
        w.Line("while (reader.CurrentTokenType != " + tokens + "ObjectEnd)");
        w.Open();
        w.Line("if (reader.CurrentTokenType != " + tokens + "String)");
        w.Indented(() => w.Line(exception + ".ThrowFormatException(\"Expected a JSON dictionary property name.\");"));
        w.Line("string key = " + (seedKey ?? "default!") + ";");
        w.Line(keyOwner + ".Read<TLuminProfile>(ref reader, ref key);");
        w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType is " + tokens + "ObjectEnd or " + tokens + "ArrayEnd)");
        w.Indented(() => w.Line(exception + ".ThrowFormatException(\"Expected a value for the JSON dictionary property.\");"));
        w.Line(valueType + " itemValue = default!;");
        if (seedValue != null)
            w.Line(seedValue);
        w.Line(valueOwner + ".Read<TLuminProfile>(ref reader, ref itemValue);");
        beforeStore?.Invoke(w);
        w.Line(store);
        ReadNext(w);
        w.Close();
    }

    /// <summary>Selects the next token and rejects a missing object terminator.</summary>
    /// <param name="w">The generated source writer.</param>
    private static void ReadNext(CodeWriter w)
    {
        w.Line("if (!reader.Read<TLuminProfile>())");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"Expected the end of the JSON dictionary object.\");"));
    }
}
