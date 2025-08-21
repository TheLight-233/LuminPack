
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
    public override void Serialize(ref LuminPackWriter writer, scoped ref ArraySegment<T?> value)
    {
        writer.WriteSpan(value.AsMemory().Span);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ArraySegment<T?> value)
    {
        var array = reader.ReadArray<T>();
        value = (array is null) ? default : (ArraySegment<T?>)array;
    }

    [Preserve]
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
    public override void Serialize(ref LuminPackWriter writer, scoped ref Memory<T?> value)
    {
        writer.WriteSpan(value.Span);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Memory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
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
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlySequence<T?> value)
    {
        var array = reader.ReadArray<T>();
        value = (array == null) ? default : new ReadOnlySequence<T?>(array);
    }

    [Preserve]
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
    public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        var ptr = value.Span.GetPinnableReference();
        var span = MemoryMarshal.CreateSpan(ref ptr, value.Length);
        writer.WriteSpan(span);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
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
    public override void Serialize(ref LuminPackWriter writer, scoped ref Memory<T?> value)
    {
        writer.WriteSpan(value.Span);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Memory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
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
    public override void Serialize(ref LuminPackWriter writer, scoped ref ReadOnlyMemory<T?> value)
    {
        var ptr = value.Span.GetPinnableReference();
        var span = MemoryMarshal.CreateSpan(ref ptr, value.Length);
        writer.WriteSpan(span);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref ReadOnlyMemory<T?> value)
    {
        value = reader.ReadArray<T>();
    }

    [Preserve]
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

