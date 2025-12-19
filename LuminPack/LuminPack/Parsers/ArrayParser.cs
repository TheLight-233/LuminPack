using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class ArrayParser<T> : LuminPackParser<T[]?>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T[]? value)
    {
        writer.WriteArray(value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T[]? value)
    {
        reader.ReadArray(ref value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T[]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();
        foreach (ref var v in value.AsSpan())
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref v!);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T[]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T[]? buffer = ArrayPool<T>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T[] newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                buffer[count++] = item!;
            }
            
            value = new T[count];
            buffer.AsSpan(0, count).CopyTo(value);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T>.Shared.Return(buffer, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T[]? value)
    {
        if (value is null || value.Length is 0)
        {
            evaluator.Add(4);
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}

[Preserve]
public sealed class UnmanagedArrayParser<T> : LuminPackParser<T[]?> 
    where T : unmanaged
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T[]? value)
    {
        writer.WriteUnmanagedArray(ref value);
        
        if (value is null || value.Length is 0)
        {
            writer.Advance(4);
            return;
        }
        
        writer.Advance(4 + value.Length * Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T[]? value)
    {
        reader.ReadUnmanagedArray(ref value);
        
        if (value is null || value.Length is 0)
        {
            reader.Advance(4);
            return;
        }
        
        reader.Advance(4 + value.Length * Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T[]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();
        foreach (ref var v in value.AsSpan())
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref v);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T[]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T[]? buffer = ArrayPool<T>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T[] newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T>.Shared.Return(buffer, clearArray: false);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                buffer[count++] = item;
            }
            
            value = new T[count];
            buffer.AsSpan(0, count).CopyTo(value);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T>.Shared.Return(buffer, clearArray: false);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T[]? value)
    {
        if (value is null || value.Length is 0)
        {
            evaluator.Add(4);
            return;
        }
        
        evaluator.Add(4 + value.Length * Unsafe.SizeOf<T>());
    }
}

[Preserve]
public sealed class DangerousUnmanagedArrayParser<T> : LuminPackParser<T[]?>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T[]? value)
    {
        writer.DangerousWriteUnmanagedArray(ref value);
        
        if (value is null || value.Length is 0)
        {
            writer.Advance(4);
            return;
        }
        
        writer.Advance(4 + value.Length * Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T[]? value)
    {
        reader.DangerousReadUnmanagedArray(ref value);
        
        if (value is null || value.Length is 0)
        {
            reader.Advance(4);
            return;
        }
        
        reader.Advance(4 + value.Length * Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T[]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();
        foreach (ref var v in value.AsSpan())
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref v!);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T[]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T[]? buffer = ArrayPool<T>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T[] newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T>.Shared.Return(buffer, clearArray: false);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            value = new T[count];
            buffer.AsSpan(0, count).CopyTo(value);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T>.Shared.Return(buffer, clearArray: false);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T[]? value)
    {
        if (value is null || value.Length is 0)
        {
            evaluator.Add(4);
            return;
        }
        
        evaluator.Add(4 + value.Length * Unsafe.SizeOf<T>());
    }
}

[Preserve]
public sealed class ArraySegmentParser<T> : LuminPackParser<ArraySegment<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ArraySegment<T?> value)
    {
        writer.WriteSpan(value.AsMemory().Span);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ArraySegment<T?> value)
    {
        var array = reader.ReadArray<T>();
        value = (array is null) ? default : (ArraySegment<T?>)array;
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ArraySegment<T?> value)
    {
        if (value.Array == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();
        for (int i = 0; i < value.Count; i++)
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref value.Array[value.Offset + i]);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ArraySegment<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = new ArraySegment<T?>(resultArray);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ArraySegment<T?> value)
    {
        if (value.Array is null || value.Array.Length is 0)
        {
            evaluator += 4;
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}

[Preserve]
public sealed class MemoryParser<T> : LuminPackParser<Memory<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Memory<T?> value)
    {
        writer.WriteSpan(value.Span);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Memory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Memory<T?> value)
    {
        if (value.Length == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        writer.WriteArrayStart();
        foreach (ref var item in value.Span)
        {
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref item);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Memory<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = resultArray;
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Memory<T?> value)
    {
        if (value.Length is 0)
        {
            evaluator += 4;
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value.Span)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}

[Preserve]
public sealed class ReadOnlySequenceParser<T> : LuminPackParser<ReadOnlySequence<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlySequence<T?> value)
    {
        if (value.IsSingleSegment)
        {
            writer.WriteSpan(value.FirstSpan);
            return;
        }

        ref var index = ref writer.GetCurrentSpanOffset();
        writer.WriteCollectionHeader(ref index, checked((int)value.Length));
        writer.Advance(4);
        
        foreach (var memory in value)
        {
            writer.WriteSpanWithOutHeader(memory.Span);
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlySequence<T?> value)
    {
        var array = reader.ReadArray<T>();
        value = (array == null) ? default : new ReadOnlySequence<T?>(array);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ReadOnlySequence<T?> value)
    {
        if (value.IsEmpty)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        writer.WriteArrayStart();
        foreach (var memory in value)
        {
            foreach (var item in memory.Span)
            {
                var temp = item;
                LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref temp);
            }
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ReadOnlySequence<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = new ReadOnlySequence<T?>(resultArray);
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlySequence<T?> value)
    {
        if (value.IsSingleSegment)
        {
            evaluator += 4 + value.FirstSpan.Length;
            return;
        }
        
        evaluator += 4 + checked((int)value.Length);
    }
}

[Preserve]
public sealed class ReadOnlyMemoryParser<T> : LuminPackParser<ReadOnlyMemory<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        var ptr = value.Span.GetPinnableReference();
        var span = MemoryMarshal.CreateSpan(ref ptr, value.Length);
        writer.WriteSpan(span);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        if (value.Length == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        writer.WriteArrayStart();
        foreach (var item in value.Span)
        {
            var temp = item;
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref temp);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = resultArray;
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlyMemory<T?> value)
    {
        if (value.Length is 0)
        {
            evaluator += 4;
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value.Span)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}

[Preserve]
public sealed class MemoryPoolParser<T> : LuminPackParser<Memory<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Memory<T?> value)
    {
        writer.WriteSpan(value.Span);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Memory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Memory<T?> value)
    {
        if (value.Length == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        writer.WriteArrayStart();
        foreach (var item in value.Span)
        {
            var temp = item;
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref temp);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Memory<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = resultArray;
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Memory<T?> value)
    {
        if (value.Length is 0)
        {
            evaluator += 4;
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value.Span)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}

[Preserve]
public sealed class ReadOnlyMemoryPoolParser<T> : LuminPackParser<ReadOnlyMemory<T?>>
{
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        var ptr = value.Span.GetPinnableReference();
        var span = MemoryMarshal.CreateSpan(ref ptr, value.Length);
        writer.WriteSpan(span);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        if (value.Length == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        writer.WriteArrayStart();
        foreach (var item in value.Span)
        {
            var temp = item;
            LuminPackParseProvider.Cache<T>.Parser!.SerializeJson(ref writer, ref temp);
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        if (reader.IsNull())
        {
            value = default;
            return;
        }

        reader.TryConsumeArrayStart();
        
        T?[]? buffer = ArrayPool<T?>.Shared.Rent(4);
        int count = 0;
        int capacity = buffer.Length;
        
        try
        {
            while (true)
            {
                if (!reader.Read())
                    break;
                
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;
                
                if (count >= capacity)
                {
                    int newCapacity = capacity * 2;
                    T?[] newBuffer = ArrayPool<T?>.Shared.Rent(newCapacity);
                    buffer.AsSpan(0, count).CopyTo(newBuffer);
                    ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                    capacity = newBuffer.Length;
                }
                
                T? item = default;
                LuminPackParseProvider.Cache<T>.Parser!.DeserializeJson(ref reader, ref item);
                
                if (item != null)
                {
                    buffer[count++] = item;
                }
            }
            
            T?[] resultArray = new T?[count];
            buffer.AsSpan(0, count).CopyTo(resultArray);
            value = resultArray;
        }
        finally
        {
            if (buffer != null)
            {
                ArrayPool<T?>.Shared.Return(buffer, clearArray: true);
            }
        }
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref ReadOnlyMemory<T?> value)
    {
        if (value.Length is 0)
        {
            evaluator += 4;
            return;
        }
        
        var eva = evaluator.GetEvaluator<T>();
        evaluator.Add(4);
        
        foreach (var v in value.Span)
        {
            var temp = v;
            eva.CalculateOffset(ref evaluator, ref temp);
        }
    }
}