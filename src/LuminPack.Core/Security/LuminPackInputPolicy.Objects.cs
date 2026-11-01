using System.ComponentModel;
using System.Runtime.CompilerServices;
using LuminPack.Core;

namespace LuminPack.Security;

public static partial class LuminPackInputPolicy
{
    /// <summary>Snapshots only the binary object state needed by the enclosing value.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct ObjectScope
    {
        internal readonly InputContext? Context;
        internal readonly LuminPackSafeSchema? Previous;
        internal readonly int Depth, Count, UniqueCount, PreviousStart;
        internal int FieldIndex;
        internal readonly bool MarkerSeen, UniqueRecord;
        internal readonly long UniqueTextWork;
        /// <summary>Captures binary state without copying JSON grammar and duplicate-name fields.</summary>
        /// <param name="context">The exclusive synchronous root.</param>
        internal ObjectScope(InputContext context)
        {
            Context = context; Previous = context.Value; Depth = context.Depth; Count = context.Count;
            UniqueCount = context.UniqueCount; PreviousStart = context.ValueStart; FieldIndex = context.FieldIndex;
            MarkerSeen = context.MarkerSeen; UniqueRecord = context.UniqueRecord; UniqueTextWork = context.UniqueTextWork;
        }
    }

    /// <summary>Begins one generated binary object while preserving original depth and resource accounting.</summary>
    /// <param name="reader">The original checked reader.</param>
    /// <param name="schema">The typed object contract.</param>
    /// <returns>The compact enclosing-state token.</returns>
    public static ObjectScope EnterObject(ref LuminPackReader reader, LuminPackSafeSchema schema)
    {
        var context = CurrentInput;
        if (context == null || !context.Reading) return default;
        if (context.Depth == 0)
        {
            context.Budget.Depth(1); context.Depth = 1; context.Value = schema; context.ValueStart = reader.Position;
            context.Count = -1; context.MarkerSeen = false; context.FieldIndex = 0;
            return default;
        }
        var scope = new ObjectScope(context);
        context.Budget.Depth(context.Depth + 1);
        context.Depth++; context.Value = schema; context.ValueStart = reader.Position;
        context.UniqueRecord = false; context.Count = -1; context.MarkerSeen = false; context.FieldIndex = 0;
        context.UniqueTextWork = 0;
        return scope;
    }

    /// <summary>Validates the parent's next fixed batch after its child completed successfully.</summary>
    /// <param name="reader">The original completed child reader.</param>
    /// <param name="scope">The compact mutable restoration token.</param>
    public static void CompleteObject(ref LuminPackReader reader, ref ObjectScope scope)
    {
        if (scope.Context != null && scope.Previous?.Nodes[0].Kind == LuminPackSafeKind.Object)
            scope.FieldIndex = ValidateFixedPrefix(ref reader, scope.Previous, scope.FieldIndex + 1, scope.Depth);
    }

    /// <summary>Restores binary parent state without a large by-value token copy.</summary>
    /// <param name="scope">The matching token, borrowed by readonly reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExitObject(in ObjectScope scope)
    {
        var context = scope.Context;
        if (context == null) return;
        context.Value = scope.Previous; context.Depth = scope.Depth; context.Count = scope.Count;
        context.UniqueCount = scope.UniqueCount; context.ValueStart = scope.PreviousStart;
        context.FieldIndex = scope.FieldIndex; context.MarkerSeen = scope.MarkerSeen;
        context.UniqueRecord = scope.UniqueRecord; context.UniqueTextWork = scope.UniqueTextWork;
    }
}
