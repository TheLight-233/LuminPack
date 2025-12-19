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
public struct ImmutableArrayView<T>
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableArray<T?> value)
    {
        if (value.IsDefault)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Length == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;
        var span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(value.AsSpan()), value.Length);

        foreach (ref var v in span)
        {
            parser.SerializeJson(ref writer, ref v);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableArray<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableArray<T?>.Empty;
                return;
            }

            // 创建正确大小的数组
            var finalArray = new T?[count];
            Array.Copy(buffer, finalArray, count);

#if NET8_0_OR_GREATER
            value = ImmutableCollectionsMarshal.AsImmutableArray(finalArray);
#else
            value = ImmutableArray.Create<T?>();
            ref var view = ref LuminPackMarshal.As<ImmutableArray<T?>, ImmutableArrayView<T?>>(ref value);
            view.array = finalArray;
#endif
        }
        finally
        {
            // 归还ArrayPool，如果T是引用类型或包含引用则清理
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
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

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableList<T?>? value)
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

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableList<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableList<T?>.Empty;
                return;
            }

            // 从buffer构建ImmutableList
            var builder = ImmutableList.CreateBuilder<T?>();
            for (int i = 0; i < count; i++)
            {
                builder.Add(buffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var tempBuffer = LuminBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);

            var count = 0;
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                count++;
                var v = item;
                parser.Serialize(ref tempWriter, ref v);
            }

            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            LuminBufferWriterPool.Return(tempBuffer);
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

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                value = ImmutableQueue.Create(rentArray);
            }
            else
            {
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableQueue<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableQueue<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableQueue<T?>.Empty;
            }
            else
            {
                // 从buffer构建ImmutableQueue
                if (buffer.Length == count)
                {
                    value = ImmutableQueue.Create(buffer);
                }
                else
                {
                    value = ImmutableQueue.CreateRange((new ArraySegment<T?>(buffer, 0, count)).AsEnumerable());
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var tempBuffer = LuminBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);

            var count = 0;
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                count++;
                var v = item;
                parser.Serialize(ref tempWriter, ref v);
            }

            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            LuminBufferWriterPool.Return(tempBuffer);
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            for (int i = length - 1; i >= 0; i--)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                value = ImmutableStack.Create(rentArray);
            }
            else
            {
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableStack<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableStack<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableStack<T?>.Empty;
            }
            else
            {
                Array.Reverse(buffer);
                // 从buffer构建ImmutableStack
                if (buffer.Length == count)
                {
                    value = ImmutableStack.Create(buffer);
                }
                else
                {
                    value = ImmutableStack.CreateRange((new ArraySegment<T?>(buffer, 0, count)).AsEnumerable());
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableDictionary<TKey, TValue?>? value)
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
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableDictionary<TKey, TValue?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var keyBuffer = ArrayPool<TKey>.Shared.Rent(initialCapacity);
        var valueBuffer = ArrayPool<TValue?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
            var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                    break;

                // 如果需要扩容
                if (count >= keyBuffer.Length)
                {
                    var newSize = keyBuffer.Length * 2;
                    
                    var newKeyBuffer = ArrayPool<TKey>.Shared.Rent(newSize);
                    var newValueBuffer = ArrayPool<TValue?>.Shared.Rent(newSize);
                    
                    Array.Copy(keyBuffer, newKeyBuffer, count);
                    Array.Copy(valueBuffer, newValueBuffer, count);
                    
                    ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
                    ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
                    
                    keyBuffer = newKeyBuffer;
                    valueBuffer = newValueBuffer;
                }

                TKey? k = default;
                TValue? v = default;
                KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
                keyBuffer[count] = k!;
                valueBuffer[count] = v;
                count++;
            }

            if (count == 0)
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

            // 从buffer构建ImmutableDictionary
            var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(keyBuffer[i], valueBuffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
            ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
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

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableHashSet<T?>? value)
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

        foreach (var item in value)
        {
            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableHashSet<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
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

            // 从buffer构建ImmutableHashSet
            var builder = ImmutableHashSet.CreateBuilder(equalityComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(buffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableSortedDictionary<TKey, TValue?>? value)
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
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableSortedDictionary<TKey, TValue?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var keyBuffer = ArrayPool<TKey>.Shared.Rent(initialCapacity);
        var valueBuffer = ArrayPool<TValue?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
            var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                    break;

                // 如果需要扩容
                if (count >= keyBuffer.Length)
                {
                    var newSize = keyBuffer.Length * 2;
                    
                    var newKeyBuffer = ArrayPool<TKey>.Shared.Rent(newSize);
                    var newValueBuffer = ArrayPool<TValue?>.Shared.Rent(newSize);
                    
                    Array.Copy(keyBuffer, newKeyBuffer, count);
                    Array.Copy(valueBuffer, newValueBuffer, count);
                    
                    ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
                    ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
                    
                    keyBuffer = newKeyBuffer;
                    valueBuffer = newValueBuffer;
                }

                TKey? k = default;
                TValue? v = default;
                KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
                keyBuffer[count] = k!;
                valueBuffer[count] = v;
                count++;
            }

            if (count == 0)
            {
                if (keyComparer != null || valueEqualityComparer != null)
                {
                    value = ImmutableSortedDictionary<TKey, TValue?>.Empty.WithComparers(keyComparer, valueEqualityComparer);
                }
                else
                {
                    value = ImmutableSortedDictionary<TKey, TValue?>.Empty;
                }
                return;
            }

            // 从buffer构建ImmutableSortedDictionary
            var builder = ImmutableSortedDictionary.CreateBuilder(keyComparer, valueEqualityComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(keyBuffer[i], valueBuffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
            ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
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

        if (length is 1)
        {
            var item = reader.ReadValue<T>();
            value = ImmutableSortedSet.Create(keyComparer, item);
            return;
        }

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

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

        var eval = evaluator.GetEvaluator<T?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ImmutableSortedSet<T?>? value)
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

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ImmutableSortedSet<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                if (keyComparer != null)
                {
                    value = ImmutableSortedSet<T?>.Empty.WithComparer(keyComparer);
                }
                else
                {
                    value = ImmutableSortedSet<T?>.Empty;
                }
                return;
            }

            // 从buffer构建ImmutableSortedSet
            var builder = ImmutableSortedSet.CreateBuilder(keyComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(buffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

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

        var eval = evaluator.GetEvaluator<T?>();

        foreach (var item in value)
        {
            var v = item;
            eval.CalculateOffset(ref evaluator, ref v);
        }
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IImmutableList<T?>? value)
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

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IImmutableList<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableList<T?>.Empty;
                return;
            }

            // 从buffer构建ImmutableList
            var builder = ImmutableList.CreateBuilder<T?>();
            for (int i = 0; i < count; i++)
            {
                builder.Add(buffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var tempBuffer = LuminBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);

            var count = 0;
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                count++;
                var v = item;
                parser.Serialize(ref tempWriter, ref v);
            }

            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            LuminBufferWriterPool.Return(tempBuffer);
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

        var rentArray = ArrayPool<T?>.Shared.Rent(length);
        try
        {
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            for (int i = 0; i < length; i++)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                value = ImmutableQueue.Create(rentArray);
            }
            else
            {
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IImmutableQueue<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IImmutableQueue<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableQueue<T?>.Empty;
            }
            else
            {
                // 从buffer构建ImmutableQueue
                if (buffer.Length == count)
                {
                    value = ImmutableQueue.Create(buffer);
                }
                else
                {
                    value = ImmutableQueue.CreateRange((new ArraySegment<T?>(buffer, 0, count)).AsEnumerable());
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var tempBuffer = LuminBufferWriterPool.Rent();
        try
        {
            var tempWriter = new LuminPackWriter(tempBuffer, writer.OptionState);

            var count = 0;
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                count++;
                var v = item;
                parser.Serialize(ref tempWriter, ref v);
            }

            writer.WriteCollectionHeader(ref index, count);
            
            writer.Advance(4);
            
            tempBuffer.WriteToAndReset(ref writer);
        }
        finally
        {
            LuminBufferWriterPool.Return(tempBuffer);
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            for (int i = length - 1; i >= 0; i--)
            {
                parser.Deserialize(ref reader, ref rentArray[i]);
            }

            if (rentArray.Length == length)
            {
                value = ImmutableStack.Create(rentArray);
            }
            else
            {
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IImmutableStack<T?>? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IImmutableStack<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
            {
                value = ImmutableStack<T?>.Empty;
            }
            else
            {
                Array.Reverse(buffer);
                // 从buffer构建ImmutableStack
                if (buffer.Length == count)
                {
                    value = ImmutableStack.Create(buffer);
                }
                else
                {
                    value = ImmutableStack.CreateRange((new ArraySegment<T?>(buffer, 0, count)).AsEnumerable());
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
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

        var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
        var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IImmutableDictionary<TKey, TValue?>? value)
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
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IImmutableDictionary<TKey, TValue?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeObjectStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var keyBuffer = ArrayPool<TKey>.Shared.Rent(initialCapacity);
        var valueBuffer = ArrayPool<TValue?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var keyParser = LuminPackParseProvider.Cache<TKey>.Parser!;
            var valueParser = LuminPackParseProvider.Cache<TValue>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                    break;

                // 如果需要扩容
                if (count >= keyBuffer.Length)
                {
                    var newSize = keyBuffer.Length * 2;
                    
                    var newKeyBuffer = ArrayPool<TKey>.Shared.Rent(newSize);
                    var newValueBuffer = ArrayPool<TValue?>.Shared.Rent(newSize);
                    
                    Array.Copy(keyBuffer, newKeyBuffer, count);
                    Array.Copy(valueBuffer, newValueBuffer, count);
                    
                    ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
                    ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
                    
                    keyBuffer = newKeyBuffer;
                    valueBuffer = newValueBuffer;
                }

                TKey? k = default;
                TValue? v = default;
                KeyValuePairParser.DeserializeJson(keyParser, valueParser, ref reader, ref k, ref v);
                keyBuffer[count] = k!;
                valueBuffer[count] = v;
                count++;
            }

            if (count == 0)
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

            // 从buffer构建ImmutableDictionary
            var builder = ImmutableDictionary.CreateBuilder(keyEqualityComparer, valueEqualityComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(keyBuffer[i], valueBuffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<TKey>.Shared.Return(keyBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TKey>());
            ArrayPool<TValue?>.Shared.Return(valueBuffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
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

        var parser = LuminPackParseProvider.Cache<T?>.Parser!;

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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref IImmutableSet<T?>? value)
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

        foreach (var item in value)
        {

            var temp = item;
            parser.SerializeJson(ref writer, ref temp);
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref IImmutableSet<T?>? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        // 使用ArrayPool实现0GC
        const int initialCapacity = 16;
        var buffer = ArrayPool<T?>.Shared.Rent(initialCapacity);
        var count = 0;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                // 如果需要扩容
                if (count >= buffer.Length)
                {
                    var newSize = buffer.Length * 2;
                    var newBuffer = ArrayPool<T?>.Shared.Rent(newSize);
                    Array.Copy(buffer, newBuffer, count);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                }

                T? item = default;
                parser.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }

            if (count == 0)
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

            // 从buffer构建ImmutableHashSet
            var builder = ImmutableHashSet.CreateBuilder(equalityComparer);
            for (int i = 0; i < count; i++)
            {
                builder.Add(buffer[i]);
            }

            value = builder.ToImmutable();
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}