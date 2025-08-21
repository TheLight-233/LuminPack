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
        var length = writer.GetStringLength(value?.OriginalString);
        
        writer.WriteString(value?.OriginalString, length);
        
        var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;
        
        writer.Advance(length + symbol);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Uri? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        var str = reader.ReadString(length);

        var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;
        
        reader.Advance(length + symbol);
        
        if (str is null)
        {
            value = null;
        }
        else
        {
            value = new Uri(str, UriKind.RelativeOrAbsolute);
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Uri? value)
    {
        var v = value?.OriginalString ?? string.Empty;
        evaluator += evaluator.GetStringLength(ref v);
    }
}