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
        return data.isValueType && data.TypeSymbol?.IsUnmanagedType == true;
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
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryGenerateFixedUnmanagedWrite(StringBuilder sb, List<LuminDataField> fields,
        int first, int last, Func<LuminDataField, string> fieldPath, string offset, string indent,
        bool advanceWriter, bool writeHeader = false)
    {
        // Direct field loads let the JIT avoid keeping one byref per argument alive.
        // Keep getters and arbitrary unmanaged structs on the existing in-parameter path.
        for (int i = first; i <= last; i++)
        {
            if (!IsFixedLengthType(fields[i].Type) || fields[i].IsPrivate || fields[i].isProperty)
            {
                return false;
            }
        }

        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    ref byte __luminFixedBlock = ref writer.GetSpanReference(" + offset + ");");
        int size = 0;
        if (writeHeader)
        {
            sb.AppendLine(indent + "    __luminFixedBlock = (byte)" + fields.Count + ";");
            size = 1;
        }
        for (int i = first; i <= last; i++)
        {
            string destination = size == 0 ? "__luminFixedBlock" : "Unsafe.Add(ref __luminFixedBlock, " + size + ")";
            sb.AppendLine(indent + "    Unsafe.WriteUnaligned(ref " + destination + ", " + fieldPath(fields[i]) + ");");
            size += int.Parse(GetFixedFieldLength(fields[i]));
        }
        sb.AppendLine(indent + (advanceWriter ? "    writer.Advance(" + size + ");" : "    " + offset + " += " + size + ";"));
        sb.AppendLine(indent + "}");
        return true;
    }

    /// <summary>Attempts to generate fixed unmanaged read and reports whether it succeeded.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="fields">The fields used by this operation.</param>
    /// <param name="first">The first used by this operation.</param>
    /// <param name="last">The last used by this operation.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryGenerateFixedUnmanagedRead(StringBuilder sb, List<LuminDataField> fields,
        int first, int last, Func<LuminDataField, string> fieldPath, string offset, string indent)
    {
        int length = 0;
        for (int i = first; i <= last; i++)
        {
            if (!IsFixedLengthType(fields[i].Type) || fields[i].IsPrivate || fields[i].isProperty || fields[i].IsCompress)
                return false;
            length += int.Parse(GetFixedFieldLength(fields[i]));
        }
        // Load the source once and assign fields directly instead of preparing
        // simultaneous out references. Constructors and private/property paths
        // retain their existing local-value and commit rules.
        sb.AppendLine(indent + "{");
        sb.AppendLine(indent + "    reader.EnsureReadable(" + offset + ", " + length + ");");
        sb.AppendLine(indent + "    ref byte __luminReadBlock = ref reader.GetSpanReference(" + offset + ");");
        int position = 0;
        for (int i = first; i <= last; i++)
        {
            string source = position == 0 ? "__luminReadBlock" : "Unsafe.Add(ref __luminReadBlock, " + position + ")";
            sb.AppendLine(indent + "    " + fieldPath(fields[i]) + " = Unsafe.ReadUnaligned<" + GetGeneratedTypeName(fields[i]) + ">(ref " + source + ");");
            position += int.Parse(GetFixedFieldLength(fields[i]));
        }
        sb.AppendLine(indent + "    " + offset + " += " + length + ";");
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
