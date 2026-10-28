using LuminPack.Code;

namespace LuminPack.Security;

/// <summary>Immutable resource limits for the option-selected external input boundary.</summary>
/// <remarks>Limits cover library-controlled input, validation and estimated decoding allocations. Application code remains trusted.</remarks>
internal sealed class LuminPackReadLimits
{
    /// <summary>The default bounded input policy.</summary>
    public static LuminPackReadLimits Default { get; } = new LuminPackReadLimits();
    /// <summary>The maximum complete encoded frame size.</summary>
    public int MaxInputBytes { get; }
    /// <summary>The maximum decoded payload size.</summary>
    public int MaxDecompressedBytes { get; }
    /// <summary>The conservative aggregate library allocation budget.</summary>
    public long MaxAllocationBytes { get; }
    /// <summary>The maximum number of elements in one container.</summary>
    public int MaxCollectionLength { get; }
    /// <summary>The maximum decoded string length in UTF-16 code units.</summary>
    public int MaxStringLength { get; }
    /// <summary>The maximum number of decoded reference values.</summary>
    public int MaxObjects { get; }
    /// <summary>The maximum schema/value nesting depth, at most 64.</summary>
    public int MaxDepth { get; }
    /// <summary>The cumulative validation byte, element and comparison work budget.</summary>
    public long MaxWork { get; }

    /// <summary>Creates a validated, immutable input policy.</summary>
    /// <param name="maxInputBytes">Maximum complete encoded bytes.</param>
    /// <param name="maxDecompressedBytes">Maximum decoded payload bytes.</param>
    /// <param name="maxAllocationBytes">Conservative aggregate allocation budget.</param>
    /// <param name="maxCollectionLength">Maximum elements in any collection.</param>
    /// <param name="maxStringLength">Maximum UTF-16 code units in a string.</param>
    /// <param name="maxObjects">Maximum decoded reference values.</param>
    /// <param name="maxDepth">Maximum nested values, between one and 64.</param>
    /// <param name="maxWork">Cumulative byte, element and comparison work.</param>
    public LuminPackReadLimits(int maxInputBytes = 8 * 1024 * 1024, int maxDecompressedBytes = 16 * 1024 * 1024,
        long maxAllocationBytes = 64L * 1024 * 1024, int maxCollectionLength = 100_000,
        int maxStringLength = 1024 * 1024, int maxObjects = 100_000, int maxDepth = 64, long maxWork = 32L * 1024 * 1024)
    {
        if (maxInputBytes < 0 || maxDecompressedBytes < 0 || maxAllocationBytes < 0 || maxCollectionLength < 0 ||
            maxStringLength < 0 || maxObjects < 0 || maxDepth < 1 || maxDepth > 64 || maxWork < 0)
            LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(maxInputBytes), "Read limits must be nonnegative and depth must be between one and 64.");
        MaxInputBytes = maxInputBytes;
        MaxDecompressedBytes = maxDecompressedBytes;
        MaxAllocationBytes = maxAllocationBytes;
        MaxCollectionLength = maxCollectionLength;
        MaxStringLength = maxStringLength;
        MaxObjects = maxObjects;
        MaxDepth = maxDepth;
        MaxWork = maxWork;
    }
}

/// <summary>Owns one root's cumulative validation and decoding budget.</summary>
internal sealed class SafeReadBudget
{
    internal LuminPackReadLimits Limits;
    private long _allocations;
    private long _work;
    private int _objects;

    /// <summary>Creates a fresh independent root budget.</summary>
    /// <param name="limits">The immutable policy.</param>
    internal SafeReadBudget(LuminPackReadLimits limits)
    { Limits = limits; Reset(limits); }

    /// <summary>Reuses the exclusive synchronous root budget after its previous operation has ended.</summary>
    /// <param name="limits">The next root's immutable limits.</param>
    internal void Reset(LuminPackReadLimits limits)
    {
        Limits = limits;
        _allocations = limits.MaxAllocationBytes;
        _work = limits.MaxWork;
        _objects = limits.MaxObjects;
    }

    /// <summary>Charges before a validation or library decoding allocation.</summary>
    /// <param name="bytes">The conservative allocation estimate.</param>
    internal void Allocate(long bytes)
    {
        if (bytes < 0 || bytes > _allocations) Fail("aggregate allocation");
        _allocations -= bytes;
    }

    /// <summary>Charges bounded traversal, scanning or key-comparison work.</summary>
    /// <param name="units">The nonnegative work estimate.</param>
    internal void Work(long units)
    {
        if (units < 0 || units > _work) Fail("cumulative work");
        _work -= units;
    }

    /// <summary>Checks a container before any count-based allocation.</summary>
    /// <param name="count">The decoded count.</param>
    /// <param name="storage">The actual or conservative element storage width.</param>
    internal void Collection(int count, int storage)
    {
        if ((uint)count > (uint)Limits.MaxCollectionLength) Fail("collection length");
        Work(count);
        Object();
        Allocate(256L + (long)count * (Math.Max(storage, 1) * 4L + 128));
    }

    /// <summary>Charges a decoded reference value before publication.</summary>
    internal void Object()
    {
        if (_objects == 0) Fail("object count");
        _objects--;
    }

    /// <summary>Checks nesting before entering a child parser.</summary>
    /// <param name="depth">The next value depth.</param>
    internal void Depth(int depth)
    {
        if (depth > Limits.MaxDepth) Fail("nesting depth");
        Work(1);
    }

    /// <summary>Rejects an exceeded input quota with a catchable data exception.</summary>
    /// <param name="name">The quota that was exceeded.</param>
    internal static void Fail(string name) => LuminPackExceptionHelper.ThrowInvalidDataException("Safe input exceeded its " + name + " limit.");
}
