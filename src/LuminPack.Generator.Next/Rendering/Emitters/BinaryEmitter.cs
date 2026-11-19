#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using LuminPack.SourceGenerator.Next.Rendering.Formatters;
namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a covered type's binary roots, facade entries and extensions in their final published form.</summary>
/// <remarks>The text is what the original pipeline publishes for these shapes: the cursor record write with one reservation,
/// proven fixed-field blocks with their field-wise fallbacks, checked reads with Boolean batch masks, the root container, the
/// state entries and the payload extensions. Lines longer than the layout width are wrapped by the publication formatter.</remarks>
internal static partial class BinaryEmitter
{
    private const string Profile = "TLuminProfile";
    private const string Constraint = "where TLuminProfile : struct, ILuminPackOptions";

    /// <summary>Spells the fail-closed body of a rejected payload operation.</summary>
    /// <param name="reason">The first rejected shape.</param>
    /// <returns>The statement.</returns>
    internal static string RejectUnsupported(string reason) => "global::LuminPack.Security.LuminPackInputPolicy.RejectUnsupported(" +
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(reason, true) + ");";

    /// <summary>Determines whether the generated binary write root takes a profile type parameter.</summary>
    /// <param name="model">The payload being written.</param>
    /// <returns>True when the write root is generic over the operation profile.</returns>
    private static bool HasWriteProfile(PayloadRenderModel model) =>
        !(model.DeclaredProfile != null && model.MemberWrite) &&
        (model.SequenceBase != null || model.Version != null || model.ProfilePayload || model.InlineArray);

    /// <summary>Determines whether the generated binary read root takes a profile type parameter.</summary>
    /// <param name="model">The payload being read.</param>
    /// <returns>True when the read root is generic over the operation profile.</returns>
    private static bool HasReadProfile(PayloadRenderModel model) =>
        !(model.DeclaredProfile != null && model.MemberWrite) &&
        (model.SequenceBase != null || model.Version != null || model.ProfilePayload || model.InlineArray || model.InlineList ||
            model.Members.Any(member => member.Kind is not (RenderMemberKind.Fixed or RenderMemberKind.Object)));

    /// <summary>Emits the complete binary file body.</summary>
    /// <param name="model">The covered type.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations selected for emission.</param>
    internal static void Emit(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        var members = model.Members;
        // A type-level wire profile fixes every string operation of the record and its named member write, so its roots need
        // no profile parameter at all.
        bool fixedProfile = model.DeclaredProfile != null && model.MemberWrite;
        // Version-tolerant roots always take the operation's profile, and so does a string anywhere in an inline object below.
        bool genericWrite = HasWriteProfile(model);
        bool genericRead = HasReadProfile(model);
        bool allFixed = members.All(member => member.IsFixed);
        string type = model.GlobalName, readable = model.Readable, container = "LuminRoots_" + readable;
        // State parameters are spelled through the imported namespace; the other helper names are qualified until presentation.
        const string writer = "LuminPackWriter", reader = "LuminPackReader";
        string Options() => w.A("ILuminPackOptions");
        string Serializer() => w.A("LuminPackSerializer");
        string Standard() => model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : w.A("LuminPackUtf16LengthOptions");
        string Constraint() => "where TLuminProfile : struct, " + Options();
        // Every method of a generic definition repeats its type parameters first, then the operation's profile.
        string Arguments(params string[] extra) => model.TypeArguments(extra);
        string TypeConstraint() => model.TypeConstraintText;
        string ConstraintLine(bool profile) => profile
            ? (TypeConstraint().Length == 0 ? Constraint() : TypeConstraint() + " " + Constraint())
            : TypeConstraint();
        string OptionsConstraint() => TypeConstraint().Length == 0
            ? "where TOptions : struct, " + Options() : TypeConstraint() + " where TOptions : struct, " + Options();
        string Root(string name, bool profile) => name + (profile ? Arguments(Profile) : Arguments());
        string Invoke(string name, string profile) => name + (profile == null ? Arguments() : Arguments(profile));
        string writeRoot = Root("RootWrite", genericWrite);
        string readRoot = Root("RootRead", genericRead);
        string writeUnion = Root("RootWriteUnion", genericWrite);
        string readUnion = Root("RootReadUnion", genericRead);
        string inline = w.MethodImpl("AggressiveInlining");
        // Interception gives every payload's state entries unique names; otherwise they are overloads.
        string suffix = model.UniqueEntries ? "_" + readable : string.Empty;
        // The Union extension entries spell the inlining option through the imported alias name.
        string polymorphic = "[" + w.Attribute("MethodImpl") + "(MethodImplOptions.AggressiveInlining)]";

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();

        // Serialize entries; the root container follows the entry that carried the write body, which is the named member
        // write when the type has one.
        w.Suppressed = !writes;
        if (model.MemberWrite)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void SerializeMember" + suffix + Arguments() + "(scoped ref " + writer + " writer, scoped in " + type + "? input) => " +
                (fixedProfile ? container + ".RootWriteMember(ref writer, input);" :
                    "SerializeMember" + suffix + Arguments(Standard()) + "(ref writer, in input);"));
            w.Blank();
            // A fixed-profile record publishes its generic member write with the other entries, below the roots.
            if (!fixedProfile)
            {
                w.Line(w.Preserve);
                w.Line(inline);
                w.Line("internal static void SerializeMember" + suffix + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
                w.Indented(() => w.Line(Constraint() + " => " + container + ".RootWriteMember<" + Profile + ">(ref writer, input);"));
                w.Blank();
            }
        }
        else
        {
            SerializeStandard();
            if (genericWrite)
            {
                SerializeProfiled();
                w.Blank();
            }
        }
        w.Suppressed = false;
        Roots();
        w.Blank();
        w.Suppressed = !writes;
        if (model.MemberWrite)
        {
            if (fixedProfile)
            {
                w.Line(w.Preserve);
                w.Line(inline);
                w.Line("internal static void SerializeMember" + suffix + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
                w.Indented(() => w.Line(Constraint() + " => SerializeMember" + suffix + "(ref writer, in input);"));
                w.Blank();
            }
            SerializeStandard();
            SerializeProfiled();
            w.Blank();
        }
        else if (!genericWrite)
        {
            SerializeProfiled();
            w.Blank();
        }
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        string deserialize = "internal static void Deserialize" + suffix + Arguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)";
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
        w.Line("internal static void Deserialize" + suffix + Arguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + container + "." + readRoot + "(ref reader, ref value);"));
        w.Blank();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        string serializeUnion = "internal static void SerializeUnion_" + readable + Arguments() + "(scoped ref " + writer + " writer, " + type + "? input)";
        string serializeUnionCall = genericWrite ? "SerializeUnion_" + readable + Arguments(Standard()) + "(ref writer, input);" :
            container + ".RootWriteUnion" + Arguments() + "(ref writer, input);";
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
        w.Line("internal static void SerializeUnion_" + readable + Arguments(Profile) + "(scoped ref " + writer + " writer, " + type + "? input)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + (genericWrite ? container + "." + writeUnion + "(ref writer, input);" : "SerializeUnion_" + readable + Arguments() + "(ref writer, input);")));
        w.Blank();
        if (model.FixedUnionCase)
        {
            string parameters = "(scoped ref " + writer + " writer, " + type + "? input, ";
            var fixedBody = FixedUnionBody(w, WriteBody(model, w, union: true), model.ManagedFixedUnionCase);
            if (model.ManagedFixedUnionCase)
            {
                Method(fixedBody, "SerializeUnionAtCursor_" + readable, false,
                    parameters + "ref byte initialCursor, uint? unionTag)", inline, "ref byte");
                w.Blank();
                string buffer = w.A("MemoryMarshal") + ".GetReference(writer.Buffer)";
                string unsafeType = w.A("Unsafe");
                Method(new List<string>
                {
                    "ref byte start = ref " + unsafeType + ".Add(ref " + buffer + ", unchecked((nint)(uint)initialIndex));",
                    "ref byte end = ref SerializeUnionAtCursor_" + readable + "(ref writer, input, ref start, unionTag);",
                    "return (int)" + unsafeType + ".ByteOffset(ref " + buffer + ", ref end);",
                }, "SerializeUnionAt_" + readable, false, parameters + "int initialIndex, uint? unionTag)", inline, "int");
            }
            else
                Method(fixedBody, "SerializeUnionAt_" + readable, false, parameters + "int initialIndex, uint? unionTag)", inline, "int");
            w.Blank();
        }
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        string deserializeUnion = "internal static void DeserializeUnion" + suffix + Arguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)";
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
        w.Line("internal static void DeserializeUnion" + suffix + Arguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(ConstraintLine(true) + " => " + (genericRead ? container + "." + readUnion + "(ref reader, ref value);" :
            "DeserializeUnion" + suffix + Arguments() + "(ref reader, ref value);")));
        w.Suppressed = !writes;
        if (model.MeasureRecord)
        {
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void MeasureRecord" + suffix + Arguments() + "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + "? value)");
            w.Open();
            w.Line("if (value is null)");
            w.Open();
            w.Line("evaluator.Add(1);");
            w.Line("return;");
            w.Close();
            w.Blank();
            if (model.Version != null)
                w.Line("evaluator.Add(" + VersionHeaderLength(model) + " + " + members.Sum(member => member.Size).ToString(CultureInfo.InvariantCulture) + ");");
            else
            {
                w.Line("int totalLength = 1;");
                // A memberless record's only byte is its marker, which the initializer already counts; constant widths
                // fold into one term, while a runtime struct size stays an expression.
                if (members.Length != 0)
                {
                    int constant = 0;
                    bool folded = members.All(member => int.TryParse(member.Width, NumberStyles.None, CultureInfo.InvariantCulture, out _));
                    if (folded)
                        foreach (var member in members)
                            constant += int.Parse(member.Width, CultureInfo.InvariantCulture);
                    w.Line("totalLength += " + (folded ? constant.ToString(CultureInfo.InvariantCulture) :
                        string.Join(" + ", members.Select(member => member.Width))) + ";");
                }
                w.Line("evaluator.Add(totalLength);");
            }
            w.Close();
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void MeasureRecord" + suffix + Arguments(Profile) + "(scoped ref global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + type + "? value)");
            w.Indented(() => w.Line(ConstraintLine(true) + " => MeasureRecord" + suffix + Arguments() + "(ref evaluator, ref value);"));
        }
        w.Suppressed = false;
        if (model.Mirrors.Length != 0)
            StorageMirrorEmitter.Emit(w, model.Mirrors);
        else if ((model.Mirror ?? model.Version?.Layout) != null)
        {
            // The root-private layout mirror that the operations reach auto-property and included non-public storage through;
            // a version-tolerant record always declares it.
            w.Blank();
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line(w.Preserve);
            w.Line("private class " + (model.Mirror ?? model.Version.Layout));
            if (model.Mirror != null && TypeConstraint().Length != 0)
                w.Indented(() => w.Line(TypeConstraint()));
            w.Open();
            MirrorFields(w, model.MirrorFields ?? model.Version.LayoutFields, model.MirrorFields != null ? model.MirrorOffsets : null);
            w.Close();
        }
        w.Close();
        w.Blank();

        // Payload extensions.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        // A payload whose write body contains an element loop advances through an isolated copy of the writer state, so a
        // failure leaves the caller's state untouched.
        if (model.ElementList || model.HasDictionary)
        {
            IsolatedWrite("internal static void Write" + Arguments() + "(this ref " + writer + " state, in " + type + "? value)", null,
                genericWrite ? Standard() : null);
            w.Blank();
            IsolatedWrite("internal static void Write" + Arguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)", OptionsConstraint(),
                genericWrite ? "TOptions" : null);
        }
        else
        {
            w.Line(inline);
            string write = "internal static void Write" + Arguments() + "(this ref " + writer + " state, in " + type + "? value)";
            string writeCall = Serializer() + "." + container + "." + Invoke("RootWrite", genericWrite ? Standard() : null) + "(ref state, value);";
            if (TypeConstraint().Length == 0)
                w.Line(write + " => " + writeCall);
            else
            {
                w.Line(write);
                w.Indented(() => w.Line(TypeConstraint() + " => " + writeCall));
            }
            w.Line(inline);
            w.Line("internal static void Write" + Arguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)");
            w.Indented(() => w.Line(OptionsConstraint() + " => " + Serializer() + "." + container + "." +
                Invoke("RootWrite", genericWrite ? "TOptions" : null) + "(ref state, value);"));
        }
        w.Suppressed = !reads;
        // A version-tolerant record and a type built by its selected constructor read through an isolated copy of the reader
        // state, so a failure leaves the caller's state untouched.
        if (model.Version != null || !model.IsStruct && model.ConstructorArguments != null)
        {
            // A version-tolerant record reads through an isolated copy of the reader state.
            IsolatedRead("internal static void Read" + Arguments() + "(this ref " + reader + " state, ref " + type + "? value)", null,
                genericRead ? Standard() : null);
            w.Blank();
            IsolatedRead("internal static void Read" + Arguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)", OptionsConstraint(),
                genericRead ? "TOptions" : null);
        }
        else
        {
            w.Line(inline);
            string read = "internal static void Read" + Arguments() + "(this ref " + reader + " state, ref " + type + "? value)";
            string readCall = Serializer() + "." + container + "." + Invoke("RootRead", genericRead ? Standard() : null) + "(ref state, ref value);";
            if (TypeConstraint().Length == 0)
                w.Line(read + " => " + readCall);
            else
            {
                w.Line(read);
                w.Indented(() => w.Line(TypeConstraint() + " => " + readCall));
            }
            w.Line(inline);
            w.Line("internal static void Read" + Arguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)");
            w.Indented(() => w.Line(OptionsConstraint() + " => " + Serializer() + "." + container + "." +
                Invoke("RootRead", genericRead ? "TOptions" : null) + "(ref state, ref value);"));
        }
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
        string writePolymorphic = "public static void WritePolymorphismValue" + Arguments() + "(ref this " + writer + " writer, scoped in " + type + "? input)";
        string writePolymorphicCall = Serializer() + ".SerializeUnion_" + readable + Arguments() + "(ref writer, input);";
        if (TypeConstraint().Length == 0)
            w.Line(writePolymorphic + " => " + writePolymorphicCall);
        else
        {
            w.Line(writePolymorphic);
            w.Indented(() => w.Line(TypeConstraint() + " => " + writePolymorphicCall));
        }
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(polymorphic);
        string readPolymorphic = "public static void ReadPolymorphismValue" + Arguments() + "(ref this " + reader + " reader, scoped ref " + type + "? value)";
        string readPolymorphicCall = Serializer() + ".DeserializeUnion" + suffix + Arguments() + "(ref reader, ref value);";
        if (TypeConstraint().Length == 0)
            w.Line(readPolymorphic + " => " + readPolymorphicCall);
        else
        {
            w.Line(readPolymorphic);
            w.Indented(() => w.Line(TypeConstraint() + " => " + readPolymorphicCall));
        }
        w.Suppressed = false;
        if (model.Version != null)
        {
            w.Blank();
            VersionSchemaClass(model, w, reads);
        }
        w.Close();
        w.Close();

        void IsolatedRead(string signature, string constraint, string profile)
        {
            w.Line(inline);
            w.Line(signature);
            if (constraint != null)
                w.Indented(() => w.Line(constraint));
            w.Open();
            w.Line(reader + " isolated = default;");
            w.Line("state.CopyTo(ref isolated);");
            EmitIsolatedVersionOperation(w, Serializer() + "." + container + "." + Invoke("RootRead", profile) +
                "(ref isolated, ref value);", model.Version != null, read: true);
            w.Line("isolated.CopyTo(ref state);");
            w.Close();
        }

        void IsolatedWrite(string signature, string constraint, string profile)
        {
            w.Line(inline);
            w.Line(signature);
            if (constraint != null)
                w.Indented(() => w.Line(constraint));
            w.Open();
            w.Line(writer + " isolated = default;");
            w.Line("state.CopyTo(ref isolated);");
            EmitIsolatedVersionOperation(w, Serializer() + "." + container + "." + Invoke("RootWrite", profile) +
                "(ref isolated, value);", model.Version != null, read: false);
            w.Line("isolated.CopyTo(ref state);");
            w.Close();
        }

        void SerializeStandard()
        {
            w.Line(w.Preserve);
            w.Line(inline);
            string signature = "internal static void Serialize" + suffix + Arguments() + "(scoped ref " + writer + " writer, scoped in " + type + "? input)";
            string call = container + "." + Invoke("RootWrite", genericWrite ? Standard() : null) + "(ref writer, input);";
            if (TypeConstraint().Length == 0)
                w.Line(signature + " => " + call);
            else
            {
                w.Line(signature);
                w.Indented(() => w.Line(TypeConstraint() + " => " + call));
            }
            w.Blank();
        }

        void SerializeProfiled()
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void Serialize" + suffix + Arguments(Profile) + "(scoped ref " + writer + " writer, scoped in " + type + "? input)");
            w.Indented(() => w.Line(ConstraintLine(true) + " => " + container + "." + writeRoot + "(ref writer, input);"));
        }

        // A stream record's body is already the lowered statement text, so the call budget is counted from it directly. A
        // mixed record counts its inline objects' expanded bodies structurally, because their member runs, not their call
        // sites, decide the budget. A member write stays a call boundary whose callee keeps its own budget, so the runtime
        // sizes the entry instead, and any record that calls one keeps no inline hint.
        bool IsHeavyRecord() => !model.IsStruct && model.Version == null && members.Any(member =>
            member.Kind is RenderMemberKind.Array or RenderMemberKind.List or RenderMemberKind.Object or
                RenderMemberKind.Dictionary or RenderMemberKind.SparseDictionary or RenderMemberKind.SortedList or
                RenderMemberKind.PairList or RenderMemberKind.NestedList or RenderMemberKind.ForwardedCollection or
                RenderMemberKind.Sequence || member.ReferencePayload);

        string WriteHint(List<string> body, bool union) => IsHeavyRecord() ? w.MethodImpl("NoInlining") : model.HasDictionary ? null :
            (model.StreamRecord || model.MixedRecord) &&
            !body.Any(line => line.IndexOf(".SerializeMember_", StringComparison.Ordinal) >= 0) &&
            !body.Any(line => line == "do" || line.StartsWith("for (", StringComparison.Ordinal) || line.StartsWith("foreach (", StringComparison.Ordinal) ||
                line.StartsWith("static ", StringComparison.Ordinal))
            ? ((model.StreamRecord ? InvocationCounter.Count(body) <= 16 : DirectInlining.MixedWrite(model, union)) ? inline : null)
            : (model.StreamRecord || model.MixedRecord ? null : (DirectInlining.Write(model, union, Sizes(body)) ? inline : null));

        string ReadHint(List<string> body) => IsHeavyRecord() ? w.MethodImpl("NoInlining") : model.StreamRecord || model.MixedRecord
            ? (InvocationCounter.Count(body) <= 16 ? inline : null)
            : (model.ConstructorArguments != null ? (DirectInlining.ConstructedRead(model) ? inline : null)
                : (DirectInlining.Read(model, Sizes(body)) ? inline : null));

        void Roots()
        {
            w.Line("internal static partial class " + container);
            w.Open();
            w.Suppressed = !writes;
            if (CanReserveFixedObject(model))
            {
                w.Line(inline);
                w.Line("internal static int FixedRootWidth() => " + FixedObjectWidth(model) + ";");
                w.Blank();
                var stores = new List<string>();
                WriteFixedObject(model, w, stores, "value", "cursor", members.Length.ToString(CultureInfo.InvariantCulture));
                Method(stores, "WriteFixedRoot", false, "(ref byte cursor, " + type + " value)", inline, prepared: true);
                w.Blank();
            }
            if (CanReserveBoundedNullableObject(model))
            {
                string bound = string.Join(" + ", members.Select(member => member.NativeNullablePayload is { } nullable
                    ? "1 + " + w.A("Unsafe") + ".SizeOf<" + nullable + ">()" : member.Width));
                w.Line(inline);
                w.Line("internal static int NullableRootBound() => checked(1 + " + bound + ");");
                w.Blank();
                Method(BoundedNullableStores(model, w), "WriteBoundedNullableRoot", false, "(ref byte destination, " + type + " value)",
                    DirectInlining.Write(model, union: false, sizes: 0) ? inline : null, returns: "int", prepared: true);
                w.Blank();
            }
            if (model.MemberWrite)
            {
                Method(MemberWriteBody(model, w), "RootWriteMember", !fixedProfile, "(scoped ref " + writer + " writer, " + type + "? input)", inline);
                w.Blank();
            }
            if (model.Version != null)
            {
                VersionRoots();
                return;
            }
            var write = Mirrored(model, w, WriteBody(model, w, union: false), writes: true, union: false);
            if (model.Proven)
                write = ProvenWriteBody(model, w, write, union: false);
            write = AddCallbacks(write, model.WriteBefore, model.WriteAfter);
            string writeParameters = "(scoped ref " + writer + " writer, " + type + "? input)";
            if (model.FixedUnionCase)
            {
                Method(write, "RootWrite", genericWrite, writeParameters, WriteHint(write, union: false));
                w.Blank();
                w.Line(w.Preserve);
                w.Line(inline);
                w.Line("internal static void RootWriteUnion(scoped ref " + writer + " writer, " + type +
                    (model.ManagedFixedUnionCase
                        ? "? input) => writer.CommitRecords(ref SerializeUnionAtCursor_" + readable + "(ref writer, input, ref writer.Cursor, null));"
                        : "? input) => writer.Position = SerializeUnionAt_" + readable + "(ref writer, input, writer.Position, null);"));
            }
            else
            {
                var payloadWrite = Mirrored(model, w, WriteBody(model, w, union: true), writes: true, union: true);
                if (model.Proven && members.Length != 1)
                    payloadWrite = ProvenWriteBody(model, w, payloadWrite, union: true);
                payloadWrite = AddCallbacks(payloadWrite, model.WriteBefore, model.WriteAfter);
                Method(FramingMerger.Share(Prepare(write, "RootWrite", writeParameters), Prepare(payloadWrite, "RootWriteUnion", writeParameters)),
                    "Write", genericWrite, writeParameters, WriteHint(write, union: false), framing: true, prepared: true);
                RootAdapter("RootWrite", "Write", genericWrite, writeParameters, "ref writer, input", "LuminPackObjectFraming");
                RootAdapter("RootWriteUnion", "Write", genericWrite, writeParameters, "ref writer, input", "LuminPackPayloadFraming");
            }
            w.Blank();
            w.Suppressed = !reads;
            var read = AddReadCallbacks(model, Mirrored(model, w, ReadBody(model, w, union: false, freshPayload: true), writes: false, union: false));
            var payloadRead = AddReadCallbacks(model, Mirrored(model, w, ReadBody(model, w, union: true, freshPayload: true), writes: false, union: true));
            string readParameters = "(scoped ref " + reader + " reader, scoped ref " + type + "? value)";
            Method(FramingMerger.Share(Prepare(read, "RootRead", readParameters), Prepare(payloadRead, "RootReadUnion", readParameters)),
                "Read", genericRead, readParameters, ReadHint(read), framing: true, prepared: true);
            RootAdapter("RootRead", "Read", genericRead, readParameters, "ref reader, ref value", "LuminPackObjectFraming");
            RootAdapter("RootReadUnion", "Read", genericRead, readParameters, "ref reader, ref value", "LuminPackPayloadFraming");
            w.Suppressed = false;
            w.Close();
        }

        void VersionRoots()
        {
            // The Union payload of a version-tolerant record is the record itself; only the inline hint follows the body size.
            // A mirrored record keeps its null handling in both bodies, so the mirror view is declared at the same position.
            // A struct never writes or reads a null marker and keeps its callbacks around the same schema body.
            string value = model.IsStruct ? type : type + "?";
            var writeBody = VersionWriteBody(model, w);
            var write = AddCallbacks(Mirrored(model, w, writeBody, writes: true, union: false), model.WriteBefore, model.WriteAfter);
            string writeHint = VersionWriteInline(model) ? inline : null;
            w.Suppressed = !writes;
            Method(write, "RootWrite", true, "(scoped ref " + writer + " writer, " + (model.IsStruct ? "scoped in " : string.Empty) + value + " input)", writeHint);
            w.Blank();
            w.Suppressed = !reads;
            Method(AddReadCallbacks(model, VersionReadBody(model, w)), "RootRead", true,
                "(scoped ref " + reader + " reader, scoped ref " + value + " value)", null);
            w.Blank();
            w.Suppressed = !writes;
            string writeParameters = "(scoped ref " + writer + " writer, " + (model.IsStruct ? "scoped in " : string.Empty) + value + " input)";
            if (model.Mirror == null || model.WriteBefore.Length == 0 && model.WriteAfter.Length == 0)
                VersionAlias("RootWriteUnion", "RootWrite", writeParameters, "ref writer, input");
            else
            {
                var unionWrite = Mirrored(model, w, AddCallbacks(writeBody, model.WriteBefore, model.WriteAfter), writes: true, union: false);
                if (unionWrite.SequenceEqual(write))
                    VersionAlias("RootWriteUnion", "RootWrite", writeParameters, "ref writer, input");
                else
                    Method(unionWrite, "RootWriteUnion", true, writeParameters, writeHint);
            }
            w.Blank();
            w.Suppressed = !reads;
            VersionAlias("RootReadUnion", "RootRead", "(scoped ref " + reader + " reader, scoped ref " + value + " value)", "ref reader, ref value");
            w.Suppressed = false;
            w.Close();
        }

        void VersionAlias(string name, string target, string parameters, string values)
        {
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + name + Arguments(Profile) + parameters);
            w.Indented(() => w.Line(ConstraintLine(true) + " => " + target + Arguments(Profile) + "(" + values + ");"));
        }

        List<string> Prepare(List<string> body, string name, string parameters)
        {
            // An explicitly untrusted graph that reaches an input-selected shape fails closed in both directions.
            if (model.Rejection != null)
                body = new List<string> { RejectUnsupported(model.Rejection) };
            else
            {
                body = InputChecks(model, w, body, read: parameters.Contains(" reader, "));
                // A recursive payload's root operations start with the stack guard; a write also checks its buffer first.
                if (model.Recursive && name is "RootWrite" or "RootRead" or "RootWriteUnion" or "RootReadUnion")
                {
                    body = new List<string>(body);
                    body.Insert(0, w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
                    if (parameters.Contains(" writer, "))
                        body.Insert(1, "writer.CheckBuffer();");
                }
            }
            return body;
        }

        void RootAdapter(string name, string target, bool generic, string parameters, string values, string marker)
        {
            w.Blank();
            w.Line(w.Preserve);
            w.Line(inline);
            w.Line("internal static void " + Root(name, generic) + parameters);
            string constraint = ConstraintLine(generic);
            string arguments = generic ? Arguments(Profile, "global::LuminPack.Code." + marker) : Arguments("global::LuminPack.Code." + marker);
            w.Indented(() => w.Line((constraint.Length == 0 ? string.Empty : constraint + " ") + "=> " + target + arguments + "(" + values + ");"));
        }

        void Method(List<string> body, string name, bool generic, string parameters, string attribute, string returns = "void", bool framing = false, bool prepared = false)
        {
            if (!prepared)
                body = Prepare(body, name, parameters);
            w.Line(w.Preserve);
            if (attribute != null)
                w.Line(attribute);
            string typeArguments = framing ? generic ? Arguments(Profile, "TLuminFraming") : Arguments("TLuminFraming") : generic ? Arguments(Profile) : Arguments();
            w.Line("internal static " + returns + " " + name + typeArguments + parameters);
            if (generic || TypeConstraint().Length != 0)
                w.Indented(() => w.Line(ConstraintLine(generic)));
            if (framing)
                w.Indented(() => w.Line("where TLuminFraming : struct"));
            w.Open();
            for (int index = 0; index < body.Count; index++)
            {
                string line = body[index];
                if (line.Length == 0)
                {
                    // A blank requested before an opening brace is a separator between two statement blocks, which the
                    // opening brace would otherwise drop.
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
    }

    /// <summary>Spells an unaligned cursor position; a plain spelling was already short in the template and registers no alias.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="cursor">The cursor supplied to <c>At</c>.</param>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <param name="plain">Whether to enable plain for this operation.</param>
    /// <returns>The resulting text.</returns>
    private static string At(CodeWriter w, string cursor, string offset, bool plain = false)
        => offset == "0" ? cursor : (plain ? "Unsafe" : w.A("Unsafe")) + ".Add(ref " + cursor + ", " + offset + ")";

    /// <summary>Adds a member's width to an offset as the original template does: constants fold until the first runtime size, after
    /// which every term is appended in order.</summary>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <param name="size">The requested size.</param>
    /// <returns>The resulting text.</returns>
    private static string Add(string offset, string size)
    {
        if (offset == "0")
            return size;
        if (int.TryParse(offset, NumberStyles.None, CultureInfo.InvariantCulture, out int total) &&
            int.TryParse(size, NumberStyles.None, CultureInfo.InvariantCulture, out int length) && total <= int.MaxValue - length)
            return (total + length).ToString(CultureInfo.InvariantCulture);
        return offset + " + " + size;
    }

    /// <summary>The packed width of a run of fixed members.</summary>
    /// <param name="members">The members supplied to <c>Width</c>.</param>
    /// <param name="first">The first supplied to <c>Width</c>.</param>
    /// <param name="last">The last supplied to <c>Width</c>.</param>
    /// <returns>The resulting text.</returns>
    private static string Width(RenderMember[] members, int first, int last)
    {
        string width = "0";
        for (int index = first; index <= last; index++)
            width = Add(width, members[index].Width);
        return width;
    }

    /// <summary>Counts the runtime-size calls of raw struct widths in a root body, which the inlining budget counts as calls.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    private static int Sizes(List<string> body)
    {
        int count = 0;
        foreach (string line in body)
            for (int index = line.IndexOf(".SizeOf<", StringComparison.Ordinal); index >= 0; index = line.IndexOf(".SizeOf<", index + 1, StringComparison.Ordinal))
                if (string.CompareOrdinal(line, index + 8, "global::System.IntPtr>", 0, 22) != 0)
                    count++;
        return count;
    }

    /// <summary>Spells a member's storage reinterpreted as bytes for a block copy or proof; see <see cref="At"/> for plain spellings.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="readOnly">Whether to enable read only for this operation.</param>
    /// <param name="plain">Whether to enable plain for this operation.</param>
    /// <returns>The resulting text.</returns>
    internal static string Bytes(CodeWriter w, RenderMember member, string owner, bool readOnly, bool plain = false)
        => (plain ? "Unsafe" : w.A("Unsafe")) + ".As<" + member.Scalar + ", byte>(ref " + (readOnly ? (plain ? "Unsafe" : w.A("Unsafe")) + ".AsRef(in " + owner + "." + member.Name + ")" : owner + "." + member.Name) + ")";

    /// <summary>Spells the proof that a run of fixed fields is laid out contiguously on a 64-bit target.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="members">The members supplied to <c>Proof</c>.</param>
    /// <param name="first">The first supplied to <c>Proof</c>.</param>
    /// <param name="last">The last supplied to <c>Proof</c>.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="readOnly">Whether to enable read only for this operation.</param>
    /// <returns>The resulting text.</returns>
    /// <param name="coreLayout">Whether the ordinary record uses the Core storage-layout capability.</param>
    internal static string Proof(CodeWriter w, RenderMember[] members, int first, int last, string owner, bool readOnly, bool coreLayout = false)
    {
        for (int index = first; index <= last; index++)
            if (members[index].Volatile)
                return "false";
        string memory = w.A("Unsafe");
        var proof = new System.Text.StringBuilder(coreLayout ? string.Empty : memory + ".SizeOf<global::System.IntPtr>() == 8");
        string address = Bytes(w, members[first], owner, readOnly);
        for (int index = first; index < last; index++)
        {
            string next = Bytes(w, members[index + 1], owner, readOnly);
            string width = members[index].Raw ? memory + ".SizeOf<" + members[index].Scalar + ">()" : members[index].Width;
            if (coreLayout && index == first)
                proof.Append("global::LuminPack.Code.LuminPackMarshal.CanBatchFields(ref ").Append(address).Append(", ref ").Append(next).Append(", ").Append(width).Append(')');
            else
                proof.Append(" && ").Append(memory).Append(".ByteOffset(ref ").Append(address).Append(", ref ").Append(next).Append(") == ").Append(width);
            address = next;
        }
        return proof.Length == 0 ? "false" : proof.ToString();
    }

    /// <summary>Ends a run of consecutive fixed members.</summary>
    /// <param name="members">The members supplied to <c>RunEnd</c>.</param>
    /// <param name="first">The first supplied to <c>RunEnd</c>.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    private static int RunEnd(RenderMember[] members, int first)
    {
        int last = first;
        while (!members[first].Volatile && last + 1 < members.Length && members[last + 1].IsFixed && !members[last + 1].Volatile)
            last++;
        return last;
    }

    /// <summary>Whether a name consists of ASCII letters only, which the presentation's expansion-number removal requires.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>Whether the input satisfies the ascii letters check.</returns>
    private static bool AsciiLetters(string name) => name.All(letter => letter is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    /// <summary>Spells the snapshot local of a variable member as the projected emitter names it.</summary>
    /// <param name="model">The record that owns the member.</param>
    /// <param name="index">The member's index.</param>
    /// <param name="camel">Whether the mixed record body camel-cases every snapshot local, as its inline object members do.</param>
    /// <returns>The resulting text.</returns>
    private static string Snapshot(PayloadRenderModel model, int index, bool camel = false)
    {
        string trimmed = model.Members[index].Name.TrimStart('_');
        string stem = trimmed + "Snapshot";
        string name = stem + index.ToString(CultureInfo.InvariantCulture);
        string chosen = AsciiLetters(trimmed) && model.Members.All(member => member.Name != stem) ? stem : name;
        // Snapshots of mirrored members are named like role locals.
        return !(model.CamelSnapshots || camel) ? chosen : char.ToLowerInvariant(chosen[0]) + chosen.Substring(1);
    }

    /// <summary>Builds a root write body, with lines prefixed by a tab for one extra indentation level.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <param name="structValue">Whether to enable struct value for this operation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> WriteBody(PayloadRenderModel model, CodeWriter w, bool union, bool structValue = false)
    {
        if (model.SequenceBase != null)
            return SequenceWriteBody(model, w, union);
        var members = model.Members;
        var body = new List<string>();
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        bool bounded = model.Bounded != null;
        if (!union && !structValue && CanReserveFixedObject(model))
            return ReservedFixedObject(model, w);
        body.Add("var value = input;");
        if (bounded)
        {
            // A bounded record reserves its complete guarantee once and asserts it where every exit meets.
            body.Add("int boundedStart = writer.Position;");
            body.Add("writer.EnsureAdditionalCapacity(" + model.Bounded.Value.ToString(CultureInfo.InvariantCulture) + ");");
        }
        if (!union && !structValue)
        {
            body.Add("if (value is null)");
            body.Add("{");
            body.Add("writer.Cursor = (byte)255;");
            body.Add("writer.Advance(1);");
            body.Add(bounded ? "goto end0;" : "return;");
            body.Add("}");
            body.Add("");
        }
        // A memberless record is its marker alone; its Union payload writes no bytes.
        if (members.Length == 0)
        {
            if (!union)
            {
                body.Add("writer.Cursor = (byte)0;");
                body.Add("writer.Advance(1);");
            }
            AddBoundExit(body, model, union);
            return body;
        }
        if (members.All(member => member.IsFixed))
        {
            if (union)
            {
                string widths = string.Join(" + ", members.Select(member => member.Width));
                body.Add("if (checked(4 + " + widths + ") > 4096)");
                body.Add("\t" + (bounded ? ";" : "writer.EnsureAdditionalCapacity(checked(" + widths + "));"));
            }
            else
            {
                body.Add("writer.Cursor = (byte)" + count + ";");
                body.Add("writer.Advance(1);");
            }
            if (members.Length == 1)
            {
                // A lone fixed member is not a contiguous group; the writer stores it directly.
                body.Add("writer.Write(value." + members[0].Name + ");");
                AddBoundExit(body, model, union);
                return body;
            }
            string width = Width(members, 0, members.Length - 1);
            body.Add("if (" + Proof(w, members, 0, members.Length - 1, "value", readOnly: true, coreLayout: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, members[0], "value", readOnly: true, plain: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            body.Add("ref byte cursor = ref writer.Cursor;");
            AddScalarStores(w, members, 0, members.Length - 1, "cursor", body);
            body.Add("writer.Advance(" + width + ");");
            body.Add("}");
            if (bounded)
                body.Add("");
            AddBoundExit(body, model, union);
            return body;
        }
        bool nullableRecord = members.Any(member => member.NativeNullablePayload != null) &&
            members.All(member => member.IsFixed || member.NativeNullablePayload != null);
        // Reserving members share the header and consecutive runs. Dictionary entries and inline blocks keep their own kernels.
        bool dictionary = model.HasDictionary;
        if (dictionary || !nullableRecord && (model.MixedRecord || members.Any(member => member.ReservableSpan)))
        {
            if (model.DeclaredProfile == null && members.Any(member => member.Kind == RenderMemberKind.Text && member.ScopeProfile == null) &&
                members.Any(member => member.ReservableSpan && !member.OriginalSpanReservation))
                AddStorageProfileBranches(model, w, body, AppendSegments);
            else
                AppendSegments(body, reserveNativeSpans: true);
            AddBoundExit(body, model, union);
            return body;

            void AppendSegments(List<string> branch, bool reserveNativeSpans)
            {
                if (dictionary)
                    DictionaryWriteBody(model, w, union, branch, reserveNativeSpans);
                else
                    MixedWriteBody(model, w, union, branch, reserveNativeSpans);
            }
        }
        // A pure stream writes its member count and lets each remaining segment advance the writer.
        if (!nullableRecord && model.StreamRecord)
        {
            StreamWriteBody(model, w, union, body);
            AddBoundExit(body, model, union);
            return body;
        }

        body.Add("ref byte cursor = ref writer.Cursor;");
        // A record whose strings a declaration fixes carries no member comment: the profile projection replaces the site.
        if (model.DeclaredProfile == null)
            body.Add("// Fields: " + string.Join(", ", members.Select(member => member.Name)));
        if (union && members.Length == 1 && !nullableRecord)
        {
            // A lone record checks itself.
            body.Add(members[0].Kind == RenderMemberKind.Text
                ? "cursor = ref writer.WriteRecordString<" + WriteProfile(model, w, members[0]) + ">(ref cursor, value." + members[0].Name + ");"
                : "cursor = ref writer.WriteRecordSpan(ref cursor, value." + members[0].Name + ");");
            body.Add("writer.Cursor = ref cursor;");
            AddBoundExit(body, model, union);
            return body;
        }
        var terms = new List<string>();
        var exactTerms = nullableRecord ? new List<string>() : null;
        if (!union)
        {
            terms.Add("1");
            exactTerms?.Add("1");
        }
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(model, index, camel: model.HasCallbacks || model.Bounded != null);
            if (member.Kind == RenderMemberKind.Fixed)
            {
                terms.Add(member.Width);
                exactTerms?.Add(member.Width);
            }
            else if (member.NativeNullablePayload != null)
            {
                string width = w.A("Unsafe") + ".SizeOf<" + member.NativeNullablePayload + ">()";
                body.Add("var " + snapshot + " = value." + member.Name + ";");
                terms.Add("1 + " + width);
                exactTerms.Add("(" + snapshot + ".HasValue ? 1 + " + width + " : 1)");
            }
            else if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("string " + snapshot + " = value." + member.Name + ";");
                terms.Add("writer.RecordBound<" + WriteProfile(model, w, member) + ">(" + snapshot + ")");
            }
            else
            {
                body.Add("var " + snapshot + " = " + w.A("LuminPackWriter") + ".Capture(value." + member.Name + ");");
                terms.Add(snapshot + ".Bound");
            }
        }
        string bound = "checked(" + string.Join(" + ", terms) + ")";
        if (nullableRecord)
        {
            body.Add("if (writer.RecordRoom(ref cursor) < " + bound + ")");
            body.Add("\tcursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", exactTerms) + "));");
        }
        else
            body.Add("cursor = ref writer.ReserveRecord(ref cursor, " + bound + ");");
        if (!union)
        {
            body.Add("cursor = (byte)" + count + ";");
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 1);");
        }
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int last = RunEnd(members, index);
                if (last == index)
                {
                    body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReserved<" + member.Scalar + ">(ref cursor, value." + member.Name + ");");
                    continue;
                }
                string width = Width(members, index, last);
                body.Add("if (" + Proof(w, members, index, last, "value", readOnly: true, coreLayout: true) + ")");
                body.Add("\tcursor = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref cursor, ref " + Bytes(w, member, "value", readOnly: true) + ", " +
                    width + ");");
                body.Add("else");
                body.Add("{");
                AddScalarStores(w, members, index, last, "cursor", body);
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + width + ");");
                body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            body.Add("// Field: " + member.Name);
            if (member.Kind == RenderMemberKind.Text)
                body.Add("cursor = ref writer.WriteReservedRecordString<" + WriteProfile(model, w, member) + ">(ref cursor, " + Snapshot(model, index, camel: model.HasCallbacks || model.Bounded != null) + ");");
            else if (member.NativeNullablePayload != null)
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedNullable<" + member.NativeNullablePayload + ">(ref cursor, " + Snapshot(model, index, camel: model.HasCallbacks || model.Bounded != null) + ");");
            else
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + Snapshot(model, index, camel: model.HasCallbacks || model.Bounded != null) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add("writer.Cursor = ref cursor;");
        if (model.Bounded != null)
            body.Add("");
        AddBoundExit(body, model, union);
        return body;
    }

    /// <summary>Builds the write body of a list-derived payload by forwarding to the closed list owner.</summary>
    /// <param name="model">The record whose payload is its list base.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether the body writes a Union payload.</param>
    /// <returns>The body lines.</returns>
    /// <remarks>The list owner writes the null header, length and elements, so a Union payload is byte-identical to a root.</remarks>
    private static List<string> SequenceWriteBody(PayloadRenderModel model, CodeWriter w, bool union)
    {
        string roots = w.A("LuminPackSerializer") + ".LuminRoots_" +
            ReadableTypeName.Of(GeneratedNames.PayloadKey(model.SequenceBase!));
        var body = new List<string>
        {
            "var value = input;",
            roots + ".RootWrite<" + Profile + ">(ref writer, in " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName +
                ", " + model.SequenceBase + ">(ref value));",
        };
        AddBoundExit(body, model, union);
        return body;
    }

    /// <summary>Builds the read body of a list-derived payload by forwarding to the closed list owner.</summary>
    /// <param name="model">The record whose payload is its list base.</param>
    /// <param name="w">The generated source writer.</param>
    /// <returns>The body lines.</returns>
    /// <remarks>The incoming instance keeps its derived type, so the list owner fills that storage instead of a plain list.</remarks>
    private static List<string> SequenceReadBody(PayloadRenderModel model, CodeWriter w)
    {
        string roots = w.A("LuminPackSerializer") + ".LuminRoots_" +
            ReadableTypeName.Of(GeneratedNames.PayloadKey(model.SequenceBase!));
        return new List<string>
        {
            "value ??= new " + model.GlobalName + "();",
            roots + ".RootRead<" + Profile + ">(ref reader, ref " + w.A("LuminPackMarshal") + ".As<" + model.GlobalName +
                ", " + model.SequenceBase + ">(ref value));",
        };
    }

    /// <summary>Closes a bounded write root: the null exit label, the completed-length assertion and the commit.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    private static void AddBoundExit(List<string> body, PayloadRenderModel model, bool union)
    {
        if (model.Bounded == null)
            return;
        string bound = model.Bounded.Value.ToString(CultureInfo.InvariantCulture);
        if (!union)
        {
            // Only the root write's null check jumps here; the Union payload has no null branch.
            body.Add("end0:");
            body.Add("\t;");
        }
        body.Add("global::System.Diagnostics.Debug.Assert(");
        body.Add("\twriter.Position - boundedStart <= " + bound + ",");
        body.Add("\t\"The LuminPackBounded complete-record guarantee was violated.\");");
        body.Add("writer.CompleteReservedRecord();");
    }

    /// <summary>Spells instance creation for a read: the pool rent call of a pooled type, or its parameterless constructor.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <returns>The resulting text.</returns>
    private static string NewInstance(PayloadRenderModel model) => model.Rent ?? "new " + model.GlobalName + "()";

    /// <summary>Spells a fixed profile wrapper around an imported string options type.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="alias">The alias supplied to <c>ScopedProfile</c>.</param>
    /// <returns>The resulting text.</returns>
    private static string ScopedProfile(CodeWriter w, string alias) =>
        "global::LuminPack.Option.LuminPackFixedOptions<" + w.A(alias) + ">";

    /// <summary>The profile a member's write kernels consume: its own declaration, the containing type's declaration, or the
    /// operation's profile.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The resulting text.</returns>
    internal static string WriteProfile(PayloadRenderModel model, CodeWriter w, RenderMember member) =>
        member.ScopeProfile != null ? ScopedProfile(w, member.ScopeProfile) :
        model.DeclaredProfile != null ? ScopedProfile(w, model.DeclaredProfile) : Profile;

    /// <summary>Separates the built-in length profiles from profiles that may observe or alter a field snapshot.</summary>
    /// <param name="model">The complete positional or versioned contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The receiving operation body.</param>
    /// <param name="append">Emits a segment with or without native field batching.</param>
    private static void AddStorageProfileBranches(PayloadRenderModel model, CodeWriter w, List<string> body, Action<List<string>, bool> append)
    {
        const string standard = "LuminPackUtf16LengthOptions", fallback = "LuminPackDefaultOptions";
        const string matches = "typeof(" + Profile + ") == typeof(";
        string profile = matches + w.A(standard) + ") || " + matches + w.A(fallback) + ") || " +
            matches + ScopedProfile(w, standard) + ") || " + matches + ScopedProfile(w, fallback) + ") || " +
            matches + w.A("LuminPackDeclaredOptions") + ")";
        bool mirrored = model.Mirror != null && model.Members.Any(member => member.Mirrored);
        body.Add("if (" + profile + ")");
        body.Add("{");
        append(body, true);
        body.Add("}");
        body.Add("else");
        body.Add("\tWriteWithProfile(ref writer, value" + (mirrored ? ", ref local" : string.Empty) + ");");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteWithProfile(scoped ref " + w.A("LuminPackWriter") + " writer, " + model.GlobalName + " value" +
            (mirrored ? ", scoped ref " + model.Mirror + " local" : string.Empty) + ")");
        body.Add("{");
        append(body, false);
        body.Add("}");
    }

    /// <summary>The profile a plain string read consumes: a declared profile is passed without the fixed wrapper, as the
    /// member and type projections do.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The profile.</returns>
    internal static string ReadProfile(PayloadRenderModel model, CodeWriter w, RenderMember member) =>
        member.ScopeProfile != null ? w.A(member.ScopeProfile) :
        model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : Profile;

    /// <summary>The declared profile a named member write passes to its plain string stores, or the operation's profile.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The resulting text.</returns>
    private static string DeclarationProfile(PayloadRenderModel model, CodeWriter w, RenderMember member) =>
        member.ScopeProfile != null ? w.A(member.ScopeProfile) :
        model.DeclaredProfile != null ? w.A(model.DeclaredProfile) : Profile;

    /// <summary>Adds the serialization callbacks around a write body: after the value local is bound, and at the end.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="before">The before supplied to <c>AddCallbacks</c>.</param>
    /// <param name="after">The after supplied to <c>AddCallbacks</c>.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> AddCallbacks(List<string> body, string[] before, string[] after)
    {
        if (before.Length == 0 && after.Length == 0)
            return body;
        var result = new List<string>(body.Count + before.Length + after.Length);
        int anchor = body.FindIndex(line => line.StartsWith("var value = input;", StringComparison.Ordinal));
        // A bounded root runs its callbacks after reserving the guarantee, as the completed-buffer lifecycle requires.
        int bounded = body.FindIndex(line => line.StartsWith("int boundedStart", StringComparison.Ordinal));
        if (bounded >= 0)
            anchor = bounded + 1;
        for (int index = 0; index < body.Count; index++)
        {
            result.Add(body[index]);
            if (index == anchor)
                result.AddRange(before);
        }
        if (anchor < 0)
            result.InsertRange(0, before);
        result.AddRange(after);
        return result;
    }

    /// <summary>Adds the deserialization callbacks around a read body; a constructed read places the completion callback inside
    /// its branches itself.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> AddReadCallbacks(PayloadRenderModel model, List<string> body)
    {
        if (!model.HasCallbacks || model.Version == null && !model.IsStruct && model.ConstructorArguments != null)
            return body;
        var result = new List<string>(body.Count + model.ReadBefore.Length + model.ReadAfter.Length);
        result.AddRange(model.ReadBefore);
        result.AddRange(body);
        // A version-tolerant read commits through the receiver it captured before the commit, which an aliased member may
        // replace in the destination; the original raised the after-read callbacks on that receiver.
        bool captured = model.Version != null && body.Any(line => line.StartsWith("var completedInstance = value;", StringComparison.Ordinal));
        foreach (string line in model.ReadAfter)
            result.Add(captured && line.StartsWith("value?.", StringComparison.Ordinal) ? "completedInstance?." + line.Substring("value?.".Length) : line);
        return result;
    }

    /// <summary>Builds the statement lines that read a covered fixed case's Union payload.</summary>
    /// <param name="model">The case's covered model, whose members are all fixed.</param>
    /// <param name="w">The file writer that spells helper aliases.</param>
    /// <param name="fresh">Whether the case is read into a new destination.</param>
    /// <returns>The body lines, opening with the instance local.</returns>
    internal static List<string> UnionReadBody(PayloadRenderModel model, CodeWriter w, bool fresh)
        => AddReadCallbacks(model, ReadBody(model, w, union: true, freshUnion: fresh));

    /// <summary>Emits an existing payload read under the enclosing root's checked input policy.</summary>
    /// <param name="model">The payload with its enclosing policy and nested read redirects applied.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="unionCase">Whether a class read consumes Union payload framing.</param>
    /// <param name="methodPrefix">The helper's prefix for this entry's wire framing.</param>
    internal static void EmitCheckedPayloadRead(PayloadRenderModel model, CodeWriter w, bool unionCase, string methodPrefix = "Read_")
    {
        bool versioned = model.Version != null;
        // Interface Union struct cases call their ordinary owner and retain its record header.
        var body = model.Circle ? CircleBinaryEmitter.ReadBody(model, w, unionCase, expanded: null) :
            versioned ? VersionReadBody(model, w) : model.IsStruct ? StructReadBody(model, w, union: false) :
            Mirrored(model, w, ReadBody(model, w, unionCase), writes: false, unionCase);
        body = InputChecks(model, w, model.Circle ? body : AddReadCallbacks(model, body), read: true);
        w.Line(w.Preserve);
        w.Line("internal static void " + methodPrefix + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + w.A("LuminPackReader") +
            " reader, scoped ref " + model.GlobalName + (model.IsStruct ? " value)" : "? value)"));
        if (model.TypeParameters.Length != 0)
            foreach (string constraint in model.TypeConstraints)
                w.Indented(() => w.Line(constraint));
        w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        if (model.Recursive)
            w.Line(w.A("LuminPackExceptionHelper") + ".EnsureSufficientStack();");
        JsonEmitter.Lines(w, body);
        w.Close();
    }

    /// <summary>Builds a mixed record's write body: the leading members reserved with the header, each inline object in
    /// place, and every following run of reserving members written through its own reservation.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="reserveNativeSpans">Whether newly captured spans may share the reservation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> MixedWriteBody(PayloadRenderModel model, CodeWriter w, bool union, List<string> body, bool reserveNativeSpans)
    {
        var members = model.Members;
        int ends = 0, cursors = 1, spans = 0;
        var names = new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 };
        // The leading members before the first inline object share the header's reservation; without any, the header is a
        // lone record of its own.
        int first = 0;
        while (first < members.Length && !IsInlineSegment(members[first], reserveNativeSpans))
            first++;
        body.Add("ref byte cursor = ref writer.Cursor;");
        if (first == 0)
        {
            if (!union)
                body.Add("cursor = ref writer.WriteRecord(ref cursor, (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");");
        }
        else
            AddProfileSegments(model, w, body, 0, first - 1, header: !union);
        body.Add("writer.Cursor = ref cursor;");
        int index = first;
        while (index < members.Length)
        {
            var member = members[index];
            if (IsTextList(member))
            {
                AddInlineListWrite(body, member, "value", w, names, ref ends, ref spans);
                index++;
                if (index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (IsElementList(member))
            {
                AddInlineElementListWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                if (index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (IsCollectionBlock(member))
            {
                AddCollectionWrite(body, model, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                if (index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            if (member.Kind == RenderMemberKind.Object)
            {
                AddInlineObjectWrite(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                if (!member.Nested.MemberWrite && index < members.Length && !StartsInlineObject(members[index]))
                    body.Add("");
                continue;
            }
            // A formatter-call array is a plain call beside the value members, which keeps no separating blank line.
            if (member.Kind == RenderMemberKind.Array && member.FormatterCall)
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, value." + member.Name + ");");
                index++;
                continue;
            }
            // A type-parameter member is written in place by the generic serializer itself.
            if (member.Kind == RenderMemberKind.Parameter)
            {
                body.Add(w.A("LuminPackSerializer") + ".Serialize" + MemberArguments(member, Profile) + "(ref writer, value." + member.Name + ");");
                index++;
                continue;
            }
            if (member.Kind == RenderMemberKind.ForwardedCollection)
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, value." + member.Name + ");");
                index++;
                continue;
            }
            // A culture-info member expands the shared owner's write body in place, as the legacy direct record does.
            if (member.Kind == RenderMemberKind.CultureInfo)
            {
                AddCultureInfoWrite(body, member, "value", w, names, ref ends);
                index++;
                continue;
            }
            // A framework member writes through its shared formatter owner, which frames its own null record.
            if (member.Kind == RenderMemberKind.Special && !ReserveSpan(member, reserveNativeSpans))
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, value." + member.Name + ");");
                index++;
                continue;
            }
            if (member.Kind == RenderMemberKind.Sequence)
            {
                member.Sequence.WriteMember(body, member, "value", 0, w, names, ref ends, ref cursors, ref spans);
                index++;
                continue;
            }
            int last = index;
            while (last + 1 < members.Length && !IsInlineSegment(members[last + 1], reserveNativeSpans))
                last++;
            body.Add("cursor = ref writer.Cursor;");
            if (last == index)
                AddLoneRecord(model, w, body, member);
            else
                AddProfileSegments(model, w, body, index, last, header: false);
            body.Add("writer.Cursor = ref cursor;");
            index = last + 1;
        }
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Writes one inline object member: a named member operation when the child publishes one, otherwise its marker
    /// and its members in place.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddInlineObjectWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddInlineObjectWrite</c>.</param>
    /// <param name="cursors">The cursors supplied to <c>AddInlineObjectWrite</c>.</param>
    /// <param name="spans">The spans supplied to <c>AddInlineObjectWrite</c>.</param>
    private static void AddInlineObjectWrite(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var child = member.Nested;
        string input = owner + "." + member.Name;
        bool outline = OutlineNestedRecord(child, read: false);
        if (outline)
        {
            string helper = RoleName(names, "WriteNested");
            body.Add(helper + "(ref writer, " + input + ");");
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void " + helper + "(scoped ref " + w.A("LuminPackWriter") + " writer, " + child.GlobalName + "? nestedInput)");
            body.Add("{");
            input = "nestedInput";
        }
        AddNestedObjectWrite(body, child, input, depth, w, names, ref ends, ref cursors, ref spans);
        if (outline)
            body.Add("}");
    }

    /// <summary>Whether the nested record has compound operations or text work beyond the flat inlining budget.</summary>
    /// <param name="child">The nested record's compiled shape.</param>
    /// <param name="read">Whether the reader's inlining budget applies.</param>
    /// <returns>Whether member calls outline the existing child operation.</returns>
    private static bool OutlineNestedRecord(PayloadRenderModel child, bool read) => child.Net8 &&
        !DirectInlining.FlatRecord(child, read) &&
        child.Members.Any(member => member.Kind is RenderMemberKind.Text or RenderMemberKind.Dictionary or RenderMemberKind.List or
            RenderMemberKind.NestedList ||
            member.Nested != null && OutlineNestedRecord(member.Nested, read));

    /// <summary>Writes the existing child operation using the member value already selected by the caller.</summary>
    /// <param name="body">The generated operation body.</param>
    /// <param name="child">The nested record.</param>
    /// <param name="input">The expression holding the selected child.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="names">The occurrence counters for locals.</param>
    /// <param name="ends">The shared counter for exit labels.</param>
    /// <param name="cursors">The shared counter for cursors.</param>
    /// <param name="spans">The shared counter for spans.</param>
    private static void AddNestedObjectWrite(List<string> body, PayloadRenderModel child, string input, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        if (child.MemberWrite)
        {
            string entry = "SerializeMember" + (child.UniqueEntries ? "_" + child.Readable : string.Empty);
            body.Add(w.A("LuminPackSerializer") + "." + entry + "<" + Profile + ">(ref writer, " + input + ");");
            return;
        }
        if (CanCallStreamMemberWriter(child))
        {
            string profile = HasWriteProfile(child) ? "<" + Profile + ">" : string.Empty;
            body.Add(BinaryRoot(w, child) + "RootWrite" + profile +
                "(ref writer, " + input + ");");
            return;
        }
        string name = InlineName(names, "inlineValue", depth);
        body.Add("var " + name + " = " + input + ";");
        body.Add("if (" + name + " is null)");
        body.Add("{");
        body.Add("writer.Cursor = (byte)255;");
        body.Add("writer.Advance(1);");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        body.Add("writer.Cursor = (byte)" + child.Members.Length.ToString(CultureInfo.InvariantCulture) + ";");
        body.Add("writer.Advance(1);");
        AddStreamWrite(body, child, name, depth + 1, w, names, ref ends, ref cursors, ref spans);
        body.Add("}");
    }

    /// <summary>Qualifies calls to the payload's binary root methods.</summary>
    /// <param name="w">The source writer that resolves the serializer alias.</param>
    /// <param name="model">The payload that owns the root container.</param>
    /// <returns>The root container name followed by a member-access dot.</returns>
    private static string BinaryRoot(CodeWriter w, PayloadRenderModel model) =>
        w.A("LuminPackSerializer") + ".LuminRoots_" + model.Readable + ".";

    /// <summary>Checks whether a nested member can call its standalone binary write root.</summary>
    /// <param name="child">The nested payload to write.</param>
    /// <returns>True when the root's stream or fixed stores preserve the member's capacity checks and written prefix on failure.</returns>
    private static bool CanCallStreamMemberWriter(PayloadRenderModel child) =>
        child.TypeParameters.Length == 0 && child.Bounded == null && !child.Recursive &&
        child.Rejection == null && !child.HasDictionary && RenderModelFactory.Covered(child) &&
        (child.StreamRecord || child.Members.All(field => field.IsFixed) && RunEnd(child.Members, 0) == child.Members.Length - 1);

    /// <summary>Expands a culture-info member's shared write body in place: the null/object header and the name string record.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The culture-info member.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit-label counter shared by the whole method.</param>
    private static void AddCultureInfoWrite(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends)
    {
        string local = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " == null)");
        body.Add("{");
        body.Add("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
        body.Add("writer.Advance(1);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("writer.Cursor = unchecked((byte)(0));");
        body.Add("writer.Advance(1);");
        body.Add("writer.Write<" + Profile + ">(" + local + ".Name);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }















    /// <summary>Writes one list member in place: a null count marker, otherwise the element count and payload. A string list
    /// writes its strings through one profiled span call; a scalar list stores the span directly.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="names">The names supplied to <c>AddInlineListWrite</c>.</param>
    /// <param name="ends">The ends supplied to <c>AddInlineListWrite</c>.</param>
    /// <param name="spans">The spans supplied to <c>AddInlineListWrite</c>.</param>
    private static void AddInlineListWrite(List<string> body, RenderMember member, string owner, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int spans)
    {
        string local = RoleName(names, "value");
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(-1);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        if (member.Scalar == "string")
        {
            string span = RoleName(names, "span");
            body.Add("var " + span + " = LuminPackMarshal.GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
            body.Add("writer.EnsureAdditionalCapacity(4);");
            body.Add("writer.Write(" + span + ".Length);");
            body.Add("writer.WriteStrings<" + Profile + ">(" + span + ");");
            body.Add("writer.CheckBuffer();");
        }
        else
            body.Add("writer.Write<" + member.Scalar + ">(LuminPackMarshal.GetListSpan(" + local + "), " + local + ".Count);");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Writes one list of packable classes in place: a null count marker, otherwise the element count and a loop that
    /// expands each element's record body, skipping a null element through its own exit label.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The list member, whose <see cref="RenderMember.Nested"/> model is the element.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth the element body continues.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method; the element label is numbered before the list's.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    private static void AddInlineElementListWrite(List<string> body, RenderMember member, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans)
    {
        var element = member.Nested;
        string local = RoleName(names, "value");
        // The element's null exit claims the first label even though the list's null exit appears first in the text.
        string elementExit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string span = RoleName(names, "span");
        body.Add("{");
        body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
        body.Add("if (" + local + " is null)");
        body.Add("{");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(-1);");
        body.Add("goto " + exit + ";");
        body.Add("}");
        body.Add("");
        body.Add("var " + span + " = LuminPackMarshal.GetListSpan(" + w.A("Unsafe") + ".AsRef(in " + local + "));");
        body.Add("writer.EnsureAdditionalCapacity(4);");
        body.Add("writer.Write(" + span + ".Length);");
        body.Add("for (int i = 0; i < " + span + ".Length; i++)");
        body.Add("{");
        body.Add("writer.CheckBuffer();");
        body.Add("{");
        string input = RoleName(names, "input");
        string value = RoleName(names, "value");
        body.Add("ref readonly " + element.GlobalName + " " + input + " = ref " + span + "[i]!;");
        body.Add("var " + value + " = " + input + ";");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add("writer.Cursor = (byte)255;");
        body.Add("writer.Advance(1);");
        body.Add("goto " + elementExit + ";");
        body.Add("}");
        body.Add("");
        AddElementWrite(body, element, value, depth, w, names, ref ends, ref cursors, ref spans);
        body.Add(elementExit + ":");
        body.Add("\t;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("writer.CheckBuffer();");
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Expands one sequence element's record body in the loop, without its null handling and with camel-cased
    /// snapshots, as a member expansion does.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="element">The element's model.</param>
    /// <param name="owner">The local holding the element.</param>
    /// <param name="depth">The inline nesting depth of the element body.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    /// <param name="state">The writer state the expanded body advances: the record's <c>writer</c>, or the isolated
    /// <c>operationState</c> a sequence owner writes through.</param>
    /// <param name="inlineSpan">Whether to enable inline span for this operation.</param>
    private static void AddElementWrite(List<string> body, PayloadRenderModel element, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans, string state = "writer", bool inlineSpan = false)
    {
        var members = element.Members;
        string count = members.Length.ToString(CultureInfo.InvariantCulture);
        if (members.All(member => member.IsFixed))
        {
            body.Add(state + ".Cursor = (byte)" + count + ";");
            body.Add(state + ".Advance(1);");
            if (members.Length == 1)
            {
                body.Add(state + ".Write(" + owner + "." + members[0].Name + ");");
                return;
            }
            string width = Width(members, 0, members.Length - 1);
            body.Add("if (" + Proof(w, members, 0, members.Length - 1, owner, readOnly: true, coreLayout: true) + ")");
            body.Add("\t" + state + ".Write(ref " + Bytes(w, members[0], owner, readOnly: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            string cursor = CursorName(ref cursors);
            body.Add("ref byte " + cursor + " = ref " + state + ".Cursor;");
            AddScalarStores(w, members, 0, members.Length - 1, cursor, body, owner);
            body.Add(state + ".Advance(" + width + ");");
            body.Add("}");
            body.Add("");
            return;
        }
        if (element.StreamRecord)
        {
            body.Add(state + ".Cursor = (byte)" + count + ";");
            body.Add(state + ".Advance(1);");
            AddStreamWrite(body, element, owner, depth, w, names, ref ends, ref cursors, ref spans);
            return;
        }
        // The element reserves its record as a whole; its members keep the member order of the template.
        string elementCursor = CursorName(ref cursors);
        body.Add("ref byte " + elementCursor + " = ref " + state + ".Cursor;");
        body.Add("// Fields: " + string.Join(", ", members.Select(member => member.Name)));
        var terms = new List<string> { "1" };
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(element, index, camel: true);
            if (member.Kind == RenderMemberKind.Fixed)
                terms.Add(member.Width);
            else if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("string " + snapshot + " = " + owner + "." + member.Name + ";");
                terms.Add(state + ".RecordBound<" + Profile + ">(" + snapshot + ")");
            }
            else
            {
                body.Add("var " + snapshot + " = " + w.A("LuminPackWriter") + ".Capture(" + owner + "." + member.Name + ");");
                terms.Add(snapshot + ".Bound");
            }
        }
        body.Add(elementCursor + " = ref " + state + ".ReserveRecord(ref " + elementCursor + ", checked(" + string.Join(" + ", terms) + "));");
        body.Add(elementCursor + " = (byte)" + count + ";");
        body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", 1);");
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int last = RunEnd(members, index);
                if (last == index)
                {
                    body.Add(elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReserved<" + member.Scalar + ">(ref " + elementCursor + ", " +
                            owner + "." + member.Name + ");");
                    continue;
                }
                string runWidth = Width(members, index, last);
                body.Add("if (" + Proof(w, members, index, last, owner, readOnly: true, coreLayout: true) + ")");
                body.Add("\t" + elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref " + elementCursor + ", ref " +
                    Bytes(w, member, owner, readOnly: true) + ", " + runWidth + ");");
                body.Add("else");
                body.Add("{");
                AddScalarStores(w, members, index, last, elementCursor, body, owner);
                body.Add(elementCursor + " = ref " + w.A("Unsafe") + ".Add(ref " + elementCursor + ", " + runWidth + ");");
                body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            body.Add("// Field: " + member.Name);
            if (member.Kind == RenderMemberKind.Text)
                body.Add(elementCursor + " = ref " + state + ".WriteReservedRecordString<" + Profile + ">(ref " + elementCursor + ", " + Snapshot(element, index, camel: true) + ");");
            else if (inlineSpan || member.ReservableSpan)
            {
                string snapshot = Snapshot(element, index, camel: true);
                body.Add(elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanInline(ref " + elementCursor +
                    ", " + snapshot + ".IsNull, " + snapshot + ".Elements);");
            }
            else
                body.Add(elementCursor + " = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref " + elementCursor + ", " + Snapshot(element, index, camel: true) + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
        body.Add(state + ".Cursor = ref " + elementCursor + ";");
    }

    /// <summary>Expands one sequence element in place: the element local, its null marker and the element's record body.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="element">The element's render model.</param>
    /// <param name="source">The expression the element is read from.</param>
    /// <param name="elementExit">The exit label of the element's null marker.</param>
    /// <param name="state">The writer state the element body advances.</param>
    /// <param name="depth">The inline nesting depth of the element body.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="spans">The span counter shared by the whole method.</param>
    /// <param name="inlineSpan">Whether to keep raw span writes in the already reserved element body.</param>
    internal static void AddSequenceElementWrite(List<string> body, PayloadRenderModel element, string source, string elementExit,
        string state, int depth, CodeWriter w, Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans, bool inlineSpan = false)
    {
        string input = RoleName(names, "input");
        string value = RoleName(names, "value");
        body.Add("{");
        body.Add("ref readonly " + element.GlobalName + " " + input + " = ref " + source + ";");
        body.Add("var " + value + " = " + input + ";");
        body.Add("if (" + value + " is null)");
        body.Add("{");
        body.Add(state + ".Cursor = (byte)255;");
        body.Add(state + ".Advance(1);");
        body.Add("goto " + elementExit + ";");
        body.Add("}");
        body.Add("");
        AddElementWrite(body, element, value, depth, w, names, ref ends, ref cursors, ref spans, state, inlineSpan);
        body.Add(elementExit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Expands one sequence element's read in place: the element's null marker and its record body.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="element">The element's render model.</param>
    /// <param name="local">The element local the record body fills.</param>
    /// <param name="reference">The declaration that binds the element local inside the block, or null when the caller already
    /// declared it.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="strings">The mirrored string local counter shared by the whole method.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="clearOnNull">Whether the local aliases a reused slot that a null marker must clear.</param>
    /// <param name="pairFields">Whether this is the ordinary list slot reader.</param>
    internal static void AddSequenceElementRead(List<string> body, PayloadRenderModel element, string local, string reference,
        CodeWriter w, Dictionary<string, int> names, ref int cursors, ref int strings, ref int ends, bool clearOnNull = false, bool pairFields = false)
    {
        string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
        ends++;
        string instance = RoleName(names, "instance");
        body.Add("{");
        if (reference != null)
            body.Add(reference);
        body.Add("if (" + w.A("LuminPackInputPolicy") + ".ReadNullMarker(ref reader, " +
            element.Members.Length.ToString(CultureInfo.InvariantCulture) + "))");
        if (clearOnNull)
        {
            body.Add("{");
            body.Add("\t" + local + " = default;");
        }
        body.Add("\tgoto " + exit + ";");
        if (clearOnNull)
            body.Add("}");
        body.Add("var " + instance + " = " + local + " ??= " + NewInstance(element) + ";");
        AddStreamRead(body, element, instance, 0, prove: true, w, names, ref cursors, ref strings, ref ends, pairFields);
        body.Add(exit + ":");
        body.Add("\t;");
        body.Add("}");
    }

    /// <summary>Spells the method-wide cursor local: the first is <c>cursor</c>, the following ones carry their ordinal.</summary>
    /// <param name="cursors">The cursors supplied to <c>CursorName</c>.</param>
    /// <returns>The resulting text.</returns>
    private static string CursorName(ref int cursors)
        => "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a lone member through the cursor without reserving first, as a one-member record does.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    private static void AddLoneRecord(PayloadRenderModel model, CodeWriter w, List<string> body, RenderMember member)
    {
        // The template keeps no member comment on a lone group whose member owns a profile boundary.
        if (member.ScopeProfile == null)
            body.Add("// Fields: " + member.Name);
        if (member.Kind == RenderMemberKind.Fixed)
            body.Add("cursor = ref writer.WriteRecord(ref cursor, value." + member.Name + ");");
        else if (member.Kind == RenderMemberKind.Text)
            body.Add("cursor = ref writer.WriteRecordString<" + WriteProfile(model, w, member) + ">(ref cursor, value." + member.Name + ");");
        else if (member.Kind == RenderMemberKind.Dictionary)
            body.Add("cursor = ref writer.WriteRecordPairs(ref cursor, value." + member.Name + ");");
        else
            body.Add("cursor = ref writer.WriteRecordSpan(ref cursor, value." + member.Name + ");");
    }

    /// <summary>Writes a leading or following run of reserving members, splitting it at each member-level profile boundary.
    /// A group that is alone and carries no header stores through its one-member record kernel, as the template does.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="first">The first supplied to <c>AddProfileSegments</c>.</param>
    /// <param name="last">The last supplied to <c>AddProfileSegments</c>.</param>
    /// <param name="header">Whether to enable header for this operation.</param>
    private static void AddProfileSegments(PayloadRenderModel model, CodeWriter w, List<string> body, int first, int last, bool header)
    {
        var members = model.Members;
        int start = first;
        bool withHeader = header;
        while (start <= last)
        {
            int end = start;
            string scope = members[start].ScopeProfile ?? string.Empty;
            while (end + 1 <= last && (members[end + 1].ScopeProfile ?? string.Empty) == scope)
                end++;
            if (start == end && !withHeader)
                AddLoneRecord(model, w, body, members[start]);
            else
                AddReservedSegment(model, w, body, start, end, withHeader);
            withHeader = false;
            start = end + 1;
        }
    }

    /// <summary>Writes a span of a mixed record through one reservation, optionally including the record header byte.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="first">The first supplied to <c>AddReservedSegment</c>.</param>
    /// <param name="last">The last supplied to <c>AddReservedSegment</c>.</param>
    /// <param name="header">Whether to enable header for this operation.</param>
    /// <param name="camel">The camel supplied to <c>AddReservedSegment</c>.</param>
    private static void AddReservedSegment(PayloadRenderModel model, CodeWriter w, List<string> body, int first, int last, bool header,
        bool? camel = null)
    {
        var members = model.Members;
        var names = new List<string>();
        for (int index = first; index <= last; index++)
            names.Add(members[index].Name);
        body.Add("// Fields: " + string.Join(", ", names));
        var terms = new List<string>();
        bool inlineSpans = false;
        if (header)
            terms.Add("1");
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            string snapshot = Snapshot(model, index, camel: camel ?? model.InlineObject);
            if (member.Kind == RenderMemberKind.Fixed)
                terms.Add(member.Width);
            else if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("string " + snapshot + " = value." + member.Name + ";");
                terms.Add("writer.RecordBound<" + WriteProfile(model, w, member) + ">(" + snapshot + ")");
            }
            else
            {
                body.Add("var " + snapshot + " = " + w.A("LuminPackWriter") + ".Capture(value." + member.Name + ");");
                terms.Add(snapshot + ".Bound");
                inlineSpans |= member.ReservableSpan;
            }
        }
        body.Add("cursor = ref writer.ReserveRecord(ref cursor, checked(" + string.Join(" + ", terms) + "));");
        if (header)
        {
            body.Add("cursor = (byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ";");
            body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, 1);");
        }
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int end = RunEnd(members, index);
                if (end > last)
                    end = last;
                if (end == index)
                {
                    body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReserved<" + member.Scalar + ">(ref cursor, value." + member.Name + ");");
                    continue;
                }
                string width = Width(members, index, end);
                body.Add("if (" + Proof(w, members, index, end, "value", readOnly: true, coreLayout: true) + ")");
                body.Add("\tcursor = ref " + w.A("LuminPackWriter") + ".WriteReservedBlock(ref cursor, ref " + Bytes(w, member, "value", readOnly: true) + ", " +
                    width + ");");
                body.Add("else");
                body.Add("{");
                AddScalarStores(w, members, index, end, "cursor", body);
                body.Add("cursor = ref " + w.A("Unsafe") + ".Add(ref cursor, " + width + ");");
                body.Add("}");
                body.Add("");
                index = end;
                continue;
            }
            body.Add("// Field: " + member.Name);
            string written = Snapshot(model, index, camel: camel ?? model.InlineObject);
            if (member.Kind == RenderMemberKind.Text)
                body.Add("cursor = ref writer.WriteReservedRecordString<" + WriteProfile(model, w, member) + ">(ref cursor, " + written + ");");
            else if (member.Kind == RenderMemberKind.Dictionary)
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReserved(ref cursor, " + written + ");");
            else if (inlineSpans)
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanInline(ref cursor, " + written + ".IsNull, " + written + ".Elements);");
            else
                body.Add("cursor = ref " + w.A("LuminPackWriter") + ".WriteReservedSpanRecord(ref cursor, " + written + ");");
        }
        if (body[body.Count - 1].Length != 0)
            body.Add("");
    }

    /// <summary>Builds a stream record's write body: the member count, then every member, with inline objects writing their own
    /// count and members in place.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> StreamWriteBody(PayloadRenderModel model, CodeWriter w, bool union, List<string> body)
    {
        int first = 0;
        if (!union)
        {
            var members = model.Members;
            string count = members.Length.ToString(CultureInfo.InvariantCulture);
            if (model.Net8 && model.ScalarHeader && !model.HasCallbacks && model.Bounded == null && model.Mirror == null &&
                members.Length > 1 && members[0].IsFixed && !members[1].IsFixed && !members[0].Volatile &&
                !members[0].Mirrored && !members[0].Property && members[0].Size > 0)
            {
                body.Add("writer.WriteObjectHeader(" + count + ", in value." + members[0].Name + ");");
                first = 1;
            }
            else
            {
                body.Add("writer.Cursor = (byte)" + count + ";");
                body.Add("writer.Advance(1);");
            }
        }
        int ends = 0, cursors = 0, spans = 0;
        AddStreamWrite(body, model, "value", 0, w, new Dictionary<string, int>(StringComparer.Ordinal) { ["value"] = 1, ["input"] = 1 }, ref ends, ref cursors, ref spans, first);
        ReduceInlineNames(body);
        return body;
    }

    /// <summary>Writes the members of a stream record or of one of its inline objects.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="model">The record written at this depth.</param>
    /// <param name="owner">The expression holding the record being written.</param>
    /// <param name="depth">The inline nesting depth, which names the inline locals.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method; the root's own value local holds the
    /// first occurrence of the list alias name.</param>
    /// <param name="ends">The exit label counter shared by the whole method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method; a mixed record's own cursor holds the first
    /// occurrence.</param>
    /// <param name="spans">The string list span counter shared by the whole method.</param>
    /// <param name="first">The first member after any scalar already written with the object marker.</param>
    private static void AddStreamWrite(List<string> body, PayloadRenderModel model, string owner, int depth, CodeWriter w,
        Dictionary<string, int> names, ref int ends, ref int cursors, ref int spans, int first = 0)
    {
        var members = model.Members;
        for (int index = first; index < members.Length; index++)
        {
            var member = members[index];
            // A string inside an inline object writes with the operation's profile, as the old pipeline's inline object
            // emits it: writer.Write<TLuminProfile>(inlineValue.Name); (ProbeMixed.O1).
            if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("writer.Write<" + Profile + ">(" + owner + "." + member.Name + ");");
                continue;
            }
            // A type-parameter member is written by the generic serializer itself.
            if (member.Kind == RenderMemberKind.Parameter)
            {
                body.Add(w.A("LuminPackSerializer") + ".Serialize" + MemberArguments(member, Profile) + "(ref writer, " + owner + "." + member.Name + ");");
                continue;
            }
            if (member.Kind == RenderMemberKind.Object)
            {
                AddInlineObjectWrite(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                // An inline object leaves a blank line before a following field unless that field opens another inline object;
                // a named member write stays a plain call and keeps no blank, as the old body's continue does.
                if (!member.Nested.MemberWrite && index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                    body.Add("");
                continue;
            }
            // A fused dictionary inside an inline object writes its original operation in place.
            if (member.Kind == RenderMemberKind.Dictionary)
            {
                AddDictionaryInline(body, model, member, owner, w, names, ref ends);
                if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                    body.Add("");
                continue;
            }
            // An array member writes through its generated formatter, which keeps its own framing and profile.
            if (member.Kind == RenderMemberKind.Array)
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, " + owner + "." + member.Name + ");");
                continue;
            }
            // A culture-info member expands the shared owner's write body in place, as the legacy direct record does.
            if (member.Kind == RenderMemberKind.CultureInfo)
            {
                AddCultureInfoWrite(body, member, owner, w, names, ref ends);
                continue;
            }
            // A framework member writes through its shared formatter owner, which frames its own null record.
            if (member.Kind == RenderMemberKind.Special)
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, " + owner + "." + member.Name + ");");
                continue;
            }
            if (member.Kind == RenderMemberKind.ForwardedCollection)
            {
                body.Add(member.BinaryFormatter + ".Write<" + WriteProfile(model, w, member) + ">(ref writer, " + owner + "." + member.Name + ");");
                continue;
            }
            if (member.Kind == RenderMemberKind.Sequence)
            {
                member.Sequence.WriteMember(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                continue;
            }
            if (member.Kind == RenderMemberKind.List)
            {
                if (member.Scalar == "string")
                {
                    AddInlineListWrite(body, member, owner, w, names, ref ends, ref spans);
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
                else if (member.Nested != null)
                {
                    AddInlineElementListWrite(body, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
                else
                {
                    string local = RoleName(names, "value");
                    string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
                    ends++;
                    body.Add("{");
                    body.Add("ref readonly " + member.TypeName + " " + local + " = ref " + owner + "." + member.Name + ";");
                    body.Add("if (" + local + " is null)");
                    body.Add("{");
                    body.Add("writer.EnsureAdditionalCapacity(4);");
                    body.Add("writer.Write(-1);");
                    body.Add("goto " + exit + ";");
                    body.Add("}");
                    body.Add("");
                    body.Add("writer.Write<" + member.Scalar + ">(LuminPackMarshal.GetListSpan(" + local + "), " + local + ".Count);");
                    body.Add(exit + ":");
                    body.Add("\t;");
                    body.Add("}");
                    if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                        body.Add("");
                }
                continue;
            }
            if (IsCollectionBlock(member))
            {
                AddCollectionWrite(body, model, member, owner, depth, w, names, ref ends, ref cursors, ref spans);
                if (index + 1 < members.Length && !StartsInlineObject(members[index + 1]))
                    body.Add("");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                body.Add("writer.Write(" + owner + "." + member.Name + ");");
                continue;
            }
            string width = Width(members, index, last);
            body.Add("if (" + Proof(w, members, index, last, owner, readOnly: true, coreLayout: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, members[index], owner, readOnly: true) + ", " + width + ");");
            body.Add("else");
            body.Add("{");
            string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
            body.Add("ref byte " + cursor + " = ref writer.Cursor;");
            AddScalarStores(w, members, index, last, cursor, body, owner);
            body.Add("writer.Advance(" + width + ");");
            body.Add("}");
            index = last;
        }
    }

    /// <summary>Spells a type-parameter member's payload call: its own parameter, then the operation's profile.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="profile">The compiled serialization profile or its generated type name.</param>
    /// <returns>The resulting text.</returns>
    private static string MemberArguments(RenderMember member, string profile) => "<" + member.Scalar + ", " + profile + ">";

    /// <summary>Adds the reset guard of a generic payload member, which depends on what its declared type is.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="member">The payload member.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="reader">The reader expression the payload call reads from.</param>
    /// <param name="target">The destination storage expression.</param>
    private static void AddParameterReset(List<string> body, RenderMember member, CodeWriter w, string reader, string target)
    {
        // A bare parameter may hold a value or a reference, so the runtime formatter decides whether the member is reset.
        if (member.BareParameter)
        {
            if (!member.ValueTypeParameter)
            {
                body.Add("if (" + w.A("LuminPackSerializer") + ".FormatterDispatch.LuminPackShouldResetMember<" + member.Scalar + ">(ref " + reader + "))");
                body.Add("\t" + target + " = default !;");
            }
        }
        // A reference payload writes a null object marker the read peeks at, while a collection owns its own null handling.
        else if (member.NullObjectGuard)
        {
            body.Add("if (" + reader + ".PeekIsNullObject())");
            body.Add("\t" + target + " = default !;");
        }
    }

    /// <summary>Spells an inline object's local the way the template numbers its depth-suffixed role local.</summary>
    /// <param name="names">The names supplied to <c>InlineName</c>.</param>
    /// <param name="role">The role supplied to <c>InlineName</c>.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <returns>The resulting text.</returns>
    private static string InlineName(Dictionary<string, int> names, string role, int depth)
    {
        string raw = role + depth.ToString(CultureInfo.InvariantCulture);
        int count = names.TryGetValue(raw, out int seen) ? seen + 1 : 1;
        names[raw] = count;
        return count == 1 ? raw : raw + count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Spells a role local without a depth suffix, as the expanded list write names the field alias it holds.</summary>
    /// <param name="names">The names supplied to <c>RoleName</c>.</param>
    /// <param name="role">The role supplied to <c>RoleName</c>.</param>
    /// <returns>The resulting text.</returns>
    internal static string RoleName(Dictionary<string, int> names, string role)
    {
        int count = names.TryGetValue(role, out int seen) ? seen + 1 : 1;
        names[role] = count;
        return count == 1 ? role : role + count.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a member's write opens with a local declaration, which the presentation keeps directly under the
    /// preceding block instead of separating it with a blank line.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input satisfies the starts inline object check.</returns>
    private static bool StartsInlineObject(RenderMember member) => member.Kind == RenderMemberKind.Object && !member.Nested.MemberWrite;

    /// <summary>Whether a member is a list of strings, which writes its payload in place instead of entering a reservation.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input is text list.</returns>
    private static bool IsTextList(RenderMember member) => member.Kind == RenderMemberKind.List && member.Scalar == "string";

    /// <summary>Whether a member is a list whose elements expand their record body in place.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input is element list.</returns>
    private static bool IsElementList(RenderMember member) => member.Kind == RenderMemberKind.List && member.Nested != null;

    /// <summary>Whether a member is written in its own block rather than inside the surrounding reservation.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="reserveNativeSpans">Whether newly captured spans may share the reservation.</param>
    /// <returns>Whether the input is inline segment.</returns>
    private static bool IsInlineSegment(RenderMember member, bool reserveNativeSpans) => !ReserveSpan(member, reserveNativeSpans) && (member.Kind == RenderMemberKind.Object || IsTextList(member) ||
        IsElementList(member) || IsCollectionBlock(member) || member.Kind == RenderMemberKind.Array && member.FormatterCall ||
        member.Kind is RenderMemberKind.Parameter or RenderMemberKind.ForwardedCollection or RenderMemberKind.CultureInfo or RenderMemberKind.Special or RenderMemberKind.Sequence);

    /// <summary>Checks whether this profile may capture the forwarded span alongside adjacent fields.</summary>
    /// <param name="member">The member being emitted.</param>
    /// <param name="reserveNativeSpans">Whether the profile's properties are constant and side-effect free.</param>
    /// <returns>Whether this segment may share a reservation.</returns>
    private static bool ReserveSpan(RenderMember member, bool reserveNativeSpans) =>
        member.ReservableSpan && (reserveNativeSpans || member.OriginalSpanReservation);

    /// <summary>Whether a member opens a block in place, whose following member starts without a separating blank line.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>Whether the input is inline block.</returns>
    private static bool IsInlineBlock(RenderMember member) =>
        member.Kind == RenderMemberKind.Object && !member.Nested.MemberWrite || IsTextList(member) || IsElementList(member);

    /// <summary>Drops the depth suffix of an inline local whose purely alphabetic stem is otherwise unused, as the presentation
    /// does.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    private static void ReduceInlineNames(List<string> body)
    {
        var pattern = new System.Text.RegularExpressions.Regex(@"\b(inlineValue|inlineRead)\d+\b");
        var order = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in body)
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(line))
                if (names.Add(match.Value))
                    order.Add(match.Value);
        // The first name that can claim a stem takes it; a later name whose stem is already taken keeps its digits.
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in order)
        {
            string stem = new string(name.TakeWhile(char.IsLetter).ToArray());
            if (stem != name && !names.Contains(stem) && claimed.Add(stem))
                replacements.Add(name, stem);
        }
        if (replacements.Count == 0)
            return;
        pattern = new System.Text.RegularExpressions.Regex(@"\b(" + string.Join("|", replacements.Keys) + @")\b");
        for (int index = 0; index < body.Count; index++)
            body[index] = pattern.Replace(body[index], match => replacements[match.Value]);
    }

    /// <summary>Projects an all-fixed Union write onto its returning cursor. The managed path combines the tag and
    /// payload widths as an offset and rebases it after any capacity reservation.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="original">The original supplied to <c>FixedUnionBody</c>.</param>
    /// <param name="managedCursor">Whether to enable managed cursor for this operation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> FixedUnionBody(CodeWriter w, List<string> original, bool managedCursor)
    {
        string unsafeType = w.A("Unsafe");
        string destination = managedCursor ? unsafeType + ".Add(ref fixedUnionCursor, fixedUnionOffset)" :
            unsafeType + ".Add(ref " + w.A("MemoryMarshal") + ".GetReference(writer.Buffer), unchecked((nint)(uint)(fixedUnionIndex)))";
        string Advance(string width) => managedCursor
            ? "fixedUnionOffset += " + width + ";"
            : "fixedUnionIndex += " + width + ";";
        var body = new List<string>
        {
            managedCursor ? "ref byte fixedUnionCursor = ref initialCursor;" : "int fixedUnionIndex = initialIndex;",
            "if (unionTag.HasValue)",
            "{",
            w.A("LuminPackWriter") + ".Write(ref " + (managedCursor ? "fixedUnionCursor" : destination) + ", unionTag.GetValueOrDefault());",
            Advance("4"),
            "}",
            "",
        };
        if (managedCursor)
            body.Insert(1, "int fixedUnionOffset = 0;");
        const string block = "\twriter.Write(ref ";
        const string advance = "writer.Advance(";
        const string write = "writer.Write(";
        foreach (string line in original)
        {
            if (line.StartsWith("\twriter.EnsureAdditionalCapacity(", StringComparison.Ordinal))
            {
                body.Add("{");
                body.Add(managedCursor ? "writer.CommitRecords(ref " + destination + ");" : "writer.Position = fixedUnionIndex;");
                body.Add(line.Substring(1));
                if (managedCursor)
                {
                    body.Add("fixedUnionCursor = ref writer.Cursor;");
                    body.Add("fixedUnionOffset = 0;");
                }
                body.Add("}");
                body.Add("");
            }
            else if (line.StartsWith(block, StringComparison.Ordinal))
            {
                // The block source and width are separated by the last top-level comma.
                string arguments = line.Substring(block.Length, line.Length - block.Length - 2);
                int comma = TopLevelComma(arguments);
                string source = arguments.Substring(0, comma), width = arguments.Substring(comma + 2);
                body.Add("{");
                body.Add(w.A("LuminPackMarshal") + ".CopyBlockUnaligned(ref " + destination + ", ref " + source + ", (uint)(" + width + "));");
                body.Add(Advance(width));
                body.Add("}");
            }
            else if (line == "ref byte cursor = ref writer.Cursor;")
                body.Add("ref byte cursor = ref " + destination + ";");
            else if (line.StartsWith(advance, StringComparison.Ordinal))
                body.Add(Advance(line.Substring(advance.Length, line.Length - advance.Length - 2)));
            else if (line.StartsWith(write, StringComparison.Ordinal))
                body.Add(Advance(w.A("LuminPackWriter") + ".Write(ref " + destination + ", " + line.Substring(write.Length, line.Length - write.Length - 1)));
            else
                body.Add(line);
        }
        if (body[body.Count - 1] == "}")
            body.Add("");
        body.Add(managedCursor ? "return ref " + destination + ";" : "return fixedUnionIndex;");
        return body;
    }

    /// <summary>Finds the last comma outside parentheses and angle brackets.</summary>
    /// <param name="text">The text supplied to <c>TopLevelComma</c>.</param>
    /// <returns>The resulting <c>int</c>.</returns>
    private static int TopLevelComma(string text)
    {
        int depth = 0, found = -1;
        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (c is '(' or '<')
                depth++;
            else if (c is ')' or '>')
                depth--;
            else if (c == ',' && depth == 0)
                found = index;
        }
        return found;
    }

    /// <summary>Renders only the root-private layout mirror of a payload whose binary operations another owner renders.</summary>
    /// <param name="model">The payload.</param>
    /// <param name="aliases">The alias mask the consumer leaves available.</param>
    /// <returns>The mirror file, or an empty string when the payload declares no mirror.</returns>
    /// <remarks>A struct with a storage plan writes its binary operations through the unmanaged owner, which does not declare the
    /// mirror its JSON operations reach private storage through.</remarks>
    internal static string MirrorSource(PayloadRenderModel model, int aliases)
    {
        if (model.Mirrors.Length == 0 && model.Mirror == null)
            return string.Empty;
        using var w = new CodeWriter(aliases) { Scoped = model.Net8 };
        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class LuminPackSerializer");
        w.Open();
        if (model.Mirrors.Length != 0)
            StorageMirrorEmitter.Emit(w, model.Mirrors);
        else
        {
            if (model.LayoutAttribute != null)
                w.Line(model.LayoutAttribute);
            w.Line(w.Preserve);
            // A struct is reinterpreted in place, so its mirror is a struct, as in the original layout declarations.
            w.Line((model.IsStruct ? "private struct " : "private class ") + model.Mirror);
            if (model.TypeConstraintText.Length != 0)
                w.Indented(() => w.Line(model.TypeConstraintText));
            w.Open();
            MirrorFields(w, model.MirrorFields, model.MirrorOffsets);
            w.Close();
        }
        w.Close();
        w.Close();
        return PublicationLayout.LayOutLines(w.Complete(false));
    }

    /// <summary>Writes a mirror's field declarations and any explicit-layout offsets.</summary>
    /// <param name="w">The file writer.</param>
    /// <param name="fields">The mirror field declarations in storage order.</param>
    /// <param name="offsets">The explicit layout offsets, with -1 for an unspecified offset, or null for sequential layout.</param>
    internal static void MirrorFields(CodeWriter w, string[] fields, int[] offsets)
    {
        for (int index = 0; index < fields.Length; index++)
        {
            if (offsets != null && index < offsets.Length && offsets[index] >= 0)
                w.Line("[global::System.Runtime.InteropServices.FieldOffset(" + offsets[index].ToString(CultureInfo.InvariantCulture) + ")]");
            w.Line("internal " + fields[index] + ";");
        }
    }

    /// <summary>Reaches a mirrored type's storage through its layout mirror: the mirror view is declared after the null handling
    /// or the instance, and every member access goes through it.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="writes">Whether to enable writes for this operation.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> Mirrored(PayloadRenderModel model, CodeWriter w, List<string> body, bool writes, bool union)
    {
        // A pool mirror only declares the layout for reuse; public fields keep their direct access.
        if (model.Mirror == null || !model.Members.Any(member => member.Mirrored))
            return body;
        // The template already spelled these helpers through imported namespaces, so they register no alias.
        const string marshal = "LuminPackMarshal";
        // Public fields stay named directly; only auto-property storage goes through the mirror.
        string pattern = @"\b" + (writes ? "value" : "instance") + @"\.(" + string.Join("|", model.Members.Where(member => member.Mirrored)
            .Select(member => System.Text.RegularExpressions.Regex.Escape(member.Name))) + @")\b";
        var result = new List<string>(body.Count + 1);
        foreach (string line in body)
            result.Add(System.Text.RegularExpressions.Regex.Replace(line, pattern, "local.$1"));
        if (writes)
        {
            // Bind the Local view after the complete null branch and any leading capacity guarantee.
            int at = 1;
            int nullTest = result.FindIndex(line => line == "if (value is null)");
            if (nullTest >= 0)
            {
                int nesting = 0;
                for (int index = nullTest + 1; index < result.Count; index++)
                {
                    if (result[index] == "{")
                        nesting++;
                    else if (result[index] == "}" && --nesting == 0)
                    {
                        at = index + 1;
                        break;
                    }
                }
            }
            result.Insert(at, "ref var local = ref " + marshal + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref Unsafe.AsRef(in value));");
        }
        else
        {
            // A constructed read declares its mirror views itself, so it has no instance local to attach one to.
            int anchor = result.FindIndex(line => line.StartsWith("var instance = ", StringComparison.Ordinal));
            if (anchor >= 0)
                result.Insert(anchor + 1, "var local = " + marshal + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref instance);");
        }
        return result;
    }

    /// <summary>Builds the named member write: the original stream writes of an object nested in another record.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> MemberWriteBody(PayloadRenderModel model, CodeWriter w)
    {
        var members = model.Members;
        var body = new List<string>
        {
            "var value = input;",
            "if (value is null)",
            "{",
            "writer.Write((byte)255);",
            "return;",
            "}",
            "",
            "writer.Write((byte)" + members.Length.ToString(CultureInfo.InvariantCulture) + ");",
        };
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == RenderMemberKind.Text)
            {
                body.Add("writer.Write<" + DeclarationProfile(model, w, member) + ">(value." + member.Name + ");");
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                body.Add("writer.Write(value." + member.Name + ");");
                continue;
            }
            string widthText = Width(members, index, last);
            body.Add("if (" + Proof(w, members, index, last, "value", readOnly: true, coreLayout: true) + ")");
            body.Add("\twriter.Write(ref " + Bytes(w, member, "value", readOnly: true) + ", " + widthText + ");");
            body.Add("else");
            body.Add("{");
            body.Add("ref byte cursor = ref writer.Cursor;");
            AddScalarStores(w, members, index, last, "cursor", body);
            body.Add("writer.Advance(" + widthText + ");");
            body.Add("}");
            body.Add("");
            index = last;
        }
        while (body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        return body;
    }

    /// <summary>Splits a reserved record write into the proven path: a record body reached directly when the record provably
    /// fits, and through the original reservation on excluded profiles and in the cold path.</summary>
    /// <param name="model">The covered type, whose variable members are strings.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="original">The reserved record write.</param>
    /// <param name="union">Whether the record is a Union payload without the null marker and member count.</param>
    /// <returns>The proven operation body.</returns>
    private static List<string> ProvenWriteBody(PayloadRenderModel model, CodeWriter w, List<string> original, bool union)
    {
        int cursorLine = original.IndexOf("ref byte cursor = ref writer.Cursor;");
        int reserve = original.FindIndex(line => line.StartsWith("cursor = ref writer.ReserveRecord(", StringComparison.Ordinal));
        var prelude = original.GetRange(0, cursorLine);
        int captureStart = cursorLine + 1;
        string comment = null;
        if (captureStart < reserve && original[captureStart].StartsWith("// Fields:", StringComparison.Ordinal))
            comment = original[captureStart++];
        var captures = original.GetRange(captureStart, reserve - captureStart);
        var record = original.GetRange(reserve + 1, original.Count - reserve - 1);
        var names = new List<string>();
        for (int index = 0; index < model.Members.Length; index++)
            if (model.Members[index].Kind == RenderMemberKind.Text)
                names.Add(Snapshot(model, index, camel: model.HasCallbacks || model.Bounded != null));
        string type = model.GlobalName + "?";
        string arguments = string.Join(", ", new[] { "ref writer", "value" }.Concat(names));
        string unsafeName = w.A("Unsafe"), standard = w.A("LuminPackUtf16LengthOptions"), fallback = w.A("LuminPackDefaultOptions");
        string recordProfile = model.DeclaredProfile != null ? ScopedProfile(w, model.DeclaredProfile) : Profile;
        string profiles = "(typeof(" + recordProfile + ") == typeof(" + standard + ") || typeof(" + recordProfile + ") == typeof(" + fallback + ") || typeof(" + recordProfile +
            ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<" + standard + ">) || typeof(" + recordProfile +
            ") == typeof(global::LuminPack.Option.LuminPackFixedOptions<" + fallback + ">))";
        // The complete bound in 64-bit arithmetic; a string record is its length prefix and UTF-16 payload.
        var terms = new List<string>();
        if (!union)
            terms.Add("1");
        int text = 0;
        foreach (var member in model.Members)
        {
            if (member.Kind != RenderMemberKind.Text)
            {
                terms.Add(member.Width);
                continue;
            }
            string name = names[text++];
            terms.Add("(4L + (" + name + " is null ? 0L : (long)" + name + ".Length * 2))");
        }
        string bound = "(long)(" + terms[0] + ")" + string.Concat(terms.Skip(1).Select(term => " + " + term));
        string inlineAttribute = w.MethodImpl("AggressiveInlining");

        var body = new List<string>
        {
            "if (!writer.TryGetObjectRecordRoom(" + profiles + ", out int objectRecordRoom))",
            "{",
            "WriteChecked(ref writer, input);",
            "return;",
            "}",
            "",
            "if (input is not null)",
            "{",
            "var value = input;",
        };
        body.AddRange(captures);
        body.Add("long objectRecordBound = " + bound + ";");
        body.Add("if (objectRecordRoom >= objectRecordBound)");
        body.Add("{");
        body.Add("WriteRecord(" + arguments + ");");
        body.Add("return;");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("WriteOriginalObject(ref writer, input);");
        body.Add(inlineAttribute);
        body.Add("static void WriteRecord(scoped ref LuminPackWriter writer, " + type + " value" + string.Concat(names.Select(name => ", string " + name)) + ")");
        body.Add("{");
        body.Add("ref byte cursor = ref writer.Cursor;");
        if (comment != null)
            body.Add(comment);
        body.AddRange(record);
        body.Add("}");
        body.Add("");
        body.Add(inlineAttribute);
        body.Add("static void WriteChecked(scoped ref LuminPackWriter writer, " + type + " input)");
        body.Add("{");
        body.AddRange(prelude);
        body.Add("ref byte cursor = ref writer.Cursor;");
        body.AddRange(captures);
        body.Add(original[reserve]);
        body.Add("writer.Cursor = ref cursor;");
        body.Add("WriteRecord(" + arguments + ");");
        body.Add("}");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void WriteOriginalObject(scoped ref LuminPackWriter writer, " + type + " input) => WriteChecked(ref writer, input);");
        return body;
    }

    /// <summary>Stores a run of fixed fields at their packed offsets from a cursor.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="members">The members supplied to <c>AddFixedStores</c>.</param>
    /// <param name="first">The first supplied to <c>AddFixedStores</c>.</param>
    /// <param name="last">The last supplied to <c>AddFixedStores</c>.</param>
    /// <param name="cursor">The cursor supplied to <c>AddFixedStores</c>.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    private static void AddFixedStores(CodeWriter w, RenderMember[] members, int first, int last, string cursor, List<string> body)
        => AddFixedStores(w, members, first, last, cursor, body, "value");

    /// <summary>Emits architecture-safe field stores after an ordinary record fails its layout proof.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="members">The ordered record fields.</param>
    /// <param name="first">The index of the first field.</param>
    /// <param name="last">The index of the last field.</param>
    /// <param name="cursor">The output cursor.</param>
    /// <param name="body">The generated statements.</param>
    /// <param name="owner">The record expression.</param>
    private static void AddScalarStores(CodeWriter w, RenderMember[] members, int first, int last, string cursor, List<string> body, string owner = "value")
    {
        string offset = "0";
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            body.Add(w.A("LuminPackWriter") + ".Write(ref " + At(w, cursor, offset) + ", " + owner + "." + member.Name + ");");
            offset = Add(offset, member.Width);
        }
    }

    /// <summary>Stores a run of fixed fields of one record, which may be an inline object, at their packed offsets from a cursor.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="members">The members supplied to <c>AddFixedStores</c>.</param>
    /// <param name="first">The first supplied to <c>AddFixedStores</c>.</param>
    /// <param name="last">The last supplied to <c>AddFixedStores</c>.</param>
    /// <param name="cursor">The cursor supplied to <c>AddFixedStores</c>.</param>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="owner">The type or declaration that owns the operation.</param>
    private static void AddFixedStores(CodeWriter w, RenderMember[] members, int first, int last, string cursor, List<string> body, string owner)
    {
        string offset = "0";
        for (int index = first; index <= last; index++)
        {
            var member = members[index];
            body.Add(member.Size >= 8 || member.Raw
                ? w.A("LuminPackWriter") + ".Write(ref " + At(w, cursor, offset) + ", in " + owner + "." + member.Name + ");"
                : w.A("Unsafe") + ".WriteUnaligned(ref " + At(w, cursor, offset) + ", " + owner + "." + member.Name + ");");
            offset = Add(offset, member.Width);
        }
    }

    /// <summary>Builds the read body of a type whose selected constructor takes parameters: temporary locals hold the decoded
    /// values, an existing instance reads in place, and a fresh one is constructed with its arguments and remaining fields.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    private static List<string> ConstructedReadBody(PayloadRenderModel model, CodeWriter w, bool union)
    {
        var members = model.Members;
        string policy = w.A("LuminPackInputPolicy");
        var body = new List<string> { "// Declare locals to hold the field values." };
        // The constructed read declares its temporaries in declaration order, while the wire order drives the decodes.
        var declared = model.DeclaredOrder == null ? members : model.DeclaredOrder
            .Select(name => System.Array.Find(members, member => member.Name == name)).Where(member => member != null).ToArray();
        foreach (var member in declared)
            body.Add(TempType(member) + " " + Camel(member.Name) + "Temp = default !;");
        if (!union)
        {
            body.Add(model.Trusted ? "if (reader.Read<byte>() == 255)" :
                "if (" + policy + ".ReadNullMarker(ref reader, " + members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("{");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        body.Add("if (value is not null)");
        body.Add("{");
        body.Add("ReadExisting(ref reader, value);");
        body.Add("return;");
        body.Add("}");
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void ReadExisting(ref " + w.A("LuminPackReader") + " reader2, " + model.GlobalName + "? value2)");
        body.Add("{");
        if (model.Mirror != null)
            body.Add("ref var reuseLocal = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value2!);");
        for (int index = 0; index < members.Length; index++)
            AddConstructedReadExisting(body, model, w, members[index], index + 1 < members.Length);
        foreach (string call in model.ReadAfter)
            body.Add(call.Replace("value?.", "value2?."));
        body.Add("}");
        body.Add("");
        foreach (var member in members)
            AddMemberRead(body, model, member, w, "reader", Camel(member.Name) + "Temp", fresh: true);
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
        var mirrored = model.Mirror == null ? Array.Empty<RenderMember>() : members.Where(member => member.Mirrored &&
            (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (mirrored.Length != 0)
        {
            body.Add("// Assign private fields.");
            body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            foreach (var member in mirrored)
                body.Add("local." + member.Name + " = " + Camel(member.Name) + "Temp!;");
        }
        body.AddRange(model.ReadAfter);
        if (model.ReadBefore.Length != 0)
            body.InsertRange(0, model.ReadBefore);
        return body;
    }

    /// <summary>Reads one member in place for the constructed read's existing-instance path.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="hasNext">Whether to enable has next for this operation.</param>
    private static void AddConstructedReadExisting(List<string> body, PayloadRenderModel model, CodeWriter w, RenderMember member, bool hasNext)
    {
        string owner = member.Mirrored ? "reuseLocal" : "value2!";
        if (member.Kind == RenderMemberKind.Text && member.Mirrored)
        {
            body.Add("var currentString = " + owner + "." + member.Name + ";");
            body.Add("var decodedString = reader2.Read<" + Profile + ">(currentString);");
            body.Add("if (!object.ReferenceEquals(currentString, decodedString))");
            body.Add("\t" + owner + "." + member.Name + " = decodedString!;");
            if (hasNext)
                body.Add("");
            return;
        }
        if (member.Kind == RenderMemberKind.Text)
        {
            body.Add("reader2.Read<" + Profile + ">(ref " + owner + "." + member.Name + ");");
            return;
        }
        if (member.Kind == RenderMemberKind.Parameter)
        {
            AddParameterReset(body, member, w, "reader2", owner + "." + member.Name);
            body.Add(w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, Profile) + "(ref reader2, ref " + owner + "." + member.Name + "!);");
            return;
        }
        AddMemberRead(body, model, member, w, "reader2", owner + "." + member.Name);
    }

    /// <summary>Spells a fixed or string member's read from the given reader expression.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <param name="reader">The reader positioned at the input value.</param>
    /// <returns>The resulting text.</returns>
    private static string FixedOrTextRead(RenderMember member, string reader) =>
        member.Kind == RenderMemberKind.Text ? reader + ".Read<" + Profile + ">((string? )null)!" :
        member.IsBoolean ? "LuminPackInputPolicy.ReadBoolean(ref " + reader + ")" :
        reader + ".Read<" + member.Scalar + ">()";

    /// <summary>Spells the temporary local type of one member, as the constructed read declares it.</summary>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The resulting text.</returns>
    private static string TempType(RenderMember member) => member.Kind == RenderMemberKind.Text ? "string?" :
        member.Kind == RenderMemberKind.Parameter && member.ReferencePayload ? member.TypeName + "?" : member.TypeName;

    /// <summary>Spells a member's temporary local with the leading letter in lower case.</summary>
    /// <param name="name">The name to look up or emit.</param>
    /// <returns>The resulting text.</returns>
    private static string Camel(string name) => PayloadEmitter.LocalName(name);

    /// <summary>Whether a record without callbacks or pooled rentals has a fixed run that its payload body can read into a new
    /// instance through the proven checked batch.</summary>
    /// <param name="model">The record.</param>
    /// <returns>Whether such a run exists.</returns>
    private static bool HasFreshPayloadRun(PayloadRenderModel model)
    {
        if (model.HasCallbacks || model.Rent != null || model.IsStruct)
            return false;
        var members = model.Members;
        for (int index = 0; index < members.Length; index++)
        {
            if (!members[index].IsFixed)
                continue;
            int last = RunEnd(members, index);
            bool plain = last > index;
            for (int item = index; plain && item <= last; item++)
                plain = !members[item].Mirrored && !members[item].Property;
            if (plain)
                return true;
            index = last;
        }
        return false;
    }

    /// <summary>Builds a root read body.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <param name="freshUnion">Whether a Union case reads into a new destination.</param>
    /// <param name="freshPayload">Whether the payload body of a record root may read a fixed run of a newly created instance
    /// through the object body's proven checked batch; both framing bodies then declare the freshness test.</param>
    /// <param name="allowStringPairs">Whether this scope permits grouping direct field reads.</param>
    /// <returns>The body.</returns>
    private static List<string> ReadBody(PayloadRenderModel model, CodeWriter w, bool union, bool freshUnion = false, bool freshPayload = false, bool allowStringPairs = true)
    {
        // A list-derived payload reads the list base body; the list owner's own length framing replaces the record marker.
        if (model.SequenceBase != null)
            return SequenceReadBody(model, w);
        // A selected constructor builds the instance from temporary locals; a raw struct's arguments only reach its JSON
        // read, so the binary body keeps the struct paths.
        if (!model.IsStruct && model.ConstructorArguments != null)
            return ConstructedReadBody(model, w, union);
        // A mixed record reads with the stream protocol: the inline objects keep their markers, so the reserved write
        // segments change nothing about the order and shape of the reads.
        if (model.StreamRecord || model.MixedRecord)
            return StreamReadBody(model, w, union, allowStringPairs && !union);
        var members = model.Members;
        var body = new List<string>();
        string Policy() => w.A("LuminPackInputPolicy");
        bool trusted = model.Trusted;
        // The Union discriminator already framed the payload. No member-count marker precedes its fields.
        if (!union)
        {
            body.Add(trusted ? "if (reader.Read<byte>() == 255)" : "if (" + Policy() + ".ReadNullMarker(ref reader, " +
                members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("\treturn;");
        }
        // Both framing bodies declare the test so the shared body keeps it outside the framing branch.
        bool freshInstance = freshPayload && HasFreshPayloadRun(model);
        if (freshInstance)
            body.Add("bool freshInstance = value is null;");
        body.Add("var instance = value ??= " + NewInstance(model) + ";");
        int cursors = 0, strings = 0;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.IsFixed)
            {
                int last = RunEnd(members, index);
                if (last == index)
                {
                    body.Add("instance." + member.Name + " = " + (member.IsBoolean && !trusted
                        ? Policy() + ".ReadBoolean(ref reader);" : "reader.Read<" + member.Scalar + ">();"));
                    continue;
                }
                // The checked block validates the complete run before copying, so a reused case gains no partial write.
                bool fixedUnionBlock = model.ManagedFixedUnionCase && !model.HasCallbacks && model.Rent == null;
                for (int item = index; fixedUnionBlock && item <= last; item++)
                    fixedUnionBlock = !members[item].Mirrored && !members[item].Property && !members[item].IsBoolean;
                // A payload read into an instance it has just created publishes nothing before it returns, so the object body's
                // proven batch, whose Boolean check follows its copy, leaves no observable partial state there. A callback could
                // observe the instance and a pooled rental is not new; both keep the field-wise read, as a reused instance does.
                bool freshBlock = !fixedUnionBlock && union && freshInstance;
                for (int item = index; freshBlock && item <= last; item++)
                    freshBlock = !members[item].Mirrored && !members[item].Property;
                bool useFieldProof = !union || fixedUnionBlock || freshBlock;
                int width = 0;
                var offsets = new List<int>();
                for (int item = index; item <= last; item++)
                {
                    if (members[item].IsBoolean)
                        offsets.Add(width);
                    width += members[item].Size;
                }
                string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
                string widthText = Width(members, index, last);
                // A runtime-sized advance is spelled as the unsigned native offset of the cursor projection.
                string advance = int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? widthText
                    : "unchecked((nint)(uint)((" + widthText + ")))";
                if (useFieldProof)
                {
                    body.Add("if (" + (freshBlock ? "freshInstance && " : string.Empty) + Proof(w, members, index, last, "instance", readOnly: false, coreLayout: true) + ")");
                    string block = Bytes(w, member, "instance", readOnly: false, plain: true);
                    if (offsets.Count == 0 || trusted)
                        body.Add("\treader.Read(ref " + block + ", " + widthText + ");");
                    else if (BatchMask(offsets, width) is { } batch)
                        body.Add("\t" + Policy() + ".ReadBatch(ref reader, ref " + block + ", " + widthText + ", " + batch + ");");
                    else
                    {
                        body.Add("{");
                        body.Add("reader.Read(ref " + block + ", " + widthText + ");");
                        for (int item = index; item <= last; item++)
                            if (members[item].IsBoolean)
                                body.Add(Policy() + ".CheckBoolean(in instance." + members[item].Name + ");");
                        body.Add("}");
                    }
                    body.Add("else");
                    body.Add("{");
                }
                body.Add("ref byte " + cursor + " = ref reader.Cursor;");
                body.Add("reader.EnsureReadable(" + widthText + ");");
                body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, " + advance + ");");
                string offset = "0";
                for (int item = index; item <= last; item++)
                {
                    var field = members[item];
                    body.Add("instance." + field.Name + " = " + (field.IsBoolean ? (trusted ? w.A("LuminPackReader") + ".Read<bool>(ref " + At(w, cursor, offset, plain: true) + ");"
                        : Policy() + ".ReadBoolean(ref " + At(w, cursor, offset, plain: true) + ");")
                        : w.A("LuminPackReader") + ".Read<" + field.Scalar + ">(ref " + At(w, cursor, offset, plain: true) + ");"));
                    offset = Add(offset, field.Width);
                }
                if (useFieldProof)
                    body.Add("}");
                body.Add("");
                index = last;
                continue;
            }
            if (allowStringPairs && !union && TryAddStringPairRead(body, model, "instance", w, ref index))
                continue;
            if (member.Kind == RenderMemberKind.Text && member.Mirrored && member.Property)
            {
                string number = strings++ == 0 ? string.Empty : strings.ToString(CultureInfo.InvariantCulture);
                // A mirrored auto-property is assigned only when the decoded instance differs; it follows a fixed run directly.
                if (body[body.Count - 1].Length == 0)
                    body.RemoveAt(body.Count - 1);
                body.Add("var currentString" + number + " = instance." + member.Name + ";");
                body.Add("var decodedString" + number + " = reader.Read<" + ReadProfile(model, w, member) + ">(currentString" + number + ");");
                body.Add("if (!object.ReferenceEquals(currentString" + number + ", decodedString" + number + "))");
                body.Add("\tinstance." + member.Name + " = decodedString" + number + "!;");
                body.Add("");
            }
            else if (member.Kind == RenderMemberKind.Text)
                // Included non-public fields pass their mirror storage by reference, as public fields do.
                body.Add("reader.Read<" + ReadProfile(model, w, member) + ">(ref instance." + member.Name + ");");
            else if (member.Kind == RenderMemberKind.Array && member.Scalar == "bool" && !trusted)
            {
                body.Add(Policy() + ".ReadBooleans(ref reader, ref instance." + member.Name + "!);");
                AddCollectionCheck(body, member, "instance." + member.Name);
            }
            else
            {
                body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref instance." + member.Name + "!);");
                AddCollectionCheck(body, member, "instance." + member.Name);
                // A Boolean list carries unmanaged values, so an untrusted read still checks the decoded bytes.
                if (member.Kind == RenderMemberKind.List && member.Scalar == "bool" && !trusted)
                    body.Add(Policy() + ".CheckBooleans(instance." + member.Name + "!);");
            }
        }
        // The presentation keeps no blank line between a mirrored string read and an unproven fixed run's cursor.
        for (int index = body.Count - 2; index > 0; index--)
            if (body[index].Length == 0 && body[index - 1].Contains(" = decodedString") && body[index + 1].StartsWith("ref byte ", StringComparison.Ordinal))
                body.RemoveAt(index);
        while (body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        RenameCheckedReadInstance(body, model);
        return body;
    }

    /// <summary>Gives both checked framing bodies the same instance local before sharing their statements.</summary>
    /// <param name="body">The statements in the generated operation body.</param>
    /// <param name="model">The normalized model for this contract.</param>
    private static void RenameCheckedReadInstance(List<string> body, PayloadRenderModel model)
    {
        if (!model.BooleanCollection || model.Trusted)
            return;
        const string name = "lp0_v2";
        var pattern = new System.Text.RegularExpressions.Regex(@"\binstance\b");
        for (int index = 0; index < body.Count; index++)
            body[index] = pattern.Replace(body[index], name);
    }

    /// <summary>Builds a stream record's read body: the null marker, then every member, with inline objects reading their own
    /// marker and members in place.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="union">Whether to enable union for this operation.</param>
    /// <returns>The resulting <c>List&lt;string&gt;</c>.</returns>
    /// <param name="pairFields">Whether this scope permits grouping direct field reads.</param>
    private static List<string> StreamReadBody(PayloadRenderModel model, CodeWriter w, bool union, bool pairFields)
    {
        var members = model.Members;
        var body = new List<string>();
        string Policy() => w.A("LuminPackInputPolicy");
        if (!union)
        {
            body.Add(model.Trusted ? "if (reader.Read<byte>() == 255)" : "if (" + Policy() + ".ReadNullMarker(ref reader, " +
                members.Length.ToString(CultureInfo.InvariantCulture) + "))");
            body.Add("\treturn;");
        }
        body.Add("var instance = value ??= " + NewInstance(model) + ";");
        int cursors = 0, strings = 0, ends = 0;
        AddStreamRead(body, model, "instance", 0, !union, w, new Dictionary<string, int>(StringComparer.Ordinal), ref cursors, ref strings, ref ends, pairFields);
        while (body.Count != 0 && body[body.Count - 1].Length == 0)
            body.RemoveAt(body.Count - 1);
        ReduceInlineNames(body);
        RenameCheckedReadInstance(body, model);
        return body;
    }

    /// <summary>Reads the members of a stream record or of one of its inline objects.</summary>
    /// <param name="body">The body lines.</param>
    /// <param name="model">The record read at this depth.</param>
    /// <param name="owner">The expression holding the record being filled.</param>
    /// <param name="depth">The inline nesting depth, which names the inline locals.</param>
    /// <param name="prove">Whether a contiguous fixed run is proven on a 64-bit target; a Union payload reads it without proof.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="names">The occurrence counters of the inline locals of this method.</param>
    /// <param name="cursors">The cursor local counter shared by the whole method.</param>
    /// <param name="strings">The mirrored string local counter shared by the whole method.</param>
    /// <param name="ends">The exit-label counter of the outlined trusted child reads.</param>
    /// <param name="pairFields">Whether the surrounding reader can group direct string fields.</param>
    private static void AddStreamRead(List<string> body, PayloadRenderModel model, string owner, int depth, bool prove,
        CodeWriter w, Dictionary<string, int> names, ref int cursors, ref int strings, ref int ends, bool pairFields)
    {
        var members = model.Members;
        string Policy() => w.A("LuminPackInputPolicy");
        bool trusted = model.Trusted;
        for (int index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.Kind == RenderMemberKind.Object)
            {
                // A trusted parent outlines an explicitly untrusted child: it peeks for the null marker and runs the child's
                // own checked root read, so the child keeps its depth-guarded and validated operations.
                if (trusted && !member.Nested.Trusted && !member.Nested.BooleanCollection && !member.Nested.IsStruct &&
                    member.Nested.ConstructorArguments == null && member.Nested.Version == null && member.Nested.Mirror == null &&
                    member.Nested.Rent == null && !member.Nested.HasCallbacks && member.Nested.Bounded == null &&
                    !member.Nested.Members.Any(item => item.Mirrored) &&
                    member.Nested.Members.All(item => item.IsFixed || item.Kind == RenderMemberKind.Text))
                {
                    if (CanCallCheckedChildRead(model, member))
                    {
                        AddFormatterMemberRead(body, model, member, owner, objectFraming: true, w, names);
                        continue;
                    }
                    string exit = "end" + ends.ToString(CultureInfo.InvariantCulture);
                    ends++;
                    body.Add("reader.EnsureReadable(1);");
                    body.Add("var nestedInput = reader.Buffer[reader.Position] == 255");
                    body.Add("\t? default(" + member.Nested.GlobalName + ")");
                    body.Add("\t: " + owner + "." + member.Name + ";");
                    body.Add("{");
                    string instanceName = "instance" + (depth + 2).ToString(CultureInfo.InvariantCulture);
                    body.AddRange(InlineRootRead(member.Nested, w, "nestedInput", instanceName, exit, ref cursors, pairFields));
                    body.Add("}");
                    body.Add("");
                    body.Add(owner + "." + member.Name + " = nestedInput!;");
                    bool opensCursorAfter = !prove && index + 1 < members.Length && members[index + 1].IsFixed && RunEnd(members, index + 1) > index + 1;
                    if (!opensCursorAfter)
                        body.Add("");
                    continue;
                }
                if (CanCallInlineMemberRead(model, member))
                {
                    AddFormatterMemberRead(body, model, member, owner, prove, w, names);
                    continue;
                }
                string name = InlineName(names, "inlineRead", depth);
                body.Add(trusted ? "if (reader.Read<byte>() == 255)" : "if (" + Policy() + ".ReadNullMarker(ref reader, " +
                    member.Nested.Members.Length.ToString(CultureInfo.InvariantCulture) + "))");
                body.Add("\t" + owner + "." + member.Name + " = default !;");
                body.Add("else");
                body.Add("{");
                body.Add("var " + name + " = " + owner + "." + member.Name + " ?? " + NewInstance(member.Nested) + ";");
                AddStreamRead(body, member.Nested, name, depth + 1, prove, w, names, ref cursors, ref strings, ref ends, pairFields);
                body.Add(owner + "." + member.Name + " = " + name + ";");
                body.Add("}");
                // A following unproven fixed run opens with its own cursor local, which the presentation keeps directly
                // under the block instead of separating it with a blank line; a proven run opens with its proof. The parent's
                // assignment after the last member keeps the blank, so it is added even without a next member.
                bool opensCursor = !prove && index + 1 < members.Length && members[index + 1].IsFixed && RunEnd(members, index + 1) > index + 1;
                if (!opensCursor)
                    body.Add("");
                continue;
            }
            if (member.Kind == RenderMemberKind.Text)
            {
                if (pairFields && TryAddStringPairRead(body, model, owner, w, ref index))
                    continue;
                if (member.Mirrored)
                {
                    // A mirrored string is assigned only when the decoded instance differs, so its storage keeps the old one.
                    string number = strings++ == 0 ? string.Empty : strings.ToString(CultureInfo.InvariantCulture);
                    body.Add("var currentString" + number + " = " + owner + "." + member.Name + ";");
                    body.Add("var decodedString" + number + " = reader.Read<" + ReadProfile(model, w, member) + ">(currentString" + number + ");");
                    body.Add("if (!object.ReferenceEquals(currentString" + number + ", decodedString" + number + "))");
                    body.Add("\t" + owner + "." + member.Name + " = decodedString" + number + "!;");
                    body.Add("");
                    continue;
                }
                body.Add("reader.Read<" + ReadProfile(model, w, member) + ">(ref " + owner + "." + member.Name + ");");
                continue;
            }
            // A type-parameter member reads through the generic serializer, assigning the reset guard only when its
            // parameter may hold a reference.
            if (member.Kind == RenderMemberKind.Parameter)
            {
                AddParameterReset(body, member, w, "reader", owner + "." + member.Name);
                body.Add(w.A("LuminPackSerializer") + ".Deserialize" + MemberArguments(member, Profile) + "(ref reader, ref " + owner + "." + member.Name + "!);");
                continue;
            }
            // A fused dictionary reads through its generated formatter, which keeps its own entry framing and profile.
            if (member.Kind == RenderMemberKind.Dictionary)
            {
                body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref " + owner + "." + member.Name + "!);");
                continue;
            }
            // A culture-info member reads through its shared formatter owner, which handles its own null marker.
            if (member.Kind == RenderMemberKind.CultureInfo)
            {
                body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref " + owner + "." + member.Name + "!);");
                continue;
            }
            // A framework member reads through its shared formatter owner, which handles its own null record.
            if (member.Kind == RenderMemberKind.Special)
            {
                AddMemberRead(body, model, member, w, "reader", owner + "." + member.Name);
                continue;
            }
            // A concurrent queue, immutable array, stack or list reads through its shared formatter owner, which frames its
            // own null record.
            if (member.Kind is RenderMemberKind.ForwardedCollection or RenderMemberKind.Sequence)
            {
                body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref " + owner + "." + member.Name + "!);");
                continue;
            }
            // A sorted, paired or nested collection reads through its generated formatter, which keeps its own framing.
            if (IsCollectionBlock(member))
            {
                body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref " + owner + "." + member.Name + "!);");
                continue;
            }
            // An array or list member reads through its generated formatter, which keeps its own framing and profile.
            if (member.Kind is RenderMemberKind.Array or RenderMemberKind.List)
            {
                if (member.Kind == RenderMemberKind.Array && member.Scalar == "bool" && !trusted)
                {
                    body.Add(Policy() + ".ReadBooleans(ref reader, ref " + owner + "." + member.Name + "!);");
                    AddCollectionCheck(body, member, owner + "." + member.Name);
                }
                else
                {
                    body.Add(member.BinaryFormatter + ".Read<" + ReadProfile(model, w, member) + ">(ref reader, ref " + owner + "." + member.Name + "!);");
                    AddCollectionCheck(body, member, owner + "." + member.Name);
                    if (member.Kind == RenderMemberKind.List && member.Scalar == "bool" && !trusted)
                        body.Add(Policy() + ".CheckBooleans(" + owner + "." + member.Name + "!);");
                }
                continue;
            }
            int last = RunEnd(members, index);
            if (last == index)
            {
                body.Add(owner + "." + member.Name + " = " + (member.IsBoolean && !trusted
                    ? Policy() + ".ReadBoolean(ref reader);" : "reader.Read<" + member.Scalar + ">();"));
                continue;
            }
            int width = 0;
            var offsets = new List<int>();
            for (int item = index; item <= last; item++)
            {
                if (members[item].IsBoolean)
                    offsets.Add(width);
                width += members[item].Size;
            }
            string cursor = "cursor" + (cursors++ == 0 ? string.Empty : cursors.ToString(CultureInfo.InvariantCulture));
            string widthText = Width(members, index, last);
            // A runtime-sized advance is spelled as the unsigned native offset of the cursor projection.
            string advance = int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? widthText
                : "unchecked((nint)(uint)((" + widthText + ")))";
            if (prove)
            {
                body.Add("if (" + Proof(w, members, index, last, owner, readOnly: false, coreLayout: true) + ")");
                string block = Bytes(w, member, owner, readOnly: false, plain: true);
                if (offsets.Count == 0 || trusted)
                    body.Add("\treader.Read(ref " + block + ", " + widthText + ");");
                else if (BatchMask(offsets, width) is { } batch)
                    body.Add("\t" + Policy() + ".ReadBatch(ref reader, ref " + block + ", " + widthText + ", " + batch + ");");
                else
                {
                    body.Add("{");
                    body.Add("reader.Read(ref " + block + ", " + widthText + ");");
                    for (int item = index; item <= last; item++)
                        if (members[item].IsBoolean)
                            body.Add(Policy() + ".CheckBoolean(in " + owner + "." + members[item].Name + ");");
                    body.Add("}");
                }
                body.Add("else");
                body.Add("{");
            }
            body.Add("ref byte " + cursor + " = ref reader.Cursor;");
            body.Add("reader.EnsureReadable(" + widthText + ");");
            body.Add("reader.Cursor = ref " + w.A("Unsafe") + ".Add(ref reader.Cursor, " + advance + ");");
            string offset = "0";
            for (int item = index; item <= last; item++)
            {
                var field = members[item];
                body.Add(owner + "." + field.Name + " = " + (field.IsBoolean ? (trusted ? w.A("LuminPackReader") + ".Read<bool>(ref " + At(w, cursor, offset, plain: true) + ");"
                    : Policy() + ".ReadBoolean(ref " + At(w, cursor, offset, plain: true) + ");")
                    : w.A("LuminPackReader") + ".Read<" + field.Scalar + ">(ref " + At(w, cursor, offset, plain: true) + ");"));
                offset = Add(offset, field.Width);
            }
            if (prove)
                body.Add("}");
            body.Add("");
            index = last;
        }
    }

    /// <summary>Checks whether the trusted parent's read can call the child's checked object root directly.</summary>
    /// <param name="parent">The containing record.</param>
    /// <param name="member">The nested member whose root validates its own object marker.</param>
    /// <returns>True when the checked object root can replace the inlined read.</returns>
    private static bool CanCallCheckedChildRead(PayloadRenderModel parent, RenderMember member) =>
        parent.Redirect == null && member.Nested.Limits?.Bounded != true && !member.Nested.Recursive &&
        member.Nested.Rejection == null && member.Nested.TypeParameters.Length == 0;

    /// <summary>Checks whether the nested read can reuse the child's concrete object or payload root.</summary>
    /// <param name="parent">The containing record.</param>
    /// <param name="member">The nested member to read.</param>
    /// <returns>True when the root can preserve the inline read's framing, validation, and member assignment.</returns>
    private static bool CanCallInlineMemberRead(PayloadRenderModel parent, RenderMember member)
    {
        var child = member.Nested;
        return parent.Net8 && !member.Mirrored && !member.Property && !member.Volatile &&
            parent.Redirect == null && child.Redirect == null && parent.Trusted == child.Trusted &&
            parent.Limits?.Bounded != true && child.Limits?.Bounded != true &&
            !child.Recursive && child.Rejection == null && child.Bounded == null && child.TypeParameters.Length == 0 &&
            child.DeclaredProfile == null && member.ScopeProfile == null &&
            RenderModelFactory.Covered(child);
    }

    /// <summary>Calls the concrete reader with the existing framing and delays the member assignment until it returns.</summary>
    /// <param name="body">The parent read statements.</param>
    /// <param name="parent">The containing record.</param>
    /// <param name="member">The nested record member.</param>
    /// <param name="owner">The containing instance expression.</param>
    /// <param name="objectFraming">Whether the child uses object framing and field address proofs.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="names">The local name counters.</param>
    private static void AddFormatterMemberRead(List<string> body, PayloadRenderModel parent, RenderMember member,
        string owner, bool objectFraming, CodeWriter w, Dictionary<string, int> names)
    {
        var child = member.Nested;
        string target = owner + "." + member.Name;
        string local = RoleName(names, "nestedValue");
        string profile = HasReadProfile(child) ? "<" + Profile + ">" : string.Empty;
        string root = BinaryRoot(w, child);
        bool outline = OutlineNestedRecord(child, read: true);
        string call = root + (objectFraming ? "RootRead" : "RootReadUnion") + profile;
        if (outline)
        {
            // Both parent framings share the helper declaration. The stateless framing argument keeps the call site precise.
            string helper = RoleName(names, "ReadNested");
            body.Add("{");
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void " + helper + "<TNestedFraming>(scoped ref " + w.A("LuminPackReader") + " state, scoped ref " + child.GlobalName + "? nested)");
            body.Add("{");
            body.Add("if (typeof(TNestedFraming) == typeof(global::LuminPack.Code.LuminPackObjectFraming))");
            body.Add("\t" + root + "RootRead" + profile + "(ref state, ref nested);");
            body.Add("else");
            body.Add("\t" + root + "RootReadUnion" + profile + "(ref state, ref nested);");
            body.Add("}");
            call = helper + (objectFraming ? "<global::LuminPack.Code.LuminPackObjectFraming>" : "<global::LuminPack.Code.LuminPackPayloadFraming>");
        }
        body.Add("var " + local + " = " + target + ";");
        if (objectFraming)
        {
            body.Add("if (reader.PeekIsNullObject())");
            body.Add("\t" + local + " = default;");
            body.Add(call + "(ref reader, ref " + local + ");");
        }
        else
        {
            string marker = parent.Trusted ? "reader.Read<byte>() == 255"
                : w.A("LuminPackInputPolicy") + ".ReadNullMarker(ref reader, " +
                  child.Members.Length.ToString(CultureInfo.InvariantCulture) + ")";
            body.Add("if (" + marker + ")");
            body.Add("\t" + local + " = default;");
            body.Add("else");
            body.Add("\t" + call + "(ref reader, ref " + local + ");");
        }
        body.Add(target + " = " + local + "!;");
        if (outline)
            body.Add("}");
        body.Add(string.Empty);
    }

    /// <summary>Builds the checked root read of an explicitly untrusted child inside a trusted parent: the child's own null
    /// marker, its existing-instance target and its cursor locals continue the parent method.</summary>
    /// <param name="child">The child record, which declares hostile input.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="target">The local holding the reused child instance.</param>
    /// <param name="instanceName">The local the child members are stored in, numbered by nesting depth.</param>
    /// <param name="exit">The label a null child jumps to.</param>
    /// <param name="cursors">The parent method's cursor local counter, advanced by the child's own cursors.</param>
    /// <param name="pairFields">Whether the surrounding reader can group direct string fields.</param>
    /// <returns>The child body lines.</returns>
    private static List<string> InlineRootRead(PayloadRenderModel child, CodeWriter w, string target, string instanceName, string exit, ref int cursors, bool pairFields)
    {
        var lines = ReadBody(child, w, union: false, allowStringPairs: pairFields);
        var instance = new System.Text.RegularExpressions.Regex(@"\binstance\b");
        string declaration = "var instance = value ??= " + NewInstance(child) + ";";
        for (int index = 0; index < lines.Count; index++)
        {
            if (index == 1)
            {
                lines[index] = "\tgoto " + exit + ";";
                continue;
            }
            if (lines[index] == declaration)
            {
                lines[index] = "var " + instanceName + " = " + target + " ??= " + NewInstance(child) + ";";
                continue;
            }
            lines[index] = instance.Replace(lines[index], instanceName);
        }
        // The child's cursor locals continue the parent method's numbering.
        var cursor = new System.Text.RegularExpressions.Regex(@"\bcursor(\d*)\b");
        int childCursors = lines.Count(line => line.StartsWith("ref byte cursor", StringComparison.Ordinal));
        if (cursors != 0)
        {
            int offset = cursors;
            for (int index = 0; index < lines.Count; index++)
                lines[index] = cursor.Replace(lines[index], match => CursorSpelling(match.Groups[1].Value, offset));
        }
        cursors += childCursors;
        // The child's fixed run ends with a blank line, which its exit label follows directly.
        if (child.Members.Length != 0 && child.Members[child.Members.Length - 1].IsFixed)
            lines.Add("");
        lines.Add(exit + ":");
        lines.Add("\t;");
        return lines;
    }

    /// <summary>Spells a cursor local with a shifted ordinal: the first is <c>cursor</c>, the following ones carry their
    /// ordinal.</summary>
    /// <param name="suffix">The suffix supplied to <c>CursorSpelling</c>.</param>
    /// <param name="offset">The offset into the current buffer or input.</param>
    /// <returns>The resulting text.</returns>
    private static string CursorSpelling(string suffix, int offset)
    {
        int ordinal = (suffix.Length == 0 ? 0 : int.Parse(suffix, CultureInfo.InvariantCulture) - 1) + offset;
        return ordinal == 0 ? "cursor" : "cursor" + (ordinal + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Whether the Booleans of a fixed run, if any, fit one batch mask.</summary>
    /// <param name="members">The members supplied to <c>HasBatchMask</c>.</param>
    /// <param name="first">The first supplied to <c>HasBatchMask</c>.</param>
    /// <param name="last">The last supplied to <c>HasBatchMask</c>.</param>
    /// <returns>Whether the input has batch mask.</returns>
    internal static bool HasBatchMask(RenderMember[] members, int first, int last)
    {
        var offsets = new List<int>();
        int width = 0;
        for (int index = first; index <= last; index++)
        {
            if (members[index].IsBoolean)
                offsets.Add(width);
            width += members[index].Size;
        }
        return offsets.Count == 0 || BatchMask(offsets, width) != null;
    }

    /// <summary>Selects one four- or eight-byte window holding every Boolean of a batch and its rejection mask.</summary>
    /// <param name="offsets">The offsets supplied to <c>BatchMask</c>.</param>
    /// <param name="length">The length of the requested range.</param>
    /// <returns>The resulting text.</returns>
    private static string BatchMask(List<int> offsets, int length)
    {
        int low = offsets.Min(), high = offsets.Max();
        int width = length >= 8 && high - low < 8 ? 8 : length >= 4 && high - low < 4 ? 4 : 0;
        if (width == 0)
            return null;
        int window = Math.Min(low, length - width);
        ulong mask = offsets.Aggregate(0UL, (bits, value) => bits | 0xFEUL << (8 * (value - window)));
        return window.ToString(CultureInfo.InvariantCulture) + ", 0x" + mask.ToString(width == 8 ? "X16" : "X8", CultureInfo.InvariantCulture) + (width == 8 ? "UL" : "U");
    }
}

