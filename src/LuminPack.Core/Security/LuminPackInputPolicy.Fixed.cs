using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using LuminPack.Core;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    /// <summary>Opens a scalar object's unchanged frame when its complete decoding costs were proven within every quota.</summary>
    /// <param name="input">The original stable input window.</param>
    /// <param name="schema">The generated closed scalar contract.</param>
    /// <param name="payload">The borrowed, fully checked original payload.</param>
    /// <param name="consumed">The complete original frame width.</param>
    /// <returns>Whether the unchanged decoder may omit ambient accounting; other inputs retain the original path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryOpenFixed(ReadOnlySpan<byte> input, LuminPackSafeSchema schema,
        out ReadOnlySpan<byte> payload, out int consumed)
    {
        if (!TryOpenFixedFrame(input, schema, schema.FixedObjectWidth,
            schema.ProvenObjectMembers, out payload, out consumed)) return false;
        if (payload[0] != 255)
            foreach (int offset in schema.FixedBooleanOffsets!)
                if (payload[offset] > 1) { payload = default; consumed = 0; return false; }
        return true;
    }

    /// <summary>Checks framing and proven resource costs before the generator's direct scalar domain guards.</summary>
    /// <param name="input">The original stable input window.</param>
    /// <param name="schema">The immutable contract and active quotas.</param>
    /// <param name="width">The compiler's exact nonnull payload width.</param>
    /// <param name="members">The compiler's exact object member count.</param>
    /// <param name="payload">The borrowed payload awaiting generated domain checks.</param>
    /// <param name="consumed">The complete frame width.</param>
    /// <returns>Whether framing and all original resource charges are proven valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryOpenFixedFrame(ReadOnlySpan<byte> input, LuminPackSafeSchema schema, int width, int members,
        out ReadOnlySpan<byte> payload, out int consumed)
    {
        payload = default; consumed = 0;
        if (schema.FixedBooleanOffsets == null || !BitConverter.IsLittleEndian || input.Length < HeaderSize ||
            width != schema.FixedObjectWidth || members != schema.ProvenObjectMembers ||
            (!schema.FixedNullAllowed && !schema.FixedPresentAllowed)) return false;
        int length = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(12, 4));
        if ((length != 1 && length != width) || length > input.Length - HeaderSize ||
            length != BinaryPrimitives.ReadInt32LittleEndian(input.Slice(16, 4)) ||
            BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(8, 4)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(input.Slice(20, 4)) != 0 ||
            !input.Slice(0, 8).SequenceEqual(Magic) || !input.Slice(24, 32).SequenceEqual(schema.Fingerprint.AsSpan(0, 32))) return false;
        var value = input.Slice(HeaderSize, length);
        if (value[0] == 255)
        {
            if (length != 1 || !schema.FixedNullAllowed) return false;
        }
        else
        {
            if (length != width || value[0] != members || !schema.FixedPresentAllowed) return false;
        }
        payload = value; consumed = HeaderSize + length;
        return true;
    }
}
