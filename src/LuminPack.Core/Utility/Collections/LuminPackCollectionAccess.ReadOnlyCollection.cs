#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Refills the exact array backing an existing read-only wrapper after a full payload proof.</summary>
    /// <typeparam name="T">The raw element type.</typeparam>
    /// <param name="reader">The reader just after the count.</param>
    /// <param name="value">The existing wrapper.</param>
    /// <param name="length">The validated element count.</param>
    /// <returns>Whether the existing array was filled.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryReadArrayBackedWrapper<T>(scoped ref LuminPackReader reader, ReadOnlyCollection<T>? value, int length)
        where T : unmanaged
    {
        if (value is null || value.GetType() != typeof(ReadOnlyCollection<T>))
            return false;

        int offset = ReadOnlyWrapperArray<T>.Offset;
        if (offset < 0)
            return false;

        var items = LuminFieldLayout.Field<IList<T>>(value, offset);
        if (items?.GetType() != typeof(T[]))
            return false;

        var array = Unsafe.As<T[]>(items);
        if (array.Length != length)
            return false;

        Span<byte> target = MemoryMarshal.AsBytes(array.AsSpan());
        if (target.Overlaps(reader.Buffer))
            return false;

        reader.Read(ref MemoryMarshal.GetReference(target), target.Length);
        return true;
    }

    /// <summary>Gets the exact array backing an existing read-only wrapper so its elements can be read in place.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The existing wrapper.</param>
    /// <param name="length">The validated element count.</param>
    /// <returns>The backing array when the wrapper is an exact <see cref="ReadOnlyCollection{T}"/> over an exact array of
    /// <paramref name="length"/> elements; otherwise <see langword="null"/>.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[]? GetArrayBackedWrapperItems<T>(ReadOnlyCollection<T>? value, int length)
    {
        if (value is null || value.GetType() != typeof(ReadOnlyCollection<T>))
            return null;

        int offset = ReadOnlyWrapperArray<T>.Offset;
        if (offset < 0)
            return null;

        // The exact type check excludes arrays of a derived element type, which would reject stores.
        var items = LuminFieldLayout.Field<IList<T>>(value, offset);
        if (items?.GetType() != typeof(T[]))
            return null;

        var array = Unsafe.As<T[]>(items);
        return array.Length == length ? array : null;
    }

    /// <summary>Checks the field on the exact closed wrapper and confirms its backing array.</summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    private static class ReadOnlyWrapperArray<T>
    {
        /// <summary>The verified offset or an unavailable marker.</summary>
        internal static readonly int Offset = FindOffset();

        /// <summary>Locates and verifies the exact framework backing field.</summary>
        /// <returns>The verified offset or an unavailable marker.</returns>
        private static int FindOffset()
        {
            var type = typeof(ReadOnlyCollection<T>);
            if (!RuntimeFeature.IsDynamicCodeCompiled || type.Assembly != typeof(object).Assembly ||
                LuminFieldLayout.Find(type, "list") is not { IsInitOnly: true } field || field.FieldType != typeof(IList<T>))
                return LuminFieldLayout.Unavailable;

            var array = Array.Empty<T>();
            int offset = LuminFieldLayout.ObjectField(type, "list", array);
            return offset >= 0 && ReferenceEquals(array, LuminFieldLayout.Field<IList<T>>(new ReadOnlyCollection<T>(array), offset))
                ? offset
                : LuminFieldLayout.Unavailable;
        }
    }
}
#endif
