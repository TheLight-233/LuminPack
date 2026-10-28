namespace LuminPackUnitTest;

/// <summary>Reads a binary record into a destination whose static type selects the generated concrete operation.</summary>
/// <typeparam name="T">The destination type.</typeparam>
/// <param name="bytes">The record.</param>
/// <param name="value">The destination.</param>
internal delegate void ReadInto<T>(ReadOnlySpan<byte> bytes, ref T value);

/// <summary>Provides value-returning reads for tests without the registration-only generic fallback.</summary>
internal static class ConcreteReads
{
    /// <summary>Reads one record through the concrete operation named by the caller's lambda.</summary>
    /// <typeparam name="T">The destination type.</typeparam>
    /// <param name="bytes">The record.</param>
    /// <param name="read">A lambda such as <c>(b, ref v) =&gt; LuminPackSerializer.Deserialize(b, ref v)</c>, which binds at compile time.</param>
    /// <returns>The decoded value.</returns>
    /// <remarks>A generic <c>Deserialize&lt;T&gt;</c> call needs a registered formatter; the lambda is compiled against the concrete type instead.</remarks>
    internal static T Read<T>(ReadOnlySpan<byte> bytes, ReadInto<T> read)
    {
        T value = default!;
        read(bytes, ref value);
        return value;
    }
}
