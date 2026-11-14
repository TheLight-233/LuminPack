using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the standalone owners of <c>IGrouping&lt;TKey, TElement&gt;</c> and <c>ILookup&lt;TKey, TElement&gt;</c>.</summary>
/// <remarks>A grouping stores the null object header or its key followed by its elements as an enumerable sequence; a lookup stores
/// the count of its groupings followed by each grouping. The JSON form of a grouping is an object with a key and an elements array,
/// and a lookup is an array of such objects. The read builds the framework's own grouping and lookup types.</remarks>
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
        if (definition == GenericIdentity.Grouping)
            return SpecialOwnerFile.Render(set, operations, shortName, declared, true,
                (w, c) =>
                {
                    w.Line("if (value is null)");
                    w.Open();
                    w.Line("writer.WriteNullObjectHeader();");
                    w.Line("return;");
                    w.Close();
                    w.Blank();
                    w.Line(key + " key = value.Key;");
                    FormatterElementEmitter.Write(w, set, set.FindFormatter(key) ?? new FormatterModel(key, FormatterDefinitions.Scalar, raw: GenericIdentity.IsRaw(key)), "key");
                    w.Line(Sequence(element) + " elements = value;");
                    w.Line(sequence + ".Write<" + c.Profile + ">(ref writer, in elements);");
                },
                (w, c) =>
                {
                    w.Line("if (reader.PeekIsNullObject())");
                    w.Open();
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
                    w.Line("string? propertyName = null;");
                    w.Line("reader.Read<" + c.Profile + ">(ref propertyName);");
                    w.Line("reader.CompleteValue();");
                    w.Line("reader.Read<" + c.Profile + ">();");
                    w.Line("if (propertyName == \"Key\")");
                    w.Indented(() => w.Line(keyOwner + ".Read<" + c.Profile + ">(ref reader, ref key);"));
                    w.Line("else if (propertyName == \"Elements\")");
                    w.Indented(() => w.Line(sequence + ".Read<" + c.Profile + ">(ref reader, ref elements);"));
                    w.Line("else");
                    w.Indented(() => w.Line("reader.Skip<" + c.Profile + ">();"));
                    w.Close();
                    w.Line("value = new " + ownedGrouping + "(key" + (key == "string" ? "!" : "") + ", elements!);");
                });

        return LookupOwner(set, operations, shortName, declared, grouping, groupingOwner, ownedLookup, key, element);
    }

    private static string LookupOwner(SharedFormatterSet set, GeneratedOperations operations, string shortName, string declared,
        string grouping, string groupingOwner, string ownedLookup, string key, string element) =>
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
                w.Line("var dict = new global::System.Collections.Generic.Dictionary<" + key + ", " + grouping + ">();");
                w.Line("if (length != 0)");
                w.Open();
                w.Line("int index = 0;");
                w.Line("do");
                w.Open();
                w.Line(grouping + " group = default !;");
                w.Line(groupingOwner + ".Read<" + c.Profile + ">(ref reader, ref group);");
                w.Line("if (group != null)");
                w.Indented(() => w.Line("dict.Add(group.Key, group);"));
                w.Close();
                w.Line("while (++index < length);");
                w.Close();
                w.Line("value = new " + ownedLookup + "(dict);");
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
                w.Line("while (reader.Read<" + c.Profile + ">())");
                w.Open();
                w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ArrayEnd)");
                w.Line("    break;");
                w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd)");
                w.Line("    continue;");
                w.Line(grouping + " group = default !;");
                w.Line(groupingOwner + ".Read<" + c.Profile + ">(ref reader, ref group);");
                w.Line("dict.Add(group.Key, group);");
                w.Close();
                w.Line("value = new " + ownedLookup + "(dict);");
            });
}
