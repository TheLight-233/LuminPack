using System.Runtime.CompilerServices;

namespace LuminPack.Option
{

    /// <summary>Specifies the encoding used for serialized strings.</summary>
    public enum LuminPackStringEncoding : byte
    {
        /// <summary>Encode strings as UTF-8.</summary>
        UTF8,
        /// <summary>Encode strings as UTF-16.</summary>
        UTF16,
    }

    /// <summary>Specifies how the serialized string boundary is represented.</summary>
    public enum LuminPackStringRecording : byte
    {
        /// <summary>Store the string length before its payload.</summary>
        Length,
        /// <summary>Terminate the string payload with an encoding-specific token.</summary>
        Token,
    }

    /// <summary>Provides reference equality comparer functionality.</summary>
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>Initializes a new instance of <c>ReferenceEqualityComparer</c>.</summary>
        private ReferenceEqualityComparer() { }

        /// <summary>Gets the instance.</summary>
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

        /// <summary>Determines whether the supplied value is equal to the current value.</summary>
        /// <param name="x">The x used by this operation.</param>
        /// <param name="y">The y used by this operation.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        /// <summary>Computes the hash code used by the configured lookup or equality strategy.</summary>
        /// <param name="obj">The obj used by this operation.</param>
        /// <returns>The result of the operation.</returns>
        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
