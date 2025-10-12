#if NET8_0_OR_GREATER

using System.Collections.Frozen;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Parsers;


[Preserve]
public sealed class FrozenDictionaryParser<TKey, TValue> : LuminPackParser<FrozenDictionary<TKey, TValue?>>
    where TKey : notnull
{
    readonly IEqualityComparer<TKey>? equalityComparer;

    public FrozenDictionaryParser() : this(null)
    {

    }

    public FrozenDictionaryParser(IEqualityComparer<TKey>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref FrozenDictionary<TKey, TValue?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
            
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
                
            writer.Advance(4);
                
            return;
        }

        var keyFormatter = writer.GetParser<TKey>();
        var valueFormatter = writer.GetParser<TValue>();

        writer.WriteCollectionHeader(ref index, value.Count);
        writer.Advance(4);
            
        var count = value.Count;
        var i = 0;
        foreach (var item in value)
        {
            i++;
            KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
        }
            
        if (i != count) 
            LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref FrozenDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        var dict = new Dictionary<TKey, TValue?>(length, equalityComparer);

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();
        for (var i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            dict.Add(k!, v);
        }
        value = dict.ToFrozenDictionary(equalityComparer);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref FrozenDictionary<TKey, TValue?>? value)
    {
        if (value is null)
        {
            evaluator += 4;
                
            return;
        }
            
        var keyFormatter = evaluator.GetEvaluator<TKey>();
        var valueFormatter = evaluator.GetEvaluator<TValue>();
            
        evaluator += 4;
            
        var count = value.Count;
        var i = 0;
            
        foreach (var item in value)
        {
            i++;
            var k = item.Key;
            var v = item.Value;
            KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
        }
            
        if (i != count) 
            LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();
    }
    
}

public sealed class FrozenSetParser<T> : LuminPackParser<FrozenSet<T?>>
{
    readonly IEqualityComparer<T?>? equalityComparer;

    public FrozenSetParser() : this(null)
    {
    }

    public FrozenSetParser(IEqualityComparer<T?>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref FrozenSet<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
            
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
                
            writer.Advance(4);
                
            return;
        }

        var parser = writer.GetParser<T?>();
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref FrozenSet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
            
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
                
            reader.Advance(4);
                
            return;
        }

        reader.Advance(4);
            
        var set = new HashSet<T>(length, equalityComparer);

        var parser = reader.GetParser<T?>();
        for (int i = 0; i < length; i++)
        {
            T? v = default;
            parser.Deserialize(ref reader, ref v);
            set.Add(v!);
        }

        value = set.ToFrozenSet(equalityComparer)!;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref FrozenSet<T?>? value)
    {
        if (value is null)
        {
            evaluator += 4;
                
            return;
        }
            
        var eval = evaluator.GetEvaluator<T>();
            
        evaluator += 4;

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}
#endif