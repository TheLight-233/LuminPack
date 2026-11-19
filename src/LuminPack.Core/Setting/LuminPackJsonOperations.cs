using LuminPack.Core;

namespace LuminPack.Option;

/// <summary>Reads a value using the code unit selected by the complete JSON document.</summary>
/// <typeparam name="TResult">The decoded value type.</typeparam>
public interface ILuminPackJsonReadOperation<TResult>
{
    /// <summary>Reads the value from the current document.</summary>
    /// <typeparam name="TCodeUnit">Byte for UTF-8, or char for UTF-16.</typeparam>
    /// <param name="reader">The active reader.</param>
    /// <returns>The decoded value.</returns>
    TResult Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged;
}

/// <summary>Writes a value using the code unit selected by the complete JSON document.</summary>
/// <typeparam name="TValue">The value type.</typeparam>
public interface ILuminPackJsonWriteOperation<TValue>
{
    /// <summary>Writes the value to the current document.</summary>
    /// <typeparam name="TCodeUnit">Byte for UTF-8, or char for UTF-16.</typeparam>
    /// <param name="writer">The active writer.</param>
    /// <param name="value">The value to encode.</param>
    void Write<TCodeUnit>(scoped ref LuminPackJsonWriter writer, TValue value) where TCodeUnit : unmanaged;
}
