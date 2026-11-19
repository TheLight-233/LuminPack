using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the standalone owners of <c>IGrouping&lt;TKey, TElement&gt;</c> and <c>ILookup&lt;TKey, TElement&gt;</c>.</summary>
/// <remarks>A grouping stores the null object header or its key followed by its elements as an enumerable sequence. Opt-in
/// framing adds a presence marker before the key. A lookup stores its group count and then each grouping. JSON stores groupings
/// as objects with a key and an elements array, and a lookup as an array of those objects.</remarks>
internal static class GroupingOwnerFile
{
    /// <summary>The enumerable sequence spelling of an element type.</summary>
    /// <param name="element">The element keyword or <c>string</c>.</param>
    /// <returns>The closed enumerable spelling the grouping owner calls through.</returns>
    internal static string Sequence(string element) => "global::System.Collections.Generic.IEnumerable<" + element + ">";

    /// <summary>Renders the owner.</summary>
    /// <param name="definition">The grouping or lookup definition.</param>
    /// <param name="args">The key and element arguments.</param>
    /// <param name="declared">The fully qualified closed type.</param>
    /// <param name="shortName">The readable owner suffix.</param>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(string definition, string[] args, string declared, string shortName, SharedFormatterSet set,
        GeneratedOperations operations)
    {
        string extension = set.Extension!;
        string key = args[0], element = args[1];
        string keyOwner = GenericOwnerFile.Owner(extension, key);
        string sequence = GenericOwnerFile.Owner(extension, Sequence(element));
        string grouping = "global::System.Linq.IGrouping<" + key + ", " + element + ">";
        string groupingOwner = GenericOwnerFile.Owner(extension, "System.Linq.IGrouping<" + key + "," + element + ">");
        string ownedGrouping = "global::LuminPack.Utility.LuminPackGrouping<" + key + ", " + element + ">";
        string ownedLookup = "global::LuminPack.Utility.LuminPackLookup<" + key + ", " + element + ">";
        var contract = set.FindFormatter(declared);
        if (definition == GenericIdentity.Grouping)
        {
            bool canReadJsonStringChoice = contract?.Supports("CanReadJsonStringChoice") == true;
            bool capture = contract?.Supports("CopyFrameworkGrouping") == true;
            bool framed = set.Options.FramedGroupings;
            return SpecialOwnerFile.Render(set, operations, shortName, declared, true,
                (w, c) =>
                {
                    if (framed)
                        w.Line("writer.EnsureAdditionalCapacity(sizeof(byte));");
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("writer.WriteNullObjectHeader();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    if (framed)
                        w.Line("writer.WriteObjectHeader(0);");
                    w.Line(key + " key = value.Key;");
                    FormatterElementEmitter.Write(w, set, set.FindFormatter(key) ?? new FormatterModel(key, FormatterDefinitions.Scalar, raw: GenericIdentity.IsRaw(key)), "key");
                    if (capture && set.FindFormatter(element) is { } elementModel)
                        WriteFrameworkGrouping(w, c, set, elementModel);
                    w.Line(Sequence(element) + " elements = value;");
                    w.Line(sequence + ".Write<" + c.Profile + ">(ref writer, in elements);");
                },
                (w, c) =>
                {
                    w.Line(framed
                        ? "if (global::LuminPack.Security.LuminPackInputPolicy.ReadNullMarker(ref reader, 0))"
                        : "if (reader.PeekIsNullObject())");
                    w.Open();
                    if (!framed)
                        w.Line("reader.Advance(1);");
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line(GenericOwnerFile.Declare(key, "key"));
                    FormatterElementEmitter.Read(w, set, set.FindFormatter(key) ?? new FormatterModel(key, FormatterDefinitions.Scalar, raw: GenericIdentity.IsRaw(key)), "key");
                    w.Line(Sequence(element) + " elements = default !;");
                    w.Line(sequence + ".Read<" + c.Profile + ">(ref reader, ref elements);");
                    w.Line("if (elements is null)");
                    w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowDeserializeObjectIsNull(nameof(elements));"));
                    w.Line("value = new " + ownedGrouping + "(key" + (key == "string" ? "!" : "") + ", elements);");
                },
                (w, c) =>
                {
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("writer.WriteNull<" + c.Profile + ">();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line("writer.WriteObjectStart<" + c.Profile + ">();");
                    w.Line("writer.WritePropertyName<" + c.Profile + ">(\"Key\");");
                    w.Line(key + " key = value.Key;");
                    w.Line(keyOwner + ".Write<" + c.Profile + ">(ref writer, key);");
                    w.Line("writer.WritePropertyName<" + c.Profile + ">(\"Elements\");");
                    w.Line(Sequence(element) + " elements = value;");
                    w.Line(sequence + ".Write<" + c.Profile + ">(ref writer, in elements);");
                    w.Line("writer.WriteObjectEnd<" + c.Profile + ">();");
                },
                (w, c) =>
                {
                    w.Line("if (reader.IsNull())");
                    w.Open();
                    w.Line("value = null;");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line(GenericOwnerFile.Declare(key, "key"));
                    w.Line(Sequence(element) + " elements = default !;");
                    w.Line("reader.TryConsumeObjectStart<" + c.Profile + ">();");
                    w.Line("while (reader.Read<" + c.Profile + ">())");
                    w.Open();
                    w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd)");
                    w.Line("    break;");
                    w.Line("int propertyChoice;");
                    if (canReadJsonStringChoice)
                    {
                        w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.String)");
                        w.Open();
                        w.Line("propertyChoice = reader.ReadStringChoice<" + c.Profile + ">(new byte[] { 75, 101, 121 }, " +
                            "global::System.MemoryExtensions.AsSpan(\"Key\"), new byte[] { 69, 108, 101, 109, 101, 110, 116, 115 }, " +
                            "global::System.MemoryExtensions.AsSpan(\"Elements\"));");
                        w.Close();
                        w.Line("else");
                        w.Open();
                    }
                    w.Line("string? propertyName = null;");
                    w.Line("reader.Read<" + c.Profile + ">(ref propertyName);");
                    w.Line("propertyChoice = propertyName == \"Key\" ? 1 : propertyName == \"Elements\" ? 2 : 0;");
                    if (canReadJsonStringChoice)
                        w.Close();
                    w.Line("reader.CompleteValue();");
                    w.Line("reader.Read<" + c.Profile + ">();");
                    w.Line("if (propertyChoice == 1)");
                    w.Indented(() => w.Line(keyOwner + ".Read<" + c.Profile + ">(ref reader, ref key);"));
                    w.Line("else if (propertyChoice == 2)");
                    w.Indented(() => w.Line(sequence + ".Read<" + c.Profile + ">(ref reader, ref elements);"));
                    w.Line("else");
                    w.Indented(() => w.Line("reader.Skip<" + c.Profile + ">();"));
                    w.Close();
                    w.Line("value = new " + ownedGrouping + "(key" + (key == "string" ? "!" : "") + ", elements!);");
                });
        }

        return LookupOwner(set, operations, shortName, declared, grouping, groupingOwner, ownedLookup, key,
            contract?.Supports("CreateLookupWithNullKey") == true);
    }

    /// <summary>Copies framework grouping elements once and writes their existing sequence representation.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="context">The grouping's compiled writer profile.</param>
    /// <param name="set">The published formatter contracts.</param>
    /// <param name="element">The closed sequence element contract.</param>
    private static void WriteFrameworkGrouping(CodeWriter w, SpecialOwnerContext context, SharedFormatterSet set, FormatterModel element)
    {
        string staging = "global::LuminPack.Utility.LuminStaging<" + element.Type + ">";
        w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.GetFrameworkGroupingCopySource(value) is { } collection)");
        w.Open();
        w.Line("int count = collection.Count;");
        w.Line("var captured = " + staging + ".Rent(count);");
        w.Line("try");
        w.Open();
        w.Line("collection.CopyTo(captured, 0);");
        w.Line("var items = new global::System.ReadOnlySpan<" + element.Type + ">(captured, 0, count);");
        if (element.Type == "string")
        {
            w.Line("writer.EnsureAdditionalCapacity(sizeof(int));");
            w.Line("writer.WriteCollectionHeader(count);");
            w.Line("writer.WriteStrings<" + context.Profile + ">(items);");
        }
        else if (element.Raw && element.RawStorage && element.Layout == null)
            w.Line("writer.Write<" + element.Type + ">(items, count);");
        else if (element.NativeNullableElement)
        {
            const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
            w.Line("int bytes = checked(count * " + unsafeType + ".SizeOf<" + element.Type + ">());");
            w.Line("writer.EnsureAdditionalCapacity(checked(sizeof(int) + bytes));");
            w.Line("writer.WriteCollectionHeader(count);");
            w.Line("if (bytes != 0) writer.Write(ref " + unsafeType + ".As<" + element.Type + ", byte>(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(items)), bytes);");
        }
        else
        {
            w.Line("int header = writer.BeginCollection();");
            w.Line("foreach (ref readonly var item in items)");
            w.Open();
            FormatterElementEmitter.Write(w, set, element, "item");
            w.Close();
            w.Line("writer.EndCollection(header, count);");
        }
        w.Line("writer.CheckBuffer();");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line(staging + ".Return(captured, count);");
        w.Close();
        w.Line("return;");
        w.Close();
    }

    /// <summary>Handles lookup owner for <c>GroupingOwnerFile</c>.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <param name="shortName">The name of the short.</param>
    /// <param name="declared">The declared supplied to <c>LookupOwner</c>.</param>
    /// <param name="grouping">The grouping supplied to <c>LookupOwner</c>.</param>
    /// <param name="groupingOwner">The grouping owner supplied to <c>LookupOwner</c>.</param>
    /// <param name="ownedLookup">The owned lookup supplied to <c>LookupOwner</c>.</param>
    /// <param name="key">The key identifying the entry.</param>
    /// <param name="supportsNullKey">Whether the referenced runtime can preserve a null-key group.</param>
    /// <returns>The resulting text.</returns>
    private static string LookupOwner(SharedFormatterSet set, GeneratedOperations operations, string shortName, string declared,
        string grouping, string groupingOwner, string ownedLookup, string key, bool supportsNullKey) =>
        SpecialOwnerFile.Render(set, operations, shortName, declared, true,
            (w, c) =>
            {
                w.Line("if (value is null)");
                w.Open();
                w.Line("writer.WriteNullCollectionHeader();");
                w.Line("return;");
                w.Close();
                w.Blank();
                w.Line("writer.EnsureAdditionalCapacity(4);");
                w.Line("writer.WriteCollectionHeader(value.Count);");
                w.Line("foreach (var item in value)");
                w.Open();
                w.Line("writer.CheckBuffer();");
                w.Line(grouping + " group = item;");
                w.Line(groupingOwner + ".Write<" + c.Profile + ">(ref writer, in group);");
                w.Close();
                w.Line("writer.CheckBuffer();");
            },
            (w, c) =>
            {
                w.Line("int length = reader.ReadLength(1);");
                w.Line("if (length < 0)");
                w.Open();
                w.Line("value = null;");
                w.Line("return;");
                w.Close();
                w.Blank();
                // Null grouping records count toward the wire length. Bound the initial reservation even when
                // the declared groups fit in the remaining input.
                w.Line("var dict = new global::System.Collections.Generic.Dictionary<" + key + ", " + grouping + ">(global::System.Math.Min(length, 64));");
                if (supportsNullKey)
                    DeclareNullGroup(w, grouping);
                w.Line("if (length != 0)");
                w.Open();
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line(grouping + " group = default !;");
                w.Line(groupingOwner + ".Read<" + c.Profile + ">(ref reader, ref group);");
                w.Line("if (group != null)");
                if (supportsNullKey)
                {
                    w.Open();
                    AddGroup(w, supportsNullKey);
                    w.Close();
                }
                else
                    w.Indented(() => AddGroup(w, supportsNullKey));
                w.Close();
                w.Line("while (++index < length);");
                w.Close();
                PublishLookup(w, ownedLookup, supportsNullKey);
            },
            (w, c) =>
            {
                w.Line("if (value == null)");
                w.Open();
                w.Line("writer.WriteNull<" + c.Profile + ">();");
                w.Line("return;");
                w.Close();
                w.Blank();
                w.Line("writer.WriteArrayStart<" + c.Profile + ">();");
                w.Line("bool isFirst = true;");
                w.Line("foreach (var item in value)");
                w.Open();
                w.Line("if (!isFirst)");
                w.Line("    writer.WriteByteRaw<" + c.Profile + ">((byte)',');");
                w.Line("else");
                w.Line("    isFirst = false;");
                w.Line("writer.SetFirstElement(true);");
                w.Line(grouping + " group = item;");
                w.Line(groupingOwner + ".Write<" + c.Profile + ">(ref writer, group);");
                w.Close();
                w.Line("writer.WriteArrayEnd<" + c.Profile + ">();");
            },
            (w, c) =>
            {
                w.Line("if (reader.IsNull())");
                w.Open();
                w.Line("value = null;");
                w.Line("return;");
                w.Close();
                w.Blank();
                w.Line("reader.TryConsumeArrayStart<" + c.Profile + ">();");
                w.Line("var dict = new global::System.Collections.Generic.Dictionary<" + key + ", " + grouping + ">();");
                if (supportsNullKey)
                    DeclareNullGroup(w, grouping);
                w.Line("while (reader.Read<" + c.Profile + ">())");
                w.Open();
                w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ArrayEnd)");
                w.Line("    break;");
                w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd)");
                w.Line("    continue;");
                w.Line(grouping + " group = default !;");
                w.Line(groupingOwner + ".Read<" + c.Profile + ">(ref reader, ref group);");
                AddGroup(w, supportsNullKey);
                w.Close();
                PublishLookup(w, ownedLookup, supportsNullKey);
            });

    /// <summary>Stages an optional null-key group separately from the dictionary.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="grouping">The closed grouping type.</param>
    private static void DeclareNullGroup(CodeWriter w, string grouping)
    {
        w.Line(grouping + "? nullGroup = null;");
        w.Line("int nullGroupIndex = 0;");
    }

    /// <summary>Adds a decoded group, retaining ordinary duplicate-key rejection.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="supportsNullKey">Whether the receiver accepts a null-key group.</param>
    private static void AddGroup(CodeWriter w, bool supportsNullKey)
    {
        if (!supportsNullKey)
        {
            w.Line("dict.Add(group.Key, group);");
            return;
        }

        w.Line("var groupKey = group.Key;");
        w.Line("if (groupKey is null)");
        w.Open();
        w.Line("if (nullGroup != null)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentException(\"An item with the same key has already been added.\");"));
        w.Line("nullGroup = group;");
        w.Line("nullGroupIndex = dict.Count;");
        w.Close();
        w.Line("else");
        w.Indented(() => w.Line("dict.Add(groupKey, group);"));
    }

    /// <summary>Publishes the complete lookup after all its groups were read.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="ownedLookup">The runtime lookup implementation type.</param>
    /// <param name="supportsNullKey">Whether a staged null-key group may be present.</param>
    private static void PublishLookup(CodeWriter w, string ownedLookup, bool supportsNullKey)
    {
        string ordinary = "new " + ownedLookup + "(dict)";
        w.Line(supportsNullKey
            ? "value = nullGroup is null ? " + ordinary + " : " + ownedLookup + ".CreateWithNullKey(dict, nullGroup, nullGroupIndex);"
            : "value = " + ordinary + ";");
    }
}
