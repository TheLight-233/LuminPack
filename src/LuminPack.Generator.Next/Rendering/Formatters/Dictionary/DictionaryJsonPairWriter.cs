using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits JSON pair framing or the matching scalar entry operation.</summary>
internal sealed class DictionaryJsonPairWriter
{
    private readonly CodeWriter _writer;
    private readonly string _keyOwner;
    private readonly string _valueOwner;
    private readonly string _keyType;
    private readonly string _valueType;
    private readonly bool _integer;
    private readonly bool _utf8Only;
    private readonly bool _scalarSeparators;
    private readonly bool _stringProperties;

    /// <summary>Captures the compiled key and value operations.</summary>
    /// <param name="writer">The generated source writer.</param>
    /// <param name="keyOwner">The key's existing JSON formatter.</param>
    /// <param name="valueOwner">The value's existing JSON formatter.</param>
    /// <param name="keyType">The emitted key type.</param>
    /// <param name="valueType">The emitted value type.</param>
    /// <param name="integer">Whether the runtime exposes the scalar entry writer.</param>
    /// <param name="utf8Only">Whether UTF-16 retains the original formatting callback boundaries.</param>
    /// <param name="capturedPair">Whether both members come from an already captured dictionary entry.</param>
    /// <param name="stringProperties">Whether each string key names an object property.</param>
    internal DictionaryJsonPairWriter(CodeWriter writer, string keyOwner, string valueOwner, string keyType, string valueType, bool integer,
        bool utf8Only = false, bool capturedPair = false, bool stringProperties = false)
    {
        _writer = writer;
        _keyOwner = keyOwner;
        _valueOwner = valueOwner;
        _keyType = keyType;
        _valueType = valueType;
        _stringProperties = stringProperties;
        _integer = integer && GenericFormatterReuse.Real(keyType) == "int" &&
            (GenericFormatterReuse.Real(valueType) is "int" or "long");
        _utf8Only = utf8Only;
        _scalarSeparators = capturedPair && !_integer && UsesScalarSeparator(keyType) && UsesScalarSeparator(valueType);
    }

    // These concrete owners write exactly one value and always handle its leading comma, including null strings.
    /// <summary>Handles uses scalar separator for <c>DictionaryJsonPairWriter</c>.</summary>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <returns>Whether the input satisfies the uses scalar separator check.</returns>
    private static bool UsesScalarSeparator(string type) => type is "string" or "int" or "uint" or "long" or "ulong";

    /// <summary>Initializes the separator state used by the individual formatter calls.</summary>
    internal void Begin()
    {
        if (!_stringProperties && !_scalarSeparators && (!_integer || _utf8Only))
            _writer.Line("bool isFirst = true;");
    }

    /// <summary>Writes a pair already captured by the original enumeration step.</summary>
    /// <param name="key">The key expression, evaluated as the original formatter takes it.</param>
    /// <param name="value">The value expression, evaluated as the original formatter takes it.</param>
    internal void Write(string key, string value)
    {
        var w = _writer;
        if (_stringProperties)
        {
            w.Line("writer.WritePropertyName<TLuminProfile>(" + key + ");");
            w.Line("var propertyValue = " + value + ";");
            w.Line(_valueOwner + ".Write<TLuminProfile>(ref writer, propertyValue);");
            if (!UsesScalarSeparator(_valueType))
                w.Line("writer.SetFirstElement(false);");
            return;
        }
        if (_integer)
        {
            string memory = w.A("Unsafe");
            if (_utf8Only)
            {
                w.Line("if (default(TLuminProfile).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
                w.Open();
            }
            string pairKey = _utf8Only ? "integerKey" : "key";
            string pairValue = _utf8Only ? "integerValue" : "itemValue";
            w.Line("var " + pairKey + " = " + key + ";");
            w.Line("var " + pairValue + " = " + value + ";");
            w.Line("writer.WriteIntegerEntry<TLuminProfile>(" + Integer(pairKey, _keyType) + ", " + Integer(pairValue, _valueType) + ");");
            if (!_utf8Only)
                return;
            w.Line("continue;");
            w.Close();

            string Integer(string local, string type) => GenericFormatterReuse.IsUnmanagedPlaceholder(type)
                ? memory + ".As<" + type + ", " + GenericFormatterReuse.Real(type) + ">(ref " + local + ")" : local;
        }

        if (!_scalarSeparators)
        {
            w.Line("if (!isFirst)");
            w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
            w.Line("else");
            w.Line("    isFirst = false;");
            w.Line("writer.SetFirstElement(true);");
        }
        w.Line("writer.WriteArrayStart<TLuminProfile>();");
        w.Line("var key = " + key + ";");
        if (!_scalarSeparators)
            w.Line("writer.SetFirstElement(true);");
        w.Line(_keyOwner + ".Write<TLuminProfile>(ref writer, key);");
        if (!_scalarSeparators)
            w.Line("writer.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("var itemValue = " + value + ";");
        if (!_scalarSeparators)
            w.Line("writer.SetFirstElement(true);");
        w.Line(_valueOwner + ".Write<TLuminProfile>(ref writer, itemValue);");
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
    }
}
