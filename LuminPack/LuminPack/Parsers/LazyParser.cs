using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class LazyParser<T> : LuminPackParser<Lazy<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Lazy<T?>? value)
    {
        
        if (value is null)
        {
            ref var index = ref writer.GetCurrentSpanOffset();
            
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }
        
        writer.WriteValue(value.Value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Lazy<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }

        var v = reader.ReadValue<T>();
        value = new Lazy<T?>(v);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Lazy<T?>? value)
    {
        if (value is null)
        {
            evaluator += 1;
            
            return;
        }
        
        var v = value.Value;
        evaluator.CalculateValue(in v);
    }
}