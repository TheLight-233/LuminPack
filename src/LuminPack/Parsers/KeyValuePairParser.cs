using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Interface;

namespace LuminPack.Parsers;

[Preserve]
public static class KeyValuePairParser
{
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

#if DEBUG
        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
#endif
        
        value.Deconstruct(out var k, out var v);
        keyFormatter!.Serialize(ref writer, ref k);
        valueFormatter!.Serialize(ref writer, ref v);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deserialize<TKey, TValue>(ILuminPackableParser<TKey>? keyFormatter, ILuminPackableParser<TValue>? valueFormatter, ref LuminPackReader reader, out TKey? key, out TValue? value)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<KeyValuePair<TKey, TValue>>())
        {
            reader.DangerousReadUnmanaged(out KeyValuePair<TKey, TValue> kvp);
            key = kvp.Key;
            value = kvp.Value;
            
            reader.Advance(Unsafe.SizeOf<KeyValuePair<TKey, TValue>>());
            
            return;
        }

#if DEBUG
        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
#endif
        
        key = default;
        value = default;
        keyFormatter!.Deserialize(ref reader, ref key);
        valueFormatter!.Deserialize(ref reader, ref value);
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

#if DEBUG
        if (keyFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TKey));
        
        if (valueFormatter is null)
            LuminPackExceptionHelper.ThrowUnSupportedDataType(typeof(TValue));
#endif
        
        
        keyFormatter!.CalculateOffset(ref evaluator, ref key);
        valueFormatter!.CalculateOffset(ref evaluator, ref value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SerializeJson<TKey, TValue>(LuminPackParser<TKey>? keyFormatter, LuminPackParser<TValue>? valueFormatter, ref LuminPackJsonWriter writer, ref TKey key, ref TValue value)
    {
        writer.SetFirstElement(true);
        keyFormatter!.SerializeJson(ref writer, ref key!);
        writer.WriteByteRaw((byte)',');
        writer.SetFirstElement(true);
        valueFormatter!.SerializeJson(ref writer, ref value!);
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DeserializeJson<TKey, TValue>(LuminPackParser<TKey>? keyFormatter, LuminPackParser<TValue>? valueFormatter, ref LuminPackJsonReader reader, ref TKey key, ref TValue value)
    {
        keyFormatter!.DeserializeJson(ref reader, ref key!);
        valueFormatter!.DeserializeJson(ref reader, ref value!);
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        writer.WriteArrayStart();
        
        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;
        
        var k = value.Key;
        var v = value.Value;
        
        KeyValuePairParser.SerializeJson(keyParser, valueParser, ref writer, ref k, ref v);
        
        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref KeyValuePair<TKey?, TValue?> value)
    {
        reader.TryConsumeArrayStart();
        
        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;
        
        TKey? k = default;
        TValue? v = default;
        
        KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
        
        value = new KeyValuePair<TKey?, TValue?>(k, v);
    }
}