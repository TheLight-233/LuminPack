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
