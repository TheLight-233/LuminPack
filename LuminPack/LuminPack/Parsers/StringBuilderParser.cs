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
    public override void Serialize(ref LuminPackWriter writer, scoped ref StringBuilder? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullCollectionHeader(ref index);
            
            writer.Advance(4);
            
            return;
        }

#if NET7_0_OR_GREATER

        // for performance reason, currently StringBuilder encode as Utf16, however try to write Utf8?
        
        writer.WriteCollectionHeader(ref index, value.Length);
            
        writer.Advance(4);

        foreach (var chunk in value.GetChunks())
        {
            ref var p = ref writer.GetSpanReference(checked(chunk.Length * 2));
            ref var src = ref LuminPackMarshal.As<char, byte>(ref MemoryMarshal.GetReference(chunk.Span));
            Unsafe.CopyBlockUnaligned(ref p, ref src, (uint)chunk.Length * 2);

            writer.Advance(chunk.Length * 2);
        }
        return;
        

#else
        // write as utf16
        writer.WriteUtf16WithLength(index, value.ToString());
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
        ref var p = ref reader.GetSpanReference(size);
        var src = LuminPackMarshal.CreateSpan(ref Unsafe.As<byte, char>(ref p), length);
        value.Append(src);

        reader.Advance(size);
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref StringBuilder? value)
    {

        if (value is null || value.Length == 0)
        {
            evaluator += 4;
            
            return;
        }
        
        evaluator += Encoding.Unicode.GetByteCount(value.ToString()) + 4;
    }
}