using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

internal readonly partial struct SharedFormatterSet
{
    private static readonly IComparer<SequenceOwnerFact> SequenceOrder =
        Comparer<SequenceOwnerFact>.Create(static (left, right) => StringComparer.Ordinal.Compare(left.DeclaredType, right.DeclaredType));
    private static readonly IComparer<DictionaryOwnerFact> DictionaryOrder =
        Comparer<DictionaryOwnerFact>.Create(static (left, right) => StringComparer.Ordinal.Compare(left.DeclaredType, right.DeclaredType));
    private static readonly IComparer<FormatterModel> GeneralOrder =
        Comparer<FormatterModel>.Create(static (left, right) => StringComparer.Ordinal.Compare(left.Type, right.Type));

    /// <summary>Sorts and compacts transient name storage before allocating its exact immutable result.</summary>
    /// <param name="values">The exclusively owned collection buffer.</param>
    /// <returns>The ordinal, duplicate-free names.</returns>
    private static string[] SortedNames(List<string> values)
    {
        if (values.Count == 0)
            return Array.Empty<string>();
        values.Sort(StringComparer.Ordinal);
        int count = 1;
        for (int index = 1; index < values.Count; index++)
            if (!StringComparer.Ordinal.Equals(values[count - 1], values[index]))
                values[count++] = values[index];
        var result = new string[count];
        values.CopyTo(0, result, 0, count);
        return result;
    }

    /// <summary>Keeps the first owner for each identity and sorts only the unique prefix.</summary>
    /// <typeparam name="T">The normalized owner fact.</typeparam>
    /// <param name="values">The exclusively owned collection buffer in discovery order.</param>
    /// <param name="identity">The ordinal owner identity.</param>
    /// <param name="order">The comparer for that identity.</param>
    /// <returns>The sorted first declarations, preserving the original grouping contract.</returns>
    private static T[] SortedOwners<T>(List<T> values, Func<T, string> identity, IComparer<T> order)
    {
        if (values.Count == 0)
            return Array.Empty<T>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        for (int index = 0; index < values.Count; index++)
        {
            T value = values[index];
            if (seen.Add(identity(value)))
                values[count++] = value;
        }
        values.Sort(0, count, order);
        var result = new T[count];
        values.CopyTo(0, result, 0, count);
        return result;
    }

    /// <summary>Copies and sorts the already unique general contracts without an ordered enumeration.</summary>
    /// <param name="values">The complete contracts keyed by ordinal type identity.</param>
    /// <returns>The exact sorted publication array.</returns>
    private static FormatterModel[] SortedGeneral(Dictionary<string, FormatterModel> values)
    {
        if (values.Count == 0)
            return Array.Empty<FormatterModel>();
        var result = new FormatterModel[values.Count];
        values.Values.CopyTo(result, 0);
        Array.Sort(result, GeneralOrder);
        return result;
    }

    /// <summary>Compares general owner arrays without allocating enumerators.</summary>
    /// <param name="left">The preceding contracts.</param>
    /// <param name="right">The current contracts.</param>
    /// <returns>Whether both normalized arrays contain the same facts in order.</returns>
    private static bool SameGeneral(FormatterModel[]? left, FormatterModel[]? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || left.Length == 0)
            return right == null || right.Length == 0;
        if (right == null || left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!left[index].Equals(right[index]))
                return false;
        return true;
    }
}
