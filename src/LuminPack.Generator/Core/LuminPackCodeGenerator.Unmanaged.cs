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

    /// <summary>Uses actual field addresses to copy an adjacent storage group, with a scalar fallback.</summary>
    /// <param name="builder">The current complete operation.</param>
    /// <param name="fields">The ordered wire fields.</param>
    /// <param name="first">The first selected fixed field.</param>
    /// <param name="last">The last selected fixed field.</param>
    /// <param name="storage">The original field storage expression.</param>
    /// <param name="indent">The statement indentation.</param>
    /// <param name="write">Whether the storage is the source rather than the destination.</param>
    /// <param name="allowBulkCopy">Whether an address-proven constructed-storage copy belongs to this operation.</param>
    /// <returns>Whether the complete group was emitted.</returns>
    private static bool TryGenerateCursorFixedGroup(StringBuilder builder, List<LuminDataField> fields,
        int first, int last, Func<LuminDataField, string> storage, string indent, bool write, bool allowBulkCopy = true)
    {
        if (last <= first) return false;
        for (int index = first; index <= last; index++)
            if (fields[index].IsVolatile || fields[index].IsCompress ||
                !IsMergeableField(fields[index])) return false;
        if (!allowBulkCopy && !write)
        {
            string readWidth = GetUnmanagedFieldSize(fields[first]);
            for (int index = first + 1; index <= last; index++)
                readWidth = AddUnmanagedFieldSize(readWidth, GetUnmanagedFieldSize(fields[index]));
            builder.AppendLine(indent + "{");
            builder.AppendLine(indent + "    ref byte fixedCursor = ref reader.GetCurrentSpanReference();");
            // The checked Advance validates the complete group before the loads read the captured position.
            builder.AppendLine(indent + "    reader.Advance(" + readWidth + ");");
            string readOffset = "0";
            for (int index = first; index <= last; index++)
            {
                string location = readOffset == "0" ? "fixedCursor" : "Unsafe.Add(ref fixedCursor, " + readOffset + ")";
                builder.AppendLine(indent + "    " + FixedRead(location, storage(fields[index]), GetGeneratedTypeName(fields[index])));
                readOffset = AddUnmanagedFieldSize(readOffset, GetUnmanagedFieldSize(fields[index]));
            }
            builder.AppendLine(indent + "}");
            return true;
        }
        string Reference(LuminDataField field) => write ? "Unsafe.AsRef(in " + storage(field) + ")" : storage(field);
        builder.AppendLine(indent + "{");
        builder.AppendLine(indent + "    ref byte fixedStorage = ref Unsafe.As<" + GetGeneratedTypeName(fields[first]) + ", byte>(ref " + Reference(fields[first]) + ");");
        var proofs = new List<string> { "Unsafe.SizeOf<global::System.IntPtr>() == 8" };
        string length = GetUnmanagedFieldSize(fields[first]);
        for (int index = first + 1; index <= last; index++)
        {
            proofs.Add("Unsafe.ByteOffset(ref fixedStorage, ref Unsafe.As<" + GetGeneratedTypeName(fields[index]) + ", byte>(ref " + Reference(fields[index]) + ")) == (nint)(" + length + ")");
            length = AddUnmanagedFieldSize(length, GetUnmanagedFieldSize(fields[index]));
        }
        builder.AppendLine(indent + "    if (" + string.Join(" && ", proofs) + ")");
        string state = write ? "writer" : "reader", operation = write ? "Write" : "Read";
        builder.AppendLine(indent + "        " + state + "." + operation + "(ref fixedStorage, " + length + ");");
        builder.AppendLine(indent + "    else");
        builder.AppendLine(indent + "    {");
        builder.AppendLine(indent + "        ref byte fixedCursor = ref " + state + ".GetCurrentSpanReference();");
        // A read advances first: the checked Advance validates the group before the loads read the captured position.
        if (!write)
            builder.AppendLine(indent + "        reader.Advance(" + length + ");");
        string offset = "0";
        for (int index = first; index <= last; index++)
        {
            string location = offset == "0" ? "fixedCursor" : "Unsafe.Add(ref fixedCursor, " + offset + ")";
            builder.AppendLine(indent + "        " + (write
                ? "global::LuminPack.Core.LuminPackWriter.Write(ref " + location + ", in " + storage(fields[index]) + ");"
                : FixedRead(location, storage(fields[index]), GetGeneratedTypeName(fields[index]))));
            offset = AddUnmanagedFieldSize(offset, GetUnmanagedFieldSize(fields[index]));
        }
        if (write)
            builder.AppendLine(indent + "        writer.Advance(" + length + ");");
        builder.AppendLine(indent + "    }");
        builder.AppendLine(indent + "}");
        return true;
    }

    /// <summary>Spells one fixed read with the architecture gate folded by the JIT, independent of helper inlining.</summary>
    /// <param name="location">The fixed input byte reference expression.</param>
    /// <param name="storage">The writable destination storage.</param>
    /// <param name="type">The destination storage type.</param>
    /// <returns>A statement that uses typed unaligned access on 64-bit and for values of at most four bytes, and the
    /// block copy for wider values on 32-bit.</returns>
    /// <remarks>Large generated roots can exhaust the JIT's inlining limits; these intrinsics need no inlining.</remarks>
    private static string FixedRead(string location, string storage, string type)
        => "if (Unsafe.SizeOf<global::System.IntPtr>() == 8 || Unsafe.SizeOf<" + type + ">() <= 4) " + storage + " = Unsafe.ReadUnaligned<" + type + ">(ref " + location + "); " +
           "else global::LuminPack.Code.LuminPackMarshal.CopyBlockUnaligned(ref Unsafe.As<" + type + ", byte>(ref " + storage + "), ref " + location + ", (uint)Unsafe.SizeOf<" + type + ">());";

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
