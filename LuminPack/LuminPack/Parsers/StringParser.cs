
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed class StringParser : LuminPackParser<string?>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref string? value)
    {
        
        int offset = writer.WriteString(value) + writer.StringRecordLength();
        
        writer.Advance(offset);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref string? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        value  = reader.ReadString(length);
        
        var symbol = reader.StringRecordLength();
        
        reader.Advance(length + symbol);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref string? value)
    {
        evaluator += evaluator.GetStringLength(ref value);
    }
}