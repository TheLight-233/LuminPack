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
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            writer.DangerousWriteUnmanaged(ref index, value);
            
            index += Unsafe.SizeOf<T>();
            
            return;
        }
        
        if (!value.HasValue)
        {
            writer.WriteNullObjectHeader(ref index);
            index += 1;
            return;
        }
        

        writer.WriteValue(value.Value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            reader.DangerousReadUnmanaged(ref index, out value);
        }
        value = reader.ReadValue<T>();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            evaluator += Unsafe.SizeOf<T>();
            
            return;
        }
        
        var eva = evaluator.GetEvaluator<T?>();

        eva.CalculateOffset(ref evaluator, ref value);
    }
}