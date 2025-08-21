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
    private static partial Regex ShortTypeNameRegex();

#else

    static readonly Regex _shortTypeNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=[\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})", RegexOptions.Compiled);
    static Regex ShortTypeNameRegex() => _shortTypeNameRegex;

#endif
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref Type? value)
    {
        ref var index = ref writer.GetCurrentSpanOffset();
        
        var full = value?.AssemblyQualifiedName;
        
        if (full is null)
        {
            writer.WriteNullStringHeader(ref index, out var offset);
                
            writer.Advance(offset);
            
            return;
        }

        var shortName = ShortTypeNameRegex().Replace(full, "");

        var length = writer.GetStringLength(shortName);
        
        writer.WriteString(shortName, length);
        
        var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;
        
        writer.Advance(length + symbol);
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref Type? value)
    {
        ref var index = ref reader.GetCurrentSpanOffset();
        
        reader.ReadStringLength(ref index, out var length);
        
        if (reader.Option.StringRecording is LuminPackStringRecording.Length)
            reader.Advance(4);
        
        var typeName = reader.ReadString(index, length) ?? string.Empty;
        
        if (typeName == string.Empty)
        {
            value = null;
            return;
        }

        value = Type.GetType(typeName, throwOnError: true);
        
        var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 0;
        
        reader.Advance(length + symbol);
        
    }

    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref Type? value)
    {
        var full = value?.AssemblyQualifiedName;

        evaluator.GetStringLength(ref full);
    }
}