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
        var length = writer.GetStringLength(value?.ToSerializedString());
        
        writer.WriteString(value?.ToSerializedString(), length);
        
        var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;
        
        writer.Advance(length + symbol);
        
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref TimeZoneInfo? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        if (reader.Option.StringRecording is LuminPackStringRecording.Length)
            reader.Advance(4);
        
        var source  = reader.ReadString(length) ?? string.Empty;
        
        if (source == string.Empty)
        {
            value = null;
            return;
        }

        value = TimeZoneInfo.FromSerializedString(source);
        
        var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 0;
        
        reader.Advance(length + symbol);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref TimeZoneInfo? value)
    {
        var str = value?.ToSerializedString();
        evaluator += evaluator.GetStringLength(ref str);
    }
}