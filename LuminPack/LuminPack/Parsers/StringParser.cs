
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class StringParser : LuminPackParser<string?>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref string? value)
    {
        
        int offset = writer.WriteString(value) + writer.StringRecordLength();
        
        writer.Advance(offset);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref string? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        value  = reader.ReadString(length);
        
        var symbol = reader.StringRecordLength();
        
        reader.Advance(length + symbol);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref string? value)
    {
        evaluator += evaluator.GetStringLength(ref value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref string? value)
    {
        writer.WriteString(value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref string? value)
    {
        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.Null)
        {
            value = null;
            reader.Read(); // 消耗null标记
            return;
        }

        if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
            throw new InvalidOperationException("Expected string token");

        value = reader.ReadString();
    }
}