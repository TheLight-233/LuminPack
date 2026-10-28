namespace LuminPack.Utility;

// Structure and lookup pattern inspired by .NET Runtime FrozenDictionary (MIT License).
// Copyright (c) .NET Foundation and Contributors
// https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static LuminPack.Internal.HashHelpers;


/// <summary>
/// Read-only frozen dictionary: one-time hash building, lock-free lookups; Type-key specialization, FastMod buckets, ref returns, allocation-free enumeration.
/// Inspired by:
/// https://source.dot.net/#System.Collections.Immutable/System/Collections/Frozen/FrozenDictionary.cs
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public abstract class LuminFrozenMap<TKey, TValue> :
    IDictionary<TKey, TValue>,
    IReadOnlyDictionary<TKey, TValue>
{
    /// <summary>Initializes a new instance of <c>LuminFrozenMap</c>.</summary>
    /// <param name="comparer">The equality strategy used to compare keys or values.</param>
    protected LuminFrozenMap(IEqualityComparer<TKey> comparer)
    {
        Comparer = comparer;
    }

    /// <summary>Gets the empty.</summary>
    public static LuminFrozenMap<TKey, TValue> Empty { get; } =
        new EmptyFrozenMap(EqualityComparer<TKey>.Default);

    /// <summary>Gets the comparer.</summary>
    public IEqualityComparer<TKey> Comparer { get; }

    /// <summary>Gets the keys core.</summary>
    private protected abstract TKey[] KeysCore { get; }
    /// <summary>Gets the values core.</summary>
    private protected abstract TValue[] ValuesCore { get; }
    /// <summary>Gets the count core.</summary>
    private protected abstract int CountCore { get; }
    /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    private protected abstract ref readonly TValue GetValueRefOrNullRefCore(TKey key);

    /// <summary>Gets the count.</summary>
    public int Count => CountCore;
    /// <summary>Gets the keys.</summary>
    public TKey[] Keys => KeysCore;
    /// <summary>Gets the values.</summary>
    public TValue[] Values => ValuesCore;

    /// <summary>Gets the keys.</summary>
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => KeysCore;
    /// <summary>Gets the values.</summary>
    ICollection<TValue> IDictionary<TKey, TValue>.Values => ValuesCore;
    /// <summary>Gets the keys.</summary>
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => KeysCore;
    /// <summary>Gets the values.</summary>
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ValuesCore;
    /// <summary>Gets the is read only.</summary>
    public bool IsReadOnly => true;

    /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly TValue GetValueRefOrNullRef(TKey key)
    {
        if (key == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(key));
        return ref GetValueRefOrNullRefCore(key);
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    public ref readonly TValue this[TKey key]
    {
        get
        {
            ref readonly TValue valueRef = ref GetValueRefOrNullRef(key);
            if (Unsafe.IsNullRef(ref Unsafe.AsRef(in valueRef)))
                global::LuminPack.Code.LuminPackExceptionHelper.ThrowKeyNotFoundException();
            return ref valueRef;
        }
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    TValue IDictionary<TKey, TValue>.this[TKey key]
    {
        get => this[key];
        set => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation();
    }

    /// <summary>Gets or sets the value associated with the supplied index or key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    TValue IReadOnlyDictionary<TKey, TValue>.this[TKey key] => this[key];

    /// <summary>Determines whether the collection contains the supplied key.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) =>
        !Unsafe.IsNullRef(ref Unsafe.AsRef(in GetValueRefOrNullRef(key)));

    /// <summary>Looks up a key and reports whether an associated value was found.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
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

    /// <summary>Determines whether the collection contains the supplied entry.</summary>
    /// <param name="item">The entry to process.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> item) =>
        TryGetValue(item.Key, out TValue value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

    /// <summary>Copies to to the supplied destination.</summary>
    /// <param name="array">The array used by this operation.</param>
    /// <param name="arrayIndex">The array index used by this operation.</param>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(array));
        TKey[] keys = KeysCore;
        TValue[] values = ValuesCore;
        for (int i = 0; i < keys.Length; i++)
            array[arrayIndex++] = new KeyValuePair<TKey, TValue>(keys[i], values[i]);
    }

    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    private protected abstract Enumerator GetEnumeratorCore();
    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    public Enumerator GetEnumerator() => GetEnumeratorCore();
    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    /// <summary>Creates an enumerator over the entries in this collection.</summary>
    /// <returns>The result of the operation.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Rejects additions because the frozen dictionary is immutable.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <param name="value">The value consumed or produced by this operation.</param>
    public void Add(TKey key, TValue value) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation();
    /// <summary>Rejects additions because the frozen dictionary is immutable.</summary>
    /// <param name="item">The entry to process.</param>
    public void Add(KeyValuePair<TKey, TValue> item) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation();
    /// <summary>Rejects removal because the frozen dictionary is immutable.</summary>
    /// <param name="key">The key used to identify the entry.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Remove(TKey key) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation<bool>();
    /// <summary>Rejects removal because the frozen dictionary is immutable.</summary>
    /// <param name="item">The entry to process.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item) => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation<bool>();
    /// <summary>Rejects clearing because the frozen dictionary is immutable.</summary>
    public void Clear() => global::LuminPack.Code.LuminPackExceptionHelper.ThrowReadOnlyCollectionMutation();

    /// <summary>Provides enumerator functionality.</summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private int _index;

        /// <summary>Initializes a new instance of <c>Enumerator</c>.</summary>
        /// <param name="keys">The keys used by this operation.</param>
        /// <param name="values">The values used by this operation.</param>
        internal Enumerator(TKey[] keys, TValue[] values)
        {
            _keys = keys;
            _values = values;
            _index = -1;
        }

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public bool MoveNext()
        {
            _index++;
            return (uint)_index < (uint)_keys.Length;
        }

        /// <summary>Gets the current.</summary>
        public readonly KeyValuePair<TKey, TValue> Current =>
            new KeyValuePair<TKey, TValue>(_keys[_index], _values[_index]);

        /// <summary>Gets the current.</summary>
        object IEnumerator.Current => Current;
        /// <summary>Resets the enumerator to its initial position.</summary>
        void IEnumerator.Reset() => _index = -1;
        /// <summary>Releases the resources or pooled state owned by this instance.</summary>
        void IDisposable.Dispose() { }
    }

    /// <summary>Provides empty frozen map functionality.</summary>
    private sealed class EmptyFrozenMap : LuminFrozenMap<TKey, TValue>
    {
        private static readonly TKey[] s_keys = Array.Empty<TKey>();
        private static readonly TValue[] s_values = Array.Empty<TValue>();

        /// <summary>Initializes a new instance of <c>EmptyFrozenMap</c>.</summary>
        /// <param name="comparer">The equality strategy used to compare keys or values.</param>
        public EmptyFrozenMap(IEqualityComparer<TKey> comparer) : base(comparer) { }

        /// <summary>Gets the keys core.</summary>
        private protected override TKey[] KeysCore => s_keys;
        /// <summary>Gets the values core.</summary>
        private protected override TValue[] ValuesCore => s_values;
        /// <summary>Gets the count core.</summary>
        private protected override int CountCore => 0;

        /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
        /// <param name="key">The key used to identify the entry.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected override ref readonly TValue GetValueRefOrNullRefCore(TKey key) =>
            ref Unsafe.NullRef<TValue>();

        /// <summary>Creates an enumerator over the entries in this collection.</summary>
        /// <returns>The result of the operation.</returns>
        private protected override Enumerator GetEnumeratorCore() => new Enumerator(s_keys, s_values);
    }

    /// <summary>Provides type frozen map functionality.</summary>
    protected internal sealed class TypeFrozenMap : LuminFrozenMap<TKey, TValue>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int[] _hashCodes;
        private readonly int[] _buckets;
        private readonly int[] _next;
        private readonly ulong _fastModMultiplier;

        /// <summary>Initializes a new instance of <c>TypeFrozenMap</c>.</summary>
        /// <param name="source">The input data to process.</param>
        public TypeFrozenMap(Dictionary<TKey, TValue> source) : base(source.Comparer)
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

        /// <summary>Gets the keys core.</summary>
        private protected override TKey[] KeysCore => _keys;
        /// <summary>Gets the values core.</summary>
        private protected override TValue[] ValuesCore => _values;
        /// <summary>Gets the count core.</summary>
        private protected override int CountCore => _keys.Length;

        /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
        /// <param name="key">The key used to identify the entry.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Creates an enumerator over the entries in this collection.</summary>
        /// <returns>The result of the operation.</returns>
        private protected override Enumerator GetEnumeratorCore() => new Enumerator(_keys, _values);
    }

    /// <summary>Provides default frozen map functionality.</summary>
    protected internal sealed class DefaultFrozenMap : LuminFrozenMap<TKey, TValue>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private readonly int[] _hashCodes;
        private readonly int[] _buckets;
        private readonly int[] _next;
        private readonly ulong _fastModMultiplier;

        /// <summary>Initializes a new instance of <c>DefaultFrozenMap</c>.</summary>
        /// <param name="source">The input data to process.</param>
        public DefaultFrozenMap(Dictionary<TKey, TValue> source) : base(source.Comparer)
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

        /// <summary>Gets the keys core.</summary>
        private protected override TKey[] KeysCore => _keys;
        /// <summary>Gets the values core.</summary>
        private protected override TValue[] ValuesCore => _values;
        /// <summary>Gets the count core.</summary>
        private protected override int CountCore => _keys.Length;

        /// <summary>Returns a reference to the stored value, or a null reference when the key is absent.</summary>
        /// <param name="key">The key used to identify the entry.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Creates an enumerator over the entries in this collection.</summary>
        /// <returns>The result of the operation.</returns>
        private protected override Enumerator GetEnumeratorCore() => new Enumerator(_keys, _values);
    }
}

/// <summary>Provides lumin frozen map functionality.</summary>
public static class LuminFrozenMap
{
    /// <summary>Performs the to lumin frozen dictionary step for the current operation.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="comparer">The equality strategy used to compare keys or values.</param>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static LuminFrozenMap<TKey, TValue> ToLuminFrozenDictionary<TKey, TValue>(
        this IEnumerable<KeyValuePair<TKey, TValue>> source,
        IEqualityComparer<TKey> comparer = null)
    {
        if (source == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(source));

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
            return LuminFrozenMap<TKey, TValue>.Empty;

        if (typeof(TKey) == typeof(Type))
            return new LuminFrozenMap<TKey, TValue>.TypeFrozenMap(dict);

        return new LuminFrozenMap<TKey, TValue>.DefaultFrozenMap(dict);
    }

    /// <summary>Performs the to lumin frozen dictionary step for the current operation.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="keySelector">The key selector used by this operation.</param>
    /// <param name="valueSelector">The value selector used by this operation.</param>
    /// <param name="comparer">The equality strategy used to compare keys or values.</param>
    /// <typeparam name="TSource">The type represented by <c>TSource</c>.</typeparam>
    /// <typeparam name="TKey">The type represented by <c>TKey</c>.</typeparam>
    /// <typeparam name="TValue">The type represented by <c>TValue</c>.</typeparam>
    /// <returns>The result of the operation.</returns>
    public static LuminFrozenMap<TKey, TValue> ToLuminFrozenDictionary<TSource, TKey, TValue>(
        this IEnumerable<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector,
        IEqualityComparer<TKey> comparer = null)
    {
        if (source == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(source));
        if (keySelector == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(keySelector));
        if (valueSelector == null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(valueSelector));

        var dict = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
        foreach (var item in source)
            dict[keySelector(item)] = valueSelector(item);

        return dict.ToLuminFrozenDictionary(comparer);
    }
}
