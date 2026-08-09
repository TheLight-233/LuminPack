using System;
using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.Formatter;

namespace LuminPack.SourceGenerator;

/// <summary>
/// Evaluator discovery is kept separate so the binary/JSON formatter table remains the
/// four-entry table used by formatter generation.
/// </summary>
public static class FormatterEvaluatorDiscovery
{
    public static Action<LuminLocalFieldData, StringBuilder> GetFormatter(string typeName)
    {
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            return CollectionEvaluatorFormatter.GenerateArrayCalculateOffsetCode;
        }

        if (FormatterDiscovery.KnownValueTypes.Contains(typeName))
        {
            return UnmanagedFormatter.GenerateCalculateOffsetCode;
        }

        if (typeName is "string" or "global::System.String")
        {
            return StringFormatter.GenerateCalculateOffsetCode;
        }

        int genericSeparator = typeName.IndexOf('<');
        string baseTypeName = genericSeparator < 0 ? typeName : typeName.Substring(0, genericSeparator);
        switch (baseTypeName)
        {
            case "global::System.Collections.Generic.KeyValuePair":
                return KeyValuePairFormatter.GenerateCalculateOffsetCode;

            case "global::System.Linq.IGrouping":
                return InterfaceGroupingFormatter.GenerateCalculateOffsetCode;

            case "global::System.Collections.Generic.Dictionary":
            case "global::System.Collections.Concurrent.ConcurrentDictionary":
            case "global::System.Collections.Generic.SortedDictionary":
            case "global::System.Collections.Generic.SortedList":
            case "global::System.Collections.ObjectModel.ReadOnlyDictionary":
            case "global::System.Collections.Generic.IDictionary":
            case "global::System.Collections.Generic.IReadOnlyDictionary":
            case "global::System.Collections.Immutable.ImmutableDictionary":
            case "global::System.Collections.Immutable.ImmutableSortedDictionary":
            case "global::System.Collections.Immutable.IImmutableDictionary":
            case "global::System.Collections.Frozen.FrozenDictionary":
                return CollectionEvaluatorFormatter.GenerateDictionaryCalculateOffsetCode;

            case "global::System.Collections.Generic.Queue":
                return CollectionEvaluatorFormatter.GenerateQueueCalculateOffsetCode;

            case "global::System.Collections.Immutable.ImmutableArray":
                return CollectionEvaluatorFormatter.GenerateValueEnumerableCalculateOffsetCode;

            case "global::System.Collections.Generic.List":
            case "global::System.Collections.Generic.Stack":
            case "global::System.Collections.Generic.LinkedList":
            case "global::System.Collections.Generic.HashSet":
            case "global::System.Collections.Generic.SortedSet":
            case "global::System.Collections.Concurrent.BlockingCollection":
            case "global::System.Collections.Concurrent.ConcurrentBag":
            case "global::System.Collections.Concurrent.ConcurrentQueue":
            case "global::System.Collections.Concurrent.ConcurrentStack":
            case "global::System.Collections.ObjectModel.Collection":
            case "global::System.Collections.ObjectModel.ObservableCollection":
            case "global::System.Collections.ObjectModel.ReadOnlyCollection":
            case "global::System.Collections.ObjectModel.ReadOnlyObservableCollection":
            case "global::System.Collections.Generic.IEnumerable":
            case "global::System.Collections.Generic.ICollection":
            case "global::System.Collections.Generic.IReadOnlyCollection":
            case "global::System.Collections.Generic.IList":
            case "global::System.Collections.Generic.IReadOnlyList":
            case "global::System.Collections.Generic.ISet":
            case "global::System.Collections.Generic.IReadOnlySet":
            case "global::System.Collections.Immutable.ImmutableList":
            case "global::System.Collections.Immutable.ImmutableQueue":
            case "global::System.Collections.Immutable.ImmutableStack":
            case "global::System.Collections.Immutable.ImmutableHashSet":
            case "global::System.Collections.Immutable.ImmutableSortedSet":
            case "global::System.Collections.Immutable.IImmutableList":
            case "global::System.Collections.Immutable.IImmutableQueue":
            case "global::System.Collections.Immutable.IImmutableStack":
            case "global::System.Collections.Immutable.IImmutableSet":
            case "global::System.Collections.Frozen.FrozenSet":
                return CollectionEvaluatorFormatter.GenerateEnumerableCalculateOffsetCode;

            default:
                return null;
        }
    }
}
