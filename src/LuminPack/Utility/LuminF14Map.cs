using LuminPack.Code;

namespace LuminPack.Utility;

#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Numerics;


/// <summary>
/// F14 hash table: Facebook's F14 algorithm - 14-slot chunks with SIMD-accelerated lookup.
/// Performance: Faster than Dictionary, competitive with native F14.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public sealed unsafe class LuminF14Map<TKey, TValue> : IDisposable
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged
{
    private const int CHUNK_SIZE = 14;
    private const int MAX_PROBE = 12;
    private const double MAX_LOAD_FACTOR = 12.0 / 14.0;
    private const byte EMPTY = 0x80;
    private const ushort VALID_SLOTS_MASK = 0x3FFF;
    
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    private struct Chunk
    {
        public fixed byte TagsBuffer[16];
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<byte> GetTags()
        {
            fixed (byte* ptr = TagsBuffer)
            {
                return Sse2.LoadVector128(ptr);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTags(Vector128<byte> vec)
        {
            fixed (byte* ptr = TagsBuffer)
            {
                Sse2.Store(ptr, vec);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetTag(int index)
        {
            fixed (byte* ptr = TagsBuffer)
            {
                return ptr[index];
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTag(int index, byte value)
        {
            fixed (byte* ptr = TagsBuffer)
            {
                ptr[index] = value;
            }
        }
    }

    private Chunk* _chunks;
    private TKey* _keys;
    private TValue* _values;
    private int _chunkCount;
    private int _chunkMask;
    private int _count;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }
    
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunkCount * CHUNK_SIZE;
    }
    
    public bool IsCreated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chunks != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LuminF14Map()
    {
        _chunkCount = 1;
        _chunkMask = 0;
        _count = 0;
        
        AllocateStorage(1);
    }

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AllocateStorage(int chunkCount)
    {
        nuint chunkBytes = (nuint)(sizeof(Chunk) * chunkCount);
        nuint keyBytes = (nuint)(sizeof(TKey) * CHUNK_SIZE * chunkCount);
        nuint valueBytes = (nuint)(sizeof(TValue) * CHUNK_SIZE * chunkCount);
        
        _chunks = (Chunk*)NativeMemory.AlignedAlloc(chunkBytes, 16);
        _keys = (TKey*)NativeMemory.AlignedAlloc(keyBytes, LuminPackMarshal.AlignOf<TKey>());
        _values = (TValue*)NativeMemory.AlignedAlloc(valueBytes, LuminPackMarshal.AlignOf<TValue>());
        
        Vector128<byte> emptyVec = Vector128.Create(EMPTY);
        for (int i = 0; i < chunkCount; i++)
        {
            _chunks[i].SetTags(emptyVec);
        }
        
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HashToTag(uint hash)
    {
        byte tag = (byte)(hash & 0x7F);
        return tag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HashToChunk(uint hash)
    {
        return (int)(hash >> (32 - BitOperations.Log2((uint)_chunkCount))) & _chunkMask;
    }

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
            Vector128<byte> tags = _chunks[chunk].GetTags();
            Vector128<byte> matches = Sse2.CompareEqual(tags, tagVec);
            uint mask = (uint)Sse2.MoveMask(matches) & VALID_SLOTS_MASK;
            
            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                
                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (_keys[globalIdx].Equals(key))
                {
                    value = _values[globalIdx];
                    return true;
                }
            }
            
            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex) break;
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(scoped in TKey key)
    {
        return TryGetValue(key, out _);
    }

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
            Vector128<byte> tags = _chunks[chunk].GetTags();
            Vector128<byte> matches = Sse2.CompareEqual(tags, tagVec);
            uint mask = (uint)Sse2.MoveMask(matches) & VALID_SLOTS_MASK;
            
            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                
                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (_keys[globalIdx].Equals(key))
                {
                    return ref _values[globalIdx];
                }
            }
            
            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex) break;
        }

        return ref Unsafe.NullRef<TValue>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(scoped in TKey key, scoped in TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException("Key already exists");
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryInsertFast(scoped in TKey key, scoped in TValue value, byte tag, int startChunk)
    {
        int chunk = startChunk;
        Vector128<byte> tagVec = Vector128.Create(tag);
        Vector128<byte> emptyVec = Vector128.Create(EMPTY);

        for (int probe = 0; probe < MAX_PROBE; probe++)
        {
            Vector128<byte> tags = _chunks[chunk].GetTags();
   
            Vector128<byte> matches = Sse2.CompareEqual(tags, tagVec);
            Vector128<byte> empties = Sse2.CompareEqual(tags, emptyVec);
        
            uint matchMask = (uint)Sse2.MoveMask(matches) & VALID_SLOTS_MASK;
            uint emptyMask = (uint)Sse2.MoveMask(empties) & VALID_SLOTS_MASK;
            
            while (matchMask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(matchMask);
                matchMask &= matchMask - 1;
                
                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (_keys[globalIdx].Equals(key))
                    return false;
            }
            
            if (emptyMask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(emptyMask);
                int globalIdx = chunk * CHUNK_SIZE + idx;
                
                _keys[globalIdx] = key;
                _values[globalIdx] = value;
                _chunks[chunk].SetTag(idx, tag);
                _count++;
                return true;
            }
        
            chunk = (chunk + 1) & _chunkMask;
            if (chunk == startChunk) break;
        }

        Resize();
        return TryAdd(key, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Resize()
    {
        int newChunkCount = _chunkCount * 2;
        int oldChunkCount = _chunkCount;
        int oldCount = _count;
        
        Chunk* oldChunks = _chunks;
        TKey* oldKeys = _keys;
        TValue* oldValues = _values;

        _chunkCount = newChunkCount;
        _chunkMask = newChunkCount - 1;
        _count = 0;
        
        AllocateStorage(newChunkCount);

        for (int ci = 0; ci < oldChunkCount; ci++)
        {
            for (int si = 0; si < CHUNK_SIZE; si++)
            {
                byte tag = oldChunks[ci].GetTag(si);
                
                if (tag != EMPTY)
                {
                    int globalIdx = ci * CHUNK_SIZE + si;
                    TKey key = oldKeys[globalIdx];
                    TValue value = oldValues[globalIdx];
                    
                    uint hash = HashKey(key);
                    byte newTag = HashToTag(hash);
                    int newChunk = HashToChunk(hash);
                    
                    TryInsertFast(key, value, newTag, newChunk);
                }
            }
        }

        NativeMemory.AlignedFree(oldChunks);
        NativeMemory.AlignedFree(oldKeys);
        NativeMemory.AlignedFree(oldValues);
    }

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
            Vector128<byte> tags = _chunks[chunk].GetTags();
            Vector128<byte> matches = Sse2.CompareEqual(tags, tagVec);
            uint mask = (uint)Sse2.MoveMask(matches) & VALID_SLOTS_MASK;
            
            while (mask != 0)
            {
                int idx = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                
                int globalIdx = chunk * CHUNK_SIZE + idx;
                if (_keys[globalIdx].Equals(key))
                {
                    _chunks[chunk].SetTag(idx, EMPTY);
                    _count--;
                    return true;
                }
            }
            
            chunk = (chunk + 1) & _chunkMask;
            if (chunk == chunkIndex) break;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_count > 0)
        {
            Vector128<byte> emptyVec = Vector128.Create(EMPTY);
            for (int i = 0; i < _chunkCount; i++)
                _chunks[i].SetTags(emptyVec);
            
            _count = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_chunks != null)
        {


            NativeMemory.AlignedFree(_chunks);
            NativeMemory.AlignedFree(_keys);
            NativeMemory.AlignedFree(_values);
            
            _chunks = null;
            _keys = null;
            _values = null;
            _count = 0;
            _chunkCount = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new Enumerator(this);

    public struct Enumerator
    {
        private readonly Chunk* _chunks;
        private readonly TKey* _keys;
        private readonly TValue* _values;
        private readonly int _chunkCount;
        private int _chunkIndex;
        private int _slotIndex;
        private TKey _currentKey;
        private TValue _currentValue;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_chunkIndex < _chunkCount)
            {
                _slotIndex++;
                
                while (_slotIndex < CHUNK_SIZE)
                {
                    byte tag = _chunks[_chunkIndex].GetTag(_slotIndex);
                    
                    if (tag != EMPTY)
                    {
                        int globalIdx = _chunkIndex * CHUNK_SIZE + _slotIndex;
                        _currentKey = _keys[globalIdx];
                        _currentValue = _values[globalIdx];
                        return true;
                    }
                    
                    _slotIndex++;
                }
                
                _chunkIndex++;
                _slotIndex = -1;
            }
            
            return false;
        }

        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, TValue>(_currentKey, _currentValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }
    }
}
#endif