#if LUMINPACK_BURST
using System.Runtime.CompilerServices;
namespace LuminPack.Burst
{
/// <summary>Maps complete Core vector loads to native vector loads instead of aggregate reconstruction.</summary>
internal static unsafe class KernelVectorMemory
{
/// <summary>Loads a complete unaligned 128-bit vector.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><returns>The vector.</returns>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static Vector128<T> Read128<T>(void* source)where T:unmanaged=>Vector128.Load((T*)source);
/// <summary>Loads a complete unaligned 128-bit vector.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><returns>The vector.</returns>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static Vector128<T> Read128<T>(ref byte source)where T:unmanaged{fixed(byte* p=&source)return Read128<T>(p);}
/// <summary>Loads a complete unaligned 256-bit vector.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><returns>The vector.</returns>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static Vector256<T> Read256<T>(void* source)where T:unmanaged=>Vector256.Load((T*)source);
/// <summary>Loads a complete unaligned 256-bit vector.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><returns>The vector.</returns>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static Vector256<T> Read256<T>(ref byte source)where T:unmanaged{fixed(byte* p=&source)return Read256<T>(p);}
}
}
#endif

