using System;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders row-major rectangular arrays with their modeled binary storage and JSON shape.</summary>
internal static class RectangularArrayOwnerFile
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Marshal = "global::System.Runtime.InteropServices.MemoryMarshal";
    private const string PackMarshal = "global::LuminPack.Code.LuminPackMarshal";
    private const string Error = "global::LuminPack.Code.LuminPackExceptionHelper";
    private const string NullableRectangle = "global::LuminPack.Code.LuminPackNullableRectangle";
    private const string Profile = "TLuminProfile";

    /// <summary>Renders the four independently selectable operations of a closed rectangular array.</summary>
    /// <param name="set">The shared publication context.</param>
    /// <param name="fact">The closed type, rank, element and optional reuse facts.</param>
    /// <param name="operations">The selected operations.</param>
    /// <returns>The generated owner source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
        => Render(set, fact.DeclaredType, fact.Scalar!, fact.ArrayRank, fact.ReuseArraySlots, operations);

    /// <summary>Renders a rectangular array using its general element and contiguous storage contracts.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The generated C# source.</returns>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations) =>
        Render(set, model.Type, model.Arguments[0].Type, model.Rank, model.ReuseArraySlots, operations, model.Arguments[0]);

    /// <summary>Renders the shared shape envelope with the analyzed element operations.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <param name="item">The current item.</param>
    /// <param name="rank">The rank supplied to <c>Render</c>.</param>
    /// <param name="reuseArraySlots">Whether to enable reuse array slots for this operation.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <param name="element">The collection element or its formatter contract.</param>
    /// <returns>The generated C# source.</returns>
    private static string Render(SharedFormatterSet set, string type, string item, int rank, bool reuseArraySlots,
        GeneratedOperations operations, FormatterModel? element = null)
    {
        FormatterModel? arrayContract = set.FindFormatter(type);
        FormatterModel? elementContract = element ?? arrayContract?.Arguments[0];
        bool text = item == "string";
        bool nullableElement = elementContract?.Definition == GenericIdentity.Nullable || GenericIdentity.NullableScalar(item) != null;
        bool raw = !nullableElement && (element?.RawCollection ?? !text);
        // The rectangular nullable plan owns the wrapper layout. Without it the element formatter owns its marker and payload.
        FormatterStorageModel? layout = nullableElement ? null : element?.Layout;
        string suffix = ReadableTypeName.Of(GeneratedNames.PayloadKey(type));
        string owner = "global::LuminPack." + GeneratedNames.Owner(set.Extension!, item);
        string? nullablePayload = arrayContract?.Supports("WriteFramedNullableSpan") == true
            ? (elementContract ?? set.FindFormatter(item))?.NativeNullablePayload : null;
        FormatterStorageModel? nullableStorage = arrayContract?.RectangularNullableStorage;
        int nullableEncoding = arrayContract?.RectangularNullableEncoding ?? 0;
        string? storageArguments = nullableEncoding != 0 && nullableStorage is { NullablePayload: { } nullableArgument }
            ? "<" + nullableArgument + ", " + FormatterStorageEmitter.Layout(nullableStorage) + ">" : null;
        string encoding = nullableEncoding.ToString(CultureInfo.InvariantCulture);
        string hasTag = nullableEncoding == 1 && storageArguments != null && nullableStorage!.ModelSizes.Length > 1 ? "true" : "false";
        string[] axes = Enumerable.Range(0, rank).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray();
        string[] dimensions = axes.Select(axis => "dimension" + axis).ToArray();
        string array = "new " + ContiguousOwnerFile.WithRank(item, "[" + string.Join(", ", dimensions) + "]");
        string width = Unsafe + ".SizeOf<" + item + ">()";

        return SpecialOwnerFile.Render(set, operations, suffix, type, referenceType: true,
            (w, _) => BinaryWrite(w), (w, _) => BinaryRead(w), (w, _) => JsonWrite(w),
            (w, context) => JsonRead(w, context.JsonReader),
            w =>
            {
                if (storageArguments != null)
                {
                    FormatterStorageEmitter.Support(w, nullableStorage!);
                    if ((operations & GeneratedOperations.BinaryRead) != 0)
                        NullableReadSupport(w);
                }
                else if (layout != null)
                    FormatterStorageEmitter.Support(w, layout);
            },
            outlineJson: true, outlineBinaryWrite: !raw && layout == null,
            outlineBinaryRead: !raw && layout == null);

        void BinaryWrite(CodeWriter w)
        {
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.EnsureAdditionalCapacity(1);");
            w.Line("writer.WriteNullObjectHeader();");
            w.Line("return;");
            w.Close();
            foreach (string axis in axes)
                w.Line("if (value.GetLowerBound(" + axis + ") != 0) " + Error +
                    ".ThrowNotSupportedException(\"Binary multidimensional arrays require zero lower bounds.\");");
            w.Line("writer.EnsureAdditionalCapacity(" + (5 + rank * 4).ToString(CultureInfo.InvariantCulture) + ");");
            w.Line("writer.WriteObjectHeader(0);");
            foreach (string axis in axes)
                w.Line("writer.Write(value.GetLength(" + axis + "));");
            w.Line("writer.WriteCollectionHeader(value.Length);");
            if (storageArguments != null)
            {
                First(w, item);
                w.Line(NullableRectangle + ".Write" + storageArguments + "(ref writer, " + Marshal +
                    ".CreateReadOnlySpan(ref first, value.Length), " + encoding + ", " + hasTag + ");");
                w.Line("writer.CheckBuffer();");
                return;
            }
            if (text)
            {
                First(w, item);
                w.Line("writer.WriteStrings<" + Profile + ">(" + Marshal + ".CreateReadOnlySpan(ref first, value.Length));");
            }
            else if (layout != null)
            {
                w.Line("writer.EnsureAdditionalCapacity(" + FormatterStorageEmitter.Width(layout, "value.Length") + ");");
                First(w, item);
                FormatterStorageEmitter.Payload(w, layout, "first", "value.Length", true);
            }
            else if (raw)
            {
                w.Line("int payloadBytes = checked(value.Length * " + width + ");");
                w.Line("writer.EnsureAdditionalCapacity(payloadBytes);");
                w.Line("writer.Write(ref " + PackMarshal + ".DangerousGetArrayDataReference<" + item + ">(value), payloadBytes);");
            }
            else
            {
                w.Line("int length = value.Length;");
                w.Line("if (length != 0)");
                w.Open();
                First(w, item);
                if (nullablePayload != null)
                {
                    // Rectangular nullable elements carry individual markers. The reserved run keeps those bytes as-is.
                    w.Line("if (!writer.TryWriteFramedNullableSpan<" + nullablePayload + ">(" + Marshal + ".CreateReadOnlySpan(ref first, length)))");
                    w.Open();
                }
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line("writer.CheckBuffer();");
                w.Line(owner + ".Write<" + Profile + ">(ref writer, in " + Unsafe + ".Add(ref first, (nint)(uint)index));");
                w.Close();
                w.Line("while (++index < length);");
                if (nullablePayload != null)
                    w.Close();
                w.Close();
            }
            w.Line("writer.CheckBuffer();");
        }

        void BinaryRead(CodeWriter w)
        {
            const string header = "1 + 3 * sizeof(int)";
            bool boundedReuse = rank == 2 && raw && layout == null && reuseArraySlots && set.Options.Net8 && !set.Options.Unity;
            if (boundedReuse)
            {
                // A matching destination proves the product and the exact payload width. An unavailable
                // complete window or a different shape keeps the original checked read and failure cursor.
                const string binaryReader = "global::LuminPack.Core.LuminPackReader";
                w.Line("#if NET8_0_OR_GREATER");
                w.Line("int copyBytes;");
                w.Line("ref byte payload = ref " + Unsafe + ".NullRef<byte>();");
                w.Line("var target = value;");
                w.Line("if (target is not null && " + Unsafe + ".SizeOf<global::System.IntPtr>() == 8)");
                w.Open();
                w.Line("long recordBytes = " + header + " + (long)target.Length * " + width + ";");
                w.Line("var input = reader.Buffer;");
                w.Line("ref byte record = ref reader.Cursor;");
                w.Line("long position = (long)" + Unsafe + ".ByteOffset(ref " + Marshal + ".GetReference(input), ref record);");
                w.Line("if (recordBytes <= input.Length && (ulong)position <= (ulong)(input.Length - recordBytes) &&");
                w.Line("    record != global::LuminPack.Code.LuminPackCode.NullObject &&");
                w.Line("    " + binaryReader + ".Read<int>(ref " + Unsafe + ".Add(ref record, 1)) == target.GetLength(0) &&");
                w.Line("    " + binaryReader + ".Read<int>(ref " + Unsafe + ".Add(ref record, 1 + sizeof(int))) == target.GetLength(1) &&");
                w.Line("    " + binaryReader + ".Read<int>(ref " + Unsafe + ".Add(ref record, 1 + 2 * sizeof(int))) == target.Length &&");
                w.Line("    (target.GetLowerBound(0) | target.GetLowerBound(1)) == 0)");
                w.Open();
                w.Line("reader.Cursor = ref " + Unsafe + ".Add(ref record, (nint)(uint)recordBytes);");
                w.Line("payload = ref " + Unsafe + ".Add(ref record, " + header + ");");
                w.Line("copyBytes = (int)(recordBytes - (" + header + "));");
                w.Line("goto CopyRawPayload;");
                w.Close();
                w.Close();
                w.Line("#endif");
            }
            w.Line("if (!reader.TryReadObjectHead())");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Line("ref byte dimensions = ref reader.Cursor;");
            w.Line("reader.Advance(" + (rank * 4).ToString(CultureInfo.InvariantCulture) + ");");
            for (int axis = 0; axis < rank; axis++)
            {
                string offset = (axis * 4).ToString(CultureInfo.InvariantCulture);
                w.Line("int " + dimensions[axis] + " = global::LuminPack.Core.LuminPackReader.Read<int>(ref " +
                    Unsafe + ".Add(ref dimensions, " + offset + "));");
            }
            w.Line("if (!reader.TryReadCollectionHead(out var length" + (layout != null || storageArguments != null ? ", 0" : "") + ")) " +
                Error + ".ThrowInvalidCollection();");
            if (rank == 2)
            {
                // Two nonnegative Int32 dimensions fit in an Int64 product, even when either dimension is zero.
                foreach (string dimension in dimensions)
                    w.Line("if (" + dimension + " < 0) " + Error + ".ThrowNegativeMultiDimensionalArrayDimension();");
                w.Line("long expectedLength = (long)" + dimensions[0] + " * " + dimensions[1] + ";");
                w.Line("if (expectedLength != length)");
                w.Open();
                w.Line("if (expectedLength > int.MaxValue) " + Error + ".ThrowMultiDimensionalArrayDimensionsTooLarge();");
                w.Line(Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
                w.Close();
            }
            else
            {
                w.Line("long expectedLength = " + string.Join(" || ", dimensions.Select(dimension => dimension + " == 0")) + " ? 0 : 1;");
                foreach (string dimension in dimensions)
                    AccumulateDimension(w, dimension);
                w.Line("if (expectedLength != length) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
            }
            if (storageArguments != null)
            {
                w.Line("value = LuminRoots_" + suffix + ".ReadNullablePayload(ref reader, length, " +
                    string.Join(", ", dimensions) + ");");
                return;
            }
            if (layout != null)
                FormatterStorageEmitter.Validate(w, layout, "length");
            if (raw)
            {
                w.Line("int payloadBytes = checked(length * " + width + ");");
                w.Line("reader.EnsureCollectionPayload(reader.Position, payloadBytes);");
                if (reuseArraySlots)
                {
                    w.Line("if (value is null || " + string.Join(" || ", axes.Select(axis =>
                        "value.GetLength(" + axis + ") != dimension" + axis + " || value.GetLowerBound(" + axis + ") != 0")) + ")");
                    w.Line("    value = " + array + ";");
                }
                else
                    w.Line("value = " + array + ";");
                if (boundedReuse)
                {
                    w.Line("#if NET8_0_OR_GREATER");
                    w.Line("target = value;");
                    w.Line("payload = ref reader.Cursor;");
                    w.Line("reader.Advance(payloadBytes);");
                    w.Line("copyBytes = payloadBytes;");
                    w.Line("CopyRawPayload:");
                    w.Line(PackMarshal + ".CopyBlockUnaligned(ref " + PackMarshal + ".DangerousGetArrayDataReference<" + item +
                        ">(target), ref payload, (uint)copyBytes);");
                    w.Line("#else");
                }
                w.Line("reader.Read(ref " + PackMarshal + ".DangerousGetArrayDataReference<" + item + ">(value), payloadBytes);");
                if (boundedReuse)
                    w.Line("#endif");
                return;
            }

            if (text && rank == 2 && reuseArraySlots)
            {
                w.Line("if (reader.TryGetStringMatrixSlots<" + Profile + ">(value, dimension0, dimension1, out var slots))");
                w.Open();
                w.Line("reader.ReadStrings<" + Profile + ">(slots);");
                w.Line("return;");
                w.Close();
            }
            if (rank == 2 && reuseArraySlots && element is { OrdinaryObject: true })
            {
                w.Line("if (value is not null && value.GetType() == typeof(" + type + ") &&");
                w.Line("    value.GetLength(0) == dimension0 && value.GetLength(1) == dimension1 &&");
                w.Line("    (value.GetLowerBound(0) | value.GetLowerBound(1)) == 0)");
                w.Open();
                w.Line("if (length == 0) return;");
                w.Line("ref var firstSlot = ref " + Unsafe + ".As<byte, " + item + ">(ref " + PackMarshal +
                    ".DangerousGetArrayDataReference<" + item + ">(value));");
                w.Line("for (int slotIndex = 0; slotIndex < length; slotIndex++)");
                w.Open();
                w.Line("ref var slot = ref " + Unsafe + ".Add(ref firstSlot, (nint)(uint)slotIndex);");
                FormatterElementEmitter.Read(w, set, element, "slot");
                w.Close();
                w.Line("return;");
                w.Close();
                w.Blank();
            }
            w.Line("value = " + array + ";");
            First(w, item);
            if (layout != null)
            {
                FormatterStorageEmitter.Payload(w, layout, "first", "length", false);
                return;
            }
            w.Line("if (length == 0) return;");
            if (arrayContract?.Supports("ReadFramedNullableInt32s") == true && !raw)
            {
                w.Line("reader.ReadFramedNullableInt32s(" + Marshal + ".CreateSpan(ref first, length));");
                return;
            }
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line(owner + ".Read<" + Profile + ">(ref reader, ref " + Unsafe + ".Add(ref first, (nint)(uint)index));");
            w.Close();
            w.Line("while (++index < length);");
        }

        void NullableReadSupport(CodeWriter w)
        {
            // Checked reader copies invoke this closed helper after their own shape and count validation.
            w.Line(w.MethodImpl("AggressiveInlining"));
            w.Line("internal static " + type + " ReadNullablePayload(scoped ref LuminPackReader reader, int length, " +
                string.Join(", ", dimensions.Select(dimension => "int " + dimension)) + ")");
            w.Open();
            w.Line(NullableRectangle + ".Validate" + storageArguments + "(ref reader, length, " + encoding + ", " +
                hasTag + ", out byte storageTag);");
            w.Line("var replacement = " + array + ";");
            w.Line("if (length != 0)");
            w.Open();
            First(w, item, "replacement");
            w.Line(NullableRectangle + ".Read" + storageArguments + "(ref reader, " + Marshal + ".CreateSpan(ref first, length), " +
                encoding + ", storageTag);");
            w.Close();
            w.Line("return replacement;");
            w.Close();
            w.Blank();
        }

        void JsonWrite(CodeWriter w)
        {
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.WriteNull<" + Profile + ">();");
            w.Line("return;");
            w.Close();
            w.Line("writer.WriteObjectStart<" + Profile + ">();");
            WriteShape(w, "Lengths", "GetLength");
            WriteShape(w, "LowerBounds", "GetLowerBound");
            w.Line("writer.WritePropertyName<" + Profile + ">(\"Values\");");
            w.Line("writer.WriteArrayStart<" + Profile + ">();");
            FirstJson(w);
            if (!JsonIntegerRunWriter.TryEmit(w, set, type, item, Marshal + ".CreateReadOnlySpan(ref first, value.Length)", readBeforeSeparator: true))
            {
                w.Line("int length = value.Length;");
                w.Line("if (length != 0)");
                w.Open();
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line("ref var item = ref " + Unsafe + ".Add(ref first, (nint)(uint)index);");
                w.Line(owner + ".Write<" + Profile + ">(ref writer, in item);");
                w.Close();
                w.Line("while (++index < length);");
                w.Close();
            }
            w.Line("writer.WriteArrayEnd<" + Profile + ">();");
            w.Line("writer.WriteObjectEnd<" + Profile + ">();");
        }

        void WriteShape(CodeWriter w, string property, string accessor)
        {
            w.Line("writer.WritePropertyName<" + Profile + ">(\"" + property + "\");");
            w.Line("writer.WriteArrayStart<" + Profile + ">();");
            foreach (string axis in axes)
                w.Line("writer.Write<" + Profile + ">(value." + accessor + "(" + axis + "));");
            w.Line("writer.WriteArrayEnd<" + Profile + ">();");
        }

        void JsonRead(CodeWriter w, string reader)
        {
            w.Line("if (reader.IsNull())");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Line("reader.TryConsumeObjectStart<" + Profile + ">();");
            string rankText = rank.ToString(CultureInfo.InvariantCulture);
            w.Line("global::System.Span<int> lengths = stackalloc int[" + rankText + "];");
            w.Line("global::System.Span<int> lowerBounds = stackalloc int[" + rankText + "];");
            w.Line("bool hasLengths = false, hasLowerBounds = false;");
            w.Line("bool hasValues = false;");
            w.Line("var items = global::System.Array.Empty<" + item + ">();");
            w.Line("int itemCount = 0;");
            string pool = "global::System.Buffers.ArrayPool<" + item + ">.Shared";
            string release = pool + ".Return(items, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + item + ">());";
            w.Line("try");
            w.Open();
            w.Line("while (reader.Read<" + Profile + ">())");
            w.Open();
            w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd) break;");
            MatchProperty(w);
            w.Line("reader.Read<" + Profile + ">();");
            w.Line("if (property == 1 || property == 2)");
            w.Open();
            w.Line("reader.TryConsumeArrayStart<" + Profile + ">();");
            w.Line("var shape = property == 1 ? lengths : lowerBounds;");
            w.Line("ref int firstDimension = ref " + Marshal + ".GetReference(shape);");
            w.Line("int index = 0;");
            w.Line("do");
            w.Open();
            w.Line("reader.Read<" + Profile + ">();");
            w.Line(Unsafe + ".Add(ref firstDimension, (nint)(uint)index) = reader.Read<int, " + Profile + ">();");
            w.Close();
            w.Line("while (++index < shape.Length);");
            w.Line("reader.ConsumeArrayEnd<" + Profile + ">();");
            w.Line("if (property == 1) hasLengths = true;");
            w.Line("else hasLowerBounds = true;");
            w.Close();
            w.Line("else if (property == 3)");
            w.Open();
            w.Line("hasValues = true;");
            w.Line("reader.TryConsumeArrayStart<" + Profile + ">();");
            w.Line("int counted = reader.CountArrayElements<" + Profile + ">();");
            w.Line("if (items.Length < counted)");
            w.Open();
            w.Line("if (items.Length != 0) " + release);
            w.Line("items = global::System.Array.Empty<" + item + ">();");
            w.Line("items = " + pool + ".Rent(counted);");
            w.Close();
            w.Line("itemCount = 0;");
            w.Line("ref var firstItem = ref " + PackMarshal + ".GetNotNullArrayReference(items);");
            w.Line("while (reader.Read<" + Profile + ">())");
            w.Open();
            w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd) break;");
            w.Line(item + " item = default!;");
            w.Line(owner + ".Read<" + Profile + ">(ref reader, ref item);");
            w.Line("if (itemCount == items.Length) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
            w.Line(Unsafe + ".Add(ref firstItem, (nint)(uint)itemCount++) = item;");
            w.Close();
            w.Close();
            w.Line("else reader.Skip<" + Profile + ">();");
            w.Close();
            w.Line("if (!hasLengths || !hasLowerBounds || !hasValues) " + Error +
                ".ThrowFormatException(\"A multidimensional JSON object requires Lengths, LowerBounds and Values properties.\");");
            w.Line("long expectedLength = " + string.Join(" || ", axes.Select(axis => "lengths[" + axis + "] == 0")) + " ? 0 : 1;");
            w.Line("ref int firstLength = ref " + Marshal + ".GetReference(lengths);");
            w.Line("int lengthIndex = 0;");
            w.Line("do");
            w.Open();
            w.Line("int dimension = " + Unsafe + ".Add(ref firstLength, (nint)(uint)lengthIndex);");
            AccumulateDimension(w, "dimension");
            w.Close();
            w.Line("while (++lengthIndex < lengths.Length);");
            w.Line("if (expectedLength != itemCount) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
            w.Line("if (" + string.Join(" && ", axes.Select(axis => "lowerBounds[" + axis + "] == 0")) + ")");
            w.Open();
            for (int axis = 0; axis < rank; axis++)
                w.Line("int " + dimensions[axis] + " = lengths[" + axes[axis] + "];");
            w.Line("value = " + array + ";");
            w.Close();
            w.Line("else");
            w.Open();
            w.Line("#if NET9_0_OR_GREATER");
            w.Line("value = (" + type + ")global::System.Array.CreateInstanceFromArrayType(typeof(" + type + "), lengths.ToArray(), lowerBounds.ToArray());");
            w.Line("#else");
            w.Line("value = (" + type + ")global::System.Array.CreateInstance(typeof(" + item + "), lengths.ToArray(), lowerBounds.ToArray());");
            w.Line("#endif");
            w.Close();
            FirstJson(w);
            w.Line(PackMarshal + ".CreateReadOnlySpan(ref " + PackMarshal + ".GetNotNullArrayReference(items), itemCount).CopyTo(" +
                PackMarshal + ".CreateSpan(ref first, itemCount));");
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("if (items.Length != 0) " + release);
            w.Close();
        }

        void FirstJson(CodeWriter w)
        {
            w.Line("#if NET8_0_OR_GREATER");
            w.Line("ref var first = ref " + Unsafe + ".As<byte, " + item + ">(ref " + Marshal + ".GetArrayDataReference((global::System.Array)value));");
            w.Line("#else");
            w.Line("ref var first = ref (value.Length == 0 ? ref " + Unsafe + ".NullRef<" + item + ">() : ref value[" +
                string.Join(", ", axes.Select(axis => "value.GetLowerBound(" + axis + ")")) + "]);");
            w.Line("#endif");
        }
    }

    /// <summary>Writes the first row-major element address after the caller validates the shape.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="item">The element type.</param>
    /// <param name="array">The row-major array that owns the element storage.</param>
    private static void First(CodeWriter w, string item, string array = "value") =>
        w.Line("ref var first = ref " + Unsafe + ".As<byte, " + item + ">(ref " + PackMarshal +
            ".DangerousGetArrayDataReference<" + item + ">(" + array + "));");

    /// <summary>Validates a dimension and accumulates its contribution to the element count.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="dimension">The dimension expression.</param>
    private static void AccumulateDimension(CodeWriter w, string dimension)
    {
        w.Line("if (" + dimension + " < 0) " + Error + ".ThrowNegativeMultiDimensionalArrayDimension();");
        w.Line("expectedLength *= " + dimension + ";");
        w.Line("if (expectedLength > int.MaxValue) " + Error + ".ThrowMultiDimensionalArrayDimensionsTooLarge();");
    }

    /// <summary>Matches the fixed envelope property names without allocating strings.</summary>
    /// <param name="w">The source writer.</param>
    private static void MatchProperty(CodeWriter w)
    {
        string[] names = { "Lengths", "LowerBounds", "Values" };
        string choice = string.Concat(names.Select((name, index) =>
            "propertyName.Length == " + name.Length.ToString(CultureInfo.InvariantCulture) +
            string.Concat(name.Select((character, position) => " && propertyName[" +
                position.ToString(CultureInfo.InvariantCulture) + "] == " + ((int)character).ToString(CultureInfo.InvariantCulture))) +
            " ? " + (index + 1).ToString(CultureInfo.InvariantCulture) + " : ")) + "0;";
        w.Line("int property;");
        w.Line("if (default(" + Profile + ").StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
        w.Open();
        w.Line("var propertyName = reader.ReadStringUtf8<" + Profile + ">();");
        w.Line("property = " + choice);
        w.Close();
        w.Line("else");
        w.Open();
        w.Line("var propertyName = reader.ReadStringUtf16<" + Profile + ">();");
        w.Line("property = " + choice);
        w.Close();
        w.Line("reader.CompleteValue();");
    }
}
