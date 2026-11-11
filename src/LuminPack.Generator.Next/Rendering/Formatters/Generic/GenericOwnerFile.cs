using System;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the standalone owners of the closed generic framework types: key and value pairs, nullable values, lazy
/// values and tuples whose arguments are raw scalars or strings.</summary>
/// <remarks>The binary members mirror the legacy emitters. A key and value pair stores its key then its value; a nullable value and
/// a lazy value store a memberless object header (or the null object header) followed by the value; a reference tuple stores an
/// object header carrying its item count, and a value tuple stores its items in order, each through the raw scalar store or the
/// profile's string store. The JSON forms use the shared element owners.</remarks>
internal static class GenericOwnerFile
{
    /// <summary>Renders the owner of a closed generic identity.</summary>
    /// <param name="identity">The normalized identity.</param>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source, or an empty string when the identity is not supported.</returns>
    internal static string Render(string identity, SharedFormatterSet set, GeneratedOperations operations)
    {
        if (!GenericIdentity.TryParse(identity, out string definition, out string[] args))
            return string.Empty;
        string declared = "global::" + identity;
        string shortName = ReadableTypeName.Of(identity);
        string extension = set.Extension!;
        switch (definition)
        {
            case GenericIdentity.KeyValuePair:
                return SpecialOwnerFile.Render(set, operations, shortName, declared, false,
                    (w, c) => PairWrite(w, c, args), (w, c) => PairRead(w, c, args, declared),
                    (w, c) => PairJsonWrite(w, c, args, extension), (w, c) => PairJsonRead(w, c, args, declared, extension));
            case GenericIdentity.Nullable:
                return SpecialOwnerFile.Render(set, operations, shortName, declared, false,
                    (w, c) => ValueWrite(w, c, args[0], "value.HasValue", "value.Value"),
                    (w, c) => ValueRead(w, c, args[0], "value = item;", "value = null;", null),
                    (w, c) => ValueJsonWrite(w, c, args[0], "!value.HasValue", "value.Value", extension, false),
                    (w, c) => ValueJsonRead(w, c, args[0], "value = null;", "value = item;", "value.GetValueOrDefault()", extension));
            case GenericIdentity.Lazy:
                return SpecialOwnerFile.Render(set, operations, shortName, declared, true,
                    (w, c) => ValueWrite(w, c, args[0], "value is not null", "value.Value"),
                    (w, c) => ValueRead(w, c, args[0], "value = new " + declared + "(item);", "value = null;", null),
                    (w, c) => ValueJsonWrite(w, c, args[0], "value is null", "value.Value", extension, true),
                    (w, c) => LazyJsonRead(w, c, args[0], declared, extension));
            case GenericIdentity.Memory:
            case GenericIdentity.ReadOnlyMemory:
            case GenericIdentity.ArraySegment:
            case GenericIdentity.ReadOnlySequence:
                return MemoryOwnerFile.Render(definition, args[0], declared, shortName, set, operations);
            default:
                return SpecialOwnerFile.Render(set, operations, shortName, declared, definition == GenericIdentity.Tuple,
                    (w, c) => TupleWrite(w, c, args, definition == GenericIdentity.Tuple),
                    (w, c) => TupleRead(w, c, args, declared, definition == GenericIdentity.Tuple),
                    (w, c) => TupleJsonWrite(w, c, args, definition == GenericIdentity.Tuple, extension),
                    (w, c) => TupleJsonRead(w, c, args, declared, definition == GenericIdentity.Tuple, extension));
        }
    }

    private static string Owner(string extension, string type) => "global::LuminPack." + GeneratedNames.Owner(extension, type);

    // Writes one item through the raw scalar store or the profile's string store.
    private static void WriteItem(CodeWriter w, SpecialOwnerContext c, string type, string local)
    {
        if (type == "string")
            w.Line("writer.Write<" + c.Profile + ">(" + local + ");");
        else
        {
            w.Line("writer.CheckBuffer();");
            w.Line("writer.Write(in " + local + ");");
        }
    }

    // Reads one item into an assignable local.
    private static void ReadItem(CodeWriter w, SpecialOwnerContext c, string type, string local)
    {
        if (type == "string")
            w.Line("reader.Read<" + c.Profile + ">(ref " + local + ");");
        else
            w.Line("reader.Read(ref global::System.Runtime.CompilerServices.Unsafe.As<" + type + ", byte>(ref " + local + "), global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + type + ">());");
    }

    private static string Declare(string type, string local) =>
        type == "string" ? "string? " + local + " = null;" : type + " " + local + " = default;";

    private static void PairWrite(CodeWriter w, SpecialOwnerContext c, string[] args)
    {
        w.Line(args[0] + " key = value.Key;");
        WriteItem(w, c, args[0], "key");
        w.Line(args[1] + " itemValue = value.Value;");
        WriteItem(w, c, args[1], "itemValue");
    }

    private static void PairRead(CodeWriter w, SpecialOwnerContext c, string[] args, string declared)
    {
        w.Line(Declare(args[0], "key"));
        w.Line(Declare(args[1], "itemValue"));
        ReadItem(w, c, args[0], "key");
        ReadItem(w, c, args[1], "itemValue");
        w.Line("value = new " + declared + "(key" + (args[0] == "string" ? "!" : "") + ", itemValue" + (args[1] == "string" ? "!" : "") + ");");
    }

    private static void PairJsonWrite(CodeWriter w, SpecialOwnerContext c, string[] args, string extension)
    {
        w.Line("writer.WriteArrayStart<" + c.Profile + ">();");
        w.Line(args[0] + " key = value.Key;");
        w.Line("writer.SetFirstElement(true);");
        w.Line(Owner(extension, args[0]) + ".Write<" + c.Profile + ">(ref writer, key);");
        w.Line("writer.WriteByteRaw<" + c.Profile + ">((byte)',');");
        w.Line(args[1] + " itemValue = value.Value;");
        w.Line("writer.SetFirstElement(true);");
        w.Line(Owner(extension, args[1]) + ".Write<" + c.Profile + ">(ref writer, itemValue);");
        w.Line("writer.WriteArrayEnd<" + c.Profile + ">();");
    }

    private static void PairJsonRead(CodeWriter w, SpecialOwnerContext c, string[] args, string declared, string extension)
    {
        w.Line("reader.TryConsumeArrayStart<" + c.Profile + ">();");
        w.Line(Declare(args[0], "key"));
        w.Line(Declare(args[1], "itemValue"));
        w.Line("if (reader.Read<" + c.Profile + ">())");
        w.Open();
        w.Line(Owner(extension, args[0]) + ".Read<" + c.Profile + ">(ref reader, ref key);");
        w.Line("if (reader.Read<" + c.Profile + ">())");
        w.Indented(() => w.Line(Owner(extension, args[1]) + ".Read<" + c.Profile + ">(ref reader, ref itemValue);"));
        w.Close();
        w.Line("reader.ConsumeArrayEnd<" + c.Profile + ">();");
        w.Line("value = new " + declared + "(key" + (args[0] == "string" ? "!" : "") + ", itemValue" + (args[1] == "string" ? "!" : "") + ");");
    }

    // A nullable or lazy value: the null object header, or a memberless object header and the value.
    private static void ValueWrite(CodeWriter w, SpecialOwnerContext c, string type, string present, string access)
    {
        w.Line("if (!(" + present + "))");
        w.Open();
        w.Line("writer.WriteNullObjectHeader();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("writer.WriteObjectHeader(0);");
        w.Line(type + " item = " + access + ";");
        WriteItem(w, c, type, "item");
    }

    private static void ValueRead(CodeWriter w, SpecialOwnerContext c, string type, string assign, string none, string? unused)
    {
        w.Line("if (!reader.TryReadObjectHead())");
        w.Open();
        w.Line(none);
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(Declare(type, "item"));
        ReadItem(w, c, type, "item");
        w.Line(type == "string" ? assign.Replace("(item)", "(item!)").Replace("= item;", "= item!;") : assign);
    }

    private static void ValueJsonWrite(CodeWriter w, SpecialOwnerContext c, string type, string absent, string access, string extension,
        bool lazy)
    {
        w.Line("if (" + absent + ")");
        w.Open();
        w.Line("writer.WriteNull<" + c.Profile + ">();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(type + " item = " + access + ";");
        if (lazy)
        {
            w.Line("writer.WriteObjectStart<" + c.Profile + ">();");
            w.Line("writer.WritePropertyName<" + c.Profile + ">(\"Value\");");
        }
        w.Line(Owner(extension, type) + ".Write<" + c.Profile + ">(ref writer, item);");
        if (lazy)
            w.Line("writer.WriteObjectEnd<" + c.Profile + ">();");
    }

    private static void ValueJsonRead(CodeWriter w, SpecialOwnerContext c, string type, string none, string assign, string seed,
        string extension)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line(none);
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(type + " item = " + seed + ";");
        w.Line(Owner(extension, type) + ".Read<" + c.Profile + ">(ref reader, ref item);");
        w.Line(assign);
    }

    private static void LazyJsonRead(CodeWriter w, SpecialOwnerContext c, string type, string declared, string extension)
    {
        w.Line("if (reader.IsNull())");
        w.Open();
        w.Line("value = null;");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line(Declare(type, "item"));
        w.Line("reader.TryConsumeObjectStart<" + c.Profile + ">();");
        w.Line("bool hasValue = false;");
        w.Line("while (reader.Read<" + c.Profile + ">())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd)");
        w.Line("    break;");
        w.Line("string? propertyName = null;");
        w.Line("reader.Read<" + c.Profile + ">(ref propertyName);");
        w.Line("reader.CompleteValue();");
        w.Line("reader.Read<" + c.Profile + ">();");
        w.Line("if (propertyName == \"Value\")");
        w.Open();
        w.Line(Owner(extension, type) + ".Read<" + c.Profile + ">(ref reader, ref item);");
        w.Line("hasValue = true;");
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("reader.Skip<" + c.Profile + ">();"));
        w.Close();
        w.Line("if (!hasValue)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"A Lazy JSON object requires a Value property.\");"));
        w.Line("value = new " + declared + "(" + (type == "string" ? "item!" : "item") + ");");
    }

    private static string ItemName(int index) => "Item" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void TupleWrite(CodeWriter w, SpecialOwnerContext c, string[] args, bool reference)
    {
        if (reference)
        {
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.WriteNullObjectHeader();");
            w.Line("return;");
            w.Close();
            w.Blank();
            w.Line("writer.WriteObjectHeader(" + args.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ");");
        }
        for (int index = 0; index < args.Length; index++)
        {
            w.Line(args[index] + " item" + index + " = value." + ItemName(index) + ";");
            WriteItem(w, c, args[index], "item" + index);
        }
    }

    private static void TupleRead(CodeWriter w, SpecialOwnerContext c, string[] args, string declared, bool reference)
    {
        if (reference)
        {
            w.Line("if (!reader.TryReadObjectHead())");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Blank();
        }
        for (int index = 0; index < args.Length; index++)
        {
            w.Line(Declare(args[index], "item" + index));
            ReadItem(w, c, args[index], "item" + index);
        }
        w.Line("value = new " + declared + "(" + Items(args) + ");");
    }

    private static string Items(string[] args)
    {
        var items = new string[args.Length];
        for (int index = 0; index < args.Length; index++)
            items[index] = "item" + index + (args[index] == "string" ? "!" : "");
        return string.Join(", ", items);
    }

    private static void TupleJsonWrite(CodeWriter w, SpecialOwnerContext c, string[] args, bool reference, string extension)
    {
        if (reference)
        {
            w.Line("if (value is null)");
            w.Open();
            w.Line("writer.WriteNull<" + c.Profile + ">();");
            w.Line("return;");
            w.Close();
            w.Blank();
        }
        w.Line("writer.WriteArrayStart<" + c.Profile + ">();");
        for (int index = 0; index < args.Length; index++)
        {
            if (index > 0)
                w.Line("writer.WriteByteRaw<" + c.Profile + ">((byte)',');");
            w.Line("writer.SetFirstElement(true);");
            w.Line(args[index] + " item" + index + " = value." + ItemName(index) + ";");
            w.Line(Owner(extension, args[index]) + ".Write<" + c.Profile + ">(ref writer, item" + index + ");");
        }
        w.Line("writer.WriteArrayEnd<" + c.Profile + ">();");
    }

    private static void TupleJsonRead(CodeWriter w, SpecialOwnerContext c, string[] args, string declared, bool reference, string extension)
    {
        if (reference)
        {
            w.Line("if (reader.IsNull())");
            w.Open();
            w.Line("value = null;");
            w.Line("return;");
            w.Close();
            w.Blank();
        }
        w.Line("reader.TryConsumeArrayStart<" + c.Profile + ">();");
        for (int index = 0; index < args.Length; index++)
        {
            w.Line("reader.Read<" + c.Profile + ">();");
            w.Line(Declare(args[index], "item" + index));
            w.Line(Owner(extension, args[index]) + ".Read<" + c.Profile + ">(ref reader, ref item" + index + ");");
        }
        w.Line("reader.Read<" + c.Profile + ">();");
        w.Line("value = new " + declared + "(" + Items(args) + ");");
    }
}
