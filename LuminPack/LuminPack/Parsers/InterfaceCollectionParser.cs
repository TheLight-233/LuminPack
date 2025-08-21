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

file static class InterfaceCollectionParserUtils
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
            writer.WriteSpan(LuminPackMarshal.GetListSpan(ref list, list.Count));
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
            var span = LuminPackMarshal.GetListSpan(ref list, list.Count);
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
            
        var parser = writer.GetParser<TElement>();
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v!);
        }
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
            
        var parser = writer.GetParser<TElement>();
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v!);
        }
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
        var parser = reader.GetParser<List<T?>>();
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
            var parser = writer.GetParser<T?>();
            
            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
        else
        {
            // write to tempbuffer(because we don't know length so can't write header)
            var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
            try
            {
                var tempWriter = new LuminPackWriter(writer.OptionState);
                tempWriter.SetWriteBuffer(tempBuffer);

                count = 0;
                var parser = writer.GetParser<T?>();
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
            }
            finally
            {
                ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
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

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();
        
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

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();
        
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

        var parser = writer.GetParser<IGrouping<TKey, TElement>>();
            
        writer.WriteCollectionHeader(ref index, value.Count);
            
        writer.Advance(4);
            
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v);
        }
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

        var parser = reader.GetParser<IGrouping<TKey, TElement>>();
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

        var parser = writer.GetParser<T>();
        
        writer.WriteCollectionHeader(ref index, value.Count);
        
        writer.Advance(4);
        
        foreach (var item in value)
        {
            var v = item;
            parser.Serialize(ref writer, ref v);
        }
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