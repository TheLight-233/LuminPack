using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed class TimeZoneInfoParser : LuminPackParser<TimeZoneInfo>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref TimeZoneInfo? value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            writer.Advance(1);
            return;
        }
        
        int offset = writer.WriteString(value.ToSerializedString()) + writer.StringRecordLength();
        
        writer.Advance(offset);
        
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref TimeZoneInfo? value)
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
        
        if (string.IsNullOrEmpty(source))
        {
            value = null;
            return;
        }

        value = TimeZoneInfo.FromSerializedString(source);
        
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref TimeZoneInfo? value)
    {
        var str = value?.ToSerializedString();
        evaluator += evaluator.GetStringLength(ref str);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref TimeZoneInfo? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteString(value.ToSerializedString());
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref TimeZoneInfo? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        var source = reader.ReadString();
        value = TimeZoneInfo.FromSerializedString(source);
    }
}
