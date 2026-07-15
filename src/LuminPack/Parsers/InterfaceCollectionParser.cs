using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Internal;
using LuminPack.Utility;

namespace LuminPack.Parsers;

using static InterfaceCollectionParserUtils;

public static class InterfaceCollectionParserUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySerializeOptimized<TCollection, TElement>(ref LuminPackWriter writer, [NotNullWhen(false)] scoped ref TCollection? value)
        where TCollection : IEnumerable<TElement>
    {
        ref var index = ref writer.GetCurrentSpanOffset();
            
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
                
            writer.Advance(4);
                
            return true;
        }

        // optimize for list or array

        if (value is TElement?[] array)
        {
            writer.WriteArray(array);
            return true;
        }


        if (value is List<TElement?> list)
        {
            writer.WriteSpan(LuminPackMarshal.GetListSpan(list, list.Count));
            return true;
        }

        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCalculateOptimized<TCollection, TElement>(ref LuminPackEvaluator evaluator, [NotNullWhen(false)] scoped ref TCollection? value)
        where TCollection : IEnumerable<TElement>
    {
            
        if (value is null)
        {
            evaluator += 4;
                
            return true;
        }

        // optimize for list or array

        if (value is TElement?[] array)
        {
            evaluator.CalculateArray(ref array);
            return true;
        }


        if (value is List<TElement?> list)
        {
            var span = LuminPackMarshal.GetListSpan(list, list.Count);
            evaluator.CalculateSpan(ref span);
            return true;
        }

        return false;
    }

    public static void SerializeCollection<TCollection, TElement>(ref LuminPackWriter writer, scoped ref TCollection? value)
        where TCollection : ICollection<TElement>
    {
        if (TrySerializeOptimized<TCollection, TElement>(ref writer, ref value)) return;

        ref var index = ref writer.GetCurrentSpanOffset();
            
        var parser = LuminPackParseProvider.Cache<TElement>.Parser!;
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v!);
        }
        writer.CheckBuffer();
    }
    
    public static void CalculateCollection<TCollection, TElement>(ref LuminPackEvaluator evaluator, scoped ref TCollection? value)
        where TCollection : ICollection<TElement>
    {
        if (TryCalculateOptimized<TCollection, TElement>(ref evaluator, ref value)) return;
        
        var eval = evaluator.GetEvaluator<TElement>();

        evaluator += 4;
            
        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v!);
        }
    }

    public static void SerializeReadOnlyCollection<TCollection, TElement>(ref LuminPackWriter writer, scoped ref TCollection? value)
        where TCollection : IReadOnlyCollection<TElement>
    {
        if (TrySerializeOptimized<TCollection, TElement>(ref writer, ref value)) return;

        ref var index = ref writer.GetCurrentSpanOffset();
            
        var parser = LuminPackParseProvider.Cache<TElement>.Parser!;
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v!);
        }
        writer.CheckBuffer();
    }
    
    public static void CalculateReadOnlyCollection<TCollection, TElement>(ref LuminPackEvaluator evaluator, scoped ref TCollection? value)
        where TCollection : IReadOnlyCollection<TElement>
    {
        if (TryCalculateOptimized<TCollection, TElement>(ref evaluator, ref value)) return;
            
        var eval = evaluator.GetEvaluator<TElement>();

        evaluator += 4;
            
        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v!);
        }
    }

    public static List<T?>? ReadList<T>(ref LuminPackReader reader)
    {
        var parser = LuminPackParseProvider.Cache<List<T?>>.Parser!;
        List<T?>? v = default;
        parser.Deserialize(ref reader, ref v);
        return v;
    }
}

[Preserve]
public sealed class InterfaceEnumerableParser<T> : LuminPackParser<IEnumerable<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IEnumerable<T?>? value)
    {
        if (TrySerializeOptimized<IEnumerable<T?>, T?>(ref writer, ref value)) return;

        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value.TryGetNonEnumeratedCountEx(out var count))
        {
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            
            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
            writer.CheckBuffer();
        }
        else
        {
            // write to tempbuffer(because we don't know length so can't write header)
            var tempBuffer = LuminBufferWriterPool.Rent();
            try
            {
                var tempWriter = new LuminPackWriter(writer.OptionState);
                tempWriter.SetWriteBuffer(tempBuffer);

                count = 0;
                var parser = LuminPackParseProvider.Cache<T?>.Parser!;
                foreach (var item in value)
                {
                    count++;
                    var v = item;
                    parser.Serialize(ref tempWriter, ref v);
                }

                tempWriter.Flush();

                // write to parameter writer.
                writer.WriteCollectionHeader(ref index, count);
                
                writer.Advance(4);
                
                tempBuffer.WriteToAndReset(ref writer);
                writer.CheckBuffer();
            }
            finally
            {
                LuminBufferWriterPool.Return(tempBuffer);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IEnumerable<T?>? value)
    {
        value = reader.ReadArray<T?>();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IEnumerable<T?>? value)
    {
        if (TryCalculateOptimized<IEnumerable<T?>, T?>(ref evaluator, ref value)) return;

        var eval = evaluator.GetEvaluator<T?>();

        evaluator += 4;

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IEnumerable<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IEnumerable<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var list = new List<T?>();

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            list.Add(item);
        }

        value = list;
    }

}

[Preserve]
public sealed class InterfaceCollectionParser<T> : LuminPackParser<ICollection<T?>>
{

    static InterfaceCollectionParser()
    {
        if (!LuminPackParseProvider.IsRegistered<List<T?>>())
        {
            LuminPackParseProvider.RegisterParsers(new ListParser<T>());
        }
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ICollection<T?>? value)
    {
        SerializeCollection<ICollection<T?>, T?>(ref writer, ref value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ICollection<T?>? value)
    {
        value = ReadList<T?>(ref reader);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ICollection<T?>? value)
    {
        CalculateCollection<ICollection<T?>, T?>(ref evaluator, ref value);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ICollection<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ICollection<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var list = new List<T?>();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            list.Add(item);
        }

        value = list;
    }

}

[Preserve]
public sealed class InterfaceReadOnlyCollectionParser<T> : LuminPackParser<IReadOnlyCollection<T?>>
{
    
    static InterfaceReadOnlyCollectionParser()
    {
        if (!LuminPackParseProvider.IsRegistered<List<T?>>())
        {
            LuminPackParseProvider.RegisterParsers(new ListParser<T>());
        }
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IReadOnlyCollection<T?>? value)
    {
        SerializeReadOnlyCollection<IReadOnlyCollection<T?>, T?>(ref writer, ref value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IReadOnlyCollection<T?>? value)
    {
        value = ReadList<T?>(ref reader);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IReadOnlyCollection<T?>? value)
    {
        CalculateReadOnlyCollection<IReadOnlyCollection<T?>, T?>(ref evaluator, ref value);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IReadOnlyCollection<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IReadOnlyCollection<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var list = new List<T?>();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            list.Add(item);
        }

        value = list;
    }

}

[Preserve]
public sealed class InterfaceListParser<T> : LuminPackParser<IList<T?>>
{

    static InterfaceListParser()
    {
        if (!LuminPackParseProvider.IsRegistered<List<T?>>())
        {
            LuminPackParseProvider.RegisterParsers(new ListParser<T>());
        }
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IList<T?>? value)
    {
        SerializeCollection<IList<T?>, T?>(ref writer, ref value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IList<T?>? value)
    {
        value = ReadList<T?>(ref reader);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IList<T?>? value)
    {
        CalculateCollection<IList<T?>, T?>(ref evaluator, ref value);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IList<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IList<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var list = new List<T?>();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            list.Add(item);
        }

        value = list;
    }

}

[Preserve]
public sealed class InterfaceReadOnlyListParser<T> : LuminPackParser<IReadOnlyList<T?>>
{

    static InterfaceReadOnlyListParser()
    {
        if (!LuminPackParseProvider.IsRegistered<List<T?>>())
        {
            LuminPackParseProvider.RegisterParsers(new ListParser<T>());
        }
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IReadOnlyList<T?>? value)
    {
        SerializeReadOnlyCollection<IReadOnlyList<T?>, T?>(ref writer, ref value);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IReadOnlyList<T?>? value)
    {
        value = ReadList<T?>(ref reader);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IReadOnlyList<T?>? value)
    {
        CalculateReadOnlyCollection<IReadOnlyList<T?>, T?>(ref evaluator, ref value);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IReadOnlyList<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IReadOnlyList<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var list = new List<T?>();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            list.Add(item);
        }

        value = list;
    }

}

[Preserve]
public sealed class InterfaceDictionaryParser<TKey, TValue> : LuminPackParser<IDictionary<TKey, TValue?>>
    where TKey : notnull
{
    readonly IEqualityComparer<TKey>? equalityComparer;

    public InterfaceDictionaryParser()
        : this(null)
    {

    }

    public InterfaceDictionaryParser(IEqualityComparer<TKey>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IDictionary<TKey, TValue?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);
        }
        writer.CheckBuffer();
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        var dict = new Dictionary<TKey, TValue?>(equalityComparer);

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;
        
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            dict.Add(k!, v);
        }

        value = dict;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IDictionary<TKey, TValue?>? value)
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
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IDictionary<TKey, TValue?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        foreach (var item in value)
        {

            var k = item.Key;
            var v = item.Value;
            writer.WriteArrayStart();
            
            KeyValuePairParser.SerializeJson(keyParser, valueParser, ref writer, ref k, ref v);
            
            writer.WriteArrayEnd();
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IDictionary<TKey, TValue?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        value = new Dictionary<TKey, TValue?>(equalityComparer);

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                break;

            TKey? k = default;
            TValue? v = default;
            KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
            value.Add(k!, v);
        }
    }

}

[Preserve]
public sealed class InterfaceReadOnlyDictionaryParser<TKey, TValue> : LuminPackParser<IReadOnlyDictionary<TKey, TValue?>>
    where TKey : notnull
{
    readonly IEqualityComparer<TKey>? equalityComparer;

    public InterfaceReadOnlyDictionaryParser()
        : this(null)
    {

    }

    public InterfaceReadOnlyDictionaryParser(IEqualityComparer<TKey>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            KeyValuePairParser.Serialize(keyParser, valueParser, ref writer, item!);
        }
        writer.CheckBuffer();
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        var dict = new Dictionary<TKey, TValue?>(equalityComparer);

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;
        
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            dict.Add(k!, v);
        }

        value = dict;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
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
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        foreach (var item in value)
        {

            var k = item.Key;
            var v = item.Value;
            writer.WriteArrayStart();
            
            KeyValuePairParser.SerializeJson(keyParser, valueParser, ref writer, ref k, ref v);
            
            writer.WriteArrayEnd();
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IReadOnlyDictionary<TKey, TValue?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        var dict = new Dictionary<TKey, TValue?>(equalityComparer);

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                break;

            TKey? k = default;
            TValue? v = default;
            KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
            dict.Add(k!, v);
        }

        value = dict;
    }

}

[Preserve]
public sealed class InterfaceLookupParser<TKey, TElement> : LuminPackParser<ILookup<TKey, TElement>>
    where TKey : notnull
{

    readonly IEqualityComparer<TKey>? equalityComparer;

    static InterfaceLookupParser()
    {
        if (!LuminPackParseProvider.IsRegistered<IGrouping<TKey, TElement>>())
        {
            LuminPackParseProvider.RegisterParsers(new InterfaceGroupingParser<TKey, TElement>());
        }
    }
    
    public InterfaceLookupParser()
        : this(null)
    {

    }

    public InterfaceLookupParser(IEqualityComparer<TKey>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }


    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ILookup<TKey, TElement>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
            
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
                
            writer.Advance(4);
                
            return;
        }

        var parser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;
            
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ILookup<TKey, TElement>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
            
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
                
            reader.Advance(4);
                
            return;
        }

        reader.Advance(4);
            
        var dict = new Dictionary<TKey, IGrouping<TKey, TElement>>(equalityComparer);

        var parser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;
        for (int i = 0; i < length; i++)
        {
            IGrouping<TKey, TElement>? item = default;
            parser.Deserialize(ref reader, ref item);
            if (item != null)
            {
                dict.Add(item.Key, item);
            }
        }
        value = new Lookup<TKey, TElement>(dict);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ILookup<TKey, TElement>? value)
    {
        if (value is null)
        {
            evaluator += 4;
                
            return;
        }

        var eval = evaluator.GetEvaluator<IGrouping<TKey, TElement>>();
            
        evaluator += 4;
            
        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ILookup<TKey, TElement>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var groupingParser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;

        foreach (var item in value)
        {

            var temp = item;
            groupingParser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ILookup<TKey, TElement>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var dict = new Dictionary<TKey, IGrouping<TKey, TElement>>(equalityComparer);

        var groupingParser = LuminPackParseProvider.Cache<IGrouping<TKey, TElement>>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            IGrouping<TKey, TElement>? g = default;
            groupingParser.DeserializeJson(ref reader, ref g);
            dict.Add(g!.Key, g!);
        }

        value = new Lookup<TKey, TElement>(dict);
    }

}

[Preserve]
public sealed class InterfaceGroupingParser<TKey, TElement> : LuminPackParser<IGrouping<TKey, TElement>>
    where TKey : notnull
{
    
    static InterfaceGroupingParser()
    {
        if (!LuminPackParseProvider.IsRegistered<IEnumerable<TElement>>())
        {
            LuminPackParseProvider.RegisterParsers(new InterfaceEnumerableParser<TElement>());
        }
    }
    
    // serialize as {key, [collection]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IGrouping<TKey, TElement>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }
        
        writer.WriteValue(value.Key);
        writer.WriteValue<IEnumerable<TElement>>(value); // write as IEnumerable<TElement>
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IGrouping<TKey, TElement>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }

        var key = reader.ReadValue<TKey>();
        var values = reader.ReadArray<TElement>() as IEnumerable<TElement>;

        if (key is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(key));
        if (values is null) LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(values));

        value = new Grouping<TKey, TElement>(key, values);

    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IGrouping<TKey, TElement>? value)
    {
        if (value is null)
        {
            evaluator += 1;
            
            return;
        }
        
        evaluator.CalculateValue(value.Key);
        evaluator.CalculateValue<IEnumerable<TElement>>(value);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IGrouping<TKey, TElement>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteObjectStart();

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var elementsParser = LuminPackParseProvider.Cache<IEnumerable<TElement>>.Parser!;

        writer.WriteByteRaw((byte)'"');
        writer.WritePropertyName("Key"u8);
        writer.WriteByteRaw((byte)'"');
        writer.WriteByteRaw((byte)':');
        var k = value.Key;
        keyParser.SerializeJson(ref writer, ref k);


        writer.WriteByteRaw((byte)'"');
        writer.WritePropertyName("Elements"u8);
        writer.WriteByteRaw((byte)'"');
        writer.WriteByteRaw((byte)':');
        IEnumerable<TElement> elements = value;
        elementsParser.SerializeJson(ref writer, ref elements);

        writer.WriteObjectEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IGrouping<TKey, TElement>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        TKey? key = default;
        IEnumerable<TElement>? elements = null;

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var elementsParser = LuminPackParseProvider.Cache<IEnumerable<TElement>>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();

                if (propertyName.SequenceEqual("Key"u8))
                {
                    keyParser.DeserializeJson(ref reader, ref key);
                }
                else if (propertyName.SequenceEqual("Elements"u8))
                {
                    elementsParser.DeserializeJson(ref reader, ref elements);
                }
            }
        }

        value = new Grouping<TKey, TElement>(key!, elements!);
    }

}

[Preserve]
public sealed class InterfaceSetParser<T> : LuminPackParser<ISet<T?>>
{

    readonly IEqualityComparer<T?>? equalityComparer;

    static InterfaceSetParser()
    {
        if (!LuminPackParseProvider.IsRegistered<HashSet<T>>())
        {
            LuminPackParseProvider.RegisterParsers(new InterfaceSetParser<T>());
        }
    }
    
    public InterfaceSetParser()
        : this(null)
    {
    }

    public InterfaceSetParser(IEqualityComparer<T?>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ISet<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;
        
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ISet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        var set = new HashSet<T?>(length, equalityComparer);

        var parser = LuminPackParseProvider.Cache<T>.Parser!;
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            set.Add(item);
        }

        value = set;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ISet<T?>? value)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ISet<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ISet<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var set = new HashSet<T?>(equalityComparer);

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            set.Add(item);
        }

        value = set;
    }

}

#if NET7_0_OR_GREATER

[Preserve]
public sealed class InterfaceReadOnlySetParser<T> : LuminPackParser<IReadOnlySet<T?>>
{

    readonly IEqualityComparer<T?>? equalityComparer;

    static InterfaceReadOnlySetParser()
    {
        if (!LuminPackParseProvider.IsRegistered<HashSet<T>>())
        {
            LuminPackParseProvider.RegisterParsers(new InterfaceReadOnlySetParser<T>());
        }
    }
    
    public InterfaceReadOnlySetParser()
        : this(null)
    {
    }

    public InterfaceReadOnlySetParser(IEqualityComparer<T?>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IReadOnlySet<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var parser = writer.GetParser<T>();
        
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref IReadOnlySet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        var set = new HashSet<T?>(length, equalityComparer);

        var parser = reader.GetParser<T>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            set.Add(item);
        }

        value = set;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IReadOnlySet<T?>? value)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IReadOnlySet<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        bool isFirst = true;
        foreach (var item in value)
        {
            if (!isFirst) writer.WriteByteRaw((byte)',');
            else isFirst = false;
            writer.SetFirstElement(true);

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IReadOnlySet<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var set = new HashSet<T?>(equalityComparer);

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                continue;

            T? item = default;
            parser.DeserializeJson(ref reader, ref item);
            set.Add(item);
        }

        value = set;
    }

}

#endif

[Preserve]
internal sealed class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
{
    readonly TKey key;
    readonly IEnumerable<TElement> elements;

    public Grouping(TKey key, IEnumerable<TElement> elements)
    {
        this.key = key;
        this.elements = elements;
    }

    public TKey Key
    {
        get
        {
            return this.key;
        }
    }

    public IEnumerator<TElement> GetEnumerator()
    {
        return this.elements.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.elements.GetEnumerator();
    }
}

[Preserve]
internal sealed class Lookup<TKey, TElement> : ILookup<TKey, TElement>
    where TKey : notnull
{
    readonly Dictionary<TKey, IGrouping<TKey, TElement>> groupings;

    public Lookup(Dictionary<TKey, IGrouping<TKey, TElement>> groupings)
    {
        this.groupings = groupings;
    }

    public IEnumerable<TElement> this[TKey key]
    {
        get
        {
            return this.groupings.TryGetValue(key, out var value) ? value : Enumerable.Empty<TElement>();
        }
    }

    public int Count
    {
        get
        {
            return this.groupings.Count;
        }
    }

    public bool Contains(TKey key)
    {
        return this.groupings.ContainsKey(key);
    }

    public IEnumerator<IGrouping<TKey, TElement>> GetEnumerator()
    {
        return this.groupings.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.groupings.Values.GetEnumerator();
    }
}