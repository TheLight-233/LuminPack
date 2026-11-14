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

    /// <summary>Captures the compiled key and value operations.</summary>
    /// <param name="writer">The generated source writer.</param>
    /// <param name="keyOwner">The key's existing JSON formatter.</param>
    /// <param name="valueOwner">The value's existing JSON formatter.</param>
    /// <param name="keyType">The emitted key type.</param>
    /// <param name="valueType">The emitted value type.</param>
    /// <param name="integer">Whether the runtime exposes the scalar entry writer.</param>
    internal DictionaryJsonPairWriter(CodeWriter writer, string keyOwner, string valueOwner, string keyType, string valueType, bool integer)
    {
        _writer = writer;
        _keyOwner = keyOwner;
        _valueOwner = valueOwner;
        _keyType = keyType;
        _valueType = valueType;
        _integer = integer && GenericFormatterReuse.Real(keyType) == "int" &&
            (GenericFormatterReuse.Real(valueType) is "int" or "long");
    }

    /// <summary>Initializes the separator state used by the individual formatter calls.</summary>
    internal void Begin()
    {
        if (!_integer)
            _writer.Line("bool isFirst = true;");
    }

    /// <summary>Writes a pair already captured by the original enumeration step.</summary>
    /// <param name="key">The key expression, evaluated as the original formatter takes it.</param>
    /// <param name="value">The value expression, evaluated as the original formatter takes it.</param>
    internal void Write(string key, string value)
    {
        var w = _writer;
        if (_integer)
        {
            string memory = w.A("Unsafe");
            w.Line("var key = " + key + ";");
            w.Line("var itemValue = " + value + ";");
            w.Line("writer.WriteIntegerEntry<TLuminProfile>(" + Integer("key", _keyType) + ", " + Integer("itemValue", _valueType) + ");");
            return;

            string Integer(string local, string type) => GenericFormatterReuse.IsUnmanagedPlaceholder(type)
                ? memory + ".As<" + type + ", " + GenericFormatterReuse.Real(type) + ">(ref " + local + ")" : local;
        }

        w.Line("if (!isFirst)");
        w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("else");
        w.Line("    isFirst = false;");
        w.Line("writer.SetFirstElement(true);");
        w.Line("writer.WriteArrayStart<TLuminProfile>();");
        w.Line("var key = " + key + ";");
        w.Line("writer.SetFirstElement(true);");
        w.Line(_keyOwner + ".Write<TLuminProfile>(ref writer, key);");
        w.Line("writer.WriteByteRaw<TLuminProfile>((byte)',');");
        w.Line("var itemValue = " + value + ";");
        w.Line("writer.SetFirstElement(true);");
        w.Line(_valueOwner + ".Write<TLuminProfile>(ref writer, itemValue);");
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
    }
}
