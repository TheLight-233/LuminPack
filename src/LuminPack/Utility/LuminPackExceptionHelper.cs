using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using LuminPack.Attribute;

namespace LuminPack.Code
{
    [Preserve]
    public static class LuminPackExceptionHelper
    {
        private class LuminPackException : Exception
        {
            public LuminPackException(string message) : base(message) { }
            
            public LuminPackException(string message, Exception innerException) : base(message, innerException) { }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string message, string? paramName = null)
        {
            throw new ArgumentException(message, paramName);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentException<T>(string message, string? paramName = null)
        {
            throw new ArgumentException(message, paramName);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string paramName, string? message = null)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string? message = null)
        {
            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowInvalidOperationException<T>(string? message = null)
        {
            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFormatException(string message)
        {
            throw new FormatException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowKeyNotFoundException(string? message = null)
        {
            throw message is null ? new System.Collections.Generic.KeyNotFoundException() : new System.Collections.Generic.KeyNotFoundException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowKeyNotFoundException<T>(string? message = null)
        {
            throw message is null ? new System.Collections.Generic.KeyNotFoundException() : new System.Collections.Generic.KeyNotFoundException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string? message = null)
        {
            throw new NotSupportedException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowNotSupportedException<T>(string? message = null)
        {
            throw new NotSupportedException(message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowPlatformNotSupportedException(string message)
        {
            throw new PlatformNotSupportedException(message);
        }

        [DoesNotReturn]
        public static void ThrowMessage(string message)
        {
            throw new LuminPackException(message);
        }

        [DoesNotReturn]
        public static void ThrowUnSupportedDataType(Type type)
        {
            throw new LuminPackException($"The Type '{type}' are UnSupported. Are you Set LuminPackable?");
        }
        
        [DoesNotReturn]
        public static void ThrowNullSerializeObject(Type type)
        {
            throw new LuminPackException($"Serializing Type '{type}' null reference.");
        }
        
        [DoesNotReturn]
        public static void ThrowNullDeSerializeObject()
        {
            throw new LuminPackException($"DeSerializing byte[] is null reference.");
        }
        
        [DoesNotReturn]
        public static void ThrowReachedDepthLimit(Type type)
        {
            throw new LuminPackException($"Serializing Type '{type}' reached depth limit, maybe detect circular reference.");
        }

        [DoesNotReturn]
        public static void ThrowSpanOutOfRange(int index)
        {
            throw new LuminPackException($"Span out of range with {index}.");
        }
        
        [DoesNotReturn]
        public static void ThrowFailedEncodingUtf8()
        {
            throw new LuminPackException($"Failed in Utf8 encoding/decoding with span<char>.");
        }
        
        [DoesNotReturn]
        public static void ThrowFailedEncodingUtf16()
        {
            throw new LuminPackException($"Failed in Utf16 encoding/decoding with span<char>.");
        }

        [DoesNotReturn]
        public static void ThrowFailedParseStringWithToken()
        {
            throw new LuminPackException($"Failed in Parse string with Token Mode. Try Change the Mode.");
        }

        [DoesNotReturn]
        public static void ThrowFailedParseStringWithLength()
        {
            throw new LuminPackException($"Failed in Parse string with Length Mode. Try Change the Mode.");
        }
        
        [DoesNotReturn]
        public static void ThrowInSufficientBuffer(int length)
        {
            throw new LuminPackException($"Length header size is larger than buffer size, length: {length}.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidStringLength(int length)
        {
            throw new InvalidDataException($"Invalid string length: {length}.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidCollectionLength(int length)
        {
            throw new InvalidDataException($"Invalid collection length: {length}.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowCollectionModifiedDuringSerialization()
        {
            throw new InvalidOperationException("Collection was modified during serialization.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNegativeMultiDimensionalArrayDimension()
        {
            throw new FormatException("A multidimensional array dimension cannot be negative.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowMultiDimensionalArrayDimensionsTooLarge()
        {
            throw new FormatException("The multidimensional array dimensions are too large.");
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowMultiDimensionalArrayLengthMismatch()
        {
            throw new FormatException("The multidimensional array element count does not match its dimensions.");
        }

        [DoesNotReturn]
        public static void ThrowNoSourceGeneratedFormatter(Type type)
        {
            throw new LuminPackException($"No source-generated formatter exists for type '{type}'.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidConcurrrentCollectionOperation()
        {
            throw new LuminPackException($"ConcurrentCollection is Added/Removed in serializing, however serialize concurrent collection is not thread-safe.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidCollection()
        {
            throw new LuminPackException("Current read to collection, the buffer header is not collection.");
        }
        
        [DoesNotReturn]
        public static void ThrowDeserializeObjectIsNull(string target)
        {
            throw new LuminPackException($"Deserialized {target} is null.");
        }

        [DoesNotReturn]
        public static void ThrowFailedEncoding(OperationStatus status)
        {
            throw new LuminPackException($"Failed in Utf8 encoding/decoding process, status: {status}.");
        }
        
        [DoesNotReturn]
        public static void ThrowBufferWriterNoInit()
        {
            throw new LuminPackException("ReusableLinkedArrayBufferWriter has no Init.");
        }
        
        [DoesNotReturn]
        public static void ThrowBufferWriterNull()
        {
            throw new LuminPackException("ReusableLinkedArrayBufferWriter is no Null.");
        }
        
        [DoesNotReturn]
        public static void ThrowNotFoundInUnionType(Type actualType, Type baseType)
        {
            throw new LuminPackException($"Type {actualType.FullName} is not annotated in {baseType.FullName} LuminPackUnion.");
        }
        
        [DoesNotReturn]
        public static void ThrowNotFoundInUnionType(ushort tag, Type baseType)
        {
            throw new LuminPackException($"Data read tag: {tag} but not found in {baseType.FullName} LuminPackUnion annotations.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidTag(ushort tag, Type baseType)
        {
            throw new LuminPackException($"Data read tag: {tag} but not found in {baseType.FullName} LuminPackUnion annotations.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidRange(int expected, int actual)
        {
            throw new LuminPackException($"Requires size is {expected} but buffer length is {actual}.");
        }

        [DoesNotReturn]
        public static void ThrowJsonNotSupported(Type type)
        {
            throw new LuminPackException($"Type of {type} Json is not supported.");
        }
    }
}
