using System.Collections.Generic;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Provides comparer functionality.</summary>
class Comparer : IEqualityComparer<(LuminDataInfo, Compilation)>
{
    /// <summary>Stores the instance.</summary>
    public static readonly Comparer Instance = new Comparer();

    /// <summary>Determines whether the supplied value is equal to the current value.</summary>
    /// <param name="x">The x used by this operation.</param>
    /// <param name="y">The y used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public bool Equals((LuminDataInfo, Compilation) x, (LuminDataInfo, Compilation) y)
    {
        return x.Item1.Equals(y.Item1);
    }

    /// <summary>Computes the hash code used by the configured lookup or equality strategy.</summary>
    /// <param name="obj">The obj used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public int GetHashCode((LuminDataInfo, Compilation) obj)
    {
        return obj.Item1.GetHashCode();
    }
}
