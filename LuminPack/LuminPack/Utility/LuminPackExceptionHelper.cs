using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace LuminPack.Code
{
    public sealed class LuminPackExceptionHelper
    {
        private class LuminPackException : Exception
        {
            public LuminPackException(string message) : base(message) { }
            
            public LuminPackException(string message, Exception innerException) : base(message, innerException) { }
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
        public static void ThrowNoParserRegistered(Type type)
        {
            throw new LuminPackException($"There are no this type of parser Registered, type: {type}.");
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
        public static void ThrowNotFoundInUnionType(Type actualType, Type baseType)
        {
            throw new LuminPackException($"Type {actualType.FullName} is not annotated in {baseType.FullName} LuminPackUnion.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidTag(ushort tag, Type baseType)
        {
            throw new LuminPackException($"Data read tag: {tag} but not found in {baseType.FullName} MemoryPackUnion annotations.");
        }
        
        [DoesNotReturn]
        public static void ThrowInvalidRange(int expected, int actual)
        {
            throw new LuminPackException($"Requires size is {expected} but buffer length is {actual}.");
        }
    }
}