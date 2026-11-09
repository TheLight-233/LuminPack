using LuminPack.Code.Core;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a covered struct's binary roots, facade entries and extensions in their final published form.</summary>
/// <remarks>A raw unmanaged struct, whose storage is its wire layout, is written and read as one value: its roots copy the
/// whole struct, the read entries call the reader's raw value read directly, and its width is the struct's size. The entries
/// take the struct by reference and need no profile, so the profiled overloads forward to the plain ones.</remarks>
internal static class DirectStructEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the complete binary file body of a raw unmanaged struct.</summary>
    /// <param name="model">The covered struct.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls; see <see cref="DirectBinaryEmitter.Emit"/>.</param>
    internal static void EmitRaw(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        string inline = w.MethodImpl("AggressiveInlining");
        string Constraint() => "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
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
        Root("RootWriteUnion");
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        Profiled("Serialize" + suffix, "(scoped ref " + writer + " writer, scoped in " + type + " input)", container + ".RootWrite(ref writer, in input);");
        w.Suppressed = !reads;
        Entry("Deserialize" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)", "reader.Read(ref value);");
        Profiled("Deserialize" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)", "Deserialize" + suffix + "(ref reader, ref value);");
        w.Suppressed = !writes;
        Entry("SerializeUnion", "(scoped ref " + writer + " writer, scoped in " + type + " input)", container + ".RootWriteUnion(ref writer, in input);");
        Profiled("SerializeUnion", "(scoped ref " + writer + " writer, scoped in " + type + " input)", "SerializeUnion(ref writer, in input);");
        w.Suppressed = !reads;
        Entry("DeserializeUnion" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)", "reader.Read(ref value);");
        Profiled("DeserializeUnion" + suffix, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            "DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = !writes;
        string evaluator = "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + " value)";
        Entry("MeasureRecord" + suffix, evaluator, "evaluator.Add(" + w.A("Unsafe") + ".SizeOf<" + type + ">());");
        Profiled("MeasureRecord" + suffix, evaluator, "MeasureRecord" + suffix + "(ref evaluator, ref value);");
        w.Suppressed = false;
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + " value) => " + serializer + "." + container + ".RootWrite(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + " value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWrite(ref state, in value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read(this ref " + reader + " state, ref " + type + " value) => " + serializer + ".Deserialize" + suffix + "(ref state, ref value);");
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + " value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + ".Deserialize" + suffix + "(ref state, ref value);"));
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
        w.Line("public static void WritePolymorphismValue(ref this " + writer + " writer, scoped in " + type + " input) => " + serializer + ".SerializeUnion(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("public static void ReadPolymorphismValue(ref this " + reader + " reader, scoped ref " + type + " value) => " + serializer +
            ".DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = false;
        w.Close();
        w.Close();

        void Entry(string name, string parameters, string body)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + parameters + " => " + body);
            w.Blank();
        }

        void Profiled(string name, string parameters, string body)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + "<" + Profile + ">" + parameters);
            w.Indented(() => w.Line(Constraint() + " => " + body));
            w.Blank();
        }

        void Root(string name)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + "(scoped ref " + writer + " writer, scoped in " + type + " input)");
            w.Open();
            w.Line("var value = input;");
            w.Line("writer.Write(in value);");
            w.Close();
        }
    }
}
