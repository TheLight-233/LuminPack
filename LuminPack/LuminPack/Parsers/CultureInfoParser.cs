using System.Globalization;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed class CultureInfoParser : LuminPackParser<CultureInfo>
{
    // treat as string
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref CultureInfo? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullStringHeader(ref index, out var offset);
                
            writer.Advance(offset);
            
            return;
        }
        
        var length = writer.GetStringLength(value.Name);
        
        writer.WriteString(value.Name, length);
        
        var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;
        
        writer.Advance(length + symbol);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref CultureInfo? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        if (reader.Option.StringRecording is LuminPackStringRecording.Length)
            reader.Advance(4);
        
        var str = reader.ReadString(index, length) ?? string.Empty;
        if (str == string.Empty)
        {
            value = null;
        }
        else
        {
            value = CultureInfo.GetCultureInfo(str);
        }
        
        var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 0;
        
        reader.Advance(length + symbol);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref CultureInfo? value)
    {
        var str = value is null ? string.Empty : value.Name;
        evaluator += evaluator.GetStringLength(ref str);
    }
}