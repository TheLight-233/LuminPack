using System.Buffers;
using System.Buffers.Binary;
using LuminPack.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;
using LuminPack.Utility;
using LuminPack.Utility.ViewModel;
using static LuminPack.Code.LuminPackMarshal;

#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Numerics;
using System.Text.Unicode;

#endif
namespace LuminPack.Core
{
    public ref partial struct LuminPackWriter
    {
        /// <summary>Returns the fixed framing width or the variable-length header upper bound of the current mode.</summary>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength()
        {
            return StringRecordLength<LuminPackDefaultOptions>();
        }

        /// <summary>Writes null utf8 length header to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNullUtf8LengthHeader(int index)
        {
            WriteEmptyOrNullUtf8LengthHeader(index, LuminPackCode.NullCollection);
        }

        /// <summary>Writes empty or null utf8 length header to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "length">The number of bytes or elements to process.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteEmptyOrNullUtf8LengthHeader(int index, int length)
        {
            EnsureCapacity(index, 8);
            // Both words belong to the record, even when the reader ignores the
            // character count. Initialize them together in a reused buffer.
            WriteReservedUtf8Header(index, length, length);
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithToken(int index, string? value)
        {
            return WriteUtf8WithToken<LuminPackDefaultOptions>(index, value);
        }

        /// <summary>Writes utf8 with length to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithLength(int index, string? value)
        {
            if (value is null)
            {
                WriteNullUtf8LengthHeader(index);
                return 0;
            }

            if (value.Length == 0)
            {
                WriteEmptyOrNullUtf8LengthHeader(index, 0);
                return 0;
            }

            var source = value.AsSpan();
#if NET8_0_OR_GREATER
            // Short ASCII records: encode straight into the reserved payload and store both header words at once,
            // without the status protocol's spilled out values or the worst-case width computation.
            if ((ulong)(uint)index + 8 + (uint)source.Length <= (uint)_buffer.Length &&
                ShortAsciiTranscoder.TryEncode(source, CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)index + 8), source.Length), escapeJson: false))
            {
                WriteReservedUtf8Header(index, source.Length, source.Length);
                return source.Length;
            }
#endif
            var startIndex = checked(index + 8);
            var dest = (uint)startIndex <= (uint)_buffer.Length ? _buffer.Slice(startIndex) : Span<byte>.Empty;
            // UTF8.GetMaxByteCount -> (length + 1) * 3
            var maxByteCount = checked((source.Length + 1) * 3);
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(source, dest, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                {
                    EnsureCapacity(index, checked(maxByteCount + 8));
                    bytesWritten = EncodeUtf8Slow(source, _buffer.Slice(index + 8));
                }
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            WriteReservedUtf8Header(index, bytesWritten, source.Length);
            return bytesWritten;
        }

        /// <summary>Encodes a UTF-8 payload into storage that was grown to its worst-case width.</summary>
        /// <param name = "value">The source characters.</param>
        /// <param name = "destination">The payload storage after the reserved header.</param>
        /// <returns>The encoded payload width.</returns>
        /// <remarks>Static, so retrying after growth never takes the writer's address.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int EncodeUtf8Slow(ReadOnlySpan<char> value, Span<byte> destination)
        {
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, destination, out _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
                LuminPackExceptionHelper.ThrowFailedEncoding(status);
            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteUtf16WithToken(int index, string? value)
        {
            return WriteUtf16WithToken<LuminPackDefaultOptions>(index, value);
        }

        /// <summary>Returns the size of the string header used by the current recording mode.</summary>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int StringRecordLength<TOptions>()
            where TOptions : struct, ILuminPackOptions
        {
            // UTF-16 token mode uses a two-byte 0x0000 terminator because individual zero bytes are common within code units.
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
                return default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 10 : 5;
            return (recording == LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 1 : 2) : (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8) ? 8 : 4;
        }

        /// <summary>
        /// Writes the string terminator.
        /// </summary>
        /// <param name = "index"></param>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void WriteStringRecordTokenHeader<TOptions>(int index)
            where TOptions : struct, ILuminPackOptions
        {
            if ((default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8))
            {
                // UTF-8 token mode uses a single zero-byte terminator.
#if NET8_0_OR_GREATER
                Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)index) = 0;
#else
                Unsafe.Add(ref Unsafe.AsRef<byte>(MemoryMarshal.GetReference(_buffer)), (nint)(uint)index) = 0;
#endif
            }
            else
            {
                // UTF-16 token mode terminates at the two-byte null character 0x0000.
                ushort token = 0;
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(index)), ref Unsafe.As<ushort, byte>(ref token), sizeof(ushort));
            }
        }

        /// <summary>Writes utf8 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int WriteUtf8WithToken<TOptions>(int index, string? value)
            where TOptions : struct, ILuminPackOptions
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 1);
                WriteStringRecordTokenHeader<LuminPackUtf8TokenOptions>(index);
                return 0;
            }

            var destination = (uint)index <= (uint)_buffer.Length ? _buffer.Slice(index) : Span<byte>.Empty;
            var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, destination, out var _, out var bytesWritten, replaceInvalidSequences: false);
            if (status != OperationStatus.Done)
            {
                if (status == OperationStatus.DestinationTooSmall)
                {
                    EnsureCapacity(index, checked((value.Length + 1) * 3 + 1));
                    bytesWritten = EncodeUtf8Slow(value.AsSpan(), _buffer.Slice(index));
                }
                else
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }

            EnsureCapacity(index, checked(bytesWritten + 1));
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader<LuminPackUtf8TokenOptions>(tokenIndex);
            return bytesWritten;
        }

        /// <summary>Writes utf16 with token to the current output.</summary>
        /// <param name = "index">The position at which the operation starts.</param>
        /// <param name = "value">The value consumed or produced by this operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private int WriteUtf16WithToken<TOptions>(int index, string? value)
            where TOptions : struct, ILuminPackOptions
        {
            if (string.IsNullOrEmpty(value) || value.Length is 0)
            {
                EnsureCapacity(index, 2);
                WriteStringRecordTokenHeader<LuminPackUtf16TokenOptions>(index);
                return 0;
            }

            int bytesWritten = checked(value.Length * sizeof(char));
            EnsureCapacity(index, checked(bytesWritten + 2));
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(index)), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytesWritten);
            var tokenIndex = index + bytesWritten;
            WriteStringRecordTokenHeader<LuminPackUtf16TokenOptions>(tokenIndex);
            return bytesWritten;
        }

        /// <summary>Writes the original two-word UTF-8 length header with the required architecture-specific access.</summary>
        /// <param name = "index">The reserved header position.</param>
        /// <param name = "bytes">The payload byte count or original null marker.</param>
        /// <param name = "characters">The UTF-16 code-unit count or original null marker.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteReservedUtf8Header(int index, int bytes, int characters)
        {
            long header = (long)(uint)bytes | ((long)characters << 32);
            if (Unsafe.SizeOf<IntPtr>() == 8)
                LuminPackWriter.Write(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(index)), header);
            else
                LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), (nint)(uint)(index)), ref Unsafe.As<long, byte>(ref header), 8);
        }

        /// <summary>Writes a complete string using the default compiled profile.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string? value) => Write<LuminPackDefaultOptions>(value);

        /// <summary>Writes a complete string record, including capacity, header and cursor advancement.</summary>
        /// <typeparam name = "TOptions">The stateless binary profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions => default(TOptions).Write(ref this, value);

        /// <summary>Writes a complete default-profile string within a generator-proven capacity region.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <param name = "capacityReserved">Whether the complete enclosing record has already reserved its proven upper bound.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(string? value, bool capacityReserved) => Write<LuminPackDefaultOptions>(value, capacityReserved);

        /// <summary>Writes and advances a string, optionally using the enclosing record's complete capacity proof.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        /// <param name = "capacityReserved">True only after reserving a proven bound for the complete enclosing operation.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<TOptions>(string? value, bool capacityReserved)
            where TOptions : struct, ILuminPackOptions
        {
            if (capacityReserved)
                WriteReservedRecord<TOptions>(value);
            else
                default(TOptions).Write(ref this, value);
        }

        /// <summary>Writes one Utf16Length string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf16LengthString(string? value)
        {
            // Room for the raw UTF-16 form; Latin-1 text then takes one byte per character. A string holds at
            // most 0x3FFFFFDF characters, so the payload and its header always fit in int.
            int recordLength = sizeof(int) + (value is null ? 0 : value.Length * sizeof(char));
            if (FitsRecord(recordLength))
            {
                // The record check proved the raw bound, which covers every written form.
                AdvanceProven(WriteUtf16Record(ref Current, value));
                return;
            }

            GrownStorage storage = WriteUtf16RecordInShortWindow(Owner, _buffer, Position, _bufferCheckLimit, value, recordLength, out Exception? failure);
            ApplyStringRecord(storage, failure);
            return;
        }

        /// <summary>Writes one Utf8Length string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf8LengthString(string? value)
        {
            int length = WriteUtf8WithLength(Position, value) + 8;
            if (value is null)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Writes one Utf8Token string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf8TokenString(string? value)
        {
            int length = WriteUtf8WithToken<LuminPackUtf8TokenOptions>(Position, value) + 1;
            if (value is null)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Writes one Utf16Token string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf16TokenString(string? value)
        {
            int length = WriteUtf16WithToken<LuminPackUtf16TokenOptions>(Position, value) + 2;
            if (value is null)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Writes one Utf8VarInt string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf8VarIntString(string? value)
        {
            int length = WriteVarIntStringBytes<LuminPackUtf8VarIntOptions>(value, false);
            if (value is null)
                Advance(length);
            else
                AdvanceSafe(length);
        }

        /// <summary>Writes one Utf16VarInt string record and publishes its cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteUtf16VarIntString(string? value)
        {
            WriteVarIntString<LuminPackUtf16VarIntOptions>(value, false);
        }

        /// <summary>Writes consecutive string records using the default compiled profile.</summary>
        /// <param name = "values">The strings, each including null or empty.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStrings(scoped ReadOnlySpan<string?> values) => WriteStrings<LuminPackDefaultOptions>(values);

        /// <summary>Writes every string of a span as consecutive records, exactly as one record write per element.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "values">The strings, each including null or empty.</param>
        /// <remarks>UTF-16 length records are written in runs whose raw upper bounds fit the existing storage, with the
        /// same per-record capacity proof as a single write; a record that does not fit takes the single-record path,
        /// which grows the storage. Other profiles write each record through the single-record path.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining | LuminPackJit.Optimized)]
        public void WriteStrings<TOptions>(scoped ReadOnlySpan<string?> values)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                int index = 0;
                while (true)
                {
                    uint limit = Math.Min((uint)_buffer.Length, BufferCheckLimit - 1);
                    uint position = (uint)Position;
                    Advance(WriteUtf16Run(ref Current, position < limit ? limit - position : 0, values, ref index));
                    if (index >= values.Length)
                        return;
                    Write<TOptions>(values[index++]);
                }
            }

            foreach (string? value in values)
                Write<TOptions>(value);
        }

        /// <summary>Writes a contiguous segment of strings whose original traversal checks the buffer before every record.</summary>
        /// <typeparam name="TOptions">The stateless binary string profile.</typeparam>
        /// <param name="values">The values in segment order.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining | LuminPackJit.Optimized)]
        public void WriteCheckedStrings<TOptions>(scoped ReadOnlySpan<string?> values) where TOptions : struct, ILuminPackOptions
        {
            if (UsesUtf16LengthRecords<TOptions>())
            {
                int index = 0;
                while (index < values.Length)
                {
                    uint limit = Math.Min((uint)_buffer.Length, BufferCheckLimit - 1);
                    uint position = (uint)Position;
                    AdvanceProven(WriteUtf16Run(ref Current, position < limit ? limit - position : 0, values, ref index));
                    if (index == values.Length)
                        return;
                    string? value = values[index++];
                    CheckBuffer();
                    Write<TOptions>(value);
                }
                return;
            }

            foreach (string? value in values)
            {
                CheckBuffer();
                Write<TOptions>(value);
            }
        }

        /// <summary>Returns whether the default profile writes UTF-16 length records, the form of reserved runs.</summary>
        /// <returns>Whether reserved runs apply.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesUtf16LengthRecords() => UsesUtf16LengthRecords<LuminPackDefaultOptions>();

        /// <summary>Returns whether a profile writes UTF-16 length records, the form of reserved runs.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <returns>A compile-time constant for the profile.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesUtf16LengthRecords<TOptions>()
            where TOptions : struct, ILuminPackOptions
            => default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length;

        #region Records

        /// <summary>Returns the signed distance from a caller-held cursor to the record limit.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <returns>The bytes an unchecked record may use there, negative once the threshold is passed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly nint RecordDistance(ref byte cursor)
        {
#if NET8_0_OR_GREATER
            return Unsafe.ByteOffset(ref cursor, ref _recordLimit);
#else
            return Unsafe.ByteOffset(ref cursor, ref MemoryMarshal.GetReference(_recordLimit));
#endif
        }

        /// <summary>Publishes a caller-held cursor as this writer's cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <remarks>Generated operations hold the cursor in a local while they write consecutive records and publish it before
        /// any operation that uses the writer itself and resume from the writer's cursor afterwards.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CommitRecords(ref byte cursor)
        {
#if NET8_0_OR_GREATER
            Cursor = ref MemoryMarshal.GetReference(MemoryMarshal.CreateSpan(ref cursor, 1));
#else
            Cursor = MemoryMarshal.CreateSpan(ref cursor, 1);
#endif
        }

        /// <summary>Returns the room an unchecked record may use at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <returns>The available bytes, negative once the threshold is passed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordRoom(ref byte cursor) => (int)Math.Min(RecordDistance(ref cursor), int.MaxValue);

        /// <summary>Ensures room for a record of known width at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "length">The record width.</param>
        /// <returns>The record position, followed by at least <paramref name = "length"/> writable bytes.</returns>
        /// <remarks>A record that does not fit publishes the cursor, applies the collection threshold and grows, exactly as a
        /// failed record check does, and continues at the published cursor.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte ReserveRecord(ref byte cursor, int length)
        {
            if ((Unsafe.SizeOf<IntPtr>() == 8 || length >= 0) && RecordDistance(ref cursor) >= (nint)(uint)length)
                return ref cursor;
            return ref GrowRecord(ref this, ref cursor, length);
        }

        /// <summary>Publishes the cursor and reserves a complete record outside its calling body.</summary>
        /// <param name = "writer">The state to refresh after growth.</param>
        /// <param name = "cursor">The current record position.</param>
        /// <param name = "length">The record width.</param>
        /// <returns>The committed cursor over the current storage window.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref byte GrowRecord(scoped ref LuminPackWriter writer, ref byte cursor, int length)
        {
            writer.CommitRecords(ref cursor);
            int position = writer.Position;
            if (position < 0 || length < 0)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(position);
            int required = checked(position + length);
            LuminBufferWriter? owner = writer.Owner;
            if (owner is not null && writer.RecordDistance(ref cursor) < 0)
            {
                owner.EnsureRecordCapacity(position, required);
                writer.Apply(new GrownStorage(owner, position));
            }
            else if (required > writer._buffer.Length)
                writer.Apply(Grow(owner, position, required, threshold: false));
            return ref writer.Current;
        }

        /// <summary>Writes one complete fixed value at a caller-held cursor.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The value.</param>
        /// <returns>The position after the value.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecord<T>(ref byte cursor, scoped in T value)
            where T : unmanaged
        {
            ref byte at = ref ReserveRecord(ref cursor, Unsafe.SizeOf<T>());
            Write(ref at, in value);
            return ref Unsafe.Add(ref at, (nint)Unsafe.SizeOf<T>());
        }

        /// <summary>Copies a proven contiguous fixed field block at a caller-held cursor.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "source">The first byte of the block.</param>
        /// <param name = "length">The block width.</param>
        /// <returns>The position after the block.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordBlock(ref byte cursor, scoped ref byte source, int length)
        {
            ref byte at = ref ReserveRecord(ref cursor, length);
            LuminPackMarshal.CopyBlockUnaligned(ref at, ref source, (uint)length);
            return ref Unsafe.Add(ref at, (nint)(uint)length);
        }

        /// <summary>Writes a string record at a caller-held cursor using the default compiled profile.</summary>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordString(ref byte cursor, string? value) => ref WriteRecordString<LuminPackDefaultOptions>(ref cursor, value);

        /// <summary>Writes a string record of the selected profile at a caller-held cursor.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        /// <remarks>A UTF-16 length record that fits its raw form is stored at the cursor, as the record check proves it. A record
        /// that does not fit and every other profile publish the cursor and take this writer's own string operation.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordString<TOptions>(ref byte cursor, string? value)
            where TOptions : struct, ILuminPackOptions
        {
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                int length = Utf16RecordBound(value);
                if (RecordDistance(ref cursor) >= (nint)(uint)length)
                    return ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedUtf16Record(ref cursor, value));
                if (typeof(TOptions) == typeof(LuminPackDefaultOptions) || typeof(TOptions) == typeof(LuminPackUtf16LengthOptions))
                    return ref WriteUtf16RecordInShortWindow(ref this, ref cursor, value, length);
            }

            CommitRecords(ref cursor);
            Write<TOptions>(value);
            return ref Current;
        }

        /// <summary>Publishes a caller-held cursor and completes its UTF-16 record outside the hot writer.</summary>
        /// <param name = "writer">The writer whose window and cursor may change.</param>
        /// <param name = "cursor">The position at the start of the record.</param>
        /// <param name = "value">The nullable string.</param>
        /// <param name = "recordLength">The full raw width.</param>
        /// <returns>The current cursor after the record.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref byte WriteUtf16RecordInShortWindow(scoped ref LuminPackWriter writer, ref byte cursor, string? value, int recordLength)
        {
            writer.CommitRecords(ref cursor);
            GrownStorage storage = WriteUtf16RecordInShortWindow(writer.Owner, writer._buffer, writer.Position, writer._bufferCheckLimit, value, recordLength, out Exception? failure);
            writer.ApplyStringRecord(storage, failure);
            return ref writer.Current;
        }

        /// <summary>Writes a raw contiguous array record at a caller-held cursor.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "array">The array, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordSpan<T>(ref byte cursor, T[]? array)
            where T : unmanaged
        {
            ReadOnlySpan<T> elements = array;
            ref byte at = ref ReserveRecord(ref cursor, SpanRecordBound<T>(array is null, elements.Length));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedSpanOutlined(ref at, array is null, elements));
        }

        /// <summary>Writes a raw contiguous list record from one snapshot of its storage at a caller-held cursor.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "list">The list, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordSpan<T>(ref byte cursor, List<T>? list)
            where T : unmanaged
        {
            ReadOnlySpan<T> elements = list is null ? default : LuminPackMarshal.GetListSpan(list);
            ref byte at = ref ReserveRecord(ref cursor, SpanRecordBound<T>(list is null, elements.Length));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedSpanOutlined(ref at, list is null, elements));
        }

        /// <summary>Writes a raw-pair dictionary record of its current count at a caller-held cursor.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "cursor">The caller's current position in this writer's storage.</param>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteRecordPairs<TKey, TValue>(ref byte cursor, Dictionary<TKey, TValue>? dictionary)
            where TKey : unmanaged where TValue : unmanaged
        {
            int count = dictionary is null ? 0 : dictionary.Count;
            ref byte at = ref ReserveRecord(ref cursor, PairRecordBound<TKey, TValue>(dictionary is null, count));
            return ref Unsafe.Add(ref at, (nint)(uint)WriteReservedRawPairs(ref at, dictionary, count));
        }

        /// <summary>Writes a raw contiguous record outside the calling root.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "elements">The captured elements.</param>
        /// <returns>The written record width.</returns>
        /// <remarks>A bulk copy dominates its own call; keeping the size-dispatched copy out of every root preserves the root's
        /// inlining budget for its other members.</remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteReservedSpanOutlined<T>(ref byte destination, bool isNull, ReadOnlySpan<T> elements)
            where T : unmanaged
            => WriteReservedSpan(ref destination, isNull, elements);

        /// <summary>Returns the raw upper bound of a string record of the selected profile.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The framing plus the widest encoding of every character.</returns>
        /// <remarks>UTF-16 records hold two bytes per character, UTF-8 at most three, so a reservation of this width covers every
        /// form <see cref = "WriteReservedRecordString{TOptions}(ref byte, string)"/> writes.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordBound<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions => default(TOptions).RecordBound(value);

        /// <summary>Returns the raw upper bound of a default-profile string record.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The framing plus the widest encoding of every character.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int RecordBound(string? value) => RecordBound<LuminPackDefaultOptions>(value);

        /// <summary>Writes a string record of the selected profile into room a reservation of its bound proved.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by at least the record's bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteReservedRecordString<TOptions>(ref byte cursor, string? value)
            where TOptions : struct, ILuminPackOptions => ref default(TOptions).WriteRecord(ref this, ref cursor, value);

        /// <summary>Writes a default-profile string record into room a reservation of its bound proved.</summary>
        /// <param name = "cursor">The caller's current position, followed by at least the record's bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref byte WriteReservedRecordString(ref byte cursor, string? value) => ref WriteReservedRecordString<LuminPackDefaultOptions>(ref cursor, value);

        /// <summary>Stores one complete fixed value into reserved room.</summary>
        /// <typeparam name = "T">The unmanaged storage type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the value.</param>
        /// <param name = "value">The value.</param>
        /// <returns>The position after the value.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<T>(ref byte cursor, scoped in T value)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)Write(ref cursor, in value));

        /// <summary>Copies a proven contiguous fixed field block into reserved room.</summary>
        /// <param name = "cursor">The caller's current position, followed by room for the block.</param>
        /// <param name = "source">The first byte of the block.</param>
        /// <param name = "length">The block width.</param>
        /// <returns>The position after the block.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReservedBlock(ref byte cursor, scoped ref byte source, int length)
        {
            LuminPackMarshal.CopyBlockUnaligned(ref cursor, ref source, (uint)length);
            return ref Unsafe.Add(ref cursor, (nint)(uint)length);
        }

        /// <summary>Captures an array as one raw contiguous record.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "array">The array, or null.</param>
        /// <returns>The record's elements, null state and exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawRecord<T> Capture<T>(T[]? array)
            where T : unmanaged
            => new RawRecord<T>(array, array is null);

        /// <summary>Captures one snapshot of a list's storage as a raw contiguous record.</summary>
        /// <typeparam name = "T">An element type whose formatter copies its native storage.</typeparam>
        /// <param name = "list">The list, or null.</param>
        /// <returns>The record's elements, null state and exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawRecord<T> Capture<T>(List<T>? list)
            where T : unmanaged
        {
            Span<T> span = LuminPackMarshal.GetListSpan(list);
            return new RawRecord<T>(LuminPackMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(span), span.Length), list is null);
        }

        /// <summary>Captures a raw-pair dictionary record with its current count.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <returns>The dictionary, its captured count and the record's exact width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RawPairsRecord<TKey, TValue> Capture<TKey, TValue>(Dictionary<TKey, TValue>? dictionary)
            where TKey : unmanaged where TValue : unmanaged
            => new RawPairsRecord<TKey, TValue>(dictionary, dictionary is null ? 0 : dictionary.Count);

        /// <summary>Writes a captured raw contiguous record into reserved room.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "record">The captured record.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<T>(ref byte cursor, RawRecord<T> record)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedSpanOutlined(ref cursor, record.IsNull, record.Elements));

        /// <summary>Writes a captured raw contiguous record kept on an outlined entry boundary.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "record">The captured record.</param>
        /// <returns>The position after the record.</returns>
        /// <remarks>A keyed entry keeps this call, whose size-dispatched copy stays out of its compact body. Sequence
        /// entries inline their element record and replace this call with <see cref = "WriteReservedSpanInline{T}"/>.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static ref byte WriteReservedSpanRecord<T>(ref byte cursor, RawRecord<T> record)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedSpanOutlined(ref cursor, record.IsNull, record.Elements));

        /// <summary>Writes a captured raw contiguous record inside an inlined element record.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "isNull">Whether the collection was null.</param>
        /// <param name = "elements">The captured elements.</param>
        /// <returns>The position after the record.</returns>
        /// <remarks>The enclosing record reservation proved the room, so this path inlines the exact stores instead of
        /// paying a call per element for a small payload.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReservedSpanInline<T>(ref byte cursor, bool isNull, scoped ReadOnlySpan<T> elements)
            where T : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedSpan(ref cursor, isNull, elements));

        /// <summary>Writes a captured raw-pair dictionary record of exactly its captured count into reserved room.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "cursor">The caller's current position, followed by room for the record's width.</param>
        /// <param name = "record">The captured record.</param>
        /// <returns>The position after the record.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte WriteReserved<TKey, TValue>(ref byte cursor, RawPairsRecord<TKey, TValue> record)
            where TKey : unmanaged where TValue : unmanaged
            => ref Unsafe.Add(ref cursor, (nint)(uint)WriteReservedRawPairs(ref cursor, record.Dictionary, record.Count));

        /// <summary>A raw contiguous record whose elements are read once for both its width and its stores.</summary>
        /// <typeparam name = "T">The raw element type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly ref struct RawRecord<T>
            where T : unmanaged
        {
            /// <summary>The captured elements.</summary>
            public readonly ReadOnlySpan<T> Elements;
            /// <summary>Whether the collection was null.</summary>
            public readonly bool IsNull;

            /// <summary>Captures a record.</summary>
            /// <param name = "elements">The elements.</param>
            /// <param name = "isNull">Whether the collection was null.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawRecord(ReadOnlySpan<T> elements, bool isNull)
            {
                Elements = elements;
                IsNull = isNull; }

            /// <summary>Gets the record's exact width.</summary>
            public int Bound {[MethodImpl(MethodImplOptions.AggressiveInlining)] get => SpanRecordBound<T>(IsNull, Elements.Length); }
        }

        /// <summary>A raw-pair dictionary record whose count is read once for both its width and its stores.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly struct RawPairsRecord<TKey, TValue>
            where TKey : unmanaged where TValue : unmanaged
        {
            /// <summary>The dictionary, or null.</summary>
            public readonly Dictionary<TKey, TValue>? Dictionary;
            /// <summary>The captured pair count.</summary>
            public readonly int Count;

            /// <summary>Captures a record.</summary>
            /// <param name = "dictionary">The dictionary.</param>
            /// <param name = "count">The captured count.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawPairsRecord(Dictionary<TKey, TValue>? dictionary, int count)
            {
                Dictionary = dictionary;
                Count = count; }

            /// <summary>Gets the record's exact width.</summary>
            public int Bound {[MethodImpl(MethodImplOptions.AggressiveInlining)] get => PairRecordBound<TKey, TValue>(Dictionary is null, Count); }
        }

        /// <summary>Writes a UTF-16 length string when its raw bound did not pass the record check.</summary>
        /// <param name = "owner">The optional output owner.</param>
        /// <param name = "buffer">The current storage window, which can still fit the compact string.</param>
        /// <param name = "position">The committed record position.</param>
        /// <param name = "checkLimit">The first cursor position requiring an owned-buffer growth check.</param>
        /// <param name = "value">The nullable string.</param>
        /// <param name = "recordLength">The full raw width.</param>
        /// <param name = "failure">An exception to rethrow after publishing a successful capacity growth.</param>
        /// <returns>The output window and cursor after the complete string record.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static GrownStorage WriteUtf16RecordInShortWindow(LuminBufferWriter? owner, Span<byte> buffer, int position, uint checkLimit, string? value, int recordLength,
            out Exception? failure)
        {
            failure = null;
            if (position < 0)
                LuminPackExceptionHelper.ThrowSpanOutOfRange(position);
            ref byte current = ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), (nint)position);
            int written = WriteUtf16RecordSlow(ref current, buffer.Length - position, value);
            if (written >= 0)
                return CompleteShortWindowString(owner, buffer, checkLimit, position + written, value is not null);

            GrownStorage growth = GrowForCapacity(owner, position, position, recordLength);
            try
            {
                ref byte grown = ref Unsafe.Add(ref MemoryMarshal.GetReference(growth.Buffer), (nint)position);
                written = WriteUtf16RecordSlow(ref grown, growth.Buffer.Length - position, value);
                return CompleteShortWindowString(owner, growth.Buffer, growth.CheckLimit, position + written, value is not null);
            }
            catch (Exception exception)
            {
                // The first growth can return the writer's original array to the pool. Publish its
                // replacement at the committed cursor before propagating any later failure.
                failure = exception;
                return growth;
            }
        }

        /// <summary>Applies the original threshold check after a non-null string has been stored.</summary>
        /// <param name = "owner">The optional output owner.</param>
        /// <param name = "buffer">The window containing the completed record.</param>
        /// <param name = "checkLimit">The cached output growth check limit.</param>
        /// <param name = "end">The absolute end of the complete record.</param>
        /// <param name = "checkThreshold">Whether the record was non-null.</param>
        /// <returns>The cursor at the end of the complete string record.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static GrownStorage CompleteShortWindowString(LuminBufferWriter? owner, Span<byte> buffer, uint checkLimit, int end, bool checkThreshold)
        {
            if (checkThreshold && owner is not null && (uint)end >= checkLimit)
                return GrowForThreshold(owner, end, end);
            return new GrownStorage(buffer, checkLimit, end);
        }

        /// <summary>Publishes current string storage before propagating any failure after its growth.</summary>
        /// <param name = "storage">The latest valid window and cursor.</param>
        /// <param name = "failure">An exception raised after that storage was acquired.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyStringRecord(GrownStorage storage, Exception? failure)
        {
            Apply(storage);
            if (failure is not null)
                LuminPackExceptionHelper.Rethrow(failure);
        }

        #endregion

        /// <summary>Moves the cursor past records written into room a preceding record check proved.</summary>
        /// <param name = "written">The run's written width, at most its reserved bound.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteRun(int written) => AdvanceProven(written);

        /// <summary>Returns the raw upper bound of a UTF-16 length string record.</summary>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The header plus two bytes per character.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Utf16RecordBound(string? value) => sizeof(int) + (value is null ? 0 : value.Length * sizeof(char));

        /// <summary>Returns the exact width of a raw contiguous collection record.</summary>
        /// <typeparam name = "T">The raw element storage type.</typeparam>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "length">The captured element count.</param>
        /// <returns>The header plus the checked payload width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SpanRecordBound<T>(bool isNull, int length)
            where T : unmanaged
            => isNull ? sizeof(int) : checked(sizeof(int) + length * Unsafe.SizeOf<T>());

        /// <summary>Writes a UTF-16 length string record into reserved room of its raw bound.</summary>
        /// <param name = "destination">The record position, followed by room for its raw bound.</param>
        /// <param name = "value">The string, including null or empty.</param>
        /// <returns>The written record width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteReservedUtf16Record(ref byte destination, string? value)
        {
#if NET8_0_OR_GREATER
            if (value is not null && Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(value.Length - 1) <= 15)
            {
                int header = StringSerializer.TryWriteShortLatin1Payload(ref Unsafe.Add(ref destination, sizeof(int)), value);
                if (header != 0)
                {
                    LuminPackWriter.Write(ref destination, header);
                    return sizeof(int) + value.Length;
                }
            }
#endif
            if (string.IsNullOrEmpty(value))
            {
                LuminPackWriter.Write<int>(ref destination, value is null ? LuminPackCode.NullCollection : 0);
                return sizeof(int);
            }

            // Every other record takes exactly one outlined call for its payload.
            int payloadHeader = StringSerializer.WriteUtf16RecordPayloadSlow(ref Unsafe.Add(ref destination, sizeof(int)), value);
            LuminPackWriter.Write(ref destination, payloadHeader);
            return sizeof(int) + StringSerializer.Utf16RecordPayload(payloadHeader);
        }

        /// <summary>Writes a raw contiguous collection record into reserved room.</summary>
        /// <typeparam name = "T">The raw element storage type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "isNull">Whether the collection is null.</param>
        /// <param name = "elements">The captured elements.</param>
        /// <returns>The written record width.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteReservedSpan<T>(ref byte destination, bool isNull, scoped ReadOnlySpan<T> elements)
            where T : unmanaged
        {
            if (isNull)
            {
                LuminPackWriter.Write<int>(ref destination, LuminPackCode.NullCollection);
                return sizeof(int);
            }

            int bytes = elements.Length * Unsafe.SizeOf<T>();
            LuminPackWriter.Write<int>(ref destination, elements.Length);
            if (bytes != 0)
                LuminPackMarshal.CopyCollectionPayload(ref Unsafe.Add(ref destination, sizeof(int)),
                    ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(elements)), (uint)bytes);
            return sizeof(int) + bytes;
        }

        /// <summary>Returns the exact width of a raw-pair dictionary record.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "isNull">Whether the dictionary is null.</param>
        /// <param name = "count">The captured pair count.</param>
        /// <returns>The header plus the checked pair payload.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PairRecordBound<TKey, TValue>(bool isNull, int count)
            where TKey : unmanaged where TValue : unmanaged
            => isNull ? sizeof(int) : checked(sizeof(int) + count * (Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>()));

        /// <summary>Writes a raw-pair dictionary record of exactly <paramref name = "count"/> pairs into reserved room.</summary>
        /// <typeparam name = "TKey">The raw key type.</typeparam>
        /// <typeparam name = "TValue">The raw value type.</typeparam>
        /// <param name = "destination">The record position, followed by room for its exact width.</param>
        /// <param name = "dictionary">The dictionary, or null.</param>
        /// <param name = "count">The pair count captured with the bound.</param>
        /// <returns>The written record width.</returns>
        /// <remarks>Dense 64-bit storage copies the live entries and then checks the version, count and free count as the
        /// original dense path does; any other dictionary enumerates and must yield exactly the captured count.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int WriteReservedRawPairs<TKey, TValue>(ref byte destination, Dictionary<TKey, TValue>? dictionary, int count)
            where TKey : unmanaged where TValue : unmanaged
        {
            if (dictionary is null)
            {
                LuminPackWriter.Write<int>(ref destination, LuminPackCode.NullCollection);
                return sizeof(int);
            }

            LuminPackWriter.Write<int>(ref destination, count);
            int pair = Unsafe.SizeOf<TKey>() + Unsafe.SizeOf<TValue>();
            if (count == 0)
                return sizeof(int);
            if (Unsafe.SizeOf<IntPtr>() == 8)
            {
                var view = LuminPackMarshal.GetDictionaryView(dictionary);
                if (view._count == count && view._freeCount == 0)
                {
                    int version = view._version;
                    LuminPackMarshal.CopyDenseRawPairs(ref Unsafe.Add(ref destination, sizeof(int)), dictionary, count);
                    if (view._version != version || view._count != count || view._freeCount != 0)
                        LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
                    return sizeof(int) + count * pair;
                }
            }

            int written = 0;
            foreach (var item in dictionary)
            {
                if (written == count)
                    LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
                ref byte target = ref Unsafe.Add(ref destination, unchecked((nint)(uint)(sizeof(int) + written * pair)));
                LuminPackWriter.Write(ref target, item.Key);
                LuminPackWriter.Write(ref Unsafe.Add(ref target, unchecked((nint)(uint)(Unsafe.SizeOf<TKey>()))), item.Value);
                written++;
            }

            if (written != count)
                LuminPackExceptionHelper.ThrowCollectionModifiedDuringSerialization();
            return sizeof(int) + count * pair;
        }

        /// <summary>Returns the room an unchecked record may use at the cursor: the storage and the collection threshold.</summary>
        /// <returns>The available bytes, negative once the threshold is passed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RecordRoom() => (int)RecordDistance();

        /// <summary>Writes key and value strings as alternating records using the default compiled profile.</summary>
        /// <param name = "keys">The keys.</param>
        /// <param name = "values">The values, at least as many as the keys.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringPairs(scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values) => WriteStringPairs<LuminPackDefaultOptions>(keys, values);

        /// <summary>Writes key and value strings as alternating records, exactly as a buffer check and two record writes per pair.</summary>
        /// <typeparam name = "TOptions">The stateless binary string profile.</typeparam>
        /// <param name = "keys">The keys.</param>
        /// <param name = "values">The values, at least as many as the keys.</param>
        /// <remarks>UTF-16 length pairs are written in runs whose raw upper bounds fit the existing storage, where the
        /// buffer check cannot act; a pair that does not fit takes the per-pair path.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringPairs<TOptions>(scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values)
            where TOptions : struct, ILuminPackOptions
        {
            values = values.Slice(0, keys.Length);
            int index = 0;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16 && default(TOptions).StringRecording == LuminPackStringRecording.Length)
            {
                while (true)
                {
                    uint limit = Math.Min((uint)_buffer.Length, BufferCheckLimit - 1);
                    uint position = (uint)Position;
                    Advance(WriteUtf16PairRun(ref Current, position < limit ? limit - position : 0, keys, values, ref index));
                    if (index >= keys.Length)
                        return;
                    CheckBuffer();
                    Write<TOptions>(keys[index]);
                    Write<TOptions>(values[index++]);
                }
            }

            for (; index < keys.Length; index++)
            {
                CheckBuffer();
                Write<TOptions>(keys[index]);
                Write<TOptions>(values[index]);
            }
        }

        /// <summary>Writes parallel string slots in pair order with the original per-string capacity fallback.</summary>
        /// <typeparam name="TOptions">The compiled binary string profile.</typeparam>
        /// <param name="keys">The key slots in enumeration order.</param>
        /// <param name="values">The corresponding value slots.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void WriteStringPairRuns<TOptions>(scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values)
            where TOptions : struct, ILuminPackOptions
        {
            values = values.Slice(0, keys.Length);
            int index = 0;
#if NET8_0_OR_GREATER
            if (ConstantStringProfile<TOptions>() && UsesUtf16LengthRecords<TOptions>())
            {
                while (index < keys.Length)
                {
                    AdvanceProven(WriteUtf16PairRun(ref Current, PrefixRoom(), keys, values, ref index));
                    if (index >= keys.Length)
                        return;

                    Write<TOptions>(keys[index]);
                    Write<TOptions>(values[index++]);
                }
                return;
            }
#endif
            for (; index < keys.Length; index++)
            {
                Write<TOptions>(keys[index]);
                Write<TOptions>(values[index]);
            }
        }

        /// <summary>Writes the leading string records that fit the default profile's current window.</summary>
        /// <param name="values">The source slots in wire order.</param>
        /// <returns>The number of complete records written.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int WriteStringPrefix(scoped ReadOnlySpan<string?> values) => WriteStringPrefix<LuminPackDefaultOptions>(values);

        /// <summary>Writes leading records for which the caller's per-element buffer check cannot grow storage.</summary>
        /// <typeparam name="TOptions">The compiled string profile.</typeparam>
        /// <param name="values">The source slots in wire order.</param>
        /// <returns>The number of complete records written, or zero for a profile with observable getters.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteStringPrefix<TOptions>(scoped ReadOnlySpan<string?> values) where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (ConstantStringProfile<TOptions>() && UsesUtf16LengthRecords<TOptions>())
            {
                int count = 0;
                AdvanceProven(WriteUtf16Run(ref Current, PrefixRoom(), values, ref count));
                return count;
            }
#endif
            return 0;
        }

        /// <summary>Writes the leading captured string pairs that fit the default profile's current window.</summary>
        /// <param name="pairs">The pairs in their original enumeration order.</param>
        /// <returns>The number of complete pairs written.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int WriteStringPairPrefix(scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs) => WriteStringPairPrefix<LuminPackDefaultOptions>(pairs);

        /// <summary>Writes complete pairs that fit before the next required capacity or profile check.</summary>
        /// <typeparam name="TOptions">The compiled string profile.</typeparam>
        /// <param name="pairs">The pairs in their original enumeration order.</param>
        /// <returns>The number of complete pairs written. Unrecognized profiles leave all records to the caller.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int WriteStringPairPrefix<TOptions>(scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs)
            where TOptions : struct, ILuminPackOptions
        {
#if NET8_0_OR_GREATER
            if (ConstantStringProfile<TOptions>() && UsesUtf16LengthRecords<TOptions>())
                return WriteUtf16PairPrefix(pairs);
#endif
            return 0;
        }

#if NET8_0_OR_GREATER
        /// <summary>Returns the bytes between an in-range cursor and the next capacity or completion limit.</summary>
        /// <returns>Available bytes, or zero for a cursor outside the window.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint PrefixRoom()
        {
            nuint offset = (nuint)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(_buffer), ref Current);
            uint limit = Math.Min((uint)_buffer.Length, BufferCheckLimit - 1);
            return offset < limit ? limit - (uint)offset : 0;
        }

        /// <summary>Writes complete captured key/value pairs while both raw record bounds fit the current window.</summary>
        /// <param name="pairs">The captured pairs.</param>
        /// <returns>The number of complete pairs written.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int WriteUtf16PairPrefix(scoped ReadOnlySpan<KeyValuePair<string, string?>> pairs)
        {
            int room = (int)PrefixRoom();
            int count = pairs.Length;
            if (room == 0 || count == 0)
                return 0;
            ref byte start = ref Current;
            ref var first = ref MemoryMarshal.GetReference(pairs);
            int written = 0;
            int i = 0;
            do
            {
                var pair = Unsafe.Add(ref first, (nint)(uint)i);
                long chars = (long)(pair.Key?.Length ?? 0) + (pair.Value?.Length ?? 0);
                if (2 * sizeof(int) + chars * sizeof(char) > (long)room - written)
                    break;
                written += WriteReservedUtf16Record(ref Unsafe.Add(ref start, (nint)(uint)written), pair.Key);
                written += WriteReservedUtf16Record(ref Unsafe.Add(ref start, (nint)(uint)written), pair.Value);
            } while (++i < count);
            AdvanceProven(written);
            return i;
        }
#endif

        /// <summary>Writes UTF-16 length record pairs while the raw upper bound of the next pair fits the remaining room.</summary>
        /// <param name="current">The first record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/> under the capacity and completion limits.</param>
        /// <param name="keys">The keys.</param>
        /// <param name="values">The values, exactly as many as the keys.</param>
        /// <param name="index">The first pair to write, updated to the first pair not written.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf16PairRun(ref byte current, uint room, scoped ReadOnlySpan<string?> keys, scoped ReadOnlySpan<string?> values, ref int index)
        {
            int count = keys.Length;
            int i = index;
            if ((uint)i >= (uint)count)
                return 0;
            ref string? firstKey = ref MemoryMarshal.GetReference(keys);
            ref string? firstValue = ref MemoryMarshal.GetReference(values);
            uint used = 0;
            do
            {
                string? key = Unsafe.Add(ref firstKey, (nint)(uint)i);
                string? value = Unsafe.Add(ref firstValue, (nint)(uint)i);
                // Two strings hold at most 2 * 0x3FFFFFDF characters, so the raw bound fits uint.
                uint bound = 2 * sizeof(int) + ((key is null ? 0u : (uint)key.Length) + (value is null ? 0u : (uint)value.Length)) * sizeof(char);
                if (bound > room - used)
                    break;
                used += (uint)WriteUtf16Record(ref Unsafe.Add(ref current, (nint)used), key, outlineMedium: true);
                used += (uint)WriteUtf16Record(ref Unsafe.Add(ref current, (nint)used), value, outlineMedium: true);
            } while (++i < count);

            index = i;
            return (int)used;
        }

        /// <summary>Writes UTF-16 length records while the raw upper bound of the next record fits the remaining room.</summary>
        /// <param name="current">The first record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/> under the capacity and completion limits.</param>
        /// <param name="values">The strings.</param>
        /// <param name="index">The first string to write, updated to the first string not written.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUtf16Run(ref byte current, uint room, scoped ReadOnlySpan<string?> values, ref int index)
        {
#if NET8_0_OR_GREATER
            if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)index < (uint)values.Length &&
                values[index] is { Length: >= 17 and <= 32 })
                return WriteMediumUtf16Run(ref current, room, values, ref index);
#endif
            return WriteGeneralUtf16Run(ref current, room, values, ref index);
        }

        /// <summary>Writes consecutive UTF-16 length records of any width.</summary>
        /// <param name="current">The first output byte.</param>
        /// <param name="room">The writable bytes before the capacity or completion limit.</param>
        /// <param name="values">The string slots in storage order.</param>
        /// <param name="index">The first remaining slot, advanced only after complete records.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteGeneralUtf16Run(ref byte current, uint room, scoped ReadOnlySpan<string?> values, ref int index)
        {
            int count = values.Length;
            int i = index;
            if ((uint)i >= (uint)count)
                return 0;
            ref string? first = ref MemoryMarshal.GetReference(values);
            uint remaining = room;
            do
            {
                string? value = Unsafe.Add(ref first, (nint)(uint)i);
                // A string holds at most 0x3FFFFFDF characters, so the raw bound fits uint.
                int length = value?.Length ?? 0;
                uint bound = sizeof(int) + (uint)length * sizeof(char);
                if (bound > remaining)
                    break;
                int written = WriteUtf16SpanRecord(ref current, value, length);
                remaining -= (uint)written;
                current = ref Unsafe.Add(ref current, (nint)(uint)written);
            } while (++i < count);

            index = i;
            return (int)(room - remaining);
        }

        /// <summary>Writes a mixed-width span element within its validated raw bound.</summary>
        /// <param name="current">The start of the element record.</param>
        /// <param name="value">The original string, including null or empty.</param>
        /// <param name="length">The length used to prove the raw bound, or zero for null.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUtf16SpanRecord(ref byte current, string? value, int length)
        {
            if (length == 0)
            {
                Write(ref current, value is null ? LuminPackCode.NullCollection : 0);
                return sizeof(int);
            }

            ref byte payload = ref Unsafe.Add(ref current, sizeof(int));
            int header;
#if NET8_0_OR_GREATER
            if (Vector128.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 1) <= 15)
            {
                int compactHeader = StringSerializer.TryWriteShortUtf16RecordPayload(ref payload, value!, length);
                if (compactHeader != 0)
                {
                    Write(ref current, compactHeader);
                    return sizeof(int) + length;
                }
            }

            if (Vector256.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(length - 17) <= 47)
            {
                if (length <= 32)
                    return WriteMediumUtf16Record(ref current, value!, length);
                header = StringSerializer.WriteMediumUtf16RecordPayload256(ref payload, ref Unsafe.AsRef(in value!.GetPinnableReference()),
                    length, outlineRaw: true);
            }
            else
#endif
                header = StringSerializer.WriteUtf16RecordPayloadSlow(ref payload, value!);
            Write(ref current, header);
            return sizeof(int) + StringSerializer.Utf16RecordPayload(header);
        }

#if NET8_0_OR_GREATER
        /// <summary>Writes medium records within the validated window and resumes general encoding at the first other width.</summary>
        /// <param name="current">The first output byte.</param>
        /// <param name="room">The writable bytes before the capacity or completion limit.</param>
        /// <param name="values">The string slots in storage order.</param>
        /// <param name="index">The first remaining slot, advanced only after complete records.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteMediumUtf16Run(ref byte current, uint room, scoped ReadOnlySpan<string?> values, ref int index)
        {
            int count = values.Length;
            int i = index;
            if ((uint)i >= (uint)count)
                return 0;
            ref string? first = ref MemoryMarshal.GetReference(values);
            uint remaining = room;
            do
            {
                string? value = Unsafe.Add(ref first, (nint)(uint)i);
                if (value is null || (uint)(value.Length - 17) > 15)
                {
                    index = i;
                    return (int)(room - remaining) + WriteGeneralUtf16Run(ref current, remaining, values, ref index);
                }
                uint bound = sizeof(int) + (uint)value.Length * sizeof(char);
                if (bound > remaining)
                    break;
                int written = WriteMediumUtf16Record(ref current, value, value.Length);
                remaining -= (uint)written;
                current = ref Unsafe.Add(ref current, (nint)(uint)written);
            } while (++i < count);

            index = i;
            return (int)(room - remaining);
        }

        /// <summary>Writes one reserved 17-to-32-character record.</summary>
        /// <param name="record">The record start, with room for the header and the raw UTF-16 payload.</param>
        /// <param name="value">The string, on a 64-bit target with accelerated 256-bit vectors.</param>
        /// <param name="length">The string's length.</param>
        /// <returns>The width of the complete compact or raw record.</returns>
        /// <remarks>Two 16-character blocks cover the whole input and stay in bounds even at length 17.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteMediumUtf16Record(ref byte record, string value, int length)
        {
            nuint tail = (nuint)(uint)(length - 16);
            ref ushort chars = ref Unsafe.As<char, ushort>(ref Unsafe.AsRef(in value.GetPinnableReference()));
            ref byte payload = ref Unsafe.Add(ref record, sizeof(int));
            var head = Vector256.LoadUnsafe(ref chars);
            var end = Vector256.LoadUnsafe(ref chars, tail);
            if (((head | end) & Vector256.Create((ushort)0xFF00)) == Vector256<ushort>.Zero)
            {
                var packed = StringSerializer.NarrowValidatedLatin1(head, end);
                packed.GetLower().StoreUnsafe(ref payload);
                packed.GetUpper().StoreUnsafe(ref payload, tail);
                Write(ref record, StringSerializer.Latin1Header(length));
                return sizeof(int) + length;
            }

            ref byte first = ref Unsafe.As<ushort, byte>(ref chars);
            nuint rawTail = tail * sizeof(char);
            LuminPackMarshal.CopyBlockUnaligned(ref payload, ref first, 32);
            LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.Add(ref payload, rawTail), ref Unsafe.Add(ref first, rawTail), 32);
            int bytes = length * sizeof(char);
            Write(ref record, bytes);
            return sizeof(int) + bytes;
        }
#endif

        /// <summary>Writes a UTF-16 length record into the remaining storage when it fits.</summary>
        /// <param name="current">The record position.</param>
        /// <param name="room">The bytes available from <paramref name="current"/>.</param>
        /// <param name="value">The original nullable string.</param>
        /// <returns>The written record width, or -1 when the record needs more storage.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int WriteUtf16RecordSlow(ref byte current, int room, string? value)
        {
            if (string.IsNullOrEmpty(value))
                return room >= sizeof(int) ? WriteUtf16Record(ref current, value) : -1;
            int length = value.Length;
            if (room - sizeof(int) >= length * sizeof(char))
                return WriteUtf16Record(ref current, value);
            if (room - sizeof(int) >= length && StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(value.AsSpan()), ref Unsafe.Add(ref current, sizeof(int)), length))
            {
                LuminPackWriter.Write(ref current, StringSerializer.Latin1Header(length));
                return sizeof(int) + length;
            }

            return -1;
        }

        /// <summary>Writes a UTF-16 length record after capacity for its raw form has been established.</summary>
        /// <param name="current">The record position.</param>
        /// <param name="value">The original nullable string.</param>
        /// <param name="outlineMedium">Whether to keep the medium record kernel out of a mixed-width loop.</param>
        /// <returns>The complete written record width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteUtf16Record(ref byte current, string? value, bool outlineMedium = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                LuminPackWriter.Write<int>(ref current, value is null ? LuminPackCode.NullCollection : 0);
                return sizeof(int);
            }

#if NET8_0_OR_GREATER
            if (!outlineMedium && Vector256.IsHardwareAccelerated && Unsafe.SizeOf<IntPtr>() == 8 && (uint)(value.Length - 17) <= 15)
                return WriteMediumUtf16Record(ref current, value, value.Length);
#endif

            int header = StringSerializer.WriteUtf16RecordPayload(ref Unsafe.Add(ref current, sizeof(int)), value);
            LuminPackWriter.Write(ref current, header);
            return sizeof(int) + StringSerializer.Utf16RecordPayload(header);
        }

        /// <summary>Encodes a complete string using an already reserved upper bound without a second capacity check.</summary>
        /// <typeparam name = "TOptions">The compile-time encoding and framing profile.</typeparam>
        /// <param name = "value">The original string, including null.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteReservedRecord<TOptions>(string? value)
            where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            var recording = default(TOptions).StringRecording;
            if (recording == LuminPackStringRecording.VarInt)
            {
                WriteVarIntString<TOptions>(value, true);
                return;
            }
            bool token = recording == LuminPackStringRecording.Token;
            int header = token ? 0 : utf8 ? 8 : 4;
            int bytes = 0;
            int utf16Header = 0;
            ref byte current = ref Current;
            if (!string.IsNullOrEmpty(value))
            {
                if (utf8)
                {
                    Span<byte> destination = _buffer.Slice(Position + header);
                    {
                        var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value.AsSpan(), destination, out _, out bytes, replaceInvalidSequences: false);
                        if (status != OperationStatus.Done)
                            LuminPackExceptionHelper.ThrowFailedEncoding(status);
                    }
                }
                else if (token)
                {
                    bytes = checked(value.Length * sizeof(char));
                    StringSerializer.SerializeUtf16(ref Unsafe.Add(ref current, (nint)(uint)(header)), ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytes);
                }
                else
                {
                    utf16Header = StringSerializer.WriteUtf16RecordPayload(ref Unsafe.Add(ref current, (nint)(uint)(header)), value);
                    bytes = StringSerializer.Utf16RecordPayload(utf16Header);
                }
            }

            if (token)
            {
                Unsafe.Add(ref current, (nint)(uint)(bytes)) = 0;
                if (!utf8)
                    Unsafe.Add(ref current, (nint)(uint)(bytes + 1)) = 0;
                Advance(bytes + (utf8 ? 1 : 2));
            }
            else
            {
                int length = value is null ? LuminPackCode.NullCollection : utf8 ? bytes : utf16Header;
                LuminPackWriter.Write(ref current, length);
                if (utf8)
                    LuminPackWriter.Write(ref Unsafe.Add(ref current, sizeof(int)), value is null ? LuminPackCode.NullCollection : value.Length);
                Advance(bytes + header);
            }
        }

        /// <summary>Writes a reserved Utf8Length record at the published cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReservedUtf8LengthString(string? value)
        {
            int bytes = 0;
            ref byte current = ref Current;
            if (!string.IsNullOrEmpty(value))
            {
                Span<byte> destination = _buffer.Slice(Position + 8);
                var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value.AsSpan(), destination, out _, out bytes, replaceInvalidSequences: false);
                if (status != OperationStatus.Done)
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            LuminPackWriter.Write(ref current, value is null ? LuminPackCode.NullCollection : bytes);
            LuminPackWriter.Write(ref Unsafe.Add(ref current, sizeof(int)), value is null ? LuminPackCode.NullCollection : value.Length);
            Advance(bytes + 8);
        }

        /// <summary>Writes a reserved Utf8Token record at the published cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReservedUtf8TokenString(string? value)
        {
            int bytes = 0;
            ref byte current = ref Current;
            if (!string.IsNullOrEmpty(value))
            {
                Span<byte> destination = _buffer.Slice(Position);
                var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value.AsSpan(), destination, out _, out bytes, replaceInvalidSequences: false);
                if (status != OperationStatus.Done)
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            Unsafe.Add(ref current, (nint)(uint)bytes) = 0;
            Advance(bytes + 1);
        }

        /// <summary>Writes a reserved Utf16Token record at the published cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReservedUtf16TokenString(string? value)
        {
            int bytes = 0;
            ref byte current = ref Current;
            if (!string.IsNullOrEmpty(value))
            {
                bytes = checked(value.Length * sizeof(char));
                StringSerializer.SerializeUtf16(ref current, ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)bytes);
            }
            Unsafe.Add(ref current, (nint)(uint)bytes) = 0;
            Unsafe.Add(ref current, (nint)(uint)(bytes + 1)) = 0;
            Advance(bytes + 2);
        }

        /// <summary>Writes a reserved Utf8VarInt record at the published cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReservedUtf8VarIntString(string? value)
        {
            WriteVarIntString<LuminPackUtf8VarIntOptions>(value, true);
        }

        /// <summary>Writes a reserved Utf16VarInt record at the published cursor.</summary>
        /// <param name="value">The nullable string to encode.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteReservedUtf16VarIntString(string? value)
        {
            WriteVarIntString<LuminPackUtf16VarIntOptions>(value, true);
        }

        /// <summary>Selects the non-default encoding kernel using a stateless profile.</summary>
        /// <typeparam name = "TOptions">The compile-time encoding and framing profile.</typeparam>
        /// <param name = "value">The string payload.</param>
        /// <param name="completeRecord">Whether the returned width includes variable-length framing.</param>
        /// <returns>The encoded payload width, including framing only when the complete-record flag is set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteEncodedString<TOptions>(string? value, out bool completeRecord)
            where TOptions : struct, ILuminPackOptions
        {
            completeRecord = false;
            if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8)
            {
                var recording = default(TOptions).StringRecording;
                if (recording == LuminPackStringRecording.VarInt)
                {
                    completeRecord = true;
                    return WriteVarIntStringBytes<TOptions>(value, false);
                }
                return recording == LuminPackStringRecording.Length ? WriteUtf8WithLength(Position, value) : WriteUtf8WithToken<TOptions>(Position, value);
            }
            return WriteUtf16WithToken<TOptions>(Position, value);
        }

        /// <summary>Writes an opt-in variable-length string record while retaining capacity ownership.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="capacityReserved">Whether the enclosing record has reserved the raw upper bound.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteVarIntString<TOptions>(string? value, bool capacityReserved) where TOptions : struct, ILuminPackOptions
            => AdvanceProven(WriteVarIntStringBytes<TOptions>(value, capacityReserved));

        /// <summary>Writes an exact variable-length record without advancing the owning cursor.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="capacityReserved">Whether the enclosing record has reserved the raw upper bound.</param>
        /// <returns>The complete written width.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteVarIntStringBytes<TOptions>(string? value, bool capacityReserved) where TOptions : struct, ILuminPackOptions
        {
            int length = VarIntRecordLength<TOptions>(value, out int payload);
            if (!capacityReserved && !FitsRecord(length))
                EnsureAdditionalCapacity(length);
            WriteVarIntRecord<TOptions>(_buffer.Slice(Position, length), value, payload);
            return length;
        }

        /// <summary>Returns the binary profile tag without changing the four established tags.</summary>
        /// <typeparam name="TOptions">The stateless string profile.</typeparam>
        /// <returns>The distinct binary profile tag.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte BinaryProfile<TOptions>() where TOptions : struct, ILuminPackOptions
        {
            var encoding = default(TOptions).StringEncoding;
            var recording = default(TOptions).StringRecording;
            return (byte)(recording == LuminPackStringRecording.VarInt ? 4 + (int)encoding : ((int)encoding << 1) | (int)recording);
        }


        /// <summary>Returns the unsigned base-128 width of a header plus one.</summary>
        /// <param name="header">The length header, or minus one for null.</param>
        /// <returns>One through five bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int VarIntHeaderLength(int header)
        {
            uint value = unchecked((uint)header + 1);
            return value < 128 ? 1 : value < 16384 ? 2 : value < 2097152 ? 3 : value < 268435456 ? 4 : 5;
        }


        /// <summary>Writes a header into its caller-proven complete range.</summary>
        /// <param name="destination">Room for the complete header.</param>
        /// <param name="header">The length header, or minus one for null.</param>
        /// <returns>The written byte count.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteVarIntHeader(Span<byte> destination, int header)
        {
            uint value = unchecked((uint)header + 1);
            if (value < 128)
            {
                destination[0] = (byte)value;
                return 1;
            }
            if (BitConverter.IsLittleEndian && value < 16384 && destination.Length >= 2)
            {
                ushort packed = (ushort)((value & 127) | 128 | ((value >> 7) << 8));
                LuminPackWriter.Write(ref MemoryMarshal.GetReference(destination), packed);
                return 2;
            }
            // A short destination retains the original checked per-byte failure and written prefix.
            int offset = 0;
            while (value >= 128)
            {
                destination[offset++] = (byte)(value | 128);
                value >>= 7; }
            destination[offset++] = (byte)value;
            return offset;
        }


        /// <summary>Calculates exact opt-in framing and payload sizes without changing the text representation.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <returns>The complete record byte count.</returns>
        internal static int VarIntRecordLength<TOptions>(string? value) where TOptions : struct, ILuminPackOptions
            => VarIntRecordLength<TOptions>(value, out _);


        /// <summary>Calculates framing once and retains the payload width for the following write.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="value">The nullable string.</param>
        /// <param name="payload">Receives the payload byte count.</param>
        /// <returns>The complete record byte count.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int VarIntRecordLength<TOptions>(string? value, out int payload) where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            payload = 0;
            if (string.IsNullOrEmpty(value))
                return utf8 ? 2 : 1;
            payload = utf8 ? LuminPackMarshal.CalculateStringByteCount(value) : StringSerializer.Utf16RecordPayloadLength(value);
            int header = utf8 ? payload : payload == value.Length ? StringSerializer.Latin1Header(value.Length) : payload;
            return checked(payload + VarIntHeaderLength(header) + (utf8 ? VarIntHeaderLength(value.Length) : 0));
        }


        /// <summary>Writes an exact-sized opt-in record through the established bounded text kernels.</summary>
        /// <typeparam name="TOptions">The stateless encoding profile.</typeparam>
        /// <param name="destination">The complete exact-sized output range.</param>
        /// <param name="value">The nullable string.</param>
        /// <param name="payload">The exact payload width established by the size pass.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteVarIntRecord<TOptions>(Span<byte> destination, string? value, int payload) where TOptions : struct, ILuminPackOptions
        {
            bool utf8 = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8;
            if (string.IsNullOrEmpty(value))
            {
                int header = value is null ? -1 : 0;
                int offset = WriteVarIntHeader(destination, header);
                if (utf8)
                    WriteVarIntHeader(destination.Slice(offset), header);
                return;
            }
            bool latin1 = !utf8 && payload == value.Length;
            int prefix = WriteVarIntHeader(destination, latin1 ? StringSerializer.Latin1Header(value.Length) : payload);
            if (utf8)
                prefix += WriteVarIntHeader(destination.Slice(prefix), value.Length);
            ref byte target = ref MemoryMarshal.GetReference(destination.Slice(prefix));
            if (utf8)
            {
                var status = StringSerializer.Serialize<LuminPackUtf8LengthOptions>(value, destination.Slice(prefix), out _, out int written, replaceInvalidSequences: false);
                if (status != OperationStatus.Done || written != payload)
                    LuminPackExceptionHelper.ThrowFailedEncoding(status);
            }
            else if (latin1)
                StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(value.AsSpan()), ref target, value.Length);
            else
                StringSerializer.SerializeUtf16(ref target, ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan())), (uint)payload);
        }
    }
}
