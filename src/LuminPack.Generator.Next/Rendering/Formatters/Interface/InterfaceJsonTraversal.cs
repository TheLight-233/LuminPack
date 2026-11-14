using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits value enumerators for the exact implementations of sequence and set interfaces.</summary>
internal static class InterfaceJsonTraversal
{
    /// <summary>Shares element formatting while preserving the caller's count query and framing.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The available formatter capabilities.</param>
    /// <param name="sequence">The declared collection interface.</param>
    /// <param name="scalar">The element type.</param>
    /// <param name="owner">The original element formatter.</param>
    /// <param name="writer">The spelling of the JSON writer type.</param>
    /// <param name="setInterface">Whether the interface can hold a framework set.</param>
    internal static void Write(CodeWriter w, SharedFormatterSet set, string sequence, string scalar, string owner, string writer,
        bool setInterface)
    {
        const string collections = "global::System.Collections.Generic.";
        string firstType = collections + (setInterface ? "HashSet" : "List") + "<" + scalar + ">";
        string secondType = setInterface ? collections + "SortedSet<" + scalar + ">" : ContiguousOwnerFile.ArrayType(scalar);
        string memory = w.A("Unsafe");
        w.Line("var actualType = value.GetType();");
        w.Line("if (actualType == typeof(" + firstType + "))");
        w.Indented(() => w.Line("WriteKnown(ref writer, " + memory + ".As<" + firstType + ">(value));"));
        w.Line("else if (actualType == typeof(" + secondType + "))");
        w.Indented(() => w.Line("WriteOther(ref writer, " + memory + ".As<" + secondType + ">(value));"));
        w.Line("else");
        w.Indented(() => w.Line("WriteInterface(ref writer, value);"));
        w.Blank();
        EmitLoop("WriteKnown", firstType);
        EmitLoop("WriteOther", secondType);
        EmitLoop("WriteInterface", sequence);

        void EmitLoop(string method, string type)
        {
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("static void " + method + "(scoped ref " + writer + " writer, " + type + " items)");
            w.Open();
            w.Line("bool isFirst = true;");
            bool array = !setInterface && method == "WriteOther";
            if (array)
            {
                w.Line("int length = items.Length;");
                w.Line("if (length == 0) return;");
                w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items);");
                w.Line("int index = 0;");
                w.Line("do");
            }
            else
                w.Line("foreach (var item in items)");
            w.Open();
            if (array)
                w.Line("var item = " + memory + ".Add(ref first, (nint)(uint)index);");
            JsonSequenceElementWriter.Emit(w, set, sequence, scalar, owner);
            w.Close();
            if (array)
                w.Line("while (++index < length);");
            w.Close();
            w.Blank();
        }
    }
}
