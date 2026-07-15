using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class StringBuilderParser : LuminPackParser<StringBuilder>
{
    [Preserve]
    public override unsafe void Serialize(ref LuminPackWriter writer, scoped ref StringBuilder? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

#if NET8_0_OR_GREATER

        // for performance reason, currently StringBuilder encode as Utf16, however try to write Utf8?
        
        writer.WriteCollectionHeader(ref index, value.Length);
            
        writer.Advance(4);

        foreach (var chunk in value.GetChunks())
        {
            int length = checked(chunk.Length * 2);
            ref var p = ref Unsafe.Add(ref writer._bufferStart, (nint)(uint)index);
            ref var src = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<char, byte>(chunk.Span));
            Unsafe.CopyBlockUnaligned(ref p, ref src, (uint)length);

            writer.Advance(length);
        }
        writer.CheckBuffer();

#else
        // write as utf16
        writer.WriteUtf16WithLength(index, value.ToString());
        writer.CheckBuffer();
#endif
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref StringBuilder? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadCollectionHead(ref index, out var length))
        {
            value = null;
            
            reader.Advance(4);
            
            return;
        }

        reader.Advance(4);
        
        if (value is null)
        {
            value = new StringBuilder(length);
        }
        else
        {
            value.Clear();
            value.EnsureCapacity(length);
        }
        
        var size = checked(length * 2);
        ref var p = ref reader.GetSpanReference(index);
        var src = LuminPackMarshal.CreateSpan(ref p, size);
        value.Append(MemoryMarshal.Cast<byte, char>(src));

        reader.Advance(size);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref StringBuilder? value)
    {

        if (value is null || value.Length == 0)
        {
            evaluator += evaluator.StringRecordLength();
            
            return;
        }
        
        evaluator += Encoding.Unicode.GetByteCount(value.ToString()) + evaluator.StringRecordLength();
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref StringBuilder? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteString(value.ToString());
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref StringBuilder? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        var str = reader.ReadString();
        
        if (value is null)
        {
            value = new StringBuilder(str);
        }
        else
        {
            value.Clear();
            value.Append(str);
        }
    }
}