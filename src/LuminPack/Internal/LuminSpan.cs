using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Internal;

/// <summary>Provides lumin span functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[StructLayout(LayoutKind.Sequential)]
#nullable disable
public readonly unsafe ref struct LuminSpan<T>
{
#if NET8_0_OR_GREATER
    internal readonly ref T _reference;
#else
    internal readonly IntPtr _reference;
    private readonly object _dummy;
#endif

    internal readonly nint _length;

    /// <summary>Gets the length.</summary>
    public nint Length => _length;

    /// <summary>Gets the is empty.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length is 0;
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="index">The position at which the operation starts.</param>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        get => ref Unsafe.Add(ref _reference, (nint)(uint)index);
#else
        get => ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), (nint)(uint)index);
#endif
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="index">The position at which the operation starts.</param>
    public ref T this[nint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        get => ref Unsafe.Add(ref _reference, index);
#else
        get => ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), index);
#endif
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="array">The array used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(T[] array)
    {
#if NET8_0_OR_GREATER
        _reference = ref MemoryMarshal.GetArrayDataReference(array);
#else
        _reference = (IntPtr)Unsafe.AsPointer(ref LuminPackMarshal.GetArrayDataReference(array));
        _dummy = array;
#endif
        _length = array.Length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(in Span<T> span)
    {
#if NET8_0_OR_GREATER
        _reference = ref MemoryMarshal.GetReference(span);
#else
        _reference = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
        _dummy = null;
#endif
        _length = span.Length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(in ReadOnlySpan<T> span)
    {
#if NET8_0_OR_GREATER
        _reference = ref MemoryMarshal.GetReference(span);
#else
        _reference = (IntPtr)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
        _dummy = null;
#endif
        _length = span.Length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="reference">The reference used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(ref T reference, int length)
    {
#if NET8_0_OR_GREATER
        _reference = ref reference;
#else
        _reference = (IntPtr)Unsafe.AsPointer(ref reference);
        _dummy = null;
#endif
        _length = length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="reference">The reference used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(ref T reference, nint length)
    {
#if NET8_0_OR_GREATER
        _reference = ref reference;
#else
        _reference = (IntPtr)Unsafe.AsPointer(ref reference);
        _dummy = null;
#endif
        _length = length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="ptr">The ptr used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(void* ptr, int length)
    {
#if NET8_0_OR_GREATER
        _reference = ref Unsafe.AsRef<T>(ptr);
#else
        _reference = (IntPtr)ptr;
        _dummy = null;
#endif
        _length = length;
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="ptr">The ptr used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(void* ptr, nint length)
    {
#if NET8_0_OR_GREATER
        _reference = ref Unsafe.AsRef<T>(ptr);
#else
        _reference = (IntPtr)ptr;
        _dummy = null;
#endif
        _length = length;
    }

    /// <summary>Obtains the reference for the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetReference()
    {
        ref T local = ref Unsafe.NullRef<T>();
        if (_length != 0)
#if NET8_0_OR_GREATER
            local = _reference;
#else
            local = ref Unsafe.AsRef<T>(_reference.ToPointer());
#endif

        return ref local;
    }

    /// <summary>Performs the offset step for the current operation.</summary>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(int offset)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.Add(ref _reference, offset);
#else
        return ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), offset);
#endif
    }

    /// <summary>Performs the offset step for the current operation.</summary>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(nint offset)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.Add(ref _reference, offset);
#else
        return ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), offset);
#endif
    }

    /// <summary>Rejects object-based hashing for a stack-only span.</summary>
    /// <returns>This method never returns.</returns>
    /// <exception cref="NotSupportedException">Object-based hashing is not supported.</exception>
    [Obsolete("GetHashCode() on Span will always throw an exception.")]
    public override int GetHashCode() =>
        LuminPackExceptionHelper.ThrowNotSupportedException<int>();

    /// <summary>Rejects object-based equality; compare spans through their equality operators.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <returns>This method never returns.</returns>
    /// <exception cref="NotSupportedException">Object-based equality is not supported.</exception>
    [Obsolete("Equals() on Span will always throw an exception. Use the equality operator instead.")]
    public override bool Equals(object obj) =>
        LuminPackExceptionHelper.ThrowNotSupportedException<bool>();

    /// <summary>Performs the to string step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        return $"System.LuminSpan<{typeof(T).Name}>[{_length}]";
    }

    /// <summary>Converts the supplied value to <c>LuminSpan&lt;T&gt;</c>.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(T[] array) => new(array);

    /// <summary>Converts the supplied value to <c>LuminSpan&lt;T&gt;</c>.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(in Span<T> span) => new(span);

    /// <summary>Converts the supplied value to <c>LuminSpan&lt;T&gt;</c>.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(in ReadOnlySpan<T> span) => new(span);

    /// <summary>Applies the <c>==</c> operator to the supplied operands.</summary>
    /// <param name="left">The left used by this operation.</param>
    /// <param name="right">The right used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool operator ==(LuminSpan<T> left, LuminSpan<T> right) =>
        left._reference.Equals(right._reference);

    /// <summary>Applies the <c>!=</c> operator to the supplied operands.</summary>
    /// <param name="left">The left used by this operation.</param>
    /// <param name="right">The right used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool operator !=(LuminSpan<T> left, LuminSpan<T> right) =>
        !left._reference.Equals(right._reference);

    /// <summary>Performs the slice step for the current operation.</summary>
    /// <param name="start">The start used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan<T> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(start));

        nint newLen = _length - start;
#if NET8_0_OR_GREATER
        return new LuminSpan<T>(ref Unsafe.Add(ref _reference, start), newLen);
#else
        return new LuminSpan<T>(ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), start), (int)newLen);
#endif
    }

    /// <summary>Performs the slice step for the current operation.</summary>
    /// <param name="start">The start used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(start));

#if NET8_0_OR_GREATER
        return new LuminSpan<T>(ref Unsafe.Add(ref _reference, start), length);
#else
        return new LuminSpan<T>(ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), start), length);
#endif
    }

    /// <summary>Performs the as span step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan()
    {
#if NET8_0_OR_GREATER
        return MemoryMarshal.CreateSpan(ref _reference, (int)_length);
#else
        return new Span<T>(_reference.ToPointer(), (int)_length);
#endif
    }

    /// <summary>Performs the to array step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        return AsSpan().ToArray();
    }

    /// <summary>Copies to to the supplied destination.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in LuminSpan<T> destination)
    {
        AsSpan().CopyTo(destination.AsSpan());
    }

    /// <summary>Copies to to the supplied destination.</summary>
    /// <param name="destination">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination)
    {
        AsSpan().CopyTo(destination);
    }

    /// <summary>Performs the fill step for the current operation.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(in T value)
    {
        AsSpan().Fill(value);
    }

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    /// <summary>Provides enumerator functionality.</summary>
    public ref struct Enumerator
    {
        private readonly LuminSpan<T> _span;
        private nint _index;

        /// <summary>Initializes a new instance of <c>Enumerator</c>.</summary>
        /// <param name="span">The buffer used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(in LuminSpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return ++_index < _span._length;
        }

        /// <summary>Gets the current.</summary>
        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
            get => ref Unsafe.Add(ref _span._reference, _index);
#else
            get => ref Unsafe.Add(ref Unsafe.AsRef<T>(_span._reference.ToPointer()), _index);
#endif
        }
    }
}

/// <summary>Provides lumin span extension functionality.</summary>
public static class LuminSpanExtension
{
    /// <summary>Performs the as lumin span step for the current operation.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this T[] array) => new(array);

    /// <summary>Performs the as lumin span step for the current operation.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this in Span<T> span) => new(span);

    /// <summary>Performs the as lumin span step for the current operation.</summary>
    /// <param name="span">The buffer used by this operation.</param>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this in ReadOnlySpan<T> span) => new(span);
}
