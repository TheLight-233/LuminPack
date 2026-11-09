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
        // A mirrored type also calls the mirror view of the read-only input: LuminPackMarshal.As and Unsafe.AsRef.
        int mirror = model.Mirror == null ? 0 : 2;
        if (members.All(member => member.IsFixed))
            return mirror + (union ? 1 : 2) + Group(members.Length) <= Limit;
        int count = mirror + (union ? 0 : 2);
        count += members.Count(member => !member.IsFixed);   // snapshot capture or string bound
        count += members.Count(member => !member.IsFixed);   // reserved record store
        count += 2;                                          // reservation and commit
        foreach (int run in Runs(model))
            count += run == 1 ? 1 : Group(run);
        return count <= Limit;
    }

    /// <summary>Whether a portable struct's write root keeps its aggressive inlining hint.</summary>
    internal static bool PortableWrite(DirectTypeModel model) => PortableCount(model, write: true) <= Limit;

    /// <summary>Whether a portable struct's read root keeps its aggressive inlining hint.</summary>
    internal static bool PortableRead(DirectTypeModel model) => PortableCount(model, write: false) <= Limit;

    /// <summary>Counts a portable struct operation's template calls: the span index and reference, the advance, the view
    /// (marshal and address) per branch, and per field its offset and transfer in each branch; a read also checks the span and the advance.</summary>
    private static int PortableCount(DirectTypeModel model, bool write)
        => write ? 7 + 4 * model.Members.Length : 7 + 2 * model.Members.Length;

    /// <summary>Whether a root read keeps its aggressive inlining hint.</summary>
    internal static bool Read(DirectTypeModel model)
    {
        int count = 1 + model.Members.Count(member => !member.IsFixed);
        // A mirrored type also calls the mirror view, and a mirrored string compares the decoded instance.
        if (model.Mirror != null)
            count += 1 + model.Members.Count(member => member.Mirrored && member.Kind == DirectMemberKind.Text);
        int first = 0;
        foreach (int run in Runs(model))
        {
            while (!model.Members[first].IsFixed)
                first++;
            // A run whose Booleans fit no batch mask checks each Boolean after the block read.
            count += run == 1 ? 1 : 5 * run + 1 + (DirectBinaryEmitter.HasBatchMask(model.Members, first, first + run - 1) ? 0 :
                model.Members.Skip(first).Take(run).Count(member => member.IsBoolean));
            first += run;
        }
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
