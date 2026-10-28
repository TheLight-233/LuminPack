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
