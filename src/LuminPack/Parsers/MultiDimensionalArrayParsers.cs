using LuminPack.Attribute;
using LuminPack.Core;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using System.Buffers;

namespace LuminPack.Parsers;

[Preserve]
public sealed class TwoDimensionalArrayParser<T> : LuminPackParser<T?[,]>
{
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
        
        var totalLength = i * j;

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {

            writer.WriteCollectionHeader(ref index, value.Length);
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);
            
            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item!);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
        
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var iLength = value.GetLength(0);
        var jLength = value.GetLength(1);

        if (iLength == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        for (int i = 0; i < iLength; i++)
        {
            writer.WriteArrayStart();

            bool isFirst = true;
            for (int j = 0; j < jLength; j++)
            {
                if (!isFirst) writer.WriteByteRaw((byte)',');
                else isFirst = false;
                writer.SetFirstElement(true);
                var temp = value[i, j];
                parser.SerializeJson(ref writer, ref temp);
            }

            writer.WriteArrayEnd();
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(1024);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;
        var iCount = 0;
        var jCount = 0;
        var firstRow = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                reader.TryConsumeArrayStart();

                var rowCount = 0;
                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                        break;

                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                        continue;
                    break;

                    if (tempIndex >= tempCapacity)
                    {
                        var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                        Array.Copy(tempArray, newArray, tempIndex);
                        ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                        tempArray = newArray;
                        tempCapacity = newArray.Length;
                    }

                    parser.DeserializeJson(ref reader, ref tempArray[tempIndex]);
                    tempIndex++;
                    rowCount++;
                }

                if (firstRow)
                {
                    jCount = rowCount;
                    firstRow = false;
                }

                iCount++;
            }

            if (iCount == 0 || jCount == 0)
            {
                value = new T[0, 0];
                return;
            }

            value = new T[iCount, jCount];

            for (int i = 0; i < iCount; i++)
            {
                for (int j = 0; j < jCount; j++)
                {
                    value[i, j] = tempArray[i * jCount + j];
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

[Preserve]
public sealed class ThreeDimensionalArrayParser<T> : LuminPackParser<T?[,,]>
{
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
        
        var totalLength = i * j * k;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);
            
            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength * kLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        var iLength = value.GetLength(0);
        var jLength = value.GetLength(1);
        var kLength = value.GetLength(2);

        if (iLength == 0)
        {
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        for (int i = 0; i < iLength; i++)
        {
            writer.WriteArrayStart();

            for (int j = 0; j < jLength; j++)
            {
                writer.WriteArrayStart();

                bool isFirst = true;
                for (int k = 0; k < kLength; k++)
                {
                    if (!isFirst) writer.WriteByteRaw((byte)',');
                    else isFirst = false;
                    writer.SetFirstElement(true);
                    var temp = value[i, j, k];
                    parser.SerializeJson(ref writer, ref temp);
                }

                writer.WriteArrayEnd();
            }

            writer.WriteArrayEnd();
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(1024);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;
        var iCount = 0;
        var jCount = 0;
        var kCount = 0;
        var firstPlane = true;
        var firstRow = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                    break;

                reader.TryConsumeArrayStart();

                var rowInPlaneCount = 0;
                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                        break;

                    reader.TryConsumeArrayStart();

                    var elemCount = 0;
                    while (reader.Read())
                    {
                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                            break;

                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ObjectEnd)
                            continue;
                        break;

                        if (tempIndex >= tempCapacity)
                        {
                            var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                            Array.Copy(tempArray, newArray, tempIndex);
                            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                            tempArray = newArray;
                            tempCapacity = newArray.Length;
                        }

                        parser.DeserializeJson(ref reader, ref tempArray[tempIndex]);
                        tempIndex++;
                        elemCount++;
                    }

                    if (firstRow)
                    {
                        kCount = elemCount;
                        firstRow = false;
                    }

                    rowInPlaneCount++;
                }

                if (firstPlane)
                {
                    jCount = rowInPlaneCount;
                    firstPlane = false;
                }

                iCount++;
            }

            if (iCount == 0 || jCount == 0 || kCount == 0)
            {
                value = new T[0, 0, 0];
                return;
            }

            value = new T[iCount, jCount, kCount];

            for (int i = 0; i < iCount; i++)
            {
                for (int j = 0; j < jCount; j++)
                {
                    for (int k = 0; k < kCount; k++)
                    {
                        value[i, j, k] = tempArray[i * jCount * kCount + j * kCount + k];
                    }
                }
            }
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

[Preserve]
public sealed class FourDimensionalArrayParser<T> : LuminPackParser<T?[,,,]>
{
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

        var totalLength = i * j * k * l;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);

            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;

            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength * kLength * lLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,,,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var iLength = value.GetLength(0);
        var jLength = value.GetLength(1);
        var kLength = value.GetLength(2);
        var lLength = value.GetLength(3);

        if (iLength == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        writer.WriteArrayStart();
        for (int i = 0; i < iLength; i++)
        {            writer.WriteArrayStart();
            for (int j = 0; j < jLength; j++)
            {                writer.WriteArrayStart();
                for (int k = 0; k < kLength; k++)
                {                    writer.WriteArrayStart();
                    bool isFirst = true;
                    for (int l = 0; l < lLength; l++)
                    {                        var temp = value[i, j, k, l];
                        if (!isFirst) writer.WriteByteRaw((byte)',');
                        else isFirst = false;
                        writer.SetFirstElement(true);
                        parser.SerializeJson(ref writer, ref temp);
                    }
                    writer.WriteArrayEnd();
                }
                writer.WriteArrayEnd();
            }
            writer.WriteArrayEnd();
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,,,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(2048);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;

        Span<int> dimCounts = stackalloc int[4];
        Span<bool> firstFlags = stackalloc bool[3];
        firstFlags[0] = firstFlags[1] = firstFlags[2] = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            var d0Count = 0;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                reader.TryConsumeArrayStart();
                var d1Count = 0;

                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                    reader.TryConsumeArrayStart();
                    var d2Count = 0;

                    while (reader.Read())
                    {
                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                        reader.TryConsumeArrayStart();
                        var d3Count = 0;

                        while (reader.Read())
                        {
                            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;

                            if (tempIndex >= tempCapacity)
                            {
                                var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                                Array.Copy(tempArray, newArray, tempIndex);
                                ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                                tempArray = newArray;
                                tempCapacity = newArray.Length;
                            }

                            parser.DeserializeJson(ref reader, ref tempArray[tempIndex++]);
                            d3Count++;
                        }

                        if (firstFlags[2]) { dimCounts[3] = d3Count; firstFlags[2] = false; }
                        d2Count++;
                    }

                    if (firstFlags[1]) { dimCounts[2] = d2Count; firstFlags[1] = false; }
                    d1Count++;
                }

                if (firstFlags[0]) { dimCounts[1] = d1Count; firstFlags[0] = false; }
                d0Count++;
            }

            dimCounts[0] = d0Count;

            if (d0Count == 0)
            {
                value = new T[0, 0, 0, 0];
                return;
            }

            value = new T[dimCounts[0], dimCounts[1], dimCounts[2], dimCounts[3]];
            var idx = 0;
            for (int i = 0; i < dimCounts[0]; i++)
            for (int j = 0; j < dimCounts[1]; j++)
            for (int k = 0; k < dimCounts[2]; k++)
            for (int l = 0; l < dimCounts[3]; l++)
                value[i, j, k, l] = tempArray[idx++];
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

[Preserve]
public sealed class FiveDimensionalArrayParser<T> : LuminPackParser<T?[,,,,]>
{
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

        var totalLength = i * j * k * l * m;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);

            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;

            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength * kLength * lLength * mLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,,,,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var iLength = value.GetLength(0);
        var jLength = value.GetLength(1);
        var kLength = value.GetLength(2);
        var lLength = value.GetLength(3);
        var mLength = value.GetLength(4);

        if (iLength == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        writer.WriteArrayStart();
        for (int i = 0; i < iLength; i++)
        {            writer.WriteArrayStart();
            for (int j = 0; j < jLength; j++)
            {                writer.WriteArrayStart();
                for (int k = 0; k < kLength; k++)
                {                    writer.WriteArrayStart();
                    for (int l = 0; l < lLength; l++)
                    {                        writer.WriteArrayStart();
                        bool isFirst = true;
                        for (int m = 0; m < mLength; m++)
                        {                            var temp = value[i, j, k, l, m];
                            if (!isFirst) writer.WriteByteRaw((byte)',');
                            else isFirst = false;
                            writer.SetFirstElement(true);
                            parser.SerializeJson(ref writer, ref temp);
                        }
                        writer.WriteArrayEnd();
                    }
                    writer.WriteArrayEnd();
                }
                writer.WriteArrayEnd();
            }
            writer.WriteArrayEnd();
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,,,,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(4096);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;

        Span<int> dimCounts = stackalloc int[5];
        Span<bool> firstFlags = stackalloc bool[4];
        for (int i = 0; i < 4; i++) firstFlags[i] = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            var d0 = 0;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                reader.TryConsumeArrayStart();
                var d1 = 0;

                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                    reader.TryConsumeArrayStart();
                    var d2 = 0;

                    while (reader.Read())
                    {
                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                        reader.TryConsumeArrayStart();
                        var d3 = 0;

                        while (reader.Read())
                        {
                            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                            reader.TryConsumeArrayStart();
                            var d4 = 0;

                            while (reader.Read())
                            {
                                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;

                                if (tempIndex >= tempCapacity)
                                {
                                    var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                                    Array.Copy(tempArray, newArray, tempIndex);
                                    ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                                    tempArray = newArray;
                                    tempCapacity = newArray.Length;
                                }

                                parser.DeserializeJson(ref reader, ref tempArray[tempIndex++]);
                                d4++;
                            }

                            if (firstFlags[3]) { dimCounts[4] = d4; firstFlags[3] = false; }
                            d3++;
                        }

                        if (firstFlags[2]) { dimCounts[3] = d3; firstFlags[2] = false; }
                        d2++;
                    }

                    if (firstFlags[1]) { dimCounts[2] = d2; firstFlags[1] = false; }
                    d1++;
                }

                if (firstFlags[0]) { dimCounts[1] = d1; firstFlags[0] = false; }
                d0++;
            }

            dimCounts[0] = d0;

            if (d0 == 0)
            {
                value = new T[0, 0, 0, 0, 0];
                return;
            }

            value = new T[dimCounts[0], dimCounts[1], dimCounts[2], dimCounts[3], dimCounts[4]];
            var idx = 0;
            for (int i = 0; i < dimCounts[0]; i++)
            for (int j = 0; j < dimCounts[1]; j++)
            for (int k = 0; k < dimCounts[2]; k++)
            for (int l = 0; l < dimCounts[3]; l++)
            for (int m = 0; m < dimCounts[4]; m++)
                value[i, j, k, l, m] = tempArray[idx++];
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

[Preserve]
public sealed class SixDimensionalArrayParser<T> : LuminPackParser<T?[,,,,,]>
{
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

        var totalLength = i * j * k * l * m * n;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);

            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;

            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength * kLength * lLength * mLength * nLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,,,,,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        Span<int> dims = stackalloc int[6];
        for (int d = 0; d < 6; d++) dims[d] = value.GetLength(d);

        if (dims[0] == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        writer.WriteArrayStart();
        for (int i = 0; i < dims[0]; i++)
        {            writer.WriteArrayStart();
            for (int j = 0; j < dims[1]; j++)
            {                writer.WriteArrayStart();
                for (int k = 0; k < dims[2]; k++)
                {                    writer.WriteArrayStart();
                    for (int l = 0; l < dims[3]; l++)
                    {                        writer.WriteArrayStart();
                        for (int m = 0; m < dims[4]; m++)
                        {                            writer.WriteArrayStart();
                            bool isFirst = true;
                            for (int n = 0; n < dims[5]; n++)
                            {                                var temp = value[i, j, k, l, m, n];
                                if (!isFirst) writer.WriteByteRaw((byte)',');
                                else isFirst = false;
                                writer.SetFirstElement(true);
                                parser.SerializeJson(ref writer, ref temp);
                            }
                            writer.WriteArrayEnd();
                        }
                        writer.WriteArrayEnd();
                    }
                    writer.WriteArrayEnd();
                }
                writer.WriteArrayEnd();
            }
            writer.WriteArrayEnd();
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,,,,,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(8192);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;

        Span<int> dimCounts = stackalloc int[6];
        Span<bool> firstFlags = stackalloc bool[5];
        for (int i = 0; i < 5; i++) firstFlags[i] = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            var d0 = 0;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                reader.TryConsumeArrayStart();
                var d1 = 0;

                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                    reader.TryConsumeArrayStart();
                    var d2 = 0;

                    while (reader.Read())
                    {
                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                        reader.TryConsumeArrayStart();
                        var d3 = 0;

                        while (reader.Read())
                        {
                            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                            reader.TryConsumeArrayStart();
                            var d4 = 0;

                            while (reader.Read())
                            {
                                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                                reader.TryConsumeArrayStart();
                                var d5 = 0;

                                while (reader.Read())
                                {
                                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;

                                    if (tempIndex >= tempCapacity)
                                    {
                                        var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                                        Array.Copy(tempArray, newArray, tempIndex);
                                        ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                                        tempArray = newArray;
                                        tempCapacity = newArray.Length;
                                    }

                                    parser.DeserializeJson(ref reader, ref tempArray[tempIndex++]);
                                    d5++;
                                }

                                if (firstFlags[4]) { dimCounts[5] = d5; firstFlags[4] = false; }
                                d4++;
                            }

                            if (firstFlags[3]) { dimCounts[4] = d4; firstFlags[3] = false; }
                            d3++;
                        }

                        if (firstFlags[2]) { dimCounts[3] = d3; firstFlags[2] = false; }
                        d2++;
                    }

                    if (firstFlags[1]) { dimCounts[2] = d2; firstFlags[1] = false; }
                    d1++;
                }

                if (firstFlags[0]) { dimCounts[1] = d1; firstFlags[0] = false; }
                d0++;
            }

            dimCounts[0] = d0;

            if (d0 == 0)
            {
                value = new T[0, 0, 0, 0, 0, 0];
                return;
            }

            value = new T[dimCounts[0], dimCounts[1], dimCounts[2], dimCounts[3], dimCounts[4], dimCounts[5]];
            var idx = 0;
            for (int i = 0; i < dimCounts[0]; i++)
            for (int j = 0; j < dimCounts[1]; j++)
            for (int k = 0; k < dimCounts[2]; k++)
            for (int l = 0; l < dimCounts[3]; l++)
            for (int m = 0; m < dimCounts[4]; m++)
            for (int n = 0; n < dimCounts[5]; n++)
                value[i, j, k, l, m, n] = tempArray[idx++];
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}

[Preserve]
public sealed unsafe class SevenDimensionalArrayParser<T> : LuminPackParser<T?[,,,,,,]>
{
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

        var totalLength = i * j * k * l * m * n * o;
        
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T?>())
        {
            var byteCount = Unsafe.SizeOf<T>() * totalLength;
            ref var src = ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value);
            ref var dest = ref writer.GetCurrentSpanReference();

            Unsafe.WriteUnaligned(ref dest, value.Length);
            Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dest, 4), ref src, (uint)byteCount);
            writer.Advance(byteCount + 4);
            writer.CheckBuffer();
        }
        else
        {
            writer.WriteCollectionHeader(ref index, value.Length);
            
            writer.Advance(4);
            
            var parser = writer.GetParser<T?>();
            
            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, totalLength);

            foreach (ref var item in span)
            {
                parser.Serialize(ref writer, ref item);
            }
            writer.CheckBuffer();
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
            var parser = LuminPackParseProvider.Cache<T?>.Parser!;

            ref var first = ref Unsafe.As<byte, T?>(ref LuminPackMarshal.DangerousGetArrayDataReference<T>(value));
            var span = LuminPackMarshal.CreateSpan(ref first, iLength * jLength * kLength * lLength * mLength * nLength * oLength);

            foreach (ref var v in span)
            {
                parser.Deserialize(ref reader, ref v);
            }
        }
    }

    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T?[,,,,,,]? value)
    {
        if (value is null)
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

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T?[,,,,,,]? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var dims = stackalloc int[7];
        for (int d = 0; d < 7; d++) dims[d] = value.GetLength(d);

        if (dims[0] == 0)
        {
            writer.WriteArrayStart();
            writer.WriteArrayEnd();
            return;
        }

        var parser = LuminPackParseProvider.Cache<T>.Parser!;

        writer.WriteArrayStart();
        for (int i = 0; i < dims[0]; i++)
        {            writer.WriteArrayStart();
            for (int j = 0; j < dims[1]; j++)
            {                writer.WriteArrayStart();
                for (int k = 0; k < dims[2]; k++)
                {                    writer.WriteArrayStart();
                    for (int l = 0; l < dims[3]; l++)
                    {                        writer.WriteArrayStart();
                        for (int m = 0; m < dims[4]; m++)
                        {                            writer.WriteArrayStart();
                            for (int n = 0; n < dims[5]; n++)
                            {                                writer.WriteArrayStart();
                                bool isFirst = true;
                                for (int o = 0; o < dims[6]; o++)
                                {                                    var temp = value[i, j, k, l, m, n, o];
                                    if (!isFirst) writer.WriteByteRaw((byte)',');
                                    else isFirst = false;
                                    writer.SetFirstElement(true);
                                    parser.SerializeJson(ref writer, ref temp);
                                }
                                writer.WriteArrayEnd();
                            }
                            writer.WriteArrayEnd();
                        }
                        writer.WriteArrayEnd();
                    }
                    writer.WriteArrayEnd();
                }
                writer.WriteArrayEnd();
            }
            writer.WriteArrayEnd();
        }
        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T?[,,,,,,]? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempArray = ArrayPool<T?>.Shared.Rent(16384);
        var tempCapacity = tempArray.Length;
        var tempIndex = 0;

        Span<int> dimCounts = stackalloc int[7];
        Span<bool>  firstFlags = stackalloc bool[6];
        for (int i = 0; i < 6; i++) firstFlags[i] = true;

        try
        {
            var parser = LuminPackParseProvider.Cache<T>.Parser!;
            var d0 = 0;

            while (reader.Read())
            {
                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                reader.TryConsumeArrayStart();
                var d1 = 0;

                while (reader.Read())
                {
                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                    reader.TryConsumeArrayStart();
                    var d2 = 0;

                    while (reader.Read())
                    {
                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                        reader.TryConsumeArrayStart();
                        var d3 = 0;

                        while (reader.Read())
                        {
                            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                            reader.TryConsumeArrayStart();
                            var d4 = 0;

                            while (reader.Read())
                            {
                                if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                                reader.TryConsumeArrayStart();
                                var d5 = 0;

                                while (reader.Read())
                                {
                                    if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;
                                    reader.TryConsumeArrayStart();
                                    var d6 = 0;

                                    while (reader.Read())
                                    {
                                        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd) break;

                                        if (tempIndex >= tempCapacity)
                                        {
                                            var newArray = ArrayPool<T?>.Shared.Rent(tempCapacity * 2);
                                            Array.Copy(tempArray, newArray, tempIndex);
                                            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                                            tempArray = newArray;
                                            tempCapacity = newArray.Length;
                                        }

                                        parser.DeserializeJson(ref reader, ref tempArray[tempIndex++]);
                                        d6++;
                                    }

                                    if (firstFlags[5]) { dimCounts[6] = d6; firstFlags[5] = false; }
                                    d5++;
                                }

                                if (firstFlags[4]) { dimCounts[5] = d5; firstFlags[4] = false; }
                                d4++;
                            }

                            if (firstFlags[3]) { dimCounts[4] = d4; firstFlags[3] = false; }
                            d3++;
                        }

                        if (firstFlags[2]) { dimCounts[3] = d3; firstFlags[2] = false; }
                        d2++;
                    }

                    if (firstFlags[1]) { dimCounts[2] = d2; firstFlags[1] = false; }
                    d1++;
                }

                if (firstFlags[0]) { dimCounts[1] = d1; firstFlags[0] = false; }
                d0++;
            }

            dimCounts[0] = d0;

            if (d0 == 0)
            {
                value = new T[0, 0, 0, 0, 0, 0, 0];
                return;
            }

            value = new T[dimCounts[0], dimCounts[1], dimCounts[2], dimCounts[3], dimCounts[4], dimCounts[5], dimCounts[6]];
            var idx = 0;
            for (int i = 0; i < dimCounts[0]; i++)
            for (int j = 0; j < dimCounts[1]; j++)
            for (int k = 0; k < dimCounts[2]; k++)
            for (int l = 0; l < dimCounts[3]; l++)
            for (int m = 0; m < dimCounts[4]; m++)
            for (int n = 0; n < dimCounts[5]; n++)
            for (int o = 0; o < dimCounts[6]; o++)
                value[i, j, k, l, m, n, o] = tempArray[idx++];
        }
        finally
        {
            ArrayPool<T?>.Shared.Return(tempArray, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}