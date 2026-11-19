using System.ComponentModel;
using LuminPack.Code;

namespace LuminPack.Core;

/// <summary>The framing result of a version-tolerant record.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct LuminPackVersionRecord
{
    internal const byte Marker = 251;
    internal const byte Full = 0x40;
    internal const byte Short = 0x50;
    internal const int MaximumFields = 249;
    internal const int DescriptorWidth = sizeof(ulong);

    /// <summary>Captures the framing decision for an incoming record.</summary>
    /// <param name="map">The map for a descriptor table requiring local slot resolution.</param>
    /// <param name="isNull">Whether the input carried a null marker.</param>
    /// <param name="isLegacy">Whether the input carries a revision-1 record.</param>
    internal LuminPackVersionRecord(LuminPackDescriptorMap? map, bool isNull = false, bool isLegacy = false)
    {
        Map = map;
        IsNull = isNull;
        IsLegacy = isLegacy;
    }

    /// <summary>Gets whether the input carried a null record.</summary>
    public bool IsNull { get; }

    /// <summary>Gets whether the input still holds a revision-1 record at the current position.</summary>
    public bool IsLegacy { get; }

    /// <summary>Gets the receiving slot map, or null for a positional, null, or legacy record.</summary>
    public LuminPackDescriptorMap? Map { get; }

    /// <summary>Computes the schema identity from the member count and the descriptor bytes.</summary>
    /// <param name="descriptors">The complete descriptor table in wire order.</param>
    /// <returns>The FNV-1a identity for the table.</returns>
    public static ulong ComputeSchema(ReadOnlySpan<byte> descriptors)
    {
        int count = descriptors.Length / DescriptorWidth;
        if (count > MaximumFields || descriptors.Length % DescriptorWidth != 0)
            LuminPackExceptionHelper.ThrowFormatException("Invalid version-tolerant descriptor count.");
        ulong hash = unchecked((14695981039346656037UL ^ (byte)count) * 1099511628211UL);
        foreach (byte value in descriptors)
            hash = unchecked((hash ^ value) * 1099511628211UL);
        return hash;
    }
}

/// <summary>Resolves a wire name key to its local slot and expected descriptor.</summary>
/// <param name="nameKey">The wire name key, including any accepted former name.</param>
/// <param name="expectedDescriptor">The descriptor expected for that exact key.</param>
/// <returns>The local slot or minus one for an unknown name key.</returns>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate int LuminPackVersionSlot(uint nameKey, out ulong expectedDescriptor);

/// <summary>Maps the descriptors of an incoming record to the receiving model.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminPackDescriptorMap
{
    private readonly byte[] _slots;
    private readonly byte[]? _widths;
    private readonly int[]? _legacyLengths;
    private readonly VersionPresence _present;

    /// <summary>Captures the validated widths and receiving slots for a descriptor table.</summary>
    /// <param name="slots">The receiving slots in wire order.</param>
    /// <param name="widths">The recorded width codes in wire order.</param>
    /// <param name="present">The supplied receiving slots.</param>
    internal LuminPackDescriptorMap(byte[] slots, byte[] widths, VersionPresence present)
    {
        _slots = slots;
        _widths = widths;
        _present = present;
    }

    /// <summary>Captures the validated lengths and receiving slots for a revision-1 table.</summary>
    /// <param name="slots">The receiving slots in wire order.</param>
    /// <param name="lengths">The exact recorded lengths in wire order.</param>
    /// <param name="present">The supplied receiving slots.</param>
    internal LuminPackDescriptorMap(byte[] slots, int[] lengths, VersionPresence present)
    {
        _slots = slots;
        _legacyLengths = lengths;
        _present = present;
    }

    /// <summary>Gets the number of incoming fields.</summary>
    public int Count => _slots.Length;

    /// <summary>Gets the receiving slot, or minus one for an unknown field.</summary>
    /// <param name="index">The incoming field ordinal.</param>
    /// <returns>The receiving slot.</returns>
    public int GetSlot(int index)
    {
        byte slot = _slots[index];
        return slot == 255 ? -1 : slot;
    }

    /// <summary>Gets the wire width code of an incoming revision-4 or revision-5 field.</summary>
    /// <param name="index">The incoming field ordinal.</param>
    /// <returns>The recorded width code.</returns>
    public byte GetWidth(int index) => _widths![index];

    /// <summary>Gets the exact payload length of an incoming revision-1 field.</summary>
    /// <param name="index">The incoming field ordinal.</param>
    /// <returns>The validated length from the revision-1 table.</returns>
    public int GetLegacyLength(int index) => _legacyLengths![index];

    /// <summary>Checks whether the incoming record supplies the receiving slot.</summary>
    /// <param name="slot">The receiving slot to check.</param>
    /// <returns>Whether the slot is present.</returns>
    public bool IsPresent(int slot) => _present.Contains(slot);
}

internal struct VersionPresence
{
    private ulong _slots0;
    private ulong _slots1;
    private ulong _slots2;
    private ulong _slots3;

    /// <summary>Checks whether the mask contains the requested receiving slot.</summary>
    /// <param name="slot">The local slot number.</param>
    /// <returns>Whether the slot is present.</returns>
    internal readonly bool Contains(int slot)
    {
        ulong word = (slot >> 6) switch
        {
            0 => _slots0,
            1 => _slots1,
            2 => _slots2,
            _ => _slots3,
        };
        return (word & (1UL << slot)) != 0;
    }

    /// <summary>Marks a receiving slot and detects duplicate input identities.</summary>
    /// <param name="slot">The local slot number.</param>
    /// <returns>Whether this is the first mark for the slot.</returns>
    internal bool Add(int slot)
    {
        ulong mask = 1UL << slot;
        switch (slot >> 6)
        {
            case 0:
                return Add(ref _slots0, mask);
            case 1:
                return Add(ref _slots1, mask);
            case 2:
                return Add(ref _slots2, mask);
            default:
                return Add(ref _slots3, mask);
        }
    }

    /// <summary>Sets a slot bit in the selected word.</summary>
    /// <param name="word">The corresponding group of 64 slot bits.</param>
    /// <param name="mask">The bit assigned to the slot.</param>
    /// <returns>Whether the bit was clear before the update.</returns>
    private static bool Add(ref ulong word, ulong mask)
    {
        bool fresh = (word & mask) == 0;
        word |= mask;
        return fresh;
    }
}
