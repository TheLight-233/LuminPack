using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the counted binary and enumerable JSON operations for a closed blocking collection.</summary>
internal static class BlockingCollectionOwnerFile
{
    /// <summary>Renders the requested operations for a blocking collection.</summary>
    /// <param name="set">The shared owner publication requirements.</param>
    /// <param name="fact">The closed collection and its element formatter.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete generated source.</returns>
    internal static string Render(SharedFormatterSet set, SequenceOwnerFact fact, GeneratedOperations operations)
    {
        using var w = new CodeWriter(set.AvailableAliases);
        string writer = w.A("LuminPackWriter");
        string reader = w.A("LuminPackReader");
        string jsonWriter = w.A("LuminPackJsonWriter");
        string jsonReader = w.A("LuminPackJsonReader");
        string options = w.A("ILuminPackOptions");
        string type = fact.DeclaredType;
        string item = fact.ForwardedType ?? fact.Scalar!;
        string element = "global::LuminPack." + GeneratedNames.Owner(set.Extension!, item);
        string roots = "LuminRoots_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(type));
        string root = "global::LuminPack.LuminPackSerializer." + roots;
        string owner = GeneratedNames.Owner(set.Extension!, type);
        bool raw = fact.Scalar != null && fact.Scalar != "string";

        return FormatterOwnerFile.Render(w, operations, "internal static partial class " + roots,
            "internal static partial class " + owner, null, EmitBinaryWrite, EmitBinaryRead, EmitJsonWrite, EmitJsonRead);

        void EmitBinaryWrite(FormatterSection section)
        {
            if (section == FormatterSection.Owner)
                Forward("Write", writer, "RootWrite", read: false);
            if (section != FormatterSection.Roots)
                return;

            Root("RootWrite", writer, read: false);
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.EnsureAdditionalCapacity(4);");
            w.Line("writer.WriteNullCollectionHeader();");
            w.Line("return;");
            w.Close();
            w.Line("int position = writer.BeginCollection();");
            w.Line("int count = 0;");
            w.Line("foreach (var item in value)");
            w.Open();
            if (item != "string")
                w.Line("writer.CheckBuffer();");
            w.Line("count++;");
            w.Line("var current = item;");
            w.Line(element + ".Write<TOptions>(ref writer, in current);");
            w.Close();
            w.Line("writer.EndCollection(position, count);");
            w.Line("writer.CheckBuffer();");
            w.Close();
            w.Blank();
        }

        void EmitBinaryRead(FormatterSection section)
        {
            if (section == FormatterSection.Owner)
                Forward("Read", reader, "RootRead", read: true);
            if (section != FormatterSection.Roots)
                return;

            Root("RootRead", reader, read: true);
            string minimum = raw ? w.A("Unsafe") + ".SizeOf<" + item + ">()" : "1";
            w.Line("int length = reader.ReadLength(" + minimum + ");");
            w.Line("if (length < 0)");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Line("var pool = global::System.Buffers.ArrayPool<" + item + ">.Shared;");
            w.Line("var items = pool.Rent(length);");
            w.Line("try");
            w.Open();
            if (raw)
                w.Line("reader.Read<" + item + ">(global::LuminPack.Code.LuminPackMarshal.CreateSpan(ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items), length));");
            else
            {
                w.Line("if (length != 0)");
                w.Open();
                w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(items);");
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line(item + " item = default!;");
                ForwardedElementRead.Emit(w, item, "reader", "item",
                    element + ".Read<TOptions>(ref reader, ref item);");
                w.Line(w.A("Unsafe") + ".Add(ref first, (nint)(uint)index) = item;");
                w.Close();
                w.Line("while (++index < length);");
                w.Close();
            }
            if (set.FindFormatter(type)?.Supports("CreateBlockingCollection") == true)
                w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateBlockingCollection(items, length);");
            else
            {
                // Without the interface casts, the conditional can convert both array branches to ArraySegment<T>.
                string source = "global::System.Collections.Generic.IEnumerable<" + item + ">";
                w.Line(source + " source = length == items.Length");
                w.Line("    ? (" + source + ")items : length == 0 ? (" + source + ")global::System.Array.Empty<" + item + ">()");
                w.Line("    : new global::System.ArraySegment<" + item + ">(items, 0, length);");
                w.Line("value = new " + type + "(new global::System.Collections.Concurrent.ConcurrentQueue<" + item + ">(source));");
            }
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("pool.Return(items, clearArray: " + (raw ? "false" : "true") + ");");
            w.Close();
            w.Close();
            w.Blank();
        }

        void EmitJsonWrite(FormatterSection section)
        {
            if (section == FormatterSection.Owner)
                Forward("Write", jsonWriter, "RootWriteJson", read: false);
            if (section != FormatterSection.Roots)
                return;

            Root("RootWriteJson", jsonWriter, read: false);
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.WriteNull<TOptions>();");
            w.Line("return;");
            w.Close();
            w.Line("writer.WriteArrayStart<TOptions>();");
            w.Line("if (value.Count != 0)");
            w.Open();
            w.Line("bool first = true;");
            w.Line("foreach (var item in value)");
            w.Open();
            w.Line("if (!first)");
            w.Line("    writer.WriteByteRaw<TOptions>((byte)',');");
            w.Line("first = false;");
            w.Line("writer.SetFirstElement(true);");
            w.Line("var current = item;");
            w.Line(element + ".Write<TOptions>(ref writer, in current);");
            w.Close();
            w.Close();
            w.Line("writer.WriteArrayEnd<TOptions>();");
            w.Close();
            w.Blank();
        }

        void EmitJsonRead(FormatterSection section)
        {
            if (section == FormatterSection.Owner)
            {
                w.Line("internal static void Read<TOptions>(ref " + jsonReader + " reader, ref " + type + " value)");
                w.Line("    where TOptions : struct, " + options);
                w.Open();
                w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.None && !reader.Read<TOptions>())");
                w.Line("    " + w.A("LuminPackExceptionHelper") + ".ThrowFormatException(\"JSON input does not contain a value.\");");
                w.Line(root + ".RootReadJson<TOptions>(ref reader, ref value);");
                w.Line("reader.CompleteValue();");
                w.Close();
                w.Blank();
            }
            if (section != FormatterSection.Roots)
                return;

            Root("RootReadJson", jsonReader, read: true);
            w.Line("if (reader.IsNull())");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Line("reader.TryConsumeArrayStart<TOptions>();");
            w.Line("value = new " + type + "();");
            w.Line("while (reader.Read<TOptions>())");
            w.Open();
            w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd)");
            w.Line("    break;");
            w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
            w.Line("    continue;");
            w.Line(item + " item = default!;");
            w.Line(element + ".Read<TOptions>(ref reader, ref item);");
            w.Line("value.Add(item);");
            w.Close();
            w.Close();
            w.Blank();
        }

        void Root(string method, string state, bool read)
        {
            string parameter = read ? " reader, scoped ref " : " writer, scoped in ";
            w.Line(w.Preserve);
            w.Line(w.MethodImpl("NoInlining"));
            w.Line("internal static void " + method + "<TOptions>(scoped ref " + state + parameter + type + " value)");
            w.Line("    where TOptions : struct, " + options);
            w.Open();
        }

        void Forward(string method, string state, string target, bool read)
        {
            string passing = read ? "ref " : "in ";
            w.Line("internal static void " + method + "<TOptions>(ref " + state + " state, " + passing + type + " value)");
            w.Line("    where TOptions : struct, " + options + " => " + root + "." + target +
                "<TOptions>(ref state, " + passing + "value);");
            w.Blank();
        }
    }
}
