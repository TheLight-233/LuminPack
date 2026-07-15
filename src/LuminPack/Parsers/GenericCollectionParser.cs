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
        writer.CheckBuffer();
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

        var parser = LuminPackParseProvider.Cache<TElement?>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref TCollection? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count > 0)
        {
            var parser = LuminPackParseProvider.Cache<TElement>.Parser!;

            bool isFirst = true;
            foreach (var item in value)
            {
                if (!isFirst) writer.WriteByteRaw((byte)',');
                else isFirst = false;
                writer.SetFirstElement(true);
                var temp = item;
                parser.SerializeJson(ref writer, ref temp);
            }
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref TCollection? value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        if (value is null)
        {
            value = new TCollection();
        }
        else
        {
            value.Clear();
        }

        var parser = LuminPackParseProvider.Cache<TElement>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            TElement? item = default;
            parser.DeserializeJson(ref reader, ref item);
            value.Add(item);
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
        writer.CheckBuffer();
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

        var parser = LuminPackParseProvider.Cache<TElement?>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref TSet? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count > 0)
        {
            var parser = LuminPackParseProvider.Cache<TElement>.Parser!;

            bool isFirst = true;
            foreach (var item in value)
            {
                if (!isFirst) writer.WriteByteRaw((byte)',');
                else isFirst = false;
                writer.SetFirstElement(true);
                var temp = item;
                parser.SerializeJson(ref writer, ref temp);
            }
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref TSet? value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        var collection = CreateSet();

        var parser = LuminPackParseProvider.Cache<TElement>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            TElement? item = default;
            parser.DeserializeJson(ref reader, ref item);
            collection.Add(item);
        }

        value = collection;
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
        writer.CheckBuffer();
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

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref TDictionary? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        // 数组格式: [[k1,v1], [k2,v2], [k3,v3]]
        writer.WriteArrayStart();

        if (value.Count > 0)
        {
            var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
            var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

            foreach (var item in value)
            {
                writer.WriteArrayStart();
                
                var k = item.Key;
                keyParser.SerializeJson(ref writer, ref k);
                
                var v = item.Value;
                valueParser.SerializeJson(ref writer, ref v);
                
                writer.WriteArrayEnd();
            }
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref TDictionary? value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        var dict = CreateDictionary();

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        // 数组格式: [[k1,v1], [k2,v2], [k3,v3]]
        reader.TryConsumeArrayStart();

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ArrayStart)
                continue;

            // 读取 [key, value] 元素
            reader.TryConsumeArrayStart();

            TKey key = default!;
            TValue? val = default;

            // 读取key (第一个元素)
            if (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ArrayEnd)
            {
                keyParser.DeserializeJson(ref reader, ref key);

                // 读取value (第二个元素)
                if (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ArrayEnd)
                {
                    valueParser.DeserializeJson(ref reader, ref val);
                }
            }

            // 跳到数组结束
            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                    continue;
            }

            dict[key!] = val;
        }

        value = dict;
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