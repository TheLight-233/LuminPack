using System.Text;
using LuminPack.Code;
using Microsoft.CodeAnalysis;
namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>Stages immutable string candidates through the original complete element operations.</summary>
internal static class ImmutableStringSetEmitter
{
    /// <summary>Emits complete immutable string set decoding with validated candidate reuse.</summary>
    /// <param name="fieldData">The closed collection contract.</param>
    /// <param name="sb">The source builder.</param>
    /// <param name="sorted">Whether the collection is sorted.</param>
    /// <returns>Whether the string branch is emitted.</returns>
    internal static bool TryGenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb, bool sorted)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        if (element?.SpecialType != SpecialType.System_String) return false;
        string family = sorted ? "ImmutableSortedSet" : "ImmutableHashSet";
        string comparer = sorted ? "Comparer" : "EqualityComparer";
        string cache = "__LuminImmutableCandidate<global::System.Collections.Immutable." + family + "<string>>.Last";
        CollectionEmitterKit.AppendReadLength(sb, element, "string", "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable." + family + "<string>.Empty; return; }");
        sb.AppendLine("            var items = global::System.Buffers.ArrayPool<string>.Shared.Rent(length);");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine("                bool fromCache = value is null;");
        sb.AppendLine("                var candidate = value;");
        sb.AppendLine("                if (candidate is null) " + cache + ".TryGetTarget(out candidate);");
        sb.AppendLine("                bool same = candidate is not null && candidate.Count == length && global::System.Object.ReferenceEquals(candidate.KeyComparer, global::System.Collections.Generic." + comparer + "<string>.Default);");
        sb.AppendLine("                bool hasPrior = same;");
        sb.AppendLine("                if (hasPrior && !fromCache) global::LuminPack.Utility.LuminTreeStorage.CopyTo(candidate!, items);");
        sb.AppendLine("                for (int i = 0; i < length; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    string prior = hasPrior ? fromCache && i == 0 ? CandidateFirst(candidate!) : items[i] : default!;");
        sb.AppendLine("                    string selected = prior;");
        sb.AppendLine("                    reader.ReadValue(ref selected);");
        sb.AppendLine("                    if (!global::System.Object.ReferenceEquals(prior, selected)) { same = false; if (fromCache) hasPrior = false; }\n                else if (hasPrior && fromCache && i == 0) global::LuminPack.Utility.LuminTreeStorage.CopyTo(candidate!, items);");
        sb.AppendLine("                    items[i] = selected;");
        sb.AppendLine("                }");
        if (sorted)
        {
            // The framework default string comparer follows CurrentCulture on every comparison.
            // An unchanged payload cannot retain a tree whose old ordering is no longer valid.
            sb.AppendLine("                if (same)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var comparer = global::System.Collections.Generic.Comparer<string>.Default;");
            sb.AppendLine("                    for (int i = 1; i < length; i++) if (comparer.Compare(items[i - 1], items[i]) >= 0) { same = false; break; }");
            sb.AppendLine("                }");
        }
        sb.AppendLine("                if (same) { " + cache + ".SetTarget(candidate!); value = candidate!; return; }");
        sb.AppendLine("                var completed = global::LuminPack.Utility.LuminTreeStorage.Create" + family + "(items, length);");
        sb.AppendLine("                " + cache + ".SetTarget(completed);");
        sb.AppendLine("                value = completed;");
        sb.AppendLine("            }");
        sb.AppendLine("            finally { global::System.Buffers.ArrayPool<string>.Shared.Return(items, true); }");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static string CandidateFirst(global::System.Collections.Immutable." + family + "<string> candidate)");
        sb.AppendLine("            {");
        if (sorted) sb.AppendLine("                return candidate.Min!;");
        else
        {
            sb.AppendLine("                using var entries = candidate.GetEnumerator();");
            sb.AppendLine("                return entries.MoveNext() ? entries.Current : default!;");
        }
        sb.AppendLine("            }");
        return true;
    }
}
