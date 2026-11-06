using System.Buffers;
using System.Buffers.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core
{
#if NET8_0_OR_GREATER
    public ref partial struct LuminPackJsonReader
#else
    public unsafe ref partial struct LuminPackJsonReader
#endif
    {
        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref long value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref int value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref string key, ref string value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref long value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref int value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Applies <c>TryReadEntry</c> with the library default profile.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry(ref int key, ref string value) => TryReadEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads the key, value and closing bracket of a dictionary entry array whose opening bracket is the current token.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise nothing was consumed and the token path reads it.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref long value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, long>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref int value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, int>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref string key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, string, string>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref long value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, long>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref int value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, int>(ref key, ref value);

        /// <inheritdoc cref = "TryReadEntry{TOptions}(ref string, ref long)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadEntry<TOptions>(ref int key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadEntryCore<TOptions, int, string>(ref key, ref value);

        /// <summary>Reads a complete scalar pair through the default profile without reparsing completed strings.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry(ref string key, ref int value) => TryReadCompleteEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads a complete scalar pair after its opening bracket, including ignored trailing values.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry<TOptions>(ref string key, ref int value)
            where TOptions : struct, ILuminPackOptions => TryReadCompleteEntryCore<TOptions, string, int>(ref key, ref value);

        /// <summary>Reads a complete scalar pair through the default profile without reparsing completed strings.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry(ref string key, ref long value) => TryReadCompleteEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads a complete scalar pair after its opening bracket, including ignored trailing values.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry<TOptions>(ref string key, ref long value)
            where TOptions : struct, ILuminPackOptions => TryReadCompleteEntryCore<TOptions, string, long>(ref key, ref value);

        /// <summary>Reads a complete scalar pair through the default profile without reparsing completed strings.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry(ref string key, ref string value) => TryReadCompleteEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads a complete scalar pair after its opening bracket, including ignored trailing values.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry<TOptions>(ref string key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadCompleteEntryCore<TOptions, string, string>(ref key, ref value);

        /// <summary>Reads a complete scalar pair through the default profile without reparsing completed strings.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry(ref int key, ref string value) => TryReadCompleteEntry<LuminPackDefaultOptions>(ref key, ref value);

        /// <summary>Reads a complete scalar pair after its opening bracket, including ignored trailing values.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <returns>Whether the profile handled the entry; unsupported profiles consume nothing.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadCompleteEntry<TOptions>(ref int key, ref string value)
            where TOptions : struct, ILuminPackOptions => TryReadCompleteEntryCore<TOptions, int, string>(ref key, ref value);

        /// <summary>Consumes completed scalars once and continues the original token grammar at a cold boundary.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TKey">The canonical key type.</typeparam>
        /// <typeparam name="TValue">The canonical value type.</typeparam>
        /// <returns>Whether the entry was consumed; unsupported profiles retain the original token path.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadCompleteEntryCore<TOptions, TKey, TValue>(ref TKey key, ref TValue value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).ValidateJsonSyntax || default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8)
                return false;
            if (CurrentTokenType != JsonTokenType.ArrayStart)
            {
                ReadEntryTokens<TOptions, TKey, TValue>(ref key, ref value);
                return true;
            }
            int start = typeof(TKey) == typeof(int) ? Position : 0;
            SkipWhitespace<TOptions>();
            if (!TryReadEntryScalar<TOptions, TKey>(out TKey parsedKey))
            {
                if (typeof(TKey) == typeof(int))
                    Position = start;
                CurrentTokenType = JsonTokenType.ArrayStart;
                ReadEntryTokens<TOptions, TKey, TValue>(ref key, ref value);
                return true;
            }
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)',')
            {
                key = parsedKey;
                FinishEntryAfterKey<TOptions, TValue>(ref value);
                return true;
            }
            Advance(1);
            SkipWhitespace<TOptions>();
            int valueStart = typeof(TValue) == typeof(int) ? Position : 0;
            if (!TryReadEntryScalar<TOptions, TValue>(out TValue parsedValue))
            {
                if (typeof(TValue) == typeof(int))
                    Position = valueStart;
                key = parsedKey;
                FinishEntryAfterKey<TOptions, TValue>(ref value);
                return true;
            }
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)']')
            {
                key = parsedKey;
                value = parsedValue;
                CompleteValue();
                DrainEntry<TOptions>();
                return true;
            }
            Advance(1);
            Depth--;
            CurrentTokenType = JsonTokenType.ArrayEnd;
            key = parsedKey;
            value = parsedValue;
            return true;
        }

        /// <summary>Runs the established entry token operations when its first scalar cannot use the fast path.</summary>
        /// <param name="key">The entry key storage.</param>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TKey">The canonical key type.</typeparam>
        /// <typeparam name="TValue">The canonical value type.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReadEntryTokens<TOptions, TKey, TValue>(ref TKey key, ref TValue value)
            where TOptions : struct, ILuminPackOptions
        {
            if (Read<TOptions>() && CurrentTokenType != JsonTokenType.ArrayEnd)
            {
                ReadEntryScalar<TOptions, TKey>(ref key);
                if (Read<TOptions>() && CurrentTokenType != JsonTokenType.ArrayEnd)
                {
                    ReadEntryScalar<TOptions, TValue>(ref value);
                    DrainEntry<TOptions>();
                }
            }
        }

        /// <summary>Continues after a completed key without allocating that key a second time.</summary>
        /// <param name="value">The entry value storage.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TValue">The canonical value type.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void FinishEntryAfterKey<TOptions, TValue>(ref TValue value)
            where TOptions : struct, ILuminPackOptions
        {
            CompleteValue();
            if (Read<TOptions>() && CurrentTokenType != JsonTokenType.ArrayEnd)
            {
                ReadEntryScalar<TOptions, TValue>(ref value);
                DrainEntry<TOptions>();
            }
        }

        /// <summary>Skips trailing entry elements with the existing token and nesting operations.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrainEntry<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            while (Read<TOptions>())
            {
                if (CurrentTokenType == JsonTokenType.ArrayEnd)
                    break;
                Skip<TOptions>();
            }
        }

        /// <summary>Uses the same typed scalar reader as generated token fallback.</summary>
        /// <param name="value">The scalar destination.</param>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="T">The canonical string, int or long scalar type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReadEntryScalar<TOptions, T>(ref T value) where TOptions : struct, ILuminPackOptions =>
            value = Read<T, TOptions>();

        /// <summary>Parses <c>key , value ]</c> directly from a trusted UTF-8 document.</summary>
        /// <param name = "key">Receives the entry key.</param>
        /// <param name = "value">Receives the entry value.</param>
        /// <returns>Whether the entry was read; otherwise the cursor and token are restored.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <typeparam name = "TKey">The string or int key type.</typeparam>
        /// <typeparam name = "TValue">The string, int or long value type.</typeparam>
        /// <remarks>
        /// Strict documents, UTF-16 documents and any other token shape keep the validated token path. Strings use the
        /// same decoder as <see cref = "Read{TOptions}(ref string)"/>; integers use the same plain-decimal parser.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadEntryCore<TOptions, TKey, TValue>(ref TKey key, ref TValue value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).ValidateJsonSyntax || default(TOptions).StringEncoding != LuminPackStringEncoding.UTF8 ||
                CurrentTokenType != JsonTokenType.ArrayStart)
                return false;
            int start = Position;
            SkipWhitespace<TOptions>();
            if (!TryReadEntryScalar<TOptions, TKey>(out TKey parsedKey))
                goto Restore;
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)',')
                goto Restore;
            Advance(1);
            SkipWhitespace<TOptions>();
            if (!TryReadEntryScalar<TOptions, TValue>(out TValue parsedValue))
                goto Restore;
            SkipWhitespace<TOptions>();
            if (Remaining <= 0 || Current != (byte)']')
                goto Restore;
            Advance(1);
            Depth--;
            CurrentTokenType = JsonTokenType.ArrayEnd;
            key = parsedKey;
            value = parsedValue;
            return true;
        Restore:
            Position = start;
            CurrentTokenType = JsonTokenType.ArrayStart;
            return false;
        }

        /// <summary>Parses one quoted string or plain decimal integer at the cursor.</summary>
        /// <param name = "result">Receives the parsed scalar.</param>
        /// <returns>Whether the token had the expected shape.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        /// <typeparam name = "T">The string, int or long scalar type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadEntryScalar<TOptions, T>(out T result)
            where TOptions : struct, ILuminPackOptions
        {
            result = default!;
            if (typeof(T) == typeof(string))
            {
                if (Remaining <= 0 || Current != (byte)'"')
                    return false;
                CurrentTokenType = JsonTokenType.String;
                string text = ReadString<TOptions>();
                result = Unsafe.As<string, T>(ref text);
                return true;
            }

            if (!TryReadPlainInteger<TOptions>(out long number))
                return false;
            if (typeof(T) == typeof(int))
            {
                if (number < int.MinValue || number > int.MaxValue)
                    return false;
                int narrow = (int)number;
                result = Unsafe.As<int, T>(ref narrow);
                return true;
            }

            result = Unsafe.As<long, T>(ref number);
            return true;
        }
    }
}
