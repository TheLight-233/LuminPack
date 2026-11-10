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
        // Every method of a generic definition repeats its type parameters first, then the operation's profile.
        string Arguments(params string[] extra) => model.TypeArguments(extra);
        string TypeConstraint() => model.TypeConstraintText;
        string ConstraintLine(bool profile) => profile
            ? (TypeConstraint().Length == 0 ? Constraint() : TypeConstraint() + " " + Constraint())
            : TypeConstraint();
        string OptionsConstraint() => TypeConstraint().Length == 0
            ? "where TOptions : struct, " + Options() : TypeConstraint() + " where TOptions : struct, " + Options();
        string RootArguments(bool profile) => profile ? Arguments(StructProfile) : Arguments();
        string Root(string name, bool profile) => name + RootArguments(profile);
        string Invoke(string name, string profile) => name + (profile == null ? Arguments() : Arguments(profile));
        string writeRoot = Root("RootWrite", genericWrite);
        string readRoot = Root("RootRead", genericRead);
        string writeUnion = Root("RootWriteUnion", genericWrite);
        string readUnion = Root("RootReadUnion", genericRead);
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
        string serialize = "internal static void Serialize" + suffix + Arguments() + "(scoped ref " + writer + " writer, scoped in " + type + " input)";
        string serializeCall = container + "." + Invoke("RootWrite", genericWrite ? Standard() : null) + "(ref writer, in input);";
        if (TypeConstraint().Length == 0)
            w.Line(serialize + " => " + serializeCall);
        else
        {
            w.Line(serialize);
            w.Indented(() => w.Line(ConstraintLine(false) + " => " + serializeCall));
        }
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize" + suffix + Arguments(StructProfile) + "(scoped ref " + writer + " writer, scoped in " + type + " input)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + container + "." + writeRoot + "(ref writer, in input);"));
        w.Suppressed = false;
        w.Blank();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        var write = MirroredStruct(model, w, WriteBody(model, w, union: false, structValue: true));
        Method(w, write, "RootWrite", genericWrite, RootArguments(genericWrite), ConstraintLine(true),
            "(scoped ref " + writer + " writer, scoped in " + type + " input)", WriteHint(model, write, union: false, inline));
        w.Blank();
        w.Suppressed = !reads;
        Method(w, StructReadBody(model, w, union: false), "RootRead", genericRead, RootArguments(genericRead), ConstraintLine(true),
            "(scoped ref " + reader + " reader, scoped ref " + type + " value)", ReadHint(model, inline));
        w.Blank();
        w.Suppressed = !writes;
        var unionBody = MirroredStruct(model, w, WriteBody(model, w, union: true, structValue: true));
        Method(w, unionBody, "RootWriteUnion", genericWrite, RootArguments(genericWrite), ConstraintLine(true),
            "(scoped ref " + writer + " writer, scoped in " + type + " input)", WriteHint(model, unionBody, union: true, inline));
        w.Blank();
        w.Suppressed = !reads;
        Method(w, StructReadBody(model, w, union: true), "RootReadUnion", genericRead, RootArguments(genericRead), ConstraintLine(true),
            "(scoped ref " + reader + " reader, scoped ref " + type + " value)", ReadHint(model, inline));
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        string deserialize = "internal static void Deserialize" + suffix + Arguments() + "(scoped ref " + reader + " reader, scoped ref " + type + " value)";
        string deserializeCall = container + "." + Invoke("RootRead", genericRead ? Standard() : null) + "(ref reader, ref value);";
        if (TypeConstraint().Length == 0)
            w.Line(deserialize + " => " + deserializeCall);
        else
        {
            w.Line(deserialize);
            w.Indented(() => w.Line(ConstraintLine(false) + " => " + deserializeCall));
        }
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize" + suffix + Arguments(StructProfile) + "(scoped ref " + reader + " reader, scoped ref " + type + " value)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + container + "." + readRoot + "(ref reader, ref value);"));
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        string serializeUnion = "internal static void SerializeUnion" + Arguments() + "(scoped ref " + writer + " writer, scoped in " + type + " input)";
        string serializeUnionCall = genericWrite ? "SerializeUnion" + Arguments(Standard()) + "(ref writer, in input);" :
            container + ".RootWriteUnion" + Arguments() + "(ref writer, in input);";
        if (TypeConstraint().Length == 0)
            w.Line(serializeUnion + " => " + serializeUnionCall);
        else
        {
            w.Line(serializeUnion);
            w.Indented(() => w.Line(TypeConstraint() + " => " + serializeUnionCall));
        }
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeUnion" + Arguments(StructProfile) + "(scoped ref " + writer + " writer, scoped in " + type + " input)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + (genericWrite ? container + "." + writeUnion + "(ref writer, in input);" :
            "SerializeUnion" + Arguments() + "(ref writer, in input);")));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        string deserializeUnion = "internal static void DeserializeUnion" + suffix + Arguments() + "(scoped ref " + reader + " reader, scoped ref " + type + " value)";
        string deserializeUnionCall = genericRead ? "DeserializeUnion" + suffix + Arguments(Standard()) + "(ref reader, ref value);" :
            container + ".RootReadUnion" + Arguments() + "(ref reader, ref value);";
        if (TypeConstraint().Length == 0)
            w.Line(deserializeUnion + " => " + deserializeUnionCall);
        else
        {
            w.Line(deserializeUnion);
            w.Indented(() => w.Line(TypeConstraint() + " => " + deserializeUnionCall));
        }
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeUnion" + suffix + Arguments(StructProfile) + "(scoped ref " + reader + " reader, scoped ref " + type + " value)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + (genericRead ? container + "." + readUnion + "(ref reader, ref value);" :
            "DeserializeUnion" + suffix + Arguments() + "(ref reader, ref value);")));
        w.Suppressed = false;
        if (model.Mirror != null)
        {
            w.Blank();
            w.Line(w.Preserve);
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line("private struct " + model.Mirror);
            w.Open();
            foreach (string field in model.MirrorFields ?? Array.Empty<string>())
                w.Line("internal " + field + ";");
            w.Close();
        }
        w.Close();
        w.Blank();

        // A managed struct reads through an isolated copy of the reader state, so a failure leaves the caller's state untouched.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write" + Arguments() + "(this ref " + writer + " state, in " + type + " value) => " + Serializer() + "." + container + "." +
            Invoke("RootWrite", genericWrite ? Standard() : null) + "(ref state, in value);");
        w.Line(inline);
        w.Line("internal static void Write" + Arguments("TOptions") + "(this ref " + writer + " state, in " + type + " value)");
        w.Indented(() => w.Line(OptionsConstraint() + " => " + Serializer() + "." + container + "." +
            Invoke("RootWrite", genericWrite ? "TOptions" : null) + "(ref state, in value);"));
        w.Suppressed = !reads;
        IsolatedRead(w, "internal static void Read" + Arguments() + "(this ref " + reader + " state, ref " + type + " value)", null,
            container + "." + Invoke("RootRead", genericRead ? Standard() : null), inline, Serializer());
        w.Blank();
        IsolatedRead(w, "internal static void Read" + Arguments("TOptions") + "(this ref " + reader + " state, ref " + type + " value)", OptionsConstraint(),
            container + "." + Invoke("RootRead", genericRead ? "TOptions" : null), inline, Serializer());
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
        w.Line("public static void WritePolymorphismValue" + Arguments() + "(ref this " + writer + " writer, scoped in " + type + " input) => " + Serializer() +
            ".SerializeUnion" + Arguments() + "(ref writer, input);");
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        w.Line("public static void ReadPolymorphismValue" + Arguments() + "(ref this " + reader + " reader, scoped ref " + type + " value) => " + Serializer() +
            ".DeserializeUnion" + suffix + Arguments() + "(ref reader, ref value);");
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
            DirectMemberKind.Array or DirectMemberKind.List || member.Kind == DirectMemberKind.Parameter).ToArray();
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
            var checks = referenced.Select(member => member.Kind == DirectMemberKind.Parameter && member.BareParameter && !member.ValueTypeParameter
                ? "(!typeof(" + member.Scalar + ").IsValueType && (object? )" + (member.Mirrored
                    ? "LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value)." : "value.") +
                    member.Name + " is not null)"
                : (member.Mirrored
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
                body.Add(member.Kind == DirectMemberKind.Parameter
                    ? w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, StructProfile) + "(ref reader, ref " + Camel(member.Name) + "Temp!);"
                    : Camel(member.Name) + "Temp = " + FixedOrTextRead(member, "reader") + ";");
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
                if (member.Kind == DirectMemberKind.Parameter)
                    body.Add(w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, StructProfile) + "(ref reader, ref " +
                        (member.Mirrored ? Camel(member.Name) + "Temp!" : "result." + member.Name + "!") + ");");
                else if (member.Mirrored)
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
        if (member.Kind == DirectMemberKind.Parameter)
        {
            AddParameterReset(body, member, w, "reader2", owner + "." + member.Name);
            body.Add(w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, StructProfile) + "(ref reader2, ref " + owner + "." + member.Name + "!);");
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
    /// <param name="w">The file writer.</param>
    /// <param name="body">The lowered statement text.</param>
    /// <param name="name">The root method name.</param>
    /// <param name="generic">Whether the root takes the operation's profile.</param>
    /// <param name="arguments">The angle-bracketed type-argument list of the root.</param>
    /// <param name="constraint">The constraint clauses of the root, or an empty string.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <param name="attribute">The inlining attribute, or null.</param>
    private static void Method(DirectWriter w, List<string> body, string name, bool generic, string arguments, string constraint,
        string parameters, string attribute)
    {
        w.Line(w.Preserve);
        if (attribute != null)
            w.Line(attribute);
        w.Line("internal static void " + name + arguments + parameters);
        if (generic)
            w.Indented(() => w.Line(constraint));
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
    private static void IsolatedRead(DirectWriter w, string signature, string constraint, string root, string inline, string serializer)
    {
        w.Line(inline);
        w.Line(signature);
        if (constraint != null)
            w.Indented(() => w.Line(constraint));
        w.Open();
        w.Line("LuminPackReader isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + root + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
    }
}
