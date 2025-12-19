using System.Text.RegularExpressions;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Parsers;

[Preserve]
public sealed partial class TypeParser : LuminPackParser<Type>
{
#if NET7_0_OR_GREATER

    [GeneratedRegex(@", Version=\d+.\d+.\d+.\d+, Culture=[\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})")]
    public static partial Regex ShortTypeNameRegex();

#else

    static readonly Regex _shortTypeNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=[\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})", RegexOptions.Compiled);
    public static Regex ShortTypeNameRegex() => _shortTypeNameRegex;

#endif
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Type? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        if (value == null)
        {
            writer.WriteNullObjectHeader();
            writer.Advance(1);
            return;
        }
        
        var full = value.AssemblyQualifiedName;
        
        if (string.IsNullOrEmpty(full))
        {
            writer.WriteNullStringHeader(ref index, out var offset2);
                
            writer.Advance(offset2);
            
            return;
        }
        
        var shortName = ShortTypeNameRegex().Replace(full, "");

        int offset = writer.WriteString(shortName) + writer.StringRecordLength();
        
        writer.Advance(offset);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Type? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        if (reader.PeekIsNullObject(ref index))
        {
            reader.Advance(1);
            value = null;
            return;
        }
        
        reader.ReadStringLength(ref index, out var length);
        
        var typeName  = reader.ReadString(length);
        
        var symbol = reader.StringRecordLength();
        
        reader.Advance(length + symbol);
        
        if (string.IsNullOrEmpty(typeName))
        {
            value = null;
            return;
        }

        value = Type.GetType(typeName, throwOnError: true);
        
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Type? value)
    {
        var full = value?.AssemblyQualifiedName;

        evaluator.GetStringLength(ref full);
    }

    [Preserve]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref Type? value)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var full = value.AssemblyQualifiedName;
        
        if (full is null)
        {
            writer.WriteNull();
            return;
        }

        var shortName = ShortTypeNameRegex().Replace(full, "");
        writer.WriteString(shortName);
    }

    [Preserve]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref Type? value)
    {
        if (reader.IsNull())
        {
            value = null;
            return;
        }

        var typeName = reader.ReadString();
        value = Type.GetType(typeName, throwOnError: true);
    }
}
