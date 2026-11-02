using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.Code.Core;

/// <summary>Emits named version-tolerant records while preserving payload and construction mechanisms.</summary>
public static class LuminPackVersionTolerantCodeGenerator
{
    const int MaxParametersPerCall = 15;

    /// <summary>Emits the shared immutable member metadata for one generic definition.</summary>
    /// <param name="data">The complete member contract.</param>
    /// <param name="sb">The generated source.</param>
    /// <param name="metadata">The consuming language and runtime capabilities.</param>
    internal static void GenerateSchema(LuminDataInfo data, StringBuilder sb, MetaInfo metadata)
    {
        string name = data.VersionSchemaType.Substring(data.VersionSchemaType.LastIndexOf(".Version_", StringComparison.Ordinal) + 1);
        sb.AppendLine("        internal static class " + name);
        LuminPackExtensionGenerator.AppendGenericConstraints(sb, data);
        sb.AppendLine("        {");
        sb.AppendLine("            internal static readonly global::LuminPack.Core.LuminPackVersionField[] Fields = new global::LuminPack.Core.LuminPackVersionField[]");
        sb.AppendLine("            {");
        foreach (var field in data.fields)
        {
            string aliases = string.Join(", ", new[] { field.Name }.Concat(field.OldNames).Where(alias => alias != field.SerializedName).Select(Literal));
            string representation = VersionMetadataEncoder.Representation(field);
            sb.AppendLine("                new global::LuminPack.Core.LuminPackVersionField(" + Literal(field.SerializedName) +
                ", new string[] { " + aliases + " }, typeof(" + field.TypeSymbol.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat) + "), " + Literal(representation) + "),");
        }
        sb.AppendLine("            };");
        var bytes = metadata.IsNet8 && Enum.IsDefined(typeof(LanguageVersion), (LanguageVersion)1200) &&
            metadata.CSharpVersion >= (LanguageVersion)1200 ? VersionMetadataEncoder.Encode(data) : null;
        if (bytes == null)
        {
            sb.AppendLine("            internal static readonly byte[] Metadata = global::LuminPack.Core.LuminPackVersionField.CreateMetadata(Fields);");
            sb.AppendLine($"            internal static readonly byte[] DefaultPrefix = global::LuminPack.Core.LuminPackVersionField.CreateDefaultPrefix(Metadata, {data.fields.Count});");
        }
        else
        {
            sb.AppendLine("            internal static global::System.ReadOnlySpan<byte> Metadata => [" + string.Join(", ", bytes) + "];");
            sb.AppendLine("            internal static global::System.ReadOnlySpan<byte> DefaultPrefix => [251, 1, 0, " + data.fields.Count +
                (bytes.Length == 0 ? "" : ", " + string.Join(", ", bytes)) + "];");
        }
        // Revision 2: header, names and types, then one width per member: a raw width below 255, or 255 for a
        // member whose payload carries a four-byte length. All of it is constant, so one comparison matches it.
        var widths = VersionMetadataEncoder.Widths(data);
        if (bytes != null && widths.All(width => width != null))
        {
            var prefix = new byte[] { 251, 2, 0, (byte)data.fields.Count }.Concat(bytes).Concat(widths.Select(width => (byte)width.Value)).ToArray();
            sb.AppendLine("            internal static global::System.ReadOnlySpan<byte> Prefix => [" + string.Join(", ", prefix) + "];");
            ulong fingerprint = 14695981039346656037UL;
            foreach (byte value in prefix.Skip(4)) fingerprint = (fingerprint ^ value) * 1099511628211UL;
            sb.AppendLine("            internal const ulong Fingerprint = 0x" + fingerprint.ToString("X16", System.Globalization.CultureInfo.InvariantCulture) + "UL;");
            AppendShortState(sb, data.fields.Count, prefix.Length > 12 ? "true" : "false");
            AppendPrefixMatch(sb, prefix);
            AppendPrefixWrite(sb, prefix);
            AppendPrefixStore(sb, prefix);
        }
        else
        {
            sb.AppendLine("            internal static readonly byte[] Prefix = global::LuminPack.Core.LuminPackVersionField.CreatePrefix(Metadata, " + data.fields.Count +
                ", new int[] { " + string.Join(", ", data.fields.Select((field, index) => widths[index] is int width ? (width == 255 ? "-1" : width.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    : "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + field.TypeName + ">()")) + " });");
            sb.AppendLine("            internal static readonly ulong Fingerprint = global::LuminPack.Core.LuminPackVersionField.Fingerprint(global::System.MemoryExtensions.AsSpan(Prefix, 4));");
            AppendShortState(sb, data.fields.Count, "Prefix.Length > 12");
            AppendPrefixMatch(sb, null);
            AppendPrefixWrite(sb, null);
            AppendPrefixStore(sb, null);
        }
        sb.AppendLine("        }");
    }

    /// <summary>Identifies a member stored at its exact raw width in a matching record.</summary>
    /// <param name="field">The member.</param><param name="width">Its constant record width, or null when known only at run time.</param>
    /// <returns>Whether the member can join a raw run.</returns>
    private static bool IsRawRunMember(LuminDataField field, int? width)
        => width is int value && value != 255 && LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type) && !field.IsVolatile && !field.IsCompress;

    /// <summary>Emits the short-record state: the member count, whether a short record is smaller, and this thread's last full write.</summary>
    /// <param name="sb">The schema class source.</param>
    /// <param name="count">The member count.</param>
    /// <param name="shorter">A constant or run-time expression telling whether the full prefix exceeds the twelve-byte short header.</param>
    /// <remarks>The first record of a schema in an operation carries its member table; later records carry only the fingerprint.
    /// A thread-static operation identity decides, so a nested operation on the same thread only makes later records full again.</remarks>
    private static void AppendShortState(StringBuilder sb, int count, string shorter)
    {
        sb.AppendLine("            internal const byte Count = " + count + ";");
        sb.AppendLine("            internal static bool Shortens => " + shorter + ";");
        sb.AppendLine("            [global::System.ThreadStatic] private static int t_written;");
        sb.AppendLine("            /// <summary>Reports whether this operation already wrote the member table, recording it otherwise.</summary>");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            private static bool WrittenBefore(ref global::LuminPack.Core.LuminPackWriter writer)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (!Shortens) return false;");
        sb.AppendLine("                int operation = writer.GetSchemaOperation();");
        sb.AppendLine("                if (t_written == operation) return true;");
        sb.AppendLine("                t_written = operation;");
        sb.AppendLine("                return false;");
        sb.AppendLine("            }");
    }

    /// <summary>Emits the prefix store into reserved record room, returning the byte after the prefix.</summary>
    /// <param name="sb">The schema class source.</param>
    /// <param name="prefix">The constant prefix with a zero profile byte, or null when its widths are known only at run time.</param>
    private static void AppendPrefixStore(StringBuilder sb, byte[] prefix)
    {
        const string memory = "global::System.Runtime.CompilerServices.Unsafe";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        // The stored byte count is returned instead of a reference, which would not escape beside the writer argument.
        sb.AppendLine("            internal static int StorePrefix<TOptions>(ref global::LuminPack.Core.LuminPackWriter writer, ref byte target) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            {");
        sb.AppendLine("                if (WrittenBefore(ref writer))");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::LuminPack.Core.LuminPackWriter.StoreVersionShort<TOptions>(ref target, Count, Fingerprint);");
        sb.AppendLine("                    return 12;");
        sb.AppendLine("                }");
        string runtime = "{ global::LuminPack.Core.LuminPackWriter.StoreVersionPrefix<TOptions>(ref target, Prefix); return Prefix.Length; }";
        if (prefix == null || prefix.Length < 8)
        {
            sb.AppendLine("                " + runtime);
            sb.AppendLine("            }");
            return;
        }
        // 32-bit targets keep typed unaligned access within four bytes and use the runtime copy.
        sb.AppendLine("                if (" + memory + ".SizeOf<nint>() != 8) " + runtime);
        AppendPrefixWords(sb, prefix, "target");
        sb.AppendLine("                return " + prefix.Length + ";");
        sb.AppendLine("            }");
    }

    /// <summary>Stores a constant prefix as disjoint eight-byte words and at most one value of each narrower width.</summary>
    /// <param name="sb">The schema class source.</param>
    /// <param name="prefix">The constant prefix with a zero profile byte.</param>
    /// <param name="target">The reserved destination reference.</param>
    private static void AppendPrefixWords(StringBuilder sb, byte[] prefix, string target)
    {
        const string memory = "global::System.Runtime.CompilerServices.Unsafe";
        int offset = 0;
        foreach (int width in new[] { 8, 4, 2, 1 })
            while (offset + width <= prefix.Length && (width == 8 || prefix.Length - offset < 2 * width))
            {
                ulong word = 0;
                for (int index = width - 1; index >= 0; index--) word = word << 8 | prefix[offset + index];
                string type = width == 8 ? "ulong" : width == 4 ? "uint" : width == 2 ? "ushort" : "byte";
                string hex = word.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
                // The binary profile is stamped into the third header byte at run time.
                string value = offset == 0
                    ? "(" + type + ")(0x" + hex + "UL | (ulong)global::LuminPack.Core.LuminPackWriter.BinaryProfile<TOptions>() << 16)"
                    : "(" + type + ")0x" + hex + (width == 8 ? "UL" : "U");
                string location = offset == 0 ? "ref " + target : "ref " + memory + ".Add(ref " + target + ", " + offset + ")";
                sb.AppendLine("                " + memory + ".WriteUnaligned(" + location + ", " + value + ");");
                offset += width;
            }
    }

    /// <summary>Emits the prefix write, storing a constant prefix as disjoint words without a runtime copy loop.</summary>
    /// <param name="sb">The schema class source.</param>
    /// <param name="prefix">The constant prefix with a zero profile byte, or null when its widths are known only at run time.</param>
    /// <remarks>Disjoint stores keep a following wide load over the record from stalling on store forwarding.</remarks>
    private static void AppendPrefixWrite(StringBuilder sb, byte[] prefix)
    {
        const string memory = "global::System.Runtime.CompilerServices.Unsafe";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static void WritePrefix<TOptions>(ref global::LuminPack.Core.LuminPackWriter writer) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            {");
        sb.AppendLine("                if (WrittenBefore(ref writer))");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::LuminPack.Core.LuminPackWriter.StoreVersionShort<TOptions>(ref writer.ReservePrefix(12), Count, Fingerprint);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        if (prefix == null || prefix.Length < 8)
        {
            sb.AppendLine("                writer.WriteVersionPrefix<TOptions>(Prefix);");
            sb.AppendLine("            }");
            return;
        }
        // 32-bit targets keep typed unaligned access within four bytes and use the runtime copy.
        sb.AppendLine("                if (" + memory + ".SizeOf<nint>() != 8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteVersionPrefix<TOptions>(Prefix);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
        sb.AppendLine("                ref byte target = ref writer.ReservePrefix(" + prefix.Length + ");");
        AppendPrefixWords(sb, prefix, "target");
        sb.AppendLine("            }");
    }

    /// <summary>Emits the same-schema prefix test, comparing a constant prefix as eight-byte words folded into one branch.</summary>
    /// <param name="sb">The schema class source.</param>
    /// <param name="prefix">The constant prefix with a zero profile byte, or null when its widths are known only at run time.</param>
    private static void AppendPrefixMatch(StringBuilder sb, byte[] prefix)
    {
        const string memory = "global::System.Runtime.CompilerServices.Unsafe";
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("            internal static bool TryReadPrefix<TOptions>(ref global::LuminPack.Core.LuminPackReader reader) where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
        sb.AppendLine("            {");
        // A short record of this schema carries the receiver's own fingerprint.
        sb.AppendLine("                if (Shortens && reader.TryReadVersionShort<TOptions>(Count, Fingerprint)) return true;");
        if (prefix == null || prefix.Length < 8)
        {
            sb.AppendLine("                return reader.TryReadVersionPrefix<TOptions>(Prefix);");
            sb.AppendLine("            }");
            return;
        }
        // 32-bit targets keep typed unaligned access within four bytes and use the runtime comparison.
        sb.AppendLine("                if (" + memory + ".SizeOf<nint>() != 8) return reader.TryReadVersionPrefix<TOptions>(Prefix);");
        sb.AppendLine("                ref byte input = ref reader.PeekInput(" + prefix.Length + ", out bool available);");
        sb.AppendLine("                if (!available) return false;");
        var offsets = new List<int>();
        for (int offset = 0; offset + 8 <= prefix.Length; offset += 8) offsets.Add(offset);
        if (prefix.Length % 8 != 0) offsets.Add(prefix.Length - 8);
        var terms = offsets.Select(offset =>
        {
            ulong word = 0;
            for (int index = 7; index >= 0; index--) word = word << 8 | prefix[offset + index];
            string load = offset == 0 ? memory + ".ReadUnaligned<ulong>(ref input)" : memory + ".ReadUnaligned<ulong>(ref " + memory + ".Add(ref input, " + offset + "))";
            string expected = "0x" + word.ToString("X16", System.Globalization.CultureInfo.InvariantCulture) + "UL";
            // The binary profile is stamped into the third header byte at run time.
            if (offset == 0) expected = "(" + expected + " | (ulong)global::LuminPack.Core.LuminPackWriter.BinaryProfile<TOptions>() << 16)";
            return "(" + load + " ^ " + expected + ")";
        });
        sb.AppendLine("                if ((" + string.Join(" | ", terms) + ") != 0) return false;");
        sb.AppendLine("                reader.Advance(" + prefix.Length + ");");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
    }

    /// <summary>Escapes metadata as a C# string literal.</summary>
    /// <param name="value">The exact member identity.</param>
    /// <returns>A quoted C# string literal.</returns>
    private static string Literal(string value) => SymbolDisplay.FormatLiteral(value, true);

    /// <summary>Selects direct volatile access or the established Local storage view.</summary>
    /// <param name="field">The member being accessed.</param>
    /// <param name="local">The Local mirror variable.</param>
    /// <returns>The generated storage expression.</returns>
    private static string Access(LuminDataField field, string local = "local") => field.IsVolatile
        ? field.IsPrivate ? "global::System.Threading.Volatile.Read(ref " + local + "." + field.Identifier + ")" : "value!." + field.Identifier
        : local + "." + field.Identifier;

    /// <summary>Assigns a reconstructed member while preserving volatile stores and private storage views.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="field">The destination member.</param>
    /// <param name="local">The Local mirror variable.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="instance">The stable reference receiving public volatile field stores.</param>
    private static void Assign(StringBuilder sb, LuminDataField field, string local, string indent, string instance = "value!")
    {
        string value = field.Name + "Temp!";
        sb.AppendLine(indent + (field.IsVolatile ? field.IsPrivate
            ? "global::System.Threading.Volatile.Write(ref " + local + "." + field.Identifier + ", " + value + ");"
            : instance + "." + field.Identifier + " = " + value + ";"
            : local + "." + field.Identifier + " = " + value + ";"));
    }

    /// <summary>Appends binary serialization statements using fixed batches or the shared-state buffer.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb) => GenerateSerializeCode(data, sb, false);

    /// <summary>Emits the original record while selecting a supported cached fixed-prefix operation.</summary>
    /// <param name="data">The complete declared member contract.</param>
    /// <param name="sb">The generated operation body.</param>
    /// <param name="fixedPrefix">Whether the referenced runtime supports fixed-prefix copying.</param>
    internal static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb, bool fixedPrefix)
    {
        string classGlobalName = data.classFullName;
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
        if (!data.isValueType)
            sb.AppendLine("            if (value is null) { writer.WriteNullObjectHeader(); return; }");
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref Unsafe.AsRef(in value));");
        // The prefix joins the first reserved group; the profile argument is replaced with the operation's compiled profile.
        if (ReservedRecordEmitter.TryGenerate(sb, data.fields, VersionMetadataEncoder.Widths(data), field => Access(field), data.VersionSchemaType + ".Prefix.Length",
                indent => sb.AppendLine(indent + "__cursor = ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __cursor, " + data.VersionSchemaType +
                    ".StorePrefix<global::LuminPack.Option.LuminPackUtf16LengthOptions>(ref writer, ref __cursor));")))
        {
            foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
                sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
            return;
        }
        // One pass: the cached prefix, raw members in batches, and variable members behind a reserved length.
        // The profile argument is replaced with the operation's compiled profile.
        sb.AppendLine($"            {data.VersionSchemaType}.WritePrefix<global::LuminPack.Option.LuminPackUtf16LengthOptions>(ref writer);");
        var widths = VersionMetadataEncoder.Widths(data);
        var batch = new List<LuminDataField>();
        void Flush()
        {
            if (batch.Count == 0) return;
            foreach (var field in batch.Where(field => field.IsVolatile))
                sb.AppendLine($"            var {field.Name}Snapshot = {Access(field)};");
            sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(" + string.Join(", ", batch.Select(field => field.IsVolatile ? field.Name + "Snapshot" : Access(field))) + "));");
            batch.Clear();
        }
        for (int i = 0; i < data.fields.Count; i++)
        {
            var field = data.fields[i];
            if (widths[i] is int width && width != 255)
            {
                batch.Add(field);
                if (batch.Count == MaxParametersPerCall) Flush();
                continue;
            }
            Flush();
            if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
            {
                // A raw member whose width is known only at run time carries a length when it is 255 bytes or wider.
                sb.AppendLine("            {");
                if (field.IsVolatile) sb.AppendLine($"                var {field.Name}Snapshot = {Access(field)};");
                sb.AppendLine($"                if (Unsafe.SizeOf<{field.TypeName}>() >= 255) writer.WriteVersionLength(Unsafe.SizeOf<{field.TypeName}>());");
                sb.AppendLine($"                writer.Advance(writer.WriteUnmanaged({(field.IsVolatile ? field.Name + "Snapshot" : Access(field))}));");
                sb.AppendLine("            }");
                continue;
            }
            sb.AppendLine("            {");
            sb.AppendLine($"                int {field.Name}Length = writer.ReserveVersionLength();");
            sb.AppendLine($"                writer.{(field.IsCompress ? "WriteValueWithCompress" : "WriteValue")}({Access(field)});");
            sb.AppendLine($"                writer.CompleteVersionLength({field.Name}Length);");
            sb.AppendLine("            }");
        }
        Flush();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
    }

    /// <summary>Appends one field operation within its dynamic framing boundary.</summary>
    /// <param name="sb">The generated source.</param>
    /// <param name="field">The known receiving member.</param>
    /// <param name="length">The recorded payload-length expression.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="restrictInput">Whether an evolved schema requires a temporary field input boundary.</param>
    private static void AppendFieldRead(StringBuilder sb, LuminDataField field, string length, string indent, bool restrictInput = true)
    {
        sb.AppendLine(indent + "{");
        indent += "    ";
        if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
        {
            sb.AppendLine(indent + $"global::LuminPack.Core.LuminPackReader.ValidateVersionFieldEnd({length}, Unsafe.SizeOf<{field.TypeName}>());");
            sb.AppendLine(indent + $"reader.EnsureReadable(reader.Position, Unsafe.SizeOf<{field.TypeName}>());");
            sb.AppendLine(indent + $"reader.Advance(reader.ReadUnmanaged(out {field.Name}Temp));");
        }
        else
        {
            sb.AppendLine(indent + $"int end = reader.GetCurrentSpanIndex() + {length};");
            if (restrictInput)
            {
                sb.AppendLine(indent + $"int previousLength = reader.EnterVersionField({length});");
                sb.AppendLine(indent + "try");
                sb.AppendLine(indent + "{");
                EmitterTypeTraits.AppendReusableElementRead(sb, field.TypeSymbol, field.Name + "Temp", indent + "    ", field.IsCompress);
                sb.AppendLine(indent + "}");
                sb.AppendLine(indent + "finally { reader.ExitVersionField(previousLength); }");
            }
            else EmitterTypeTraits.AppendReusableElementRead(sb, field.TypeSymbol, field.Name + "Temp", indent, field.IsCompress);
            sb.AppendLine(indent + "global::LuminPack.Core.LuminPackReader.ValidateVersionFieldEnd(reader.GetCurrentSpanIndex(), end);");
        }
        sb.AppendLine(indent.Substring(4) + "}");
    }

    /// <summary>Appends reads that map complete names, skip unknown payloads and retain reusable storage.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb) => GenerateDeserializeCode(data, sb, false);

    /// <summary>Appends binary deserialization statements, with a same-schema path that reads into the object like an ordinary record.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="guardNestedReads">Whether inline nested records check their markers for untrusted input.</param>
    internal static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb, bool guardNestedReads)
    {
        string classGlobalName = data.classFullName;
        var widths = VersionMetadataEncoder.Widths(data);
        bool allRaw = widths.All(width => width is int value && value != 255);
        bool returnFixedFields = allRaw && data.fields.Count > 0 && data.fields.Count <= 7;
        var reusableFields = data.fields.Where(field => field.IsVolatile ||
            !allRaw && !LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type)).ToArray();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
        foreach (var field in data.localFields)
            sb.AppendLine($"            {field.TypeName}{(field.IsValue ? "" : "?")} {field.Name}Temp = default!;");
        // The profile argument is replaced with the operation's compiled profile.
        sb.AppendLine($"            bool sameSchema = {data.VersionSchemaType}.TryReadPrefix<global::LuminPack.Option.LuminPackUtf16LengthOptions>(ref reader);");
        sb.AppendLine("            int revision = 2;");
        sb.AppendLine($"            int count = sameSchema ? {data.fields.Count} : reader.ReadVersionHeader(out revision);");
        sb.AppendLine("            if (count < 0) { value = default; return; }");
        if (!data.isValueType && reusableFields.Length != 0)
        {
            sb.AppendLine("            if (value is not null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                ref var reuseLocal = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
            foreach (var field in reusableFields)
                sb.AppendLine($"                {field.Name}Temp = {Access(field, "reuseLocal")};");
            sb.AppendLine("            }");
        }
        sb.AppendLine("            if (sameSchema)");
        sb.AppendLine("            {");
        for (int i = 0; i < data.fields.Count; i++)
        {
            var field = data.fields[i];
            // A run of raw members has no length prefixes in a matching record, so one checked advance covers the run.
            int last = i;
            while (last + 1 < data.fields.Count && IsRawRunMember(data.fields[last + 1], widths[last + 1])) last++;
            if (last > i && IsRawRunMember(field, widths[i]) &&
                LuminPackCodeGenerator.TryGenerateCursorFixedGroup(sb, data.fields, i, last, member => member.Name + "Temp", "                ", write: false, allowBulkCopy: false))
            {
                i = last;
                continue;
            }
            string length = "Unsafe.SizeOf<" + field.TypeName + ">()";
            if (widths[i] is not int width || width == 255)
            {
                sb.AppendLine(LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type)
                    ? $"                int length{i} = Unsafe.SizeOf<{field.TypeName}>() < 255 ? Unsafe.SizeOf<{field.TypeName}>() : reader.ReadVersionPayloadLength();"
                    : $"                int length{i} = reader.ReadVersionPayloadLength();");
                length = "length" + i;
            }
            AppendFieldRead(sb, field, length, "                ", restrictInput: false);
        }
        sb.AppendLine("            }");
        string mappedParameters = returnFixedFields ? "" : string.Join(", ", data.fields.Select(field => "ref " + field.TypeSymbol.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat) + " " + field.Name + "Temp"));
        string mappedArguments = returnFixedFields ? "" : string.Join(", ", data.fields.Select(field => "ref " + field.Name + "Temp"));
        string returnedFields = string.Join(", ", data.fields.Select(field => field.Name + "Temp"));
        string returnedTypes = string.Join(", ", data.fields.Select(field => field.TypeName));
        string returnType = !returnFixedFields ? "void" : data.fields.Count == 1 ? returnedTypes : "(" + returnedTypes + ")";
        string assignment = !returnFixedFields ? "" : data.fields.Count == 1 ? returnedFields + " = " : "(" + returnedFields + ") = ";
        sb.AppendLine("            else " + assignment + "ReadEvolvedFields(ref reader, count, revision" + (mappedArguments.Length == 0 ? "" : ", " + mappedArguments) + ");");
        sb.AppendLine("            // Keep the uncommon mapped read separate from the same-schema native body.");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static " + returnType + " ReadEvolvedFields(ref LuminPackReader reader, int count, int revision" + (mappedParameters.Length == 0 ? "" : ", " + mappedParameters) + ")");
        sb.AppendLine("            {");
        if (returnFixedFields)
            foreach (var field in data.fields)
                sb.AppendLine($"                {field.TypeName} {field.Name}Temp = default;");
        sb.AppendLine("                Span<int> widths = stackalloc int[count];");
        sb.AppendLine("                Span<int> slots = stackalloc int[count];");
        sb.AppendLine($"                Span<byte> present = stackalloc byte[{data.fields.Count}];");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine($"                reader.ReadVersionTable({data.VersionSchemaType}.Fields, revision, slots, widths, present);");
        sb.AppendLine("#else");
        sb.AppendLine($"                reader.Position = LuminPackReader.ReadVersionTable(reader, {data.VersionSchemaType}.Fields, revision, slots, widths, present);");
        sb.AppendLine("#endif");
        sb.AppendLine("                for (int i = 0; i < count; i++)");
        sb.AppendLine("                {");
        // Revision 1 supplies every length in its table; revision 2 supplies raw widths and inline variable lengths.
        sb.AppendLine("                    int length = widths[i] >= 0 ? widths[i] : reader.ReadVersionPayloadLength();");
        sb.AppendLine("                    switch (slots[i])");
        sb.AppendLine("                    {");
        for (int i = 0; i < data.fields.Count; i++)
        {
            sb.AppendLine($"                        case {i}:");
            AppendFieldRead(sb, data.fields[i], "length", "                            ");
            sb.AppendLine("                            break;");
        }
        sb.AppendLine("                        default: reader.Advance(length); break;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        for (int i = 0; i < data.fields.Count; i++)
            sb.AppendLine($"                if (present[{i}] == 0) {data.fields[i].Name}Temp = default!;");
        if (returnFixedFields)
            sb.AppendLine("                return " + (data.fields.Count == 1 ? returnedFields : "(" + returnedFields + ")") + ";");
        sb.AppendLine("            }");
        // Collect constructor arguments.
        var constructorParams = new List<string>();
        if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
        {
            foreach (var param in data.SelectedConstructor.Parameters)
            {
                var matchingField = data.fields.FirstOrDefault(f => f.Name == param.MatchingFieldName);
                if (matchingField != null)
                {
                    constructorParams.Add($"{matchingField.Name}Temp!");
                }
                else
                {
                    constructorParams.Add("default");
                }
            }
        }

        string constructorArgs = string.Join(", ", constructorParams);

        // Collect public fields assigned through an object initializer.
        var initializerFields = data.fields.Where(f =>
            (data.SelectedConstructor == null ||
             !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
            !f.IsPrivate && !f.isProperty
        ).ToList();

        // Collect private fields that require separate assignments.
        var privateFields = data.fields.Where(f =>
            (data.SelectedConstructor == null ||
             !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
            (f.IsPrivate || f.isProperty)
        ).ToList();

        if (!data.isValueType)
        {
            sb.AppendLine("            var completedInstance = value;");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
        }
        if (data.RentPoolMethod != null)
        {
            // Obtain the instance from the object pool.
            if (data.RentPoolMethod.ReturnsByRef)
                sb.AppendLine($"            value = ref {classGlobalName}.{data.RentPoolMethod.Name}()!;");
            else
            {
                sb.AppendLine($"            value = {classGlobalName}.{data.RentPoolMethod.Name}();");
            }

            // Populate all fields of pooled objects without relying on constructors.
            sb.AppendLine($"            // Populate every field on the pooled instance.");

            // Assign public and private fields through the Local mirror.
            if (data.isValueType)
            {
                sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
            }
            else
            {
                sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
            }

            // Populate all fields.
            foreach (var field in data.fields)
            {
                Assign(sb, field, "local", "            ");
            }
        }
        else
        {
            // Construct the instance with an object initializer.
            if (initializerFields.Count > 0)
            {
                // Handle an object initializer.
                if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                {
                    sb.AppendLine($"            value = new {classGlobalName}({constructorArgs})");
                }
                else
                {
                    sb.AppendLine($"            value = new {classGlobalName}()");
                }
                sb.AppendLine("            {");
                foreach (var field in initializerFields)
                {
                    sb.AppendLine($"                {field.Identifier} = {field.Name}Temp!,");
                }
                sb.AppendLine("            };");
            }
            else
            {
                // Handle construction without an object initializer.
                if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                {
                    sb.AppendLine($"            value = new {classGlobalName}({constructorArgs});");
                }
                else
                {
                    sb.AppendLine($"            value = new {classGlobalName}();");
                }
            }

            // Assign private fields through the Local mirror.
            if (privateFields.Count > 0)
            {
                sb.AppendLine($"            // Assign private fields.");
                if (data.isValueType)
                {
                    sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                }
                else
                {
                    sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                }

                foreach (var field in privateFields)
                {
                    Assign(sb, field, "local", "            ");
                }
            }
        }

        if (!data.isValueType)
        {
            sb.AppendLine("                completedInstance = value;");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine($"                ref var reuseLocal = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref completedInstance!);");
            foreach (var field in data.fields)
                Assign(sb, field, "reuseLocal", "                ", "completedInstance!");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            {(data.isValueType ? "value." : "completedInstance?.")}{item.Item1}();");
        }
        sb.AppendLine();
    }
}
