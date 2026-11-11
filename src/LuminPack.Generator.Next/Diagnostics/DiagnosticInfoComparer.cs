using System;
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Diagnostics;

/// <summary>Reuses incremental diagnostic output when every captured source coordinate and message is unchanged.</summary>
internal sealed class DiagnosticInfoComparer : IEqualityComparer<DiagnosticInfo[]>
{
    internal static readonly DiagnosticInfoComparer Instance = new DiagnosticInfoComparer();

    /// <inheritdoc/>
    public bool Equals(DiagnosticInfo[]? left, DiagnosticInfo[]? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.Length != right.Length)
            return false;
        return left.AsSpan().SequenceEqual(right.AsSpan());
    }

    /// <inheritdoc/>
    public int GetHashCode(DiagnosticInfo[] diagnostics)
    {
        int hash = 0;
        foreach (DiagnosticInfo diagnostic in diagnostics)
            hash = unchecked(hash * 31 + diagnostic.GetHashCode());
        return hash;
    }
}
