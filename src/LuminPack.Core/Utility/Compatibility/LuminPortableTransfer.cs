using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Utility;

/// <summary>Preserves the generated-code entry points now named <see cref="LuminPortable{T,TLayout}"/>.</summary>
/// <typeparam name="T">The reference-free portable storage type.</typeparam>
/// <typeparam name="TLayout">The generated layout description.</typeparam>
/// <remarks>Existing generated consumers remain binary compatible. New generation uses <see cref="LuminPortable{T,TLayout}"/>.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPortableTransfer<T, TLayout> where TLayout : struct, ILuminPortableLayout
{
    /// <summary>Packs complete leading elements through the canonical implementation.</summary>
    /// <param name="source">The first complete storage element.</param>
    /// <param name="count">The validated element count.</param>
    /// <param name="destination">The first byte of the reserved packed output.</param>
    /// <returns>The completed prefix element count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Pack(ref T source, int count, ref byte destination) => LuminPortable<T, TLayout>.Pack(ref source, count, ref destination);

    /// <summary>Unpacks complete leading elements through the canonical implementation.</summary>
    /// <param name="source">The first byte of the validated packed input.</param>
    /// <param name="count">The validated element count.</param>
    /// <param name="destination">The first complete destination storage element.</param>
    /// <returns>The completed prefix element count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Unpack(ref byte source, int count, ref T destination) => LuminPortable<T, TLayout>.Unpack(ref source, count, ref destination);
}
