using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>Stack&lt;T&gt;</c> whose element is a packable class.</summary>
/// <remarks>The write copies the stack once and expands each element's record body from the last slot, which is the stored
/// bottom-first order. The read fills a pooled block and pushes it in stored order; the JSON read stages the top-first order and
/// pushes it back reversed.</remarks>
internal static class StackPayloadOwnerFile
{
    private static readonly EnumerableOwnerSpec Spec = new EnumerableOwnerSpec(c => c.StackPayloadWrite("value", false), Read,
        "staged.Add(item);", Before, After, readInlining: "NoInlining", outlineJson: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The payload-element stack owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations) =>
        EnumerableOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Read(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Sequence + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0" + (c.Text && c.Supports("ReadStringStackStorage") ?
            " || global::LuminPack.Code.LuminPackCollectionAccess.TryReadStringStackStorage<TLuminProfile>(ref operationState, value, length)" : "") + ")");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        c.PooledRead(() =>
        {
            w.Line("for (int index = 0; index < length; index++)");
            w.Indented(() => w.Line("value.Push(pooled[index]);"));
        });
    }

    private static void Before(EnumerableOwnerContext c)
        => c.W.Line("var staged = new global::System.Collections.Generic.List<" + c.Scalar + ">();");

    private static void After(EnumerableOwnerContext c)
    {
        var w = c.W;
        w.Line("value = new " + c.Sequence + "(staged.Count);");
        w.Line("for (int index = staged.Count - 1; index >= 0; index--)");
        w.Indented(() => w.Line("value.Push(staged[index]);"));
    }
}
