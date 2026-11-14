#nullable disable
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the shared write and read statements of the list-backed object-model wrapper collections.</summary>
internal static class WrapperTraversal
{
    /// <summary>Writes a nonnull wrapper through the caller's isolated operation state.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="local">The nonnull wrapper expression.</param>
    /// <remarks>A wrapper over a plain list writes the list's elements as one contiguous block; the bytes equal the wrapper's own
    /// enumeration. Any other backing collection is counted while it is enumerated.</remarks>
    internal static void Write(List<string> body, string element, string local)
    {
        // A string element has no contiguous raw storage, so the wrapper is counted while it is enumerated.
        if (element == "string")
        {
            InterfaceTraversal.Counted(body, element, local);
            return;
        }
        body.Add("int wrapperDepth = 0;");
        body.Add("var wrapperList = global::LuminPack.Code.LuminPackCollectionAccess.GetUnderlyingIList(" + local + ", ref wrapperDepth);");
        body.Add("if (wrapperList != null)");
        body.Add("{");
        body.Add("global::System.ReadOnlySpan<" + element + "> wrapperItems = global::LuminPack.Code.LuminPackMarshal.GetListSpan(wrapperList, wrapperList.Count);");
        body.Add("operationState.Write<" + element + ">(wrapperItems, wrapperItems.Length);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("int wrapperPosition = operationState.BeginCollection();");
        body.Add("int wrapperCount = 0;");
        body.Add("foreach (var wrapperItem in " + local + ")");
        body.Add("{");
        body.Add("operationState.CheckBuffer();");
        body.Add("wrapperCount++;");
        body.Add("var wrapperValue = wrapperItem;");
        // A Boolean element writes through a read-only alias, as the legacy element write does.
        if (element == "bool")
        {
            body.Add("ref readonly bool wrapperFlag = ref wrapperValue;");
            body.Add("operationState.Write(in wrapperFlag);");
        }
        else
            body.Add("operationState.Write(in wrapperValue);");
        body.Add("}");
        body.Add("");
        body.Add("operationState.EndCollection(wrapperPosition, wrapperCount);");
        body.Add("operationState.CheckBuffer();");
        body.Add("}");
    }

    /// <summary>Writes a nonnull collection builder's logical backing-array range.</summary>
    /// <param name="body">The generated statements.</param>
    /// <param name="element">The scalar element type.</param>
    /// <param name="type">The closed builder type.</param>
    /// <param name="local">The nonnull builder expression.</param>
    internal static void WriteBuilder(List<string> body, string element, string type, string local)
    {
        if (element == "string")
        {
            InterfaceTraversal.Counted(body, element, local);
            return;
        }
        body.Add("var builderView = global::LuminPack.Code.LuminPackMarshal.As<" + type + ", global::LuminPack.Code.ReadOnlyCollectionBuilderView<" +
            element + ">>(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in " + local + "));");
        body.Add("global::System.ReadOnlySpan<" + element + "> builderItems = global::System.MemoryExtensions.AsSpan(builderView.items, 0, " + local + ".Count);");
        body.Add("operationState.Write<" + element + ">(builderItems, builderItems.Length);");
        body.Add("operationState.CheckBuffer();");
    }

    /// <summary>Reads the payload block of <c>length</c> raw elements into a span.</summary>
    /// <param name="c">The owner context.</param>
    /// <param name="span">The destination span expression.</param>
    internal static void ReadSpan(EnumerableOwnerContext c, string span) => c.ReadBlock(span);

    /// <summary>Renders a wrapper owner whose binary write is the shared wrapper write.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed sequence owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <param name="spec">The family algorithms.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations,
        EnumerableOwnerSpec spec) => EnumerableOwnerTemplate.Render(set, fact, operations, spec);

    /// <summary>Emits the shared wrapper write for an owner root.</summary>
    /// <param name="c">The owner context.</param>
    internal static void WriteRoot(EnumerableOwnerContext c)
    {
        if (WriteStringStorage(c))
            return;
        if (c.Forwarded)
        {
            c.WrapperWrite("value");
            return;
        }
        if (c.Payload != null)
        {
            c.CountedWrite("value");
            return;
        }
        var lines = new List<string>();
        Write(lines, c.Scalar, "value");
        foreach (string line in lines)
            c.W.Line(line);
    }

    /// <summary>Emits the checked JSON storage writer and its completed-array exit where available.</summary>
    /// <param name="c">The owner context.</param>
    internal static void WriteJsonIntegers(EnumerableOwnerContext c)
    {
        if (!c.Supports("WriteWrappedIntegers") || GenericFormatterReuse.Real(c.Scalar) is not ("int" or "long"))
            return;

        var w = c.W;
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteWrappedIntegers<TLuminProfile, " + c.Scalar + ">(ref writer, value))");
        w.Open();
        w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        w.Line("return;");
        w.Close();
    }

    /// <summary>Emits the builder write for an owner root.</summary>
    /// <param name="c">The owner context.</param>
    internal static void WriteBuilderRoot(EnumerableOwnerContext c)
    {
        if (WriteStringStorage(c))
            return;
        if (c.Forwarded)
        {
            c.BuilderWrite("value");
            return;
        }
        if (c.Payload != null)
        {
            c.CountedWrite("value");
            return;
        }
        var lines = new List<string>();
        WriteBuilder(lines, c.Scalar, c.Sequence, "value");
        foreach (string line in lines)
            c.W.Line(line);
    }

    private static bool WriteStringStorage(EnumerableOwnerContext c)
    {
        if (!c.Text || !c.Supports("WriteWrappedStrings"))
            return false;
        c.W.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryWriteWrappedStrings<TLuminProfile>(ref operationState, value))");
        c.W.Line("    goto operationComplete_RootWrite;");
        var lines = new List<string>();
        InterfaceTraversal.Counted(lines, c.Scalar, "value");
        foreach (var line in lines)
            c.W.Line(line);
        return true;
    }

    /// <summary>Emits insertion after the element formatter and checks the target's current subscribers.</summary>
    /// <param name="c">The owner context.</param>
    /// <param name="target">The observable target local.</param>
    internal static void AppendObservable(EnumerableOwnerContext c, string target) =>
        c.W.Line(c.Supports("AppendObservable") ?
            "global::LuminPack.Code.LuminPackCollectionAccess.AppendObservable(" + target + ", item);" :
            target + ".Add(item);");

    /// <summary>Gets the optional capacity expression for a newly owned backing collection.</summary>
    /// <param name="c">The owner context.</param>
    /// <returns>A constructor argument, or the empty string for the original constructor.</returns>
    internal static string JsonCapacity(EnumerableOwnerContext c) =>
        c.Supports("WrapperJsonCapacity") ? "reader.GetArrayCapacityHint<TLuminProfile>(128)" : "";

    /// <summary>Creates a fresh JSON wrapper or clears the original through its public operation.</summary>
    /// <param name="c">The owner context.</param>
    /// <param name="fresh">The statements that create the fresh result.</param>
    internal static void PrepareJson(EnumerableOwnerContext c, System.Action fresh)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Open();
        fresh();
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
    }

    /// <summary>Sizes the constructor-owned list of a fresh observable result when the hint and storage APIs are present.</summary>
    /// <param name="c">The owner context.</param>
    /// <param name="target">The fresh observable target local.</param>
    internal static void SizeObservableJson(EnumerableOwnerContext c, string target)
    {
        string capacity = JsonCapacity(c);
        if (capacity.Length == 0 || !c.Supports("ObservableJsonStorage"))
            return;
        c.W.Line("int capacity = " + capacity + ";");
        c.W.Line("if (capacity > 0 && global::LuminPack.Code.LuminPackCollectionAccess.GetSequenceStorage<" + c.Scalar +
            ">(" + target + ") is global::System.Collections.Generic.List<" + c.Scalar + "> backing)");
        c.W.Indented(() => c.W.Line("backing.Capacity = capacity;"));
    }
}
