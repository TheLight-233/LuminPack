using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LuminPack.Internal
{
    public static class XxHash3
    {
        private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
        private const ulong Prime64_3 = 0x165667B19E3779F9UL;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Hash64Utf8(ReadOnlySpan<byte> data)
        {
            if (data.Length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;
            
            ref byte dataRef = ref MemoryMarshal.GetReference(data);
            fixed (byte* ptr = &dataRef)
            {
                return ComputeHash(ptr, data.Length);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Hash64Utf16(ReadOnlySpan<char> chars)
        {
            if (chars.Length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;
            
            ref char charRef = ref MemoryMarshal.GetReference(chars);
            ref byte byteRef = ref Unsafe.As<char, byte>(ref charRef);
            fixed (byte* ptr = &byteRef)
            {
                return ComputeHash(ptr, chars.Length * 2);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ulong ComputeHash(byte* input, int length)
        {
            if (length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;
                
            if (length <= 16)
            {
                if (length > 8)
                {
                    ulong low = *(ulong*)input ^ (11400714819323198549UL);
                    ulong high = *(ulong*)(input + length - 8) ^ (2870177450012600325UL);
                    ulong hash = low + high + (ulong)length;
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }
                
                if (length >= 4)
                {
                    ulong val1 = *(uint*)input;
                    ulong val2 = *(uint*)(input + length - 4);
                    ulong hash = (2870177450012600261UL + val1 + ((val2 << 32) | val2));
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }
                
                byte c1 = input[0];
                byte c2 = input[length >> 1];
                byte c3 = input[length - 1];
                uint combined = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)length << 8);
                ulong hash2 = (11400714819323198485UL + combined);
                hash2 ^= hash2 >> 37;
                hash2 *= Prime64_3;
                hash2 ^= hash2 >> 32;
                return hash2;
            }
            
            ulong acc = (ulong)length * Prime64_1;
            int remaining = length;
            byte* ptr = input;
            
            while (remaining >= 16)
            {
                ulong v1 = *(ulong*)ptr;
                ulong v2 = *(ulong*)(ptr + 8);
                acc += v1 * Prime64_1;
                acc += v2 * Prime64_3;
                ptr += 16;
                remaining -= 16;
            }
            
            if (remaining > 0)
            {
                if (remaining >= 8)
                {
                    acc += *(ulong*)ptr * Prime64_1;
                    ptr += 8;
                    remaining -= 8;
                }
                if (remaining >= 4)
                {
                    acc += *(uint*)ptr * Prime64_3;
                    ptr += 4;
                    remaining -= 4;
                }
                while (remaining > 0)
                {
                    acc += *ptr * Prime64_1;
                    ptr++;
                    remaining--;
                }
            }
            
            acc ^= acc >> 37;
            acc *= Prime64_3;
            acc ^= acc >> 32;
            return acc;
        }
    }
}