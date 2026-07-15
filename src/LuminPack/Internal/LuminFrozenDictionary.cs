namespace LuminPack.Internal;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public abstract class LuminFrozenDictionary<TKey, TValue> : 
    IDictionary<TKey, TValue>, 
    IReadOnlyDictionary<TKey, TValue>
{
    protected LuminFrozenDictionary(IEqualityComparer<TKey> comparer)
    {
        Comparer = comparer;
    }

    public static LuminFrozenDictionary<TKey, TValue> Empty { get; } = 
        new EmptyFrozenDictionary(EqualityComparer<TKey>.Default);

    public IEqualityComparer<TKey> Comparer { get; }

    private protected abstract TKey[] KeysCore { get; }
    private protected abstract TValue[] ValuesCore { get; }
    private protected abstract int CountCore { get; }
    private protected abstract ref readonly TValue GetValueRefOrNullRefCore(TKey key);

    public int Count => CountCore;
    public TKey[] Keys => KeysCore;
    public TValue[] Values => ValuesCore;

    ICollection<TKey> IDictionary<TKey, TValue>.Keys => KeysCore;
    ICollection<TValue> IDictionary<TKey, TValue>.Values => ValuesCore;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => KeysCore;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ValuesCore;
    public bool IsReadOnly => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly TValue GetValueRefOrNullRef(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        return ref GetValueRefOrNullRefCore(key);
    }

    public ref readonly TValue this[TKey key]
    {
        get
        {
            ref readonly TValue valueRef = ref GetValueRefOrNullRef(key);
            if (Unsafe.IsNullRef(ref Unsafe.AsRef(in valueRef)))
                throw new KeyNotFoundException();
            return ref valueRef;
        }
    }

    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        get => this[key];
        set => throw new NotSupportedException();
    }

    TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => this[key];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) =>
        !Unsafe.IsNullRef(ref Unsafe.AsRef(in GetValueRefOrNullRef(key)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, out TValue value)
    {
        ref readonly TValue valueRef = ref GetValueRefOrNullRef(key);
        if (!Unsafe.IsNullRef(ref Unsafe.AsRef(in valueRef)))
        {
            value = valueRef;
            return true;
        }
        value = default;
        return false;
    }

    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        TryGetValue(item.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        TKey[] keys = KeysCore;
        TValue[] values = ValuesCore;
        for (int i = 0; i < keys.Length; i++)
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
    }

    private protected abstract Enumerator GetEnumeratorCore();
    public Enumerator GetEnumerator() => GetEnumeratorCore();
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(TKey key, TValue value) => throw new NotSupportedException();
    public void Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
    public bool Remove(TKey key) => throw new NotSupportedException();
    public bool Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();

    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private int _index;

        internal Enumerator(TKey[] keys, TValue[] values)
        {
            _keys = keys;
            _values = values;
            _index = -1;
        }

        public bool MoveNext()
        {
            _index++;
            return (uint)_index < (uint)_keys.Length;
        }

        public readonly KeyValuePair<TKey, TValue> Current => 
            new KeyValuePair<TKey, TValue>(_keys[_index], _values[_index]);

        object IEnumerator.Current => Current;
        void IEnumerator.Reset() => _index = -1;
        void IDisposable.Dispose() { }
    }

    // 空字典
    private sealed class EmptyFrozenDictionary : LuminFrozenDictionary<TKey, TValue>
    {
        private static readonly TKey[] s_keys = Array.Empty<TKey>();
        private static readonly TValue[] s_values = Array.Empty<TValue>();

        public EmptyFrozenDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }
        
        private protected override TKey[] KeysCore => s_keys;
        private protected override TValue[] ValuesCore => s_values;
        private protected override int CountCore => 0;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected override ref readonly TValue GetValueRefOrNullRefCore(TKey key) => 
            ref Unsafe.NullRef<TValue>();
        
        private protected override Enumerator GetEnumeratorCore() => new Enumerator(s_keys, s_values);
    }
    
    protected internal sealed class TypeFrozenDictionary : LuminFrozenDictionary<TKey, TValue>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int[] _hashCodes;
        private readonly int[] _buckets;
        private readonly int[] _next;
        private readonly ulong _fastModMultiplier;

        public TypeFrozenDictionary(Dictionary<TKey, TValue> source) : base(source.Comparer)
        {
            int count = source.Count;
            _keys = new TKey[count];
            _values = new TValue[count];
            _hashCodes = new int[count];
            _next = new int[count];
            
            int index = 0;
            foreach (var pair in source)
            {
                _keys[index] = pair.Key;
                _values[index] = pair.Value;
                index++;
            }

            int bucketSize = GetPrime(count);
            _buckets = new int[bucketSize];
            
            for (int i = 0; i < bucketSize; i++)
                _buckets[i] = -1;

            _fastModMultiplier = GetFastModMultiplier((uint)bucketSize);

            for (int i = 0; i < count; i++)
            {
                int hashCode;
                if (typeof(TKey) == typeof(Type))
                {
                    Type type = (Type)(object)_keys[i];
                    hashCode = type.TypeHandle.GetHashCode() & 0x7FFFFFFF;
                }
                else
                {
                    hashCode = source.Comparer.GetHashCode(_keys[i]) & 0x7FFFFFFF;
                }
                
                _hashCodes[i] = hashCode;
                uint bucketIndex = FastMod((uint)hashCode, (uint)bucketSize, _fastModMultiplier);
                
                _next[i] = _buckets[bucketIndex];
                _buckets[bucketIndex] = i;
            }
        }

        private protected override TKey[] KeysCore => _keys;
        private protected override TValue[] ValuesCore => _values;
        private protected override int CountCore => _keys.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected override ref readonly TValue GetValueRefOrNullRefCore(TKey key)
        {
            int hashCode;
            if (typeof(TKey) == typeof(Type))
            {
                Type type = (Type)(object)key;
                hashCode = type.TypeHandle.GetHashCode() & 0x7FFFFFFF;
            }
            else
            {
                hashCode = Comparer.GetHashCode(key) & 0x7FFFFFFF;
            }
            
            uint bucketIndex = FastMod((uint)hashCode, (uint)_buckets.Length, _fastModMultiplier);
            
            int[] hashCodes = _hashCodes;
            TKey[] keys = _keys;
            
            for (int i = _buckets[bucketIndex]; i >= 0; i = _next[i])
            {
                if (hashCodes[i] == hashCode)
                {
                    if (typeof(TKey) == typeof(Type))
                    {
                        if ((object)keys[i] == (object)key)
                            return ref _values[i];
                    }
                    else if (Comparer.Equals(keys[i], key))
                    {
                        return ref _values[i];
                    }
                }
            }

            return ref Unsafe.NullRef<TValue>();
        }

        private protected override Enumerator GetEnumeratorCore() => new Enumerator(_keys, _values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FastMod(uint value, uint divisor, ulong multiplier) =>
            (uint)(((((multiplier * value) >> 32) + 1) * divisor) >> 32);

        private static ulong GetFastModMultiplier(uint divisor) =>
            ulong.MaxValue / divisor + 1;

        private static int GetPrime(int min)
        {
            int[] primes = {
                3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
                1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
                17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437
            };
            foreach (int prime in primes)
                if (prime >= min) return prime;
            for (int i = min | 1; i < int.MaxValue; i += 2)
                if (IsPrime(i)) return i;
            return min;
        }

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0) return candidate == 2;
            int limit = (int)Math.Sqrt(candidate);
            for (int divisor = 3; divisor <= limit; divisor += 2)
                if (candidate % divisor == 0) return false;
            return true;
        }
    }
    
    protected internal sealed class DefaultFrozenDictionary : LuminFrozenDictionary<TKey, TValue>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int[] _hashCodes;
        private readonly int[] _buckets;
        private readonly int[] _next;
        private readonly ulong _fastModMultiplier;

        public DefaultFrozenDictionary(Dictionary<TKey, TValue> source) : base(source.Comparer)
        {
            int count = source.Count;
            _keys = new TKey[count];
            _values = new TValue[count];
            _hashCodes = new int[count];
            _next = new int[count];
            
            int index = 0;
            foreach (var pair in source)
            {
                _keys[index] = pair.Key;
                _values[index] = pair.Value;
                index++;
            }

            int bucketSize = GetPrime(count);
            _buckets = new int[bucketSize];
            
            for (int i = 0; i < bucketSize; i++)
                _buckets[i] = -1;

            _fastModMultiplier = GetFastModMultiplier((uint)bucketSize);

            IEqualityComparer<TKey> comparer = source.Comparer;
            for (int i = 0; i < count; i++)
            {
                int hashCode = comparer.GetHashCode(_keys[i]) & 0x7FFFFFFF;
                _hashCodes[i] = hashCode;
                
                uint bucketIndex = FastMod((uint)hashCode, (uint)bucketSize, _fastModMultiplier);
                
                _next[i] = _buckets[bucketIndex];
                _buckets[bucketIndex] = i;
            }
        }

        private protected override TKey[] KeysCore => _keys;
        private protected override TValue[] ValuesCore => _values;
        private protected override int CountCore => _keys.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected override ref readonly TValue GetValueRefOrNullRefCore(TKey key)
        {
            IEqualityComparer<TKey> comparer = Comparer;
            int hashCode = comparer.GetHashCode(key) & 0x7FFFFFFF;
            uint bucketIndex = FastMod((uint)hashCode, (uint)_buckets.Length, _fastModMultiplier);
            
            int[] hashCodes = _hashCodes;
            TKey[] keys = _keys;
            
            for (int i = _buckets[bucketIndex]; i >= 0; i = _next[i])
            {
                if (hashCodes[i] == hashCode && comparer.Equals(keys[i], key))
                    return ref _values[i];
            }

            return ref Unsafe.NullRef<TValue>();
        }

        private protected override Enumerator GetEnumeratorCore() => new Enumerator(_keys, _values);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FastMod(uint value, uint divisor, ulong multiplier) =>
            (uint)(((((multiplier * value) >> 32) + 1) * divisor) >> 32);

        private static ulong GetFastModMultiplier(uint divisor) =>
            ulong.MaxValue / divisor + 1;

        private static int GetPrime(int min)
        {
            int[] primes = {
                3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
                1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
                17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437
            };
            foreach (int prime in primes)
                if (prime >= min) return prime;
            for (int i = min | 1; i < int.MaxValue; i += 2)
                if (IsPrime(i)) return i;
            return min;
        }

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0) return candidate == 2;
            int limit = (int)Math.Sqrt(candidate);
            for (int divisor = 3; divisor <= limit; divisor += 2)
                if (candidate % divisor == 0) return false;
            return true;
        }
    }
}

public static class LuminFrozenDictionary
{
    public static LuminFrozenDictionary<TKey, TValue> ToLuminFrozenDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> source, 
        IEqualityComparer<TKey> comparer = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        comparer = comparer ?? EqualityComparer<TKey>.Default;

        Dictionary<TKey, TValue> dict;
        if (source is Dictionary<TKey, TValue> existing && existing.Comparer.Equals(comparer))
        {
            dict = existing;
        }
        else
        {
            dict = new Dictionary<TKey, TValue>(comparer);
            foreach (var pair in source)
                dict[pair.Key] = pair.Value;
        }

        if (dict.Count == 0)
            return LuminFrozenDictionary<TKey, TValue>.Empty;

        // Type 优化
        if (typeof(TKey) == typeof(Type))
            return new LuminFrozenDictionary<TKey, TValue>.TypeFrozenDictionary(dict);

        return new LuminFrozenDictionary<TKey, TValue>.DefaultFrozenDictionary(dict);
    }

    public static LuminFrozenDictionary<TKey, TValue> ToLuminFrozenDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector,
        IEqualityComparer<TKey> comparer = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
        if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));

        var dict = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
        foreach (var item in source)
            dict[keySelector(item)] = valueSelector(item);

        return dict.ToLuminFrozenDictionary(comparer);
    }
}