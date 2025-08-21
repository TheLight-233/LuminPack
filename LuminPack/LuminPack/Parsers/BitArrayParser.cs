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
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }

        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        
        writer.WriteUnmanagedArray(ref index, view.m_array, out var offset);
        
        writer.Advance(offset);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref BitArray? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }

        reader.ReadUnmanaged(out int length);

        reader.Advance(4);
        
        var bitArray = new BitArray(length, false); // create internal int[] and set m_length to length

        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref bitArray);
        
        reader.ReadUnmanagedArray(ref index, ref view.m_array!, length, out var offset);

        reader.Advance(offset);
        
        value = bitArray;
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref BitArray? value)
    {
        if (value is null)
        {
            evaluator += 1;
            
            return;
        }
        
        ref var view = ref LuminPackMarshal.As<BitArray, BitArrayView>(ref value);
        
        evaluator.CalculateArray(ref view.m_array);
    }
}

#pragma warning disable CS8618
[Preserve]
internal class BitArrayView
{
    public int[] m_array;
    public int m_length;
    public int _version;
}