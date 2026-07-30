using System.Collections;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class BitArrayParser : LuminPackParser<BitArray>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref BitArray? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
                
            writer.Advance(4);
                
            return;
        }
        
        int offset;
#if NET8_0_OR_GREATER
        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        writer.WriteCollectionHeader(ref index, view.m_length);
        writer.WriteUnmanagedArrayWithOutHeader(ref index, view.m_array, view.m_array.Length, out offset);
#else
        var length = value.Length;
        var wordCount = (length + 31) >> 5;
        writer.WriteCollectionHeader(ref index, length);
        var words = System.Buffers.ArrayPool<int>.Shared.Rent(Math.Max(wordCount, 1));
        try
        {
            if (wordCount != 0)
                value.CopyTo(words, 0);
            writer.WriteUnmanagedArrayWithOutHeader(ref index, words, wordCount, out offset);
        }
        finally
        {
            System.Buffers.ArrayPool<int>.Shared.Return(words);
        }
#endif
        
        writer.Advance(4 + offset);
        writer.CheckBuffer();
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref BitArray? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
                
            reader.Advance(4);
                
            return;
        }

        reader.Advance(4);

#if NET8_0_OR_GREATER
        if (value is null || value.Length != length)
            value = new BitArray(length, false);
        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        reader.ReadUnmanagedArray(ref index, ref view.m_array!, view.m_array.Length, out var offset);
        reader.Advance(offset);
#else
        var wordCount = (length + 31) >> 5;
        var words = new int[wordCount];
        reader.ReadUnmanagedArray(ref index, ref words, wordCount, out var offset);
        reader.Advance(offset);
        value = new BitArray(words) { Length = length };
#endif
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref BitArray? value)
    {
        if (value is null)
        {
            evaluator += sizeof(int);
            
            return;
        }
        
#if NET8_0_OR_GREATER
        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        evaluator.CalculateArray(ref view.m_array);
#else
        evaluator += checked(sizeof(int) + (((value.Length + 31) >> 5) * sizeof(int)));
#endif
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref BitArray? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteArrayStart();

        if (value.Count > 0)
        {
            for (int i = 0; i < value.Count; i++)
            {
                writer.WriteBool(value[i]);
            }
        }

        writer.WriteArrayEnd();
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref BitArray? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        reader.TryConsumeArrayStart();

        var tempList = new List<bool>();

        while (reader.Read())
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.ArrayEnd)
                break;

            tempList.Add(reader.GetBoolean());
        }

        if (tempList.Count > 0)
        {
            value = new BitArray(tempList.Count);
            for (int i = 0; i < tempList.Count; i++)
            {
                value[i] = tempList[i];
            }
        }
        else
        {
            value = new BitArray(0);
        }
    }
}

[Preserve]
public sealed class BitArrayView
{
    public int[] m_array;
    public int m_length;
    public int _version;
}
