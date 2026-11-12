using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders arrays whose elements are read and written by generated static owners.</summary>
internal static class ArrayForwardingOwnerFile
{
    /// <summary>Renders the requested array operations.</summary>
    /// <param name="set">The shared owner publication requirements.</param>
    /// <param name="fact">The array type and the type of its forwarded element.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete generated source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string serializer = w.A("LuminPackSerializer");
        string writer = w.A("LuminPackWriter");
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string options = w.A("ILuminPackOptions");
        string preserve = w.Preserve;
        string utf16 = w.A("LuminPackUtf16LengthOptions");
        string marshal = w.A("LuminPackMarshal");
        string extension = set.Extension!;
        string itemType = fact.ForwardedType!;
        string collection = fact.DeclaredType;
        string roots = "LuminRoots_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(collection));
        string root = serializer + "." + roots;
        string elementOwner = "global::LuminPack." + GeneratedNames.Owner(extension, itemType);
        string owner = GeneratedNames.Owner(extension, collection);
        bool compressElements = itemType.IndexOf('<') < 0 && itemType.EndsWith("[]", StringComparison.Ordinal) ||
            GenericFormatterReuse.IsCompressedPlaceholder(itemType);

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots,
            "internal static partial class " + owner, "internal static partial class " + extension,
            EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    BinaryWrite("RootWrite", compressed: false);
                    BinaryWrite("RootWriteCompressed", compressElements);
                    break;
                case FormatterSection.Entries:
                    w.Line("internal static void SerializeWithCompress<TOptions>(scoped ref " + writer + " writer, scoped in " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + roots + ".RootWriteCompressed<TOptions>(ref writer, in value);");
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    WriteForwarder(writer, "RootWrite");
                    break;
                case FormatterSection.Extensions:
                    w.Line("public static void WriteValueWithCompress(this ref " + writer + " writer, in " + collection + " value)");
                    w.Line("    => " + root + ".RootWriteCompressed<" + utf16 + ">(ref writer, in value);");
                    break;
            }
        }

        void EmitBinaryRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    BinaryRead("RootRead", compressed: false);
                    BinaryRead("RootReadCompressed", compressElements);
                    break;
                case FormatterSection.Entries:
                    w.Line("internal static void DeserializeWithCompress<TOptions>(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + roots + ".RootReadCompressed<TOptions>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line("internal static void Read<TOptions>(ref " + reader + " reader, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options + " => " + root + ".RootRead<TOptions>(ref reader, ref value);");
                    w.Blank();
                    break;
                case FormatterSection.Extensions:
                    w.Line("public static void ReadValueWithCompress(this ref " + reader + " reader, ref " + collection + " value)");
                    w.Line("    => " + root + ".RootReadCompressed<" + utf16 + ">(ref reader, ref value);");
                    break;
            }
        }

        void EmitJsonWrite(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootWriteJson<TLuminProfile>(scoped ref " + jsonWriter + " writer, " + collection + " input)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("var value = input;");
                    w.Line("if (value == null)");
                    w.Open();
                    w.Line("writer.WriteNull<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayStart<TLuminProfile>();");
                    w.Line("bool isFirst = true;");
                    w.Line("foreach (ref var item in value.AsSpan())");
                    w.Open();
                    w.Line("if (!isFirst)");
                    w.Line("    writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("else");
                    w.Line("    isFirst = false;");
                    w.Line("writer.SetFirstElement(true);");
                    w.Line(elementOwner + ".Write<TLuminProfile>(ref writer, item);");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteArrayEnd<TLuminProfile>();");
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    WriteForwarder(jsonWriter, "RootWriteJson");
                    break;
            }
        }

        void EmitJsonRead(FormatterSection section)
        {
            switch (section)
            {
                case FormatterSection.Roots:
                    w.Line(preserve);
                    w.Line("internal static void RootReadJson<TLuminProfile>(scoped ref " + jsonReader + " reader, scoped ref " + collection + " value)");
                    w.Line("    where TLuminProfile : struct, " + options);
                    w.Open();
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                    w.Line("if (value is not null && value.GetType() == typeof(" + collection + ") && reader.CountArrayElements<TLuminProfile>() == value.Length)");
                    w.Open();
                    w.Line("for (int i = 0; i < value.Length; i++)");
                    w.Open();
                    w.Line("reader.Read<TLuminProfile>();");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref value[i]);");
                    w.Close();
                    w.Blank();
                    w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line(collection + "? buffer = global::System.Buffers.ArrayPool<" + itemType + ">.Shared.Rent(4);");
                    w.Line("int count = 0;");
                    w.Line("int capacity = buffer.Length;");
                    w.Line("try");
                    w.Open();
                    w.Line("while (true)");
                    w.Open();
                    w.Line("if (!reader.Read<TLuminProfile>())");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
                    w.Line("    break;");
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    continue;");
                    w.Line("if (count >= capacity)");
                    w.Open();
                    w.Line("int newCapacity = capacity * 2;");
                    w.Line("var newBuffer = global::System.Buffers.ArrayPool<" + itemType + ">.Shared.Rent(newCapacity);");
                    w.Line("buffer.AsSpan(0, count).CopyTo(newBuffer);");
                    w.Line("global::System.Buffers.ArrayPool<" + itemType + ">.Shared.Return(buffer, clearArray: true);");
                    w.Line("buffer = newBuffer;");
                    w.Line("capacity = newBuffer.Length;");
                    w.Close();
                    w.Blank();
                    w.Line(itemType + " item = default !;");
                    w.Line(elementOwner + ".Read<TLuminProfile>(ref reader, ref item);");
                    w.Line("buffer[count++] = item;");
                    w.Close();
                    w.Blank();
                    w.Line("value = buffer.AsSpan(0, count).ToArray();");
                    w.Close();
                    w.Line("finally");
                    w.Open();
                    w.Line("if (buffer != null)");
                    w.Line("    global::System.Buffers.ArrayPool<" + itemType + ">.Shared.Return(buffer, clearArray: true);");
                    w.Close();
                    w.Close();
                    w.Blank();
                    break;
                case FormatterSection.Owner:
                    w.Line("internal static void Read<TOptions>(ref " + jsonReader + " reader, ref " + collection + " value)");
                    w.Line("    where TOptions : struct, " + options);
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !reader.Read<TOptions>())");
                    w.Line("    " + w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                    w.Line(root + ".RootReadJson<TOptions>(ref reader, ref value);");
                    w.Line("reader.CompleteValue();");
                    w.Close();
                    w.Blank();
                    break;
            }
        }

        void BinaryWrite(string method, bool compressed)
        {
            w.Line(preserve);
            w.Line("internal static void " + method + "<TOptions>(scoped ref " + writer + " writer, scoped in " + collection + " value)");
            w.Line("    where TOptions : struct, " + options);
            w.Open();
            w.Line("writer.EnsureAdditionalCapacity(4);");
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.WriteNullCollectionHeader();");
            w.Line("return;");
            w.Close();
            w.Line("writer.WriteCollectionHeader(value.Length);");
            w.Line("foreach (ref readonly var item in value.AsSpan())");
            w.Open();
            w.Line("writer.CheckBuffer();");
            w.Line((compressed ? serializer + ".SerializeWithCompress" : elementOwner + ".Write") + "<TOptions>(ref writer, in item);");
            w.Close();
            w.Line("writer.CheckBuffer();");
            w.Close();
            w.Blank();
        }

        void BinaryRead(string method, bool compressed)
        {
            w.Line(preserve);
            w.Line("internal static void " + method + "<TOptions>(scoped ref " + reader + " reader, scoped ref " + collection + " value)");
            w.Line("    where TOptions : struct, " + options);
            w.Open();
            w.Line("int length = reader.ReadLength(1);");
            w.Line("if (length < 0)");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Line("if (length == 0)");
            w.Open();
            w.Line("value = global::System.Array.Empty<" + itemType + ">();");
            w.Line("return;");
            w.Close();
            w.Line("if (value is null || value.Length != length)");
            w.Line("    value = " + marshal + ".AllocateUninitializedArray<" + itemType + ">(length);");
            w.Line("for (int i = 0; i < length; i++)");
            w.Open();
            string read = compressed ? serializer + ".DeserializeWithCompress" : elementOwner + ".Read";
            ForwardedElementRead.Emit(w, itemType, "reader", "value[i]", read + "<TOptions>(ref reader, ref value[i]);", compressed);
            w.Close();
            w.Close();
            w.Blank();
        }

        void WriteForwarder(string state, string method)
        {
            w.Line("internal static void Write<TOptions>(ref " + state + " writer, in " + collection + " value)");
            w.Line("    where TOptions : struct, " + options + " => " + root + "." + method + "<TOptions>(ref writer, " + (method == "RootWriteJson" ? "value" : "in value") + ");");
            w.Blank();
        }
    }
}
