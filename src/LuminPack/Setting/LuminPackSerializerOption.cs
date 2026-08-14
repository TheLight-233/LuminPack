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
        public static readonly LuminPackSerializerOption Utf16 =  Default with { StringEncoding = LuminPackStringEncoding.UTF16 };
        /// <summary>Gets a predefined option instance that records strings with terminator tokens.</summary>
        public static readonly LuminPackSerializerOption Token = Default with { StringRecording = LuminPackStringRecording.Token };
        /// <summary>Gets a predefined option instance that records strings with length prefixes.</summary>
        public static readonly LuminPackSerializerOption Length = Default with { StringRecording = LuminPackStringRecording.Length };

        /// <summary>Gets a predefined option instance that uses UTF-16 and length-prefixed strings.</summary>
        public static readonly LuminPackSerializerOption Utf16WithLength = new ()
        {
            StringEncoding =  LuminPackStringEncoding.UTF16,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ResetToDefault()
        {
            StringEncoding = LuminPackStringEncoding.UTF8;
            StringRecording = LuminPackStringRecording.Length;
            StandardFormat = new StandardFormat('G');
            MaxJsonDepth = 1024;
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
    
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        private ReferenceEqualityComparer() { }

        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
