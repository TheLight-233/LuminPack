using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;

namespace LuminPack.Internal;

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
    
    public nint Length => _length;
    
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length is 0;
    }

    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        get => ref Unsafe.Add(ref _reference, (nint)(uint)index);
#else
        get => ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), (nint)(uint)index);
#endif
    }

    public ref T this[nint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        get => ref Unsafe.Add(ref _reference, index);
#else
        get => ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), index);
#endif
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(int offset)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.Add(ref _reference, offset);
#else
        return ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), offset);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Offset(nint offset)
    {
#if NET8_0_OR_GREATER
        return ref Unsafe.Add(ref _reference, offset);
#else
        return ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), offset);
#endif
    }
    
    [Obsolete("GetHashCode() on Span will always throw an exception.")]
    public override int GetHashCode() => throw new NotSupportedException();
    [Obsolete("Equals() on Span will always throw an exception. Use the equality operator instead.")]
    public override bool Equals(object obj) => throw new NotSupportedException();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString()
    {
        return $"System.LuminSpan<{typeof (T).Name}>[{_length}]";
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(T[] array) => new (array);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(in Span<T> span) => new (span);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator LuminSpan<T>(in ReadOnlySpan<T> span) => new (span);
    
    public static bool operator ==(LuminSpan<T> left, LuminSpan<T> right) => left._reference.Equals(right._reference);
    public static bool operator !=(LuminSpan<T> left, LuminSpan<T> right) => !left._reference.Equals(right._reference);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan<T> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(start));
        nint newLen = _length - start;
#if NET8_0_OR_GREATER
        return new LuminSpan<T>(ref Unsafe.Add(ref _reference, start), newLen);
#else
        return new LuminSpan<T>(ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), start), (int)newLen);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminSpan<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            throw new ArgumentOutOfRangeException(nameof(start));
#if NET8_0_OR_GREATER
        return new LuminSpan<T>(ref Unsafe.Add(ref _reference, start), length);
#else
        return new LuminSpan<T>(ref Unsafe.Add(ref Unsafe.AsRef<T>(_reference.ToPointer()), start), length);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan()
    {
#if NET8_0_OR_GREATER
        return MemoryMarshal.CreateSpan(ref _reference, (int)_length);
#else
        return new Span<T>(_reference.ToPointer(), (int)_length);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] ToArray()
    {
        return AsSpan().ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(in LuminSpan<T> destination)
    {
        AsSpan().CopyTo(destination.AsSpan());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination)
    {
        AsSpan().CopyTo(destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(in T value)
    {
        AsSpan().Fill(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);
    
    public ref struct Enumerator
    {
        private readonly LuminSpan<T> _span;
        private nint _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(in LuminSpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return ++_index < _span._length;
        }
        
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

public static class LuminSpanExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this T[] array) => new(array);
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this in Span<T> span) => new(span);
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LuminSpan<T> AsLuminSpan<T>(this in ReadOnlySpan<T> span) => new(span);
}
