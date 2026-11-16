#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Code;

public static partial class LuminPackCollectionAccess
{
    /// <summary>Gets the exact dictionary backing that the framework wrapper enumerates.</summary>
    /// <typeparam name="TValue">The type stored as the value in the backing dictionary.</typeparam>
    /// <param name="wrapper">The original read-only dictionary.</param>
    /// <returns>The exact dictionary, or null when its public wrapper must enumerate.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Dictionary<string, TValue>? GetReadOnlyStringDictionaryStorage<TValue>(ReadOnlyDictionary<string, TValue> wrapper)
    {
        if (wrapper.GetType() != typeof(ReadOnlyDictionary<string, TValue>))
            return null;
        int offset = ReadOnlyDictionaryStorage<string, TValue>.Offset;
        if (offset < 0)
            return null;
        var storage = LuminFieldLayout.Field<IDictionary<string, TValue>>(wrapper, offset);
        return storage?.GetType() == typeof(Dictionary<string, TValue>) ? Unsafe.As<Dictionary<string, TValue>>(storage) : null;
    }

    /// <summary>Gets the exact dictionary behind an existing read-only dictionary so a read can reuse it in place.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="wrapper">The existing wrapper.</param>
    /// <returns>The exact backing dictionary, or <see langword="null"/> for another wrapper or backing type.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Dictionary<TKey, TValue>? GetReadOnlyDictionaryStorage<TKey, TValue>(ReadOnlyDictionary<TKey, TValue>? wrapper)
        where TKey : notnull
    {
        if (wrapper is null || wrapper.GetType() != typeof(ReadOnlyDictionary<TKey, TValue>))
            return null;
        int offset = ReadOnlyDictionaryStorage<TKey, TValue>.Offset;
        if (offset < 0)
            return null;
        var storage = LuminFieldLayout.Field<IDictionary<TKey, TValue>>(wrapper, offset);
        return storage?.GetType() == typeof(Dictionary<TKey, TValue>) ? Unsafe.As<Dictionary<TKey, TValue>>(storage) : null;
    }

    /// <summary>Bounds each retained value staging array.</summary>
    private const int MaximumStableDictionaryCount = 4096;

    /// <summary>Stages values for a complete record whose ordered keys match the exact framework backing.</summary>
    /// <typeparam name="TValue">An intrinsic Int32 or string value.</typeparam>
    /// <typeparam name="TOptions">The original compiled binary profile.</typeparam>
    /// <param name="reader">The original operation state before the count.</param>
    /// <param name="wrapper">The existing read-only dictionary wrapper.</param>
    /// <returns>Whether the original backing was updated and the complete reader was published.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static bool TryReadStableReadOnlyDictionary<TValue, TOptions>(
        ref LuminPackReader reader, ReadOnlyDictionary<string, TValue> wrapper)
        where TOptions : struct, ILuminPackOptions
    {
        bool stringValues = typeof(TValue) == typeof(string);
        if ((!stringValues && typeof(TValue) != typeof(int)) || IntPtr.Size != 8 ||
            (typeof(TOptions) != typeof(LuminPackUtf16LengthOptions) && typeof(TOptions) != typeof(LuminPackDefaultOptions) &&
             typeof(TOptions) != typeof(LuminPackFixedOptions<LuminPackUtf16LengthOptions>) &&
             typeof(TOptions) != typeof(LuminPackFixedOptions<LuminPackDefaultOptions>)) ||
            wrapper.GetType() != typeof(ReadOnlyDictionary<string, TValue>))
            return false;

        int offset = ReadOnlyDictionaryStorage<string, TValue>.Offset;
        if (offset < 0)
            return false;

        var storage = LuminFieldLayout.Field<IDictionary<string, TValue>>(wrapper, offset);
        if (storage?.GetType() != typeof(Dictionary<string, TValue>))
            return false;

        var dictionary = Unsafe.As<Dictionary<string, TValue>>(storage);
        if (!ReferenceEquals(dictionary.Comparer, EqualityComparer<string>.Default) ||
            !LuminPackMarshal.TryGetDictionaryView(dictionary, out var view))
            return false;

        var entries = view._entries;
        int count = view._count;
        int version = view._version;
        if (count is <= 0 or > MaximumStableDictionaryCount || view._freeCount != 0 || entries is null || entries.Length < count)
            return false;

        ref var firstEntry = ref LuminPackMarshal.GetNotNullArrayReference(entries);
        ReadOnlySpan<byte> input = reader.Buffer;
        if (!input.IsEmpty)
        {
            // Compare managed addresses without reading or exposing any entry bytes.
            nint entryBytes = (nint)entries.Length * Unsafe.SizeOf<LuminPackMarshal.DictionaryView<string, TValue>.Entry>();
            nint displacement = Unsafe.ByteOffset(ref Unsafe.As<LuminPackMarshal.DictionaryView<string, TValue>.Entry, byte>(ref firstEntry),
                ref MemoryMarshal.GetReference(input));
            if (displacement > -(nint)input.Length && displacement < entryBytes)
                return false;
        }

        LuminPackReader probe = default;
        reader.CopyTo(ref probe);
        // A key and either supported value require two complete words at minimum.
        if (probe.ReadLength() != count || count > (input.Length - probe.Position) / (2 * sizeof(int)))
            return false;

        TValue[]? values = ReadOnlyDictionaryStaging<TValue>.Items.TryTake();
        if (values is null || values.Length < count)
            values = new TValue[count];
        try
        {
            // Check that the captured storage still matches before reading any entries.
            if (!StableReadOnlyDictionaryGeneration(dictionary, view, entries, count, version))
                return false;

            ref var firstValue = ref LuminPackMarshal.GetNotNullArrayReference(values);
            for (int index = 0; index < count; index++)
            {
                string key = Unsafe.Add(ref firstEntry, (nint)(uint)index).Key;
                string? decodedKey = probe.Read<TOptions>(key);
                if (key is null || !ReferenceEquals(decodedKey, key))
                    return false;

                if (stringValues)
                {
                    TValue previous = Unsafe.Add(ref firstEntry, (nint)(uint)index).Value;
                    string? decodedValue = probe.Read<TOptions>(Unsafe.As<TValue, string?>(ref previous));
                    Unsafe.Add(ref firstValue, (nint)(uint)index) = Unsafe.As<string?, TValue>(ref decodedValue);
                }
                else
                {
                    int decodedValue = probe.Read<int>();
                    Unsafe.Add(ref firstValue, (nint)(uint)index) = Unsafe.As<int, TValue>(ref decodedValue);
                }
            }

            if (!StableReadOnlyDictionaryGeneration(dictionary, view, entries, count, version))
                return false;

            // A value replacement preserves existing key order, buckets and enumeration version.
            for (int index = 0; index < count; index++)
                Unsafe.Add(ref firstEntry, (nint)(uint)index).Value = Unsafe.Add(ref firstValue, (nint)(uint)index);
        }
        finally
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                Array.Clear(values, 0, count);
            ReadOnlyDictionaryStaging<TValue>.Items.TryStore(values);
        }

        probe.CopyTo(ref reader);
        return true;
    }

    /// <summary>Owns exclusive value staging without exposing rental or return diagnostics.</summary>
    /// <typeparam name="TValue">The supported scalar or string value type.</typeparam>
    private static class ReadOnlyDictionaryStaging<TValue>
    {
        /// <summary>The four bounded scratch slots for this closed value type.</summary>
        internal static readonly AtomicSlotPool<TValue[]> Items = new(4);
    }

    /// <summary>Checks the captured entry generation before any destination publication.</summary>
    /// <typeparam name="TValue">The exact supported value type.</typeparam>
    /// <param name="dictionary">The exact framework dictionary being read.</param>
    /// <param name="view">The previously validated framework layout.</param>
    /// <param name="entries">The captured entry storage.</param>
    /// <param name="count">The captured dense occupied count.</param>
    /// <param name="version">The captured entry generation.</param>
    /// <returns>Whether the proof still applies.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StableReadOnlyDictionaryGeneration<TValue>(Dictionary<string, TValue> dictionary,
        LuminPackMarshal.DictionaryView<string, TValue> view, LuminPackMarshal.DictionaryView<string, TValue>.Entry[] entries,
        int count, int version) =>
        view._count == count && view._freeCount == 0 && view._version == version && ReferenceEquals(view._entries, entries) &&
        ReferenceEquals(dictionary.Comparer, EqualityComparer<string>.Default);

    /// <summary>Binds the framework's backing dictionary for one closed wrapper type.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    private static class ReadOnlyDictionaryStorage<TKey, TValue> where TKey : notnull
    {
        /// <summary>The verified offset or the unavailable marker.</summary>
        internal static readonly int Offset = FindOffset();

        /// <summary>Checks the field shape and verifies that the public constructor stores the input dictionary there.</summary>
        /// <returns>The verified offset or the unavailable marker.</returns>
        private static int FindOffset()
        {
            var type = typeof(ReadOnlyDictionary<TKey, TValue>);
            if (!RuntimeFeature.IsDynamicCodeCompiled || type.Assembly != typeof(object).Assembly ||
                LuminFieldLayout.Find(type, "m_dictionary") is not { IsInitOnly: true } field ||
                field.FieldType != typeof(IDictionary<TKey, TValue>))
                return LuminFieldLayout.Unavailable;

            var marker = new Dictionary<TKey, TValue>();
            int offset = LuminFieldLayout.ObjectField(type, "m_dictionary", marker);
            return offset >= 0 && ReferenceEquals(marker, LuminFieldLayout.Field<IDictionary<TKey, TValue>>(
                new ReadOnlyDictionary<TKey, TValue>(marker), offset)) ? offset : LuminFieldLayout.Unavailable;
        }
    }
}
#endif
