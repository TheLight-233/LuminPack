using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Internal;

/// <summary>Provides lumin span functionality.</summary>
/// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
[StructLayout(LayoutKind.Sequential)]
#nullable disable
public readonly ref struct LuminSpan<T>
{
#if NET8_0_OR_GREATER
    internal readonly ref T _reference;
#else
    internal readonly Span<T> _reference;
#endif

    /// <summary>Gets the storage address on every target, including empty views.</summary>
    private ref T Reference
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        get => ref _reference;
#else
        get => ref MemoryMarshal.GetReference(_reference);
#endif
    }

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
        get => ref Unsafe.Add(ref Reference, (nint)(uint)index);
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="index">The position at which the operation starts.</param>
    public ref T this[nint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref Reference, index);
    }

    /// <summary>Initializes a new instance of <c>LuminSpan</c>.</summary>
    /// <param name="array">The array used by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan(T[] array)
    {
#if NET8_0_OR_GREATER
        _reference = ref MemoryMarshal.GetArrayDataReference(array);
#else
        _reference = MemoryMarshal.CreateSpan(ref LuminPackMarshal.GetArrayReference(array), 1);
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
        _reference = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), 1);
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
        _reference = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), 1);
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
        _reference = MemoryMarshal.CreateSpan(ref reference, 1);
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
        _reference = MemoryMarshal.CreateSpan(ref reference, 1);
#endif
        _length = length;
    }

    /// <summary>Obtains the reference for the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetReference() => ref _length == 0 ? ref Unsafe.NullRef<T>() : ref Reference;

    /// <summary>Performs the offset step for the current operation.</summary>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(int offset)
    {
        return ref Unsafe.Add(ref Reference, (nint)(offset));
    }

    /// <summary>Performs the offset step for the current operation.</summary>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(nint offset)
    {
        return ref Unsafe.Add(ref Reference, offset);
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
        Unsafe.AreSame(ref left.Reference, ref right.Reference);

    /// <summary>Applies the <c>!=</c> operator to the supplied operands.</summary>
    /// <param name="left">The left used by this operation.</param>
    /// <param name="right">The right used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool operator !=(LuminSpan<T> left, LuminSpan<T> right) =>
        !Unsafe.AreSame(ref left.Reference, ref right.Reference);

    /// <summary>Performs the slice step for the current operation.</summary>
    /// <param name="start">The start used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan<T> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(start));

        nint newLen = _length - start;
        return new LuminSpan<T>(ref Unsafe.Add(ref Reference, unchecked((nint)(uint)(start))), newLen);
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

        return new LuminSpan<T>(ref Unsafe.Add(ref Reference, unchecked((nint)(uint)(start))), length);
    }

    /// <summary>Performs the as span step for the current operation.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan()
    {
        return MemoryMarshal.CreateSpan(ref Reference, (int)_length);
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
        internal Enumerator(scoped in LuminSpan<T> span)
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
            get => ref Unsafe.Add(ref _span.Reference, _index);
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
