using LuminPack.Attribute;
using LuminPack.Core;
using System.Runtime.CompilerServices;
using LuminPack.Code;

namespace LuminPack.Parsers;

[Preserve]
public sealed class TwoDimensionalArrayParser<T> : LuminPackParser<T?[,]>
{
    // {i-length, j-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }
        

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        writer.WriteUnmanaged(ref index, i, j);

        writer.Advance(8);
        

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {

            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength);
        
        reader.Advance(8);

        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);
        
        if (value is not null && value.GetLength(0) == iLength && value.GetLength(1) == jLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);

            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = -1;
            var count = 0;
            while (count++ < length)
            {
                if (j < jLength - 1)
                {
                    j++;
                }
                else
                {
                    j = 0;
                    i++;
                }
                
                parser.Deserialize(ref reader, ref value[i, j]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            
            return;
        }

        evaluator += 12;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}

[Preserve]
public sealed class ThreeDimensionalArrayParser<T> : LuminPackParser<T?[,,]>
{
    // {i-length, j-length, k-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }
        

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        var k = value.GetLength(2);
        writer.WriteUnmanaged(ref index, i, j, k);
        
        writer.Advance(12);
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j * k;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }
        

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength, out int kLength);
        
        reader.Advance(12);

        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);
        
        if (value != null && value.GetLength(0) == iLength && value.GetLength(1) == jLength && value.GetLength(2) == kLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength, kLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength * kLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);

            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = 0;
            var k = -1;
            var count = 0;
            while (count++ < length)
            {
                if (k < kLength - 1)
                {
                    k++;
                }
                else if (j < jLength - 1)
                {
                    k = 0;
                    j++;
                }
                else
                {
                    k = 0;
                    j = 0;
                    i++;
                }

                parser.Deserialize(ref reader, ref value[i, j, k]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            
            return;
        }

        evaluator += 16;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}

[Preserve]
public sealed class FourDimensionalArrayParser<T> : LuminPackParser<T?[,,,]>
{
    // {i-length, j-length, k-length, l-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,,,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        var k = value.GetLength(2);
        var l = value.GetLength(3);
        writer.WriteUnmanaged(ref index, i, j, k, l);

        writer.Advance(16);
        

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j * k * l;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,,,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }
        

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength, out int kLength, out int lLength);

        reader.Advance(16);
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);
        
        if (value != null && value.GetLength(0) == iLength && value.GetLength(1) == jLength && value.GetLength(2) == kLength && value.GetLength(3) == lLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength, kLength, lLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength * kLength * lLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);

            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = 0;
            var k = 0;
            var l = -1;
            var count = 0;
            while (count++ < length)
            {
                if (l < lLength - 1)
                {
                    l++;
                }
                else if (k < kLength - 1)
                {
                    l = 0;
                    k++;
                }
                else if (j < jLength - 1)
                {
                    l = 0;
                    k = 0;
                    j++;
                }
                else
                {
                    l = 0;
                    k = 0;
                    j = 0;
                    i++;
                }

                parser.Deserialize(ref reader, ref value[i, j, k, l]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            
            return;
        }

        evaluator += 20;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}

[Preserve]
public sealed class FiveDimensionalArrayParser<T> : LuminPackParser<T?[,,,,]>
{
    // {i-length, j-length, k-length, l-length, m-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,,,,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        var k = value.GetLength(2);
        var l = value.GetLength(3);
        var m = value.GetLength(4);
        writer.WriteUnmanaged(ref index, i, j, k, l, m);
        writer.Advance(20);


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j * k * l * m;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            writer.Advance(4);
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,,,,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength, out int kLength, out int lLength, out int mLength);
        reader.Advance(20);

        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);

        if (value != null && value.GetLength(0) == iLength && value.GetLength(1) == jLength && value.GetLength(2) == kLength && value.GetLength(3) == lLength && value.GetLength(4) == mLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength, kLength, lLength, mLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength * kLength * lLength * mLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);
            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = 0;
            var k = 0;
            var l = 0;
            var m = -1;
            var count = 0;
            while (count++ < length)
            {
                if (m < mLength - 1)
                {
                    m++;
                }
                else if (l < lLength - 1)
                {
                    m = 0;
                    l++;
                }
                else if (k < kLength - 1)
                {
                    m = 0;
                    l = 0;
                    k++;
                }
                else if (j < jLength - 1)
                {
                    m = 0;
                    l = 0;
                    k = 0;
                    j++;
                }
                else
                {
                    m = 0;
                    l = 0;
                    k = 0;
                    j = 0;
                    i++;
                }

                parser.Deserialize(ref reader, ref value[i, j, k, l, m]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            return;
        }

        evaluator += 24;

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}

[Preserve]
public sealed class SixDimensionalArrayParser<T> : LuminPackParser<T?[,,,,,]>
{
    // {i-length, j-length, k-length, l-length, m-length, n-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,,,,,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        var k = value.GetLength(2);
        var l = value.GetLength(3);
        var m = value.GetLength(4);
        var n = value.GetLength(5);
        writer.WriteUnmanaged(ref index, i, j, k, l, m, n);
        writer.Advance(24);


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j * k * l * m * n;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            writer.Advance(4);
            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,,,,,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength, out int kLength, out int lLength,
            out int mLength, out int nLength);
        reader.Advance(24);

        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);

        if (value != null && value.GetLength(0) == iLength && value.GetLength(1) == jLength &&
            value.GetLength(2) == kLength && value.GetLength(3) == lLength && value.GetLength(4) == mLength &&
            value.GetLength(5) == nLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength, kLength, lLength, mLength, nLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength * kLength * lLength * mLength * nLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);
            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = 0;
            var k = 0;
            var l = 0;
            var m = 0;
            var n = -1;
            var count = 0;
            while (count++ < length)
            {
                if (n < nLength - 1)
                {
                    n++;
                }
                else if (m < mLength - 1)
                {
                    n = 0;
                    m++;
                }
                else if (l < lLength - 1)
                {
                    n = 0;
                    m = 0;
                    l++;
                }
                else if (k < kLength - 1)
                {
                    n = 0;
                    m = 0;
                    l = 0;
                    k++;
                }
                else if (j < jLength - 1)
                {
                    n = 0;
                    m = 0;
                    l = 0;
                    k = 0;
                    j++;
                }
                else
                {
                    n = 0;
                    m = 0;
                    l = 0;
                    k = 0;
                    j = 0;
                    i++;
                }

                parser.Deserialize(ref reader, ref value[i, j, k, l, m, n]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            return;
        }

        evaluator += 28;

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}

[Preserve]
public sealed class SevenDimensionalArrayParser<T> : LuminPackParser<T?[,,,,,,]>
{
    // {i-length, j-length, k-length, l-length, m-length, n-length, o-length, [total-length, values]}

    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T?[,,,,,,]? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();

        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            writer.Advance(1);
            return;
        }

        var i = value.GetLength(0);
        var j = value.GetLength(1);
        var k = value.GetLength(2);
        var l = value.GetLength(3);
        var m = value.GetLength(4);
        var n = value.GetLength(5);
        var o = value.GetLength(6);
        writer.WriteUnmanaged(ref index, i, j, k, l, m, n, o);

        writer.Advance(28);
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * i * j * k * l * m * n * o;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            writer.Advance(4);

            var parser = writer.GetParser<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.Serialize(ref writer, ref v);
            }
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T?[,,,,,,]? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();

        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            reader.Advance(1);
            return;
        }

        reader.ReadUnmanaged(ref index, out int iLength, out int jLength, out int kLength, out int lLength, out int mLength, out int nLength, out int oLength);
        reader.Advance(28);

        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            LuminPackExceptionHelper.ThrowInvalidCollection();
        }

        reader.Advance(4);

        if (value != null && value.GetLength(0) == iLength && value.GetLength(1) == jLength && value.GetLength(2) == kLength && value.GetLength(3) == lLength && value.GetLength(4) == mLength && value.GetLength(5) == nLength && value.GetLength(6) == oLength && value.Length == length)
        {
            // allow overwrite
        }
        else
        {
            value = new T[iLength, jLength, kLength, lLength, mLength, nLength, oLength];
        }


        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * iLength * jLength * kLength * lLength * mLength * nLength * oLength;
            ref var dest = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var src = ref reader.GetCurrentSpanReference();
            Unsafe.CopyBlockUnaligned(ref dest, ref src, (uint)byteCount);

            reader.Advance(byteCount);
        }
        else
        {
            var parser = reader.GetParser<T?>();

            var i = 0;
            var j = 0;
            var k = 0;
            var l = 0;
            var m = 0;
            var n = 0;
            var o = -1;
            var count = 0;
            while (count++ < length)
            {
                if (o < oLength - 1)
                {
                    o++;
                }
                else if (n < nLength - 1)
                {
                    o = 0;
                    n++;
                }
                else if (m < mLength - 1)
                {
                    o = 0;
                    n = 0;
                    m++;
                }
                else if (l < lLength - 1)
                {
                    o = 0;
                    n = 0;
                    m = 0;
                    l++;
                }
                else if (k < kLength - 1)
                {
                    o = 0;
                    n = 0;
                    m = 0;
                    l = 0;
                    k++;
                }
                else if (j < jLength - 1)
                {
                    o = 0;
                    n = 0;
                    m = 0;
                    l = 0;
                    k = 0;
                    j++;
                }
                else
                {
                    o = 0;
                    n = 0;
                    m = 0;
                    l = 0;
                    k = 0;
                    j = 0;
                    i++;
                }

                parser.Deserialize(ref reader, ref value[i, j, k, l, m, n, o]);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,,,]? value)
    {
        if (value is null) // NullObject
        {
            evaluator += 1;
            return;
        }

        evaluator += 32;

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            evaluator += Unsafe.SizeOf<T>() * value.Length;
        }
        else
        {
            var parser = evaluator.GetEvaluator<T?>();
            foreach (var item in value)
            {
                var v = item;
                parser.CalculateOffset(ref evaluator, ref v);
            }
        }
    }
}
