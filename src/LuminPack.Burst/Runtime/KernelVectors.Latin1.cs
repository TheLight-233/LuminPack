#if LUMINPACK_BURST
using System;
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
namespace LuminPack.Burst
{
    internal unsafe partial struct Vector128<T> where T : unmanaged
    {
        /// <summary>Tests complete vector bits for equality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether every lane matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector128<T> left, Vector128<T> right) => Vector128.EqualsAll(left, right);
        /// <summary>Tests complete vector bits for inequality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether a lane differs.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector128<T> left, Vector128<T> right) => !Vector128.EqualsAll(left, right);
        /// <summary>Compares boxed values in managed diagnostic execution.</summary><param name="other">Candidate.</param><returns>Whether equal.</returns>
        public override bool Equals(object other) => other is Vector128<T> vector && this == vector;
        /// <summary>Gets a managed diagnostic hash.</summary><returns>The first word's hash.</returns>
        public override int GetHashCode() => (int)Value.UInt0;
        /// <summary>Extracts the first lane.</summary><returns>The first lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T ToScalar() => GetElement(0);
    }
    internal static unsafe partial class Vector128
    {
        /// <summary>Widens each logical half in order.</summary><param name="source">Bytes.</param><returns>The low and high word vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector128<ushort>, Vector128<ushort>) Widen(Vector128<byte> source) => (WidenLower(source), WidenUpper(source));
    }
    internal unsafe partial struct Vector256<T> where T : unmanaged
    {
        /// <summary>Tests complete vector bits for equality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether every lane matches.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector256<T> left, Vector256<T> right) => Vector256.EqualsAll(left, right);
        /// <summary>Tests complete vector bits for inequality.</summary><param name="left">First.</param><param name="right">Second.</param><returns>Whether a lane differs.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector256<T> left, Vector256<T> right) => !Vector256.EqualsAll(left, right);
        /// <summary>Compares boxed values in managed diagnostic execution.</summary><param name="other">Candidate.</param><returns>Whether equal.</returns>
        public override bool Equals(object other) => other is Vector256<T> vector && this == vector;
        /// <summary>Gets a managed diagnostic hash.</summary><returns>The first word's hash.</returns>
        public override int GetHashCode() => (int)Value.UInt0;
        /// <summary>Extracts the first lane.</summary><returns>The first lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T ToScalar() => GetElement(0);
    }
    internal static unsafe partial class Vector256
    {
        /// <summary>Widens each logical half in order.</summary><param name="source">Bytes.</param><returns>The low and high word vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Vector256<ushort>, Vector256<ushort>) Widen(Vector256<byte> source) => (WidenLower(source), WidenUpper(source));
    }
    internal static unsafe partial class Vector128
    {
        /// <summary>Places a scalar in the low bytes without reading beyond its storage.</summary><typeparam name="T">Scalar type.</typeparam><param name="value">Scalar.</param><returns>The vector with defined low lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> CreateScalarUnsafe<T>(T value) where T : unmanaged { v128 result = default; *(T*)&result = value; return new Vector128<T> { Value = result }; }
    }
}
#endif

