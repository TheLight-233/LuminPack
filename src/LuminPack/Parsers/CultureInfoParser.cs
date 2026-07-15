using System.Globalization;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed class CultureInfoParser : LuminPackParser<CultureInfo>
{
    public static readonly bool IsInvariantMode = 
        ReferenceEquals(CultureInfo.CurrentCulture, CultureInfo.InvariantCulture) &&
        ReferenceEquals(CultureInfo.CurrentUICulture, CultureInfo.InvariantCulture);
    
    public static readonly string InvariantCultureName = CultureInfo.InvariantCulture.Name;
    
    // treat as string
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref CultureInfo? value)
    {
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            writer.Advance(1);
            return;
        }
        
        int offset = writer.WriteString(value.Name) + writer.StringRecordLength();
        
        writer.Advance(offset);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref CultureInfo? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (reader.PeekIsNullObject(ref index))
        {
            reader.Advance(1);
            value = null;
            return;
        }
        
        reader.ReadStringLength(ref index, out var length);
        
        var str  = reader.ReadString(length);
        
        var symbol = reader.StringRecordLength();
        
        reader.Advance(length + symbol);
        
        if (string.IsNullOrEmpty(str))
        {
            value = null;
        }
        else
        {
            if (IsInvariantMode)
            {
                value = str == InvariantCultureName ? CultureInfo.InvariantCulture : null;
            }
            else
            {
                value = CultureInfo.GetCultureInfo(str);
            }
        }
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref CultureInfo? value)
    {
        var str = value is null ? string.Empty : value.Name;
        evaluator += evaluator.GetStringLength(ref str);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref CultureInfo? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteString(value.Name);
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref CultureInfo? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        var str = reader.ReadString();
        if (IsInvariantMode)
        {
            value = str == InvariantCultureName ? CultureInfo.InvariantCulture : null;
        }
        else
        {
            value = CultureInfo.GetCultureInfo(str);
        }
    }
}
