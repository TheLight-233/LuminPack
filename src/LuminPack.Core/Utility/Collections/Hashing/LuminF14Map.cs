using LuminPack.Code;

namespace LuminPack.Utility;

#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Numerics;


/// <summary>
/// F14 hash table: Facebook's F14 algorithm - 14-slot chunks with SIMD-accelerated lookup.
/// Performance: Faster than Dictionary, competitive with native F14.
/// </summary>
/// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
/// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public sealed class LuminF14Map<TKey, TValue> : IDisposable
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged
{
    private const int CHUNK_SIZE = 14;
    private const int MAX_PROBE = 12;
    private const double MAX_LOAD_FACTOR = 12.0 / 14.0;
    private const byte EMPTY = 0x80;
    private const ushort VALID_SLOTS_MASK = 0x3FFF;

    /// <summary>Provides chunk functionality.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    private struct Chunk
    {
        /// <summary>Stores the tags buffer.</summary>
        private Vector128<byte> _tags;

        /// <summary>Obtains the tags for the supplied input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector128<byte> GetTags() => _tags;

        /// <summary>Updates the tags used by the current operation.</summary>
        /// <param name="vec">The vec used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTags(Vector128<byte> vec) => _tags = vec;

        /// <summary>Obtains the tag for the supplied input.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetTag(int index)
        {
            return Unsafe.Add(ref Unsafe.As<Vector128<byte>, byte>(ref _tags), index);
        }

        /// <summary>Updates the tag used by the current operation.</summary>
        /// <param name="index">The position at which the operation starts.</param>
        /// <param name="value">The value consumed or produced by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag(int index, byte value)
        {
            Unsafe.Add(ref Unsafe.As<Vector128<byte>, byte>(ref _tags), index) = value;
        }
    }

    private nint _chunks;
    private nint _keys;
    private nint _values;
    private int _chunkCount;
    private int _chunkMask;
    private int _count;

    /// <summary>Gets the count.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Gets the capacity.</summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkCount * CHUNK_SIZE;
    }

    /// <summary>Gets the is created.</summary>
    public bool IsCreated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunks != 0;
    }

    /// <summary>Initializes a new instance of <c>LuminF14Map</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminF14Map()
    {
        _chunkCount = 1;
        _chunkMask = 0;
        _count = 0;

        AllocateStorage(1);
    }

    /// <summary>Initializes a new instance of <c>LuminF14Map</c>.</summary>
    /// <param name="capacity">The requested storage capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminF14Map(int capacity)
    {
        int chunks = Math.Max(1, (int)Math.Ceiling(capacity / (CHUNK_SIZE * MAX_LOAD_FACTOR)));
        chunks = (int)BitOperations.RoundUpToPowerOf2((uint)chunks);

        _chunkCount = chunks;
        _chunkMask = chunks - 1;
        _count = 0;

        AllocateStorage(chunks);
    }

    /// <summary>Performs the allocate storage step for the current operation.</summary>
    /// <param name="chunkCount">The chunk count used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateStorage(int chunkCount)
    {
        nuint chunkBytes = (nuint)(Unsafe.SizeOf<Chunk>() * chunkCount);
        nuint keyBytes = (nuint)(Unsafe.SizeOf<TKey>() * CHUNK_SIZE * chunkCount);
        nuint valueBytes = (nuint)(Unsafe.SizeOf<TValue>() * CHUNK_SIZE * chunkCount);

        nint chunks = 0;
        nint keys = 0;
        nint values = 0;
        try
        {
            chunks = AllocateAligned(chunkBytes, 16);
            keys = AllocateAligned(keyBytes, LuminPackMarshal.AlignOf<TKey>());
            values = AllocateAligned(valueBytes, LuminPackMarshal.AlignOf<TValue>());

            Vector128<byte> emptyVec = Vector128.Create(EMPTY);
            for (int i = 0; i < chunkCount; i++)
                ElementAt<Chunk>(chunks, i).SetTags(emptyVec);
        }
        catch (Exception exception)
        {
            FreeAligned(chunks);
            FreeAligned(keys);
            FreeAligned(values);
            LuminPackExceptionHelper.Rethrow(exception);
        }

        _chunks = chunks;
        _keys = keys;
        _values = values;
    }

    /// <summary>Allocates a stationary range with the original native alignment.</summary>
    /// <param name="byteCount">The number of bytes to allocate.</param>
    /// <param name="alignment">The alignment in bytes.</param>
    /// <returns>The address of the native allocation.</returns>
    private static unsafe nint AllocateAligned(nuint byteCount, nuint alignment)
        => (nint)NativeMemory.AlignedAlloc(byteCount, alignment);

    /// <summary>Releases storage created by the aligned allocator.</summary>
    /// <param name="address">The native allocation address, or zero.</param>
    private static unsafe void FreeAligned(nint address) => NativeMemory.AlignedFree((void*)address);

    /// <summary>Gets an element within a live native range.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="address">The native base address.</param>
    /// <param name="index">The element index in the range.</param>
    /// <returns>A reference to the element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref T ElementAt<T>(nint address, int index)
        => ref Unsafe.Add(ref LuminPackMarshal.RefAt<T>(address), (nint)(uint)index);

    /// <summary>Determines whether h key are present.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashKey(scoped in TKey key)
    {
        uint h = (uint)key.GetHashCode();

        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;

        return h;
    }

    /// <summary>Determines whether h to tag are present.</summary>
    /// <param name="hash">The hash used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HashToTag(uint hash)
    {
        byte tag = (byte)(hash & 0x7F);
        return tag;
    }

    /// <summary>Determines whether h to chunk are present.</summary>
    /// <param name="hash">The hash used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HashToChunk(uint hash)
    {
        return (int)(hash >> (32 - BitOperations.Log2((uint)_chunkCount))) & _chunkMask;
    }

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(scoped in TKey key, out TValue value)
    {
        if (_count == 0)
        {
            value = default;
            return false;
        }

        uint hash = HashKey(key);
        byte tag = HashToTag(hash);
        int chunkIndex = HashToChunk(hash);

        Vector128<byte> tagVec = Vector128.Create(tag);
        int chunk = chunkIndex;

        for (int probe = 0; probe < MAX_PROBE; probe++)
        {
            Vector128<byte> tags = ElementAt<Chunk>(_chunks, chunk).GetTags();
            Vector128<byte> matches = Vector128.Equals(tags, tagVec);
            uint mask = matches.ExtractMostSignificantBits() & VALID_SLOTS_MASK;

            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;

                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (ElementAt<TKey>(_keys, globalIdx).Equals(key))
                {
                    value = ElementAt<TValue>(_values, globalIdx);
                    return true;
                }
            }

            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex)
                break;
        }

        value = default;
        return false;
    }

    /// <summary>Determines whether the collection contains the supplied key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(scoped in TKey key)
    {
        return TryGetValue(key, out _);
    }

    /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueRefOrNullRef(scoped in TKey key)
    {
        if (_count == 0)
            return ref Unsafe.NullRef<TValue>();

        uint hash = HashKey(key);
        byte tag = HashToTag(hash);
        int chunkIndex = HashToChunk(hash);

        Vector128<byte> tagVec = Vector128.Create(tag);
        int chunk = chunkIndex;

        for (int probe = 0; probe < MAX_PROBE; probe++)
        {
            Vector128<byte> tags = ElementAt<Chunk>(_chunks, chunk).GetTags();
            Vector128<byte> matches = Vector128.Equals(tags, tagVec);
            uint mask = matches.ExtractMostSignificantBits() & VALID_SLOTS_MASK;

            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;

                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (ElementAt<TKey>(_keys, globalIdx).Equals(key))
                {
                    return ref ElementAt<TValue>(_values, globalIdx);
                }
            }

            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex)
                break;
        }

        return ref Unsafe.NullRef<TValue>();
    }

    /// <summary>Adds the supplied entry to the collection.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(scoped in TKey key, scoped in TValue value)
    {
        if (!TryAdd(key, value))
            LuminPackExceptionHelper.ThrowArgumentException("Key already exists");
    }

    /// <summary>Adds the supplied entry to the collection.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(scoped in TKey key, scoped in TValue value)
    {
        if (_count >= (int)(Capacity * MAX_LOAD_FACTOR))
            Resize();

        uint hash = HashKey(key);
        byte tag = HashToTag(hash);
        int chunkIndex = HashToChunk(hash);

        return TryInsertFast(key, value, tag, chunkIndex);
    }

    /// <summary>Attempts to insert fast and reports whether it succeeded.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="tag">The tag used by this operation.</param>
    /// <param name="startChunk">The start chunk used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryInsertFast(scoped in TKey key, scoped in TValue value, byte tag, int startChunk)
    {
        int chunk = startChunk;
        Vector128<byte> tagVec = Vector128.Create(tag);
        Vector128<byte> emptyVec = Vector128.Create(EMPTY);

        for (int probe = 0; probe < MAX_PROBE; probe++)
        {
            Vector128<byte> tags = ElementAt<Chunk>(_chunks, chunk).GetTags();

            Vector128<byte> matches = Vector128.Equals(tags, tagVec);
            Vector128<byte> empties = Vector128.Equals(tags, emptyVec);

            uint matchMask = matches.ExtractMostSignificantBits() & VALID_SLOTS_MASK;
            uint emptyMask = empties.ExtractMostSignificantBits() & VALID_SLOTS_MASK;

            while (matchMask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(matchMask);
                matchMask &= matchMask - 1;

                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (ElementAt<TKey>(_keys, globalIdx).Equals(key))
                    return false;
            }

            if (emptyMask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(emptyMask);
                int globalIdx = chunk * CHUNK_SIZE + idx;

                ElementAt<TKey>(_keys, globalIdx) = key;
                ElementAt<TValue>(_values, globalIdx) = value;
                ElementAt<Chunk>(_chunks, chunk).SetTag(idx, tag);
                _count++;
                return true;
            }

            chunk = (chunk + 1) & _chunkMask;
            if (chunk == startChunk)
                break;
        }

        Resize();
        return TryAdd(key, value);
    }

    /// <summary>Performs the resize step for the current operation.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize()
    {
        int newChunkCount = _chunkCount * 2;
        int oldChunkCount = _chunkCount;

        nint oldChunks = _chunks;
        nint oldKeys = _keys;
        nint oldValues = _values;

        AllocateStorage(newChunkCount);
        _chunkCount = newChunkCount;
        _chunkMask = newChunkCount - 1;
        _count = 0;

        for (int ci = 0; ci < oldChunkCount; ci++)
        {
            for (int si = 0; si < CHUNK_SIZE; si++)
            {
                byte tag = ElementAt<Chunk>(oldChunks, ci).GetTag(si);

                if (tag != EMPTY)
                {
                    int globalIdx = ci * CHUNK_SIZE + si;
                    TKey key = ElementAt<TKey>(oldKeys, globalIdx);
                    TValue value = ElementAt<TValue>(oldValues, globalIdx);

                    uint hash = HashKey(key);
                    byte newTag = HashToTag(hash);
                    int newChunk = HashToChunk(hash);

                    TryInsertFast(key, value, newTag, newChunk);
                }
            }
        }

        FreeAligned(oldChunks);
        FreeAligned(oldKeys);
        FreeAligned(oldValues);
    }

    /// <summary>Removes the entry matching the supplied key or value.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(scoped in TKey key)
    {
        if (_count == 0)
            return false;

        uint hash = HashKey(key);
        byte tag = HashToTag(hash);
        int chunkIndex = HashToChunk(hash);

        Vector128<byte> tagVec = Vector128.Create(tag);
        int chunk = chunkIndex;

        for (int probe = 0; probe < MAX_PROBE; probe++)
        {
            Vector128<byte> tags = ElementAt<Chunk>(_chunks, chunk).GetTags();
            Vector128<byte> matches = Vector128.Equals(tags, tagVec);
            uint mask = matches.ExtractMostSignificantBits() & VALID_SLOTS_MASK;

            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;

                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (ElementAt<TKey>(_keys, globalIdx).Equals(key))
                {
                    ElementAt<Chunk>(_chunks, chunk).SetTag(idx, EMPTY);
                    _count--;
                    return true;
                }
            }

            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex)
                break;
        }

        return false;
    }

    /// <summary>Clears the entries or buffered contents retained by this instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_count > 0)
        {
            Vector128<byte> emptyVec = Vector128.Create(EMPTY);
            for (int i = 0; i < _chunkCount; i++)
                ElementAt<Chunk>(_chunks, i).SetTags(emptyVec);

            _count = 0;
        }
    }

    /// <summary>Releases the resources or pooled state owned by this instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_chunks != 0)
        {


            FreeAligned(_chunks);
            FreeAligned(_keys);
            FreeAligned(_values);

            _chunks = 0;
            _keys = 0;
            _values = 0;
            _count = 0;
            _chunkCount = 0;
        }
    }

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>Provides enumerator functionality.</summary>
    public struct Enumerator
    {
        private readonly nint _chunks;
        private readonly nint _keys;
        private readonly nint _values;
        private readonly int _chunkCount;
        private int _chunkIndex;
        private int _slotIndex;
        private TKey _currentKey;
        private TValue _currentValue;

        /// <summary>Initializes a new instance of <c>Enumerator</c>.</summary>
        /// <param name="map">The map used by this operation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(LuminF14Map<TKey, TValue> map)
        {
            _chunks = map._chunks;
            _keys = map._keys;
            _values = map._values;
            _chunkCount = map._chunkCount;
            _chunkIndex = 0;
            _slotIndex = -1;
            _currentKey = default;
            _currentValue = default;
        }

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_chunkIndex < _chunkCount)
            {
                _slotIndex++;

                while (_slotIndex < CHUNK_SIZE)
                {
                    byte tag = ElementAt<Chunk>(_chunks, _chunkIndex).GetTag(_slotIndex);

                    if (tag != EMPTY)
                    {
                        int globalIdx = _chunkIndex * CHUNK_SIZE + _slotIndex;
                        _currentKey = ElementAt<TKey>(_keys, globalIdx);
                        _currentValue = ElementAt<TValue>(_values, globalIdx);
                        return true;
                    }

                    _slotIndex++;
                }

                _chunkIndex++;
                _slotIndex = -1;
            }

            return false;
        }

        /// <summary>Gets the current.</summary>
        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, TValue>(_currentKey, _currentValue);
        }

        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }
    }
}
#endif
