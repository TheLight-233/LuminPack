#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Rendering.Emitters;

internal static partial class BinaryEmitter
{
    private const string VersionCore = "global::LuminPack.Core.";

    /// <summary>Emits the scoped descriptor header and one operation per member.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="withNull">Whether this body owns the null marker.</param>
    /// <returns>The generated write statements.</returns>
    internal static List<string> VersionWriteBody(PayloadRenderModel model, CodeWriter w, bool withNull = true)
    {
        var body = new List<string> { "var value = input;" };
        if (!model.IsStruct && withNull)
        {
            body.Add("if (value is null)");
            body.Add("{");
            body.Add("writer.EnsureAdditionalCapacity(1);");
            body.Add("writer.WriteNullObjectHeader();");
            body.Add("return;");
            body.Add("}");
            body.Add("");
        }
        var schema = model.Version;
        string profile = VersionProfile(model, w);
        body.Add("writer.WriteVersionRecord<" + profile + ">(" + schema.Schema + ".Schema, " +
            schema.Schema + ".Descriptors, " + schema.Schema + ".Cache);");
        bool framed = schema.Descriptors.Any(word => word >> 56 == 255);
        if (framed)
        {
            body.Add("int length, scope = -1;");
            body.Add("try");
            body.Add("{");
        }
        for (int index = 0; index < model.Members.Length; index++)
        {
            RenderMember member = model.Members[index];
            byte width = schema.Width(index);
            if (width <= 250 && member.IsFixed)
            {
                int end = index + 1;
                int reservation = width;
                for (; end < model.Members.Length && schema.Width(end) <= 250 && model.Members[end].IsFixed; end++)
                    reservation += schema.Width(end);
                body.Add("writer.EnsureAdditionalCapacity(" + VersionNumber(reservation) + ");");
                for (; index < end; index++)
                {
                    member = model.Members[index];
                    body.Add("Write" + VersionMemberName(member) + "(ref writer, value." + member.Name + ");");
                }
                index--;
                continue;
            }
            if (width == 255)
            {
                body.Add("length = writer.ReserveVersionLength();");
                body.Add("scope = writer.EnterVersionScope();");
            }
            body.Add("Write" + VersionMemberName(member) + "(ref writer, value." + member.Name + ");");
            if (width == 255)
            {
                body.Add("writer.ExitVersionScope(scope);");
                body.Add("scope = -1;");
                body.Add("writer.CompleteVersionLength(length);");
            }
        }
        if (framed)
        {
            body.Add("}");
            body.Add("finally");
            body.Add("{");
            body.Add("if (scope >= 0)");
            body.Add("\twriter.ExitVersionScope(scope);");
            body.Add("}");
        }
        if (withNull)
            ApplyBoundedWrite(model, body);

        body.Add("");
        string writerType = w.A("LuminPackWriter");
        bool outlineStrings = OutlineVersionStrings(model);
        if (outlineStrings)
        {
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void WriteStringField(scoped ref " + writerType + " writer, string? field) => writer.WriteVersionString<" +
                profile + ">(field);");
            body.Add("");
        }
        for (int index = 0; index < model.Members.Length; index++)
        {
            RenderMember member = model.Members[index];
            byte width = schema.Width(index);
            string signature = "static void Write" + VersionMemberName(member) + "(scoped ref " + writerType + " writer, " +
                schema.TempTypes[index] + " field)";
            if (width == 254)
            {
                string write = outlineStrings ? "WriteStringField(ref writer, field);" : "writer.WriteVersionString<" + profile + ">(field);";
                body.Add(signature + " => " + write);
                continue;
            }
            if (width <= 250 && member.IsFixed)
            {
                body.Add(signature + " => writer.Write(in field);");
                continue;
            }
            if (index != 0)
                body.Add("");
            if (width == 255)
                body.Add(w.MethodImpl("NoInlining"));
            body.Add(signature);
            body.Add("{");
            AddMemberWrite(body, model, member, w, "field");
            if (schema.FixedNullable[index])
            {
                body.Add("if (!field.HasValue)");
                body.Add("	writer.PadVersionField(" + VersionNumber(width - 1) + ");");
            }
            body.Add("}");
            body.Add("");
        }
        return body;
    }

    /// <summary>Keeps repeated string codec expansion inside one closed method for larger records.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <returns>Whether at least five members use unframed string records.</returns>
    private static bool OutlineVersionStrings(PayloadRenderModel model) =>
        model.Version.Descriptors.Count(word => word >> 56 == 254) >= 5;

    /// <summary>Selects the inline hint for a small fixed version-tolerant record.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <returns>Whether to request root inlining.</returns>
    internal static bool VersionWriteInline(PayloadRenderModel model) =>
        model.Members.Length <= 4 && model.Version.Descriptors.All(word => word >> 56 <= 250);

    /// <summary>Stages versioned members before applying constructor, pooling or reuse semantics.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="withNull">Whether this body accepts the null marker.</param>
    /// <returns>The generated read statements.</returns>
    internal static List<string> VersionReadBody(PayloadRenderModel model, CodeWriter w, bool withNull = true)
    {
        var members = model.Members;
        var schema = model.Version;
        string profile = VersionProfile(model, w);
        string qualifiedSchema = schema.Schema;
        string fieldsType = qualifiedSchema + ".Fields";
        var temps = Enumerable.Range(0, members.Length).Select(index => "fields.F" + VersionNumber(index)).ToArray();
        var body = new List<string>
        {
            "var record = reader.ReadVersionRecord<" + profile + ">(" + qualifiedSchema + ".Schema, " +
                qualifiedSchema + ".Descriptors, " + qualifiedSchema + ".Cache);",
            "if (record.IsNull)",
            "{",
        };
        if (withNull)
        {
            body.Add("value = default;");
            body.Add("return;");
        }
        else
            body.Add(w.A("LuminPackExceptionHelper") + ".ThrowInvalidDataException(\"A tracked record body cannot be null.\");");
        body.Add("}");
        body.Add("");
        body.Add(fieldsType + " fields = default;");
        if (!model.IsStruct && members.Any(member => !member.IsFixed))
        {
            body.Add("if (value is not null)");
            body.Add("{");
            if (members.Any(member => !member.IsFixed && member.Mirrored))
                body.Add("ref var seed = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            for (int index = 0; index < members.Length; index++)
                if (!members[index].IsFixed)
                    body.Add(temps[index] + " = " + (members[index].Mirrored ? "seed." : "value!.") + members[index].Name + ";");
            body.Add("}");
        }

        body.Add("if (record.Map is { } map)");
        body.Add("	ReadMapped(ref reader, ref fields, map, legacy: false);");
        body.Add("else if (record.IsLegacy)");
        body.Add("	ReadMapped(ref reader, ref fields, reader.ReadLegacyVersionRecord<" + profile + ">(" +
            qualifiedSchema + ".Legacy.Fields), legacy: true);");
        if (members.Length != 0)
        {
            body.Add("else");
            body.Add("{");
            for (int index = 0; index < members.Length; index++)
            {
                if (schema.Width(index) == 255)
                    body.Add("ReadFramed(ref reader, ref fields, " + VersionNumber(index) + ");");
                else
                    body.Add("Read" + VersionMemberName(members[index]) + "(ref reader, ref fields" +
                        (schema.FixedNullable[index] ? ", legacy: false);" : ");"));
            }
            body.Add("}");
        }
        body.Add("");
        AddVersionPublication(model, body, temps, registered: !withNull);
        AppendMappedRead(model, w, body, fieldsType, profile, temps);
        if (members.Length != 0)
            body.Add("");
        bool outlineStrings = OutlineVersionStrings(model);
        if (outlineStrings)
        {
            body.Add(w.MethodImpl("NoInlining"));
            body.Add("static void ReadStringField<TStringProfile>(scoped ref " + w.A("LuminPackReader") +
                " reader, scoped ref string? field) where TStringProfile : struct, " + w.A("ILuminPackOptions") +
                " => reader.Read<TStringProfile>(ref field);");
            body.Add("");
        }
        for (int index = 0; index < members.Length; index++)
            AppendVersionFieldRead(model, w, body, fieldsType, index, temps[index], outlineStrings);
        return body;
    }

    /// <summary>Emits the common mapped loop for descriptor and legacy records.</summary>
    /// <param name="model">The reading payload.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="body">The receiving root statements.</param>
    /// <param name="fieldsType">The fully qualified staged state type.</param>
    /// <param name="profile">The record's compiled profile.</param>
    /// <param name="temps">The staged member expressions.</param>
    private static void AppendMappedRead(PayloadRenderModel model, CodeWriter w, List<string> body, string fieldsType,
        string profile, string[] temps)
    {
        var schema = model.Version;
        string stringLimit = model.Limits?.String is int limit && limit != int.MaxValue ? ", " + VersionNumber(limit) : string.Empty;
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void ReadMapped(scoped ref " + w.A("LuminPackReader") + " reader, scoped ref " + fieldsType +
            " fields, " + VersionCore + "LuminPackDescriptorMap map, bool legacy)");
        body.Add("{");
        body.Add("if (legacy)");
        body.Add("\tReadLegacyFields(ref reader, ref fields, map);");
        body.Add("else");
        body.Add("{");
        body.Add("for (int index = 0; index < map.Count; index++)");
        body.Add("{");
        body.Add("int slot = map.GetSlot(index);");
        body.Add("byte width = map.GetWidth(index);");
        body.Add("if (slot < 0)");
        body.Add("\treader.SkipVersionField<" + profile + ">(width" + stringLimit + ");");
        bool framed = schema.Descriptors.Any(word => word >> 56 == 255);
        if (framed)
        {
            body.Add("else if (width == 255)");
            body.Add("\tReadFramed(ref reader, ref fields, slot);");
        }
        body.Add("else");
        body.Add("\tReadSlot(ref reader, ref fields, slot, legacy: false);");
        body.Add("}");
        body.Add("}");
        for (int index = 0; index < model.Members.Length; index++)
        {
            if (model.Members[index].IsFixed)
                continue;
            body.Add("if (!map.IsPresent(" + VersionNumber(index) + "))");
            body.Add("	" + temps[index] + " = default!;");
        }
        body.Add("}");
        string signature = "(scoped ref " + w.A("LuminPackReader") + " reader, scoped ref " + fieldsType + " fields";
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void ReadLegacyFields" + signature + ", " + VersionCore + "LuminPackDescriptorMap map)");
        body.Add("{");
        body.Add("for (int index = 0; index < map.Count; index++)");
        body.Add("{");
        body.Add("int slot = map.GetSlot(index), length = map.GetLegacyLength(index);");
        body.Add("if (slot < 0)");
        body.Add("\treader.Advance(length);");
        body.Add("else");
        body.Add("\tReadScopedSlot(ref reader, ref fields, slot, reader.EnterLegacyVersionField(length), legacy: true);");
        body.Add("}");
        body.Add("}");
        if (framed)
        {
            body.Add("");
            body.Add("static void ReadFramed" + signature + ", int slot) =>");
            body.Add("\tReadScopedSlot(ref reader, ref fields, slot, reader.EnterVersionField(), legacy: false);");
        }
        body.Add("");
        body.Add(w.MethodImpl("NoInlining"));
        body.Add("static void ReadScopedSlot" + signature + ", int slot, " + VersionCore + "LuminPackVersionFieldScope scope, bool legacy)");
        body.Add("{");
        body.Add("try");
        body.Add("{");
        body.Add("ReadSlot(ref reader, ref fields, slot, legacy);");
        body.Add("reader.ValidateVersionFieldEnd(scope);");
        body.Add("}");
        body.Add("finally");
        body.Add("{");
        body.Add("reader.ExitVersionField(scope);");
        body.Add("}");
        body.Add("}");
        body.Add("");
        body.Add("static void ReadSlot" + signature + ", int slot, bool legacy)");
        body.Add("{");
        if (model.Members.Length != 0)
        {
            body.Add("switch (slot)");
            body.Add("{");
            for (int index = 0; index < model.Members.Length; index++)
            {
                body.Add("case " + VersionNumber(index) + ": Read" + VersionMemberName(model.Members[index]) +
                    "(ref reader, ref fields" + (schema.FixedNullable[index] ? ", legacy); return;" : "); return;"));
            }
            body.Add("}");
        }
        body.Add("}");
    }

    /// <summary>Emits the staged member operation shared by positional and mapped reads.</summary>
    /// <param name="model">The reading payload.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="body">The receiving root statements.</param>
    /// <param name="fieldsType">The fully qualified staged state type.</param>
    /// <param name="index">The member's slot.</param>
    /// <param name="target">The staged field expression.</param>
    /// <param name="outlineStrings">Whether direct string reads share an outlined codec call.</param>
    private static void AppendVersionFieldRead(PayloadRenderModel model, CodeWriter w, List<string> body, string fieldsType,
        int index, string target, bool outlineStrings)
    {
        var member = model.Members[index];
        var schema = model.Version;
        byte width = schema.Width(index);
        bool padded = schema.FixedNullable[index];
        string signature = "static void Read" + VersionMemberName(member) + "(scoped ref " + w.A("LuminPackReader") +
            " reader, scoped ref " + fieldsType + " fields" + (padded ? ", bool legacy" : "") + ")";
        if (outlineStrings && width == 254)
        {
            body.Add(signature + " => ReadStringField<" + ReadProfile(model, w, member) + ">(ref reader, ref " + target + ");");
            return;
        }
        var read = new List<string>();
        AddMemberRead(read, model, member, w, "reader", target);
        if (!padded && read.Count == 1)
        {
            body.Add(signature + " => " + read[0]);
            return;
        }
        body.Add("");
        body.Add(signature);
        body.Add("{");
        body.AddRange(read);
        if (padded)
        {
            body.Add("if (!legacy && !" + target + ".HasValue)");
            body.Add("	reader.Advance(" + VersionNumber(width - 1) + ");");
        }
        body.Add("}");
    }

    /// <summary>Publishes the staged fields into the selected constructor, rented instance or reused instance.</summary>
    /// <param name="model">The reading payload.</param>
    /// <param name="body">The receiving statements.</param>
    /// <param name="temps">The staged member expressions.</param>
    /// <param name="registered">Whether the instance was already registered for reference tracking.</param>
    private static void AddVersionPublication(PayloadRenderModel model, List<string> body, string[] temps, bool registered)
    {
        var members = model.Members;
        bool mirroredMembers = model.Members.Any(member => member.Mirrored);
        var initializer = members.Where(member => !member.Mirrored &&
            (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(member.Name))).ToArray();
        if (model.IsStruct)
        {
            if (model.Rent != null)
            {
                body.Add("value = " + (model.RentByRef ? "ref " : string.Empty) + model.Rent + ";");
                for (int index = 0; index < members.Length; index++)
                    if (!members[index].Mirrored)
                        body.Add("value." + members[index].Name + " = " + temps[index] + "!;");
            }
            else
                AddVersionConstruction(model, body, initializer, temps);
            if (mirroredMembers)
            {
                body.Add("ref var local = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref value);");
                for (int index = 0; index < members.Length; index++)
                    if (members[index].Mirrored && (model.Rent != null || model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name)))
                        body.Add("local." + members[index].Name + " = " + temps[index] + "!;");
            }
            return;
        }
        body.Add("var completedInstance = value;");
        if (registered)
        {
            PublishExisting();
            return;
        }
        body.Add("if (value is null)");
        body.Add("{");
        if (model.Rent != null)
        {
            // A rented instance is populated field by field, mirrored members through the layout mirror.
            body.Add("value = " + model.Rent + ";");
            body.Add("// Populate every field on the pooled instance.");
            if (mirroredMembers)
                body.Add("ref var local = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
            for (int index = 0; index < members.Length; index++)
                body.Add((members[index].Mirrored ? "local." : "value!.") + members[index].Name + " = " + temps[index] + "!;");
        }
        else
        {
            AddVersionConstruction(model, body, initializer, temps);
            if (mirroredMembers)
            {
                body.Add("// Assign private fields.");
                body.Add("ref var local = ref " + "LuminPackMarshal" + ".As<" + model.GlobalName + ", " + model.Mirror + ">(ref value!);");
                for (int index = 0; index < members.Length; index++)
                    if (members[index].Mirrored && (model.ConstructorMembers == null || !model.ConstructorMembers.Contains(members[index].Name)))
                        body.Add("local." + members[index].Name + " = " + temps[index] + "!;");
            }
        }
        body.Add("completedInstance = value;");
        body.Add("}");
        body.Add("else");
        body.Add("{");
        PublishExisting();
        body.Add("}");

        void PublishExisting()
        {
            if (mirroredMembers)
                body.Add("ref var reuseLocal = ref LuminPackMarshal.As<" + model.GlobalName + ", " + model.Mirror + ">(ref completedInstance!);");
            for (int index = 0; index < members.Length; index++)
                body.Add((members[index].Mirrored ? "reuseLocal." : "completedInstance!.") + members[index].Name + " = " + temps[index] + "!;");
        }
    }


    /// <summary>Spells the fresh construction of a version-tolerant value, with the object initializer only for visible members.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="body">The receiving statements.</param>
    /// <param name="initializer">The members assigned by the object initializer.</param>
    /// <param name="temps">The temporary local of each member.</param>
    private static void AddVersionConstruction(PayloadRenderModel model, List<string> body, RenderMember[] initializer, string[] temps)
    {
        string arguments = model.ConstructorArguments == null ? string.Empty : string.Join(", ", model.ConstructorArguments.Select(name =>
            name == null ? "default" : temps[Array.FindIndex(model.Members, member => member.Name == name)] + "!"));
        if (initializer.Length == 0)
        {
            body.Add("value = new " + model.GlobalName + "(" + arguments + ");");
            return;
        }
        body.Add("value = new " + model.GlobalName + "(" + arguments + ")");
        body.Add("{");
        foreach (var member in initializer)
            body.Add(member.Name + " = " + temps[System.Array.IndexOf(model.Members, member)] + "!,");
        body.Add("};");
    }

    /// <summary>Emits version-tolerant value roots through their non-nullable state and schema contracts.</summary>
    /// <param name="model">The render model.</param>
    /// <param name="w">The writer.</param>
    /// <param name="operations">The operations the project calls.</param>
    internal static void EmitVersionStruct(PayloadRenderModel model, CodeWriter w, GeneratedOperations operations)
        => EmitStruct(model, w, operations);


    /// <summary>The maximum header length used by a measured versioned record.</summary>
    /// <param name="model">The measured version-tolerant model.</param>
    /// <returns>The complete descriptor header length.</returns>
    private static string VersionHeaderLength(PayloadRenderModel model) => VersionNumber(11 + 8 * model.Members.Length);

    /// <summary>Emits the descriptor table, slot lookup, staged read state and cold revision-1 schema.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <param name="w">The source writer.</param>
    /// <param name="reads">Whether compatibility and staging are required.</param>
    private static void VersionSchemaClass(PayloadRenderModel model, CodeWriter w, bool reads)
    {
        var schema = model.Version;
        w.Line("internal static class " + schema.Name);
        if (model.TypeConstraintText.Length != 0)
            w.Indented(() => w.Line(model.TypeConstraintText));
        w.Open();
        if (schema.Dynamic)
        {
            w.Line("private static readonly byte[] DescriptorBytes = CreateDescriptors();");
            w.Line("internal static global::System.ReadOnlySpan<byte> Descriptors => DescriptorBytes;");
            w.Line("internal static readonly ulong Schema = " + VersionCore + "LuminPackVersionRecord.ComputeSchema(Descriptors);");
            w.Blank();
            w.Line("private static byte[] CreateDescriptors()");
            w.Open();
            w.Line("byte[] bytes = new byte[" + VersionNumber(schema.Descriptors.Length * 8) + "];");
            for (int index = 0; index < schema.Descriptors.Length; index++)
            {
                string descriptor = VersionDescriptor(schema.Descriptors[index]);
                if (schema.RuntimeContracts[index])
                    descriptor = "Legacy.Fields[" + VersionNumber(index) + "].Describe(" +
                        VersionKey(schema.NameKeys[index][0]) + ", " + VersionNumber(schema.Width(index)) + ")";
                w.Line("global::System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(global::System.MemoryExtensions.AsSpan(bytes, " +
                    VersionNumber(index * 8) + "), " + descriptor + ");");
            }
            w.Line("return bytes;");
            w.Close();
        }
        else
        {
            ulong hash = unchecked((14695981039346656037UL ^ (uint)schema.Descriptors.Length) * 1099511628211UL);
            var bytes = new byte[schema.Descriptors.Length * 8];
            for (int index = 0; index < bytes.Length; index++)
            {
                byte value = (byte)(schema.Descriptors[index / 8] >> (index % 8 * 8));
                bytes[index] = value;
                hash = unchecked((hash ^ value) * 1099511628211UL);
            }
            w.Line("internal const ulong Schema = " + VersionDescriptor(hash) + ";");
            string values = string.Join(", ", bytes.Select(value => value.ToString(CultureInfo.InvariantCulture)));
            w.Line(model.ConstantSpans
                ? "internal static global::System.ReadOnlySpan<byte> Descriptors => [" + values + "];"
                : "internal static readonly byte[] Descriptors = new byte[] { " + values + " };");
        }

        w.Line("internal static readonly " + VersionCore + "LuminPackVersionMapCache Cache = new " +
            VersionCore + "LuminPackVersionMapCache(SlotOf);");
        w.Blank();
        w.Line("private static int SlotOf(uint nameKey, out ulong descriptor)");
        w.Open();
        if (schema.Descriptors.Length == 0)
        {
            w.Line("descriptor = 0;");
            w.Line("return -1;");
        }
        else
        {
            w.Line("var (slot, typeWidth) = nameKey switch");
            w.Open();
            for (int index = 0; index < schema.Descriptors.Length; index++)
            {
                ulong upper = schema.Descriptors[index] & 0xFFFFFFFF00000000UL;
                string expected = schema.RuntimeContracts[index]
                    ? "global::System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(Descriptors.Slice(" +
                        VersionNumber(index * 8) + ")) & 0xFFFFFFFF00000000UL"
                    : VersionDescriptor(upper);
                foreach (uint key in schema.NameKeys[index])
                    w.Line(VersionKey(key) + " => (" + VersionNumber(index) + ", " + expected + "),");
            }
            w.Line("_ => (-1, 0UL),");
            w.Close(";");
            w.Line("descriptor = typeWidth | nameKey;");
            w.Line("return slot;");
        }
        w.Close();

        if (reads)
        {
            w.Blank();
            w.Line("internal struct Fields");
            w.Open();
            for (int index = 0; index < schema.TempTypes.Length; index++)
                w.Line("internal " + schema.TempTypes[index] + " F" + VersionNumber(index) + ";");
            w.Close();
        }
        if (reads || schema.Dynamic)
        {
            w.Blank();
            w.Line("internal static class Legacy");
            w.Open();
            w.Line("internal static readonly " + VersionCore + "LuminPackVersionField[] Fields = new " + VersionCore + "LuminPackVersionField[]");
            w.Open();
            foreach (string entry in schema.FieldEntries)
                w.Line(entry);
            w.Close(";");
            w.Close();
        }
        w.Close();
    }

    /// <summary>Restores the advancing operation's shared registration if an isolated cursor fails.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="operation">The call into the isolated instance's binary root.</param>
    /// <param name="versioned">Whether the root publishes version schemas.</param>
    /// <param name="read">Whether the child state holds a reader's registrations.</param>
    private static void EmitIsolatedVersionOperation(CodeWriter w, string operation, bool versioned, bool read)
    {
        if (!versioned)
        {
            w.Line(operation);
            return;
        }
        w.Line("int scope = isolated.EnterVersionScope();");
        w.Line("bool completed = false;");
        w.Line("try");
        w.Open();
        w.Line(operation);
        w.Line("completed = true;");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("if (!completed)");
        w.Indented(() => w.Line(read ? "isolated.RollbackVersionScope(scope);" : "isolated.ExitVersionScope(scope);"));
        w.Close();
    }

    /// <summary>Spells the codec the record header and its inherited string members share.</summary>
    /// <param name="model">The version-tolerant payload.</param>
    /// <param name="w">The source writer.</param>
    /// <returns>The codec type.</returns>
    private static string VersionProfile(PayloadRenderModel model, CodeWriter w) =>
        model.DeclaredProfile == null ? Profile : ScopedProfile(w, model.DeclaredProfile);

    /// <summary>Spells the identifier suffix shared by a member's read and write helpers.</summary>
    /// <param name="member">The named field or property.</param>
    /// <returns>A helper name suffix.</returns>
    private static string VersionMemberName(RenderMember member) => "Member" + member.Name.TrimStart('@');

    /// <summary>Formats an integer literal independently of the build machine's culture.</summary>
    /// <param name="value">The integer value.</param>
    /// <returns>The C# integer literal.</returns>
    private static string VersionNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats a name key with a fixed width for easy comparison against a descriptor.</summary>
    /// <param name="key">The hash of a member name.</param>
    /// <returns>The hexadecimal uint literal.</returns>
    private static string VersionKey(uint key) => "0x" + key.ToString("X8", CultureInfo.InvariantCulture) + "U";

    /// <summary>Formats a descriptor or schema word.</summary>
    /// <param name="value">The descriptor or the schema hash.</param>
    /// <returns>The hexadecimal ulong literal.</returns>
    private static string VersionDescriptor(ulong value) => "0x" + value.ToString("X16", CultureInfo.InvariantCulture) + "UL";
}
