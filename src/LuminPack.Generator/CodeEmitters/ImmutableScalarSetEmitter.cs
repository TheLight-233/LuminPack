using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Shares complete immutable set reuse for proven framework scalar storage.</summary>
internal static class ImmutableScalarSetEmitter
{
    /// <summary>Emits validated scalar decoding and retains an equal default-comparer immutable destination.</summary>
    /// <param name="fieldData">The canonical closed immutable set contract.</param>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="sorted">Whether the destination uses ordered comparison rather than hashing.</param>
    /// <returns>Whether the known scalar branch supplied the complete binary reader.</returns>
    internal static bool TryGenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb, bool sorted)
    {
        var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
        if (!EmitterTypeTraits.IsKnownScalar(element)) return false;
        string elementType = CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName);
        string family = sorted ? "ImmutableSortedSet" : "ImmutableHashSet";
        string comparer = sorted ? "Comparer" : "EqualityComparer";
        string cache = "__LuminImmutableCandidate<global::System.Collections.Immutable." + family + "<" + elementType + ">>.Last";
        CollectionEmitterKit.AppendReadLength(sb, element, elementType, "value = null;", "            ", scalarElements: true);
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Immutable." + family + "<" + elementType + ">.Empty; return; }");
        CollectionEmitterKit.AppendPooledRead(sb, element, elementType, new[]
        {
            "bool fromCache = value is null;",
            "var candidate = value;",
            "if (candidate is null) " + cache + ".TryGetTarget(out candidate);",
            "if (candidate is not null && candidate.Count == length && global::System.Object.ReferenceEquals(candidate.KeyComparer, global::System.Collections.Generic." + comparer + "<" + elementType + ">.Default) && (!fromCache || CandidateHasFirst(candidate, __pooled[0])))",
            "{",
            "    var prior = global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Rent(length);",
            "    try",
            "    {",
            "        global::LuminPack.Utility.LuminTreeStorage.CopyTo(candidate, prior);",
            "        if (global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(prior.AsSpan(0, length)).SequenceEqual(global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(__pooled.AsSpan(0, length)))) { " + cache + ".SetTarget(candidate); value = candidate; return; }",
            "    }",
            "    finally { global::System.Buffers.ArrayPool<" + elementType + ">.Shared.Return(prior, false); }",
            "}",
            "var completed = global::LuminPack.Utility.LuminTreeStorage.Create" + family + "(__pooled, length);",
            cache + ".SetTarget(completed);",
            "value = completed;"
        }, "            ", scalarElements: true);
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static bool CandidateHasFirst(global::System.Collections.Immutable." + family + "<" + elementType + "> candidate, " + elementType + " wireFirst)");
        sb.AppendLine("            {");
        if (sorted)
        {
            sb.AppendLine("                var stored = candidate.Min;");
            sb.AppendLine("                return global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref stored, 1)).SequenceEqual(global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref wireFirst, 1)));");
        }
        else
        {
            // Membership is only a negative hint; the complete raw comparison still decides a hit.
            sb.AppendLine("                return candidate.Contains(wireFirst);");
        }
        sb.AppendLine("            }");
        return true;
    }
}
