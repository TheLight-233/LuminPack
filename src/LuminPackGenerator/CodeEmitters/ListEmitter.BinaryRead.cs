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
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (freshValue)
        {
            sb.AppendLine($"            var items = reader.CreateFreshListArray<{elementType}>(length, out value);");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                value = new global::System.Collections.Generic.List<{elementType}>(length);");
            sb.AppendLine("            }");
            sb.AppendLine("            else if (value.Count == length)");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Clear();");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            var span = LuminPackMarshal.GetListSpan(global::System.Runtime.CompilerServices.Unsafe.AsRef(in value), length);");
        }
        if (useUnmanagedFastPath)
        {
            if (freshValue)
                sb.AppendLine("            var span = items.AsSpan();");
            sb.AppendLine("            reader.DangerousReadUnmanagedSpan(ref index, ref span, out var spanOffset);");
            sb.AppendLine("            reader.Advance(spanOffset);");
            return;
        }

        if (!elementClassificationKnown && !elementType.Contains("?"))
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            if (freshValue)
                sb.AppendLine("                var span = items.AsSpan();");
            sb.AppendLine("                reader.DangerousReadUnmanagedSpan(ref index, ref span, out var spanOffset);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else if (!elementClassificationKnown)
        {
            sb.AppendLine($"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{elementType}>())");
            sb.AppendLine("            {");
            if (freshValue)
                sb.AppendLine("                var span = items.AsSpan();");
            sb.AppendLine("                reader.Advance(4);");
            sb.AppendLine("                reader.DangerousReadUnmanagedSpan(ref index, ref span, length, out var spanOffset);");
            sb.AppendLine("                reader.Advance(spanOffset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
        }

        // The unmanaged fast paths consume the collection header themselves and report an
        // offset including that header.  The recursive static path starts after the header.
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        if (freshValue)
        {
            var element = EmitterTypeTraits.GetFirstTypeArgument(fieldData);
            bool knownReference = element?.IsReferenceType == true;
            if (element?.IsValueType != true)
            {
                if (!knownReference)
                {
                    sb.AppendLine($"            if (!typeof({elementType}).IsValueType)");
                    sb.AppendLine("            {");
                }
                string indent = knownReference ? "            " : "                ";
                if (!useArrayDataIntrinsic)
                    sb.AppendLine(indent + "ref var referenceItems = ref LuminPackMarshal.DangerousGetArrayDataReference(items);");
                sb.AppendLine(indent + "for (int i = 0; i < items.Length; i++)");
                sb.AppendLine(indent + "{");
                sb.AppendLine(indent + $"    {elementType} item = default!;");
                sb.AppendLine(indent + "    reader.ReadValue(ref item);");
                string destination = useArrayDataIntrinsic
                    ? "global::System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(items)"
                    : "referenceItems";
                sb.AppendLine(indent + "    global::System.Runtime.CompilerServices.Unsafe.Add(ref " + destination + ", (nint)(uint)i) = item;");
                sb.AppendLine(indent + "}");
                sb.AppendLine(indent + "return;");
                // Concrete reference elements must not expand a second, unreachable
                // copy of their entire formatter graph in the value-type loop.
                if (knownReference) return;
                sb.AppendLine("            }");
                sb.AppendLine();
            }
            sb.AppendLine("            ref var first = ref LuminPackMarshal.DangerousGetArrayDataReference(items);");
            sb.AppendLine("            for (nint i = 0; i < length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            if (span.IsEmpty)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
            sb.AppendLine("            for (nint i = 0; i < span.Length; i++)");
            sb.AppendLine("            {");
            sb.AppendLine("                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
            sb.AppendLine("            }");
        }
    }

    /// <summary>Appends binary reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCodeWithCompress(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        bool elementClassificationKnown = EmitterTypeTraits.TryGetIsUnmanaged(
            EmitterTypeTraits.GetFirstTypeArgument(fieldData), out bool elementIsUnmanaged);
        bool useUnmanagedFastPath = elementClassificationKnown
            ? elementIsUnmanaged
            : KnownValueTypes.Contains(elementType);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
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
        sb.AppendLine("            else if (value.Count == length)");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
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
        string elementCallDeserialize = KnownValueTypes.Contains(elementType) || IsLeafTypeKnownValueType(elementType)
            ? "reader.ReadValueWithCompress(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);"
            : "reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);";
        sb.AppendLine("            if (span.IsEmpty)");
        sb.AppendLine("            {");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            ref var first = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(span);");
        sb.AppendLine("            for (nint i = 0; i < span.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine($"                {elementCallDeserialize}");
        sb.AppendLine("            }");
    }
}
