using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Interface;

[Preserve]
public interface ILuminPackableParser
{
    [Preserve]
    public void Serialize(ref LuminPackWriter writer, scoped ref object? value);
    
    [Preserve]
    public void Deserialize(ref LuminPackReader reader, scoped ref object? value);
}

[Preserve]
public interface ILuminPackableParser<T>
{
    [Preserve]
    public void Serialize(ref LuminPackWriter writer, scoped ref T? value);
    
    [Preserve]
    public void Deserialize(ref LuminPackReader reader, scoped ref T? value);
}