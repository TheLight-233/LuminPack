using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;

namespace LuminPack.Parsers;

[Preserve]
internal struct ImmutableArrayView<T>
{
    public T[]? array;
}

[Preserve]
public sealed class ImmutableArrayParser<T> : LuminPackParser<ImmutableArray<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableArray<T?> value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value.IsDefault)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
        }
        else
        {
            writer.WriteSpan(value.AsSpan());
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableArray<T?> value)
    {
        var array = reader.ReadArray<T?>();
        
        if (array is null)
        {
            value = default;
            return;
        }

        if (array.Length == 0)
        {
            value = ImmutableArray<T?>.Empty;
            return;
        }

#if NET8_0_OR_GREATER
            value = ImmutableCollectionsMarshal.AsImmutableArray(array);
#else
        value = ImmutableArray.Create<T?>();
        ref var view = ref LuminPackMarshal.As<ImmutableArray<T?>, ImmutableArrayView<T?>>(ref value);
        view.array = array;
#endif
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableArray<T?> value)
    {
        var v = value.AsSpan();
        evaluator.CalculateSpan(ref v);
    }
}

[Preserve]
public sealed class ImmutableListParser<T> : LuminPackParser<ImmutableList<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableList<T?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableList<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length == 0)
        {
            value = ImmutableList<T?>.Empty;
            return;
        }

        if (length == 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableList.Create(item);
            return;
        }

        var parser = reader.GetParser<T?>();

        var builder = ImmutableList.CreateBuilder<T?>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            builder.Add(item);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableList<T?>? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }

        var eval = evaluator.GetEvaluator<T?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public sealed class ImmutableQueueParser<T> : LuminPackParser<ImmutableQueue<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableQueue<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        // ImmutableQueue<T> has no Count, so use similar serialization of IEnumerable<T>

        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(writer.OptionState);
            
            tempWriter.SetWriteBuffer(tempBuffer);

            var count = 0;
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
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableQueue<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length is 0)
        {
            value = ImmutableQueue<T?>.Empty;
            return;
        }

        if (length is 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableQueue.Create(item);
            return;
        }

        // ImmutableQueue<T> has no builder

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                // we can use T[] ctor
                value = ImmutableQueue.Create(rentArray);
            }
            else
            {
                // IEnumerable<T> method
                value = ImmutableQueue.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableQueue<T?>? value)
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

[Preserve]
public sealed class ImmutableStackParser<T> : LuminPackParser<ImmutableStack<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableStack<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(writer.OptionState);
            
            tempWriter.SetWriteBuffer(tempBuffer);

            var count = 0;
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
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableStack<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length is 0)
        {
            value = ImmutableStack<T?>.Empty;
            return;
        }

        if (length is 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableStack.Create(item);
            return;
        }

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                // we can use T[] ctor
                value = ImmutableStack.Create(rentArray);
            }
            else
            {
                // IEnumerable<T> method
                value = ImmutableStack.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableStack<T?>? value)
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

[Preserve]
public sealed class ImmutableDictionaryParser<TKey, TValue> : LuminPackParser<ImmutableDictionary<TKey, TValue?>?> 
    where TKey : notnull
{
    readonly IEqualityComparer<TKey>? keyEqualityComparer;
    readonly IEqualityComparer<TValue?>? valueEqualityComparer;

    public ImmutableDictionaryParser()
        : this(null, null)
    {

    }

    public ImmutableDictionaryParser(IEqualityComparer<TKey>? keyEqualityComparer, IEqualityComparer<TValue?>? valueEqualityComparer)
    {
        this.keyEqualityComparer = keyEqualityComparer;
        this.valueEqualityComparer = valueEqualityComparer;
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableDictionary<TKey, TValue?>? value)
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
        foreach (var item in value)
        {
            KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length is 0)
        {
            value = ImmutableDictionary<TKey, TValue?>.Empty;
            if (keyEqualityComparer != null || valueEqualityComparer != null)
            {
                value = value.WithComparers(keyEqualityComparer, valueEqualityComparer);
            }
            return;
        }

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();

        var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            builder.Add(k!, v);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableDictionary<TKey, TValue?>? value)
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
public sealed class ImmutableHashSetParser<T> : LuminPackParser<ImmutableHashSet<T?>>
{
    
    readonly IEqualityComparer<T?>? equalityComparer;

    public ImmutableHashSetParser()
        : this(null)
    {

    }

    public ImmutableHashSetParser(IEqualityComparer<T?>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableHashSet<T?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableHashSet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (length is 0)
        {
            value = ImmutableHashSet<T?>.Empty;
            if (equalityComparer != null)
            {
                value = value.WithComparer(equalityComparer);
            }
            return;
        }

        if (length == 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableHashSet.Create(equalityComparer, item);
            return;
        }

        var parser = reader.GetParser<T?>();

        var builder = ImmutableHashSet.CreateBuilder(equalityComparer);
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            builder.Add(item);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableHashSet<T?>? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }

        var eval = evaluator.GetEvaluator<T?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public sealed class ImmutableSortedDictionaryParser<TKey, TValue> : LuminPackParser<ImmutableSortedDictionary<TKey, TValue?>?>
    where TKey : notnull
{
    readonly IComparer<TKey>? keyComparer;
    readonly IEqualityComparer<TValue?>? valueEqualityComparer;

    public ImmutableSortedDictionaryParser()
        : this(null, null)
    {

    }

    public ImmutableSortedDictionaryParser(IComparer<TKey>? keyComparer, IEqualityComparer<TValue?>? valueEqualityComparer)
    {
        this.keyComparer = keyComparer;
        this.valueEqualityComparer = valueEqualityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableSortedDictionary<TKey, TValue?>? value)
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
        foreach (var item in value)
        {
            KeyValuePairParser.Serialize(keyFormatter, valueFormatter, ref writer, item!);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableSortedDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (length is 0)
        {
            value = ImmutableSortedDictionary<TKey, TValue?>.Empty;
            if (keyComparer != null || valueEqualityComparer != null)
            {
                value = value.WithComparers(keyComparer, valueEqualityComparer);
            }
            return;
        }

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();

        var builder = ImmutableSortedDictionary.CreateBuilder(keyComparer, valueEqualityComparer);
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            builder.Add(k!, v);
        }

        value = builder.ToImmutable();
    
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableSortedDictionary<TKey, TValue?>? value)
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
public sealed class ImmutableSortedSetParser<T> : LuminPackParser<ImmutableSortedSet<T?>?>
{
    
    readonly IComparer<T?>? keyComparer;

    public ImmutableSortedSetParser()
        : this(null)
    {

    }

    public ImmutableSortedSetParser(IComparer<T?>? keyComparer)
    {
        this.keyComparer = keyComparer;
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ImmutableSortedSet<T?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ImmutableSortedSet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);
        
        if (length is 0)
        {
            value = ImmutableSortedSet<T?>.Empty;
            if (keyComparer != null)
            {
                value = value.WithComparer(keyComparer);
            }
            return;
        }

        if (length == 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableSortedSet.Create(keyComparer, item);
            return;
        }

        var parser = reader.GetParser<T?>();

        var builder = ImmutableSortedSet.CreateBuilder(keyComparer);
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            builder.Add(item);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ImmutableSortedSet<T?>? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }
        
        var eval = evaluator.GetEvaluator<T>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public sealed class InterfaceImmutableListParser<T> : LuminPackParser<IImmutableList<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IImmutableList<T?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref IImmutableList<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (length is 0)
        {
            value = ImmutableList<T?>.Empty;
            return;
        }

        if (length == 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableList.Create(item);
            return;
        }

        var parser = reader.GetParser<T?>();

        var builder = ImmutableList.CreateBuilder<T?>();
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            builder.Add(item);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IImmutableList<T?>? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }
        
        var eval = evaluator.GetEvaluator<T>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}

[Preserve]
public sealed class InterfaceImmutableQueueParser<T> : LuminPackParser<IImmutableQueue<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IImmutableQueue<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        // ImmutableQueue<T> has no Count, so use similar serialization of IEnumerable<T>

        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(writer.OptionState);
            
            tempWriter.SetWriteBuffer(tempBuffer);

            var count = 0;
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
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IImmutableQueue<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length is 0)
        {
            value = ImmutableQueue<T?>.Empty;
            return;
        }

        if (length is 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableQueue.Create(item);
            return;
        }

        // ImmutableQueue<T> has no builder

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                // we can use T[] ctor
                value = ImmutableQueue.Create(rentArray);
            }
            else
            {
                // IEnumerable<T> method
                value = ImmutableQueue.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IImmutableQueue<T?>? value)
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

[Preserve]
public sealed class InterfaceImmutableStackParser<T> : LuminPackParser<IImmutableStack<T?>>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IImmutableStack<T?>? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

        var tempBuffer = ReusableLinkedArrayBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(writer.OptionState);
            
            tempWriter.SetWriteBuffer(tempBuffer);

            var count = 0;
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
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            ReusableLinkedArrayBufferWriterPool.Return(tempBuffer);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref IImmutableStack<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }
        
        reader.Advance(4);

        if (length is 0)
        {
            value = ImmutableStack<T?>.Empty;
            return;
        }

        if (length is 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableStack.Create(item);
            return;
        }

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = reader.GetParser<T?>();
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                // we can use T[] ctor
                value = ImmutableStack.Create(rentArray);
            }
            else
            {
                // IEnumerable<T> method
                value = ImmutableStack.CreateRange((new ArraySegment<T?>(rentArray, 0, length)).AsEnumerable());
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(rentArray, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IImmutableStack<T?>? value)
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

[Preserve]
public sealed class InterfaceImmutableDictionaryParser<TKey, TValue> : LuminPackParser<IImmutableDictionary<TKey, TValue?>?>
    where TKey : notnull
{
    readonly IEqualityComparer<TKey>? keyEqualityComparer;
    readonly IEqualityComparer<TValue?>? valueEqualityComparer;

    public InterfaceImmutableDictionaryParser()
        : this(null, null)
    {

    }

    public InterfaceImmutableDictionaryParser(IEqualityComparer<TKey>? keyEqualityComparer, IEqualityComparer<TValue?>? valueEqualityComparer)
    {
        this.keyEqualityComparer = keyEqualityComparer;
        this.valueEqualityComparer = valueEqualityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IImmutableDictionary<TKey, TValue?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref IImmutableDictionary<TKey, TValue?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (length is 0)
        {
            if (keyEqualityComparer != null || valueEqualityComparer != null)
            {
                value = ImmutableDictionary<TKey, TValue?>.Empty.WithComparers(keyEqualityComparer, valueEqualityComparer);
            }
            else
            {
                value = ImmutableDictionary<TKey, TValue?>.Empty;
            }
            return;
        }

        var keyParser = reader.GetParser<TKey>();
        var valueParser = reader.GetParser<TValue>();

        var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);
        for (int i = 0; i < length; i++)
        {
            KeyValuePairParser.Deserialize(keyParser, valueParser, ref reader, out var k, out var v);
            builder.Add(k!, v);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IImmutableDictionary<TKey, TValue?>? value)
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
public sealed class InterfaceImmutableSetParser<T> : LuminPackParser<IImmutableSet<T?>>
{
    readonly IEqualityComparer<T?>? equalityComparer;

    public InterfaceImmutableSetParser()
        : this(null)
    {

    }

    public InterfaceImmutableSetParser(IEqualityComparer<T?>? equalityComparer)
    {
        this.equalityComparer = equalityComparer;
    }

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref IImmutableSet<T?>? value)
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref IImmutableSet<T?>? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (length is 0)
        {
            if (equalityComparer != null)
            {
                value = ImmutableHashSet<T?>.Empty.WithComparer(equalityComparer);
            }
            else
            {
                value = ImmutableHashSet<T?>.Empty;
            }
            return;
        }

        if (length == 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableHashSet.Create(equalityComparer, item);
            return;
        }

        var parser = reader.GetParser<T?>();

        var builder = ImmutableHashSet.CreateBuilder(equalityComparer);
        for (int i = 0; i < length; i++)
        {
            T? item = default;
            parser.Deserialize(ref reader, ref item);
            builder.Add(item);
        }

        value = builder.ToImmutable();
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref IImmutableSet<T?>? value)
    {
        if (value is null || value.Count == 0)
        {
            evaluator += 4;
            
            return;
        }

        var eval = evaluator.GetEvaluator<T?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }
}