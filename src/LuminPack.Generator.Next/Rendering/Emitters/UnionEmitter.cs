#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

/// <summary>Writes a covered Union root's published binary and JSON files: the root operations, the payload extensions, the
/// virtual dispatch slots, the runtime registration and every case's slot overrides.</summary>
/// <remarks>Union roots publish the same slot contract in both formats. The lines are the original emitter's own lines, with
/// helper names spelled through the consumer's aliases; the publication layout wraps long lines and compacts the short case
/// rows of the marked switch statements.</remarks>
internal static class UnionEmitter
{
    private const string Profile = "TLuminProfile";
    private static readonly Regex ValueWord = new(@"\bvalue\b", RegexOptions.CultureInvariant);
    private static readonly Regex ReaderWord = new(@"\breader\b", RegexOptions.CultureInvariant);
    private static readonly Regex InstanceWord = new(@"\binstance\b", RegexOptions.CultureInvariant);
    private static readonly Regex CursorWord = new(@"\bcursor([0-9]*)\b", RegexOptions.CultureInvariant);

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
        string inline = w.MethodImpl("AggressiveInlining"), constraint = "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
        string slotPrefix = "Write_" + model.UnionSuffix;

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class " + serializer);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_" + model.Readable + model.TypeArguments() + "(scoped ref " + writer + " writer, scoped in " + type + "? input) => " +
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
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.Cursor = " + w.A("LuminPackCode") + ".NullObject;");
        w.Line("writer.Advance(1);");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(type + "." + slotPrefix + "_Compiled<" + Profile + ">(ref writer, value);");
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        if (ReadKeepsInline(model, w))
            w.Line(inline);
        w.Line("internal static void RootRead" + (model.UnionReadGeneric ? model.TypeArguments(Profile) : model.TypeArguments()) + "(scoped ref " + reader +
            " reader, scoped ref " + type + "? value)");
        if (model.UnionReadGeneric)
            w.Indented(() => w.Line(constraint));
        w.Open();
        w.Line("if (!reader." + (model.UnionWide ? "TryPeekWideUnionHeader" : "TryPeekUnionHeader") + "(out var tag))");
        w.Open();
        w.Line("value = default;");
        w.Line("return;");
        w.Close();
        w.Blank();
        var counters = new SwitchCounters();
        w.Line("switch (tag)");
        w.Open();
        ReadArms(model, w, true, "reader", counters, slotPrefix);
        w.Close();
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_" + model.Readable + model.TypeArguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
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
        w.Line("internal static void Write" + model.TypeArguments() + "(this ref " + writer + " state, in " + type + "? value) => " + serializer + "." + container +
            ".RootWrite" + model.TypeArguments(standard) + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWrite" + model.TypeArguments("TOptions") + "(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments() + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (model.UnionReadGeneric ? model.TypeArguments(standard) : model.TypeArguments()) + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions")));
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
        if (model.UnionReferenced)
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
        string inline = w.MethodImpl("AggressiveInlining"), constraint = "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
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
        w.Line("var value = input;");
        w.Line("if (value is null)");
        w.Open();
        w.Line("writer.WriteNull<" + Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(type + "." + slotPrefix + "_Compiled<" + Profile + ">(ref writer, value);");
        w.Close();
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line("internal static void RootReadJson" + model.TypeArguments(Profile) + "(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint));
        w.Open();
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
            "? input) => " + container + ".RootWriteJson" + model.TypeArguments(standard) + "(ref writer, input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_" + model.Readable + model.TypeArguments(Profile) + "(scoped ref " + writer + " writer, scoped in " + type + "? input)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootWriteJson" + model.TypeArguments(Profile) + "(ref writer, input);"));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_" + model.Readable + model.TypeArguments() + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
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
        w.Line("internal static void Write" + model.TypeArguments() + "(this ref " + writer + " state, in " + type + "? value) => " + serializer + "." + container +
            ".RootWriteJson" + model.TypeArguments(standard) + "(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write" + model.TypeArguments("TOptions") + "(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWriteJson" + model.TypeArguments("TOptions") + "(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments() + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<" + standard + ">())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson" + model.TypeArguments(standard) + "(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read" + model.TypeArguments("TOptions") + "(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson" + model.TypeArguments("TOptions") + "(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Close();

        if (model.UnionReferenced)
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

    /// <summary>The running name counters of the two read switches, which share one method scope.</summary>
    private sealed class SwitchCounters
    {
        internal int Members;
        internal int Instances;
        internal int Cursors;
    }

    /// <summary>Emits the marked case switch of a read root, fresh or seeded from an existing instance.</summary>
    private static void ReadArms(PayloadRenderModel model, CodeWriter w, bool reuse, string reader, SwitchCounters counters, string slotPrefix,
        string? freshTarget = null, string? freshExit = null)
    {
        string target = freshTarget ?? "value";
        string exit = freshExit ?? "return;";
        foreach (var item in model.UnionCases)
        {
            int member = counters.Members++;
            w.Line("case " + item.Id.ToString(CultureInfo.InvariantCulture) + ":");
            w.Open();
            if (item.OwnEntry)
            {
                w.Line(item.Type + " member" + Number(member) + " = " + (reuse
                    ? StructCase(item) ? "(" + target + " is " + item.Type + " existing" + Number(member) + " ? existing" + Number(member) + " : default);"
                        : w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(" + target + ")!;"
                    : "default !;"));
                // A struct case reads its boxed value through its payload owner, which every struct file publishes.
                w.Line(StructCase(item)
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
            var body = BinaryEmitter.UnionReadBody(item.Model, w);
            bool hasInstance = body.Count != 0 && body[0].StartsWith("var instance = ", StringComparison.Ordinal);
            int instance = counters.Instances;
            if (hasInstance)
                counters.Instances++;
            int cursors = counters.Cursors;
            body = Rename(body, member, hasInstance ? instance : -1, cursors, ref counters.Cursors, reader, reuse);
            foreach (string line in body)
                w.Line(line);
            w.Blank();
            w.Line(target + " = member" + Number(member) + ";");
            w.Line(exit);
            w.Close();
            w.Blank();
        }
        w.Line("default:");
        w.Open();
        if (model.Registration)
        {
            w.Line("if (" + "global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(" + "tag" + ", out var registeredFormatter" +
                string.Empty + ") && registeredFormatter" + string.Empty + ".ReadValue != null)");
            w.Open();
            w.Line(model.GlobalName + " registeredValue" + string.Empty + " = default !;");
            w.Line("((" + "global::LuminPack.Code.LuminPackReadDelegate<" + model.GlobalName + ">)registeredFormatter" + string.Empty +
                ".ReadValue)(ref " + reader + ", ref registeredValue" + string.Empty + ");");
            w.Line(target + " = registeredValue" + string.Empty + ";");
            w.Line(exit);
            w.Close();
            w.Blank();
        }
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(" + "tag" + ", typeof(" + model.GlobalName + "));");
        w.Line(exit);
        w.Close();
    }

    /// <summary>Renames the locals of one expanded case body to the running switch names.</summary>
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
        w.Line("if (!" + reader + "." + (model.UnionWide ? "TryPeekWideUnionHeader" : "TryPeekUnionHeader") + "(out var tag))");
        w.Open();
        w.Line(target + " = default;");
        w.Line("goto " + label + ";");
        w.Close();
        w.Blank();
        w.Line("// Union cases");
        w.Line("switch (tag)");
        w.Open();
        ReadArms(model, w, false, reader, new SwitchCounters(), "Write_" + model.UnionSuffix, target, "goto " + label + ";");
        w.Close();
    }

    private static List<string> Rename(List<string> body, int member, int instance, int cursors, ref int cursorCount, string reader, bool reuse)
    {
        string memberName = "member" + Number(member);
        string instanceName = instance < 0 ? null : "instance" + Number(instance);
        var result = new List<string>(body.Count);
        int localCursors = 0;
        foreach (string original in body)
        {
            foreach (Match match in CursorWord.Matches(original))
            {
                int local = match.Groups[1].Value.Length == 0 ? 1 : int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                localCursors = Math.Max(localCursors, local);
            }
        }
        foreach (string original in body)
        {
            string line = ValueWord.Replace(original, memberName);
            if (instanceName != null)
                line = InstanceWord.Replace(line, instanceName);
            string shifted = CursorWord.Replace(line, match =>
            {
                int local = match.Groups[1].Value.Length == 0 ? 0 : int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - 1;
                int index = cursors + local;
                return "cursor" + (index == 0 ? string.Empty : (index + 1).ToString(CultureInfo.InvariantCulture));
            });
            if (reader != "reader")
                shifted = ReaderWord.Replace(shifted, reader);
            result.Add(shifted);
        }
        cursorCount = cursors + localCursors;
        return result;
    }

    /// <summary>Spells a local's occurrence number, which the first occurrence leaves off.</summary>
    private static string Number(int index) => index == 0 ? string.Empty : (index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether the read root stays inside the runtime's sixteen-call inlining budget.</summary>
    private static bool ReadKeepsInline(PayloadRenderModel model, CodeWriter w)
    {
        // The two switches share the peek and the existing-instance call, and each repeats the three registered-member calls.
        int calls = 8;
        foreach (var item in model.UnionCases)
            calls += item.OwnEntry ? 2 : 2 * InvocationCounter.Count(BinaryEmitter.UnionReadBody(item.Model, w)) + 1;
        return calls <= 16;
    }

    /// <summary>Emits the JSON read body of the root.</summary>
    private static void JsonReadBody(PayloadRenderModel model, CodeWriter w, string reader, string type)
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
        w.Line("if (default(" + Profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8)");
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

    /// <summary>Emits the union property index comparison of a JSON property name local.</summary>
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

    /// <summary>Emits the generic dispatch path, retaining exact-type dispatch for its existing tier.</summary>
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
        if (!json)
        {
            string standard = w.A(model.UnionStandardBinary);
            w.Line("if (default(" + Profile + ").StringEncoding == default(" + standard + ").StringEncoding && default(" + Profile +
                ").StringRecording == default(" + standard + ").StringRecording)");
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
            if (model.UnionTier == 1)
            {
                w.Line("switch (value)");
                w.Open();
                for (int index = 0; index < model.UnionTierCases.Length; index++)
                {
                    string type = model.UnionTierCases[index];
                    string variable = "caseValue" + index.ToString(CultureInfo.InvariantCulture);
                    string guard = model.UnionTierSealed[index] ? string.Empty : " when ((object)value).GetType() == typeof(" + type + ")";
                    w.Line("case " + type + " " + variable + guard + ":");
                    w.Indented(() =>
                    {
                        w.Line(variable + "." + method + arguments + "(ref writer);");
                        w.Line("return;");
                    });
                }
                w.Close();
            }
            w.Line("value." + method + arguments + "(ref writer);");
        }
    }

    /// <summary>Emits the registered-member fallback of the matching format.</summary>
    private static void RegisteredWriter(PayloadRenderModel model, CodeWriter w, string writer, bool json)
    {
        string delegateName = json ? "LuminPackUnionJsonWriteDelegate" : "LuminPackUnionWriteDelegate";
        string operation = json ? "WriteValueJson" : "WriteValue";
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        w.Line("private static void " + (json ? "WriteRegisteredJson_" : "WriteRegistered_") + model.UnionSuffix +
            "(" + model.GlobalName + " value, ref " + state + " writer)");
        w.Open();
        if (model.Registration)
        {
            w.Line("if (global::LuminPack.Code.LuminPackUnionFormatter<" + model.GlobalName + ">.TryGetFormatter(((object)value).GetType(), out var registration) && registration." + operation + " != null)");
            w.Indented(() => w.Line("((global::LuminPack.Code." + delegateName + "<" + model.GlobalName + ">)registration." + operation + ")(ref writer, ref value);"));
            w.Line("else");
            w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));"));
        }
        else
            w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));");
        w.Close();
    }

    /// <summary>Emits one case's slot overrides of one format.</summary>
    private static void CaseSlots(PayloadRenderModel model, UnionRenderCase item, CodeWriter w, bool json)
    {
        string prefix = model.UnionInterface ? "void " + model.GlobalName + "." :
            item.Sealed ? "public sealed override void " : "public override void ";
        string operation = json ? "WriteJson_" : "Write_";
        if (!json && item.FixedCursor)
            foreach (var slot in Slots(model, false))
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A("LuminPackWriter") + " writer)");
                w.Indented(() => w.Line("=> writer.CurrentIndex = " + w.A("LuminPackSerializer") + ".SerializeUnionAt_" + item.Readable + "(ref writer, this, writer.CurrentIndex, (byte)" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");"));
            }
        else if (!json)
            foreach (var slot in Slots(model, false))
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + SlotArguments(slot) + "(ref " + w.A("LuminPackWriter") + " writer)");
                w.Open();
                w.Line("writer." + (model.UnionWide ? "WriteWideUnionHeader" : "WriteUnionHeader") + "(" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");");
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
                w.Line("return " + w.A("LuminPackSerializer") + ".SerializeUnionAt_" + item.Readable + "(ref writer, this, initialIndex, (byte)" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");");
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
                w.Line("if (default(" + profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8)");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU8);"));
                w.Line("else");
                w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU16);"));
                w.Line("writer.Write<" + profile + ">(" + item.Id.ToString(CultureInfo.InvariantCulture) + ");");
                w.Line("if (default(" + profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8)");
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
    private static string Prefix(UnionRenderCase item) => item.Arguments.Length == 0 ? string.Empty : item.Arguments + ", ";

    /// <summary>Converts a read case to the root, through object when either side is generic.</summary>
    private static string Assign(PayloadRenderModel model, UnionRenderCase item, string member) =>
        item.Definition != null || model.TypeParameters.Length != 0 ? "(" + model.GlobalName + ")(object)" + member : member;

    /// <summary>Emits the slots of the cases one generic definition supplies.</summary>
    /// <remarks>An open case writes through its definition's generic entries directly. Closed cases select their tag through a
    /// dispatch slot resolved once per closed definition, then reinterpret the instance as the closed case, as the original
    /// pipeline does.</remarks>
    /// <summary>Emits the runtime slot that selects a generic definition's closed case, which both formats' slots read.</summary>
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
        for (int index = 0; index < group.Length; index++)
            w.Line("case var runtimeType when runtimeType == typeof(" + group[index].Type + "): return " +
                index.ToString(CultureInfo.InvariantCulture) + ";");
        w.Line("default:");
        w.Indented(() => w.Line("return -1;"));
        w.Close();
        w.Close();
        w.Blank();
    }

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
                for (int index = 0; index < group.Length; index++)
                {
                    w.Line("case " + index.ToString(CultureInfo.InvariantCulture) + ":");
                    w.Open();
                    w.Line("var genericValue = this;");
                    w.Line("ref var concreteValue = ref " + w.A("LuminPackMarshal") + ".As<" + open + ", " + group[index].Type + ">(ref genericValue);");
                    GenericCaseWrite(model, group[index], w, json, profile, "concreteValue");
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
    private static void GenericCaseWrite(PayloadRenderModel model, UnionRenderCase item, CodeWriter w, bool json, string profile, string value)
    {
        string id = item.Id.ToString(CultureInfo.InvariantCulture);
        if (!json)
        {
            w.Line("writer." + (model.UnionWide ? "WriteWideUnionHeader" : "WriteUnionHeader") + "(" + id + ");");
            w.Line(w.A("LuminPackSerializer") + ".SerializeUnion_" + item.Readable + "<" + Prefix(item) + profile + ">(ref writer, " + value + ");");
            return;
        }
        w.Line("writer.WriteObjectStart<" + profile + ">();");
        w.Line("if (default(" + profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8)");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU8);"));
        w.Line("else");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".TypeU16);"));
        w.Line("writer.Write<" + profile + ">(" + id + ");");
        w.Line("if (default(" + profile + ").StringEncoding == " + w.A("LuminPackStringEncoding") + ".UTF8)");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU8);"));
        w.Line("else");
        w.Indented(() => w.Line("writer.WritePropertyName<" + profile + ">(" + w.A("LuminPackConstUtf8") + ".ValueU16);"));
        w.Line("global::LuminPack.Binary_" + model.Extension + "_" + item.Readable + ".Write<" + Prefix(item) + profile + ">(ref writer, " + value + ");");
        w.Line("writer.WriteObjectEnd<" + profile + ">();");
    }

    /// <summary>Spells a generic definition with its own parameter names, as the case's partial declaration sees itself.</summary>
    private static string OpenSpelling(string definition) => definition;

    /// <summary>Whether a case is a struct implementing an interface root.</summary>
    private static bool StructCase(UnionRenderCase item) => item.Chain.Length != 0 &&
        item.Chain[item.Chain.Length - 1].IndexOf(" struct ", StringComparison.Ordinal) >= 0;

    /// <summary>Spells a case's payload owner, which publishes its binary and JSON operations.</summary>
    private static string CaseOwner(PayloadRenderModel model, UnionRenderCase item) =>
        "global::LuminPack.Binary_" + model.Extension + "_" + item.Readable;

    /// <summary>Lists one format's profile slots.</summary>
    private static UnionRenderSlot[] Slots(PayloadRenderModel model, bool json) => json ? new[]
    {
        new UnionRenderSlot(string.Empty, Profile, string.Empty, 0, 0),
    } : new[]
    {
        new UnionRenderSlot(string.Empty, Profile, string.Empty, 0, 0),
        new UnionRenderSlot("_Default", model.UnionStandardBinary, string.Empty, 0, 0),
    };

    /// <summary>Spells the arbitrary-profile slot's generic parameter list.</summary>
    private static string SlotArguments(UnionRenderSlot slot) => slot.Profile == Profile ? "<" + Profile + ">" : string.Empty;

    /// <summary>Opens a namespace and the root's partial type chain.</summary>
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
    private static void CloseChain(int depth, string @namespace, CodeWriter w)
    {
        for (int index = 0; index < depth; index++)
            w.Close();
        if (@namespace != null)
            w.Close();
    }

    /// <summary>The hidden-editor attribute line.</summary>
    private static string Hidden(CodeWriter w) => "[" + w.Attribute("EditorBrowsable") + "(" + w.A("EditorBrowsableState") + ".Never)]";
}
