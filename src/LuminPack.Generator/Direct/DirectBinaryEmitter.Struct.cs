using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using LuminPack.Code.Core;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a managed struct's binary roots in the record form the original pipeline publishes.</summary>
/// <remarks>A struct never writes or reads a null marker. Its root write reserves the record exactly as a class record does,
/// while its root read consumes the member-count byte, reuses an existing value whose reference members are set, and otherwise
/// decodes into a fresh instance or its selected constructor. Mirrored storage, including private included fields, is reached
/// through the root-private layout mirror.</remarks>
internal static partial class DirectBinaryEmitter
{
    private const string StructProfile = "TLuminProfile";

    /// <summary>Emits the complete binary file body of a managed struct.</summary>
    /// <param name="model">The managed struct.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitStruct(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        var members = model.Members;
        bool genericWrite = model.ProfilePayload || model.InlineArray;
        bool genericRead = model.ProfilePayload || model.InlineArray || model.InlineList ||
            members.Any(member => member.Kind is not (DirectMemberKind.Fixed or DirectMemberKind.Object));
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string Options() => w.A("ILuminPackOptions");
        string Serializer() => w.A("LuminPackSerializer");
        string Standard() => w.A("LuminPackUtf16LengthOptions");
        string Constraint() => "where TLuminProfile : struct, " + Options();
        string writeRoot = genericWrite ? "RootWrite<" + StructProfile + ">" : "RootWrite";
        string readRoot = genericRead ? "RootRead<" + StructProfile + ">" : "RootRead";
        string writeUnion = genericWrite ? "RootWriteUnion<" + StructProfile + ">" : "RootWriteUnion";
        string readUnion = genericRead ? "RootReadUnion<" + StructProfile + ">" : "RootReadUnion";
        string inline = w.MethodImpl("AggressiveInlining");
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        string polymorphic = "[" + w.Attribute("MethodImpl") + "(MethodImplOptions.AggressiveInlining)]";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize" + suffix + "(scoped ref " + writer + " writer, scoped in " + type + " input) => " + container + "." +
            (genericWrite ? "RootWrite<" + Standard() + ">" : "RootWrite") + "(ref writer, in input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize" + suffix + "<" + StructProfile + ">(scoped ref " + writer + " writer, scoped in " + type + " input)");
        w.Indented(() => w.Line(Constraint() + " => " + container + "." + writeRoot + "(ref writer, in input);"));
        w.Suppressed = false;
        w.Blank();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        var write = MirroredStruct(model, w, WriteBody(model, w, union: false, structValue: true));
        Method(w, write, "RootWrite", genericWrite, "(scoped ref " + writer + " writer, scoped in " + type + " input)",
            WriteHint(model, write, union: false, inline));
        w.Blank();
        w.Suppressed = !reads;
        Method(w, StructReadBody(model, w, union: false), "RootRead", genericRead, "(scoped ref " + reader + " reader, scoped ref " + type + " value)",
            ReadHint(model, inline));
        w.Blank();
        w.Suppressed = !writes;
        var unionBody = MirroredStruct(model, w, WriteBody(model, w, union: true, structValue: true));
        Method(w, unionBody, "RootWriteUnion", genericWrite, "(scoped ref " + writer + " writer, scoped in " + type + " input)",
            WriteHint(model, unionBody, union: true, inline));
        w.Blank();
        w.Suppressed = !reads;
        Method(w, StructReadBody(model, w, union: true), "RootReadUnion", genericRead,
            "(scoped ref " + reader + " reader, scoped ref " + type + " value)", ReadHint(model, inline));
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize" + suffix + "(scoped ref " + reader + " reader, scoped ref " + type + " value) => " + container + "." +
            (genericRead ? "RootRead<" + Standard() + ">" : "RootRead") + "(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize" + suffix + "<" + StructProfile + ">(scoped ref " + reader + " reader, scoped ref " + type + " value)");
        w.Indented(() => w.Line(Constraint() + " => " + container + "." + readRoot + "(ref reader, ref value);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeUnion(scoped ref " + writer + " writer, scoped in " + type + " input) => " +
            (genericWrite ? "SerializeUnion<" + Standard() + ">(ref writer, in input);" : container + ".RootWriteUnion(ref writer, in input);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeUnion<" + StructProfile + ">(scoped ref " + writer + " writer, scoped in " + type + " input)");
        w.Indented(() => w.Line(Constraint() + " => " + (genericWrite ? container + "." + writeUnion + "(ref writer, in input);" : "SerializeUnion(ref writer, in input);")));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "(scoped ref " + reader + " reader, scoped ref " + type + " value) => " +
            (genericRead ? "DeserializeUnion" + suffix + "<" + Standard() + ">(ref reader, ref value);" : container + ".RootReadUnion(ref reader, ref value);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + "<" + StructProfile + ">(scoped ref " + reader + " reader, scoped ref " + type + " value)");
        w.Indented(() => w.Line(Constraint() + " => " + (genericRead ? container + "." + readUnion + "(ref reader, ref value);" :
            "DeserializeUnion" + suffix + "(ref reader, ref value);")));
        w.Suppressed = false;
        if (model.Mirror != null)
        {
            w.Blank();
            w.Line(w.Preserve);
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line("private struct " + model.Mirror);
            w.Open();
            MirrorFields(w, model.MirrorFields ?? Array.Empty<string>(), model.MirrorOffsets);
            w.Close();
        }
        w.Close();
        w.Blank();

        // A managed struct reads through an isolated copy of the reader state, so a failure leaves the caller's state untouched.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + " value) => " + Serializer() + "." + container + "." +
            (genericWrite ? "RootWrite<" + Standard() + ">" : "RootWrite") + "(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + " value)");
        w.Indented(() => w.Line("where TOptions : struct, " + Options() + " => " + Serializer() + "." + container + "." +
            (genericWrite ? "RootWrite<TOptions>" : "RootWrite") + "(ref state, in value);"));
        w.Suppressed = !reads;
        IsolatedRead(w, "internal static void Read(this ref " + reader + " state, ref " + type + " value)", null, genericRead ? Standard() : null,
            container, inline, Serializer());
        w.Blank();
        IsolatedRead(w, "internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + " value)", "where TOptions : struct, " + Options(),
            genericRead ? "TOptions" : null, container, inline, Serializer());
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
        w.Line(polymorphic);
        w.Line("public static void WritePolymorphismValue(ref this " + writer + " writer, scoped in " + type + " input) => " + Serializer() + ".SerializeUnion(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        w.Line("public static void ReadPolymorphismValue(ref this " + reader + " reader, scoped ref " + type + " value) => " + Serializer() +
            ".DeserializeUnion" + suffix + "(ref reader, ref value);");
        w.Suppressed = false;
        w.Close();
        w.Close();
    }

    /// <summary>Writes a managed struct's read root: the header byte, existing-reference reuse, and the fresh construction.</summary>
    /// <param name="model">The managed struct.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="union">Whether this is the Union payload without the member-count byte.</param>
    /// <returns>The read body lines.</returns>
    private static List<string> StructReadBody(DirectTypeModel model, DirectWriter w, bool union)
    {
        var members = model.Members;
        var body = new List<string>();
        string Policy() => w.A("LuminPackInputPolicy");
        bool constructed = model.ConstructorArguments != null;
        var referenced = members.Where(member => member.Kind is DirectMemberKind.Text or DirectMemberKind.Object or
            DirectMemberKind.Array or DirectMemberKind.List).ToArray();
        var mirrored = members.Where(member => member.Mirrored).ToArray();
        var temps = (constructed ? members : mirrored.Where(member => model.ConstructorMembers == null ||
            !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (temps.Length != 0)
        {
            body.Add("// Declare locals to hold the field values.");
            var declared = model.DeclaredOrder == null ? temps : model.DeclaredOrder
                .Select(name => Array.Find(temps, member => member.Name == name)).Where(member => member != null).ToArray();
            foreach (var member in declared)
                body.Add(TempType(member) + " " + Camel(member.Name) + "Temp = default !;");
        }
        if (!union)
        {
            body.Add("reader.EnsureReadable(1);");
            body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, 1);");
        }
        if (referenced.Length != 0)
        {
            var checks = referenced.Select(member => (member.Mirrored
                ? "LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value)." + member.Name
                : "value." + member.Name) + " is not null");
            body.Add("if (" + string.Join(" || ", checks) + ")");
            body.Add("{");
            body.Add("ReadExisting(ref reader, ref value);");
            body.Add("return;");
            body.Add("}");
            body.Add("");
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void ReadExisting(ref " + w.A("LuminPackReader") + " reader2, ref " + model.GlobalName + " value2)");
            body.Add("{");
            if (model.Mirror != null && mirrored.Length != 0)
                body.Add("ref var reuseLocal = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value2!);");
            for (int index = 0; index < members.Length; index++)
                AddStructExistingRead(body, members[index], index + 1 < members.Length, w);
            body.Add("}");
            body.Add("");
        }
        if (constructed)
        {
            foreach (var member in members)
                body.Add(Camel(member.Name) + "Temp = " + FixedOrTextRead(member, "reader") + ";");
            body.Add("// Construct the object and populate its members.");
            string arguments = string.Join(", ", model.ConstructorArguments.Select(name =>
                name == null ? "default" : Camel(name) + "Temp!"));
            string construction = "value = new " + model.GlobalName + "(" + arguments + ")";
            var initialized = members.Where(member => !member.Mirrored &&
                (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
            if (initialized.Length == 0)
                body.Add(construction + ";");
            else
            {
                body.Add(construction);
                body.Add("{");
                foreach (var member in initialized)
                    body.Add(member.Name + " = " + Camel(member.Name) + "Temp!,");
                body.Add("};");
            }
            AddStructMirrorAssignments(body, model, w, mirrored.Where(member => model.ConstructorMembers == null ||
                !model.ConstructorMembers.Contains(member.Name)).ToArray());
        }
        else
        {
            body.Add("var result = new " + model.GlobalName + "();");
            foreach (var member in members)
            {
                if (member.Mirrored)
                    body.Add(Camel(member.Name) + "Temp = " + FixedOrTextRead(member, "reader") + ";");
                else
                    body.Add("result." + member.Name + " = " + FixedOrTextRead(member, "reader") + ";");
            }
            body.Add("// Construct the object and populate its members.");
            body.Add("value = result;");
            AddStructMirrorAssignments(body, model, w, mirrored);
        }
        return body;
    }

    /// <summary>Reads one member in place for a managed struct's existing-instance path.</summary>
    private static void AddStructExistingRead(List<string> body, DirectMember member, bool hasNext, DirectWriter w)
    {
        string owner = member.Mirrored ? "reuseLocal" : "value2!";
        if (member.Kind == DirectMemberKind.Text && member.Mirrored && member.Property)
        {
            body.Add("var currentString = " + owner + "." + member.Name + ";");
            body.Add("var decodedString = reader2.Read<" + StructProfile + ">(currentString);");
            body.Add("if (!object.ReferenceEquals(currentString, decodedString))");
            body.Add("\t" + owner + "." + member.Name + " = decodedString!;");
            if (hasNext)
                body.Add("");
            return;
        }
        if (member.Kind == DirectMemberKind.Text)
        {
            body.Add("reader2.Read<" + StructProfile + ">(ref " + owner + "." + member.Name + ");");
            return;
        }
        body.Add(owner + "." + member.Name + " = " + FixedOrTextRead(member, "reader2") + ";");
    }

    /// <summary>Assigns the mirrored members of a fresh struct through its layout mirror.</summary>
    private static void AddStructMirrorAssignments(List<string> body, DirectTypeModel model, DirectWriter w, DirectMember[] members)
    {
        if (model.Mirror == null || members.Length == 0)
            return;
        body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value);");
        foreach (var member in members)
            body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
    }

    /// <summary>Reaches a mirrored struct's storage through its layout mirror.</summary>
    private static List<string> MirroredStruct(DirectTypeModel model, DirectWriter w, List<string> body)
    {
        if (model.Mirror == null || !model.Members.Any(member => member.Mirrored))
            return body;
        string pattern = @"\bvalue\.(" + string.Join("|", model.Members.Where(member => member.Mirrored)
            .Select(member => Regex.Escape(member.Name))) + @")\b";
        var result = new List<string>(body.Count + 1);
        foreach (string line in body)
            result.Add(Regex.Replace(line, pattern, "local.$1"));
        result.Insert(1, "ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref Unsafe.AsRef(in value));");
        return result;
    }

    /// <summary>Whether a managed struct's write root keeps its aggressive inlining hint.</summary>
    private static string WriteHint(DirectTypeModel model, List<string> body, bool union, string inline)
        => model.StreamRecord || model.MixedRecord
            ? (DirectInlining.MixedWrite(model, union) ? inline : null)
            : (DirectInlining.Write(model, union, Sizes(body)) ? inline : null);

    /// <summary>Whether a managed struct's read root keeps its aggressive inlining hint; an inline object or collection
    /// member keeps its larger expanded body out of line.</summary>
    private static string ReadHint(DirectTypeModel model, string inline)
        => model.Members.Any(member => member.Kind is DirectMemberKind.Object or DirectMemberKind.Array or DirectMemberKind.List)
            ? null
            : (DirectInlining.Read(model, 0) ? inline : null);

    /// <summary>Emits one root method from body lines, with the struct's non-nullable value parameter.</summary>
    private static void Method(DirectWriter w, List<string> body, string name, bool generic, string parameters, string attribute)
    {
        w.Line(w.Preserve);
        if (attribute != null)
            w.Line(attribute);
        w.Line("internal static void " + name + (generic ? "<" + StructProfile + ">" : string.Empty) + parameters);
        if (generic)
            w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        for (int index = 0; index < body.Count; index++)
        {
            string line = body[index];
            if (line.Length == 0)
            {
                if (index + 1 < body.Count && body[index + 1] == "{")
                    w.HardBlank();
                else
                    w.Blank();
            }
            else if (line == "}")
                w.Close();
            else if (line == "};")
                w.Close(";");
            else if (line == "{")
                w.Open();
            else if (line.StartsWith("\t", StringComparison.Ordinal))
                w.Indented(() => w.Line(line.Substring(1)));
            else
                w.Line(line);
        }
        w.Close();
    }

    /// <summary>Emits an isolated reader-state extension read.</summary>
    private static void IsolatedRead(DirectWriter w, string signature, string constraint, string profile, string container,
        string inline, string serializer)
    {
        w.Line(inline);
        w.Line(signature);
        if (constraint != null)
            w.Indented(() => w.Line(constraint));
        w.Open();
        w.Line("LuminPackReader isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (profile == null ? string.Empty : "<" + profile + ">") + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
    }
}
