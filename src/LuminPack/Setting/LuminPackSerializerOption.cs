using System.Buffers;
using System.Runtime.CompilerServices;

namespace LuminPack.Option
{
    /// <summary>
    /// Configures LuminPack binary and JSON serialization behavior.
    /// </summary>
    /// <remarks>
    /// Convenience serializer APIs accept an instance for a single call. High-performance APIs that accept
    /// a <see cref="Utility.LuminBufferWriter"/> use the option instance owned by that buffer writer instead.
    /// </remarks>
    public record LuminPackSerializerOption
    {
        internal static readonly LuminPackSerializerOption InternalDefault =
            new LuminPackSerializerOption
            {
                StringEncoding = LuminPackStringEncoding.UTF8,
                StringRecording = LuminPackStringRecording.Length,
                StandardFormat = new StandardFormat('G'),
            };

        /// <summary>Gets the predefined option instance initialized with LuminPack defaults.</summary>
        public static readonly LuminPackSerializerOption Default = InternalDefault with { };

        /// <summary>Gets a predefined option instance that uses UTF-8 string encoding.</summary>
        public static readonly LuminPackSerializerOption Utf8 = Default with { StringEncoding = LuminPackStringEncoding.UTF8 };
        /// <summary>Gets a predefined option instance that uses UTF-16 string encoding.</summary>
        public static readonly LuminPackSerializerOption Utf16 = Default with { StringEncoding = LuminPackStringEncoding.UTF16 };
        /// <summary>Gets a predefined option instance that records strings with terminator tokens.</summary>
        public static readonly LuminPackSerializerOption Token = Default with { StringRecording = LuminPackStringRecording.Token };
        /// <summary>Gets a predefined option instance that records strings with length prefixes.</summary>
        public static readonly LuminPackSerializerOption Length = Default with { StringRecording = LuminPackStringRecording.Length };

        /// <summary>Gets a predefined option instance that uses UTF-16 and length-prefixed strings.</summary>
        public static readonly LuminPackSerializerOption Utf16WithLength = new()
        {
            StringEncoding = LuminPackStringEncoding.UTF16,
            StringRecording = LuminPackStringRecording.Length,
            StandardFormat = new StandardFormat('G'),
        };


        /// <summary>Gets or sets the encoding used for string payloads.</summary>
        public LuminPackStringEncoding StringEncoding { get; set; } = LuminPackStringEncoding.UTF8;

        /// <summary>Gets or sets whether strings use length prefixes or terminator tokens.</summary>
        public LuminPackStringRecording StringRecording { get; set; } = LuminPackStringRecording.Length;

        /// <summary>Gets or sets the standard format used by supported formatted values.</summary>
        public StandardFormat StandardFormat { get; set; } = new('G');

        /// <summary>
        /// Gets or sets the maximum JSON nesting depth accepted during deserialization.
        /// Defaults to 1024; values of 0 or less disable the limit.
        /// </summary>
        public int MaxJsonDepth { get; set; } = 1024;

        /// <summary>
        /// Gets or sets whether the JSON reader validates the full RFC 8259 grammar (number syntax, separators,
        /// bracket matching, trailing commas, token boundaries) in addition to converting values.
        /// <para>The default is <c>false</c>, which is the trusted fast path meant for documents produced by
        /// LuminPack itself: values are still parsed with correct rounding, but malformed shapes are not
        /// rejected, which is what kept JSON deserialization about twice as fast as System.Text.Json before the
        /// grammar validation was added.</para>
        /// <para>Set it to <c>true</c> when the JSON comes from an untrusted or hand-written source; the reader
        /// then rejects <c>01</c>, <c>1.</c>, <c>+1</c>, <c>[1 2]</c>, <c>{"a" 1}</c>, <c>[1,]</c>, mismatched
        /// brackets, trailing data and similar shapes.</para>
        /// </summary>
        public bool ValidateJsonSyntax { get; set; }

        /// <summary>Performs the reset to default step for the current operation.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetToDefault()
        {
            StringEncoding = LuminPackStringEncoding.UTF8;
            StringRecording = LuminPackStringRecording.Length;
            StandardFormat = new StandardFormat('G');
            MaxJsonDepth = 1024;
            ValidateJsonSyntax = false;
        }

    }

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
