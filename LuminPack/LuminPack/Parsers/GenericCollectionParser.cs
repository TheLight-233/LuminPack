using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class GenericCollectionFormatter<TCollection, TElement> : LuminPackParser<TCollection?>
    where TCollection : ICollection<TElement?>, new()
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref TCollection? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var parser = writer.GetParser<TElement?>();

        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref TCollection? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = default;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        var parser = reader.GetParser<TElement?>();

        var collection = new TCollection();
        for (int i = 0; i < length; i++)
        {
            TElement? v = default;
            parser.Deserialize(ref reader, ref v);
            collection.Add(v);
        }

        value = collection;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref TCollection? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }

        var eval = evaluator.GetEvaluator<TElement?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public abstract class GenericSetParserBase<TSet, TElement> : LuminPackParser<TSet?>
    where TSet : ISet<TElement?>
{
    [Preserve]
    protected abstract TSet CreateSet();

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref TSet? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var parser = writer.GetParser<TElement?>();

        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref TSet? value)
    {
        
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = default;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        var parser = reader.GetParser<TElement?>();

        var collection = CreateSet();
        for (int i = 0; i < length; i++)
        {
            TElement? v = default;
            parser.Deserialize(ref reader, ref v);
            collection.Add(v);
        }

        value = collection;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref TSet? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }

        var eval = evaluator.GetEvaluator<TElement?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public sealed class GenericSetParser<TSet, TElement> : GenericSetParserBase<TSet, TElement>
    where TSet : ISet<TElement?>, new()
{
    protected override TSet CreateSet()
    {
        return new();
    }
}


[Preserve]
public abstract class GenericDictionaryParserBase<TDictionary, TKey, TValue> : LuminPackParser<TDictionary?>
    where TKey : notnull
    where TDictionary : IDictionary<TKey, TValue?>
{
    [Preserve]
    protected abstract TDictionary CreateDictionary();

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref TDictionary? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var keyParser = writer.GetParser<TKey>();
        var valueParser = writer.GetParser<TValue>();

        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref TDictionary? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = default;
            
            reader.Advance(4);
            
            return;
        }

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();

        var dict = CreateDictionary();
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            dict.Add(k!, v);
        }

        value = dict;
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref TDictionary? value)
    {
        if (value is null)
        {
            evaluator += 4;
                
            return;
        }
            
        var keyFormatter = evaluator.GetEvaluator<TKey>();
        var valueFormatter = evaluator.GetEvaluator<TValue>();
            
        evaluator += 4;
        foreach (var item in value)
        {
            var k = item.Key;
            var v = item.Value;
            KeyValuePairParser.CalculateOffset(keyFormatter, valueFormatter, ref evaluator, ref k, ref v);
        }
    }
}

[Preserve]
public sealed class GenericDictionaryParser<TDictionary, TKey, TValue> : GenericDictionaryParserBase<TDictionary, TKey, TValue>
    where TKey : notnull
    where TDictionary : IDictionary<TKey, TValue?>, new()
{
    [Preserve]
    protected override TDictionary CreateDictionary()
    {
        return new();
    }
}