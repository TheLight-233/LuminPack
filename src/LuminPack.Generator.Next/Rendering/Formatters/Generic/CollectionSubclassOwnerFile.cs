using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Forwards a statically known collection-only subclass through its framework base formatter.</summary>
internal static class CollectionSubclassOwnerFile
{
    /// <summary>Renders the subclass owner with explicit registration taking precedence over base forwarding.</summary>
    /// <param name="set">The shared formatter owners and options.</param>
    /// <param name="model">The subclass and its base formatter.</param>
    /// <param name="operations">The operations requested for the subclass.</param>
    /// <returns>The complete source file.</returns>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        FormatterModel collection = model.Arguments[0];
        string type = model.Type;
        string baseType = collection.Type;
        string baseOwner = GenericOwnerFile.Owner(set.Extension!, baseType);
        return SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(type)), type, true,
            (w, context) => Write(w, context.Profile, json: false), (w, context) => Read(w, context.Profile, json: false),
            (w, context) => Write(w, context.Profile, json: true), (w, context) => Read(w, context.Profile, json: true));

        void Write(CodeWriter w, string profile, bool json)
        {
            ExplicitRegistration(w, profile, json, read: false);
            w.Line(baseOwner + ".Write<" + profile + ">(ref writer, (" + baseType + ")value);");
        }

        void Read(CodeWriter w, string profile, bool json)
        {
            ExplicitRegistration(w, profile, json, read: true);
            w.Line("var target = value;");
            w.Line("if (target is null && !reader." + (json ? "IsNull()" : "PeekIsNullCollection()") + ")");
            w.Line("    target = new " + type + "();");
            w.Line(baseType + "? items = target;");
            if (!json && model.CheckedCollectionSubclass && baseType == "global::System.Collections.Generic.List<bool>")
                w.Line(w.A("LuminPackInputPolicy") + ".ReadBooleans(ref reader, ref items);");
            else
                w.Line(baseOwner + ".Read<" + profile + ">(ref reader, ref items!);");
            w.Line("if (items is not null && !object.ReferenceEquals(items, target))");
            w.Open();
            w.Line("var destination = (" + baseType + ")target!;");
            w.Line("destination.Clear();");
            if (collection.Arguments.Length == 1)
                w.Line("destination.AddRange(items);");
            else
            {
                w.Line("foreach (var pair in items)");
                w.Line("    destination.Add(pair.Key, pair.Value);");
            }
            w.Close();
            w.Line("value = items is not null ? target : null;");
        }

        void ExplicitRegistration(CodeWriter w, string profile, bool json, bool read)
        {
            if (set.Options.RegisterMode == RegisterMode.Disabled)
                return;
            string operation = read ? "Read" : "Write";
            string kind = json ? "Json" : string.Empty;
            string state = read ? "reader" : "writer";
            w.Line("if (global::LuminPack.Code.LuminPackFormatterRegistry<global::LuminPack.Option.LuminPackFallbackOptions<" + profile +
                ", " + model.Underlying + ">>.TryGetCached" + kind + "Value<" + type + ">(out var registered) &&");
            w.Line("    registered." + operation + "Value" + kind + " is global::LuminPack.Code.LuminPack" + kind + operation + "Delegate<" + type + "> invoke)");
            w.Open();
            w.Line("invoke(ref " + state + ", " + (read ? "ref" : "in") + " value);");
            w.Line("return;");
            w.Close();
        }
    }
}
