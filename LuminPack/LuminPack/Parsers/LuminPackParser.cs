using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Interface;

namespace LuminPack.Parsers;

[Preserve]
public abstract class LuminPackParser<T> : ILuminPackableParser<T>, ILuminPackableParser, ILuminPackEvaluator<T>
{

    protected LuminPackParser(){}
    
    [Preserve]
    public abstract void Serialize(ref LuminPackWriter writer, scoped ref T? value);
    
    [Preserve]
    public abstract void Deserialize(ref LuminPackReader reader, scoped ref T? value);
    
    [Preserve]
    void ILuminPackableParser.Serialize(ref LuminPackWriter writer, scoped ref object? value)
    {
        var v = (value is null)
            ? default
            : (T?)value;
        Serialize(ref writer, ref v);
    }

    [Preserve]
    void ILuminPackableParser.Deserialize(ref LuminPackReader reader, scoped ref object? value)
    {
        var v = (value is null)
            ? default
            : (T?)value;
        Deserialize(ref reader, ref v);
        value = v;
    }

    [Preserve]
    public abstract void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value);
}