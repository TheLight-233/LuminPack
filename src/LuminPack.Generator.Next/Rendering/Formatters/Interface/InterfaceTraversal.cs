#nullable disable
using System.Collections.Generic;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the shared write of sequence interfaces used by owner roots and inline members.</summary>
internal static class InterfaceTraversal
{
    /// <summary>Writes a nonnull sequence interface through the caller's isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="local">The nonnull interface expression.</param>
    /// <param name="memory">The spelling of the unsafe memory helper.</param>
    /// <remarks>The exact runtime type of the common implementations is tested first, so a list or an array writes its contiguous
    /// storage without interface dispatch; every other implementation is counted while it is enumerated.</remarks>
    internal static void Write(List<string> body, string element, string local, string memory)
    {
        // A string element has no contiguous raw storage, so every implementation is counted while it is enumerated.
        if (element == "string")
        {
            Counted(body, element, local);
            return;
        }
        string list = "global::System.Collections.Generic.List<" + element + ">";
        body.Add("if (" + local + ".GetType() == typeof(" + list + "))");
        body.Add("{");
        body.Add("global::System.ReadOnlySpan<" + element + "> listItems = global::LuminPack.Code.LuminPackMarshal.GetListSpan(" + memory + ".As<" + list + ">(" + local + "));");
        body.Add("operationState.Write<" + element + ">(listItems, listItems.Length);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("else if (" + local + ".GetType() == typeof(" + element + "[]))");
        body.Add("{");
        body.Add("global::System.ReadOnlySpan<" + element + "> arrayItems = " + memory + ".As<" + element + "[]>(" + local + ");");
        body.Add("operationState.Write<" + element + ">(arrayItems, arrayItems.Length);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        Counted(body, element, local);
        body.Add("}");
    }

    /// <summary>Writes a nonnull set interface through the caller's isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="local">The nonnull interface expression.</param>
    /// <param name="memory">The spelling of the unsafe memory helper.</param>
    internal static void WriteSet(List<string> body, string element, string local, string memory)
    {
        if (element == "string")
        {
            Counted(body, element, local);
            return;
        }
        string set = "global::System.Collections.Generic.HashSet<" + element + ">";
        body.Add("if (" + local + ".GetType() == typeof(" + set + "))");
        body.Add("{");
        body.Add("global::LuminPack.Code.LuminPackMarshal.WriteHashSetRaw(ref operationState, " + memory + ".As<" + set + ">(" + local + "));");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        Counted(body, element, local);
        body.Add("}");
    }

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

    internal static void Counted(List<string> body, string element, string local)
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
            body.Add("operationState.Write<TLuminProfile>(interfaceValue);");
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
