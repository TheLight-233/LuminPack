using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

// Generated formatter owners are field-free readonly structs. Shared collection formatters take them as struct type arguments
// and call them as default(T).Write(...), which binds to the exact owner of each instantiation without boxing or dispatch.
// The members are instance members on every target: NativeAOT cannot compile static virtual calls in generic code shared
// over reference-type elements, and the empty receiver costs nothing once the call is inlined.

/// <summary>Writes one value to the binary state; implemented by generated formatter owners.</summary>
/// <typeparam name="T">The written value type.</typeparam>
public interface ILuminPackWriter<T>
{
    /// <summary>Writes one complete value with a compiled profile.</summary>
    /// <typeparam name="TOptions">The stateless compiled profile.</typeparam>
    /// <param name="writer">The binary writer state.</param>
    /// <param name="value">The value.</param>
    void Write<TOptions>(scoped ref LuminPackWriter writer, in T value) where TOptions : struct, ILuminPackOptions;
}

/// <summary>Reads one value from the binary state; implemented by generated formatter owners.</summary>
/// <typeparam name="T">The read value type.</typeparam>
public interface ILuminPackReader<T>
{
    /// <summary>Reads one complete value with a compiled profile, reusing the existing value where its contract allows.</summary>
    /// <typeparam name="TOptions">The stateless compiled profile.</typeparam>
    /// <param name="reader">The binary reader state.</param>
    /// <param name="value">The existing value and the result.</param>
    void Read<TOptions>(scoped ref LuminPackReader reader, scoped ref T value) where TOptions : struct, ILuminPackOptions;
}

/// <summary>Writes one JSON value; implemented by generated formatter owners.</summary>
/// <typeparam name="T">The written value type.</typeparam>
public interface ILuminPackJsonWriter<T>
{
    /// <summary>Writes one complete JSON value with a compiled profile.</summary>
    /// <typeparam name="TOptions">The stateless compiled profile.</typeparam>
    /// <param name="writer">The JSON writer state.</param>
    /// <param name="value">The value.</param>
    void Write<TOptions>(scoped ref LuminPackJsonWriter writer, in T value) where TOptions : struct, ILuminPackOptions;
}

/// <summary>Reads one JSON value; implemented by generated formatter owners.</summary>
/// <typeparam name="T">The read value type.</typeparam>
public interface ILuminPackJsonReader<T>
{
    /// <summary>Reads one complete JSON value with a compiled profile, reusing the existing value where its contract allows.</summary>
    /// <typeparam name="TOptions">The stateless compiled profile.</typeparam>
    /// <param name="reader">The JSON reader state.</param>
    /// <param name="value">The existing value and the result.</param>
    void Read<TOptions>(scoped ref LuminPackJsonReader reader, scoped ref T value) where TOptions : struct, ILuminPackOptions;
}

/// <summary>Writes a contiguous block of values in their packed or tagged storage layout; implemented by the generated owners
/// of laid-out structs so shared contiguous collections keep the closed block transfer.</summary>
/// <typeparam name="T">The element type.</typeparam>
public interface ILuminPackBlockWriter<T>
{
    /// <summary>Returns the wire width of a block, including its layout tag when the block is tagged and nonempty.</summary>
    /// <param name="count">The element count.</param>
    /// <returns>The byte width.</returns>
    int BlockWidth(int count);

    /// <summary>Writes a block whose capacity the caller ensured.</summary>
    /// <param name="writer">The binary writer state.</param>
    /// <param name="first">The first element.</param>
    /// <param name="count">The element count.</param>
    void WriteBlock(scoped ref LuminPackWriter writer, scoped ref T first, int count);
}

/// <summary>Reads a contiguous block of values from their packed or tagged storage layout; implemented by the generated owners
/// of laid-out structs.</summary>
/// <typeparam name="T">The element type.</typeparam>
public interface ILuminPackBlockReader<T>
{
    /// <summary>Validates a complete block before the caller allocates or mutates its destination.</summary>
    /// <param name="reader">The binary reader state.</param>
    /// <param name="count">The element count.</param>
    void ValidateBlock(scoped ref LuminPackReader reader, int count);

    /// <summary>Reads a validated block.</summary>
    /// <param name="reader">The binary reader state.</param>
    /// <param name="first">The first destination element.</param>
    /// <param name="count">The element count.</param>
    void ReadBlock(scoped ref LuminPackReader reader, scoped ref T first, int count);
}
