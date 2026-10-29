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
        var fixedLengths = VersionMetadataEncoder.FixedLengths(data);
        if (fixedLengths != null)
        {
            var fixedBytes = (bytes ?? VersionMetadataEncoder.Encode(data)).Concat(fixedLengths).ToArray();
            string declaration = bytes == null ? "readonly byte[] " : "global::System.ReadOnlySpan<byte> ";
            string assignment = bytes == null ? " = new byte[] { " : " => [";
            string terminator = bytes == null ? " };" : "];";
            sb.AppendLine("            internal static " + declaration + "FixedMetadata" + assignment + string.Join(", ", fixedBytes) + terminator);
            sb.AppendLine("            internal static " + declaration + "DefaultFixedPrefix" + assignment + "251, 1, 0, " + data.fields.Count +
                (fixedBytes.Length == 0 ? "" : ", " + string.Join(", ", fixedBytes)) + terminator);
        }
        sb.AppendLine("        }");
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
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
        if (!data.isValueType)
            sb.AppendLine("            if (value is null) { writer.WriteNullObjectHeader(); return; }");
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref Unsafe.AsRef(in value));");
        int memberCount = data.fields.Count;
        if (LuminPackCodeGenerator.FindAllUnmanagedType(data.fields))
        {
            sb.AppendLine($"            writer.WriteVersionHeader({memberCount});");
            sb.AppendLine($"            writer.WriteVersionMetadata({data.VersionSchemaType}.Metadata, {memberCount});");
            for (int i = 0; i < memberCount; i++)
                sb.AppendLine($"            writer.WriteVarInt(Unsafe.SizeOf<{data.fields[i].TypeName}>());");
            for (int start = 0; start < memberCount; start += MaxParametersPerCall)
            {
                var batch = data.fields.Skip(start).Take(MaxParametersPerCall).ToArray();
                foreach (var field in batch.Where(field => field.IsVolatile))
                    sb.AppendLine($"            var {field.Name}Snapshot = {Access(field)};");
                sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(" + string.Join(", ", batch.Select(field => field.IsVolatile ? field.Name + "Snapshot" : Access(field))) + "));");
            }
        }
        else
        {
            sb.AppendLine("            var writerBuffer = global::LuminPack.Utility.LuminBufferWriterPool.Rent();");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                var tempWriter = new LuminPackWriter(writerBuffer); tempWriter.Options = writer.OptionState;");
            sb.AppendLine($"                Span<int> offsets = stackalloc int[{memberCount}];");
            for (int i = 0; i < memberCount; i++)
            {
                var field = data.fields[i];
                if (field.IsVolatile) sb.AppendLine($"                var {field.Name}Snapshot = {Access(field)};");
                if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
                    sb.AppendLine($"                tempWriter.Advance(tempWriter.WriteUnmanaged({(field.IsVolatile ? field.Name + "Snapshot" : Access(field))}));");
                else
                    sb.AppendLine($"                tempWriter.{(field.IsCompress ? "WriteValueWithCompress" : "WriteValue")}({Access(field)});");
                sb.AppendLine($"                offsets[{i}] = tempWriter.GetCurrentSpanIndex();");
            }
            sb.AppendLine($"                writer.WriteVersionHeader({memberCount});");
            sb.AppendLine($"                writer.WriteVersionMetadata({data.VersionSchemaType}.Metadata, {memberCount});");
            sb.AppendLine($"                for (int i = 0; i < {memberCount}; i++)");
            sb.AppendLine("                    writer.WriteVarInt(offsets[i] - (i == 0 ? 0 : offsets[i - 1]));");
            sb.AppendLine("                writerBuffer.CompleteWrite(tempWriter.GetCurrentSpanIndex());");
            sb.AppendLine("                writerBuffer.WriteToAndReset(ref writer);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally { global::LuminPack.Utility.LuminBufferWriterPool.Return(writerBuffer); }");
        }
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
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string classGlobalName = data.classFullName;
        var fixedLengths = VersionMetadataEncoder.FixedLengths(data);
        bool returnFixedFields = fixedLengths != null && data.fields.Count > 0 && data.fields.Count <= 7;
        var reusableFields = data.fields.Where(field => field.IsVolatile ||
            fixedLengths == null && !LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type)).ToArray();
        string metadataMember = fixedLengths == null ? "Metadata" : "FixedMetadata";
        string prefixMember = fixedLengths == null ? "DefaultPrefix" : "DefaultFixedPrefix";
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
            sb.AppendLine(item.Item3 ? $"            {classGlobalName}.{item.Item1}();" : $"            value{(data.isValueType ? "." : "?.")}{item.Item1}();");
        foreach (var field in data.localFields)
            sb.AppendLine($"            {field.TypeName}{(field.IsValue ? "" : "?")} {field.Name}Temp = default!;");
        sb.AppendLine($"            bool sameSchema = reader.TryReadVersionSchema({data.VersionSchemaType}.{metadataMember}, {data.fields.Count}, {data.VersionSchemaType}.{prefixMember});");
        sb.AppendLine($"            int count = sameSchema ? {data.fields.Count} : reader.ReadVersionHeader();");
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
        if (fixedLengths != null)
        {
            sb.AppendLine($"                reader.ValidateVersionPayloadSize({fixedLengths.Sum(length => (int)length)});");
            for (int i = 0; i < data.fields.Count; i++)
                AppendFieldRead(sb, data.fields[i], $"Unsafe.SizeOf<{data.fields[i].TypeName}>()", "                ", restrictInput: false);
        }
        else
        {
            sb.AppendLine($"                reader.ValidateVersionPayloadSize({data.fields.Count});");
            for (int i = 0; i < data.fields.Count; i++)
                sb.AppendLine($"                int delta{i} = reader.ReadVersionLength({data.fields.Count - i - 1});");
            sb.AppendLine("                reader.ValidateVersionPayloadSize(" + (data.fields.Count == 0 ? "0L" : "(long)" + string.Join(" + ", Enumerable.Range(0, data.fields.Count).Select(i => "delta" + i))) + ");");
            for (int i = 0; i < data.fields.Count; i++)
                AppendFieldRead(sb, data.fields[i], "delta" + i, "                ", restrictInput: false);
        }
        sb.AppendLine("            }");
        string mappedParameters = returnFixedFields ? "" : string.Join(", ", data.fields.Select(field => "ref " + field.TypeSymbol.ToDisplayString(Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat) + " " + field.Name + "Temp"));
        string mappedArguments = returnFixedFields ? "" : string.Join(", ", data.fields.Select(field => "ref " + field.Name + "Temp"));
        string returnedFields = string.Join(", ", data.fields.Select(field => field.Name + "Temp"));
        string returnedTypes = string.Join(", ", data.fields.Select(field => field.TypeName));
        string returnType = !returnFixedFields ? "void" : data.fields.Count == 1 ? returnedTypes : "(" + returnedTypes + ")";
        string assignment = !returnFixedFields ? "" : data.fields.Count == 1 ? returnedFields + " = " : "(" + returnedFields + ") = ";
        sb.AppendLine("            else " + assignment + "ReadEvolvedFields(ref reader, count" + (mappedArguments.Length == 0 ? "" : ", " + mappedArguments) + ");");
        sb.AppendLine("            // Keep the uncommon mapped read separate from the same-schema native body.");
        sb.AppendLine("            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        sb.AppendLine("            static " + returnType + " ReadEvolvedFields(ref LuminPackReader reader, int count" + (mappedParameters.Length == 0 ? "" : ", " + mappedParameters) + ")");
        sb.AppendLine("            {");
        if (returnFixedFields)
            foreach (var field in data.fields)
                sb.AppendLine($"                {field.TypeName} {field.Name}Temp = default;");
        sb.AppendLine("                Span<int> deltas = stackalloc int[count];");
        sb.AppendLine("                Span<int> slots = stackalloc int[count];");
        sb.AppendLine($"                Span<byte> present = stackalloc byte[{data.fields.Count}];");
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine($"                reader.ReadVersionTable({data.VersionSchemaType}.Fields, slots, deltas, present);");
        sb.AppendLine("#else");
        sb.AppendLine($"                reader.Position = LuminPackReader.ReadVersionTable(reader, {data.VersionSchemaType}.Fields, slots, deltas, present);");
        sb.AppendLine("#endif");
        sb.AppendLine("                for (int i = 0; i < count; i++)");
        sb.AppendLine("                {");
        sb.AppendLine("                    switch (slots[i])");
        sb.AppendLine("                    {");
        for (int i = 0; i < data.fields.Count; i++)
        {
            sb.AppendLine($"                        case {i}:");
            AppendFieldRead(sb, data.fields[i], "deltas[i]", "                            ");
            sb.AppendLine("                            break;");
        }
        sb.AppendLine("                        default: reader.Advance(deltas[i]); break;");
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
