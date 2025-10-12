using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Interface;

namespace LuminPack.Parsers;

[Preserve]
public abstract class LuminPackParser<T> : ILuminPackableParser<T>, ILuminPackableParser, ILuminPackEvaluator<T>
{

    protected LuminPackParser(){}
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Serialize(ref LuminPackWriter writer, scoped ref T? value);
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void Deserialize(ref LuminPackReader reader, scoped ref T? value);
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILuminPackableParser.Serialize(ref LuminPackWriter writer, scoped ref object? value)
    {
        var v = (value is null)
            ? default
            : (T?)value;
        Serialize(ref writer, ref v);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ILuminPackableParser.Deserialize(ref LuminPackReader reader, scoped ref object? value)
    {
        var v = (value is null)
            ? default
            : (T?)value;
        Deserialize(ref reader, ref v);
        value = v;
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public abstract void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T? value);
}