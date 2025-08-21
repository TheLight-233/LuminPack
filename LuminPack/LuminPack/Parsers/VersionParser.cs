using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class VersionParser : LuminPackParser<Version>
{
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Version? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value is null)
        {
            writer.WriteNullObjectHeader(ref index);
            
            writer.Advance(1);
            
            return;
        }

        writer.WriteUnmanaged(ref index, value.Major, value.Minor, value.Build, value.Revision);
        
        writer.Advance(16);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Version? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (!reader.TryReadObjectHead(ref index))
        {
            value = null;
            
            reader.Advance(1);
            
            return;
        }
        
        reader.ReadUnmanaged(ref index, out int major, out int minor, out int build, out int revision);

        // when use new Version(major, minor), build and revision will be -1, it can not use constructor.
        if (revision == -1)
        {
            if (build == -1)
            {
                value = new Version(major, minor);
            }
            else
            {
                value = new Version(major, minor, build);
            }
        }
        else
        {
            value = new Version(major, minor, build, revision);
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Version? value)
    {
        if (value == null)
        {
            evaluator += 1;
            
            return;
        }
        
        evaluator += 16;
    }
}