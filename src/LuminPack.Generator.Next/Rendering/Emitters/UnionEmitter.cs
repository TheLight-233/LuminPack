#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a covered Union root's published binary and JSON files: the root operations, the payload extensions, the
/// virtual dispatch slots, the runtime registration and every case's slot overrides.</summary>
/// <remarks>Union roots publish the same slot contract in both formats. The lines are the original emitter's own lines, with
/// helper names spelled through the consumer's aliases; the publication layout wraps long lines and compacts the short case
/// rows of the marked switch statements.</remarks>
internal static class UnionEmitter
{
    private const string Profile = "TLuminProfile";

    /// <summary>Emits the binary file.</summary>
    /// <param name="model">The covered Union root.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitBinary(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string writer = w.A("LuminPackWriter"), reader = w.A("LuminPackReader");
        string type = model.GlobalName, container = "LuminRoots_" + model.Readable;
        string serializer = w.A("LuminPackSerializer"), standard = w.A(model.UnionStandardBinary);
        string constraints = model.TypeConstraints.Length == 0 ? string.Empty : model.TypeConstraintText + " ";
        string inline = w.MethodImpl("AggressiveInlining"), constraint = constraints + "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
        string slotPrefix = "Write_" + model.UnionSuffix;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class " + serializer);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_" + model.Readable + model.TypeArguments() + "(scoped ref " + writer + " writer, scoped in " + type + "? input) " + constraints + "=> " +
            container + ".RootWrite" + model.TypeArguments(standard) + "(ref writer, input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_" + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + writer + " writer, scoped in " + type + "? input)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootWrite" + model.TypeArguments(Profile) + "(ref writer, input);"));
        w.Suppressed = false;

        w.Blank();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite" + model.TypeArguments(Profile) + "(scoped ref " + writer + " writer, " + type + "? input)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        Reject(model, w);
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("writer.Write(0u);");
        w.Line("return;");
        w.Close();
        w.Blank();
        if (StaticDispatch(model))
            ExactDispatch(model, w, json: false);
        else
            w.Line(type + "." + slotPrefix + "_Compiled<" + Profile + ">(ref writer, value);");
        w.Close();
        if (StaticDispatch(model))
        {
            ExactWriters(model, w, constraints, json: false);
            if (model.Registration)
            {
                w.Blank();
                RegisteredWriter(model, w, writer, json: false);
            }
        }
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        var fixedReads = PlanFixedReads(model, w);
        if (ReadKeepsInline(model, fixedReads))
            w.Line(inline);
        w.Line("internal static void RootRead" + (model.UnionReadGeneric ? model.TypeArguments(Profile) : model.TypeArguments()) + "(scoped ref " + reader +
            " reader, scoped ref " + type + "? value)");
        if (model.UnionReadGeneric)
            w.Indented(() => w.Line(constraint));
        else if (constraints.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        ReadSwitch(model, w, fixedReads);
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_" + model.Readable + model.TypeArguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) " + constraints + "=> " +
            container + ".RootRead" + (model.UnionReadGeneric ? model.TypeArguments(standard) : model.TypeArguments()) + "(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_" + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootRead" + (model.UnionReadGeneric ? model.TypeArguments(Profile) : model.TypeArguments()) +
            "(ref reader, ref value);"));
        w.Suppressed = false;
        w.Close();
        w.Blank();

        // Payload extensions.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments() + "(this ref " + writer + " state, in " + type + "? value) " + constraints + "=> " + serializer + "." + container +
            ".RootWrite" + model.TypeArguments(standard) + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line(constraints + "where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWrite" + model.TypeArguments("TOptions") + "(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments() + "(this ref " + reader + " state, ref " + type + "? value)");
        if (constraints.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (model.UnionReadGeneric ? model.TypeArguments(standard) : model.TypeArguments()) + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line(constraints + "where TOptions : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (model.UnionReadGeneric ? model.TypeArguments("TOptions") : model.TypeArguments()) + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Close();

        // An imported root keeps the dispatch and registration its own assembly declared inside it and its cases.
        if (model.UnionReferenced || StaticDispatch(model))
            return;
        // Root dispatch and registration. The virtual slots only write, so they follow the write operation of their format.
        w.Blank();
        OpenType(model.UnionNamespace, model.UnionChain, w);
        w.Suppressed = !writes;
        if (model.UnionCursor)
            CursorRoot(model, w, writer);
        w.Suppressed = false;
        Registration(model, w, type);
        w.Suppressed = !writes;
        RootSlots(model, w, writer, false);
        CompiledSlot(model, w, writer, false);
        RegisteredWriter(model, w, writer, false);
        w.Suppressed = false;
        if (model.UnionInterface)
        {
            // The publication partition recurses into partial classes but not into interfaces, so a root's JSON slots stay
            // in the binary file while its cases' JSON slots move to the JSON file.
            w.Suppressed = (operations & GeneratedOperations.JsonWrite) == 0;
            RootSlots(model, w, writer, true);
            CompiledSlot(model, w, writer, true);
            RegisteredWriter(model, w, writer, true);
            w.Suppressed = false;
        }
        CloseChain(model.UnionChain.Length, model.UnionNamespace, w);

        foreach (var group in CaseGroups(model))
        {
            w.Blank();
            OpenType(group[0].Namespace, group[0].Chain, w);
            if (group[0].Definition != null)
            {
                // Both formats' generic case slots dispatch through the slot the binary file declares.
                w.Suppressed = !writes && (operations & GeneratedOperations.JsonWrite) == 0;
                GenericSlotField(group, w, "LuminPackUnionDispatchSlot_" + model.UnionSuffix);
                w.Suppressed = !writes;
                GenericCaseSlots(model, group, w, false);
            }
            else
            {
                w.Suppressed = !writes;
                CaseSlots(model, group[0], w, false);
            }
            w.Suppressed = false;
            CloseChain(group[0].Chain.Length, group[0].Namespace, w);
        }
    }

    /// <summary>Emits the JSON file.</summary>
    /// <param name="model">The covered Union root.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitJson(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.JsonWrite) != 0, reads = (operations & GeneratedOperations.JsonRead) != 0;
        string writer = w.A("LuminPackJsonWriter"), reader = w.A("LuminPackJsonReader");
        string type = model.GlobalName, container = "LuminRoots_" + model.Readable;
        string serializer = w.A("LuminPackSerializer"), standard = w.A(model.UnionStandardJson);
        string constraints = model.TypeConstraints.Length == 0 ? string.Empty : model.TypeConstraintText + " ";
        string inline = w.MethodImpl("AggressiveInlining"), constraint = constraints + "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
        string slotPrefix = "WriteJson_" + model.UnionSuffix;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class " + serializer);
        w.Open();
        w.Suppressed = !writes && !reads;
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line("internal static void RootWriteJson" + model.TypeArguments(Profile) + "(scoped ref " + writer + " writer, " + type + "? input)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        Reject(model, w);
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.WriteNull<" + Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        if (StaticDispatch(model))
            ExactDispatch(model, w, json: true);
        else
            w.Line(type + "." + slotPrefix + "_Compiled<" + Profile + ">(ref writer, value);");
        w.Close();
        if (StaticDispatch(model))
            ExactWriters(model, w, constraints, json: true);
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line("internal static void RootReadJson" + model.TypeArguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        Reject(model, w);
        JsonReadBody(model, w, reader, type);
        w.Close();
        w.Suppressed = !writes && !reads;
        w.Close();
        w.Suppressed = false;
        w.Blank();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_" + model.Readable + model.TypeArguments() + "(scoped ref " + writer + " writer, scoped in " + type +
            "? input) " + constraints + "=> " + container + ".RootWriteJson" + model.TypeArguments(standard) + "(ref writer, input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_" + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + writer + " writer, scoped in " + type + "? input)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootWriteJson" + model.TypeArguments(Profile) + "(ref writer, input);"));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_" + model.Readable + model.TypeArguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) " + constraints + "=> " +
            container + ".RootReadJson" + model.TypeArguments(standard) + "(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_" + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootReadJson" + model.TypeArguments(Profile) + "(ref reader, ref value);"));
        w.Suppressed = false;
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments() + "(this ref " + writer + " state, in " + type + "? value) " + constraints + "=> " + serializer + "." + container +
            ".RootWriteJson" + model.TypeArguments(standard) + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line(constraints + "where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWriteJson" + model.TypeArguments("TOptions") + "(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments() + "(this ref " + reader + " state, ref " + type + "? value)");
        if (constraints.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<" + standard + ">())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson" + model.TypeArguments(standard) + "(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line(constraints + "where TOptions : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson" + model.TypeArguments("TOptions") + "(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Close();

        if (model.UnionReferenced || StaticDispatch(model))
            return;
        w.Blank();
        if (!model.UnionInterface)
        {
            OpenType(model.UnionNamespace, model.UnionChain, w);
            w.Suppressed = !writes;
            RootSlots(model, w, writer, true);
            CompiledSlot(model, w, writer, true);
            RegisteredWriter(model, w, writer, true);
            w.Suppressed = false;
            CloseChain(model.UnionChain.Length, model.UnionNamespace, w);
        }
        foreach (var group in CaseGroups(model))
        {
            w.Blank();
            OpenType(group[0].Namespace, group[0].Chain, w);
            w.Suppressed = !writes;
            if (group[0].Definition != null)
                GenericCaseSlots(model, group, w, true);
            else
                CaseSlots(model, group[0], w, true);
            w.Suppressed = false;
            CloseChain(group[0].Chain.Length, group[0].Namespace, w);
        }
    }

    /// <summary>Emits the isolated Union member read under its enclosing record's input policy.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    internal static void EmitCheckedMemberRead(PayloadRenderModel model, CodeWriter w)
    {
        string reader = w.A("LuminPackReader");
        w.Line(w.Preserve);
        w.Line("internal static void Read_" + model.Readable + "<" + Profile + ">(scoped ref " + reader +
            " state, scoped ref " + model.GlobalName + "? value)");
        w.Indented(() => w.Line("where " + Profile + " : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line("ReadUnion_" + model.Readable + "<" + Profile + ">(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
        w.Line("private static void ReadUnion_" + model.Readable + "<" + Profile + ">(scoped ref " + reader +
            " reader, scoped ref " + model.GlobalName + "? value)");
        w.Indented(() => w.Line("where " + Profile + " : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        ReadSwitch(model, w);
        w.Close();
    }

    /// <summary>Reads the Union marker and dispatches the same case switch in either input scope.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    internal static void ReadSwitch(PayloadRenderModel model, CodeWriter w) => ReadSwitch(model, w, PlanFixedReads(model, w));

    /// <summary>Emits a reusable read switch with its expanded case bodies already captured.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="fixedReads">The planned fixed reads in case order.</param>
    private static void ReadSwitch(PayloadRenderModel model, CodeWriter w, BinaryEmitter.UnionCaseRead[] fixedReads)
    {
        Reject(model, w);
        w.Line("uint tag = reader.Read<uint>();");
        w.Blank();
        var blocks = SharedBlocks.Plan(fixedReads, "readBlock");
        blocks.Declare(model, w, reuse: true);
        w.Line("switch (tag)");
        w.Open();
        ReadArms(model, w, fixedReads, blocks, true, "reader", "value", "return;");
        w.Close();
        blocks.Emit(w, "reader", reuse: true, "value", "return;");
    }

    /// <summary>Emits the marked case switch of a read root, fresh or seeded from an existing instance.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="fixedReads">The planned fixed reads in case order.</param>
    /// <param name="blocks">The checked block reads that same-width fixed cases share.</param>
    /// <param name="reuse">Whether to reuse the existing destination.</param>
    /// <param name="reader">The reader positioned at the input value.</param>
    /// <param name="target">The destination the cases publish.</param>
    /// <param name="exit">The statement that leaves a completed case.</param>
    private static void ReadArms(PayloadRenderModel model, CodeWriter w, BinaryEmitter.UnionCaseRead[] fixedReads,
        SharedBlocks blocks, bool reuse, string reader, string target, string exit)
    {
        int instances = 0, cursors = 0;
        for (int member = 0; member < model.UnionCases.Length; member++)
        {
            var item = model.UnionCases[member];
            w.Line("case " + NameHash(item) + ":");
            w.Open();
            if (item.OwnEntry)
            {
                w.Line(item.Type + " member" + Number(member) + " = " + (reuse
                    ? StructCase(item) ? "(" + target + " is " + item.Type + " existing" + Number(member) + " ? existing" + Number(member) + " : default);"
                        : w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(" + target + ")!;"
                    : "default !;"));
                // A struct case reads its boxed value through its payload owner, which every struct file publishes.
                w.Line(NeedsCheckedCase(model, item)
                    ? (model.Redirect?.RequestUnionCase(item.Model) ??
                       w.A("LuminPackSerializer") + ".LuminUnionInput_" + model.Readable + ".Read_" + item.Readable) +
                      "<" + Prefix(item) + (item.ReadGeneric ? Profile : w.A(model.UnionStandardBinary)) + ">(ref " + reader + ", ref member" + Number(member) + ");"
                    : StructCase(item)
                    ? CaseOwner(model, item) + ".Read<" + Profile + ">(ref " + reader + ", ref member" + Number(member) + ");"
                    : w.A("LuminPackSerializer") + ".DeserializeUnion" + (model.UniqueEntries ? "_" + item.Readable : string.Empty) +
                    (item.ReadGeneric ? "<" + Prefix(item) + Profile + ">" : item.Arguments.Length != 0 ? "<" + item.Arguments + ">" : string.Empty) +
                    "(ref " + reader + ", ref member" + Number(member) + ");");
                w.Line(target + " = " + Assign(model, item, "member" + Number(member)) + ";");
                w.Line(exit);
                w.Close();
                w.Blank();
                continue;
            }
            w.Line(item.Type + " member" + Number(member) + " = " + (reuse
                ? w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(" + target + ")!;"
                : "default !;"));
            var fixedRead = fixedReads[member];
            var lines = fixedRead.Lines;
            int instance = lines.Count != 0 && lines[0].StartsWith("var instance = ", StringComparison.Ordinal) ? instances++ : -1;
            var body = RenameLocals(lines, member, instance, ref cursors, reader);
            if (fixedRead.SharedBlock is { } sharedBlock)
                body = blocks.Redirect(body, sharedBlock, w, "instance" + Number(instance), reuse ? null : target, "member" + Number(member));
            foreach (string line in body)
                w.Line(line);
            w.Blank();
            // A reused exact case is already in the slot; skip the reference store and its write barrier.
            string assigned = Assign(model, item, "member" + Number(member));
            w.Line(reuse
                ? "if (!global::System.Object.ReferenceEquals(" + target + ", member" + Number(member) + ")) " + target + " = " + assigned + ";"
                : target + " = " + assigned + ";");
            w.Line(exit);
            w.Close();
            w.Blank();
        }
        w.Line("default:");
        w.Open();
        w.Line("if (tag == 0u) { " + target + " = default; " + exit + " }");
        if (model.Registration)
        {
            w.Line("if (tag == uint.MaxValue)");
            w.Open();
            w.Line(reader + "." + (model.UnionWide ? "TryPeekWideUnionHeader" : "TryPeekUnionHeader") + "(out ushort registeredTag);");
            w.Line("if (" + "global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(" + "registeredTag" + ", out var registeredFormatter" +
                string.Empty + ") && registeredFormatter" + string.Empty + ".ReadValue != null)");
            w.Open();
            w.Line(model.GlobalName + " registeredValue" + string.Empty + " = default !;");
            w.Line("((" + "global::LuminPack.Code.LuminPackReadDelegate<" + model.GlobalName + ">)registeredFormatter" + string.Empty +
                ".ReadValue)(ref " + reader + ", ref registeredValue" + string.Empty + ");");
            w.Line(target + " = registeredValue" + string.Empty + ";");
            w.Line(exit);
            w.Close();
            w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(registeredTag, typeof(" + model.GlobalName + "));");
            w.Close();
            w.Blank();
        }
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException($\"Unknown Union hash: 0x{tag:X8}.\");");
        w.Line(exit);
        w.Close();
    }

    /// <summary>Emits one fresh Union element read in place: the header check and the case arms, each ending at a label.</summary>
    /// <param name="model">The Union root render model.</param>
    /// <param name="w">The owner writer, whose operation profile is <c>TLuminProfile</c> and whose reader is <c>reader</c>.</param>
    /// <param name="target">The local that receives the element.</param>
    /// <param name="label">The label every arm jumps to after assigning the element.</param>
    /// <param name="reader">The reader local.</param>
    /// <remarks>This is the original fresh Union loop: a collection reading into a new storage array expands the dispatch per
    /// element instead of calling the root's read entry.</remarks>
    internal static void FreshElementRead(PayloadRenderModel model, CodeWriter w, string target, string label, string reader = "reader")
    {
        Reject(model, w);
        w.Line("uint tag = " + reader + ".Read<uint>();");
        w.Blank();
        var fixedReads = PlanFixedReads(model, w);
        var blocks = SharedBlocks.Plan(fixedReads, label + "Block");
        blocks.Declare(model, w, reuse: false);
        w.Line("// Union cases");
        w.Line("switch (tag)");
        w.Open();
        ReadArms(model, w, fixedReads, blocks, false, reader, target, "goto " + label + ";");
        w.Close();
        blocks.Emit(w, reader, reuse: false, target, "goto " + label + ";");
    }

    /// <summary>The checked block reads of one case switch that two or more fixed cases of the same payload width share.</summary>
    /// <remarks>Each case keeps its own allocation or reuse, its field-address proof and its field-wise fallback; a proven case
    /// selects its destination block and jumps to the one checked read of its width. A fresh case assigns its local target
    /// before the jump, which its caller publishes only after the label; a reused case publishes after the shared read, as
    /// its own arm does.</remarks>
    private sealed class SharedBlocks
    {
        private readonly string _prefix;
        private readonly List<string> _widths;

        /// <summary>Creates the shared reads for the widths that occur more than once.</summary>
        /// <param name="prefix">The label and local prefix, unique in the enclosing method.</param>
        /// <param name="widths">The shared payload widths in first-case order.</param>
        private SharedBlocks(string prefix, List<string> widths)
        {
            _prefix = prefix;
            _widths = widths;
        }

        /// <summary>Finds the payload widths that two or more fixed cases read as one checked block.</summary>
        /// <param name="fixedReads">The planned fixed reads in case order.</param>
        /// <param name="prefix">The label and local prefix.</param>
        /// <returns>The plan, empty when no width repeats.</returns>
        internal static SharedBlocks Plan(BinaryEmitter.UnionCaseRead[] fixedReads, string prefix)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var read in fixedReads)
            {
                if (read.SharedBlock is not { } block)
                    continue;
                string width = block.Width;
                if (counts.TryGetValue(width, out int count))
                    counts[width] = count + 1;
                else
                {
                    counts.Add(width, 1);
                    order.Add(width);
                }
            }
            return new SharedBlocks(prefix, order.Where(width => counts[width] > 1).ToList());
        }

        /// <summary>Declares the shared destination block and, for a reused case, its selected object.</summary>
        /// <param name="model">The Union root.</param>
        /// <param name="w">The source writer.</param>
        /// <param name="reuse">Whether the switch reuses the existing destination.</param>
        internal void Declare(PayloadRenderModel model, CodeWriter w, bool reuse)
        {
            if (_widths.Count == 0)
                return;
            w.Line("ref byte " + _prefix + "Target = ref " + w.A("Unsafe") + ".NullRef<byte>();");
            if (reuse)
                w.Line(model.GlobalName + " " + _prefix + "Owner = default!;");
        }

        /// <summary>Replaces a proven case's own block read with the jump to its shared read.</summary>
        /// <param name="body">The renamed case body.</param>
        /// <param name="block">The checked block selected by the case's proof.</param>
        /// <param name="w">The source writer.</param>
        /// <param name="instance">The renamed instance local.</param>
        /// <param name="freshTarget">The fresh local target, or null for a reused destination.</param>
        /// <param name="member">The case object local.</param>
        /// <returns>The case body.</returns>
        internal List<string> Redirect(List<string> body, BinaryEmitter.CheckedUnionBlock block, CodeWriter w, string instance,
            string? freshTarget, string member)
        {
            int index = _widths.IndexOf(block.Width);
            if (index < 0)
                return body;
            var result = new List<string>(body.Count + 4);
            result.AddRange(body.Take(block.ReadIndex));
            result.Add("{");
            result.Add(_prefix + "Target = ref " + BinaryEmitter.Bytes(w, block.First, instance, readOnly: false, plain: true) + ";");
            result.Add((freshTarget ?? _prefix + "Owner") + " = " + member + ";");
            result.Add("goto " + Label(index) + ";");
            result.Add("}");
            result.AddRange(body.Skip(block.ReadIndex + 1));
            return result;
        }

        /// <summary>Emits each shared checked block read after the switch.</summary>
        /// <param name="w">The source writer.</param>
        /// <param name="reader">The reader local.</param>
        /// <param name="reuse">Whether the switch reuses the existing destination.</param>
        /// <param name="target">The destination the cases publish.</param>
        /// <param name="exit">The statement that leaves a completed case.</param>
        internal void Emit(CodeWriter w, string reader, bool reuse, string target, string exit)
        {
            for (int index = 0; index < _widths.Count; index++)
            {
                w.Blank();
                w.Line(Label(index) + ":");
                w.Line(reader + ".Read(ref " + _prefix + "Target, " + _widths[index] + ");");
                if (reuse)
                    w.Line("if (!global::System.Object.ReferenceEquals(" + target + ", " + _prefix + "Owner)) " + target + " = " + _prefix + "Owner;");
                w.Line(exit);
            }
        }

        /// <summary>Spells the label of a shared read.</summary>
        /// <param name="index">The shared width index.</param>
        /// <returns>The label.</returns>
        private string Label(int index) => _prefix + Number(index);

    }

    /// <summary>Rejects an input-selected case graph before reading or writing its framing.</summary>
    /// <param name="model">The Union contract.</param>
    /// <param name="w">The source writer.</param>
    private static void Reject(PayloadRenderModel model, CodeWriter w)
    {
        if (model.Rejection != null)
            w.Line(BinaryEmitter.RejectUnsupported(model.Rejection));
    }

    /// <summary>Spells the fixed read's locals for this arm without renaming user names in its operands.</summary>
    /// <param name="body">The fixed case statements, before arm-local substitution.</param>
    /// <param name="member">The case value local's index.</param>
    /// <param name="instance">The instance local's index, or a negative value if no instance is declared.</param>
    /// <param name="cursorCount">The number of cursor locals emitted before this arm, updated for the following arm.</param>
    /// <param name="reader">The reader variable in the receiving operation.</param>
    /// <returns>The original statement layout with only the generated identifiers changed.</returns>
    private static List<string> RenameLocals(List<string> body, int member, int instance, ref int cursorCount, string reader)
    {
        const string opening = "{\n";
        string source = opening + string.Join("\n", body) + "\n}";
        var block = SyntaxFactory.ParseStatement(source);
        var names = new Dictionary<string, string>(StringComparer.Ordinal) { ["value"] = "member" + Number(member) };
        if (instance >= 0)
            names.Add("instance", "instance" + Number(instance));
        if (reader != "reader")
            names.Add("reader", reader);
        foreach (var local in block.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            string name = local.Identifier.Text;
            if (local.Parent is VariableDeclarationSyntax { Type: RefTypeSyntax } && name.StartsWith("cursor", StringComparison.Ordinal))
                names.Add(name, "cursor" + Number(cursorCount++));
        }
        var changes = new List<TextChange>();
        foreach (var token in block.DescendantTokens())
        {
            if (names.TryGetValue(token.Text, out string replacement) && replacement != token.Text && IsLocalIdentifier(token))
                changes.Add(new TextChange(token.Span, replacement));
        }
        if (changes.Count == 0)
            return body;
        // Token spans are in physical source order. Keep Roslyn's line mapping when any input entry spans multiple lines.
        if (body.Any(static line => line.IndexOfAny(PhysicalLineBreaks) >= 0))
            return SourceText.From(source).WithChanges(changes).Lines.Skip(1).Take(body.Count).Select(line => line.ToString()).ToList();

        var result = new List<string>(body);
        using var buffer = new PooledTextBuffer(128);
        int lineStart = opening.Length, changeIndex = 0;
        for (int lineIndex = 0; lineIndex < body.Count && changeIndex < changes.Count; lineIndex++)
        {
            string line = body[lineIndex];
            int lineEnd = lineStart + line.Length;
            if (changes[changeIndex].Span.Start < lineEnd)
            {
                int copied = 0;
                buffer.Length = 0;
                do
                {
                    TextChange edit = changes[changeIndex++];
                    int start = edit.Span.Start - lineStart;
                    buffer.Append(line, copied, start - copied).Append(edit.NewText);
                    copied = edit.Span.End - lineStart;
                }
                while (changeIndex < changes.Count && changes[changeIndex].Span.Start < lineEnd);
                result[lineIndex] = buffer.Append(line, copied, line.Length - copied).ToString();
            }
            lineStart = lineEnd + 1;
        }
        return result;
    }

    private static readonly char[] PhysicalLineBreaks = { '\n', '\r', '\u0085', '\u2028', '\u2029' };

    /// <summary>Distinguishes a local declaration or use from a member, named argument, or type name.</summary>
    /// <param name="token">An identifier matching one of the generated local names.</param>
    /// <returns>Whether the identifier can refer to the generated local.</returns>
    private static bool IsLocalIdentifier(SyntaxToken token)
    {
        if (token.Parent is VariableDeclaratorSyntax local)
            return local.Identifier == token;
        return token.Parent is IdentifierNameSyntax name && !SyntaxFacts.IsInNamespaceOrTypeContext(name) && (name.Parent switch
        {
            MemberAccessExpressionSyntax member => member.Expression == name,
            MemberBindingExpressionSyntax or NameColonSyntax => false,
            _ => true
        });
    }

    /// <summary>Spells a local's occurrence number, which the first occurrence leaves off.</summary>
    /// <param name="index">The zero-based item index.</param>
    /// <returns>The resulting text.</returns>
    private static string Number(int index) => index == 0 ? string.Empty : (index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether the read root stays inside the runtime's sixteen-call inlining budget.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="fixedReads">The planned fixed reads in case order.</param>
    /// <returns>Whether the read root qualifies for the original inline hint.</returns>
    private static bool ReadKeepsInline(PayloadRenderModel model, BinaryEmitter.UnionCaseRead[] fixedReads)
    {
        // Preserve the budget's two contributions per expanded fixed case, which both use the same statements.
        int calls = 8;
        for (int index = 0; index < model.UnionCases.Length; index++)
            calls += model.UnionCases[index].OwnEntry ? 2 : InvocationCounter.Count(fixedReads[index].Lines) * 2 + 1;
        return calls <= 16;
    }

    /// <summary>Builds each expanded fixed case once in the Union root's input scope.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="w">The source writer.</param>
    /// <returns>The checked read plans in case order. Cases with their own read entry leave their slot empty.</returns>
    private static BinaryEmitter.UnionCaseRead[] PlanFixedReads(PayloadRenderModel root, CodeWriter w)
    {
        var reads = new BinaryEmitter.UnionCaseRead[root.UnionCases.Length];
        for (int index = 0; index < reads.Length; index++)
        {
            var item = root.UnionCases[index];
            if (item.OwnEntry)
                continue;
            PayloadRenderModel model = item.Model;
            if ((model.Trusted || root.Redirect != null) && root.Limits is { } limits)
                model = model.WithInput(limits, recursive: false, root.Redirect);
            reads[index] = BinaryEmitter.UnionReadBody(model, w);
        }
        return reads;
    }

    /// <summary>Whether the case needs its own copy under a different Union root policy.</summary>
    /// <param name="root">The Union root.</param>
    /// <param name="item">The case.</param>
    /// <returns>Whether to call the Union-specific checked body.</returns>
    internal static bool NeedsCheckedCase(PayloadRenderModel root, UnionRenderCase item) =>
        root.Rejection == null && root.Limits is { } limits && item.OwnEntry &&
        item.Model is { Rejection: null } model &&
        (model.TypeParameters.Length == 0 || item.Arguments.Length != 0) &&
        (!model.IsStruct || model.ManagedStruct) && (root.Redirect != null || model.Trusted || limits.Explicit && !limits.Equals(model.Limits));

    /// <summary>Emits the JSON read body of the root.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The reader positioned at the input value.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    private static void JsonReadBody(PayloadRenderModel model, CodeWriter w, string reader, string type)
    {
        OpenJsonCases(w, reader);
        foreach (var item in model.UnionCases)
        {
            w.Line("case " + item.Id.ToString(CultureInfo.InvariantCulture) + ":");
            w.Open();
            string member = "member" + Number(Array.IndexOf(model.UnionCases, item));
            w.Line(item.Type + " " + member + " = " + (StructCase(item)
                ? "(value is " + item.Type + " existing" + member + " ? existing" + member + " : default);"
                : w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(value)!;"));
            w.Line("global::LuminPack.Binary_" + model.Extension + "_" + item.Readable + ".Read<" + Prefix(item) + Profile + ">(ref reader, ref " + member + ");");
            w.Line("value = " + Assign(model, item, member) + ";");
            w.Line("break;");
            w.Close();
            w.Blank();
        }
        w.Line("default:");
        w.Open();
        if (model.Registration)
        {
            w.Line("if (" + "global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(tag, out var registeredFormatter) && registeredFormatter.ReadValueJson != null)");
            w.Open();
            w.Line(model.GlobalName + " registeredValue = default !;");
            w.Line("((global::LuminPack.Code.LuminPackJsonReadDelegate<" + model.GlobalName + ">)registeredFormatter.ReadValueJson)(ref reader, ref registeredValue);");
            w.Line("value = registeredValue;");
            w.Line("break;");
            w.Close();
            w.Blank();
        }
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(tag, typeof(" + model.GlobalName + "));");
        w.Line("break;");
        w.Close();
        w.Close();
        w.Close();
    }

    /// <summary>Emits JSON Union framing and opens the case switch inside the property loop.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="reader">The JSON reader type name used to qualify token kinds.</param>
    internal static void OpenJsonCases(CodeWriter w, string reader)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = default;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("reader.TryConsumeObjectStart<" + Profile + ">();");
        w.Line("ushort tag = 0;");
        w.Line("while (reader.Read<" + Profile + ">())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd)");
        w.Indented(() => w.Line("return;"));
        w.Line("if (reader.CurrentTokenType != " + reader + ".JsonTokenType.String)");
        w.Indented(() => w.Line("continue;"));
        w.Line("int local;");
        w.Line("if (default(" + Profile + ").JsonByteCount(1) == 1)");
        w.Open();
        w.Line("var propertyName = reader.ReadStringUtf8<" + Profile + ">();");
        PropertyMatch(w, "propertyName");
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("var propertyName2 = reader.ReadStringUtf16<" + Profile + ">();");
        PropertyMatch(w, "propertyName2");
        w.Close();
        w.Blank();
        w.Line("reader.CompleteValue();");
        w.Line("if (!reader.Read<" + Profile + ">())");
        w.Indented(() => w.Line("break;"));
        w.Line("if (local == 1)");
        w.Open();
        w.Line("tag = (ushort)reader.Read<int, " + Profile + ">();");
        w.Line("continue;");
        w.Close();
        w.Blank();
        w.Line("if (local != 2)");
        w.Open();
        w.Line("reader.Skip<" + Profile + ">();");
        w.Line("continue;");
        w.Close();
        w.Blank();
        w.Line("// Union cases");
        w.Line("switch (tag)");
        w.Open();
    }

    /// <summary>Emits the union property index comparison of a JSON property name local.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="name">The name to look up or emit.</param>
    private static void PropertyMatch(CodeWriter w, string name)
    {
        w.Line("local = " + name + ".Length == 5 &&");
        w.Line("    " + name + "[0] == 36 &&");
        w.Line("    " + name + "[1] == 116 &&");
        w.Line("    " + name + "[2] == 121 &&");
        w.Line("    " + name + "[3] == 112 &&");
        w.Line("    " + name + "[4] == 101");
        w.Line("    ? 1");
        w.Line("    : " + name + ".Length == 6 &&");
        w.Line("        " + name + "[0] == 36 &&");
        w.Line("        " + name + "[1] == 118 &&");
        w.Line("        " + name + "[2] == 97 &&");
        w.Line("        " + name + "[3] == 108 &&");
        w.Line("        " + name + "[4] == 117 &&");
        w.Line("        " + name + "[5] == 101");
        w.Line("        ? 2");
        w.Line("        : 0;");
    }

    /// <summary>Emits the cursor root methods of a fixed-cursor Union.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The writer receiving the output.</param>
    private static void CursorRoot(PayloadRenderModel model, CodeWriter w, string writer)
    {
        string suffix = model.UnionSuffix;
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line(Hidden(w));
        w.Line("public virtual int WriteCursor_" + suffix + "(ref " + writer + " writer, int initialIndex)");
        w.Open();
        w.Line("return WriteCursorFallback_" + suffix + "(this, ref writer, initialIndex);");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line(Hidden(w));
        w.Line("protected static int WriteCursorFallback_" + suffix + "(" + model.GlobalName + " value, ref " + writer + " writer, int initialIndex)");
        w.Open();
        w.Line("writer.CurrentIndex = initialIndex;");
        w.Line("value.Write_" + suffix + "<" + w.A(model.UnionStandardBinary) + ">(ref writer);");
        w.Line("return writer.Position;");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits the runtime registration members of the binary root.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    private static void Registration(PayloadRenderModel model, CodeWriter w, string type)
    {
        if (model.Registration && model.TypeParameters.Length != 0)
        {
            w.Line("private static readonly bool LuminPackUnionTags_" + model.UnionSuffix +
                " = global::LuminPack.Code.LuminPackUnionFormatter<" + type + ">.ReserveTags(new int[] { " +
                string.Join(", ", model.UnionCases.Select(item => item.Id.ToString(CultureInfo.InvariantCulture)).Distinct()) + " });");
            w.Blank();
        }
    }

    /// <summary>Emits the root's virtual write slots in profile order.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    private static void RootSlots(PayloadRenderModel model, CodeWriter w, string writer, bool json)
    {
        string operation = json ? "WriteJson_" : "Write_";
        string helper = json ? "WriteRegisteredJson_" : "WriteRegistered_";
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        string modifier = model.UnionInterface ? string.Empty : "public virtual ";
        foreach (var slot in Slots(model, json))
        {
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line(modifier + "void " + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + state + " writer)");
            w.Indented(() => w.Line((slot.Profile == Profile ? "where " + Profile + " : struct, " + w.A("ILuminPackOptions") + " " : string.Empty) +
                "=> " + helper + model.UnionSuffix + "(this, ref writer);"));
            w.Blank();
        }
    }

    /// <summary>Checks whether the Union publishes its writer beside the serializer.</summary>
    /// <param name="model">The normalized Union contract.</param>
    /// <returns>Whether the writer uses only exact comparisons.</returns>
    private static bool StaticDispatch(PayloadRenderModel model) => (UnionDispatchTier)model.UnionTier != UnionDispatchTier.Virtual;

    /// <summary>Formats an unsigned case-name constant for generated binary switch and write operations.</summary>
    /// <param name="item">The declared case.</param>
    /// <returns>The hexadecimal C# constant.</returns>
    private static string NameHash(UnionRenderCase item) => "0x" + item.NameHash.ToString("X8", CultureInfo.InvariantCulture) + "u";

    /// <summary>Spells the name hash as a signed constant for the cached generic write dispatch.</summary>
    /// <param name="item">The declared case.</param>
    /// <returns>The signed constant with the same four bytes as the name hash.</returns>
    private static string GenericDispatchId(UnionRenderCase item) => "unchecked((int)" + NameHash(item) + ")";

    /// <summary>Dispatches a closed binary writer to concrete slots before trying the defining root's virtual slots.</summary>
    /// <param name="model">The closed Union contract.</param>
    /// <param name="w">The formatter's source writer.</param>
    internal static void EmitClosedWriteDispatch(UnionModel model, CodeWriter w)
    {
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.EnsureAdditionalCapacity(4);");
        w.Line("writer.Write(0u);");
        w.Line("return;");
        w.Close();
        w.Line("bool useDefault = " + MatchesDeclaredBinaryProfile(w, model.StandardBinary) + ";");
        string receiver = ExactTypeReceiver(w, model.HidesGetType);
        string branch = "if";
        foreach (UnionCaseModel item in model.Cases.OrderBy(item => item.Tag))
        {
            w.Line(branch + " (" + receiver + ".GetType() == typeof(" + item.Type.GlobalName + "))");
            w.Open();
            w.Line("var concreteValue = " + w.A("Unsafe") + ".As<" + item.Type.GlobalName + ">(value);");
            Slots("concreteValue");
            w.Line("return;");
            w.Close();
            branch = "else if";
        }
        Slots("value");

        void Slots(string target)
        {
            string slot = target + ".Write_" + model.ClosedWriteSlot;
            w.Line("if (useDefault) " + slot + "_Default(ref writer);");
            w.Line("else " + slot + "<" + Profile + ">(ref writer);");
        }
    }

    /// <summary>Spells the receiver that resolves each dispatch comparison to <c>object.GetType</c>.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="hidesGetType">Whether the Union root can hide the inherited method.</param>
    /// <returns>The receiver expression for each comparison.</returns>
    private static string ExactTypeReceiver(CodeWriter w, bool hidesGetType)
    {
        if (!hidesGetType)
            return "value";
        w.Line("object caseValue = value;");
        return "caseValue";
    }

    /// <summary>Spells the binary virtual slot's selection of the declared profile.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="alias">The declaring root's standard binary profile alias.</param>
    /// <returns>The generated profile comparison.</returns>
    private static string MatchesDeclaredBinaryProfile(CodeWriter w, string alias)
    {
        string standard = w.A(alias);
        return "default(" + Profile + ").StringEncoding == default(" + standard + ").StringEncoding && default(" + Profile +
            ").StringRecording == default(" + standard + ").StringRecording";
    }

    /// <summary>Emits compact exact-type branches to the concrete case writers.</summary>
    /// <param name="model">The normalized Union contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="json">Whether this operation writes JSON.</param>
    private static void ExactDispatch(PayloadRenderModel model, CodeWriter w, bool json)
    {
        string operation = json ? "WriteJsonCase" : "WriteCase";
        string arguments = model.TypeArguments(Profile);
        string receiver = ExactTypeReceiver(w, model.UnionHidesGetType);
        int index = 0;
        foreach (UnionRenderCase item in model.UnionCases.OrderBy(item => item.Id))
        {
            string branch = index == 0 ? "if" : "else if";
            w.Line(branch + " (" + receiver + ".GetType() == typeof(" + item.Type + ")) { " + operation + Number(index++) + arguments + "(ref writer, value); return; }");
        }
        if (model.Registration)
        {
            if (!json)
            {
                w.Line("WriteRegistered_" + model.UnionSuffix + model.TypeArguments() + "(value, ref writer);");
                return;
            }
            w.Line("if (global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(((object)value).GetType(), out var registration) && registration.WriteValueJson is not null)");
            w.Open();
            w.Line("((global::LuminPack.Code.LuminPackUnionJsonWriteDelegate<" + model.GlobalName + ">)registration.WriteValueJson)(ref writer, ref value);");
            w.Line("return;");
            w.Close();
        }
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));");
    }

    /// <summary>Emits the known case bodies in the serializer so the user declarations need no virtual slots.</summary>
    /// <param name="model">The normalized Union contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="constraints">The root type constraints.</param>
    /// <param name="json">Whether this operation writes JSON.</param>
    private static void ExactWriters(PayloadRenderModel model, CodeWriter w, string constraints, bool json)
    {
        string operation = json ? "WriteJsonCase" : "WriteCase";
        string state = w.A(json ? "LuminPackJsonWriter" : "LuminPackWriter");
        string arguments = model.TypeArguments(Profile);
        // Several variable-size cases would each inline their capacity and growth path into the Union collection loop.
        bool outlineVariableCases = !json && model.UnionCases.Count(item => item.Definition == null && !item.FixedCursor) >= 5;
        int index = 0;
        foreach (UnionRenderCase item in model.UnionCases.OrderBy(item => item.Id))
        {
            bool outline = outlineVariableCases && item.Definition == null && !item.FixedCursor;
            w.Line(w.MethodImpl(outline ? "NoInlining" : "AggressiveInlining"));
            w.Line("private static void " + operation + Number(index++) + arguments + "(ref " + state + " writer, " + model.GlobalName + " value)");
            w.Indented(() => w.Line(constraints + "where " + Profile + " : struct, " + w.A("ILuminPackOptions")));
            w.Open();
            w.Line(StructCase(item)
                ? "var concreteValue = (" + item.Type + ")(object)value;"
                : "var concreteValue = " + w.A("Unsafe") + ".As<" + item.Type + ">(value);");
            if (json || item.Definition != null)
                GenericCaseWrite(model, item, w, json, Profile, "concreteValue");
            else if (item.FixedCursor)
            {
                string serializer = w.A("LuminPackSerializer");
                w.Line(item.Model?.ManagedFixedUnionCase == true
                    ? "writer.CommitRecords(ref " + serializer + ".SerializeUnionAtCursor_" + item.Readable + "(ref writer, concreteValue, ref writer.Cursor, " + NameHash(item) + "));"
                    : "writer.CurrentIndex = " + serializer + ".SerializeUnionAt_" + item.Readable + "(ref writer, concreteValue, writer.CurrentIndex, " + NameHash(item) + ");");
            }
            else
            {
                w.Line("writer.EnsureAdditionalCapacity(4);");
                w.Line("writer.Write(" + NameHash(item) + ");");
                w.Line(StructCase(item)
                    ? CaseOwner(model, item) + ".Write<" + Profile + ">(ref writer, in concreteValue);"
                    : w.A("LuminPackSerializer") + ".SerializeUnion_" + item.Readable + "<" + Profile + ">(ref writer, concreteValue);");
            }
            w.Close();
        }
    }

    /// <summary>Emits the generic dispatch path, retaining exact-type dispatch for its existing tier.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    private static void CompiledSlot(PayloadRenderModel model, CodeWriter w, string writer, bool json)
    {
        string operation = json ? "WriteJson_" : "Write_";
        string target = (json ? "WriteJson_" : "Write_") + model.UnionSuffix;
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        w.Line(Hidden(w));
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("public static void " + operation + model.UnionSuffix + "_Compiled<" + Profile + ">(ref " + state + " writer, " + model.GlobalName + " value)");
        w.Indented(() => w.Line("where " + Profile + " : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        Reject(model, w);
        if (!json)
        {
            w.Line("if (" + MatchesDeclaredBinaryProfile(w, model.UnionStandardBinary) + ")");
            w.Open();
            DispatchSlot(target + "_Default", generic: false);
            w.Line("return;");
            w.Close();
        }
        DispatchSlot(target, generic: true);
        w.Close();
        w.Blank();

        void DispatchSlot(string method, bool generic)
        {
            string arguments = generic ? "<" + Profile + ">" : string.Empty;
            w.Line("value." + method + arguments + "(ref writer);");
        }
    }

    /// <summary>Emits the registered-member fallback of the matching format.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="writer">The writer receiving the output.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    private static void RegisteredWriter(PayloadRenderModel model, CodeWriter w, string writer, bool json)
    {
        string delegateName = json ? "LuminPackUnionJsonWriteDelegate" : "LuminPackUnionWriteDelegate";
        string operation = json ? "WriteValueJson" : "WriteValue";
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        bool exact = StaticDispatch(model);
        if (exact)
            w.Line(w.MethodImpl("NoInlining"));
        w.Line("private static void " + (json ? "WriteRegisteredJson_" : "WriteRegistered_") + model.UnionSuffix + (exact ? model.TypeArguments() : string.Empty) +
            "(" + model.GlobalName + " value, ref " + state + " writer)");
        if (exact && model.TypeConstraints.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        if (model.Registration)
        {
            w.Line("if (global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(((object)value).GetType(), out var registration) && registration." + operation + " != null)");
            w.Open();
            if (!json)
            {
                w.Line("writer.EnsureAdditionalCapacity(4);");
                w.Line("writer.Write(uint.MaxValue);");
            }
            w.Line("((global::LuminPack.Code." + delegateName + "<" + model.GlobalName + ">)registration." + operation + ")(ref writer, ref value);");
            w.Close();
            w.Line("else");
            w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));"));
        }
        else
            w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));");
        w.Close();
    }

    /// <summary>Emits one case's slot overrides of one format.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="item">The current item.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    private static void CaseSlots(PayloadRenderModel model, UnionRenderCase item, CodeWriter w, bool json)
    {
        string prefix = model.UnionInterface ? "void " + (item.InterfaceContract ?? model.GlobalName) + "." :
            item.Sealed ? "public sealed override void " : "public override void ";
        string operation = json ? "WriteJson_" : "Write_";
        if (!json && item.FixedCursor)
            foreach (var slot in Slots(model, false))
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A("LuminPackWriter") + " writer)");
                string tag = NameHash(item);
                string serializer = w.A("LuminPackSerializer");
                w.Indented(() => w.Line(item.Model?.ManagedFixedUnionCase == true
                    ? "=> writer.CommitRecords(ref " + serializer + ".SerializeUnionAtCursor_" + item.Readable + "(ref writer, this, ref writer.Cursor, " + tag + "));"
                    : "=> writer.CurrentIndex = " + serializer + ".SerializeUnionAt_" + item.Readable + "(ref writer, this, writer.CurrentIndex, " + tag + ");"));
            }
        else if (!json)
            foreach (var slot in Slots(model, false))
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A("LuminPackWriter") + " writer)");
                w.Open();
                w.Line("writer.EnsureAdditionalCapacity(4);");
                w.Line("writer.Write(" + NameHash(item) + ");");
                w.Line("var concreteValue = this;");
                w.Line(StructCase(item)
                    ? CaseOwner(model, item) + ".Write<" + (slot.Profile == Profile ? Profile : w.A(slot.Profile)) + ">(ref writer, in concreteValue);"
                    : w.A("LuminPackSerializer") + ".SerializeUnion_" + item.Readable + "<" + (slot.Profile == Profile ? Profile : w.A(slot.Profile)) + ">(ref writer, concreteValue);");
                w.Close();
            }
        if (!json && model.UnionCursor)
        {
            w.Blank();
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line((model.UnionInterface ? "int " + model.GlobalName + "." : item.Sealed ? "public sealed override int " : "public override int ") +
                "WriteCursor_" + model.UnionSuffix + "(ref LuminPackWriter writer, int initialIndex)");
            w.Open();
            if (item.FixedCursor)
            {
                w.Line("if (((object)this).GetType() != typeof(" + item.Type + "))");
                w.Indented(() => w.Line("return WriteCursorFallback_" + model.UnionSuffix + "(this, ref writer, initialIndex);"));
                w.Line("return " + w.A("LuminPackSerializer") + ".SerializeUnionAt_" + item.Readable + "(ref writer, this, initialIndex, " +
                    NameHash(item) + ");");
            }
            else
            {
                w.Line("writer.CurrentIndex = initialIndex;");
                w.Line("((" + model.GlobalName + ")this).Write_" + model.UnionSuffix + "<" + w.A(model.UnionStandardBinary) + ">(ref writer);");
                w.Line("return writer.Position;");
            }
            w.Close();
        }
        if (json)
            foreach (var slot in Slots(model, true))
            {
                string profile = (slot.Profile == Profile ? Profile : w.A(slot.Profile));
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A("LuminPackJsonWriter") + " writer)");
                w.Open();
                w.Line("writer.WriteObjectStart<" + profile + ">();");
                w.Line("if (default(" + profile + ").JsonByteCount(1) == 1)");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU8);"));
                w.Line("else");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU16);"));
                w.Line("writer.Write<" + profile + ">(" + item.Id.ToString(CultureInfo.InvariantCulture) + ");");
                w.Line("if (default(" + profile + ").JsonByteCount(1) == 1)");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU8);"));
                w.Line("else");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU16);"));
                w.Line("var concreteValue = this;");
                if (StructCase(item))
                    w.Line(CaseOwner(model, item) + ".Write<" + profile + ">(ref writer, in concreteValue);");
                else
                    w.Line(CaseOwner(model, item) + ".Write<" + profile + ">(ref writer, concreteValue);");
                w.Line("writer.WriteObjectEnd<" + profile + ">();");
                w.Close();
            }
    }

    /// <summary>Groups the cases by the declaration that holds their slots: one group per generic definition.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <returns>The resulting <c>List&lt;UnionRenderCase[]&gt;</c>.</returns>
    private static List<UnionRenderCase[]> CaseGroups(PayloadRenderModel model)
    {
        var groups = new List<UnionRenderCase[]>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in model.UnionCases)
        {
            if (item.Definition == null)
                groups.Add(new[] { item });
            else if (seen.Add(item.Definition))
                groups.Add(model.UnionCases.Where(other => other.Definition == item.Definition).ToArray());
        }
        return groups;
    }

    /// <summary>Spells a generic case's type arguments ahead of the operation profile.</summary>
    /// <param name="item">The current item.</param>
    /// <returns>The resulting text.</returns>
    private static string Prefix(UnionRenderCase item) => item.Arguments.Length == 0 ? string.Empty : item.Arguments + ", ";

    /// <summary>Converts a read case to the root, through object when either side is generic.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="item">The current item.</param>
    /// <param name="member">The member being analyzed or emitted.</param>
    /// <returns>The resulting text.</returns>
    private static string Assign(PayloadRenderModel model, UnionRenderCase item, string member) =>
        item.Definition != null || model.TypeParameters.Length != 0 ? "(" + model.GlobalName + ")(object)" + member : member;

    /// <summary>Emits the case-name hash cached once per closed generic definition and used by both write formats.</summary>
    /// <param name="group">The group supplied to <c>GenericSlotField</c>.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="slotField">The slot field supplied to <c>GenericSlotField</c>.</param>
    private static void GenericSlotField(UnionRenderCase[] group, CodeWriter w, string slotField)
    {
        string open = OpenSpelling(group[0].Definition);
        if (group.Length == 1 && group[0].Type == open)
            return;
        w.Line("private static readonly int " + slotField + " = Resolve" + slotField + "();");
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("private static int Resolve" + slotField + "()");
        w.Open();
        w.Line("// Union cases");
        w.Line("switch (typeof(" + open + "))");
        w.Open();
        foreach (UnionRenderCase item in group)
            w.Line("case var runtimeType when runtimeType == typeof(" + item.Type + "): return " + GenericDispatchId(item) + ";");
        w.Line("default:");
        w.Indented(() => w.Line("return -1;"));
        w.Close();
        w.Close();
        w.Blank();
    }

    /// <summary>Handles generic case slots for <c>UnionEmitter</c>.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="group">The group supplied to <c>GenericCaseSlots</c>.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    private static void GenericCaseSlots(PayloadRenderModel model, UnionRenderCase[] group, CodeWriter w, bool json)
    {
        string definition = group[0].Definition;
        string open = OpenSpelling(definition);
        bool direct = group.Length == 1 && group[0].Type == open;
        string prefix = model.UnionInterface ? "void " + model.GlobalName + "." :
            group.All(item => item.Sealed) ? "public sealed override void " : "public override void ";
        string operation = json ? "WriteJson_" : "Write_";
        string slotField = "LuminPackUnionDispatchSlot_" + model.UnionSuffix;
        foreach (var slot in Slots(model, json))
        {
            string profile = (slot.Profile == Profile ? Profile : w.A(slot.Profile));
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A(json ? "LuminPackJsonWriter" : "LuminPackWriter") + " writer)");
            w.Open();
            if (direct)
                GenericCaseWrite(model, group[0], w, json, profile, "this");
            else
            {
                w.Line("// Union cases");
                w.Line("switch (" + slotField + ")");
                w.Open();
                foreach (UnionRenderCase item in group)
                {
                    w.Line("case " + GenericDispatchId(item) + ":");
                    w.Open();
                    w.Line("var genericValue = this;");
                    w.Line("ref var concreteValue = ref " + w.A("LuminPackMarshal") + ".As<" + open + ", " + item.Type + ">(ref genericValue);");
                    GenericCaseWrite(model, item, w, json, profile, "concreteValue");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                }
                w.Line("default:");
                w.Open();
                w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(GetType(), typeof(" + model.GlobalName + "));");
                w.Line("return;");
                w.Close();
                w.Close();
            }
            w.Close();
            w.Blank();
        }
        if (!json && model.UnionCursor)
        {
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line((model.UnionInterface ? "int " + model.GlobalName + "." : group.All(item => item.Sealed) ? "public sealed override int " : "public override int ") +
                "WriteCursor_" + model.UnionSuffix + "(ref LuminPackWriter writer, int initialIndex)");
            w.Open();
            w.Line("writer.CurrentIndex = initialIndex;");
            w.Line("((" + model.GlobalName + ")this).Write_" + model.UnionSuffix + "<" + w.A(model.UnionStandardBinary) + ">(ref writer);");
            w.Line("return writer.Position;");
            w.Close();
        }
    }

    /// <summary>Writes one generic case's header and payload through its definition's generic entries.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="item">The current item.</param>
    /// <param name="w">The generated source writer.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    /// <param name="profile">The compiled serialization profile or its generated type name.</param>
    /// <param name="value">The value to process.</param>
    private static void GenericCaseWrite(PayloadRenderModel model, UnionRenderCase item, CodeWriter w, bool json, string profile, string value)
    {
        string id = item.Id.ToString(CultureInfo.InvariantCulture);
        if (!json)
        {
            w.Line("writer.EnsureAdditionalCapacity(4);");
            w.Line("writer.Write(" + NameHash(item) + ");");
            w.Line(w.A("LuminPackSerializer") + ".SerializeUnion_" + item.Readable + "<" + Prefix(item) + profile + ">(ref writer, " + value + ");");
            return;
        }
        w.Line("writer.WriteObjectStart<" + profile + ">();");
        w.Line("if (default(" + profile + ").JsonByteCount(1) == 1)");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU8);"));
        w.Line("else");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU16);"));
        w.Line("writer.Write<" + profile + ">(" + id + ");");
        w.Line("if (default(" + profile + ").JsonByteCount(1) == 1)");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU8);"));
        w.Line("else");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU16);"));
        w.Line("global::LuminPack.Binary_" + model.Extension + "_" + item.Readable + ".Write<" + Prefix(item) + profile + ">(ref writer, " + (StructCase(item) ? "in " : "") + value + ");");
        w.Line("writer.WriteObjectEnd<" + profile + ">();");
    }

    /// <summary>Spells a generic definition with its own parameter names, as the case's partial declaration sees itself.</summary>
    /// <param name="definition">The definition supplied to <c>OpenSpelling</c>.</param>
    /// <returns>The resulting text.</returns>
    private static string OpenSpelling(string definition) => definition;

    /// <summary>Whether a case is a struct implementing an interface root.</summary>
    /// <param name="item">The current item.</param>
    /// <returns>Whether the input satisfies the struct case check.</returns>
    private static bool StructCase(UnionRenderCase item) => item.Chain.Length != 0 &&
        item.Chain[item.Chain.Length - 1].IndexOf(" struct ", StringComparison.Ordinal) >= 0;

    /// <summary>Spells a case's payload owner, which publishes its binary and JSON operations.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="item">The current item.</param>
    /// <returns>The resulting text.</returns>
    private static string CaseOwner(PayloadRenderModel model, UnionRenderCase item) =>
        "global::LuminPack.Binary_" + model.Extension + "_" + item.Readable;

    /// <summary>Lists one format's profile slots.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    /// <returns>The resulting <c>UnionRenderSlot[]</c>.</returns>
    private static UnionRenderSlot[] Slots(PayloadRenderModel model, bool json) => json ? new[]
    {
        new UnionRenderSlot(string.Empty, Profile, string.Empty, 0, 0),
    } : new[]
    {
        new UnionRenderSlot(string.Empty, Profile, string.Empty, 0, 0),
        new UnionRenderSlot("_Default", model.UnionStandardBinary, string.Empty, 0, 0),
    };

    /// <summary>Spells the arbitrary-profile slot's generic parameter list.</summary>
    /// <param name="slot">The slot supplied to <c>SlotArguments</c>.</param>
    /// <returns>The resulting text.</returns>
    private static string SlotArguments(UnionRenderSlot slot) => slot.Profile == Profile ? "<" + Profile + ">" : string.Empty;

    /// <summary>Opens a namespace and the root's partial type chain.</summary>
    /// <param name="namespace">The namespace supplied to <c>OpenType</c>.</param>
    /// <param name="chain">The chain supplied to <c>OpenType</c>.</param>
    /// <param name="w">The generated source writer.</param>
    private static void OpenType(string @namespace, string[] chain, CodeWriter w)
    {
        if (@namespace != null)
        {
            w.Line("namespace " + @namespace);
            w.Open();
        }
        foreach (string declaration in chain)
        {
            w.Line(declaration);
            w.Open();
        }
    }

    /// <summary>Closes a partial type chain and its namespace.</summary>
    /// <param name="depth">The current nesting depth.</param>
    /// <param name="namespace">The namespace supplied to <c>CloseChain</c>.</param>
    /// <param name="w">The generated source writer.</param>
    private static void CloseChain(int depth, string @namespace, CodeWriter w)
    {
        for (int index = 0; index < depth; index++)
            w.Close();
        if (@namespace != null)
            w.Close();
    }

    /// <summary>The hidden-editor attribute line.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <returns>The resulting text.</returns>
    private static string Hidden(CodeWriter w) => "[" + w.Attribute("EditorBrowsable") + "(" + w.A("EditorBrowsableState") + ".Never)]";
}
