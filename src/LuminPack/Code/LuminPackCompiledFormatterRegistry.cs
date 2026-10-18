using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>Registers closed, profile-specific method pointers for opaque Custom or external formatters.</summary>
/// <typeparam name="TOptions">The stateless profile used to close the formatter methods.</typeparam>
/// <remarks>Unregistered profiles retain the original declaration-owned registration. No configuration object is attached to an operation state.</remarks>
public static class LuminPackFormatterRegistry<TOptions> where TOptions : struct, ILuminPackOptions
{
    private static readonly LuminCircleReferenceMap<nint, LuminPackFormatterEntry> s_entries = LuminPackCompiledFormatterTables.Get<TOptions>();

    /// <summary>Registers method pointers whose bodies explicitly use this compiled profile.</summary>
    /// <param name="type">The formatter's payload type.</param>
    /// <param name="writeValue">The required binary writer method address.</param>
    /// <param name="readValue">The required binary reader method address.</param>
    /// <param name="writeValueJson">The optional JSON writer method address.</param>
    /// <param name="readValueJson">The optional JSON reader method address.</param>
    /// <param name="calculateOffset">The optional exact binary evaluator method address.</param>
    public static void Register(Type type, nint writeValue, nint readValue, nint writeValueJson = 0, nint readValueJson = 0, nint calculateOffset = 0)
    {
        if (type is null) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(type));
        if (writeValue == 0) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(writeValue));
        if (readValue == 0) LuminPackExceptionHelper.ThrowArgumentNullException(nameof(readValue));
        var entry = new LuminPackFormatterEntry { WriteValue = writeValue, ReadValue = readValue, WriteValueJson = writeValueJson,
            ReadValueJson = readValueJson, CalculateOffset = calculateOffset };
        nint key = LuminPackMarshal.GetMethodTable(type);
        lock (LuminPackCompiledFormatterTables.Sync)
        {
            if (!s_entries.TryAdd(in key, in entry)) LuminPackExceptionHelper.ThrowArgumentException("A formatter for this type and compiled profile is already registered.");
        }
    }

    /// <summary>Looks up a closed-profile formatter, then the original declaration-owned registration.</summary>
    /// <param name="methodTable">The payload MethodTable key.</param>
    /// <param name="entry">Receives the registered static method addresses.</param>
    /// <returns>Whether either registration table contains this payload.</returns>
    public static bool TryGetValue(in nint methodTable, out LuminPackFormatterEntry entry)
    {
        return s_entries.TryGetValue(in methodTable, out entry) || LuminPackFormatterRegistry.TryGetValue(in methodTable, out entry);
    }

    /// <summary>Finds JSON methods for this document encoding, preventing a UTF-8 default formatter from entering a UTF-16 document.</summary>
    /// <param name="methodTable">The payload MethodTable key.</param>
    /// <param name="entry">Receives the registered static method addresses.</param>
    /// <returns>Whether an encoding-compatible registration exists.</returns>
    /// <remarks>Default registrations use UTF-8. Other encodings require an explicit closed-profile registration.</remarks>
    public static bool TryGetJsonValue(in nint methodTable, out LuminPackFormatterEntry entry)
    {
        if (s_entries.TryGetValue(in methodTable, out entry)) return true;
        entry = default;
        return default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 && LuminPackFormatterRegistry.TryGetValue(in methodTable, out entry);
    }
}

/// <summary>Shares method-pointer tables between stateless profiles with identical constants, without retaining profile types.</summary>
internal static class LuminPackCompiledFormatterTables
{
    internal static readonly object Sync = new();
    private static readonly Dictionary<Profile, LuminCircleReferenceMap<nint, LuminPackFormatterEntry>> Tables = new();

    /// <summary>Resolves a profile's shared table once at closed generic type initialization.</summary>
    /// <typeparam name="TOptions">The stateless profile whose properties are constant.</typeparam>
    /// <returns>The MethodTable-keyed formatter map for the complete constant profile.</returns>
    internal static LuminCircleReferenceMap<nint, LuminPackFormatterEntry> Get<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var value = default(TOptions);
        var key = new Profile(value.StringEncoding, value.StringRecording, value.StandardFormat, value.MaxJsonDepth, value.ValidateJsonSyntax);
        lock (Sync)
        {
            if (!Tables.TryGetValue(key, out var entries)) Tables.Add(key, entries = new(16));
            return entries;
        }
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
