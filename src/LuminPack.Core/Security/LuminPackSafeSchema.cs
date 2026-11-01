using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using LuminPack.Code;

namespace LuminPack.Security;

/// <summary>Compiler-proven wire shapes accepted by the independent safe validator.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum LuminPackSafeKind
{
    /// <summary>An unreviewed shape that is always rejected.</summary>
    Unsupported,
    /// <summary>A canonical Boolean.</summary>
    Boolean,
    /// <summary>An unsigned byte.</summary>
    Byte,
    /// <summary>A signed byte.</summary>
    SByte,
    /// <summary>A signed short.</summary>
    Int16,
    /// <summary>An unsigned short.</summary>
    UInt16,
    /// <summary>A signed integer.</summary>
    Int32,
    /// <summary>An unsigned integer.</summary>
    UInt32,
    /// <summary>A signed long.</summary>
    Int64,
    /// <summary>An unsigned long.</summary>
    UInt64,
    /// <summary>A single-precision value.</summary>
    Single,
    /// <summary>A double-precision value.</summary>
    Double,
    /// <summary>A UTF-16 code unit.</summary>
    Char,
    /// <summary>A string under its declared wire profile.</summary>
    String,
    /// <summary>A declared enumeration value.</summary>
    Enum,
    /// <summary>A headerless Packed structure.</summary>
    Struct,
    /// <summary>An ordinary object with ordered members.</summary>
    Object,
    /// <summary>A counted sequence.</summary>
    Sequence,
    /// <summary>A counted unique set.</summary>
    Set,
    /// <summary>A counted sequence of unique key/value pairs.</summary>
    Dictionary,
    /// <summary>A nullable value.</summary>
    Nullable,
    /// <summary>A queue with its canonical metadata.</summary>
    Queue,
    /// <summary>A rectangular array.</summary>
    MultiArray,
    /// <summary>A fixed GUID.</summary>
    Guid,
    /// <summary>A decimal with canonical flags.</summary>
    Decimal,
    /// <summary>A valid date/time tick representation.</summary>
    DateTime,
    /// <summary>A signed duration.</summary>
    TimeSpan
}

/// <summary>An immutable compiler-produced description of one existing wire operation.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminPackSafeNode
{
    internal readonly LuminPackSafeKind Kind;
    internal readonly int Storage;
    internal readonly int Mode;
    internal readonly int Rank;
    internal readonly int[] Children;
    internal readonly int[] JsonChildren;
    internal readonly string[] Names;
    internal readonly byte[][] JsonNames;
    internal readonly ulong[] Values;
    internal readonly bool Flags;
    internal readonly bool Sorted;
    internal readonly bool Signed;
    // The original constructor retains interpretation when no generated fixed-layout facts are available.
    internal readonly int FixedWidth = -2, FixedDepth = 1;
    internal readonly bool FixedDomain;

    /// <summary>Captures an immutable schema node emitted from the original formatter metadata.</summary>
    /// <param name="kind">The reviewed wire shape.</param>
    /// <param name="storage">Actual or conservative CLR storage width.</param>
    /// <param name="mode">The existing string mode.</param>
    /// <param name="rank">Array rank, or primitive width for an enum.</param>
    /// <param name="children">Ordered binary child node indices.</param>
    /// <param name="jsonChildren">Ordered JSON member node indices.</param>
    /// <param name="names">Exact JSON property names.</param>
    /// <param name="values">Declared enum bit patterns.</param>
    /// <param name="flags">Whether enum combinations are allowed.</param>
    /// <param name="sorted">Whether keys use ordered comparison.</param>
    /// <param name="signed">Whether an enum has a signed underlying value.</param>
    public LuminPackSafeNode(LuminPackSafeKind kind, int storage, int mode = 3, int rank = 0, int[]? children = null,
        int[]? jsonChildren = null, string[]? names = null, ulong[]? values = null, bool flags = false, bool sorted = false, bool signed = false)
    {
        Kind = kind;
        Storage = storage;
        Mode = mode;
        Rank = rank;
        Children = children is null ? Array.Empty<int>() : (int[])children.Clone();
        JsonChildren = jsonChildren is null ? Children : (int[])jsonChildren.Clone();
        Names = names is null ? Array.Empty<string>() : (string[])names.Clone();
        JsonNames = new byte[Names.Length][];
        for (int i = 0; i < Names.Length; i++) JsonNames[i] = Encoding.UTF8.GetBytes(Names[i]);
        Values = values is null ? Array.Empty<ulong>() : (ulong[])values.Clone();
        Flags = flags;
        Sorted = sorted;
        Signed = signed;
    }

    /// <summary>Captures compiler-proven fixed wire facts alongside the original immutable node contract.</summary>
    /// <param name="kind">The reviewed wire shape.</param>
    /// <param name="storage">Actual or conservative CLR storage width.</param>
    /// <param name="mode">The existing string mode.</param>
    /// <param name="rank">Array rank, or primitive width for an enum.</param>
    /// <param name="children">Ordered binary child node indices.</param>
    /// <param name="jsonChildren">Ordered JSON member node indices.</param>
    /// <param name="names">Exact JSON property names.</param>
    /// <param name="values">Declared enum bit patterns.</param>
    /// <param name="flags">Whether enum combinations are allowed.</param>
    /// <param name="sorted">Whether keys use ordered comparison.</param>
    /// <param name="signed">Whether an enum has a signed underlying value.</param>
    /// <param name="fixedWidth">The exact fixed wire width, or minus one for a variable shape.</param>
    /// <param name="fixedDepth">The original fixed-width analysis depth.</param>
    /// <param name="fixedDomain">Whether the fixed storage contains semantically restricted values.</param>
    public LuminPackSafeNode(LuminPackSafeKind kind, int storage, int fixedWidth, int fixedDepth, bool fixedDomain,
        int mode = 3, int rank = 0, int[]? children = null, int[]? jsonChildren = null, string[]? names = null,
        ulong[]? values = null, bool flags = false, bool sorted = false, bool signed = false)
        : this(kind, storage, mode, rank, children, jsonChildren, names, values, flags, sorted, signed)
    { FixedWidth = fixedWidth; FixedDepth = fixedDepth; FixedDomain = fixedDomain; }
}

/// <summary>A generated, immutable compatibility and validation contract for the separate safe envelope.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LuminPackSafeSchema
{
    internal readonly LuminPackSafeNode[] Nodes;
    internal readonly byte[] Fingerprint;
    internal readonly int JsonEncoding;
    internal readonly int JsonDepth;
    internal readonly string? Rejection;
    internal readonly LuminPackReadLimits Limits;
    internal readonly byte[] BinaryPrefix;
    internal readonly byte[] JsonPrefix;
    private readonly bool _jsonSupported;
    internal readonly int StaticDepth;
    internal readonly int FixedObjectWidth;
    internal readonly int ProvenObjectMembers = -1;
    internal readonly int[]? FixedBooleanOffsets;
    internal readonly bool FixedPresentAllowed, FixedNullAllowed;
    internal readonly byte[]? FixedPresentPrefix, FixedNullPrefix;
    internal readonly int ProvenPayloadLimit;
    /// <summary>The compiler-selected document encoding, zero for UTF-8 and one for UTF-16.</summary>
    public int DocumentEncoding => JsonEncoding;

    /// <summary>Freezes the reviewed graph and hashes its canonical type/member/profile identity.</summary>
    /// <param name="identity">The compiler-produced canonical schema and options identity.</param>
    /// <param name="nodes">The root-first closed validation graph.</param>
    /// <param name="jsonEncoding">Zero for UTF-8, one for UTF-16.</param>
    /// <param name="rejection">A fail-closed explanation for an unsupported contract.</param>
    /// <param name="maxInputBytes">Maximum encoded bytes.</param>
    /// <param name="maxDecompressedBytes">Maximum decoded bytes.</param>
    /// <param name="maxAllocationBytes">Estimated aggregate library allocations.</param>
    /// <param name="maxCollectionLength">Maximum container count.</param>
    /// <param name="maxStringLength">Maximum UTF-16 string length.</param>
    /// <param name="maxObjects">Maximum reference-value count.</param>
    /// <param name="maxDepth">Maximum value depth.</param>
    /// <param name="maxWork">Maximum validation work.</param>
    /// <param name="maxJsonDepth">The existing document profile's container bound.</param>
    public LuminPackSafeSchema(string identity, LuminPackSafeNode[] nodes, int jsonEncoding, string? rejection = null,
        int maxInputBytes = 8 * 1024 * 1024, int maxDecompressedBytes = 16 * 1024 * 1024,
        long maxAllocationBytes = 64L * 1024 * 1024, int maxCollectionLength = 100_000,
        int maxStringLength = 1024 * 1024, int maxObjects = 100_000, int maxDepth = 64, long maxWork = 32L * 1024 * 1024, int maxJsonDepth = 64)
    {
        Nodes = (LuminPackSafeNode[])nodes.Clone();
        _jsonSupported = true;
        foreach (var node in Nodes)
            if (node.Kind is LuminPackSafeKind.Guid or LuminPackSafeKind.DateTime or LuminPackSafeKind.TimeSpan or LuminPackSafeKind.MultiArray || node.Names.Length > 256)
                _jsonSupported = false;
        JsonEncoding = jsonEncoding;
        JsonDepth = maxJsonDepth;
        Rejection = rejection;
        Limits = new LuminPackReadLimits(maxInputBytes, maxDecompressedBytes, maxAllocationBytes, maxCollectionLength,
            maxStringLength, maxObjects, maxDepth, maxWork);
        using var hash = SHA256.Create();
        Fingerprint = hash.ComputeHash(Encoding.UTF8.GetBytes(identity));
        BinaryPrefix = new byte[LuminPackInputPolicy.HeaderSize];
        Encoding.ASCII.GetBytes("LPSAFE02").CopyTo(BinaryPrefix, 0);
        Fingerprint.CopyTo(BinaryPrefix, 24);
        JsonPrefix = (jsonEncoding == 0 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(
            "{\"$luminpack\":2,\"$schema\":\"" + Convert.ToBase64String(Fingerprint) + "\",\"$value\":");
    }

    /// <summary>Freezes a graph with a compiler-proven finite depth bound and the unchanged protocol identity.</summary>
    /// <param name="identity">The compiler-produced canonical schema and options identity.</param>
    /// <param name="nodes">The root-first closed validation graph.</param>
    /// <param name="jsonEncoding">Zero for UTF-8, one for UTF-16.</param>
    /// <param name="rejection">A fail-closed explanation for an unsupported contract.</param>
    /// <param name="maxInputBytes">Maximum encoded bytes.</param>
    /// <param name="maxDecompressedBytes">Maximum decoded bytes.</param>
    /// <param name="maxAllocationBytes">Estimated aggregate library allocations.</param>
    /// <param name="maxCollectionLength">Maximum container count.</param>
    /// <param name="maxStringLength">Maximum UTF-16 string length.</param>
    /// <param name="maxObjects">Maximum reference-value count.</param>
    /// <param name="maxDepth">Maximum value depth.</param>
    /// <param name="maxWork">Maximum validation work.</param>
    /// <param name="maxJsonDepth">The existing document profile's container bound.</param>
    /// <param name="maxSchemaDepth">The finite declared value depth, or zero when recursive or unreviewed.</param>
    public LuminPackSafeSchema(string identity, LuminPackSafeNode[] nodes, int jsonEncoding, string? rejection,
        int maxInputBytes, int maxDecompressedBytes, long maxAllocationBytes, int maxCollectionLength,
        int maxStringLength, int maxObjects, int maxDepth, long maxWork, int maxJsonDepth, int maxSchemaDepth)
        : this(identity, nodes, jsonEncoding, rejection, maxInputBytes, maxDecompressedBytes, maxAllocationBytes,
            maxCollectionLength, maxStringLength, maxObjects, maxDepth, maxWork, maxJsonDepth)
    {
        StaticDepth = maxSchemaDepth;
        if (rejection != null || Nodes.Length == 0 || Nodes[0].Kind != LuminPackSafeKind.Object || Nodes[0].Rank != 0 ||
            Nodes[0].Children.Length > 249) return;
        FixedNullAllowed = FixedCostsFit(1, 0, 0, false);
        int width = 1;
        int strings = 0;
        var booleans = new System.Collections.Generic.List<int>();
        foreach (int child in Nodes[0].Children)
        {
            var node = Nodes[child];
            if (node.Kind == LuminPackSafeKind.String) { strings++; continue; }
            if (node.FixedWidth < 0 || !(node.Kind is >= LuminPackSafeKind.Boolean and <= LuminPackSafeKind.Char ||
                node.Kind is LuminPackSafeKind.Guid or LuminPackSafeKind.TimeSpan)) return;
            if (node.Kind == LuminPackSafeKind.Boolean) booleans.Add(width);
            width = checked(width + node.FixedWidth);
        }
        ProvenObjectMembers = Nodes[0].Children.Length;
        if (strings != 0)
        {
            ProvenPayloadLimit = StringCostsLimit(strings);
            return;
        }
        FixedObjectWidth = width;
        FixedBooleanOffsets = booleans.ToArray();
        FixedPresentAllowed = FixedCostsFit(width, Nodes[0].Children.Length, Nodes[0].Storage, true);
        if (FixedPresentAllowed) FixedPresentPrefix = CreateFixedPrefix(width, Nodes[0].Children.Length);
        if (FixedNullAllowed) FixedNullPrefix = CreateFixedPrefix(1, 255);
    }

    /// <summary>Freezes every original frame byte and marker for an exact-width quota-proven record.</summary>
    /// <param name="width">The complete payload width, including its marker.</param>
    /// <param name="marker">The original member count or null marker.</param>
    /// <returns>The unchanged complete header followed by its exact object marker.</returns>
    private byte[] CreateFixedPrefix(int width, int marker)
    {
        var prefix = new byte[LuminPackInputPolicy.HeaderSize + 1];
        BinaryPrefix.CopyTo(prefix, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(12, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(prefix.AsSpan(16, 4), width);
        prefix[LuminPackInputPolicy.HeaderSize] = (byte)marker;
        return prefix;
    }

    /// <summary>Bounds every original charge of a flat scalar/string object by its validated payload length.</summary>
    /// <param name="strings">The number of declared string slots, including possibly null or empty values.</param>
    /// <returns>The largest payload for which all original quotas are proven, or zero for the guarded path.</returns>
    private int StringCostsLimit(int strings)
    {
        int fields = Nodes[0].Children.Length;
        if (Limits.MaxObjects < strings + 1 || Limits.MaxDepth < 2) return 0;
        long allocationBase = 4096L + 4L * LuminPackInputPolicy.HeaderSize + Nodes[0].Storage + 64L + 128L * strings;
        long workBase = LuminPackInputPolicy.HeaderSize + 1L + fields + (fields == 0 ? 0 : 1);
        if (Limits.MaxAllocationBytes < allocationBase || Limits.MaxWork < workBase) return 0;
        long limit = Math.Min((long)Limits.MaxInputBytes - LuminPackInputPolicy.HeaderSize, Limits.MaxDecompressedBytes);
        limit = Math.Min(limit, Limits.MaxStringLength);
        // Input framing charges four bytes of allocation per byte; decoded text adds at most another four.
        limit = Math.Min(limit, (Limits.MaxAllocationBytes - allocationBase) / 8);
        limit = Math.Min(limit, (Limits.MaxWork - workBase) / 2);
        return (int)Math.Max(0, limit);
    }

    /// <summary>Proves the original streaming charges for a fixed scalar object before it can use a stateless read.</summary>
    /// <param name="width">The exact payload width including its marker.</param>
    /// <param name="fields">The number of immediate scalar fields.</param>
    /// <param name="storage">The original conservative object storage estimate.</param>
    /// <param name="present">Whether an object is present instead of a null marker.</param>
    /// <returns>Whether every original resource charge fits the active immutable policy.</returns>
    private bool FixedCostsFit(int width, int fields, int storage, bool present)
    {
        long frame = LuminPackInputPolicy.HeaderSize + (long)width;
        long allocations = 4 * frame + 4096 + (present ? storage + 64L : 0);
        long work = frame + 1 + fields + (fields == 0 ? 0 : 1);
        return frame <= Limits.MaxInputBytes && width <= Limits.MaxDecompressedBytes &&
            allocations <= Limits.MaxAllocationBytes && work <= Limits.MaxWork &&
            (!present || Limits.MaxObjects >= 1) && Limits.MaxDepth >= (fields == 0 ? 1 : 2);
    }

    /// <summary>Rejects unsupported schemas and machine byte orders before interpreting payload data.</summary>
    public void EnsureSupported()
    {
        if (!BitConverter.IsLittleEndian)
            LuminPackExceptionHelper.ThrowPlatformNotSupportedException("The safe protocol currently requires a verified little-endian target.");
        if (Rejection is not null || Nodes.Length == 0)
            LuminPackExceptionHelper.ThrowNotSupportedException(Rejection ?? "No generated safe schema exists for this type.");
    }

    /// <summary>Rejects specialized JSON shapes before entering any original formatter.</summary>
    internal void EnsureJsonSupported()
    {
        EnsureSupported();
        if (!_jsonSupported)
            LuminPackExceptionHelper.ThrowNotSupportedException("This external JSON graph contains an unreviewed specialized shape or more than 256 members in one object.");
    }
}
