using System.Collections.Generic;
using LuminPack.Code;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

class Comparer : IEqualityComparer<(LuminDataInfo, Compilation)>
{
    public static readonly Comparer Instance = new Comparer();

    public bool Equals((LuminDataInfo, Compilation) x, (LuminDataInfo, Compilation) y)
    {
        return x.Item1.Equals(y.Item1);
    }

    public int GetHashCode((LuminDataInfo, Compilation) obj)
    {
        return obj.Item1.GetHashCode();
    }
}