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
    public ref partial struct LuminPackJsonReader
    {
        /// <summary>Reads a complete statically selected JSON value through the canonical API.</summary>
        /// <typeparam name = "T">The scalar or immutable string type selected by the caller.</typeparam>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T, TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null or JsonTokenType.PropertyName) && !Read<TOptions>())
                LuminPackExceptionHelper.ThrowFormatException("JSON input does not contain a value.");
            T value = ReadValueCore<T, TOptions>();
            CurrentTokenType = JsonTokenType.None;
            return value;
        }

        /// <summary>Decodes the already selected token through a JIT-specialized scalar kernel.</summary>
        /// <typeparam name = "T">The statically selected value type.</typeparam>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed token value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private T ReadValueCore<T, TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (typeof(T) == typeof(int))
            {
                int value = ReadInt<TOptions>();
                return Unsafe.As<int, T>(ref value);
            }

            if (typeof(T) == typeof(uint))
            {
                uint value = ReadUInt<TOptions>();
                return Unsafe.As<uint, T>(ref value);
            }

            if (typeof(T) == typeof(byte))
            {
                byte value = ReadByte<TOptions>();
                return Unsafe.As<byte, T>(ref value);
            }

            if (typeof(T) == typeof(sbyte))
            {
                sbyte value = ReadSByte<TOptions>();
                return Unsafe.As<sbyte, T>(ref value);
            }

            if (typeof(T) == typeof(short))
            {
                short value = ReadShort<TOptions>();
                return Unsafe.As<short, T>(ref value);
            }

            if (typeof(T) == typeof(ushort))
            {
                ushort value = ReadUShort<TOptions>();
                return Unsafe.As<ushort, T>(ref value);
            }

            if (typeof(T) == typeof(long))
            {
                long value = ReadLong<TOptions>();
                return Unsafe.As<long, T>(ref value);
            }

            if (typeof(T) == typeof(ulong))
            {
                ulong value = ReadULong<TOptions>();
                return Unsafe.As<ulong, T>(ref value);
            }

            if (typeof(T) == typeof(float))
            {
                float value = ReadFloat<TOptions>();
                return Unsafe.As<float, T>(ref value);
            }

            if (typeof(T) == typeof(double))
            {
                double value = ReadDouble<TOptions>();
                return Unsafe.As<double, T>(ref value);
            }

            if (typeof(T) == typeof(decimal))
            {
                decimal value = ReadDecimal<TOptions>();
                return Unsafe.As<decimal, T>(ref value);
            }

            if (typeof(T) == typeof(char))
            {
                char value = ReadChar<TOptions>();
                return Unsafe.As<char, T>(ref value);
            }

            if (typeof(T) == typeof(string))
            {
                string value = CurrentTokenType == JsonTokenType.Null ? null! : ReadString<TOptions>();
                return Unsafe.As<string, T>(ref value);
            }

            if (typeof(T) == typeof(bool))
            {
                bool value = GetBoolean();
                return Unsafe.As<bool, T>(ref value);
            }

            LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));
            return default!;
        }

        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref int value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref int value)
            where TOptions : struct, ILuminPackOptions => value = Read<int, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref uint value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref uint value)
            where TOptions : struct, ILuminPackOptions => value = Read<uint, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref byte value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref byte value)
            where TOptions : struct, ILuminPackOptions => value = Read<byte, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref sbyte value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref sbyte value)
            where TOptions : struct, ILuminPackOptions => value = Read<sbyte, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref short value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref short value)
            where TOptions : struct, ILuminPackOptions => value = Read<short, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref ushort value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref ushort value)
            where TOptions : struct, ILuminPackOptions => value = Read<ushort, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref long value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref long value)
            where TOptions : struct, ILuminPackOptions => value = Read<long, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref ulong value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref ulong value)
            where TOptions : struct, ILuminPackOptions => value = Read<ulong, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref float value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref float value)
            where TOptions : struct, ILuminPackOptions => value = Read<float, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref double value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref double value)
            where TOptions : struct, ILuminPackOptions => value = Read<double, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref decimal value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref decimal value)
            where TOptions : struct, ILuminPackOptions => value = Read<decimal, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref char value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref char value)
            where TOptions : struct, ILuminPackOptions => value = Read<char, TOptions>();
        /// <summary>Reads a complete JSON value into its selected storage.</summary>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read(ref bool value) => Read<LuminPackDefaultOptions>(ref value);
        /// <summary>Reads a complete compiled-profile JSON value into its selected storage.</summary>
        /// <typeparam name = "TOptions">The stateless JSON profile.</typeparam>
        /// <param name = "value">The destination storage.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Read<TOptions>(ref bool value)
            where TOptions : struct, ILuminPackOptions => value = Read<bool, TOptions>();
        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Read()
        {
            return Read<LuminPackDefaultOptions>();
        }

        /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadTrusted()
        {
            return ReadTrusted<LuminPackDefaultOptions>();
        }

        /// <summary>Reads the next JSON token with structural validation enabled.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadStrict()
        {
            return ReadStrict<LuminPackDefaultOptions>();
        }

        /// <summary>Reads next float value from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue()
        {
            return ReadNextFloatValue<LuminPackDefaultOptions>();
        }

        /// <summary>Reads int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadInt()
        {
            return ReadInt<LuminPackDefaultOptions>();
        }

        /// <summary>Reads u int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ReadUInt()
        {
            return ReadUInt<LuminPackDefaultOptions>();
        }

        /// <summary>Reads byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByte()
        {
            return ReadByte<LuminPackDefaultOptions>();
        }

        /// <summary>Reads s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private sbyte ReadSByte()
        {
            return ReadSByte<LuminPackDefaultOptions>();
        }

        /// <summary>Reads short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private short ReadShort()
        {
            return ReadShort<LuminPackDefaultOptions>();
        }

        /// <summary>Reads u short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort()
        {
            return ReadUShort<LuminPackDefaultOptions>();
        }

        /// <summary>Reads long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long ReadLong()
        {
            return ReadLong<LuminPackDefaultOptions>();
        }

        /// <summary>Reads u long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ReadULong()
        {
            return ReadULong<LuminPackDefaultOptions>();
        }

        /// <summary>Reads float from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloat()
        {
            return ReadFloat<LuminPackDefaultOptions>();
        }

        /// <summary>Reads double from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDouble()
        {
            return ReadDouble<LuminPackDefaultOptions>();
        }

        /// <summary>Reads decimal from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private decimal ReadDecimal()
        {
            return ReadDecimal<LuminPackDefaultOptions>();
        }

        /// <summary>Reads char from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        private char ReadChar()
        {
            return ReadChar<LuminPackDefaultOptions>();
        }

        /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
        /// <returns>True when a complete boolean literal was consumed.</returns>
        private bool ReadBoolean()
        {
            return ReadBoolean<LuminPackDefaultOptions>();
        }

        /// <summary>Reads null from the current input.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        private bool ReadNull()
        {
            return ReadNull<LuminPackDefaultOptions>();
        }

        /// <summary>
        /// Advances to the next JSON token: the original lenient tokenizer when the document is trusted
        /// (option ValidateJsonSyntax = false, the default), the fully validated tokenizer otherwise.
        /// </summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public bool Read<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonToken<TOptions>(ref this);
        }

        /// <summary>Advances a trusted reader, checking adjacent tokens before scanning whitespace.</summary>
        /// <typeparam name="TOptions">The document profile.</typeparam>
        /// <returns>Whether a token was selected.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ReadTrustedToken<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            return TryReadAdjacentToken<TOptions>() || ReadTrusted<TOptions>();
        }
        /// <summary>Selects adjacent scalar and container tokens without running the whitespace scanner.</summary>
        /// <typeparam name="TOptions">The stateless encoding and grammar profile.</typeparam>
        /// <returns>Whether the fast path selected a complete token head.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadAdjacentToken<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<TryReadAdjacentTokenOperation<TOptions>, bool>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct TryReadAdjacentTokenOperation<TOptions> : ILuminPackJsonReadOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.TryReadAdjacentTokenEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadAdjacentTokenEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (!TryReadCurrentChar<TOptions>(out char head))
                return false;
            int skip = 0;
            if (head == ',' || head == ':')
            {
                int step = Unsafe.SizeOf<TCodeUnit>();
                int position = Position;
                if ((uint)position > (uint)Buffer.Length || Buffer.Length - position < step * 2)
                    return false;
                if (LuminPackMarshal.ConstantStringProfile<TOptions>())
                {
                    ref byte next = ref Unsafe.Add(ref Current, step);
                    head = step == 1 ? (char)next : LuminPackReader.Read<char>(ref next);
                }
                else
                    head = NumberCharAt<TOptions>(position + step);
                skip = step;
            }

            JsonTokenType selected;
            if (head == '"')
                selected = JsonTokenType.String;
            else if ((uint)(head - '0') <= 9u || head == '-')
                selected = JsonTokenType.Number;
            else
                return TryReadAdjacentContainer<TOptions>(head, skip);
            if (skip != 0)
                Advance(skip);
            CurrentTokenType = selected;
            return true;
        }

        /// <summary>Attempts to read adjacent container.</summary>
        /// <typeparam name="TOptions">The compiled serialization options.</typeparam>
        /// <param name="head">The head supplied to <c>TryReadAdjacentContainer</c>.</param>
        /// <param name="skip">The skip supplied to <c>TryReadAdjacentContainer</c>.</param>
        /// <returns>Whether the operation succeeded.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadAdjacentContainer<TOptions>(char head, int skip) where TOptions : struct, ILuminPackOptions
        {
            JsonTokenType token;
            switch (head)
            {
                case '{':
                    token = JsonTokenType.ObjectStart;
                    break;
                case '}':
                    token = JsonTokenType.ObjectEnd;
                    break;
                case '[':
                    token = JsonTokenType.ArrayStart;
                    break;
                case ']':
                    token = JsonTokenType.ArrayEnd;
                    break;
                default: return false;
            }

            int step = default(TOptions).JsonByteCount(1);
            CurrentTokenType = token;
            Advance(skip + step);
            if (head is '}' or ']')
                Depth--;
            else
            {
                Depth++;
                default(TOptions).CheckJsonDepth(Depth);
            }
            return true;
        }

        /// <summary>Reads the next JSON token using the configured trusted-input path.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadTrusted<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadTrustedOperation<TOptions>, bool>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadTrustedOperation<TOptions> : ILuminPackJsonReadOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadTrustedEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private bool ReadTrustedEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            SkipWhitespace<TOptions>();
            if (Remaining <= 0)
                return false;
            char c;
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                byte b = Current;
                c = (char)b;
            }
            else
            {
                if (Remaining <= 1)
                    return false;
                c = LuminPackReader.Read<char>(ref Current);
            }

            while (true)
            {
                switch (c)
                {
                    case '{':
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        Depth++;
                        default(TOptions).CheckJsonDepth(Depth);
                        return true;
                    case '}':
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        Depth--;
                        return true;
                    case '[':
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        Depth++;
                        default(TOptions).CheckJsonDepth(Depth);
                        return true;
                    case ']':
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        Depth--;
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
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        SkipWhitespace<TOptions>();
                        if (Remaining <= 0)
                            return false;
                        if ((Unsafe.SizeOf<TCodeUnit>() == 1))
                            c = (char)Current;
                        else
                        {
                            if (Remaining <= 1)
                                return false;
                            c = LuminPackReader.Read<char>(ref Current);
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
        internal bool ReadStrict<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadStrictOperation<TOptions>, bool>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadStrictOperation<TOptions> : ILuminPackJsonReadOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadStrictEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadStrictEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            SkipWhitespace<TOptions>();
            if (Remaining <= 0)
            {
                if (default(TOptions).TracksJsonGrammar() && Depth > 0)
                    ThrowMalformedJson("Unterminated JSON object or array");
                return false;
            }

            char c;
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                byte b = Current;
                c = (char)b;
            }
            else
            {
                if (Remaining <= 1)
                {
                    if (Depth > 0)
                        ThrowMalformedJson("Unterminated JSON object or array");
                    return false;
                }

                c = LuminPackReader.Read<char>(ref Current);
            }

            while (true)
            {
                byte state = GrammarState;
                switch (c)
                {
                    case '{':
                        if (default(TOptions).TracksJsonGrammar() && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        PushContainer<TOptions>(true);
                        CurrentTokenType = JsonTokenType.ObjectStart;
                        return true;
                    case '}':
                        if (default(TOptions).TracksJsonGrammar() && state != JsonStateNameOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected '}' in JSON input");
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        PopContainer<TOptions>(true);
                        CurrentTokenType = JsonTokenType.ObjectEnd;
                        return true;
                    case '[':
                        if (default(TOptions).TracksJsonGrammar() && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        PushContainer<TOptions>(false);
                        CurrentTokenType = JsonTokenType.ArrayStart;
                        return true;
                    case ']':
                        if (default(TOptions).TracksJsonGrammar() && state != JsonStateValueOrEnd && state != JsonStateAfterValue)
                            ThrowMalformedJson("Unexpected ']' in JSON input");
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        PopContainer<TOptions>(false);
                        CurrentTokenType = JsonTokenType.ArrayEnd;
                        return true;
                    case '"':
                        if (IsNameState(state))
                        {
                            GrammarState = JsonStateColon;
                        }
                        else
                        {
                            if (!IsValueState(state))
                                ThrowMalformedJson("Expected a JSON value");
                            GrammarState = JsonStateAfterValue;
                        }

                        CurrentTokenType = JsonTokenType.String;
                        return true;
                    case 't':
                    case 'f':
                        if (default(TOptions).TracksJsonGrammar() && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadBoolean<TOptions>();
                    case 'n':
                        if (default(TOptions).TracksJsonGrammar() && !IsValueState(state))
                            ThrowMalformedJson("Expected a JSON value");
                        return ReadNull<TOptions>();
                    case ',':
                    {
                        if (default(TOptions).TracksJsonGrammar())
                        {
                            if (state != JsonStateAfterValue)
                                ThrowMalformedJson("Unexpected ',' in JSON input");
                            if (Depth == 0)
                                ThrowMalformedJson("Unexpected ',' outside a JSON container");
                        }

                        GrammarState = IsCurrentContainerObject() ? JsonStateName : JsonStateValue;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
                        SkipWhitespace<TOptions>();
                        if (!TryReadCurrentChar<TOptions>(out c))
                        {
                            ThrowMalformedJson(Depth > 0 ? "Unterminated JSON object or array" : "Unexpected end of JSON input after ','");
                        }

                        continue;
                    }

                    case ':':
                    {
                        if (default(TOptions).TracksJsonGrammar() && state != JsonStateColon)
                            ThrowMalformedJson("Unexpected ':' in JSON input");
                        GrammarState = JsonStateValue;
                        Advance(Unsafe.SizeOf<TCodeUnit>());
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
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.Number;
                            return true;
                        }

                        return false;
                }
            }
        }

        /// <summary>Reads next float value from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float ReadNextFloatValue<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (!Read<TOptions>() || CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowFormatException("Expected a number in JSON array");
            return ReadFloat<TOptions>();
        }

        /// <summary>Reads int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int ReadInt<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= int.MinValue && plain <= int.MaxValue)
                return (int)plain;
            return ReadIntSlow<TOptions>();
        }

        /// <summary>Parses an int token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private int ReadIntSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON integer");
            if (shape.Negative)
            {
                if (magnitude > 2_147_483_648UL)
                    ThrowMalformedJson("Invalid JSON integer");
                Position = shape.End;
                return unchecked((int)(0UL - magnitude));
            }

            if (magnitude > 2_147_483_647UL)
                ThrowMalformedJson("Invalid JSON integer");
            Position = shape.End;
            return (int)magnitude;
        }

        /// <summary>Reads u int from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private uint ReadUInt<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= uint.MaxValue)
                return (uint)plain;
            return ReadUIntSlow<TOptions>();
        }

        /// <summary>Parses a uint token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private uint ReadUIntSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned integer");
            if (magnitude > 4_294_967_295UL)
                ThrowMalformedJson("Invalid JSON unsigned integer");
            Position = shape.End;
            return (uint)magnitude;
        }

        /// <summary>Reads byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private byte ReadByte<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= byte.MaxValue)
                return (byte)plain;
            return ReadByteSlow<TOptions>();
        }

        /// <summary>Parses a byte token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private byte ReadByteSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON byte");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON byte");
            if (magnitude > 255UL)
                ThrowMalformedJson("Invalid JSON byte");
            Position = shape.End;
            return (byte)magnitude;
        }

        /// <summary>Reads s byte from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private sbyte ReadSByte<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= sbyte.MinValue && plain <= sbyte.MaxValue)
                return (sbyte)plain;
            return ReadSByteSlow<TOptions>();
        }

        /// <summary>Parses an sbyte token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private sbyte ReadSByteSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON signed byte");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON signed byte");
            if (shape.Negative)
            {
                if (magnitude > 128UL)
                    ThrowMalformedJson("Invalid JSON signed byte");
                Position = shape.End;
                return unchecked((sbyte)(0UL - magnitude));
            }

            if (magnitude > 127UL)
                ThrowMalformedJson("Invalid JSON signed byte");
            Position = shape.End;
            return (sbyte)magnitude;
        }

        /// <summary>Reads short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private short ReadShort<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= short.MinValue && plain <= short.MaxValue)
                return (short)plain;
            return ReadShortSlow<TOptions>();
        }

        /// <summary>Parses a short token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private short ReadShortSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON short integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON short integer");
            if (shape.Negative)
            {
                if (magnitude > 32_768UL)
                    ThrowMalformedJson("Invalid JSON short integer");
                Position = shape.End;
                return unchecked((short)(0UL - magnitude));
            }

            if (magnitude > 32_767UL)
                ThrowMalformedJson("Invalid JSON short integer");
            Position = shape.End;
            return (short)magnitude;
        }

        /// <summary>Reads u short from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ushort ReadUShort<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0 && plain <= ushort.MaxValue)
                return (ushort)plain;
            return ReadUShortSlow<TOptions>();
        }

        /// <summary>Parses a ushort token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ushort ReadUShortSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned short integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned short integer");
            if (magnitude > 65_535UL)
                ThrowMalformedJson("Invalid JSON unsigned short integer");
            Position = shape.End;
            return (ushort)magnitude;
        }

        /// <summary>Reads long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private long ReadLong<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain))
                return plain;
            return ReadLongSlow<TOptions>();
        }

        /// <summary>Parses a long token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private long ReadLongSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger)
                ThrowMalformedJson("Invalid JSON long integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON long integer");
            if (shape.Negative)
            {
                if (magnitude > 9_223_372_036_854_775_808UL)
                    ThrowMalformedJson("Invalid JSON long integer");
                Position = shape.End;
                return unchecked((long)(0UL - magnitude));
            }

            if (magnitude > 9_223_372_036_854_775_807UL)
                ThrowMalformedJson("Invalid JSON long integer");
            Position = shape.End;
            return (long)magnitude;
        }

        /// <summary>Reads u long from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private ulong ReadULong<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryReadPlainInteger<TOptions>(out long plain) && plain >= 0)
                return (ulong)plain;
            return ReadULongSlow<TOptions>();
        }

        /// <summary>Parses a ulong token that is not a plain delimited decimal, outside the inlined fast path.</summary>
        /// <returns>The validated value.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private ulong ReadULongSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            if (!shape.IsInteger || shape.Negative)
                ThrowMalformedJson("Invalid JSON unsigned long integer");
            ulong magnitude = ParseUnsignedDigits<TOptions>(shape.IntStart, shape.IntEnd, "Invalid JSON unsigned long integer");
            Position = shape.End;
            return magnitude;
        }

        /// <summary>Reads float from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloat<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadFloatOperation<TOptions>, float>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadFloatOperation<TOptions> : ILuminPackJsonReadOperation<float> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadFloatEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloatEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
            {
                float result;
                if (exact && ((exponent == 0
#if !NET10_0_OR_GREATER
                    && mantissa <= (1UL << 53)
#endif
                    ) || (mantissa <= (1UL << 24) && exponent >= -10 && exponent <= 10)))
                {
                    // Nonzero exponents reach this branch only with a 24-bit mantissa.
                    result = exponent == 0 ? (float)mantissa : exponent > 0 ? (float)(int)mantissa * s_pow10Float[exponent] : (float)(int)mantissa / s_pow10Float[-exponent];
                    if (negative)
                        result = -result;
                }
                else
                {
                    result = (Unsafe.SizeOf<TCodeUnit>() == 1) ? FallbackParseFloatUtf8(Buffer.Slice(Position, end - Position)) : FallbackParseFloatUtf16(NumberChars(Position, end));
                }

                Position = end;
                return result;
            }

            return ReadFloatSlow<TOptions>();
        }

        /// <summary>Retains the complete numeric fallback outside generated callers' fast parsing paths.</summary>
        /// <typeparam name="TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed and validated floating-point value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ReadFloatSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadFloatSlowOperation<TOptions>, float>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadFloatSlowOperation<TOptions> : ILuminPackJsonReadOperation<float> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadFloatSlowEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private float ReadFloatSlowEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            float value;
            if (!TryParseFloatExact<TOptions>(shape, out value))
            {
                value = (Unsafe.SizeOf<TCodeUnit>() == 1) ? FallbackParseFloatUtf8(Buffer.Slice(Position, shape.End - Position)) : FallbackParseFloatUtf16(NumberChars(Position, shape.End));
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads double from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDouble<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadDoubleOperation<TOptions>, double>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadDoubleOperation<TOptions> : ILuminPackJsonReadOperation<double> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadDoubleEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDoubleEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            if (TryScanFloatingPoint<TOptions>(out ulong mantissa, out int exponent, out bool negative, out int end, out bool exact))
            {
                double result;
                if (exact && ((exponent == 0
#if !NET9_0_OR_GREATER
                    && mantissa <= long.MaxValue
#endif
                    ) || (mantissa <= (1UL << 53) && exponent >= -22 && exponent <= 22)))
                {
                    result = exponent == 0 ? (double)mantissa : exponent > 0 ? (double)mantissa * s_pow10Double[exponent] : (double)mantissa / s_pow10Double[-exponent];
                    if (negative)
                        result = -result;
                }
                else
                {
                    result = (Unsafe.SizeOf<TCodeUnit>() == 1) ? FallbackParseDoubleUtf8(Buffer.Slice(Position, end - Position)) : FallbackParseDoubleUtf16(NumberChars(Position, end));
                }

                Position = end;
                return result;
            }

            return ReadDoubleSlow<TOptions>();
        }

        /// <summary>Retains the complete numeric fallback outside generated callers' fast parsing paths.</summary>
        /// <typeparam name="TOptions">The stateless JSON profile.</typeparam>
        /// <returns>The parsed and validated floating-point value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double ReadDoubleSlow<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadDoubleSlowOperation<TOptions>, double>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadDoubleSlowOperation<TOptions> : ILuminPackJsonReadOperation<double> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public double Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadDoubleSlowEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private double ReadDoubleSlowEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            double value;
            if (!TryParseDoubleExact<TOptions>(shape, out value))
            {
                value = (Unsafe.SizeOf<TCodeUnit>() == 1) ? FallbackParseDoubleUtf8(Buffer.Slice(Position, shape.End - Position)) : FallbackParseDoubleUtf16(NumberChars(Position, shape.End));
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads decimal from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private decimal ReadDecimal<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadDecimalOperation<TOptions>, decimal>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadDecimalOperation<TOptions> : ILuminPackJsonReadOperation<decimal> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public decimal Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadDecimalEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private decimal ReadDecimalEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if (CurrentTokenType != JsonTokenType.Number)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Expected number");
            JsonNumberShape shape = ScanJsonNumber<TOptions>(Position);
            decimal value;
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                var token = Buffer.Slice(Position, shape.End - Position);
                if (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length)
                    ThrowMalformedJson("Invalid JSON decimal number");
            }
            else
            {
                if (!decimal.TryParse(NumberChars(Position, shape.End), System.Globalization.NumberStyles.Float, System.Globalization.NumberFormatInfo.InvariantInfo, out value))
                    ThrowMalformedJson("Invalid JSON decimal number");
            }

            Position = shape.End;
            return value;
        }

        /// <summary>Reads char from the current input.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private char ReadChar<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadCharOperation<TOptions>, char>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadCharOperation<TOptions> : ILuminPackJsonReadOperation<char> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public char Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadCharEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private char ReadCharEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                var utf8 = ReadStringUtf8<TOptions>();
                if (utf8.Length == 0)
                    return '\0';
                if (utf8.Length == 1 && utf8[0] <= 0x7F)
                    return (char)utf8[0];
                Span<char> chars = stackalloc char[2];
                int len = Utf8PlatformDecoder.Decode(utf8, chars);
                return len > 0 ? chars[0] : '\0';
            }
            else
            {
                var utf16 = ReadStringUtf16<TOptions>();
                return utf16.Length > 0 ? utf16[0] : '\0';
            }
        }

        /// <summary>Consumes a true or false literal and updates the current token and reader position.</summary>
        /// <returns>True when a complete boolean literal was consumed.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadBoolean<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadBooleanOperation<TOptions>, bool>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadBooleanOperation<TOptions> : ILuminPackJsonReadOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadBooleanEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadBooleanEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                if (Current == 't')
                {
                    if (Remaining >= 4 && Buffer[Position + 1] == 'r' && Buffer[Position + 2] == 'u' && Buffer[Position + 3] == 'e')
                    {
                        Advance(4);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.True;
                        return true;
                    }
                }
                else if (Current == 'f')
                {
                    if (Remaining >= 5 && Buffer[Position + 1] == 'a' && Buffer[Position + 2] == 'l' && Buffer[Position + 3] == 's' && Buffer[Position + 4] == 'e')
                    {
                        Advance(5);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.False;
                        return true;
                    }
                }
            }
            else
            {
                char c = LuminPackReader.Read<char>(ref Current);
                if (c == 't')
                {
                    if (Remaining >= 8)
                    {
                        char c1 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                        char c2 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                        char c3 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                        if (c1 == 'r' && c2 == 'u' && c3 == 'e')
                        {
                            Advance(8);
                            RequireTokenEndBoundary<TOptions>(Position);
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.True;
                            return true;
                        }
                    }
                }
                else if (c == 'f')
                {
                    if (Remaining >= 10)
                    {
                        char c1 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                        char c2 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                        char c3 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                        char c4 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 8)));
                        if (c1 == 'a' && c2 == 'l' && c3 == 's' && c4 == 'e')
                        {
                            Advance(10);
                            RequireTokenEndBoundary<TOptions>(Position);
                            GrammarState = JsonStateAfterValue;
                            CurrentTokenType = JsonTokenType.False;
                            return true;
                        }
                    }
                }
            }

            LuminPackExceptionHelper.ThrowFormatException("Invalid boolean value");
            return false;
        }

        /// <summary>Reads null from the current input.</summary>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadNull<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            return default(TOptions).ReadJsonValue<ReadNullOperation<TOptions>, bool>(ref this);
        }

        /// <summary>Runs the scalar kernel with the document codec's concrete code unit.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        private readonly struct ReadNullOperation<TOptions> : ILuminPackJsonReadOperation<bool> where TOptions : struct, ILuminPackOptions
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Read<TCodeUnit>(scoped ref LuminPackJsonReader reader) where TCodeUnit : unmanaged =>
                reader.ReadNullEncoded<TOptions, TCodeUnit>();
        }

        /// <summary>Reads the value in the selected document representation.</summary>
        /// <typeparam name="TOptions">The compiled document profile.</typeparam>
        /// <typeparam name="TCodeUnit">The physical code unit.</typeparam>
        /// <returns>The decoded result.</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private bool ReadNullEncoded<TOptions, TCodeUnit>() where TOptions : struct, ILuminPackOptions where TCodeUnit : unmanaged
        {
            if ((Unsafe.SizeOf<TCodeUnit>() == 1))
            {
                if (Remaining >= 4 && Current == 'n' && Buffer[Position + 1] == 'u' && Buffer[Position + 2] == 'l' && Buffer[Position + 3] == 'l')
                {
                    Advance(4);
                    RequireTokenEndBoundary<TOptions>(Position);
                    GrammarState = JsonStateAfterValue;
                    CurrentTokenType = JsonTokenType.Null;
                    return true;
                }
            }
            else
            {
                if (Remaining >= 8)
                {
                    char c0 = LuminPackReader.Read<char>(ref Current);
                    char c1 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 2)));
                    char c2 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 4)));
                    char c3 = LuminPackReader.Read<char>(ref Unsafe.Add(ref MemoryMarshal.GetReference(Buffer), (nint)(uint)(Position + 6)));
                    if (c0 == 'n' && c1 == 'u' && c2 == 'l' && c3 == 'l')
                    {
                        Advance(8);
                        RequireTokenEndBoundary<TOptions>(Position);
                        GrammarState = JsonStateAfterValue;
                        CurrentTokenType = JsonTokenType.Null;
                        return true;
                    }
                }
            }

            LuminPackExceptionHelper.ThrowFormatException("Invalid null value");
            return false;
        }
    }
}
