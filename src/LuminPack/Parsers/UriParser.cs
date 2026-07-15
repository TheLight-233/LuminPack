using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed class UriParser : LuminPackParser<Uri?>
{
    
    // treat as a string
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Uri? value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            writer.Advance(1);
            return;
        }
        
        int offset = writer.WriteString(value.OriginalString) + writer.StringRecordLength();
        
        writer.Advance(offset);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Uri? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (reader.PeekIsNullObject(ref index))
        {
            reader.Advance(1);
            value = null;
            return;
        }
        
        reader.ReadStringLength(ref index, out var length);
        
        var source = reader.ReadString(length);
        
        var symbol = reader.StringRecordLength();
        
        reader.Advance(length + symbol);
        
        if (source is null)
        {
            value = null;
        }
        else
        {
            value = new Uri(source, UriKind.RelativeOrAbsolute);
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Uri? value)
    {
        var v = value?.OriginalString ?? string.Empty;
        evaluator += evaluator.GetStringLength(ref v);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Uri? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteString(value.OriginalString);
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Uri? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        var str = reader.ReadString();
        value = new Uri(str, UriKind.RelativeOrAbsolute);
    }
}
