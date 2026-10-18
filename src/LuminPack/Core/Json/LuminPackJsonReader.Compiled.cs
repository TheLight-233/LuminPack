using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Core;
public unsafe ref partial struct LuminPackJsonReader
{
    /// <summary>Performs the skip whitespace step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal void SkipWhitespace<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            while (_currentIndex < _bufferReference.Length)
            {
                byte b = _bufferReference[_currentIndex];
                if (b == ' ' || b == '\t' || b == '\r' || b == '\n')
                    _currentIndex++;
                else
                    break;
            }
        }
        else
        {
            while (_currentIndex + 1 < _bufferReference.Length)
            {
                char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    _currentIndex += 2;
                else
                    break;
            }
        }
    }

    /// <summary>Performs the push container step for the current operation.</summary>
    /// <param name = "isObject">The is object used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void PushContainer<TOptions>(bool isObject)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        _depth++;
        if (default(TOptions).MaxJsonDepth > 0 && _depth > default(TOptions).MaxJsonDepth)
            ThrowMalformedJson("JSON nesting depth exceeds the configured maximum");
        if (default(TOptions).ValidateJsonSyntax)
        {
            if (_depth <= 64)
            {
                if (isObject)
                    _containerKindBits |= 1UL << (_depth - 1);
                else
                    _containerKindBits &= ~(1UL << (_depth - 1));
            }
            else
            {
                SetDeepContainerKind(_depth, isObject);
            }
        }

        _jsonState = isObject ? JsonStateNameOrEnd : JsonStateValueOrEnd;
    }

    /// <summary>Performs the pop container step for the current operation.</summary>
    /// <param name = "isObject">The is object used by this operation.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void PopContainer<TOptions>(bool isObject)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (default(TOptions).ValidateJsonSyntax && (_depth == 0 || IsCurrentContainerObject() != isObject))
            ThrowMalformedJson("Mismatched JSON closing bracket");
        if (_depth > 0)
            _depth--;
        _jsonState = JsonStateAfterValue;
    }

    /// <summary>Re-reads the next character after a separator; returns false at end of input.</summary>
    /// <param name = "c">Receives the c produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryReadCurrentChar<TOptions>(out char c)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if (_currentIndex >= _bufferReference.Length)
            {
                c = default;
                return false;
            }

            c = (char)_bufferReference[_currentIndex];
            return true;
        }

        if (_currentIndex + 1 >= _bufferReference.Length)
        {
            c = default;
            return false;
        }

        c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
        return true;
    }

    /// <summary>Ensures a number or literal token ends at EOF or at a JSON structural delimiter.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void RequireTokenEndBoundary<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!default(TOptions).ValidateJsonSyntax)
            return;
        if (!IsValueDelimiterAt<TOptions>(index))
            ThrowMalformedJson("Invalid JSON token: expected a value delimiter");
    }

    /// <summary>Determines whether the supplied value has the value delimiter at classification.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IsValueDelimiterAt<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if ((uint)index >= (uint)_bufferReference.Length)
                return true;
            byte b = _bufferReference[index];
            return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r' || b == (byte)',' || b == (byte)']' || b == (byte)'}';
        }

        if (index + 1 >= _bufferReference.Length)
            return true;
        char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(index));
        return c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == ',' || c == ']' || c == '}';
    }

    /// <summary>
    /// Fast exit for plain integer tokens (what game data is made of): the pre-change reader returned
    /// 1-2 digit values from two byte comparisons and left the rest to Utf8Parser, and benchmarking showed
    /// integer parsing 3.5x slower without a fast path. The grammar and the token boundary are still
    /// enforced — a leading zero must not be followed by a digit, the token must end at a value delimiter,
    /// and the magnitude must fit in 19 digits / <see cref = "long "/> — while every other shape (fractions,
    /// exponents, out-of-range literals) falls through to <see cref = "ScanJsonNumber"/>.
    /// </summary>
    /// <param name = "value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryReadPlainInteger<TOptions>(out long value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        value = 0;
        int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
        int i = _currentIndex;
        bool negative = false;
        if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '-')
        {
            negative = true;
            i += step;
        }

        uint first = NumberDigitOrNonDigit<TOptions>(i);
        if (first > 9u)
            return false;
        if (first == 0u)
        {
            if (NumberDigitOrNonDigit<TOptions>(i + step) <= 9u || !IsValueDelimiterAt<TOptions>(i + step))
                return false;
            _currentIndex = i + step;
            return true;
        }

        ulong magnitude = first;
        int digits = 1;
        int j = i + step;
        while (true)
        {
            uint digit = NumberDigitOrNonDigit<TOptions>(j);
            if (digit > 9u)
                break;
            if (digits == 19)
            {
                // 20 digits can never fit; let the strict scanner report the range error.
                if (!IsNumberAtEnd<TOptions>(j))
                    return false;
                break;
            }

            magnitude = magnitude * 10 + digit;
            digits++;
            j += step;
        }

        if (!IsValueDelimiterAt<TOptions>(j))
            return false;
        if (negative ? magnitude > 9_223_372_036_854_775_808UL : magnitude > 9_223_372_036_854_775_807UL)
            return false;
        _currentIndex = j;
        value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
        return true;
    }

    /// <summary>Digit value at <paramref name = "index"/> or 10 when the buffer ends there.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private uint NumberDigitOrNonDigit<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => IsNumberAtEnd<TOptions>(index) ? 10u : NumberDigitAt<TOptions>(index);
    /// <summary>
    /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
    /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
    /// </summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool Read<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => default(TOptions).ValidateJsonSyntax ? ReadStrict<TOptions>() : ReadTrusted<TOptions>();
    /// <summary>
    /// Matches an unescaped generated property name and advances to its value token.
    /// A mismatch leaves the reader unchanged so ordinary property lookup can handle
    /// whitespace, reordered, missing, unknown, duplicate or escaped names.
    /// </summary>
    /// <param name = "utf8Name">The utf8 name used by this operation.</param>
    /// <param name = "utf16Name">The utf16 name used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryReadPropertyName<TOptions>(ReadOnlySpan<byte> utf8Name, ReadOnlySpan<char> utf16Name)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int index = _currentIndex;
        if ((uint)index >= (uint)_bufferReference.Length)
            return false;
        bool comma;
        int end;
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            comma = _bufferReference[index] == ',';
            if (comma)
                index++;
            var remaining = _bufferReference.Slice(index);
            if (remaining.Length < utf8Name.Length + 3 || remaining[0] != '"' || remaining[utf8Name.Length + 1] != '"' || remaining[utf8Name.Length + 2] != ':' || !remaining.Slice(1, utf8Name.Length).SequenceEqual(utf8Name))
                return false;
            end = index + utf8Name.Length + 3;
        }
        else
        {
            var remaining = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(index));
            if (remaining.IsEmpty)
                return false;
            comma = remaining[0] == ',';
            if (comma)
                remaining = remaining.Slice(1);
            if (remaining.Length < utf16Name.Length + 3 || remaining[0] != '"' || remaining[utf16Name.Length + 1] != '"' || remaining[utf16Name.Length + 2] != ':' || !remaining.Slice(1, utf16Name.Length).SequenceEqual(utf16Name))
                return false;
            end = index + (utf16Name.Length + 3 + (comma ? 1 : 0)) * 2;
        }

        if (default(TOptions).ValidateJsonSyntax)
        {
            // Preserve exactly the separator/container checks performed by ReadStrict.
            if (_depth == 0 || !IsCurrentContainerObject() || (comma ? _jsonState != JsonStateAfterValue : !IsNameState(_jsonState)))
                return false;
            _jsonState = JsonStateValue;
        }

        _currentIndex = end;
        if (!Read<TOptions>())
            ThrowMalformedJson("Expected a JSON property value");
        return true;
    }

    /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ReadTrusted<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        SkipWhitespace<TOptions>();
        if (_currentIndex >= _bufferReference.Length)
            return false;
        char c;
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            byte b = _bufferReference[_currentIndex];
            c = (char)b;
        }
        else
        {
            if (_currentIndex + 1 >= _bufferReference.Length)
                return false;
            c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
        }

        while (true)
        {
            switch (c)
            {
                case '{':
                    CurrentTokenType = JsonTokenType.ObjectStart;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    _depth++;
                    if (default(TOptions).MaxJsonDepth > 0 && _depth > default(TOptions).MaxJsonDepth)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                    return true;
                case '}':
                    CurrentTokenType = JsonTokenType.ObjectEnd;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    _depth--;
                    return true;
                case '[':
                    CurrentTokenType = JsonTokenType.ArrayStart;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    _depth++;
                    if (default(TOptions).MaxJsonDepth > 0 && _depth > default(TOptions).MaxJsonDepth)
                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
                    return true;
                case ']':
                    CurrentTokenType = JsonTokenType.ArrayEnd;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    _depth--;
                    return true;
                case '"':
                    CurrentTokenType = JsonTokenType.String;
                    return true;
                case 't':
                case 'f':
                    return ReadBoolean<TOptions>();
                case 'n':
                    return ReadNull<TOptions>();
                case ',':
                case ':':
                {
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    SkipWhitespace<TOptions>();
                    if (_currentIndex >= _bufferReference.Length)
                        return false;
                    if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
                        c = (char)_bufferReference[_currentIndex];
                    else
                    {
                        if (_currentIndex + 1 >= _bufferReference.Length)
                            return false;
                        c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                    }

                    continue;
                }

                default:
                    if ((c >= '0' && c <= '9') || c == '-')
                    {
                        CurrentTokenType = JsonTokenType.Number;
                        return true;
                    }

                    return false;
            }
        }
    }

    /// <summary>Reads the next JSON token with structural validation enabled.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ReadStrict<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        SkipWhitespace<TOptions>();
        if (_currentIndex >= _bufferReference.Length)
        {
            if (default(TOptions).ValidateJsonSyntax && _depth > 0)
                ThrowMalformedJson("Unterminated JSON object or array");
            return false;
        }

        char c;
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            byte b = _bufferReference[_currentIndex];
            c = (char)b;
        }
        else
        {
            if (_currentIndex + 1 >= _bufferReference.Length)
            {
                if (_depth > 0)
                    ThrowMalformedJson("Unterminated JSON object or array");
                return false;
            }

            c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
        }

        while (true)
        {
            byte state = _jsonState;
            switch (c)
            {
                case '{':
                    if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                        ThrowMalformedJson("Expected a JSON value");
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    PushContainer<TOptions>(true);
                    CurrentTokenType = JsonTokenType.ObjectStart;
                    return true;
                case '}':
                    if (default(TOptions).ValidateJsonSyntax && state != JsonStateNameOrEnd && state != JsonStateAfterValue)
                        ThrowMalformedJson("Unexpected '}' in JSON input");
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    PopContainer<TOptions>(true);
                    CurrentTokenType = JsonTokenType.ObjectEnd;
                    return true;
                case '[':
                    if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                        ThrowMalformedJson("Expected a JSON value");
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    PushContainer<TOptions>(false);
                    CurrentTokenType = JsonTokenType.ArrayStart;
                    return true;
                case ']':
                    if (default(TOptions).ValidateJsonSyntax && state != JsonStateValueOrEnd && state != JsonStateAfterValue)
                        ThrowMalformedJson("Unexpected ']' in JSON input");
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    PopContainer<TOptions>(false);
                    CurrentTokenType = JsonTokenType.ArrayEnd;
                    return true;
                case '"':
                    if (IsNameState(state))
                    {
                        _jsonState = JsonStateColon;
                    }
                    else
                    {
                        if (!IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        _jsonState = JsonStateAfterValue;
                    }

                    CurrentTokenType = JsonTokenType.String;
                    return true;
                case 't':
                case 'f':
                    if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                        ThrowMalformedJson("Expected a JSON value");
                    return ReadBoolean<TOptions>();
                case 'n':
                    if (default(TOptions).ValidateJsonSyntax && !IsValueState(state))
                        ThrowMalformedJson("Expected a JSON value");
                    return ReadNull<TOptions>();
                case ',':
                {
                    if (default(TOptions).ValidateJsonSyntax)
                    {
                        if (state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected ',' in JSON input");
                        if (_depth == 0)
                            ThrowMalformedJson("Unexpected ',' outside a JSON container");
                    }

                    _jsonState = IsCurrentContainerObject() ? JsonStateName : JsonStateValue;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    SkipWhitespace<TOptions>();
                    if (!TryReadCurrentChar<TOptions>(out c))
                    {
                        ThrowMalformedJson(_depth > 0 ? "Unterminated JSON object or array" : "Unexpected end of JSON input after ','");
                    }

                    continue;
                }

                case ':':
                {
                    if (default(TOptions).ValidateJsonSyntax && state != JsonStateColon)
                        ThrowMalformedJson("Unexpected ':' in JSON input");
                    _jsonState = JsonStateValue;
                    _currentIndex += (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
                    SkipWhitespace<TOptions>();
                    if (!TryReadCurrentChar<TOptions>(out c))
                        ThrowMalformedJson("Unexpected end of JSON input after ':'");
                    continue;
                }

                default:
                    if ((c >= '0' && c <= '9') || c == '-')
                    {
                        if (!IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.Number;
                        return true;
                    }

                    return false;
            }
        }
    }

    /// <summary>Attempts to consume array start and reports whether it succeeded.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryConsumeArrayStart<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType == JsonTokenType.ArrayStart)
            return true;
        if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ArrayStart)
            return true;
        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON array");
        return false;
    }

    /// <summary>Skips the current JSON value, including any nested containers.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Skip<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        switch (CurrentTokenType)
        {
            case JsonTokenType.ObjectStart:
            case JsonTokenType.ArrayStart:
            {
                int initialDepth = _depth;
                while (Read<TOptions>())
                {
                    if ((CurrentTokenType == JsonTokenType.ObjectEnd || CurrentTokenType == JsonTokenType.ArrayEnd) && _depth < initialDepth)
                    {
                        return;
                    }

                    SkipScalarToken<TOptions>();
                }

                global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON object or array");
                break;
            }

            case JsonTokenType.String:
            case JsonTokenType.PropertyName:
                SkipStringToken<TOptions>();
                break;
            case JsonTokenType.Number:
                SkipNumberToken<TOptions>();
                break;
            case JsonTokenType.True:
            case JsonTokenType.False:
            case JsonTokenType.Null:
                break;
        }
    }

    /// <summary>Verifies that no non-whitespace JSON remains after the current top-level value.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void EnsureEndOfDocument<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        SkipWhitespace<TOptions>();
        if (_currentIndex != _bufferReference.Length)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unexpected data after the top-level JSON value");
    }

    /// <summary>Performs the skip scalar token step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SkipScalarToken<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        switch (CurrentTokenType)
        {
            case JsonTokenType.String:
            case JsonTokenType.PropertyName:
                SkipStringToken<TOptions>();
                break;
            case JsonTokenType.Number:
                SkipNumberToken<TOptions>();
                break;
        }
    }

    /// <summary>Performs the skip string token step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SkipStringToken<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            SkipStringUtf8<TOptions>();
        else
            SkipStringUtf16<TOptions>();
    }

    /// <summary>Reads string utf8 from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> ReadStringUtf8<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => ReadStringUtf8<TOptions>(true);
    /// <summary>Reads string utf8 from the current input.</summary>
    /// <param name = "unescape">The unescape used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> ReadStringUtf8<TOptions>(bool unescape)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
        if (!(default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-16 JSON");
        _currentIndex++;
        int start = _currentIndex;
        int scan = _currentIndex;
#if NET8_0_OR_GREATER
        var remaining = _bufferReference.Slice(_currentIndex);
        int special = remaining.IndexOfAny(NumberCharLookup.StringSpecialBytes);
        if (special >= 0 && remaining[special] == '"')
        {
            _currentIndex += special + 1;
            return _bufferReference.Slice(start, special);
        }

        if (special < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
        if (remaining[special] < 0x20)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
        scan += special;
#endif
        bool hasEscape = false;
        while (scan < _bufferReference.Length)
        {
            byte b = _bufferReference[scan];
            if (b == '"')
            {
                var raw = _bufferReference.Slice(start, scan - start);
                _currentIndex = scan + 1;
                if (!hasEscape)
                    return raw;
                if (unescape)
                    return UnescapeUtf8(raw);
                ValidateStringEscapes(raw);
                return raw;
            }
            else if (b == '\\')
            {
                hasEscape = true;
                scan++;
                if (scan >= _bufferReference.Length)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                scan++;
            }
            else
            {
                if (b < 0x20)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                scan++;
            }
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
        return default;
    }

    /// <summary>Reads string from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public string ReadString<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            var utf8 = ReadStringUtf8<TOptions>();
#if NET8_0_OR_GREATER
            if ((uint)(utf8.Length - 17) <= 15)
            {
                var ascii = global::LuminPack.Internal.ShortAsciiTranscoder.TryDecodeMedium(ref MemoryMarshal.GetReference(utf8), utf8.Length);
                if (ascii is not null)
                    return ascii;
            }

#endif
            return Encoding.UTF8.GetString(utf8);
        }
        else
        {
            var utf16 = ReadStringUtf16<TOptions>();
            return new string (utf16);
        }
    }

    /// <summary>Reads string utf16 from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> ReadStringUtf16<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => ReadStringUtf16<TOptions>(true);
    /// <summary>Reads string utf16 from the current input.</summary>
    /// <param name = "unescape">The unescape used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<char> ReadStringUtf16<TOptions>(bool unescape)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.String && CurrentTokenType != JsonTokenType.PropertyName)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string");
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("The reader is configured for UTF-8 JSON");
        _currentIndex += 2;
        int start = _currentIndex;
        int scan = _currentIndex;
#if NET8_0_OR_GREATER
        int available = (_bufferReference.Length - _currentIndex) & ~1;
        var charSpan = MemoryMarshal.Cast<byte, char>(_bufferReference.Slice(_currentIndex, available));
        int special = charSpan.IndexOfAny(NumberCharLookup.StringSpecialChars);
        if (special >= 0 && charSpan[special] == '"')
        {
            var result = charSpan[..special];
            _currentIndex += (special + 1) * 2;
            return result;
        }

        if (special < 0)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
        if (charSpan[special] < 0x20)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
        scan += special * 2;
#endif
        bool hasEscape = false;
        while (scan + 1 < _bufferReference.Length)
        {
            char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(scan));
            if (c == '"')
            {
                int byteCount = scan - start;
                var bytes = _bufferReference.Slice(start, byteCount);
                var raw = MemoryMarshal.Cast<byte, char>(bytes);
                _currentIndex = scan + 2;
                if (!hasEscape)
                    return raw;
                if (unescape)
                    return UnescapeUtf16(raw);
                ValidateStringEscapes(raw);
                return raw;
            }

            if (c == '\\')
            {
                hasEscape = true;
                scan += 2;
                if (scan + 1 >= _bufferReference.Length)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON escape sequence");
                scan += 2;
            }
            else
            {
                if (c < 0x20)
                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unescaped control character in JSON string");
                scan += 2;
            }
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Unterminated JSON string");
        return default;
    }

    /// <summary>Performs the skip string utf8 step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SkipStringUtf8<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        _ = ReadStringUtf8<TOptions>(false);
    }

    /// <summary>Performs the skip string utf16 step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SkipStringUtf16<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        _ = ReadStringUtf16<TOptions>(false);
    }

    /// <summary>Reads string choice from the current input.</summary>
    /// <param name = "firstUtf8">The first utf8 used by this operation.</param>
    /// <param name = "firstUtf16">The first utf16 used by this operation.</param>
    /// <param name = "secondUtf8">The second utf8 used by this operation.</param>
    /// <param name = "secondUtf16">The second utf16 used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal int ReadStringChoice<TOptions>(ReadOnlySpan<byte> firstUtf8, ReadOnlySpan<char> firstUtf16, ReadOnlySpan<byte> secondUtf8, ReadOnlySpan<char> secondUtf16)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            var value = ReadStringUtf8<TOptions>();
            if (value.SequenceEqual(firstUtf8))
                return 1;
            if (value.SequenceEqual(secondUtf8))
                return 2;
        }
        else
        {
            var value = ReadStringUtf16<TOptions>();
            if (value.SequenceEqual(firstUtf16))
                return 1;
            if (value.SequenceEqual(secondUtf16))
                return 2;
        }

        return 0;
    }

    /// <summary>
    /// For nested objects, reuses an existing ObjectStart token instead of advancing again.
    /// </summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public bool TryConsumeObjectStart<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType == JsonTokenType.ObjectStart)
        {
            return true;
        }

        if (Read<TOptions>() && CurrentTokenType == JsonTokenType.ObjectStart)
        {
            return true;
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected JSON object");
        return false;
    }

    /// <summary>Reads next float value from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float ReadNextFloatValue<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected a number in JSON array");
        return ReadFloat<TOptions>();
    }

    /// <summary>Performs the consume array end step for the current operation.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void ConsumeArrayEnd<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.ArrayEnd)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Expected the end of a JSON array");
    }

    /// <summary>Determines whether the supplied value has the number at end classification.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool IsNumberAtEnd<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? index >= _bufferReference.Length : index + 1 >= _bufferReference.Length;
    /// <summary>Performs the number char at step for the current operation.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private char NumberCharAt<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions => (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? (char)_bufferReference[index] : Unsafe.ReadUnaligned<char>(ref GetSpanReference(index));
    /// <summary>Performs the number digit at step for the current operation.</summary>
    /// <param name = "index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private uint NumberDigitAt<TOptions>(int index)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        char c = NumberCharAt<TOptions>(index);
        return (uint)(c - '0');
    }

    /// <summary>Validates a complete number token: [minus] int [frac] [exp] followed by EOF or a value delimiter.</summary>
    /// <param name = "start">The start used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private JsonNumberShape ScanJsonNumber<TOptions>(int start)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
        int i = start;
        bool negative = false;
        if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '-')
        {
            negative = true;
            i += step;
        }

        int intStart = i;
        int intDigits = 0;
        if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '0')
        {
            i += step;
            intDigits = 1;
        }
        else
        {
            while (!IsNumberAtEnd<TOptions>(i))
            {
                uint d = NumberDigitAt<TOptions>(i);
                if (d > 9u)
                    break;
                i += step;
                intDigits++;
            }
        }

        if (intDigits == 0)
            ThrowMalformedJson("Invalid JSON number: expected an integer part");
        int intEnd = i;
        int fracStart = i;
        int fracEnd = i;
        bool hasFraction = false;
        if (!IsNumberAtEnd<TOptions>(i) && NumberCharAt<TOptions>(i) == '.')
        {
            hasFraction = true;
            i += step;
            fracStart = i;
            while (!IsNumberAtEnd<TOptions>(i))
            {
                uint d = NumberDigitAt<TOptions>(i);
                if (d > 9u)
                    break;
                i += step;
            }

            fracEnd = i;
            if (fracEnd == fracStart)
                ThrowMalformedJson("Invalid JSON number: expected digits after '.'");
        }

        int exponent = 0;
        bool hasExponent = false;
        if (!IsNumberAtEnd<TOptions>(i))
        {
            char marker = NumberCharAt<TOptions>(i);
            if (marker == 'e' || marker == 'E')
            {
                hasExponent = true;
                i += step;
                bool expNegative = false;
                if (!IsNumberAtEnd<TOptions>(i))
                {
                    char sign = NumberCharAt<TOptions>(i);
                    if (sign == '+' || sign == '-')
                    {
                        expNegative = sign == '-';
                        i += step;
                    }
                }

                int expStart = i;
                int expValue = 0;
                while (!IsNumberAtEnd<TOptions>(i))
                {
                    uint d = NumberDigitAt<TOptions>(i);
                    if (d > 9u)
                        break;
                    if (expValue < 1_000_000)
                        expValue = expValue * 10 + (int)d;
                    i += step;
                }

                if (i == expStart)
                    ThrowMalformedJson("Invalid JSON number: expected digits in the exponent");
                exponent = expNegative ? -expValue : expValue;
            }
        }

        RequireTokenEndBoundary<TOptions>(i);
        return new JsonNumberShape(i, intStart, intEnd, fracStart, fracEnd, exponent, negative, hasFraction, hasExponent);
    }

    /// <summary>Performs the parse unsigned digits step for the current operation.</summary>
    /// <param name = "start">The start used by this operation.</param>
    /// <param name = "end">The end used by this operation.</param>
    /// <param name = "error">The error used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private ulong ParseUnsignedDigits<TOptions>(int start, int end, string error)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
        if ((end - start) / step > 20)
            ThrowMalformedJson(error);
        if (end - start == step)
            return NumberDigitAt<TOptions>(start);
        ulong acc = 0;
        for (int i = start; i < end; i += step)
        {
            ulong d = NumberDigitAt<TOptions>(i);
            if (acc > 1_844_674_407_370_955_161UL || (acc == 1_844_674_407_370_955_161UL && d > 5UL))
                ThrowMalformedJson(error);
            acc = acc * 10 + d;
        }

        return acc;
    }

    /// <summary>Converts token digits to mantissa * 10^exponent10 without dropping significant digits.</summary>
    /// <param name = "shape">The shape used by this operation.</param>
    /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
    /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryAccumulateDecimalDigits<TOptions>(in JsonNumberShape shape, out ulong mantissa, out int exponent10)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
        mantissa = 0;
        exponent10 = 0;
        int digits = 0;
        bool started = false;
        for (int i = shape.IntStart; i < shape.IntEnd; i += step)
        {
            uint d = NumberDigitAt<TOptions>(i);
            if (!started)
            {
                if (d == 0u)
                    continue;
                started = true;
            }

            if (digits == 19)
                return false;
            mantissa = mantissa * 10 + d;
            digits++;
        }

        for (int i = shape.FracStart; i < shape.FracEnd; i += step)
        {
            uint d = NumberDigitAt<TOptions>(i);
            if (!started)
            {
                if (d == 0u)
                {
                    if (exponent10 > -400)
                        exponent10--;
                    continue;
                }

                started = true;
            }

            if (digits == 19)
                return false;
            mantissa = mantissa * 10 + d;
            digits++;
            if (exponent10 > -400)
                exponent10--;
        }

        if (shape.HasExponent)
            exponent10 += shape.Exponent;
        return true;
    }

    /// <summary>Attempts to scan floating point and reports whether it succeeded.</summary>
    /// <param name = "mantissa">Receives the mantissa produced by the operation.</param>
    /// <param name = "exponent10">Receives the exponent10 produced by the operation.</param>
    /// <param name = "negative">Receives the negative produced by the operation.</param>
    /// <param name = "end">Receives the end produced by the operation.</param>
    /// <param name = "exactMantissa">Receives the exact mantissa produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent10, out bool negative, out int end, out bool exactMantissa)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        int step = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : 2;
        int index = _currentIndex;
        negative = !IsNumberAtEnd<TOptions>(index) && NumberCharAt<TOptions>(index) == '-';
        if (negative)
            index += step;
        mantissa = 0;
        exponent10 = 0;
        int places = 0;
        exactMantissa = true;
        end = index;
        uint first = NumberDigitOrNonDigit<TOptions>(index);
        if (first > 9)
            return false;
        mantissa = first;
        index += step;
        int digits = 1;
        uint digit;
        while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
        {
            if (first == 0)
                return false;
            if (digits < 19)
            {
                mantissa = mantissa * 10 + digit;
                digits++;
            }
            else
                exactMantissa = false;
            index += step;
        }

        if (!IsNumberAtEnd<TOptions>(index) && NumberCharAt<TOptions>(index) == '.')
        {
            index += step;
            int fractionStart = index;
            while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
            {
                if (digits < 19)
                {
                    mantissa = mantissa * 10 + digit;
                    digits++;
                }
                else
                    exactMantissa = false;
                places++;
                index += step;
            }

            if (index == fractionStart)
                return false;
        }

        int exponent = 0;
        if (!IsNumberAtEnd<TOptions>(index) && (NumberCharAt<TOptions>(index) == 'e' || NumberCharAt<TOptions>(index) == 'E'))
        {
            index += step;
            bool negativeExponent = false;
            if (!IsNumberAtEnd<TOptions>(index) && (NumberCharAt<TOptions>(index) == '+' || NumberCharAt<TOptions>(index) == '-'))
            {
                negativeExponent = NumberCharAt<TOptions>(index) == '-';
                index += step;
            }

            int exponentStart = index;
            while ((digit = NumberDigitOrNonDigit<TOptions>(index)) <= 9)
            {
                if (exponent < 10_000)
                    exponent = exponent * 10 + (int)digit;
                else
                    exactMantissa = false;
                index += step;
            }

            if (index == exponentStart)
                return false;
            if (negativeExponent)
                exponent = -exponent;
        }

        if (!IsValueDelimiterAt<TOptions>(index))
            return false;
        long scale = (long)exponent - places;
        if (scale < -400 || scale > 400)
            exactMantissa = false;
        else
            exponent10 = (int)scale;
        end = index;
        return true;
    }

    /// <summary>
    /// Exact fast path: exactly representable operands need a single IEEE-754 operation, so the
    /// result equals correct rounding of the decimal literal. Every other input returns false and
    /// is handled by the general-purpose parser.
    /// </summary>
    /// <param name = "shape">The shape used by this operation.</param>
    /// <param name = "value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryParseDoubleExact<TOptions>(in JsonNumberShape shape, out double value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        value = 0;
        if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
            return false;
        if (mantissa == 0)
        {
            value = shape.Negative ? -0.0 : 0.0;
            return true;
        }

        if (exponent10 == 0)
        {
            value = shape.Negative ? -(double)mantissa : (double)mantissa;
            return true;
        }

        if (mantissa > (1UL << 53) || exponent10 < -22 || exponent10 > 22)
            return false;
        double m = mantissa;
        double result = exponent10 > 0 ? m * s_pow10Double[exponent10] : m / s_pow10Double[-exponent10];
        value = shape.Negative ? -result : result;
        return true;
    }

    /// <summary>Attempts to parse float exact and reports whether it succeeded.</summary>
    /// <param name = "shape">The shape used by this operation.</param>
    /// <param name = "value">Receives the value produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryParseFloatExact<TOptions>(in JsonNumberShape shape, out float value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        value = 0;
        if (!TryAccumulateDecimalDigits<TOptions>(shape, out ulong mantissa, out int exponent10))
            return false;
        if (mantissa == 0)
        {
            value = shape.Negative ? -0f : 0f;
            return true;
        }

        if (exponent10 == 0)
        {
            value = shape.Negative ? -(float)mantissa : (float)mantissa;
            return true;
        }

        if (mantissa > (1UL << 24) || exponent10 < -10 || exponent10 > 10)
            return false;
        float m = mantissa;
        float result = exponent10 > 0 ? m * s_pow10Float[exponent10] : m / s_pow10Float[-exponent10];
        value = shape.Negative ? -result : result;
        return true;
    }

    /// <summary>Reads int from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int ReadInt<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= int.MinValue && plain <= int.MaxValue)
            return (int)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger)
            ThrowMalformedJson("Invalid JSON integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON integer");
        if (shape.Negative)
        {
            if (magnitude > 2_147_483_648UL)
                ThrowMalformedJson("Invalid JSON integer");
            _currentIndex = shape.End;
            return unchecked((int)(0UL - magnitude));
        }

        if (magnitude > 2_147_483_647UL)
            ThrowMalformedJson("Invalid JSON integer");
        _currentIndex = shape.End;
        return (int)magnitude;
    }

    /// <summary>Reads u int from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= uint.MaxValue)
            return (uint)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger || shape.Negative)
            ThrowMalformedJson("Invalid JSON unsigned integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned integer");
        if (magnitude > 4_294_967_295UL)
            ThrowMalformedJson("Invalid JSON unsigned integer");
        _currentIndex = shape.End;
        return (uint)magnitude;
    }

    /// <summary>Reads byte from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public byte ReadByte<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= byte.MaxValue)
            return (byte)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger || shape.Negative)
            ThrowMalformedJson("Invalid JSON byte");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON byte");
        if (magnitude > 255UL)
            ThrowMalformedJson("Invalid JSON byte");
        _currentIndex = shape.End;
        return (byte)magnitude;
    }

    /// <summary>Reads s byte from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public sbyte ReadSByte<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= sbyte.MinValue && plain <= sbyte.MaxValue)
            return (sbyte)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger)
            ThrowMalformedJson("Invalid JSON signed byte");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON signed byte");
        if (shape.Negative)
        {
            if (magnitude > 128UL)
                ThrowMalformedJson("Invalid JSON signed byte");
            _currentIndex = shape.End;
            return unchecked((sbyte)(0UL - magnitude));
        }

        if (magnitude > 127UL)
            ThrowMalformedJson("Invalid JSON signed byte");
        _currentIndex = shape.End;
        return (sbyte)magnitude;
    }

    /// <summary>Reads short from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public short ReadShort<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= short.MinValue && plain <= short.MaxValue)
            return (short)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger)
            ThrowMalformedJson("Invalid JSON short integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON short integer");
        if (shape.Negative)
        {
            if (magnitude > 32_768UL)
                ThrowMalformedJson("Invalid JSON short integer");
            _currentIndex = shape.End;
            return unchecked((short)(0UL - magnitude));
        }

        if (magnitude > 32_767UL)
            ThrowMalformedJson("Invalid JSON short integer");
        _currentIndex = shape.End;
        return (short)magnitude;
    }

    /// <summary>Reads u short from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ushort ReadUShort<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= ushort.MaxValue)
            return (ushort)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger || shape.Negative)
            ThrowMalformedJson("Invalid JSON unsigned short integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned short integer");
        if (magnitude > 65_535UL)
            ThrowMalformedJson("Invalid JSON unsigned short integer");
        _currentIndex = shape.End;
        return (ushort)magnitude;
    }

    /// <summary>Reads long from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public long ReadLong<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain))
            return plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger)
            ThrowMalformedJson("Invalid JSON long integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON long integer");
        if (shape.Negative)
        {
            if (magnitude > 9_223_372_036_854_775_808UL)
                ThrowMalformedJson("Invalid JSON long integer");
            _currentIndex = shape.End;
            return unchecked((long)(0UL - magnitude));
        }

        if (magnitude > 9_223_372_036_854_775_807UL)
            ThrowMalformedJson("Invalid JSON long integer");
        _currentIndex = shape.End;
        return (long)magnitude;
    }

    /// <summary>Reads u long from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0)
            return (ulong)plain;
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        if (!shape.IsInteger || shape.Negative)
            ThrowMalformedJson("Invalid JSON unsigned long integer");
        ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned long integer");
        _currentIndex = shape.End;
        return magnitude;
    }

    /// <summary>Reads float from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public float ReadFloat<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
        {
            float result;
            if (exact && (exponent == 0 || (mantissa <= (1UL << 24) && exponent >= -10 && exponent <= 10)))
            {
                result = exponent == 0 ? (float)mantissa : exponent > 0 ? (float)mantissa * s_pow10Float[exponent] : (float)mantissa / s_pow10Float[-exponent];
                if (negative)
                    result = -result;
            }
            else
            {
                result = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseFloatUtf8(_bufferReference.Slice(_currentIndex, end - _currentIndex)) : FallbackParseFloatUtf16(NumberChars(_currentIndex, end));
            }

            _currentIndex = end;
            return result;
        }

        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        float value;
        if (!TryParseFloatExact<TOptions>(shape, out value))
        {
            value = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseFloatUtf8(_bufferReference.Slice(_currentIndex, shape.End - _currentIndex)) : FallbackParseFloatUtf16(NumberChars(_currentIndex, shape.End));
        }

        _currentIndex = shape.End;
        return value;
    }

    /// <summary>Reads double from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public double ReadDouble<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
        {
            double result;
            if (exact && (exponent == 0 || (mantissa <= (1UL << 53) && exponent >= -22 && exponent <= 22)))
            {
                result = exponent == 0 ? (double)mantissa : exponent > 0 ? (double)mantissa * s_pow10Double[exponent] : (double)mantissa / s_pow10Double[-exponent];
                if (negative)
                    result = -result;
            }
            else
            {
                result = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseDoubleUtf8(_bufferReference.Slice(_currentIndex, end - _currentIndex)) : FallbackParseDoubleUtf16(NumberChars(_currentIndex, end));
            }

            _currentIndex = end;
            return result;
        }

        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        double value;
        if (!TryParseDoubleExact<TOptions>(shape, out value))
        {
            value = (default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? FallbackParseDoubleUtf8(_bufferReference.Slice(_currentIndex, shape.End - _currentIndex)) : FallbackParseDoubleUtf16(NumberChars(_currentIndex, shape.End));
        }

        _currentIndex = shape.End;
        return value;
    }

    /// <summary>Reads decimal from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public decimal ReadDecimal<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if (CurrentTokenType != JsonTokenType.Number)
            global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
        JsonNumberShape shape = ScanJsonNumber<TOptions>(_currentIndex);
        decimal value;
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            var token = _bufferReference.Slice(_currentIndex, shape.End - _currentIndex);
            if (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length)
                ThrowMalformedJson("Invalid JSON decimal number");
        }
        else
        {
            if (!decimal.TryParse(NumberChars(_currentIndex, shape.End), System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                ThrowMalformedJson("Invalid JSON decimal number");
        }

        _currentIndex = shape.End;
        return value;
    }

    /// <summary>Reads char from the current input.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public char ReadChar<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            var utf8 = ReadStringUtf8<TOptions>();
            if (utf8.Length == 0)
                return '\0';
            if (utf8.Length == 1 && utf8[0] <= 0x7F)
                return (char)utf8[0];
            Span<char> chars = stackalloc char[2];
            int len = Encoding.UTF8.GetChars(utf8, chars);
            return len > 0 ? chars[0] : '\0';
        }
        else
        {
            var utf16 = ReadStringUtf16<TOptions>();
            return utf16.Length > 0 ? utf16[0] : '\0';
        }
    }

    /// <summary>Advances past the current number token using the same strict grammar as the numeric readers.</summary>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void SkipNumberToken<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        _currentIndex = ScanJsonNumber<TOptions>(_currentIndex).End;
    }

    /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
    /// <returns>True when a complete boolean literal was consumed.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ReadBoolean<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if (_bufferReference[_currentIndex] == 't')
            {
                if (_currentIndex + 4 <= _bufferReference.Length && _bufferReference[_currentIndex + 1] == 'r' && _bufferReference[_currentIndex + 2] == 'u' && _bufferReference[_currentIndex + 3] == 'e')
                {
                    _currentIndex += 4;
                    RequireTokenEndBoundary<TOptions>(_currentIndex);
                    _jsonState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.True;
                    return true;
                }
            }
            else if (_bufferReference[_currentIndex] == 'f')
            {
                if (_currentIndex + 5 <= _bufferReference.Length && _bufferReference[_currentIndex + 1] == 'a' && _bufferReference[_currentIndex + 2] == 'l' && _bufferReference[_currentIndex + 3] == 's' && _bufferReference[_currentIndex + 4] == 'e')
                {
                    _currentIndex += 5;
                    RequireTokenEndBoundary<TOptions>(_currentIndex);
                    _jsonState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.False;
                    return true;
                }
            }
        }
        else
        {
            char c = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
            if (c == 't')
            {
                if (_currentIndex + 8 <= _bufferReference.Length)
                {
                    char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                    char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                    char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                    if (c1 == 'r' && c2 == 'u' && c3 == 'e')
                    {
                        _currentIndex += 8;
                        RequireTokenEndBoundary<TOptions>(_currentIndex);
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.True;
                        return true;
                    }
                }
            }
            else if (c == 'f')
            {
                if (_currentIndex + 10 <= _bufferReference.Length)
                {
                    char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                    char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                    char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                    char c4 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 8));
                    if (c1 == 'a' && c2 == 'l' && c3 == 's' && c4 == 'e')
                    {
                        _currentIndex += 10;
                        RequireTokenEndBoundary<TOptions>(_currentIndex);
                        _jsonState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.False;
                        return true;
                    }
                }
            }
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid boolean value");
        return false;
    }

    /// <summary>Reads null from the current input.</summary>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool ReadNull<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        if ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8))
        {
            if (_currentIndex + 4 <= _bufferReference.Length && _bufferReference[_currentIndex] == 'n' && _bufferReference[_currentIndex + 1] == 'u' && _bufferReference[_currentIndex + 2] == 'l' && _bufferReference[_currentIndex + 3] == 'l')
            {
                _currentIndex += 4;
                RequireTokenEndBoundary<TOptions>(_currentIndex);
                _jsonState = JsonStateAfterValue;
                CurrentTokenType = JsonTokenType.Null;
                return true;
            }
        }
        else
        {
            if (_currentIndex + 8 <= _bufferReference.Length)
            {
                char c0 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex));
                char c1 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 2));
                char c2 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 4));
                char c3 = Unsafe.ReadUnaligned<char>(ref GetSpanReference(_currentIndex + 6));
                if (c0 == 'n' && c1 == 'u' && c2 == 'l' && c3 == 'l')
                {
                    _currentIndex += 8;
                    RequireTokenEndBoundary<TOptions>(_currentIndex);
                    _jsonState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.Null;
                    return true;
                }
            }
        }

        global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException("Invalid null value");
        return false;
    }
}
