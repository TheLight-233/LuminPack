using System.Runtime.CompilerServices;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class UnmanagedParsers<T> : LuminPackParser<T> 
    where T : unmanaged
{
    [Preserve]
    private static UnmanagedParsers<T> Instance { get; } = 
        new UnmanagedParsers<T>();

    static UnmanagedParsers()
    {
        LuminPackParseProvider.RegisterParsers(Instance);
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T value)
    {
        Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);
        writer.Advance(Unsafe.SizeOf<T>());
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T value)
    {
        value = Unsafe.ReadUnaligned<T>(ref reader.GetCurrentSpanReference());
        reader.Advance(Unsafe.SizeOf<T>());
    }
    
    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T value)
    {
        evaluator += Unsafe.SizeOf<T>();
    }
}

[Preserve]
public sealed class DangerousUnmanagedParsers<T> : LuminPackParser<T>
    where T : unmanaged
{

    [Preserve]
    private static DangerousUnmanagedParsers<T> Instance { get; } = 
        new DangerousUnmanagedParsers<T>();

    static DangerousUnmanagedParsers()
    {
        LuminPackParseProvider.RegisterParsers(Instance);
    }
    
    [Preserve]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T value)
    {
        Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);
        writer.Advance(Unsafe.SizeOf<T>());
    }

    [Preserve]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T value)
    {
        value = Unsafe.ReadUnaligned<T>(ref reader.GetCurrentSpanReference());
        reader.Advance(Unsafe.SizeOf<T>());
    }
    
    [Preserve]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T value)
    {
        evaluator += Unsafe.SizeOf<T>();
    }
}