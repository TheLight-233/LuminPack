using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>Compiler-free declaration facts used only by caller-facing serializer documentation.</summary>
internal sealed class PayloadDocumentation : IEquatable<PayloadDocumentation>
{
    internal readonly TypeModel Model;
    internal readonly string TypeId, Layout;
    internal readonly string[] MemberTypes, MemberIds, StorageNames, StorageTypes, StorageOffsets;

    /// <summary>Freezes the documented declaration and its physical storage without retaining symbols.</summary>
    /// <param name="model">The exact serialization contract.</param>
    /// <param name="typeId">The compiler's documentation identifier for the payload definition.</param>
    /// <param name="layout">The declaration's CLR layout and explicit settings.</param>
    /// <param name="memberTypes">The declared type of each serialized member.</param>
    /// <param name="memberIds">The compiler documentation link of each serialized member.</param>
    /// <param name="storageNames">All native storage names, including ignored and backing storage.</param>
    /// <param name="storageTypes">The corresponding native field types.</param>
    /// <param name="storageOffsets">Explicit field offsets or the runtime-defined marker.</param>
    internal PayloadDocumentation(TypeModel model, string typeId, string layout, string[] memberTypes, string[] memberIds,
        string[] storageNames, string[] storageTypes, string[] storageOffsets)
    {
        Model = model;
        TypeId = typeId;
        Layout = layout;
        MemberTypes = memberTypes;
        MemberIds = memberIds;
        StorageNames = storageNames;
        StorageTypes = storageTypes;
        StorageOffsets = storageOffsets;
    }

    /// <inheritdoc/>
    public bool Equals(PayloadDocumentation? other) => ReferenceEquals(this, other) || other != null &&
        Model.Equals(other.Model) && TypeId == other.TypeId && Layout == other.Layout && Same(MemberTypes, other.MemberTypes) &&
        Same(MemberIds, other.MemberIds) && Same(StorageNames, other.StorageNames) && Same(StorageTypes, other.StorageTypes) &&
        Same(StorageOffsets, other.StorageOffsets);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PayloadDocumentation);

    /// <inheritdoc/>
    public override int GetHashCode() => Model.GetHashCode() ^ Layout.GetHashCode();

    /// <summary>Compares normalized documentation facts without allocating enumerators.</summary>
    /// <param name="left">The previous facts.</param>
    /// <param name="right">The current facts.</param>
    /// <returns>Whether every fact agrees in order.</returns>
    private static bool Same(string[] left, string[] right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (left[index] != right[index])
                return false;
        return true;
    }
}
