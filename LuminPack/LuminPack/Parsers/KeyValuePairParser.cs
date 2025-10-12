
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Interface;

namespace LuminPack.Parsers;

[Preserve]
public static class KeyValuePairParser
{
    // for Dictionary serialization

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Serialize<TKey, TValue>(ILuminPackableParser<TKey>? keyFormatter, ILuminPackableParser<TValue>? valueFormatter, ref LuminPackWriter writer, KeyValuePair<TKey?, TValue?> value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            
            writer.Advance(Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>());
            
            return;
        }

        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
        
        value.Deconstruct(out var k, out var v);
        keyFormatter.Serialize(ref writer, ref k);
        valueFormatter.Serialize(ref writer, ref v);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deserialize<TKey, TValue>(ILuminPackableParser<TKey>? keyFormatter, ILuminPackableParser<TValue>? valueFormatter, ref LuminPackReader reader, out TKey? key, out TValue? value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            reader.DangerousReadUnmanaged(out KeyValuePair<TKey?, TValue?> kvp);
            key = kvp.Key;
            value = kvp.Value;
            
            reader.Advance(Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>());
            
            return;
        }

        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
        
        key = default;
        value = default;
        keyFormatter.Deserialize(ref reader, ref key);
        valueFormatter.Deserialize(ref reader, ref value);
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CalculateOffset<TKey, TValue>(ILuminPackEvaluator<TKey>? keyFormatter, ILuminPackEvaluator<TValue>? valueFormatter, ref LuminPackEvaluator evaluator, scoped ref TKey? key, scoped ref TValue? value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            evaluator += Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>();
            
            return;
        }

        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
        
        keyFormatter.CalculateOffset(ref evaluator, ref key);
        valueFormatter.CalculateOffset(ref evaluator, ref value);
    }
}

[Preserve]
public sealed class KeyValuePairParser<TKey, TValue> : LuminPackParser<KeyValuePair<TKey?, TValue?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            writer.DangerousWriteUnmanaged(value);
            
            writer.Advance(Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>());
            
            return;
        }

        writer.WriteValue(value.Key);
        writer.WriteValue(value.Value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            reader.DangerousReadUnmanaged(out value);
            
            reader.Advance(Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>());
            
            return;
        }

        value = new KeyValuePair<TKey?, TValue?>(
            reader.ReadValue<TKey>(),
            reader.ReadValue<TValue>()
        );
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey?, TValue?>>())
        {
            evaluator += Unsafe.SizeOf<KeyValuePair<TKey?, TValue?>>();
            
            return;
        }
        
        var keyEvaluator = evaluator.GetEvaluator<TKey>();
        var valueEvaluator = evaluator.GetEvaluator<TValue>();
        var key = value.Key;
        var valueKey = value.Value;

        keyEvaluator.CalculateOffset(ref evaluator, ref key);
        valueEvaluator.CalculateOffset(ref evaluator, ref valueKey);
    }
}