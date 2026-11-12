using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>Stops unchanged collected facts before downstream incremental transforms run.</summary>
/// <typeparam name="T">The compilation-independent fact type.</typeparam>
internal sealed class FactArrayComparer<T> : IEqualityComparer<ImmutableArray<T>> where T : struct, IEquatable<T>
{
    internal static readonly FactArrayComparer<T> Instance = new();

    /// <inheritdoc/>
    public bool Equals(ImmutableArray<T> left, ImmutableArray<T> right)
    {
        if (left == right)
            return true;
        if (left.IsDefault || right.IsDefault || left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!left[index].Equals(right[index]))
                return false;
        return true;
    }

    /// <inheritdoc/>
    public int GetHashCode(ImmutableArray<T> facts)
    {
        if (facts.IsDefault)
            return 0;
        int hash = facts.Length;
        for (int index = 0; index < facts.Length; index++)
            hash = unchecked(hash * 31 + facts[index].GetHashCode());
        return hash;
    }
}
