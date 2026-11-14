#nullable disable
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the shared write of sequence interfaces used by owner roots and inline members.</summary>
internal static class InterfaceTraversal
{
    /// <summary>Writes a nonnull frozen set's item array as one block through the caller's isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="local">The nonnull frozen set expression.</param>
    internal static void WriteFrozen(List<string> body, string element, string local)
    {
        if (element == "string")
        {
            Counted(body, element, local);
            return;
        }
        body.Add("global::System.ReadOnlySpan<" + element + "> frozenItems = " + local + ".Items.AsSpan();");
        body.Add("operationState.Write<" + element + ">(frozenItems, frozenItems.Length);");
        body.Add("operationState.CheckBuffer();");
    }

    /// <summary>Reserves and backfills the count of a single enumeration.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The element's source spelling.</param>
    /// <param name="local">The collection expression to enumerate.</param>
    /// <param name="outlineText">Whether an enclosing operation supplies the outlined string write.</param>
    internal static void Counted(List<string> body, string element, string local, bool outlineText = false)
    {
        body.Add("int interfacePosition = operationState.BeginCollection();");
        body.Add("int interfaceCount = 0;");
        body.Add("foreach (var interfaceItem in " + local + ")");
        body.Add("{");
        if (element != "string")
            body.Add("operationState.CheckBuffer();");
        body.Add("interfaceCount++;");
        body.Add("var interfaceValue = interfaceItem;");
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        if (element == "string")
            body.Add(outlineText ? "WriteStringItem(ref operationState, interfaceValue);" : "operationState.Write<TLuminProfile>(interfaceValue);");
        else if (element == "bool")
        {
            body.Add("ref readonly bool interfaceFlag = ref interfaceValue;");
            body.Add("operationState.Write(in interfaceFlag);");
        }
        else
            body.Add("operationState.Write(in interfaceValue);");
        body.Add("}");
        body.Add("operationState.EndCollection(interfacePosition, interfaceCount);");
        body.Add("operationState.CheckBuffer();");
    }
}
