using System.Linq;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Applies <c>GeneratedExecutionPolicy</c>'s sixteen-call root budget to covered binary roots.</summary>
/// <remarks>The policy counts the invocations of the expanded operation template before the cursor and storage projections
/// rewrite it. These counts follow that template's statements for the covered shapes: the null marker, member snapshots and
/// string bounds, the reservation and header advance, one store or load per lone fixed member or variable member, and for a
/// run of fixed members its contiguity proof, block copy and field-wise fallback.</remarks>
internal static class DirectInlining
{
    private const int Limit = 16;

    /// <summary>Whether a root write keeps its aggressive inlining hint.</summary>
    internal static bool Write(DirectTypeModel model, bool union)
    {
        var members = model.Members;
        if (members.All(member => member.IsFixed))
            return (union ? 1 : 2) + Group(members.Length) <= Limit;
        int count = union ? 0 : 2;
        count += members.Count(member => !member.IsFixed);   // snapshot capture or string bound
        count += members.Count(member => !member.IsFixed);   // reserved record store
        count += 2;                                          // reservation and commit
        foreach (int run in Runs(model))
            count += run == 1 ? 1 : Group(run);
        return count <= Limit;
    }

    /// <summary>Whether a root read keeps its aggressive inlining hint.</summary>
    internal static bool Read(DirectTypeModel model)
    {
        int count = 1 + model.Members.Count(member => !member.IsFixed);
        foreach (int run in Runs(model))
            count += run == 1 ? 1 : 5 * run + 1;
        return count <= Limit;
    }

    /// <summary>Whether an all-fixed Union read keeps its aggressive inlining hint.</summary>
    internal static bool FixedUnionRead(DirectTypeModel model) => 2 * model.Members.Length + 1 <= Limit;

    /// <summary>Counts a fixed-run write: its proof, block copy, field-wise stores and cursor advance.</summary>
    private static int Group(int fields) => 7 * fields - 1;

    /// <summary>Lists the lengths of consecutive fixed member runs.</summary>
    private static System.Collections.Generic.IEnumerable<int> Runs(DirectTypeModel model)
    {
        var members = model.Members;
        for (int index = 0; index < members.Length; index++)
        {
            if (!members[index].IsFixed)
                continue;
            int last = index;
            while (last + 1 < members.Length && members[last + 1].IsFixed)
                last++;
            yield return last - index + 1;
            index = last;
        }
    }
}
