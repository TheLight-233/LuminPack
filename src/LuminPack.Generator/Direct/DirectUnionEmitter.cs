using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using LuminPack.Code.Core;

namespace LuminPack.SourceGenerator.Direct;

/// <summary>Writes a covered Union root's published binary and JSON files: the root operations, the payload extensions, the
/// virtual dispatch slots, the runtime registration and every case's slot overrides.</summary>
/// <remarks>Union roots publish the same slot contract in both formats. The lines are the original emitter's own lines, with
/// helper names spelled through the consumer's aliases; the publication layout wraps long lines and compacts the short case
/// rows of the marked switch statements.</remarks>
internal static class DirectUnionEmitter
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
    internal static void EmitBinary(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.BinaryWrite) != 0, reads = (operations & GeneratedOperations.BinaryRead) != 0;
        string writer = w.A("LuminPackWriter"), reader = w.A("LuminPackReader");
        string type = model.GlobalName, container = "LuminRoots_" + model.Readable;
        string serializer = w.A("LuminPackSerializer"), standard = w.A(model.UnionStandardBinary);
        string inline = w.MethodImpl("AggressiveInlining"), constraint = "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
        string slotPrefix = "LuminPackUnionSerialize_" + model.UnionSuffix;
        foreach (var slot in model.UnionBinarySlots)
            w.A(slot.Profile);

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class " + serializer);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_" + model.Readable + "(scoped ref " + writer + " writer, scoped in " + type + "? input) => " +
            container + ".RootWrite<" + standard + ">(ref writer, input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Serialize_" + model.Readable + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootWrite<" + Profile + ">(ref writer, input);"));
        w.Suppressed = false;

        w.Blank();
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void RootWrite<" + Profile + ">(scoped ref " + writer + " writer, " + type + "? input)");
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
        w.Line("internal static void RootRead" + (model.UnionReadGeneric ? "<" + Profile + ">" : string.Empty) + "(scoped ref " + reader +
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
        w.Line("if (value is not null)");
        w.Open();
        w.Line("value = ReadExistingUnion(ref reader, value, tag);");
        w.Line("return;");
        w.Close();
        w.Blank();
        var counters = new SwitchCounters();
        w.Line("// Union cases");
        w.Line("switch (tag)");
        w.Open();
        ReadArms(model, w, false, "reader", counters, slotPrefix);
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static " + type + "? ReadExistingUnion(ref " + reader + " reader2, " + type + "? value2, ushort tag2)");
        w.Open();
        var reuse = new SwitchCounters { Members = counters.Members, Instances = counters.Instances, Cursors = counters.Cursors };
        w.Line("// Union cases");
        w.Line("switch (tag2)");
        w.Open();
        ReadArms(model, w, true, "reader2", reuse, slotPrefix);
        w.Close();
        w.Close();
        w.Close();
        w.Close();
        w.Suppressed = false;
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_" + model.Readable + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
            container + ".RootRead" + (model.UnionReadGeneric ? "<" + standard + ">" : string.Empty) + "(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void Deserialize_" + model.Readable + "<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootRead" + (model.UnionReadGeneric ? "<" + Profile + ">" : string.Empty) +
            "(ref reader, ref value);"));
        w.Close();
        w.Blank();

        // Payload extensions.
        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + "? value) => " + serializer + "." + container +
            ".RootWrite<" + standard + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWrite<TOptions>(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read(this ref " + reader + " state, ref " + type + "? value)");
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (model.UnionReadGeneric ? "<" + standard + ">" : string.Empty) + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line(reader + " isolated = default;");
        w.Line("state.CopyTo(ref isolated);");
        w.Line(serializer + "." + container + ".RootRead" + (model.UnionReadGeneric ? "<TOptions>" : string.Empty) + "(ref isolated, ref value);");
        w.Line("isolated.CopyTo(ref state);");
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Close();

        // Root dispatch and registration.
        w.Blank();
        OpenType(model.UnionNamespace, model.UnionChain, w);
        if (model.UnionCursor)
            CursorRoot(model, w, writer);
        Registration(model, w, type);
        RootSlots(model, w, writer, false);
        CompiledSlot(model, w, writer, false);
        DispatchHelpers(model, w, writer, false);
        RegisteredWriter(model, w, writer, false);
        if (model.UnionInterface)
        {
            // The publication partition recurses into partial classes but not into interfaces, so a root's JSON slots stay
            // in the binary file while its cases' JSON slots move to the JSON file.
            RootSlots(model, w, writer, true);
            CompiledSlot(model, w, writer, true);
            DispatchHelpers(model, w, writer, true);
            RegisteredWriter(model, w, writer, true);
        }
        CloseChain(model.UnionChain.Length, model.UnionNamespace, w);

        foreach (var item in model.UnionCases)
        {
            w.Blank();
            OpenType(item.Namespace, item.Chain, w);
            CaseSlots(model, item, w, false);
            CloseChain(item.Chain.Length, item.Namespace, w);
        }
    }

    /// <summary>Emits the JSON file.</summary>
    /// <param name="model">The covered Union root.</param>
    /// <param name="w">The file writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitJson(DirectTypeModel model, DirectWriter w, GeneratedOperations operations)
    {
        bool writes = (operations & GeneratedOperations.JsonWrite) != 0, reads = (operations & GeneratedOperations.JsonRead) != 0;
        string writer = w.A("LuminPackJsonWriter"), reader = w.A("LuminPackJsonReader");
        string type = model.GlobalName, container = "LuminRoots_" + model.Readable;
        string serializer = w.A("LuminPackSerializer"), standard = w.A(model.UnionStandardJson);
        string inline = w.MethodImpl("AggressiveInlining"), constraint = "where TLuminProfile : struct, " + w.A("ILuminPackOptions");
        string slotPrefix = "LuminPackUnionSerializeJson_" + model.UnionSuffix;
        foreach (var slot in model.UnionJsonSlots)
            w.A(slot.Profile);

        w.Line("namespace LuminPack");
        w.Open();
        w.Line("public static partial class " + serializer);
        w.Open();
        w.Suppressed = !writes && !reads;
        w.Line("internal static partial class " + container);
        w.Open();
        w.Suppressed = !writes;
        w.Line(w.Preserve);
        w.Line("internal static void RootWriteJson<" + Profile + ">(scoped ref " + writer + " writer, " + type + "? input)");
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
        w.Line("internal static void RootReadJson<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint));
        w.Open();
        JsonReadBody(model, w, reader, type);
        w.Close();
        w.Close();
        w.Suppressed = false;
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_" + model.Readable + "(scoped ref " + writer + " writer, scoped in " + type +
            "? input) => " + container + ".RootWriteJson<" + standard + ">(ref writer, input);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void SerializeJson_" + model.Readable + "<" + Profile + ">(scoped ref " + writer + " writer, scoped in " + type + "? input)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootWriteJson<" + Profile + ">(ref writer, input);"));
        w.Blank();
        w.Suppressed = !reads;
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_" + model.Readable + "(scoped ref " + reader + " reader, scoped ref " + type + "? value) => " +
            container + ".RootReadJson<" + standard + ">(ref reader, ref value);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line(inline);
        w.Line("internal static void DeserializeJson_" + model.Readable + "<" + Profile + ">(scoped ref " + reader + " reader, scoped ref " + type + "? value)");
        w.Indented(() => w.Line(constraint + " => " + container + ".RootReadJson<" + Profile + ">(ref reader, ref value);"));
        w.Close();
        w.Blank();

        w.Line("internal static partial class " + model.Owner);
        w.Open();
        w.Suppressed = !writes;
        w.Line(inline);
        w.Line("internal static void Write(this ref " + writer + " state, in " + type + "? value) => " + serializer + "." + container +
            ".RootWriteJson<" + standard + ">(ref state, value);");
        w.Line(inline);
        w.Line("internal static void Write<TOptions>(this ref " + writer + " state, in " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions") + " => " + serializer + "." + container + ".RootWriteJson<TOptions>(ref state, value);"));
        w.Suppressed = !reads;
        w.Line(inline);
        w.Line("internal static void Read(this ref " + reader + " state, ref " + type + "? value)");
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<" + standard + ">())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson<" + standard + ">(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Blank();
        w.Line(inline);
        w.Line("internal static void Read<TOptions>(this ref " + reader + " state, ref " + type + "? value)");
        w.Indented(() => w.Line("where TOptions : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        w.Line("if (state.CurrentTokenType == " + reader + ".JsonTokenType.None && !state.Read<TOptions>())");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");"));
        w.Line(serializer + "." + container + ".RootReadJson<TOptions>(ref state, ref value);");
        w.Line("state.CompleteValue();");
        w.Close();
        w.Suppressed = false;
        w.Close();
        w.Close();

        w.Blank();
        if (!model.UnionInterface)
        {
            OpenType(model.UnionNamespace, model.UnionChain, w);
            RootSlots(model, w, writer, true);
            CompiledSlot(model, w, writer, true);
            DispatchHelpers(model, w, writer, true);
            RegisteredWriter(model, w, writer, true);
            CloseChain(model.UnionChain.Length, model.UnionNamespace, w);
        }
        foreach (var item in model.UnionCases)
        {
            w.Blank();
            OpenType(item.Namespace, item.Chain, w);
            CaseSlots(model, item, w, true);
            CloseChain(item.Chain.Length, item.Namespace, w);
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
    private static void ReadArms(DirectTypeModel model, DirectWriter w, bool reuse, string reader, SwitchCounters counters, string slotPrefix)
    {
        string target = reuse ? "value2" : "value";
        foreach (var item in model.UnionCases)
        {
            int member = counters.Members++;
            w.Line("case " + item.Id.ToString(CultureInfo.InvariantCulture) + ":");
            w.Open();
            if (item.OwnEntry)
            {
                w.Line(item.Type + " member" + Number(member) + " = " + (reuse
                    ? w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(" + target + ")!;"
                    : "default !;"));
                w.Line(w.A("LuminPackSerializer") + ".DeserializeUnion" + (model.UniqueEntries ? "_" + item.Readable : string.Empty) +
                    (item.ReadGeneric ? "<" + Profile + ">" : string.Empty) + "(ref " + reader + ", ref member" + Number(member) + ");");
                w.Line(target + " = member" + Number(member) + ";");
                w.Line("return" + (reuse ? " " + target : string.Empty) + ";");
                w.Close();
                w.Blank();
                continue;
            }
            w.Line(item.Type + " member" + Number(member) + " = " + (reuse
                ? w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(" + target + ")!;"
                : "default !;"));
            var body = DirectBinaryEmitter.UnionReadBody(item.Model, w);
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
            w.Line("return" + (reuse ? " " + target : string.Empty) + ";");
            w.Close();
            w.Blank();
        }
        w.Line("default:");
        w.Open();
        w.Line("if (" + model.GlobalName + ".TryGetRegisteredFormatter(" + (reuse ? "tag2" : "tag") + ", out var registeredFormatter" +
            (reuse ? "2" : string.Empty) + ") && registeredFormatter" + (reuse ? "2" : string.Empty) + ".ReadValue != null)");
        w.Open();
        w.Line(model.GlobalName + " registeredValue" + (reuse ? "2" : string.Empty) + " = default !;");
        w.Line("((" + "global::LuminPack.Code.LuminPackReadDelegate<" + model.GlobalName + ">)registeredFormatter" + (reuse ? "2" : string.Empty) +
            ".ReadValue)(ref " + reader + ", ref registeredValue" + (reuse ? "2" : string.Empty) + ");");
        w.Line(target + " = registeredValue" + (reuse ? "2" : string.Empty) + ";");
        w.Line("return" + (reuse ? " " + target : string.Empty) + ";");
        w.Close();
        w.Blank();
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(" + (reuse ? "tag2" : "tag") + ", typeof(" + model.GlobalName + "));");
        w.Line("return" + (reuse ? " " + target : string.Empty) + ";");
        w.Close();
    }

    /// <summary>Renames the locals of one expanded case body to the running switch names.</summary>
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
            if (reuse)
                shifted = ReaderWord.Replace(shifted, "reader2");
            result.Add(shifted);
        }
        cursorCount = cursors + localCursors;
        return result;
    }

    /// <summary>Spells a local's occurrence number, which the first occurrence leaves off.</summary>
    private static string Number(int index) => index == 0 ? string.Empty : (index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether the read root stays inside the runtime's sixteen-call inlining budget.</summary>
    private static bool ReadKeepsInline(DirectTypeModel model, DirectWriter w)
    {
        // The two switches share the peek and the existing-instance call, and each repeats the three registered-member calls.
        int calls = 8;
        foreach (var item in model.UnionCases)
            calls += item.OwnEntry ? 2 : 2 * DirectCalls.Count(DirectBinaryEmitter.UnionReadBody(item.Model, w)) + 1;
        return calls <= 16;
    }

    /// <summary>Emits the JSON read body of the root.</summary>
    private static void JsonReadBody(DirectTypeModel model, DirectWriter w, string reader, string type)
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
            w.Line(item.Type + " " + member + " = " + w.A("LuminPackMarshal") + ".AsExact<" + item.Type + ">(value)!;");
            w.Line("global::LuminPack.Binary_" + model.Extension + "_" + item.Readable + ".Read<" + Profile + ">(ref reader, ref " + member + ");");
            w.Line("value = " + member + ";");
            w.Line("break;");
            w.Close();
            w.Blank();
        }
        w.Line("default:");
        w.Open();
        w.Line("if (" + model.GlobalName + ".TryGetRegisteredFormatter(tag, out var registeredFormatter) && registeredFormatter.ReadValueJson != null)");
        w.Open();
        w.Line(model.GlobalName + " registeredValue = default !;");
        w.Line("((global::LuminPack.Code.LuminPackJsonReadDelegate<" + model.GlobalName + ">)registeredFormatter.ReadValueJson)(ref reader, ref registeredValue);");
        w.Line("value = registeredValue;");
        w.Line("break;");
        w.Close();
        w.Blank();
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(tag, typeof(" + model.GlobalName + "));");
        w.Line("break;");
        w.Close();
        w.Close();
        w.Close();
    }

    /// <summary>Emits the union property index comparison of a JSON property name local.</summary>
    private static void PropertyMatch(DirectWriter w, string name)
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
    private static void CursorRoot(DirectTypeModel model, DirectWriter w, string writer)
    {
        string suffix = model.UnionSuffix;
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line(Hidden(w));
        w.Line("public virtual int LuminPackUnionSerializeCursor_" + suffix + "(ref " + writer + " writer, int initialIndex)");
        w.Open();
        w.Line("return LuminPackUnionCursorFallback_" + suffix + "(this, ref writer, initialIndex);");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line(Hidden(w));
        w.Line("protected static int LuminPackUnionCursorFallback_" + suffix + "(" + model.GlobalName + " value, ref " + writer + " writer, int initialIndex)");
        w.Open();
        w.Line("writer.CurrentIndex = initialIndex;");
        w.Line("value.LuminPackUnionSerialize_" + suffix + "(ref writer);");
        w.Line("return writer.Position;");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits the runtime registration members of the binary root.</summary>
    private static void Registration(DirectTypeModel model, DirectWriter w, string type)
    {
        string suffix = model.UnionSuffix;
        string known = string.Join(" || ", model.UnionCases.Select(item => "tag == " + item.Id.ToString(CultureInfo.InvariantCulture)).Distinct());
        w.Line("private static readonly " + w.A("LuminPackFormatterTable") + " LuminPackUnionRegistry_" + suffix + " = new " + w.A("LuminPackFormatterTable") + "();");
        w.Blank();
        w.Line(w.Preserve);
        w.Line("public static void Register<TMember>(int tag,");
        w.Line("    global::LuminPack.Code.LuminPackUnionWriteDelegate<" + type + "> writeValue,");
        w.Line("    global::LuminPack.Code.LuminPackReadDelegate<" + type + "> readValue,");
        w.Line("    global::LuminPack.Code.LuminPackUnionJsonWriteDelegate<" + type + "> writeValueJson,");
        w.Line("    global::LuminPack.Code.LuminPackJsonReadDelegate<" + type + "> readValueJson)");
        w.Open();
        w.Line("if ((uint)tag > ushort.MaxValue) " + w.A("LuminPackExceptionHelper") + ".ThrowArgumentException(\"A Union tag must fit in UInt16.\");");
        w.Line("if (" + known + ") " + w.A("LuminPackExceptionHelper") + ".ThrowArgumentException(\"This Union tag belongs to a generated case.\");");
        w.Line("if (!typeof(" + type + ").IsAssignableFrom(typeof(TMember))) " + w.A("LuminPackExceptionHelper") +
            ".ThrowArgumentException(\"The registered member must implement or derive from the Union root.\");");
        w.Line("if (writeValue == null || readValue == null || writeValueJson == null || readValueJson == null) " + w.A("LuminPackExceptionHelper") +
            ".ThrowArgumentException(\"All four Union operations are required.\");");
        w.Line("lock (LuminPackUnionRegistry_" + suffix + ")");
        w.Open();
        w.Line("if (TryGetRegisteredFormatter(tag, out _)) " + w.A("LuminPackExceptionHelper") + ".ThrowArgumentException(\"This Union tag is already registered.\");");
        w.Line("if (!LuminPackUnionRegistry_" + suffix + ".TryAdd(typeof(TMember), new " + w.A("LuminPackFormatter") +
            " { Tag = tag, WriteValue = writeValue, ReadValue = readValue, WriteValueJson = writeValueJson, ReadValueJson = readValueJson }))");
        w.Open();
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowArgumentException(\"A formatter for this union member is already registered.\");");
        w.Close();
        w.Close();
        w.Close();
        w.Blank();
        w.Line(w.Preserve);
        w.Line("public static void Register<TMember, TOptions>(");
        w.Line("    int tag,");
        w.Line("    global::LuminPack.Code.LuminPackUnionWriteDelegate<" + type + "> writeValue,");
        w.Line("    global::LuminPack.Code.LuminPackReadDelegate<" + type + "> readValue,");
        w.Line("    global::LuminPack.Code.LuminPackUnionJsonWriteDelegate<" + type + "> writeValueJson,");
        w.Line("    global::LuminPack.Code.LuminPackJsonReadDelegate<" + type + "> readValueJson)");
        w.Line("    where TOptions : struct, " + w.A("ILuminPackOptions") + " => Register<TMember>(tag, writeValue, readValue, writeValueJson, readValueJson);");
        w.Blank();
        w.Line(w.Preserve);
        w.Line("public static bool TryGetRegisteredFormatter(int tag, out " + w.A("LuminPackFormatter") + " entry)");
        w.Open();
        w.Line("lock (LuminPackUnionRegistry_" + suffix + ")");
        w.Open();
        w.Line("return LuminPackUnionRegistry_" + suffix + ".TryGetTag(tag, out entry);");
        w.Close();
        w.Close();
        w.Blank();
        w.Line("private static bool LuminPackTryGetRegistered_" + suffix + "(global::System.Type type, out " + w.A("LuminPackFormatter") + " entry)");
        w.Open();
        w.Line("lock (LuminPackUnionRegistry_" + suffix + ") return LuminPackUnionRegistry_" + suffix + ".TryGetValue((nint)" + w.A("LuminPackMarshal") +
            ".GetMethodTable(type), out entry);");
        w.Close();
        w.Blank();
    }

    /// <summary>Emits the root's virtual write slots in profile order.</summary>
    private static void RootSlots(DirectTypeModel model, DirectWriter w, string writer, bool json)
    {
        string operation = json ? "LuminPackUnionSerializeJson_" : "LuminPackUnionSerialize_";
        string helper = json ? "LuminPackUnionWriteRegisteredJson_" : "LuminPackUnionWriteRegistered_";
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        string modifier = model.UnionInterface ? string.Empty : "public virtual ";
        foreach (var slot in Slots(model, json))
        {
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line(modifier + "void " + operation + model.UnionSuffix + slot.Suffix + "(ref " + state + " writer)");
            w.Indented(() => w.Line("=> " + helper + model.UnionSuffix + "(this, ref writer);"));
        }
        w.Blank();
    }

    /// <summary>Emits the compiled profile selector and, for a pattern tier, its exact-type dispatch helpers.</summary>
    private static void CompiledSlot(DirectTypeModel model, DirectWriter w, string writer, bool json)
    {
        string operation = json ? "LuminPackUnionSerializeJson_" : "LuminPackUnionSerialize_";
        var slotList = json ? model.UnionJsonSlots : model.UnionBinarySlots;
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        w.Line(Hidden(w));
        w.Line(w.MethodImpl("AggressiveInlining"));
        w.Line("public static void " + operation + model.UnionSuffix + "_Compiled<" + Profile + ">(ref " + state + " writer, " + model.GlobalName + " value)");
        w.Indented(() => w.Line("where TLuminProfile : struct, " + w.A("ILuminPackOptions")));
        w.Open();
        if (!json && model.UnionCanonical)
            foreach (var slot in slotList)
            {
                var identities = new List<string>
                {
                    "typeof(" + w.A(slot.Profile) + ")",
                    "typeof(global::LuminPack.Option.LuminPackFixedOptions<" + w.A(slot.Profile) + ">)",
                };
                if (slot.Encoding == 1 && slot.Recording == 0)
                    foreach (string alias in new[] { "LuminPackDefaultOptions", "LuminPackDeclaredOptions" })
                    {
                        identities.Add("typeof(" + w.A(alias) + ")");
                        identities.Add("typeof(global::LuminPack.Option.LuminPackFixedOptions<" + w.A(alias) + ">)");
                    }
                w.Line("if (typeof(" + Profile + ") == " + string.Join(" || typeof(" + Profile + ") == ", identities) + ")");
                w.Open();
                w.Line(Dispatch(model, slot, writer, json));
                w.Line("return;");
                w.Close();
                w.Blank();
            }
        foreach (var slot in slotList)
        {
            string condition = "if (default(" + Profile + ").StringEncoding == (" + w.A("LuminPackStringEncoding") + ")" + slot.Encoding.ToString(CultureInfo.InvariantCulture);
            if (!json)
                condition += " && default(" + Profile + ").StringRecording == (" + w.A("LuminPackStringRecording") + ")" + slot.Recording.ToString(CultureInfo.InvariantCulture);
            w.Line(condition + ")");
            w.Open();
            w.Line(Dispatch(model, slot, writer, json));
            w.Line("return;");
            w.Close();
            w.Blank();
        }
        w.Line(w.A("LuminPackExceptionHelper") + ".ThrowInvalidOperationException(\"The compiled profile has an invalid string encoding or recording mode.\");");
        w.Close();
        w.Blank();
    }

    /// <summary>Spells the compiled selector's call to one profile slot.</summary>
    private static string Dispatch(DirectTypeModel model, DirectUnionSlot slot, string writer, bool json)
    {
        string name = (json ? "LuminPackUnionSerializeJson_" : "LuminPackUnionSerialize_") + model.UnionSuffix + slot.Suffix;
        if (model.UnionTier == 1)
            return name + "_Dispatch(ref writer, value);";
        return "value." + name + "(ref writer);";
    }

    /// <summary>Emits one exact-type dispatch helper per profile slot of a pattern tier.</summary>
    private static void DispatchHelpers(DirectTypeModel model, DirectWriter w, string writer, bool json)
    {
        if (model.UnionTier != 1)
            return;
        int rootDepth = (model.UnionNamespace == null ? 0 : model.UnionNamespace.Split('.').Length) + model.UnionChain.Length;
        bool normalizeAll = model.UnionCursor && (json || rootDepth >= 3);
        string operation = json ? "LuminPackUnionSerializeJson_" : "LuminPackUnionSerialize_";
        foreach (var slot in Slots(model, json))
        {
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line("private static void " + operation + model.UnionSuffix + slot.Suffix + "_Dispatch(ref " + writer + " writer, " + model.GlobalName + " value)");
            w.Open();
            w.Line("// Union cases");
            w.Line("switch (value)");
            w.Open();
            for (int index = 0; index < model.UnionTierCases.Length; index++)
            {
                string type = model.UnionTierCases[index];
                string variable = "caseValue" + (index == 0 ? string.Empty : index.ToString(CultureInfo.InvariantCulture));
                string name = type + " " + variable;
                string call = variable + "." + operation + model.UnionSuffix + slot.Suffix + "(ref writer);";
                string guard = model.UnionTierSealed[index] ? string.Empty : " when ((object)value).GetType() == typeof(" + type + ")";
                var item = model.UnionCases.FirstOrDefault(value => value.Type == type);
                bool normalize = normalizeAll || slot.Suffix.Length != 0 && item != null &&
                    (item.Namespace == null ? 0 : item.Namespace.Split('.').Length) + item.Chain.Length >= 3;
                if (!normalize)
                {
                    w.Line("case " + name + guard + ": " + call + " return;");
                    continue;
                }
                string oneLine = "case " + name + guard + ": " + call + " return;";
                // The presentation joins a marked case row that fits its 260-column budget.
                if (w.Indent * 4 + oneLine.Length <= 260)
                {
                    w.Line(oneLine);
                    continue;
                }
                w.Line("case " + name + guard + ":");
                w.Indented(() =>
                {
                    w.Line(call);
                    w.Line("return;");
                });
            }
            w.Line("default:");
            w.Indented(() =>
            {
                w.Line("value." + operation + model.UnionSuffix + slot.Suffix + "(ref writer);");
                w.Line("return;");
            });
            w.Close();
            w.Close();
            w.Blank();
        }
    }

    /// <summary>Emits the registered-member fallback of the matching format.</summary>
    private static void RegisteredWriter(DirectTypeModel model, DirectWriter w, string writer, bool json)
    {
        string delegateName = json ? "LuminPackUnionJsonWriteDelegate" : "LuminPackUnionWriteDelegate";
        string operation = json ? "WriteValueJson" : "WriteValue";
        string state = json ? w.A("LuminPackJsonWriter") : writer;
        w.Line("private static void " + (json ? "LuminPackUnionWriteRegisteredJson_" : "LuminPackUnionWriteRegistered_") + model.UnionSuffix +
            "(" + model.GlobalName + " value, ref " + state + " writer)");
        w.Open();
        w.Line("if (LuminPackTryGetRegistered_" + model.UnionSuffix + "(((object)value).GetType(), out var registration) && registration." + operation + " != null)");
        w.Indented(() => w.Line("((global::LuminPack.Code." + delegateName + "<" + model.GlobalName + ">)registration." + operation + ")(ref writer, ref value);"));
        w.Line("else");
        w.Indented(() => w.Line(w.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(((object)value).GetType(), typeof(" + model.GlobalName + "));"));
        w.Close();
    }

    /// <summary>Emits one case's slot overrides of one format.</summary>
    private static void CaseSlots(DirectTypeModel model, DirectUnionCase item, DirectWriter w, bool json)
    {
        string prefix = model.UnionInterface ? "void " + model.GlobalName + "." :
            item.Sealed ? "public sealed override void " : "public override void ";
        string operation = json ? "LuminPackUnionSerializeJson_" : "LuminPackUnionSerialize_";
        if (!json && item.FixedCursor)
            foreach (var slot in model.UnionBinarySlots)
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + "(ref " + w.A("LuminPackWriter") + " writer)");
                w.Indented(() => w.Line("=> writer.CurrentIndex = " + w.A("LuminPackSerializer") + ".SerializeUnionAt(ref writer, this, writer.CurrentIndex, (byte)" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");"));
            }
        else if (!json)
            foreach (var slot in model.UnionBinarySlots)
            {
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + "(ref " + w.A("LuminPackWriter") + " writer)");
                w.Open();
                w.Line("writer." + (model.UnionWide ? "WriteWideUnionHeader" : "WriteUnionHeader") + "(" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");");
                w.Line("var concreteValue = this;");
                w.Line(w.A("LuminPackSerializer") + ".SerializeUnion<" + w.A(slot.Profile) + ">(ref writer, concreteValue);");
                w.Close();
            }
        if (!json && model.UnionCursor)
        {
            w.Blank();
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line(Hidden(w));
            w.Line((model.UnionInterface ? "int " + model.GlobalName + "." : item.Sealed ? "public sealed override int " : "public override int ") +
                "LuminPackUnionSerializeCursor_" + model.UnionSuffix + "(ref LuminPackWriter writer, int initialIndex)");
            w.Open();
            if (item.FixedCursor)
            {
                w.Line("if (((object)this).GetType() != typeof(" + item.Type + "))");
                w.Indented(() => w.Line("return LuminPackUnionCursorFallback_" + model.UnionSuffix + "(this, ref writer, initialIndex);"));
                w.Line("return " + w.A("LuminPackSerializer") + ".SerializeUnionAt(ref writer, this, initialIndex, (byte)" +
                    item.Id.ToString(CultureInfo.InvariantCulture) + ");");
            }
            else
            {
                w.Line("writer.CurrentIndex = initialIndex;");
                w.Line("LuminPackUnionSerialize_" + model.UnionSuffix + "(ref writer);");
                w.Line("return writer.Position;");
            }
            w.Close();
        }
        if (json)
            foreach (var slot in model.UnionJsonSlots)
            {
                string profile = w.A(slot.Profile);
                w.Line(w.MethodImpl("AggressiveInlining"));
                w.Line(Hidden(w));
                w.Line(prefix + operation + model.UnionSuffix + slot.Suffix + "(ref " + w.A("LuminPackJsonWriter") + " writer)");
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
                w.Raw(new string(' ', Math.Max(0, w.Indent - 1) * 4) + "writer.Write<" + profile + ">(concreteValue);");
                w.Line("writer.WriteObjectEnd<" + profile + ">();");
                w.Close();
            }
    }

    /// <summary>Lists one format's profile slots.</summary>
    private static DirectUnionSlot[] Slots(DirectTypeModel model, bool json) => json ? model.UnionJsonSlots : model.UnionBinarySlots;

    /// <summary>Opens a namespace and the root's partial type chain.</summary>
    private static void OpenType(string @namespace, string[] chain, DirectWriter w)
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
    private static void CloseChain(int depth, string @namespace, DirectWriter w)
    {
        for (int index = 0; index < depth; index++)
            w.Close();
        if (@namespace != null)
            w.Close();
    }

    /// <summary>The hidden-editor attribute line.</summary>
    private static string Hidden(DirectWriter w) => "[" + w.Attribute("EditorBrowsable") + "(" + w.A("EditorBrowsableState") + ".Never)]";
}
