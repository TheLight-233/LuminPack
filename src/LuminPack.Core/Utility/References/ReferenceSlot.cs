using System.Runtime.CompilerServices;

namespace LuminPack.Utility;

/// <summary>Holds one object reference in an array that the runtime stores into without a covariance check.</summary>
/// <remarks>An <c>object[]</c> may be a covariant array of a derived type, so each store into it calls a type-checking
/// helper; an array of this struct is exact, so a store is a plain reference write.</remarks>
internal struct ReferenceSlot
{
    /// <summary>The referenced object, or null for an empty slot.</summary>
    public object? Value;
}
