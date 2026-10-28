using System.Collections.Generic;

namespace LuminPack.SourceGenerator;

/// <summary>Compares the complete generation settings without changing the public mutable metadata type.</summary>
internal sealed class MetaInfoValueComparer : IEqualityComparer<MetaInfo>
{
    internal static readonly MetaInfoValueComparer Instance = new MetaInfoValueComparer();

    /// <summary>Compares every setting read by the current emitters.</summary>
    /// <param name="left">The previous generation settings.</param>
    /// <param name="right">The current generation settings.</param>
    /// <returns>Whether both values specify the same generation behavior.</returns>
    public bool Equals(MetaInfo left, MetaInfo right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;
        return object.Equals(left.ParseOptions, right.ParseOptions) &&
               left.CSharpVersion == right.CSharpVersion &&
               left.IsNet8 == right.IsNet8 &&
               left.IsNet9_OR_GREATER == right.IsNet9_OR_GREATER &&
               left.IsForUnity == right.IsForUnity &&
               left.AllowUnsafe == right.AllowUnsafe &&
               left.RegisterMode == right.RegisterMode;
    }

    /// <summary>Hashes the same generation settings used for equality.</summary>
    /// <param name="value">The settings to hash.</param>
    /// <returns>A hash consistent with value equality.</returns>
    public int GetHashCode(MetaInfo value)
    {
        if (value == null) return 0;
        unchecked
        {
            int hash = value.ParseOptions?.GetHashCode() ?? 0;
            hash = hash * 31 + (int)value.CSharpVersion;
            hash = hash * 31 + value.IsNet8.GetHashCode();
            hash = hash * 31 + value.IsNet9_OR_GREATER.GetHashCode();
            hash = hash * 31 + value.IsForUnity.GetHashCode();
            hash = hash * 31 + value.AllowUnsafe.GetHashCode();
            return hash * 31 + (int)value.RegisterMode;
        }
    }
}
