using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders a list whose elements use their own generated static formatter.</summary>
internal static class ListForwardingOwnerFile
{
    /// <summary>Selects the retained list specializations or the default static element traversal.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="fact">The fact supplied to <c>RenderDefault</c>.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The generated C# source.</returns>
    internal static string RenderDefault(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        FormatterModel model = set.FindFormatter(fact.DeclaredType)!;
        FormatterModel element = model.Arguments[0];
        if (element.RawCollection)
            return ListScalarOwnerFile.Render(set, SequenceOwnerFact.ForScalar(fact.Family, model.Type, element.Type), operations);
        if (element.Type == "string")
            return ListStringOwnerFile.Render(set, fact, operations);
        if (element.Definition == FormatterDefinitions.Array)
            return Render(set, fact, operations);
        return ContiguousOwnerFile.Render(set, model, operations, ContiguousShape.List);
    }

    /// <summary>Renders the binary and JSON operations for one closed list.</summary>
    /// <param name="set">The shared formatter publication requirements.</param>
    /// <param name="fact">The list type and the type of its forwarded element.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The generated source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string collection = fact.DeclaredType;
        string item = fact.ForwardedType!;
        string owner = GeneratedNames.Owner(set.Extension!, collection);
        string element = "global::LuminPack." + GeneratedNames.Owner(set.Extension!, item);
        string serializer = "global::LuminPack.LuminPackSerializer";
        string marshal = w.A("LuminPackMarshal");
        string reader = w.A("LuminPackReader");
        string writer = w.A("LuminPackWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string options = w.A("ILuminPackOptions");
        bool stringElement = item == "string";
        bool compressElements = GenericFormatterReuse.IsJaggedCompressionElement(item) ||
            GenericFormatterReuse.IsCompressedPlaceholder(item);

        string roots = "LuminRoots_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(collection));
        string root = serializer + "." + roots;
        string defaultOptions = w.A("LuminPackUtf16LengthOptions");

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots,
            "internal static partial class " + owner, "internal static partial class " + set.Extension,
            EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    WriteBinary(w, collection, writer, options, marshal, element, "RootWrite", compressed: false, stringElements: stringElement);
                    WriteBinary(w, collection, writer, options, marshal, compressElements ? serializer : element,
                        "RootWriteCompressed", compressElements);
                    break;
                case FormatterSection.Owner:
                    WriteForwarder(writer, "RootWrite");
                    break;
                case FormatterSection.Extensions:
                    w.Line("public static void WriteValueWithCompress(this ref " + writer + " writer, in " + collection + " value)");
                    w.Line("    => " + root + ".RootWriteCompressed<" + defaultOptions + ">(ref writer, in value);");
                    break;
            }
        }

        void EmitBinaryRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    ReadBinary(w, collection, item, reader, options, marshal, element, "RootRead", compressed: false, stringElements: stringElement);
                    ReadBinary(w, collection, item, reader, options, marshal, compressElements ? serializer : element,
                        "RootReadCompressed", compressElements);
                    break;
                case FormatterSection.Owner:
                    w.Line("internal static void Read<TOptions>(ref " + reader + " reader, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + root + ".RootRead<TOptions>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Extensions:
                    w.Line("public static void ReadValueWithCompress(this ref " + reader + " reader, ref " + collection + " value)");
                    w.Line("    => " + root + ".RootReadCompressed<" + defaultOptions + ">(ref reader, ref value);");
                    break;
            }
        }

        void EmitJsonWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    w.Line(w.Preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootWriteJson<TOptions>(scoped ref " + jsonWriter + " writer, in " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("writer.WriteNull<TOptions>();");
                    w.Line("return;");
                    w.Close();
                    w.Line("writer.WriteArrayStart<TOptions>();");
                    w.Line("var span = " + marshal + ".GetListSpan(value);");
                    w.Line("if (!span.IsEmpty)");
                    w.Open();
                    w.Line("ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
                    w.Line("int index = 0;");
                    w.Line("do");
                    w.Open();
                    const string slot = "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, (nint)(uint)index)";
                    if (stringElement)
                        w.Line("writer.WriteElement<TOptions>(" + slot + ", index != 0);");
                    else
                    {
                        w.Line("if (index != 0)");
                        w.Line("    writer.WriteByteRaw<TOptions>((byte)',');");
                        w.Line("writer.SetFirstElement(true);");
                        w.Line(element + ".Write<TOptions>(ref writer, in " + slot + ");");
                    }
                    w.Close();
                    w.Line("while (++index < span.Length);");
                    w.Close();
                    w.Line("writer.WriteArrayEnd<TOptions>();");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    WriteForwarder(jsonWriter, "RootWriteJson");
                    break;
            }
        }

        void EmitJsonRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    w.Line(w.Preserve);
                    w.Line(w.MethodImpl("NoInlining"));
                    w.Line("internal static void RootReadJson<TOptions>(scoped ref " + jsonReader + " reader, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Line("reader.TryConsumeArrayStart<TOptions>();");
                    w.Line("if (value is null || value.GetType() != typeof(" + collection + "))");
                    w.Line("    value = new " + collection + "(reader.GetArrayCapacityHint<TOptions>(10));");
                    w.Line("int count = 0;");
                    w.Line("while (reader.Read<TOptions>())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line("var item = count < value.Count ? value[count] : default;");
                    w.Line(element + ".Read<TOptions>(ref reader, ref item);");
                    w.Line("if (count < value.Count)");
                    w.Line("    value[count] = item;");
                    w.Line("else");
                    w.Line("    value.Add(item);");
                    w.Line("count++;");
                    w.Close();
                    w.Line("if (count < value.Count)");
                    w.Line("    value.RemoveRange(count, value.Count - count);");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line("internal static void Read<TOptions>(ref " + jsonReader + " reader, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !reader.Read<TOptions>())");
                    w.Line("    " + w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(root + ".RootReadJson<TOptions>(ref reader, ref value);");
                    w.Line("reader.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }

        void WriteForwarder(string state, string method)
        {
            w.Line("internal static void Write<TOptions>(ref " + state + " writer, in " + collection + " value)");
            w.Line("    where TOptions : struct, " + options + " => " + root + "." + method + "<TOptions>(ref writer, in value);");
            w.Blank();
        }
    }

    /// <summary>Emits the list header and a static write for each element.</summary>
    /// <param name="w">The owner source writer.</param>
    /// <param name="collection">The closed list type.</param>
    /// <param name="writer">The binary state type.</param>
    /// <param name="options">The compiled profile interface.</param>
    /// <param name="marshal">The list storage helper.</param>
    /// <param name="element">The container that provides the element operation.</param>
    /// <param name="method">The generated method name.</param>
    /// <param name="compressed">Whether the element uses its compressed operation.</param>
    /// <param name="stringElements">Whether to transfer the normal string payload as a span.</param>
    private static void WriteBinary(CodeWriter w, string collection, string writer, string options,
        string marshal, string element, string method, bool compressed, bool stringElements = false)
    {
        string operation = compressed ? "SerializeWithCompress" : "Write";
        w.Line(w.Preserve);
        if (stringElements)
            w.Line(w.MethodImpl("NoInlining"));
        w.Line("internal static void " + method + "<TOptions>(scoped ref " + writer + " writer, in " + collection + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.WriteNullCollectionHeader();");
        w.Line("return;");
        w.Close();
        w.Line("var span = " + marshal + ".GetListSpan(value);");
        w.Line("writer.Write(span.Length);");
        if (stringElements)
            w.Line("writer.WriteStrings<TOptions>(span);");
        else
        {
            w.Line("foreach (ref readonly var item in span)");
            w.Open();
            w.Line("writer.CheckBuffer();");
            w.Line(element + "." + operation + "<TOptions>(ref writer, in item);");
            w.Close();
        }
        w.Line("writer.CheckBuffer();");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits a length check before obtaining and filling the reusable list span.</summary>
    /// <param name="w">The owner source writer.</param>
    /// <param name="collection">The closed list type.</param>
    /// <param name="item">The child collection type.</param>
    /// <param name="reader">The binary state type.</param>
    /// <param name="options">The compiled profile interface.</param>
    /// <param name="marshal">The list storage helper.</param>
    /// <param name="element">The container that provides the element operation.</param>
    /// <param name="method">The generated method name.</param>
    /// <param name="compressed">Whether the element uses its compressed operation.</param>
    /// <param name="stringElements">Whether to transfer the normal string payload as a span.</param>
    private static void ReadBinary(CodeWriter w, string collection, string item, string reader, string options,
        string marshal, string element, string method, bool compressed, bool stringElements = false)
    {
        string operation = compressed ? "DeserializeWithCompress" : "Read";
        w.Line(w.Preserve);
        if (stringElements)
            w.Line(w.MethodImpl("NoInlining"));
        w.Line("internal static void " + method + "<TOptions>(scoped ref " + reader + " reader, ref " + collection + " value)");
        w.Line("    where TOptions : struct, " + options);
        w.Open();
        if (stringElements)
        {
            // A string list reads through an isolated copy of the reader and publishes its cursor only after the last element,
            // so a failure leaves the caller's reader where it was.
            string complete = "operationComplete_" + method;
            w.Line(reader + " operationState = default;");
            w.Line("reader.CopyTo(ref operationState);");
            w.Line("int length = operationState.ReadLength(1);");
            w.Line("if (length < 0)");
            w.Open();
            w.Line("value = null;");
            w.Line("goto " + complete + ";");
            w.Close();
            w.Blank();
            w.Line("value ??= new " + collection + "(length);");
            w.Line("var span = " + marshal + ".GetListSpan(value, length);");
            w.Line("operationState.ReadStrings<TOptions>(span);");
            w.Blank();
            w.Line(complete + ":");
            w.Line("    ;");
            w.Line("operationState.CopyTo(ref reader);");
            w.Close();
            w.Blank();
            return;
        }
        w.Line("int length = reader.ReadLength(1);");
        w.Line("if (length < 0)");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Line("value ??= new " + collection + "(length);");
        w.Line("var span = " + marshal + ".GetListSpan(value, length);");
        w.Line("foreach (ref var item in span)");
        w.Open();
        ForwardedElementRead.Emit(w, item, "reader", "item",
            element + "." + operation + "<TOptions>(ref reader, ref item);", compressed);
        w.Close();
        w.Close();
        w.Blank();
    }
}
