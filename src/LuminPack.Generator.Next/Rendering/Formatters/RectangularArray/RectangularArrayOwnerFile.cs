using System;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders row-major rectangular arrays with the legacy binary and JSON shapes.</summary>
internal static class RectangularArrayOwnerFile
{
    private const string Unsafe = "global::System.Runtime.CompilerServices.Unsafe";
    private const string Marshal = "global::System.Runtime.InteropServices.MemoryMarshal";
    private const string PackMarshal = "global::LuminPack.Code.LuminPackMarshal";
    private const string Error = "global::LuminPack.Code.LuminPackExceptionHelper";
    private const string Profile = "TLuminProfile";

    /// <summary>Renders the four independently selectable operations of a closed rectangular array.</summary>
    /// <param name="set">The shared publication context.</param>
    /// <param name="fact">The closed type, rank, element and optional reuse facts.</param>
    /// <param name="operations">The selected operations.</param>
    /// <returns>The generated owner source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
        => Render(set, fact.DeclaredType, fact.Scalar!, fact.ArrayRank, fact.ReuseArraySlots, operations);

    /// <summary>Renders a rectangular array using its general element and contiguous storage contracts.</summary>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations) =>
        Render(set, model.Type, model.Arguments[0].Type, model.Rank, model.ReuseArraySlots, operations, model.Arguments[0]);

    /// <summary>Renders the shared shape envelope with the analyzed element operations.</summary>
    private static string Render(SharedFormatterSet set, string type, string item, int rank, bool reuseArraySlots,
        GeneratedOperations operations, FormatterModel? element = null)
    {
        bool text = item == "string";
        bool raw = element != null ? element.RawCollection && element.Definition != GenericIdentity.Nullable :
            !text && GenericIdentity.NullableScalar(item) == null;
        FormatterStorageModel? layout = element?.Layout;
        string suffix = ReadableTypeName.Of(GeneratedNames.PayloadKey(type));
        string owner = "global::LuminPack." + GeneratedNames.Owner(set.Extension!, item);
        string[] axes = Enumerable.Range(0, rank).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray();
        string[] dimensions = axes.Select(axis => "dimension" + axis).ToArray();
        string array = "new " + ContiguousOwnerFile.WithRank(item, "[" + string.Join(", ", dimensions) + "]");
        string width = Unsafe + ".SizeOf<" + item + ">()";

        return SpecialOwnerFile.Render(set, operations, suffix, type, referenceType: true,
            (w, _) => BinaryWrite(w), (w, _) => BinaryRead(w), (w, _) => JsonWrite(w),
            (w, context) => JsonRead(w, context.JsonReader),
            w => { if (layout != null) FormatterStorageEmitter.Support(w, layout); },
            outlineJson: true, outlineBinaryWrite: !raw && !text && layout == null);

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
                First(w, item);
                w.Line("for (nint i = 0; i < value.Length; i++)");
                w.Open();
                w.Line("writer.CheckBuffer();");
                w.Line(owner + ".Write<" + Profile + ">(ref writer, in " + Unsafe + ".Add(ref first, i));");
                w.Close();
            }
            w.Line("writer.CheckBuffer();");
        }

        void BinaryRead(CodeWriter w)
        {
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
            w.Line("if (!reader.TryReadCollectionHead(out var length" + (layout != null ? ", 0" : "") + ")) " + Error + ".ThrowInvalidCollection();");
            w.Line("long expectedLength = " + string.Join(" || ", dimensions.Select(dimension => dimension + " == 0")) + " ? 0 : 1;");
            foreach (string dimension in dimensions)
                AccumulateDimension(w, dimension);
            w.Line("if (expectedLength != length) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
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
                w.Line("reader.Read(ref " + PackMarshal + ".DangerousGetArrayDataReference<" + item + ">(value), payloadBytes);");
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
            w.Line("value = " + array + ";");
            First(w, item);
            if (layout != null)
            {
                FormatterStorageEmitter.Payload(w, layout, "first", "length", false);
                return;
            }
            w.Line("for (nint i = 0; i < length; i++)");
            w.Open();
            w.Line(owner + ".Read<" + Profile + ">(ref reader, ref " + Unsafe + ".Add(ref first, i));");
            w.Close();
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
                w.Line("for (int i = 0; i < value.Length; i++)");
                w.Open();
                w.Line("ref var item = ref " + Unsafe + ".Add(ref first, (nint)(uint)i);");
                w.Line(owner + ".Write<" + Profile + ">(ref writer, in item);");
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
            w.Line("int[] lengths = null, lowerBounds = null;");
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
            w.Line("var shape = new int[" + rank.ToString(CultureInfo.InvariantCulture) + "];");
            w.Line("for (int i = 0; i < shape.Length; i++)");
            w.Open();
            w.Line("reader.Read<" + Profile + ">();");
            w.Line("shape[i] = reader.Read<int, " + Profile + ">();");
            w.Close();
            w.Line("reader.ConsumeArrayEnd<" + Profile + ">();");
            w.Line("if (property == 1) lengths = shape;");
            w.Line("else lowerBounds = shape;");
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
            w.Line("while (reader.Read<" + Profile + ">())");
            w.Open();
            w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd) break;");
            w.Line(item + " item = default!;");
            w.Line(owner + ".Read<" + Profile + ">(ref reader, ref item);");
            w.Line("if (itemCount == items.Length) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
            w.Line("items[itemCount++] = item;");
            w.Close();
            w.Close();
            w.Line("else reader.Skip<" + Profile + ">();");
            w.Close();
            w.Line("if (lengths is null || lowerBounds is null || !hasValues) " + Error +
                ".ThrowFormatException(\"A multidimensional JSON object requires Lengths, LowerBounds and Values properties.\");");
            w.Line("long expectedLength = global::System.Array.IndexOf(lengths, 0) >= 0 ? 0 : 1;");
            w.Line("for (int i = 0; i < lengths.Length; i++)");
            w.Open();
            AccumulateDimension(w, "lengths[i]");
            w.Close();
            w.Line("if (expectedLength != itemCount) " + Error + ".ThrowMultiDimensionalArrayLengthMismatch();");
            w.Line("#if NET9_0_OR_GREATER");
            w.Line("value = (" + type + ")global::System.Array.CreateInstanceFromArrayType(typeof(" + type + "), lengths, lowerBounds);");
            w.Line("#else");
            w.Line("value = (" + type + ")global::System.Array.CreateInstance(typeof(" + item + "), lengths, lowerBounds);");
            w.Line("#endif");
            FirstJson(w);
            w.Line("items.AsSpan(0, itemCount).CopyTo(" + Marshal + ".CreateSpan(ref first, itemCount));");
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
    private static void First(CodeWriter w, string item) =>
        w.Line("ref var first = ref " + Unsafe + ".As<byte, " + item + ">(ref " + PackMarshal +
            ".DangerousGetArrayDataReference<" + item + ">(value));");

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
