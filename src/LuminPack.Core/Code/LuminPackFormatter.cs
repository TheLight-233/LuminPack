using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>
/// Common method-pointer carrier for manual, Custom, compiled-profile and external Union registrations. Each operation is the raw address of a
/// static method (converted to <see cref="nint"/>); 0 means the operation is not supported.
/// The generic dispatch casts the pointer back to the T-typed function pointer and calls it
/// directly, so the value is never boxed.
/// </summary>
public struct LuminPackFormatter
{
    /// <summary>Stores the wire tag when this formatter is registered as a Union member.</summary>
    public int Tag;
    /// <summary>Stores the write value.</summary>
    public nint WriteValue;
    /// <summary>Stores the read value.</summary>
    public nint ReadValue;
    /// <summary>Stores the write value json.</summary>
    public nint WriteValueJson;
    /// <summary>Stores the read value json.</summary>
    public nint ReadValueJson;
    /// <summary>Stores the calculate offset.</summary>
    public nint CalculateOffset;
}

/// <summary>
/// Global explicit-registration table used by generic fallbacks and opaque Custom operations.
/// Concrete ordinary types do not consult this table. A raw MethodTable key does not keep
/// an assembly alive; callers must keep registered code loaded for every invocation.
/// </summary>
public static class LuminPackFormatterRegistry
{
    private static readonly ConcurrentDictionary<nint, LuminPackFormatter> s_entries = new();

    /// <summary>
    /// Registers the raw method pointers for a type the source generator did not cover.
    /// <paramref name="writeValue"/> and <paramref name="readValue"/> are required; the others may
    /// be 0 to leave that operation unsupported.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="writeValue">The write value used by this operation.</param>
    /// <param name="readValue">The read value used by this operation.</param>
    /// <param name="writeValueJson">The write value json used by this operation.</param>
    /// <param name="readValueJson">The read value json used by this operation.</param>
    /// <param name="calculateOffset">The calculate offset used by this operation.</param>
    public static void Register(
        Type type,
        nint writeValue,
        nint readValue,
        [System.Runtime.InteropServices.Optional] nint writeValueJson,
        [System.Runtime.InteropServices.Optional] nint readValueJson,
        [System.Runtime.InteropServices.Optional] nint calculateOffset)
    {
        if (type is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(type));
        if (writeValue == 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(writeValue));
        if (readValue == 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(readValue));

        var entry = new LuminPackFormatter
        {
            WriteValue = writeValue,
            ReadValue = readValue,
            WriteValueJson = writeValueJson,
            ReadValueJson = readValueJson,
            CalculateOffset = calculateOffset
        };

        nint key = (nint)LuminPackMarshal.GetMethodTable(type);
        if (!s_entries.TryAdd(key, entry))
            LuminPackExceptionHelper.ThrowArgumentException("A formatter for this type is already registered.");
        Interlocked.Increment(ref LuminPackCompiledFormatterTables.Revision);
    }

    /// <summary>Looks up the registered entry for a MethodTable key.</summary>
    /// <param name="methodTable">The method table used by this operation.</param>
    /// <param name="entry">Receives the entry produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool TryGetValue(in nint methodTable, out LuminPackFormatter entry)
    {
        return s_entries.TryGetValue(methodTable, out entry);
    }
}

/// <summary>Registers closed, profile-specific method pointers for opaque Custom or external formatters.</summary>
/// <typeparam name="TOptions">The stateless profile used to close the formatter methods.</typeparam>
/// <remarks>Unregistered profiles retain the original declaration-owned registration. No configuration object is attached to an operation state.</remarks>
public static partial class LuminPackFormatterRegistry<TOptions> where TOptions : struct, ILuminPackOptions
{
    private static readonly ConcurrentDictionary<nint, LuminPackFormatter> s_entries = LuminPackCompiledFormatterTables.Get<TOptions>();

    /// <summary>Registers method pointers whose bodies explicitly use this compiled profile.</summary>
    /// <param name="type">The formatter's payload type.</param>
    /// <param name="writeValue">The required binary writer method address.</param>
    /// <param name="readValue">The required binary reader method address.</param>
    /// <param name="writeValueJson">The optional JSON writer method address.</param>
    /// <param name="readValueJson">The optional JSON reader method address.</param>
    /// <param name="calculateOffset">The optional exact binary evaluator method address.</param>
    public static void Register(Type type, nint writeValue, nint readValue,
        [System.Runtime.InteropServices.Optional] nint writeValueJson,
        [System.Runtime.InteropServices.Optional] nint readValueJson,
        [System.Runtime.InteropServices.Optional] nint calculateOffset)
    {
        if (type is null) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(type));
        if (writeValue == 0) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(writeValue));
        if (readValue == 0) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(readValue));
        var entry = new LuminPackFormatter { WriteValue = writeValue, ReadValue = readValue, WriteValueJson = writeValueJson,
            ReadValueJson = readValueJson, CalculateOffset = calculateOffset };
        nint key = LuminPackMarshal.GetMethodTable(type);
        // Resolve the closed profile before publishing its immutable entry.
        var entries = s_entries;
        if (!entries.TryAdd(key, entry)) LuminPackExceptionHelper.ThrowArgumentException("A formatter for this type and compiled profile is already registered.");
        Interlocked.Increment(ref LuminPackCompiledFormatterTables.Revision);
    }

    /// <summary>Looks up a closed-profile formatter, then the original declaration-owned registration.</summary>
    /// <param name="methodTable">The payload MethodTable key.</param>
    /// <param name="entry">Receives the registered static method addresses.</param>
    /// <returns>Whether either registration table contains this payload.</returns>
    public static bool TryGetValue(in nint methodTable, out LuminPackFormatter entry)
    {
        var entries = s_entries;
        if (entries.TryGetValue(methodTable, out entry)) return true;
        return LuminPackFormatterRegistry.TryGetValue(methodTable, out entry);
    }

    /// <summary>Finds JSON methods for this document encoding, preventing a default formatter from entering a document with another encoding.</summary>
    /// <param name="methodTable">The payload MethodTable key.</param>
    /// <param name="entry">Receives the registered static method addresses.</param>
    /// <returns>Whether an encoding-compatible registration exists.</returns>
    /// <remarks>Default registrations use UTF-16. Other encodings require an explicit closed-profile registration.</remarks>
    public static bool TryGetJsonValue(in nint methodTable, out LuminPackFormatter entry)
    {
        var entries = s_entries;
        if (entries.TryGetValue(methodTable, out entry)) return true;
        entry = default;
        return default(TOptions).StringEncoding == default(LuminPackDefaultOptions).StringEncoding && LuminPackFormatterRegistry.TryGetValue(methodTable, out entry);
    }
}

/// <summary>Shares method-pointer tables between stateless profiles with identical constants, without retaining profile types.</summary>
internal static class LuminPackCompiledFormatterTables
{
    internal static long Revision;
    private static readonly ConcurrentDictionary<Profile, ConcurrentDictionary<nint, LuminPackFormatter>> Tables = new();

    /// <summary>Resolves a profile's shared table once at closed generic type initialization.</summary>
    /// <typeparam name="TOptions">The stateless profile whose properties are constant.</typeparam>
    /// <returns>The MethodTable-keyed formatter map for the complete constant profile.</returns>
    internal static ConcurrentDictionary<nint, LuminPackFormatter> Get<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var value = default(TOptions);
        var key = new Profile(value.StringEncoding, value.StringRecording, value.StandardFormat, value.MaxJsonDepth, value.ValidateJsonSyntax);
        return Tables.GetOrAdd(key, static _ => new());
    }

    /// <summary>Identifies the complete wire and JSON policy using scalar values rather than a CLR type identity.</summary>
    private readonly struct Profile : IEquatable<Profile>
    {
        private readonly LuminPackStringEncoding _encoding;
        private readonly LuminPackStringRecording _recording;
        private readonly char _format;
        private readonly byte _precision;
        private readonly int _depth;
        private readonly bool _strict;

        /// <summary>Captures the immutable constants of one stateless profile.</summary>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="recording">The binary string framing.</param>
        /// <param name="format">The formatted-value policy.</param>
        /// <param name="depth">The JSON depth bound.</param>
        /// <param name="strict">The JSON grammar policy.</param>
        internal Profile(LuminPackStringEncoding encoding, LuminPackStringRecording recording, System.Buffers.StandardFormat format, int depth, bool strict)
        { _encoding = encoding; _recording = recording; _format = format.Symbol; _precision = format.Precision; _depth = depth; _strict = strict; }

        /// <summary>Compares every option constant used by a closed formatter.</summary>
        /// <param name="other">The other complete profile.</param>
        /// <returns>Whether both registrations use the same policy.</returns>
        public bool Equals(Profile other) => _encoding == other._encoding && _recording == other._recording && _format == other._format &&
            _precision == other._precision && _depth == other._depth && _strict == other._strict;

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is Profile other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => unchecked((((int)_encoding * 397 ^ (int)_recording) * 397 ^ _format) * 397 ^ _precision) ^ _depth ^ (_strict ? int.MinValue : 0);
    }
}

public static partial class LuminPackFormatterRegistry<TOptions> where TOptions : struct, ILuminPackOptions
{
    /// <summary>Looks up an immutable entry snapshot and invalidates it after any successful registration.</summary>
    /// <typeparam name="T">The exact payload type.</typeparam>
    /// <param name="entry">Receives the matching registered method addresses.</param>
    /// <returns>Whether the profile or declaration table contains a formatter.</returns>
    /// <remarks>The first lookup and a lookup invalidated by registration allocate an immutable snapshot. Unchanged registrations require no per-lookup allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCachedValue<T>(out LuminPackFormatter entry)
    {
        var snapshot = Volatile.Read(ref Cache<T>.Binary);
        if (snapshot != null && snapshot.Revision == Volatile.Read(ref LuminPackCompiledFormatterTables.Revision))
        {
            entry = snapshot.Entry;
            return snapshot.Found;
        }
        return ResolveCached<T>(false, out entry);
    }

    /// <summary>Looks up an encoding-compatible snapshot while retaining late profile registration.</summary>
    /// <typeparam name="T">The exact payload type.</typeparam>
    /// <param name="entry">Receives the compatible registered method addresses.</param>
    /// <returns>Whether the existing JSON lookup accepts the formatter.</returns>
    /// <remarks>The cache retains the encoding checks of <see cref="TryGetJsonValue"/> and observes later profile registrations.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCachedJsonValue<T>(out LuminPackFormatter entry)
    {
        var snapshot = Volatile.Read(ref Cache<T>.Json);
        if (snapshot != null && snapshot.Revision == Volatile.Read(ref LuminPackCompiledFormatterTables.Revision))
        {
            entry = snapshot.Entry;
            return snapshot.Found;
        }
        return ResolveCached<T>(true, out entry);
    }

    /// <summary>Resolves through the concurrent maps and safely publishes a complete cache entry.</summary>
    /// <typeparam name="T">The exact payload type.</typeparam>
    /// <param name="json">Whether to apply the existing JSON compatibility rule.</param>
    /// <param name="entry">Receives the resolved method addresses.</param>
    /// <returns>Whether the selected original lookup succeeds.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ResolveCached<T>(bool json, out LuminPackFormatter entry)
    {
        while (true)
        {
            long revision = Volatile.Read(ref LuminPackCompiledFormatterTables.Revision);
            bool found = json ? TryGetJsonValue(in Cache<T>.Key, out entry) : TryGetValue(in Cache<T>.Key, out entry);
            if (revision != Volatile.Read(ref LuminPackCompiledFormatterTables.Revision)) continue;
            var snapshot = new Snapshot(revision, found, entry);
            if (json) Volatile.Write(ref Cache<T>.Json, snapshot);
            else Volatile.Write(ref Cache<T>.Binary, snapshot);
            return found;
        }
    }

    /// <summary>Owns snapshots for one payload/profile without storing payload Type references.</summary>
    /// <typeparam name="T">The exact payload.</typeparam>
    private static class Cache<T>
    {
        internal static readonly nint Key = LuminPackMarshal.GetMethodTable(typeof(T));
        internal static Snapshot? Binary;
        internal static Snapshot? Json;
    }

    /// <summary>Publishes all method addresses and their registration generation as one immutable reference.</summary>
    private sealed class Snapshot
    {
        internal readonly long Revision;
        internal readonly bool Found;
        internal readonly LuminPackFormatter Entry;

        /// <summary>Captures one complete revision-validated lookup result.</summary>
        /// <param name="revision">The validated registration generation.</param>
        /// <param name="found">Whether the lookup succeeded.</param>
        /// <param name="entry">The complete immutable value copied from the concurrent map.</param>
        internal Snapshot(long revision, bool found, LuminPackFormatter entry)
        { Revision = revision; Found = found; Entry = entry; }
    }
}
