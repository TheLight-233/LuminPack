using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders the existing ABI views, packed scalar transfers and tagged contiguous payload operations.</summary>
internal static class FormatterStorageEmitter
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Marshal = "global::LuminPack.Code.LuminPackMarshal";
    /// <summary>Names the verified runtime layout descriptor for a closed storage type.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Layout(FormatterStorageModel model) => "StorageLayout_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
    /// <summary>Names the ABI-preserving storage mirror.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    private static string View(FormatterStorageModel model) => "StorageView_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
    /// <summary>Names the runtime tag transfer helper.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Tag(FormatterStorageModel model) => "global::LuminPack.Utility.LuminStorageTag<" + model.Type + ", " + Layout(model) + ">";
    /// <summary>Returns the contiguous payload width, including a tag only for nonempty tagged storage.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "count">The count used by this operation.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Width(FormatterStorageModel model, string count) =>
        GenericFormatterReuse.Block(model, write: true) is { } block ? block + ".BlockWidth(" + count + ")" : model.Tagged ? "(" + count + " == 0 ? 0 : checked(1 + " + count + " * " + Tag(model) + ".Width))" : "checked(" + count + " * " + model.WireSize.ToString(CultureInfo.InvariantCulture) + ")";
    /// <summary>Emits mirrors and offset descriptors once inside the owning roots container.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    internal static void Support(CodeWriter w, FormatterStorageModel model)
    {
        // A shared owner's laid-out element keeps its descriptors in its own formatter owner.
        if (GenericFormatterReuse.IsBlockPlaceholder(model))
            return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Emit(model);
        void Emit(FormatterStorageModel current)
        {
            if (!current.Aggregate || !seen.Add(current.Type))
                return;
            foreach (FormatterStorageField field in current.Fields)
                Emit(field.Storage);
            if (!current.Direct && current.NullablePayload == null)
            {
                if (current.Attribute != null)
                    w.Line(current.Attribute);
                w.Line("private struct " + View(current));
                w.Open();
                for (int i = 0; i < current.Fields.Length; i++)
                {
                    FormatterStorageField field = current.Fields[i];
                    if (field.Offset >= 0)
                        w.Line("[global::System.Runtime.InteropServices.FieldOffset(" + field.Offset.ToString(CultureInfo.InvariantCulture) + ")]");
                    w.Line("internal " + (field.Volatile ? "volatile " : "") + field.Storage.Type + " F" + i + ";");
                }

                w.Close();
                w.Blank();
            }

            if (current.Volatile)
                return;
            string inline = w.MethodImpl("AggressiveInlining");
            string layoutContract = current.NullablePayload != null ? "ILuminNullableStorageLayout" :
                current.Tagged ? "ILuminStorageLayout" : "ILuminPortableLayout";
            w.Line("private readonly struct " + Layout(current) + " : global::LuminPack.Utility." + layoutContract);
            w.Open();
            w.Line("public int WireSize { " + inline + " get => " + current.WireSize.ToString(CultureInfo.InvariantCulture) + "; }");
            w.Line("public void Describe(global::System.Span<int> storageOffsets)");
            w.Open();
            w.Line(current.Type + " probe = default;");
            w.Line("ref byte origin = ref " + Unsafe + ".As<" + current.Type + ", byte>(ref probe);");
            w.Line("int next = 0;");
            int local = 0;
            Visit(w, current, "probe", ref local, (leaf, expression, _) =>
            {
                if (leaf.WireSize == 0)
                    return;
                w.Line("int offset" + local + " = (int)" + Unsafe + ".ByteOffset(ref origin, ref " + Unsafe + ".As<" + leaf.Type + ", byte>(ref " + expression + "));");
                w.Open();
                w.Line("int i = 0;");
                w.Line("do");
                w.Indented(() => w.Line("storageOffsets[next++] = offset" + local + " + i;"));
                w.Line("while (++i < " + leaf.WireSize.ToString(CultureInfo.InvariantCulture) + ");");
                w.Close();
            });
            w.Close();
            if (current.Tagged)
            {
                w.Line("public int ModelCount { " + inline + " get => " + current.ModelSizes.Length.ToString(CultureInfo.InvariantCulture) + "; }");
                w.Line("public int ModelSize(int tag)");
                w.Open();
                for (int i = 0; i < current.ModelSizes.Length; i++)
                    w.Line("if (tag == " + (i + 1) + ") return " + current.ModelSizes[i] + ";");
                w.Line("return 0;");
                w.Close();
                w.Line("public void DescribeModel(int tag, global::System.Span<int> storageOffsets)");
                w.Open();
                for (int i = 0; i < current.ModelOffsets.Length; i++)
                {
                    w.Line("if (tag == " + (i + 1) + ")");
                    w.Open();
                    int[] offsets = current.ModelOffsets[i];
                    for (int j = 0; j < offsets.Length;)
                    {
                        int start = j++;
                        while (j < offsets.Length && offsets[j] == offsets[j - 1] + 1)
                            j++;
                        w.Open();
                        w.Line("int i = 0;");
                        w.Line("do");
                        w.Indented(() => w.Line("storageOffsets[" + start + " + i] = " + offsets[start] + " + i;"));
                        w.Line("while (++i < " + (j - start) + ");");
                        w.Close();
                    }

                    w.Line("return;");
                    w.Close();
                }

                w.Close();
                if (current.NullablePayload != null)
                    DescribeModelByte(w, current);
            }

            w.Close();
            w.Blank();
        }
    }

    /// <summary>Emits a single modeled byte lookup for validation of raw nullable layout fields.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="model">The nullable's verified storage descriptor and known wire models.</param>
    private static void DescribeModelByte(CodeWriter w, FormatterStorageModel model)
    {
        w.Line("public int ModelByteOffset(int tag, int dataByte)");
        w.Open();
        for (int index = 0; index < model.ModelOffsets.Length; index++)
        {
            w.Line("if (tag == " + (index + 1) + ")");
            w.Open();
            int[] offsets = model.ModelOffsets[index];
            for (int end = 0; end < offsets.Length;)
            {
                int start = end++;
                while (end < offsets.Length && offsets[end] == offsets[end - 1] + 1)
                    end++;
                string current = "dataByte - " + start;
                w.Line("if ((uint)(" + current + ") < " + (end - start) + "u)");
                w.Indented(() => w.Line("return " + offsets[start] + " + (" + current + ");"));
            }
            w.Line("return -1;");
            w.Close();
        }
        w.Line("return -1;");
        w.Close();
    }

    /// <summary>Transfers a single value using its packed wire, including when its collections use a layout tag.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "expression">The addressable value expression.</param>
    /// <param name = "write">Whether to emit the write operation.</param>
    internal static void Value(CodeWriter w, FormatterStorageModel model, string expression, bool write)
    {
        string state = write ? "writer" : "reader";
        if (write)
            w.Line("writer.EnsureAdditionalCapacity(" + model.WireSize + ");");
        else
            w.Line("reader.EnsureReadable(" + model.WireSize + ");");
        w.Line("ref byte wire = ref " + state + ".Cursor;");
        Transfer(w, model, expression, "wire", write);
        w.Line(state + ".Advance(" + model.WireSize + ");");
    }

    /// <summary>Validates a complete contiguous payload before a destination allocation or mutation.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "count">The count used by this operation.</param>
    /// <param name = "reader">The reader used by this operation.</param>
    internal static void Validate(CodeWriter w, FormatterStorageModel model, string count, string reader = "reader")
    {
        if (GenericFormatterReuse.Block(model, write: false) is { } block)
        {
            w.Line(block + ".ValidateBlock(ref " + reader + ", " + count + ");");
            return;
        }

        if (!model.Tagged)
        {
            w.Line(reader + ".EnsureCollectionPayload(" + reader + ".Position, checked(" + count + " * " + model.WireSize + "));");
            return;
        }

        w.Line("if (" + count + " != 0)");
        w.Open();
        w.Line(reader + ".EnsureCollectionPayload(" + reader + ".Position, 1);");
        w.Line("int storageWidth = " + Tag(model) + ".WidthOf(" + reader + ".Cursor);");
        w.Line(reader + ".EnsureCollectionPayload(" + reader + ".Position + 1, checked(" + count + " * storageWidth));");
        w.Close();
    }

    /// <summary>Transfers a validated contiguous element block with the original vectorized prefix and scalar tail.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "first">The first used by this operation.</param>
    /// <param name = "count">The count used by this operation.</param>
    /// <param name = "write">Whether to emit the write operation.</param>
    /// <param name = "state">The operation-state variable name.</param>
    internal static void Payload(CodeWriter w, FormatterStorageModel model, string first, string count, bool write, string state = "reader")
    {
        if (write && state == "reader")
            state = "writer";
        // A shared owner transfers a laid-out element block through the element's formatter owner.
        if (GenericFormatterReuse.Block(model, write) is { } block)
        {
            w.Line(block + (write ? ".WriteBlock(ref " : ".ReadBlock(ref ") + state + ", ref " + first + ", " + count + ");");
            return;
        }

        w.Line("ref " + model.Type + " storageFirst = ref " + first + ";");
        w.Line("ref byte storageWire = ref " + state + ".Cursor;");
        if (model.Tagged)
        {
            w.Line("if (" + count + " != 0)");
            w.Open();
            if (write)
            {
                w.Line("storageWire = " + Tag(model) + ".Tag;");
                w.Line(Tag(model) + ".Pack(ref storageFirst, " + count + ", ref " + Unsafe + ".Add(ref storageWire, 1));");
                w.Line(state + ".Advance(" + Width(model, count) + ");");
            }
            else
            {
                w.Line("byte storageTag = storageWire;");
                w.Line("int storageWidth = " + Tag(model) + ".WidthOf(storageTag);");
                w.Line(Tag(model) + ".Unpack(storageTag, ref " + Unsafe + ".Add(ref storageWire, 1), " + count + ", ref storageFirst);");
                w.Line(state + ".Advance(checked(1 + " + count + " * storageWidth));");
            }

            w.Close();
            return;
        }

        string helper = "global::LuminPack.Utility.LuminPortable<" + model.Type + ", " + Layout(model) + ">";
        w.Line("int storageStart = " + (model.Volatile ? "0;" : write ? helper + ".Pack(ref storageFirst, " + count + ", ref storageWire);" : helper + ".Unpack(ref storageWire, " + count + ", ref storageFirst);"));
        w.Line("storageWire = ref " + Unsafe + ".Add(ref storageWire, (nint)storageStart * " + model.WireSize + ");");
        w.Line("if (storageStart < " + count + ")");
        w.Open();
        w.Line("int storageIndex = storageStart;");
        w.Line("do");
        w.Open();
        Transfer(w, model, Unsafe + ".Add(ref storageFirst, (nint)(uint)storageIndex)", "storageWire", write);
        w.Line("storageWire = ref " + Unsafe + ".Add(ref storageWire, " + model.WireSize + ");");
        w.Close();
        w.Line("while (++storageIndex < " + count + ");");
        w.Close();
        w.Line(state + ".Advance(checked(" + count + " * " + model.WireSize + "));");
    }

    /// <summary>Transfers packed scalar leaves through the architecture-aware runtime stores.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "expression">The addressable value expression.</param>
    /// <param name = "wire">The wire used by this operation.</param>
    /// <param name = "write">Whether to emit the write operation.</param>
    internal static void Transfer(CodeWriter w, FormatterStorageModel model, string expression, string wire, bool write)
    {
        int position = 0, local = 0;
        Visit(w, model, expression, ref local, (leaf, access, isVolatile) =>
        {
            string address = Unsafe + ".Add(ref " + wire + ", " + position + ")";
            if (write)
            {
                if (isVolatile)
                {
                    w.Line("var snapshot" + position + " = " + access + ";");
                    access = "snapshot" + position;
                }

                w.Line("global::LuminPack.Core.LuminPackWriter.Write(ref " + address + ", " + access + ");");
            }
            else
                w.Line(access + " = global::LuminPack.Core.LuminPackReader.Read<" + leaf.Type + ">(ref " + address + ");");
            position += leaf.WireSize;
        });
    }

    /// <summary>Visits fields through ABI mirrors while keeping tuple component addresses and volatile facts.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "expression">The addressable value expression.</param>
    /// <param name = "local">The local used by this operation.</param>
    /// <param name = "leaf">The leaf used by this operation.</param>
    /// <param name = "isVolatile">The is volatile used by this operation.</param>
    private static void Visit(CodeWriter w, FormatterStorageModel model, string expression, ref int local, Action<FormatterStorageModel, string, bool> leaf, bool isVolatile = false)
    {
        if (!model.Aggregate)
        {
            leaf(model, expression, isVolatile);
            local++;
            return;
        }

        if (model.NullablePayload is { } payload)
        {
            leaf(model.Fields[0].Storage, Unsafe + ".As<" + model.Type + ", bool>(ref " + expression + ")", isVolatile);
            local++;
            string value = "nullablePayload" + local++;
            w.Line("ref " + payload + " " + value + " = ref " + Unsafe +
                ".AsRef(in global::System.Nullable.GetValueRefOrDefaultRef(in " + expression + "));");
            Visit(w, model.Fields[1].Storage, value, ref local, leaf, isVolatile);
            return;
        }

        string storage = expression;
        if (!model.Direct)
        {
            storage = "storageView" + local++;
            w.Line("ref var " + storage + " = ref " + Marshal + ".As<" + model.Type + ", " + View(model) + ">(ref " + Unsafe + ".AsRef(in " + expression + "));");
        }

        for (int i = 0; i < model.Fields.Length; i++)
        {
            FormatterStorageField field = model.Fields[i];
            Visit(w, field.Storage, storage + "." + (model.Direct ? field.Name : "F" + i), ref local, leaf, isVolatile || field.Volatile);
        }
    }
}
