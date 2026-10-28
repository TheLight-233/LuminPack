#if LUMINPACK_BURST
using System.Runtime.InteropServices;
using LuminPack.Core;
using Unity.Burst;
namespace LuminPack.Burst
{
    public static unsafe partial class BurstBackend
    {
        /// <summary>Compares two complete ordinary byte ranges.</summary><param name="left">First.</param><param name="right">Second.</param><param name="count">Bytes.</param><returns>One for equality.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int EqualFunction(byte* left, byte* right, uint count);
        /// <summary>Encodes a complete bounded UTF-16 window.</summary><param name="source">Input.</param><param name="length">Characters.</param><param name="destination">Output.</param><param name="capacity">Bytes.</param><param name="consumed">Consumed characters.</param><param name="written">Produced bytes.</param><param name="flags">Policies.</param><returns>OperationStatus.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Utf8EncodeFunction(ushort* source, int length, byte* destination, int capacity, int* consumed, int* written, uint flags);
        /// <summary>Decodes a complete bounded UTF-8 window.</summary><param name="source">Input.</param><param name="length">Bytes.</param><param name="destination">Output.</param><param name="capacity">Characters.</param><param name="consumed">Consumed bytes.</param><param name="written">Produced characters.</param><param name="flags">Policies.</param><returns>OperationStatus.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Utf8DecodeFunction(byte* source, int length, ushort* destination, int capacity, int* consumed, int* written, uint flags);
        /// <summary>Compares complete valid UTF-8 text.</summary><param name="source">Input.</param><param name="length">Bytes.</param><param name="candidate">Candidate.</param><param name="characters">Characters.</param><returns>One for equality.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int Utf8EqualFunction(byte* source, int length, ushort* candidate, int characters);
        /// <summary>Encodes bounded ASCII with a no-write refusal contract.</summary><param name="source">Input.</param><param name="length">Characters.</param><param name="destination">Output.</param><param name="flags">JSON escaping policy.</param><returns>One on success.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] public delegate int AsciiEncodeFunction(ushort* source, int length, byte* destination, uint flags);
        private static readonly EqualFunction EqualRoot = BurstKernels.Equal;
        private static readonly Utf8EncodeFunction EncodeRoot = BurstKernels.Utf8Encode;
        private static readonly Utf8DecodeFunction DecodeRoot = BurstKernels.Utf8Decode;
        private static readonly Utf8EqualFunction TextEqualRoot = BurstKernels.Utf8Equal;
        private static readonly AsciiEncodeFunction AsciiRoot = BurstKernels.AsciiEncode;
        /// <summary>Compiles every text pointer before the registration table is published.</summary><param name="table">The unpublished table.</param><returns>Whether all pointers were created.</returns>
        private static bool CompileTextKernels(ref LuminPackBurst.KernelTable table)
        {
            var equal = BurstCompiler.CompileFunctionPointer<EqualFunction>(EqualRoot);
            var encode = BurstCompiler.CompileFunctionPointer<Utf8EncodeFunction>(EncodeRoot);
            var decode = BurstCompiler.CompileFunctionPointer<Utf8DecodeFunction>(DecodeRoot);
            var textEqual = BurstCompiler.CompileFunctionPointer<Utf8EqualFunction>(TextEqualRoot);
            var ascii = BurstCompiler.CompileFunctionPointer<AsciiEncodeFunction>(AsciiRoot);
            if (!equal.IsCreated || !encode.IsCreated || !decode.IsCreated || !textEqual.IsCreated || !ascii.IsCreated) return false;
            table.Equal = (delegate* unmanaged[Cdecl]<byte*, byte*, uint, int>)(void*)equal.Value;
            table.Utf8Encode = (delegate* unmanaged[Cdecl]<char*, int, byte*, int, int*, int*, uint, int>)(void*)encode.Value;
            table.Utf8Decode = (delegate* unmanaged[Cdecl]<byte*, int, char*, int, int*, int*, uint, int>)(void*)decode.Value;
            table.Utf8Equal = (delegate* unmanaged[Cdecl]<byte*, int, char*, int, int>)(void*)textEqual.Value;
            table.AsciiEncode = (delegate* unmanaged[Cdecl]<char*, int, byte*, uint, int>)(void*)ascii.Value;
            return true;
        }
    }
}
#endif

