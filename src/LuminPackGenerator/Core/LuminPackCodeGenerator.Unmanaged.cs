using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Emits binary field reads, writes, and size evaluation from analyzed serialization contracts.</summary>
public static partial class LuminPackCodeGenerator
{

    /// <summary>Determines whether the supplied value has the pure value type struct classification.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsPureValueTypeStruct(LuminDataField field)
    {
        if (field.TypeSymbol?.IsUnmanagedType == true && !EmitterTypeTraits.CanUseUnmanagedSerialization(field.TypeSymbol)) return false;
        if (field.Type != LuminFiledType.Struct)
        {
            return false;
        }
        if (field.ClassFields.Count == 0)
        {
            return false;
        }
        foreach (LuminDataField classField in field.ClassFields)
        {
            if (classField.Type == LuminFiledType.Other)
            {
                return false;
            }
            if (classField.FieldType == LuminDataType.Reference)
            {
                return false;
            }
            if (classField.Type == LuminFiledType.Struct)
            {
                if (!IsPureValueTypeStruct(classField))
                {
                    return false;
                }
            }
            else if (classField.Type == LuminFiledType.Array || classField.Type == LuminFiledType.List || classField.Type == LuminFiledType.String)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Determines whether the supplied value has the pure unmanaged struct classification.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsPureUnmanagedStruct(LuminDataInfo data)
    {
        return data.isValueType && EmitterTypeTraits.CanUseUnmanagedSerialization(data.TypeSymbol);
    }

    /// <summary>Determines whether the supplied value has the mergeable field classification.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsMergeableField(LuminDataField field)
    {
        // Unity's C# compiler does not accept Nullable<T> as an argument for
        // `where T : unmanaged`, even when newer Roslyn versions report the
        // closed nullable type as unmanaged. Keep nullable values on their
        // dedicated formatter path instead of folding them into a constrained
        // multi-value WriteUnmanaged/ReadUnmanaged call.
        if (field.TypeSymbol is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            })
        {
            return false;
        }

        if (EmitterTypeTraits.TryGetIsUnmanaged(field.TypeSymbol, out bool isUnmanaged))
        {
            return isUnmanaged;
        }

        if (!IsUnmanagedFiledType(field.Type) && (field.Type != LuminFiledType.Struct || !IsPureValueTypeStruct(field)) && !CodeEmitterRegistry.KnownValueTypes.Contains(field.TypeName))
        {
            return CodeEmitterRegistry.KnownValueTypes.Contains("global::" + field.TypeName);
        }
        return true;
    }

    /// <summary>Attempts to generate fixed unmanaged write and reports whether it succeeded.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="fields">The fields used by this operation.</param>
    /// <param name="first">The first used by this operation.</param>
    /// <param name="last">The last used by this operation.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <param name="advanceWriter">The advance writer used by this operation.</param>
    /// <param name="writeHeader">The write header used by this operation.</param>
    /// <param name="separateHeaderOn32Bit">Whether the original 32-bit path writes its header outside the packed field view.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryGenerateFixedUnmanagedWrite(StringBuilder sb, List<LuminDataField> fields,
        int first, int last, Func<LuminDataField, string> fieldPath, string offset, string indent,
        bool advanceWriter, bool writeHeader = false, bool separateHeaderOn32Bit = false)
    {
        // Direct field loads let the JIT avoid keeping one byref per argument alive.
        // Keep getters and private storage on the existing in-parameter path.
        bool containsStruct = false;
        bool containsVolatile = false;
        for (int i = first; i <= last; i++)
        {
            if (fields[i].IsPrivate || fields[i].isProperty ||
                (!IsFixedLengthType(fields[i].Type) && !EmitterTypeTraits.CanUseUnmanagedSerialization(fields[i].TypeSymbol)))
            {
                return false;
            }
            containsStruct |= !IsFixedLengthType(fields[i].Type);
            containsVolatile |= fields[i].IsVolatile;
        }

        sb.AppendLine(indent + "{");
        string bodyIndent = indent + "    ";
        // Keep typed access and proven bulk copies on 64-bit; IL2CPP needs packed block copies on 32-bit.
        sb.AppendLine(indent + "    if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        sb.AppendLine(indent + "    {");
        sb.AppendLine(bodyIndent + "    ref byte __luminFixedBlock = ref writer.GetSpanReference(" + offset + ");");
        string size = "0";
        if (writeHeader)
        {
            sb.AppendLine(bodyIndent + "    __luminFixedBlock = (byte)" + fields.Count + ";");
            size = "1";
        }
        bool copyAdjacentFields = last > first && !containsVolatile;
        if (copyAdjacentFields)
        {
            sb.AppendLine(bodyIndent + "    ref byte __luminSourceBlock = ref Unsafe.As<" + GetGeneratedTypeName(fields[first]) + ", byte>(ref Unsafe.AsRef(in " + fieldPath(fields[first]) + "));");
            var adjacent = new List<string>();
            string sourceOffset = GetUnmanagedFieldSize(fields[first]);
            for (int i = first + 1; i <= last; i++)
            {
                adjacent.Add("Unsafe.ByteOffset(ref __luminSourceBlock, ref Unsafe.As<" + GetGeneratedTypeName(fields[i]) +
                    ", byte>(ref Unsafe.AsRef(in " + fieldPath(fields[i]) + "))) == (nint)(" + sourceOffset + ")");
                sourceOffset = AddUnmanagedFieldSize(sourceOffset, GetUnmanagedFieldSize(fields[i]));
            }
            sb.AppendLine(bodyIndent + "    if (" + string.Join(" && ", adjacent) + ")");
            sb.AppendLine(bodyIndent + "    {");
            string destination = writeHeader ? "Unsafe.Add(ref __luminFixedBlock, 1)" : "__luminFixedBlock";
            sb.AppendLine(bodyIndent + "        Unsafe.CopyBlockUnaligned(ref " + destination + ", ref __luminSourceBlock, (uint)(" + sourceOffset + "));");
            sb.AppendLine(bodyIndent + "    }");
            sb.AppendLine(bodyIndent + "    else");
            sb.AppendLine(bodyIndent + "    {");
        }
        for (int i = first; i <= last; i++)
        {
            string destination = size == "0" ? "__luminFixedBlock" : "Unsafe.Add(ref __luminFixedBlock, " + size + ")";
            sb.AppendLine(bodyIndent + (copyAdjacentFields ? "        " : "    ") + "Unsafe.WriteUnaligned(ref " + destination + ", " + fieldPath(fields[i]) + ");");
            string fieldSize = GetUnmanagedFieldSize(fields[i]);
            size = AddUnmanagedFieldSize(size, fieldSize);
        }
        if (copyAdjacentFields) sb.AppendLine(bodyIndent + "    }");
        sb.AppendLine(bodyIndent + (advanceWriter ? "    writer.Advance(" + size + ");" : "    " + offset + " += " + size + ";"));
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    else");
        sb.AppendLine(indent + "    {");
        if (!containsStruct)
        {
            // Primitive batches are tightly concatenated on the wire. A
            // naturally aligned aggregate would introduce padding on x86.
            sb.AppendLine(indent + "        int __lumin32Offset = " + offset + ";");
            if (writeHeader)
            {
                sb.AppendLine(indent + "        writer.WriteObjectHeader(ref __lumin32Offset, " + fields.Count + ");");
                sb.AppendLine(indent + "        __lumin32Offset += 1;");
            }
            for (int i = first; i <= last; i++)
                sb.AppendLine(indent + "        __lumin32Offset += writer.WriteUnmanaged(ref __lumin32Offset, " + fieldPath(fields[i]) + ");");
            sb.AppendLine(indent + (advanceWriter ? "        writer.Advance(" + size + ");" : "        " + offset + " = __lumin32Offset;"));
        }
        else
        {
            string arguments = string.Join(", ", fields.GetRange(first, last - first + 1).ConvertAll(field => fieldPath(field)));
            if (writeHeader && separateHeaderOn32Bit)
            {
                sb.AppendLine(indent + "        writer.WriteObjectHeader(ref " + offset + ", " + fields.Count + ");");
                sb.AppendLine(indent + "        " + offset + " += 1;");
            }
            else if (writeHeader) arguments = "(byte)" + fields.Count + ", " + arguments;
            sb.AppendLine(indent + (advanceWriter
                ? "        writer.Advance(writer.WriteUnmanaged(" + arguments + "));"
                : "        " + offset + " += writer.WriteUnmanaged(ref " + offset + ", " + arguments + ");"));
        }
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "}");
        return true;
    }

    /// <summary>Builds a constant or JIT-specializable byte width without inferring a struct's layout.</summary>
    /// <param name="field">An already validated raw unmanaged field.</param>
    /// <returns>The field's complete storage width, including its own padding.</returns>
    private static string GetUnmanagedFieldSize(LuminDataField field) => IsFixedLengthType(field.Type)
        ? GetFixedFieldLength(field) : "Unsafe.SizeOf<" + GetGeneratedTypeName(field) + ">()";

    /// <summary>Folds known byte widths while retaining every runtime-sized storage term in its original order.</summary>
    /// <param name="offset">The preceding fixed fields' accumulated width.</param>
    /// <param name="size">The next field's complete storage width.</param>
    /// <returns>A constant sum or the unchanged ordered runtime expression.</returns>
    private static string AddUnmanagedFieldSize(string offset, string size)
    {
        if (offset == "0") return size;
        if (int.TryParse(offset, out int total) && int.TryParse(size, out int length) &&
            total >= 0 && length >= 0 && total <= int.MaxValue - length)
            return (total + length).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return offset + " + " + size;
    }

    /// <summary>Emits fixed reads with proven storage copies where the operation context permits them.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="fields">The fields used by this operation.</param>
    /// <param name="first">The first used by this operation.</param>
    /// <param name="last">The last used by this operation.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <param name="copyToStorage">Whether this operation permits copies into its constructed public field storage.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryGenerateFixedUnmanagedRead(StringBuilder sb, List<LuminDataField> fields,
        int first, int last, Func<LuminDataField, string> fieldPath, string offset, string indent, bool copyToStorage)
    {
        bool containsStruct = false;
        bool containsVolatile = false;
        for (int i = first; i <= last; i++)
        {
            if (fields[i].IsPrivate || fields[i].isProperty || fields[i].IsCompress ||
                (!IsFixedLengthType(fields[i].Type) &&
                 (!copyToStorage || !EmitterTypeTraits.CanUseUnmanagedSerialization(fields[i].TypeSymbol))))
                return false;
            containsStruct |= !IsFixedLengthType(fields[i].Type);
            containsVolatile |= fields[i].IsVolatile;
        }
        // Constructor and pool temporaries retain their established grouped read.
        // Taking their addresses would inhibit promotion and change commit behavior.
        string length = "0";
        for (int i = first; i <= last; i++) length = AddUnmanagedFieldSize(length, GetUnmanagedFieldSize(fields[i]));
        bool copyAdjacentFields = copyToStorage && last > first && !containsVolatile;
        string arguments = string.Join(", ", fields.GetRange(first, last - first + 1).ConvertAll(field => "out " + fieldPath(field)));
        sb.AppendLine(indent + "{");
        string bodyIndent = indent + "    ";
        sb.AppendLine(indent + "    if (global::System.Runtime.CompilerServices.Unsafe.SizeOf<global::System.IntPtr>() == 8)");
        sb.AppendLine(indent + "    {");
        sb.AppendLine(bodyIndent + "    reader.EnsureReadable(" + offset + ", " + length + ");");
        sb.AppendLine(bodyIndent + "    ref byte __luminReadBlock = ref reader.GetSpanReference(" + offset + ");");
        if (copyAdjacentFields)
        {
            sb.AppendLine(bodyIndent + "    ref byte __luminTargetBlock = ref Unsafe.As<" + GetGeneratedTypeName(fields[first]) + ", byte>(ref " + fieldPath(fields[first]) + ");");
            var adjacent = new List<string>();
            string targetOffset = GetUnmanagedFieldSize(fields[first]);
            for (int i = first + 1; i <= last; i++)
            {
                adjacent.Add("Unsafe.ByteOffset(ref __luminTargetBlock, ref Unsafe.As<" + GetGeneratedTypeName(fields[i]) +
                    ", byte>(ref " + fieldPath(fields[i]) + ")) == (nint)(" + targetOffset + ")");
                targetOffset = AddUnmanagedFieldSize(targetOffset, GetUnmanagedFieldSize(fields[i]));
            }
            sb.AppendLine(bodyIndent + "    if (" + string.Join(" && ", adjacent) + ")");
            sb.AppendLine(bodyIndent + "    {");
            sb.AppendLine(bodyIndent + "        Unsafe.CopyBlockUnaligned(ref __luminTargetBlock, ref __luminReadBlock, (uint)(" + length + "));");
            sb.AppendLine(bodyIndent + "    }");
            sb.AppendLine(bodyIndent + "    else");
            sb.AppendLine(bodyIndent + "    {");
        }
        if (containsStruct)
            sb.AppendLine(bodyIndent + (copyAdjacentFields ? "        " : "    ") + "reader.ReadUnmanaged(ref " + offset + ", " + arguments + ");");
        else
        {
            string position = "0";
            for (int i = first; i <= last; i++)
            {
                string source = position == "0" ? "__luminReadBlock" : "Unsafe.Add(ref __luminReadBlock, " + position + ")";
                sb.AppendLine(bodyIndent + (copyAdjacentFields ? "        " : "    ") + fieldPath(fields[i]) + " = Unsafe.ReadUnaligned<" + GetGeneratedTypeName(fields[i]) + ">(ref " + source + ");");
                position = AddUnmanagedFieldSize(position, GetUnmanagedFieldSize(fields[i]));
            }
        }
        if (copyAdjacentFields) sb.AppendLine(bodyIndent + "    }");
        sb.AppendLine(bodyIndent + "    " + offset + " += " + length + ";");
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "    else");
        sb.AppendLine(indent + "    {");
        if (containsStruct)
            sb.AppendLine(indent + "        " + offset + " += reader.ReadUnmanaged(ref " + offset + ", " + arguments + ");");
        else
            for (int i = first; i <= last; i++)
                sb.AppendLine(indent + "        " + offset + " += reader.ReadUnmanaged(ref " + offset + ", out " + fieldPath(fields[i]) + ");");
        sb.AppendLine(indent + "    }");
        sb.AppendLine(indent + "}");
        return true;
    }

    /// <summary>Finds the next mergeable field in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindNextMergeableField(LuminDataInfo data, int index)
    {
        for (int i = index + 1; i < data.fields.Count && IsMergeableField(data.fields[i]); i++)
        {
            index++;
        }
        return index;
    }

    /// <summary>Finds the next mergeable field in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindNextMergeableField(List<LuminDataField> data, int index)
    {
        for (int i = index + 1; i < data.Count && IsMergeableField(data[i]); i++)
        {
            index++;
        }
        return index;
    }

    /// <summary>Finds the next unmanaged type in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindNextUnmanagedType(LuminDataInfo data, int index)
    {
        for (int i = index + 1; i < data.fields.Count && IsUnmanagedFiledType(data.fields[i].Type); i++)
        {
            index++;
        }
        return index;
    }

    /// <summary>Finds the next unmanaged type in the supplied metadata.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="index">The position at which the operation starts.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindNextUnmanagedType(List<LuminDataField> data, int index)
    {
        for (int i = index + 1; i < data.Count && IsUnmanagedFiledType(data[i].Type); i++)
        {
            index++;
        }
        return index;
    }

    /// <summary>Finds the all unmanaged type in the supplied metadata.</summary>
    /// <param name="datas">The datas used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FindAllUnmanagedType(List<LuminDataField> datas)
    {
        bool isUnmanaged;
        foreach (LuminDataField data in datas)
        {
            LuminFiledType type = data.Type;
            isUnmanaged = ((type == LuminFiledType.String || type - 16 <= LuminFiledType.SByte) ? true : false);
            if (isUnmanaged)
            {
                isUnmanaged = false;
            }
            else
            {
                if (data.Type != LuminFiledType.Struct || FindAllUnmanagedType(data.ClassFields))
                {
                    continue;
                }
                isUnmanaged = false;
            }
            goto IL_0067;
        }
        return true;
    IL_0067:
        return isUnmanaged;
    }
}
