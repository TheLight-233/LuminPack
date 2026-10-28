using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

/// <summary>Provides lumin pack circle reference code generator functionality.</summary>
public static class LuminPackCircleReferenceCodeGenerator
{
    const int MaxParametersPerCall = 15;

    /// <summary>Appends statements that calculate the binary payload size.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    public static void GenerateCalculateOffsetCode(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        sb.AppendLine("            var (existsReference, id) = evaluator.OptionState.GetOrAddReference(value);");
        sb.AppendLine("            if (existsReference)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += 1;");
        sb.AppendLine("                evaluator += LuminPackEvaluator.CalculateVarInt(id);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        int slotCount = data.fields.Count == 0 ? 0 : (int)data.fields.Max(field => field.Order) + 1;
        sb.AppendLine($"            int size = {1 + slotCount - data.fields.Count};");
        sb.AppendLine("            size += LuminPackEvaluator.CalculateVarInt(id);");
        sb.AppendLine();
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
        foreach (var field in data.fields)
        {
            if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
            {
                sb.AppendLine($"            size += Unsafe.SizeOf<{field.TypeName}>() + LuminPackEvaluator.CalculateVarInt(Unsafe.SizeOf<{field.TypeName}>());");
            }
            else
            {
                sb.AppendLine($"            var {field.Name}TempLength = evaluator.Value;");
                sb.AppendLine("            {");
                sb.AppendLine($"                var __luminPackOffsetValue = local.@{field.Name};");
                sb.AppendLine("                evaluator.CalculateOffset(ref __luminPackOffsetValue);");
                sb.AppendLine("            }");
                sb.AppendLine($"            size += LuminPackEvaluator.CalculateVarInt(evaluator.Value - {field.Name}TempLength);");
            }
        }
        sb.AppendLine();
        sb.AppendLine("            evaluator += size;");
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = TypeMetaChecker.BuildFormatterClassName(data);
        string classGlobalName = data.classFullName;
        bool isAllUnmanagedType = LuminPackCodeGenerator.FindAllUnmanagedType(data.fields);
        uint memberCount = data.fields.Count == 0 ? 0 : data.fields.Max(x => x.Order) + 1;

        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for (var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }
            classFullName += ">";
        }

        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();

        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader();");
            //sb.AppendLine("                writer.Advance(1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref Unsafe.AsRef(in value));");
        sb.AppendLine("            var (existsReference, id) = writer.OptionState.GetOrAddReference(value);");
        sb.AppendLine("            if (existsReference)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteObjectReferenceId(ref index, id);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (isAllUnmanagedType)
        {
            sb.AppendLine($"            writer.WriteObjectHeader(ref index, {memberCount});");
            sb.AppendLine($"            writer.Advance(1);");
            for (int order = 0; order < memberCount; order++)
            {
                var field = data.fields.FirstOrDefault(f => f.Order == order);
                if (field is null)
                {
                    sb.AppendLine("            writer.WriteVarInt(0); writer.Advance(LuminPackEvaluator.CalculateVarInt(0));");
                }
                else
                {
                    sb.AppendLine($"            writer.WriteVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<{field.TypeName}>());");
                    //sb.AppendLine($"            writer.Advance(LuminPackEvaluator.CalculateVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<{field.TypeName}>()));");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"            writer.WriteVarInt(id);");
            //sb.AppendLine($"            writer.Advance(LuminPackEvaluator.CalculateVarInt(id));");
            if (data.fields.Count > 0 && data.fields.Count <= MaxParametersPerCall)
            {
                // Write up to 15 fields in one call.
                sb.Append($"            writer.Advance(writer.WriteUnmanaged(");
                for (var i = 0; i < data.fields.Count; i++)
                {
                    if (i == 0)
                        sb.Append($"local.@{data.fields[i].Name}");
                    else
                        sb.Append($", local.@{data.fields[i].Name}");
                }
                sb.Append("));");
            }
            else
            {
                // Split more than 15 fields across multiple calls.
                int fieldCount = data.fields.Count;
                int processed = 0;

                while (processed < fieldCount)
                {
                    int batchSize = Math.Min(MaxParametersPerCall, fieldCount - processed);

                    sb.AppendLine();
                    sb.Append($"            writer.Advance(writer.WriteUnmanaged(");

                    // Append the current field batch.
                    for (int i = 0; i < batchSize; i++)
                    {
                        if (i == 0)
                            sb.Append($"local.@{data.fields[processed + i].Name}");
                        else
                            sb.Append($", local.@{data.fields[processed + i].Name}");
                    }
                    sb.Append("));");

                    processed += batchSize;

                    // Separate non-final batches with a newline.
                    if (processed < fieldCount)
                    {
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine();

        }
        else
        {
            sb.AppendLine("            var writerBuffer = global::LuminPack.Utility.LuminBufferWriterPool.Rent();");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var tempWriter = new LuminPackWriter(writerBuffer, writer.OptionState);");
            sb.AppendLine($"                Span<int> offsets = stackalloc int[{memberCount}];");
            // Process members in order, skipping empty entries.
            for (int order = 0; order < memberCount; order++)
            {
                var field = data.fields.FirstOrDefault(f => f.Order == order);
                if (field is not null)
                {
                    if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
                    {
                        sb.AppendLine($"                tempWriter.Advance(tempWriter.WriteUnmanaged(local.@{field.Name}));");
                    }
                    else
                    {
                        if (field.IsCompress)
                            sb.AppendLine($"                tempWriter.WriteValueWithCompress(local.@{field.Name});");
                        else
                            sb.AppendLine($"                tempWriter.WriteValue(local.@{field.Name});");
                    }

                }
                else
                {
                    // Record the current position for empty fields without writing payload bytes.
                    sb.AppendLine("                // Empty field placeholder");
                }
                sb.AppendLine($"                offsets[{order}] = tempWriter.GetCurrentSpanIndex();");
            }
            //sb.AppendLine($"                tempWriter.Flush();");
            sb.AppendLine();
            sb.AppendLine($"                writer.WriteObjectHeader(ref index, {memberCount});");
            sb.AppendLine($"                writer.Advance(1);");
            sb.AppendLine($"                for (int i = 0; i < {memberCount}; i++)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    int delta;");
            sb.AppendLine($"                    if (i is 0)");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        delta = offsets[i];");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    else");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        delta = offsets[i] - offsets[i - 1];");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    writer.WriteVarInt(delta);");
            //sb.AppendLine($"                    writer.Advance(LuminPackEvaluator.CalculateVarInt(delta));");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                writer.WriteVarInt(id);");
            //sb.AppendLine($"                writer.Advance(LuminPackEvaluator.CalculateVarInt(id));");
            sb.AppendLine("                writerBuffer.CompleteWrite(tempWriter.GetCurrentSpanIndex());");
            sb.AppendLine($"                writerBuffer.WriteToAndReset(ref writer);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                global::LuminPack.Utility.LuminBufferWriterPool.Return(writerBuffer);");
            sb.AppendLine("            }");
        }

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = TypeMetaChecker.BuildFormatterClassName(data);
        string classGlobalName = data.classFullName;
        bool isAllUnmanagedType = LuminPackCodeGenerator.FindAllUnmanagedType(data.fields);
        uint memberCount = data.fields.Count == 0 ? 0 : data.fields.Max(x => x.Order) + 1;

        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for (var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }
            classFullName += ">";
        }

        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();

        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");

        // Declare locals for every field.
        sb.AppendLine("            // Declare locals to hold the field values.");
        foreach (var field in data.localFields)
        {
            var nullable = field.IsValue ? "" : "?";
            sb.AppendLine($"            {field.TypeName}{nullable} {field.Name}Temp = default!;");
        }
        sb.AppendLine();

        sb.AppendLine("            if (!reader.TryReadObjectHead(ref offset, out var count))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            uint id;");
        sb.AppendLine("            if (count is LuminPackCode.ReferenceId)");
        sb.AppendLine("            {");
        sb.AppendLine("                id = reader.ReadVarIntUInt32();");
        sb.AppendLine($"                value = ({classGlobalName})reader.OptionState.GetObjectReference(id);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");

        sb.AppendLine("            Span<int> deltas = stackalloc int[count];");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                deltas[i] = reader.ReadVarIntInt32();");
        sb.AppendLine("            }");
        sb.AppendLine("            id = reader.ReadVarIntUInt32();");
        sb.AppendLine();

        // Collect constructor arguments using default values.
        var constructorParams = new List<string>();
        if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
        {
            foreach (var param in data.SelectedConstructor.Parameters)
            {
                // Use a default value instead of reading the field.
                constructorParams.Add("default!");
            }
        }

        string constructorArgs = string.Join(", ", constructorParams);

        // Create the object with default argument values.
        if (!data.isValueType)
        {
            sb.AppendLine("            bool reuseObject = value is not null && !reader.OptionState.IsObjectReferenceRegistered(value);");
            sb.AppendLine("            if (!reuseObject)");
            sb.AppendLine("            {");
        }
        if (data.RentPoolMethod == null)
        {
            if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
            {
                sb.AppendLine($"            value = new {classGlobalName}({constructorArgs});");
            }
            else
            {
                sb.AppendLine($"            value = new {classGlobalName}();");
            }
        }
        else
        {
            // Obtain the instance from the object pool.
            if (data.RentPoolMethod.ReturnsByRef)
                sb.AppendLine($"            value = ref {classGlobalName}.{data.RentPoolMethod.Name}()!;");
            else
            {
                sb.AppendLine($"            value = {classGlobalName}.{data.RentPoolMethod.Name}();");
            }
        }


        if (!data.isValueType) sb.AppendLine("            }");
        // Register the object reference before reading nested members.
        sb.AppendLine($"            reader.OptionState.AddObjectReference(id, value!);");
        sb.AppendLine();
        if (!data.isValueType)
        {
            sb.AppendLine("            if (reuseObject)");
            sb.AppendLine("            {");
            sb.AppendLine($"            ref var reuseLocal = ref LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
            foreach (var field in data.fields)
                sb.AppendLine($"            {field.Name}Temp = reuseLocal.{field.Identifier};");
            sb.AppendLine("            }");
        }

        // Deserialize into local variables.
        for (var order = 0; order < memberCount; order++)
        {
            var field = data.fields.FirstOrDefault(f => f.Order == order);

            if (field is not null)
            {
                sb.AppendLine($"            if ({order} < count && deltas[{order}] != 0)");
                sb.AppendLine("            {");
                if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
                {
                    sb.AppendLine($"                reader.EnsureReadable(reader.Position, Unsafe.SizeOf<{field.TypeName}>());");
                    sb.AppendLine($"                reader.Advance(reader.ReadUnmanaged(out {field.Name}Temp));");
                }
                else
                {
                    LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.AppendReusableElementRead(sb, field.TypeSymbol,
                        field.Name + "Temp", "                ", field.IsCompress);
                }
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                sb.AppendLine($"                {field.Name}Temp = default!;");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            if ({order} < count) reader.Advance(deltas[{order}]);");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"            for (int unknownOrder = {memberCount}; unknownOrder < count; unknownOrder++)");
        sb.AppendLine("                reader.Advance(deltas[unknownOrder]);");
        sb.AppendLine("            // Assign the field value.");

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

        if (data.RentPoolMethod != null)
        {
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
                sb.AppendLine($"            local.{field.Identifier} = {field.Name}Temp!;");
            }
        }
        else
        {
            // Assign public fields through an object initializer.
            if (initializerFields.Count > 0)
            {
                sb.AppendLine("            // Assign public fields.");
                foreach (var field in initializerFields)
                {
                    sb.AppendLine($"            value.{field.Identifier} = {field.Name}Temp!;");
                }
                sb.AppendLine();
            }

            // Assign private fields through the Local mirror.
            if (privateFields.Count > 0)
            {
                sb.AppendLine("            // Assign private fields.");
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
                    sb.AppendLine($"            local.{field.Identifier} = {field.Name}Temp!;");
                }
            }
        }
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
    }
}
