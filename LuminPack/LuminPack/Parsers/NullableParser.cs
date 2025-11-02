using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class NullableParser<T> : LuminPackParser<T?> 
    where T : struct
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (!value.HasValue)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            writer.DangerousWriteUnmanaged(ref index, value.Value);
            writer.Advance(Unsafe.SizeOf<T>());
            return;
        }
        
        T valRef = value.Value;
        writer.WriteValue(valRef);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (reader.PeekIsNullObject(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            reader.DangerousReadUnmanaged(ref index, out T val);
            value = val;
            reader.Advance(Unsafe.SizeOf<T>());
            return;
        }
        
        T valRef = reader.ReadValue<T>();
        value = valRef;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value)
    {
        if (!value.HasValue)
        {
            evaluator += 1;
            return;
        }
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            evaluator += Unsafe.SizeOf<T>();
            return;
        }
        
        
        T val = value.Value;
        var eva = evaluator.GetEvaluator<T>();
        eva.CalculateOffset(ref evaluator, ref val);
    }
}