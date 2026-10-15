using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using LuminPack.Attribute;

namespace LuminPack.Code
{
    /// <summary>Centralizes exception construction, throwing, and stack-preserving rethrows.</summary>
    [Preserve]
    public static class LuminPackExceptionHelper
    {
        /// <summary>Rethrows an exception after cleanup while preserving its original stack trace.</summary>
        /// <param name="exception">The exception caught by the caller.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Rethrow(Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        /// <summary>Provides lumin pack exception functionality.</summary>
        private class LuminPackException : Exception
        {
            /// <summary>Initializes a new instance of <c>LuminPackException</c>.</summary>
            /// <param name="message">The message describing the failure.</param>
            public LuminPackException(string message) : base(message) { }

            /// <summary>Initializes a new instance of <c>LuminPackException</c>.</summary>
            /// <param name="message">The message describing the failure.</param>
            /// <param name="innerException">The exception that caused this failure.</param>
            public LuminPackException(string message, Exception innerException) : base(message, innerException) { }
        }

        /// <summary>Raises the exception for argument exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        /// <param name="paramName">The name of the parameter that failed validation.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string message, string? paramName = null)
        {
            throw new ArgumentException(message, paramName);
        }

        /// <summary>Raises the exception for argument exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        /// <param name="paramName">The name of the parameter that failed validation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>This method never returns a value.</returns>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentException<T>(string message, string? paramName = null)
        {
            throw new ArgumentException(message, paramName);
        }

        /// <summary>Raises the exception for argument null exception.</summary>
        /// <param name="paramName">The name of the parameter that failed validation.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        /// <summary>Raises the exception for argument null exception.</summary>
        /// <param name="paramName">The name of the parameter that failed validation.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>This method never returns a value.</returns>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        /// <summary>Raises the exception for argument out of range exception.</summary>
        /// <param name="paramName">The name of the parameter that failed validation.</param>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string paramName, string? message = null)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        /// <summary>Raises the exception for invalid operation exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string? message = null)
        {
            throw new InvalidOperationException(message);
        }

        /// <summary>Raises the exception for invalid operation exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>This method never returns a value.</returns>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowInvalidOperationException<T>(string? message = null)
        {
            throw new InvalidOperationException(message);
        }

        /// <summary>Raises the exception for format exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFormatException(string message)
        {
            throw new FormatException(message);
        }

        /// <summary>Raises the exception for invalid data exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }

        /// <summary>Raises the exception for key not found exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowKeyNotFoundException(string? message = null)
        {
            throw message is null ? new System.Collections.Generic.KeyNotFoundException() : new System.Collections.Generic.KeyNotFoundException(message);
        }

        /// <summary>Raises the exception for key not found exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>This method never returns a value.</returns>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowKeyNotFoundException<T>(string? message = null)
        {
            throw message is null ? new System.Collections.Generic.KeyNotFoundException() : new System.Collections.Generic.KeyNotFoundException(message);
        }

        /// <summary>Raises the exception for not supported exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string? message = null)
        {
            throw new NotSupportedException(message);
        }

        /// <summary>Raises the exception for not supported exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
        /// <returns>This method never returns a value.</returns>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowNotSupportedException<T>(string? message = null)
        {
            throw new NotSupportedException(message);
        }

        /// <summary>Rejects mutation of an immutable collection with the standard parameterless exception message.</summary>
        /// <exception cref="NotSupportedException">The collection cannot be modified.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowReadOnlyCollectionMutation()
        {
            throw new NotSupportedException();
        }

        /// <summary>Rejects a value-returning mutation of an immutable collection with the standard exception message.</summary>
        /// <typeparam name="T">The return type required by the rejecting member.</typeparam>
        /// <returns>This method never returns.</returns>
        /// <exception cref="NotSupportedException">The collection cannot be modified.</exception>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowReadOnlyCollectionMutation<T>()
        {
            throw new NotSupportedException();
        }

        /// <summary>Raises the exception for platform not supported exception.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowPlatformNotSupportedException(string message)
        {
            throw new PlatformNotSupportedException(message);
        }

        /// <summary>Raises the exception for message.</summary>
        /// <param name="message">The message describing the failure.</param>
        [DoesNotReturn]
        public static void ThrowMessage(string message)
        {
            throw new LuminPackException(message);
        }

        /// <summary>Raises the exception for un supported data type.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        [DoesNotReturn]
        public static void ThrowUnSupportedDataType(Type type)
        {
            throw new LuminPackException($"The Type '{type}' are UnSupported. Are you Set LuminPackable?");
        }

        /// <summary>Raises the exception for null serialize object.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        [DoesNotReturn]
        public static void ThrowNullSerializeObject(Type type)
        {
            throw new LuminPackException($"Serializing Type '{type}' null reference.");
        }

        /// <summary>Raises the exception for null de serialize object.</summary>
        [DoesNotReturn]
        public static void ThrowNullDeSerializeObject()
        {
            throw new LuminPackException($"DeSerializing byte[] is null reference.");
        }

        /// <summary>Raises the exception for reached depth limit.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        [DoesNotReturn]
        public static void ThrowReachedDepthLimit(Type type)
        {
            throw new LuminPackException($"Serializing Type '{type}' reached depth limit, maybe detect circular reference.");
        }

        /// <summary>Raises the exception for span out of range.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        [DoesNotReturn]
        public static void ThrowSpanOutOfRange(int index)
        {
            throw new LuminPackException($"Span out of range with {index}.");
        }

        /// <summary>Raises the exception for failed encoding utf8.</summary>
        [DoesNotReturn]
        public static void ThrowFailedEncodingUtf8()
        {
            throw new LuminPackException($"Failed in Utf8 encoding/decoding with span<char>.");
        }

        /// <summary>Raises the exception for failed encoding utf16.</summary>
        [DoesNotReturn]
        public static void ThrowFailedEncodingUtf16()
        {
            throw new LuminPackException($"Failed in Utf16 encoding/decoding with span<char>.");
        }

        /// <summary>Raises the exception for failed parse string with token.</summary>
        [DoesNotReturn]
        public static void ThrowFailedParseStringWithToken()
        {
            throw new LuminPackException($"Failed in Parse string with Token Mode. Try Change the Mode.");
        }

        /// <summary>Raises the exception for failed parse string with length.</summary>
        [DoesNotReturn]
        public static void ThrowFailedParseStringWithLength()
        {
            throw new LuminPackException($"Failed in Parse string with Length Mode. Try Change the Mode.");
        }

        /// <summary>Raises the exception for in sufficient buffer.</summary>
        /// <param name="length">The number of bytes or elements to process.</param>
        [DoesNotReturn]
        public static void ThrowInSufficientBuffer(int length)
        {
            throw new LuminPackException($"Length header size is larger than buffer size, length: {length}.");
        }

        /// <summary>Raises the exception for invalid string length.</summary>
        /// <param name="length">The number of bytes or elements to process.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidStringLength(int length)
        {
            throw new InvalidDataException($"Invalid string length: {length}.");
        }

        /// <summary>Raises the exception for invalid collection length.</summary>
        /// <param name="length">The number of bytes or elements to process.</param>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCollectionLength(int length)
        {
            throw new InvalidDataException($"Invalid collection length: {length}.");
        }

        /// <summary>Raises the exception for collection modified during serialization.</summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowCollectionModifiedDuringSerialization()
        {
            throw new InvalidOperationException("Collection was modified during serialization.");
        }

        /// <summary>Raises the exception for negative multi dimensional array dimension.</summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNegativeMultiDimensionalArrayDimension()
        {
            throw new FormatException("A multidimensional array dimension cannot be negative.");
        }

        /// <summary>Raises the exception for multi dimensional array dimensions too large.</summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowMultiDimensionalArrayDimensionsTooLarge()
        {
            throw new FormatException("The multidimensional array dimensions are too large.");
        }

        /// <summary>Raises the exception for multi dimensional array length mismatch.</summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowMultiDimensionalArrayLengthMismatch()
        {
            throw new FormatException("The multidimensional array element count does not match its dimensions.");
        }

        /// <summary>Raises the exception for no source generated formatter.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        [DoesNotReturn]
        public static void ThrowNoSourceGeneratedFormatter(Type type)
        {
            throw new LuminPackException($"No source-generated formatter exists for type '{type}'.");
        }

        /// <summary>Raises the exception for invalid concurrrent collection operation.</summary>
        [DoesNotReturn]
        public static void ThrowInvalidConcurrrentCollectionOperation()
        {
            throw new LuminPackException($"ConcurrentCollection is Added/Removed in serializing, however serialize concurrent collection is not thread-safe.");
        }

        /// <summary>Raises the exception for invalid collection.</summary>
        [DoesNotReturn]
        public static void ThrowInvalidCollection()
        {
            throw new LuminPackException("Current read to collection, the buffer header is not collection.");
        }

        /// <summary>Raises the exception for deserialize object is null.</summary>
        /// <param name="target">The target used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowDeserializeObjectIsNull(string target)
        {
            throw new LuminPackException($"Deserialized {target} is null.");
        }

        /// <summary>Raises the exception for failed encoding.</summary>
        /// <param name="status">The status used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowFailedEncoding(OperationStatus status)
        {
            throw new LuminPackException($"Failed in Utf8 encoding/decoding process, status: {status}.");
        }

        /// <summary>Raises the exception for buffer writer no init.</summary>
        [DoesNotReturn]
        public static void ThrowBufferWriterNoInit()
        {
            throw new LuminPackException("ReusableLinkedArrayBufferWriter has no Init.");
        }

        /// <summary>Raises the exception for buffer writer null.</summary>
        [DoesNotReturn]
        public static void ThrowBufferWriterNull()
        {
            throw new LuminPackException("ReusableLinkedArrayBufferWriter is no Null.");
        }

        /// <summary>Raises the exception for not found in union type.</summary>
        /// <param name="actualType">The actual type used by this operation.</param>
        /// <param name="baseType">The base type used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowNotFoundInUnionType(Type actualType, Type baseType)
        {
            throw new LuminPackException($"Type {actualType.FullName} is not annotated in {baseType.FullName} LuminPackUnion.");
        }

        /// <summary>Raises the exception for not found in union type.</summary>
        /// <param name="tag">The tag used by this operation.</param>
        /// <param name="baseType">The base type used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowNotFoundInUnionType(ushort tag, Type baseType)
        {
            throw new LuminPackException($"Data read tag: {tag} but not found in {baseType.FullName} LuminPackUnion annotations.");
        }

        /// <summary>Raises the exception for invalid tag.</summary>
        /// <param name="tag">The tag used by this operation.</param>
        /// <param name="baseType">The base type used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowInvalidTag(ushort tag, Type baseType)
        {
            throw new LuminPackException($"Data read tag: {tag} but not found in {baseType.FullName} LuminPackUnion annotations.");
        }

        /// <summary>Raises the exception for invalid range.</summary>
        /// <param name="expected">The expected used by this operation.</param>
        /// <param name="actual">The actual used by this operation.</param>
        [DoesNotReturn]
        public static void ThrowInvalidRange(int expected, int actual)
        {
            throw new LuminPackException($"Requires size is {expected} but buffer length is {actual}.");
        }

        /// <summary>Raises the exception for json not supported.</summary>
        /// <param name="type">The type or symbol to analyze.</param>
        [DoesNotReturn]
        public static void ThrowJsonNotSupported(Type type)
        {
            throw new LuminPackException($"Type of {type} Json is not supported.");
        }
    }
}
