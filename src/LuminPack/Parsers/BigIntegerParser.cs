using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class BigIntegerParser : LuminPackParser<BigInteger>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref BigInteger value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
#if !UNITY_2021_2_OR_NEWER
        Span<byte> temp = stackalloc byte[255];
        if (value.TryWriteBytes(temp, out var written))
        {
            writer.WriteUnmanagedSpan(ref index, temp[..written], out var offset);
            
            writer.Advance(offset);
            
            return;
        }
        else
#endif
        {
            var byteArray = value.ToByteArray();
            
            writer.WriteUnmanagedArray(ref index, byteArray, out var offset);
            
            writer.Advance(offset);
        }
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref BigInteger value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = default;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        ref var src = ref reader.GetSpanReference(index);
        value = new BigInteger(MemoryMarshal.CreateReadOnlySpan(ref src, length));

        reader.Advance(length);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref BigInteger value)
    {
#if !UNITY_2021_2_OR_NEWER
        Span<byte> temp = stackalloc byte[255];
        if (value.TryWriteBytes(temp, out var written))
        {
            var tempSpan = temp.Slice(written);

            if (temp.IsEmpty)
            {
                evaluator += 4;
                
                return;
            }
        
            evaluator += 4 + tempSpan.Length * Unsafe.SizeOf<byte>();
            
            return;
        }
        else
#endif
        {
            var byteArray = value.ToByteArray();
            
            evaluator.CalculateArray(ref byteArray);
            
        }
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref BigInteger value)
    {
        writer.WriteString(value.ToString());
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref BigInteger value)
    {
        var str = reader.ReadString();
        value = BigInteger.Parse(str);
    }
}
