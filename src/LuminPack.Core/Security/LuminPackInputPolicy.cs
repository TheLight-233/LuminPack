using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPack.Security;

/// <summary>Generated-operation support for the explicitly selected bounded input option.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class LuminPackInputPolicy
{
    /// <summary>The fixed binary safe-envelope width.</summary>
    public const int HeaderSize = 56;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LPSAFE02");
    private static readonly ConditionalWeakTable<object, ActiveScope> Active = new ConditionalWeakTable<object, ActiveScope>();

    /// <summary>Tracks an active operation without changing any trusted state layout or retaining a consumer assembly.</summary>
    private sealed class ActiveScope
    {
        internal bool Entered;
    }

    /// <summary>Begins one independent root while nested operations retain the same validated input.</summary>
    /// <param name="state">The existing exclusive operation state.</param>
    /// <returns>Whether this operation owns the validation boundary.</returns>
    public static bool TryBegin(object state)
    {
        var scope = Active.GetValue(state, _ => new ActiveScope());
        if (scope.Entered) return false;
        scope.Entered = true;
        return true;
    }

    /// <summary>Ends the root in its mandatory exception-safe cleanup.</summary>
    /// <param name="state">The same exclusive state that began the root.</param>
    public static void End(object state)
    {
        if (Active.TryGetValue(state, out var scope)) scope.Entered = false;
    }

    /// <summary>Rejects oversized document input before token scanning or transcoding.</summary>
    /// <param name="bytes">The encoded byte count.</param>
    /// <param name="maximum">The declaration's fixed byte limit.</param>
    public static void CheckInput(long bytes, int maximum)
    {
        if (bytes < 0 || bytes > maximum) SafeReadBudget.Fail("encoded input");
    }

    /// <summary>Rejects an ambient profile that disagrees with the selected external schema.</summary>
    /// <typeparam name="TActual">The actual operation profile.</typeparam>
    /// <typeparam name="TExpected">The schema's declared profile.</typeparam>
    public static void CheckInputProfile<TActual, TExpected>()
        where TActual : struct, ILuminPackOptions where TExpected : struct, ILuminPackOptions
    {
        if (default(TActual).StringEncoding != default(TExpected).StringEncoding ||
            default(TActual).StringRecording != default(TExpected).StringRecording ||
            default(TActual).MaxJsonDepth != default(TExpected).MaxJsonDepth ||
            default(TActual).ValidateJsonSyntax != default(TExpected).ValidateJsonSyntax ||
            !default(TActual).StandardFormat.Equals(default(TExpected).StandardFormat))
            LuminPackExceptionHelper.ThrowInvalidOperationException("The external-input operation profile must match its compiled schema.");
    }

    /// <summary>Bounds JSON characters before scanning or renting transcoding storage.</summary>
    /// <param name="input">The caller's character document.</param>
    /// <param name="encoding">The compiled document encoding.</param>
    /// <param name="maximum">The compiled encoded byte limit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckCharacters(ReadOnlySpan<char> input, int encoding, int maximum)
    {
        if (maximum == int.MaxValue) return;
        CheckInput((long)input.Length * (encoding == 1 ? 2 : 1), maximum);
        if (encoding == 0) CheckInput(Encoding.UTF8.GetByteCount(input), maximum);
    }

    /// <summary>Reads at most one byte beyond a selected input cap before rejecting an oversized stream.</summary>
    /// <param name="input">The caller-owned source.</param>
    /// <param name="output">The facade-owned staging stream.</param>
    /// <param name="maximum">The compiled byte limit.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The bounded copy completion.</returns>
    public static async System.Threading.Tasks.Task CopyInputAsync(System.IO.Stream input, System.IO.Stream output, int maximum, System.Threading.CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[Math.Min(8192, maximum == int.MaxValue ? 8192 : maximum + 1)];
        int total = 0;
        while (true)
        {
            int request = (int)Math.Min(buffer.Length, (long)maximum - total + 1);
            int read = await input.ReadAsync(buffer, 0, request, cancellationToken).ConfigureAwait(false);
            if (read == 0) return;
            CheckInput((long)total + read, maximum);
            await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            total += read;
        }
    }

    /// <summary>Writes the schema and profile identifier around an unchanged validated payload.</summary>
    /// <param name="payload">The existing formatter's complete output.</param>
    /// <param name="schema">The generated closed contract.</param>
    /// <param name="json">Whether the payload is encoded JSON.</param>
    /// <returns>The independently identified external-data frame.</returns>
    public static byte[] Pack(ReadOnlySpan<byte> payload, LuminPackSafeSchema schema, bool json)
    {
        schema.EnsureSupported();
        var budget = new SafeReadBudget(schema.Limits);
        CheckInput(payload.Length, schema.Limits.MaxDecompressedBytes);
        if (json)
        {
            ValidateJson(payload, schema, budget);
            byte[] prefix = JsonPrefix(schema);
            int suffix = schema.JsonEncoding == 0 ? 1 : 2;
            int length = checked(prefix.Length + payload.Length + suffix);
            CheckInput(length, schema.Limits.MaxInputBytes);
            budget.Allocate(length + 24L);
            var output = new byte[length];
            prefix.CopyTo(output, 0);
            payload.CopyTo(output.AsSpan(prefix.Length));
            output[length - suffix] = (byte)'}';
            return output;
        }
        SafeBinaryValidator.Validate(payload, schema, budget);
        int total = checked(HeaderSize + payload.Length);
        CheckInput(total, schema.Limits.MaxInputBytes);
        budget.Allocate(total + 24L);
        var frame = new byte[total];
        Magic.CopyTo(frame, 0);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(12), payload.Length);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(16), payload.Length);
        schema.Fingerprint.CopyTo(frame, 24);
        payload.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    /// <summary>Owns, checks compatibility, budgets and validates a complete leading input record before target mutation.</summary>
    /// <param name="input">The caller's current input window.</param>
    /// <param name="schema">The expected generated contract.</param>
    /// <param name="json">Whether the document is JSON.</param>
    /// <param name="consumed">Receives the complete encoded record width.</param>
    /// <returns>An independently owned, fully validated payload for the unchanged formatter.</returns>
    /// <remarks>The schema digest checks compatibility and does not authenticate a sender.</remarks>
    public static byte[] Open(ReadOnlySpan<byte> input, LuminPackSafeSchema schema, bool json, out int consumed)
    {
        schema.EnsureSupported();
        consumed = 0;
        var budget = new SafeReadBudget(schema.Limits);
        if (json)
        {
            CheckInput(input.Length, schema.Limits.MaxInputBytes);
            budget.Allocate(input.Length * 8L + 8192);
            budget.Work(input.Length);
            byte[] owned = input.ToArray();
            byte[] prefix = JsonPrefix(schema);
            int suffix = schema.JsonEncoding == 0 ? 1 : 2;
            if (owned.Length < prefix.Length + suffix || !owned.AsSpan(0, prefix.Length).SequenceEqual(prefix))
                Invalid("The JSON schema/profile envelope is invalid or incompatible.");
            var remaining = owned.AsSpan(prefix.Length);
            CheckInput(remaining.Length - suffix, schema.Limits.MaxDecompressedBytes);
            int width = ValidateJson(remaining, schema, budget, true);
            CheckInput(width, schema.Limits.MaxDecompressedBytes);
            if (width > remaining.Length - suffix || remaining[width] != '}' || suffix == 2 && remaining[width + 1] != 0)
                Invalid("The JSON schema/profile envelope is incomplete.");
            var payload = remaining.Slice(0, width);
            consumed = prefix.Length + width + suffix;
            return payload.ToArray();
        }
        if (input.Length < HeaderSize) Invalid("The external-data header is truncated.");
        int encoded = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(12, 4));
        if (encoded < 0 || encoded > input.Length - HeaderSize) Invalid("The external-data frame length is invalid.");
        int count = HeaderSize + encoded;
        CheckInput(count, schema.Limits.MaxInputBytes);
        budget.Allocate(8L * count + 8192);
        budget.Work(count);
        byte[] snapshot = input.Slice(0, count).ToArray();
        if (!snapshot.AsSpan(0, Magic.Length).SequenceEqual(Magic) ||
            BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(12, 4)) != encoded ||
            BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(20, 4)) != 0 ||
            !snapshot.AsSpan(24, 32).SequenceEqual(schema.Fingerprint))
            Invalid("The external-data schema/profile identifier is invalid or incompatible.");
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(8, 4));
        int decoded = BinaryPrimitives.ReadInt32LittleEndian(snapshot.AsSpan(16, 4));
        if (flags > 1 || decoded < 0) Invalid("The external-data envelope flags or decoded length are invalid.");
        CheckInput(decoded, schema.Limits.MaxDecompressedBytes);
        byte[] bytes;
        if (flags == 1)
        {
            var compressed = snapshot.AsSpan(HeaderSize);
            if (LuminCompressor.GetDecompressedSize(compressed) != decoded) Invalid("The compressed payload length disagrees with its envelope.");
            budget.Allocate(decoded + 24L);
            budget.Work(decoded);
            bytes = LuminCompressor.Decompress(compressed);
        }
        else
        {
            if (decoded != encoded) Invalid("An uncompressed envelope must have equal encoded and decoded lengths.");
            bytes = snapshot.AsSpan(HeaderSize).ToArray();
        }
        SafeBinaryValidator.Validate(bytes, schema, budget);
        consumed = count;
        return bytes;
    }

    /// <summary>Primes the original strict JSON reader at the framed value boundary.</summary>
    /// <param name="reader">The new payload reader.</param>
    /// <param name="schema">The fixed document encoding.</param>
    public static void PrimeJson(ref LuminPackJsonReader reader, LuminPackSafeSchema schema)
    {
        bool found = schema.JsonEncoding == 0 ? reader.Read<SafeUtf8Options>() : reader.Read<SafeUtf16Options>();
        if (!found) Invalid("The framed JSON document contains no value.");
    }

    /// <summary>Rejects trailing document data before returning a successfully framed value.</summary>
    /// <param name="remaining">The encoded bytes after the framed root.</param>
    /// <param name="schema">The compiled encoding.</param>
    public static void EnsureJsonFrameEnd(ReadOnlySpan<byte> remaining, LuminPackSafeSchema schema)
    {
        int width = schema.JsonEncoding == 0 ? 1 : 2;
        if (remaining.Length % width != 0) Invalid("The JSON document has a partial trailing code unit.");
        for (int i = 0; i < remaining.Length; i += width)
            if (remaining[i] is not (9 or 10 or 13 or 32) || width == 2 && remaining[i + 1] != 0)
                Invalid("Unexpected data after the framed JSON document.");
    }

    /// <summary>Publishes one validated framed JSON value with the original writer's separator and ownership state.</summary>
    /// <param name="writer">The caller's existing writer.</param>
    /// <param name="frame">The complete validated document value.</param>
    /// <param name="schema">The fixed document encoding.</param>
    public static void WriteJsonFrame(ref LuminPackJsonWriter writer, ReadOnlySpan<byte> frame, LuminPackSafeSchema schema)
    {
        if (!writer.IsFirstElement)
        {
            if (schema.JsonEncoding == 0) writer.WriteByteRaw<LuminPackUtf8LengthOptions>((byte)',');
            else writer.WriteByteRaw<LuminPackUtf16LengthOptions>((byte)',');
        }
        writer.WriteRaw(frame);
        writer.IsFirstElement = false;
    }

    /// <summary>Builds the canonical JSON protocol prefix in the selected document encoding.</summary>
    /// <param name="schema">The expected schema and document profile.</param>
    /// <returns>The exact canonical prefix.</returns>
    private static byte[] JsonPrefix(LuminPackSafeSchema schema)
    {
        string prefix = "{\"$luminpack\":2,\"$schema\":\"" + Convert.ToBase64String(schema.Fingerprint) + "\",\"$value\":";
        return (schema.JsonEncoding == 0 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(prefix);
    }

    /// <summary>Selects one of the existing strict token readers without a second JSON tokenizer.</summary>
    /// <param name="payload">The complete independent JSON payload.</param>
    /// <param name="schema">The reviewed type graph.</param>
    /// <param name="budget">The shared root budget.</param>
    /// <param name="prefix">Whether the enclosing protocol owns trailing bytes.</param>
    /// <returns>The validated value width.</returns>
    private static int ValidateJson(ReadOnlySpan<byte> payload, LuminPackSafeSchema schema, SafeReadBudget budget, bool prefix = false)
    {
        if (schema.JsonEncoding == 0) return SafeJsonValidator.Validate<SafeUtf8Options>(payload, schema, budget, prefix);
        return SafeJsonValidator.Validate<SafeUtf16Options>(payload, schema, budget, prefix);
    }

    /// <summary>Raises a catchable malformed-data error before the original decoder runs.</summary>
    /// <param name="message">The rejected invariant.</param>
    internal static void Invalid(string message) => LuminPackExceptionHelper.ThrowInvalidDataException(message);

    /// <summary>Fixed strict UTF-8 validation constants used only in the selected safe boundary.</summary>
    internal readonly struct SafeUtf8Options : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => new System.Buffers.StandardFormat('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 64;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => true;
    }

    /// <summary>Fixed strict UTF-16 validation constants used only in the selected safe boundary.</summary>
    internal readonly struct SafeUtf16Options : ILuminPackOptions
    {
        /// <inheritdoc/>
        public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
        /// <inheritdoc/>
        public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
        /// <inheritdoc/>
        public System.Buffers.StandardFormat StandardFormat => new System.Buffers.StandardFormat('G');
        /// <inheritdoc/>
        public int MaxJsonDepth => 64;
        /// <inheritdoc/>
        public bool ValidateJsonSyntax => true;
    }
}
