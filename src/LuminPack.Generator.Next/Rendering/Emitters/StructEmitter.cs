#nullable disable
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a covered struct's binary roots, facade entries and extensions in their final published form.</summary>
/// <remarks>A raw unmanaged struct, whose storage is its wire layout, is written and read as one value: its roots copy the
/// whole struct, the read entries call the reader's raw value read directly, and its width is the struct's size. A portable
/// struct copies each scalar field at its packed wire offset through a positional storage view, with typed unaligned stores on
/// 64-bit targets and the writer's checked stores otherwise; its reads go through read roots. The entries take the struct by
/// reference and need no profile, so the profiled overloads forward to the plain ones.</remarks>
internal static class StructEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete binary file body of a covered struct.</summary>
    /// <param name="model">The covered struct.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; see <see cref="BinaryEmitter.Emit"/>.</param>
    internal static void Emit(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        // A version-tolerant struct writes a schema prefix and revisions around the struct's non-nullable members.
        if (model.Version != null)
        {
            BinaryEmitter.EmitVersionStruct(model, w, operations);
            return;
        }
        if (model.ManagedStruct)
        {
            BinaryEmitter.EmitStruct(model, w, operations);
            return;
        }
        bool portable = model.View != null;
        int wire = portable ? model.PortableView.WireSize : model.Members.Sum(member => member.Size);
        string width = wire.ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        string inline = w.MethodImpl("AggressiveInlining");
        string Constraint() => ConstraintText("where TLuminProfile : struct, " + w.A("ILuminPackOptions"));
        string ConstraintText(string profile) => model.TypeConstraintText.Length == 0 ? profile : model.TypeConstraintText + " " + profile;
        string serializer = w.A("LuminPackSerializer");

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        w.Suppressed = !writes;
        Entry("Serialize" + suffix, "(scoped ref " + writer + " writer, scoped in " + type + " input)", container + ".RootWrite(ref writer, in input);");
        w.Suppressed = false;
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        Root("RootWrite");
        w.Blank();
        if (portable)
        {
            w.Suppressed = !reads;
            ReadRoot("RootRead");
            w.Blank();
            w.Suppressed = !writes;
        }
        Root("RootWriteUnion");
        if (portable)
        {
            w.Blank();
            w.Suppressed = !reads;
            ReadRoot("RootReadUnion");
        }
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        Profiled("Serialize" + suffix, "(scoped ref " + writer + " writer, scoped in " + type + " input)", container + ".RootWrite(ref writer, in input);");
        w.Suppressed = !reads;
        Entry("Deserialize" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            portable ? container + ".RootRead(ref reader, ref value);" : "reader.Read(ref value);");
        Profiled("Deserialize" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            portable ? container + ".RootRead(ref reader, ref value);" : "Deserialize" + suffix + "(ref reader, ref value);");
        w.Suppressed = !writes;
        Entry("SerializeUnion", "(scoped ref " + writer + " writer, scoped in " + type + " input)", container + ".RootWriteUnion(ref writer, in input);");
        Profiled("SerializeUnion", "(scoped ref " + writer + " writer, scoped in " + type + " input)", "SerializeUnion(ref writer, in input);");
        w.Suppressed = !reads;
        Entry("DeserializeUnion" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            portable ? container + ".RootReadUnion(ref reader, ref value);" : "reader.Read(ref value);");
        Profiled("DeserializeUnion" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            "DeserializeUnion" + suffix + "(ref reader, ref value);");
        // The record-width entry is selected per payload, as the original operation filter does.
        if (model.MeasureRecord)
        {
            w.Suppressed = !writes;
            string evaluator = "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + " value)";
            Entry("MeasureRecord" + suffix, evaluator, "evaluator.Add(" + (portable ? width : w.A("Unsafe") + ".SizeOf<" + type + ">()") + ");");
            Profiled("MeasureRecord" + suffix, evaluator, "MeasureRecord" + suffix + "(ref evaluator, ref value);");
        }
        w.Suppressed = false;
        // A raw or portable struct with auto-property storage declares the mirror its JSON roots reach it through. The
        // positional storage view follows it, and stays declared when the projection named the fields directly.
        if (model.Mirror != null)
        {
            w.Line(w.Preserve);
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line("private struct " + model.Mirror);
            if (model.TypeConstraintText.Length != 0)
                w.Indented(() => w.Line(model.TypeConstraintText));
            w.Open();
            BinaryEmitter.MirrorFields(w, model.MirrorFields ?? System.Array.Empty<string>(), model.MirrorOffsets);
            w.Close();
            if (portable)
                w.Blank();
        }
        if (portable)
        {
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line("private struct " + model.View);
            w.Open();
            foreach (var field in model.PortableView.Fields)
            {
                if (field.Offset >= 0)
                    w.Line("[global::System.Runtime.InteropServices.FieldOffset(" + field.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")]");
                w.Line("internal " + (field.Volatile ? "volatile " : string.Empty) + field.Type + " " + field.Name + ";");
            }
            w.Close();
        }
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        Entry("Write", "(this ref " + writer + " state, in " + type + " value)", serializer + "." + container + ".RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments("TOptions") + "(this ref " + writer + " state, in " + type + " value)");
        w.Indented(() => w.Line(ConstraintText("where TOptions : struct, " + w.A("ILuminPackOptions")) + " => " + serializer + "." + container + ".RootWrite(ref state, in value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        string read = portable ? container + ".RootRead" : "Deserialize" + suffix;
        w.Line("internal static void Read" + model.TypeArguments() + "(this ref " + reader + " state, ref " + type + " value)" +
            (model.TypeConstraintText.Length == 0 ? string.Empty : " " + model.TypeConstraintText) + " => " + serializer + "." + read + "(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments("TOptions") + "(this ref " + reader + " state, ref " + type + " value)");
        w.Indented(() => w.Line(ConstraintText("where TOptions : struct, " + w.A("ILuminPackOptions")) + " => " + serializer + "." + read + "(ref state, ref value);"));
        w.Suppressed = false;
        w.Close();
        w.Close();
        w.Blank();

        w.Line("namespace LuminPack.Generated");
        w.Open();
        w.Line("internal static partial class " + model.Extension);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("public static void WritePolymorphismValue" + model.TypeArguments() + "(ref this " + writer + " writer, scoped in " + type + " input)" +
            (model.TypeConstraintText.Length == 0 ? string.Empty : " " + model.TypeConstraintText) + " => " + serializer + ".SerializeUnion(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("public static void ReadPolymorphismValue" + model.TypeArguments() + "(ref this " + reader + " reader, scoped ref " + type + " value)" +
            (model.TypeConstraintText.Length == 0 ? string.Empty : " " + model.TypeConstraintText) + " => " + serializer +
            ".DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = false;
        w.Close();
        w.Close();

        void Entry(string name, string parameters, string body)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + model.TypeArguments() + parameters +
                (model.TypeConstraintText.Length == 0 ? string.Empty : " " + model.TypeConstraintText) + " => " + body);
            w.Blank();
        }

        void Profiled(string name, string parameters, string body)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + model.TypeArguments(Profile) + parameters);
            w.Indented(() => w.Line(Constraint() + " => " + body));
            w.Blank();
        }

        void Root(string name)
        {
            w.Line(w.Preserve);
            if (!portable || DirectInlining.PortableWrite(model))
                w.Line(inline);
            w.Line("internal static void " + name + model.TypeArguments() + "(scoped ref " + writer + " writer, scoped in " + type + " input)");
            if (model.TypeConstraintText.Length != 0)
                w.Indented(() => w.Line(model.TypeConstraintText));
            w.Open();
            w.Line("var value = input;");
            if (!portable)
            {
                w.Line("writer.Write(in value);");
                w.Close();
                return;
            }
            string unsafeName = w.A("Unsafe");
            w.Line("int portableIndex = writer.Position;");
            w.Line("ref byte portableWire = ref " + unsafeName + ".Add(ref " + w.A("MemoryMarshal") + ".GetReference(writer.Buffer), unchecked((nint)(uint)(portableIndex)));");
            Transfers("portableStorage", true, (address, field) => w.A("LuminPackWriter") + ".Write(ref " + address + ", " + field + ");");
            w.Blank();
            w.Line("writer.Advance(" + width + ");");
            w.Close();
        }

        void ReadRoot(string name)
        {
            w.Line(w.Preserve);
            if (DirectInlining.PortableRead(model))
                w.Line(inline);
            w.Line("internal static void " + name + model.TypeArguments() + "(scoped ref " + reader + " reader, scoped ref " + type + " value)");
            if (model.TypeConstraintText.Length != 0)
                w.Indented(() => w.Line(model.TypeConstraintText));
            w.Open();
            string unsafeName = w.A("Unsafe");
            w.Line("int portableIndex = reader.Position;");
            w.Line("reader.EnsureReadable(portableIndex, " + width + ");");
            w.Line("ref byte portableWire = ref " + unsafeName + ".Add(ref " + w.A("MemoryMarshal") + ".GetReference(reader.Buffer), unchecked((nint)(uint)(portableIndex)));");
            Transfers("portableStorage", false, null);
            w.Line("reader.EnsureReadable(" + width + ");");
            w.Line("reader.Cursor = ref " + unsafeName + ".Add(ref reader.Cursor, " + width + ");");
            w.Close();
        }

        // Copies each scalar at its packed offset, through the positional view unless the projection named the fields directly.
        void Transfers(string view, bool write, System.Func<string, string, string> transfer,
            System.Collections.Generic.HashSet<string> used = null)
        {
            if (!model.ViewElided)
                w.Line("ref var " + view + " = ref LuminPackMarshal.As<" + type + ", " + model.View + ">(ref " + w.A("Unsafe") + ".AsRef(in value));");
            used ??= new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            int offset = 0, index = 0;
            foreach (var leaf in model.PortableView.Leaves)
            {
                string field = model.ViewElided ? "value." + leaf.Access : view + "." + leaf.Access;
                if (write && leaf.Volatile)
                {
                    // A volatile write reads the field once into a snapshot, as a ref to volatile storage is illegal.
                    string stem = "portableField" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "Snapshot";
                    string snapshot = stem;
                    for (int count = 2; !used.Add(snapshot); count++)
                        snapshot = stem + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    w.Line("var " + snapshot + " = " + field + ";");
                    field = snapshot;
                }
                string address = w.A("Unsafe") + ".Add(ref portableWire, " + offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                w.Line(write ? transfer(address, field) : field + " = " + (leaf.Scalar == "bool"
                    ? w.A("LuminPackInputPolicy") + ".ReadBoolean(ref " + address + ");"
                    : w.A("LuminPackReader") + ".Read<" + leaf.Scalar + ">(ref " + address + ");"));
                offset += leaf.Size;
                index++;
            }
        }
    }
}

