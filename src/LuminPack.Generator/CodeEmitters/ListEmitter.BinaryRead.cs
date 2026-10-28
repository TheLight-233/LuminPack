using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

public static partial class ListEmitter

{

    /// <summary>Appends binary reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: false);
    }

    /// <summary>Appends binary reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        GenerateFreshDeserializeCode(fieldData, sb, useArrayDataIntrinsic: false);
    }

    /// <summary>Appends fresh-list reading statements using only APIs available on the selected target.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="useArrayDataIntrinsic">Whether the target exposes the intrinsic array data reference API.</param>
    public static void GenerateFreshDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb, bool useArrayDataIntrinsic)
    {
        GenerateDeserializeCodeCore(fieldData, sb, freshValue: true, useArrayDataIntrinsic);
    }

    /// <summary>Appends binary reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="freshValue">The fresh value used by this operation.</param>
    /// <param name="useArrayDataIntrinsic">Whether the target exposes the intrinsic array data reference API.</param>
    private static void GenerateDeserializeCodeCore(
        LuminLocalFieldData fieldData, StringBuilder sb, bool freshValue, bool useArrayDataIntrinsic = false)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), list: true, write: false, freshList: freshValue)) return;
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        if (useUnmanagedFastPath)
        {
            sb.AppendLine("            int length = reader.ReadLength(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + elementType + ">());");
            sb.AppendLine("            if (length < 0) { value = null; return; }");
            if (freshValue)
            {
                sb.AppendLine("            var items = reader.CreateFreshListArray<" + elementType + ">(length, out value);");
                EmitterTypeTraits.AppendValidatedRawRead(sb, elementType, "items.AsSpan()", "length");
            }
            else
            {
                sb.AppendLine("            value ??= new global::System.Collections.Generic.List<" + elementType + ">(length);");
                EmitterTypeTraits.AppendValidatedRawRead(sb, elementType, "LuminPackMarshal.GetListSpan(value, length)", "length");
            }
            return;
        }
        sb.AppendLine("            int length = reader.ReadLength(1);");
        sb.AppendLine("            if (length < 0) { value = null; return; }");
        if (freshValue)
        {
            sb.AppendLine("            var items = reader.CreateFreshListArray<" + elementType + ">(length, out value);");
            sb.AppendLine("            var span = items.AsSpan();");
        }
        else
        {
            sb.AppendLine("            value ??= new global::System.Collections.Generic.List<" + elementType + ">(length);");
            sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(value, length);");
        }
        if (!elementClassificationKnown)
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + elementType + ">())");
            sb.AppendLine("            { reader.Read<" + elementType + ">(span); return; }");
        }
        if (freshValue)
        {
            sb.AppendLine("            ref var first = ref " + (useArrayDataIntrinsic ? "global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items)" : "LuminPackMarshal.DangerousGetArrayDataReference(items)") + ";");
            sb.AppendLine("            for (int i = 0; i < length; i++) reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        }
        else
        {
            sb.AppendLine("            for (int i = 0; i < length; i++)");
            sb.AppendLine("            {");
            EmitterTypeTraits.AppendReusableElementRead(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), "span[i]", "                ");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Appends binary reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        if (PortableStructEmitter.TryAppendCollection(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), list: true, write: false)) return;
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                value = new global::System.Collections.Generic.List<{elementType}>(length);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
        sb.AppendLine();

        if (useUnmanagedFastPath)
        {
            if (elementType.Contains("?"))
            {
                sb.AppendLine("            reader.Advance(4);");
                sb.AppendLine("            reader.DangerousReadUnmanagedSpan(ref index, ref span, length, out var spanOffset);");
                sb.AppendLine("            reader.Advance(spanOffset);");
                return;
            }

            // The index points to the length header; DangerousRead consumes it before reading compressed elements.

            sb.AppendLine("            reader.DangerousReadUnmanagedSpanWithCompress(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            LuminPackMarshal.SetListSize(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementClassificationKnown && !elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            // Keep the index at the length header expected by DangerousRead.
            sb.AppendLine("                reader.DangerousReadUnmanagedSpanWithCompress(ref index, ref span, out var spanOffset);");
            sb.AppendLine("                LuminPackMarshal.SetListSize(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else if (!elementClassificationKnown)
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                reader.DangerousReadUnmanagedSpan(ref index, ref span, length, out var spanOffset);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // Reference path only: now advance past the count header

        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();

        // Reference elements: propagate WithCompress only when leaf element is a KnownValueType.
        bool compressedElement = KnownValueTypes.Contains(elementType) || IsLeafTypeKnownValueType(elementType);
        sb.AppendLine("            if (span.IsEmpty)");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
        sb.AppendLine("            for (nint i = 0; i < span.Length; i++)");
        sb.AppendLine("            {");
        EmitterTypeTraits.AppendReusableElementRead(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData),
            "global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)", "                ", compressedElement);
        sb.AppendLine("            }");
    }
}
