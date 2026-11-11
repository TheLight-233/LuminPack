using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
internal readonly partial struct SharedFormatterSet
{
    /// <summary>The default closed contracts reached through recursive static element operations.</summary>
    internal readonly FormatterModel[] GeneralOwners;
    /// <summary>Finds a concrete element contract after dependency collection.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <returns>The normalized result of the operation.</returns>
    internal FormatterModel? FindFormatter(string type)
    {
        foreach (FormatterModel model in GeneralOwners ?? Array.Empty<FormatterModel>())
            if (GeneratedNames.PayloadKey(model.Type) == GeneratedNames.PayloadKey(type))
                return model;
        return null;
    }

    /// <summary>Collects general formatter requirements from every member and nested payload.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "formatters">The formatters used by this operation.</param>
    /// <param name = "payloads">The payloads used by this operation.</param>
    private static void CollectGeneral(TypeModel model, Dictionary<string, FormatterModel> formatters, HashSet<string> payloads)
    {
        if (!payloads.Add(model.Identity.GlobalName))
            return;
        foreach (MemberModel member in model.Members)
        {
            if (member is SpecialMember { Formatter: { } formatter })
                CollectFormatter(formatter, formatters, payloads);
            if (member is PayloadMember { Nested: { } nested })
                CollectGeneral(nested, formatters, payloads);
            if (member is CollectionMember { Nested: { } element })
                CollectGeneral(element, formatters, payloads);
            if (member is DictionaryMember { Nested: { } value })
                CollectGeneral(value, formatters, payloads);
        }

        if (model is UnionModel union)
            foreach (UnionCaseModel item in union.Cases)
                if (item.Model != null)
                    CollectGeneral(item.Model, formatters, payloads);
    }

    /// <summary>Visits each closed contract once, retaining a complete definition when a cycle previously contributed a reference.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "formatters">The formatters used by this operation.</param>
    /// <param name = "payloads">The payloads used by this operation.</param>
    private static void CollectFormatter(FormatterModel model, Dictionary<string, FormatterModel> formatters, HashSet<string> payloads)
    {
        if (formatters.TryGetValue(model.Type, out FormatterModel? prior) && (prior.Payload != null || model.Payload == null))
            return;
        formatters[model.Type] = model;
        foreach (FormatterModel argument in model.Arguments)
            CollectFormatter(argument, formatters, payloads);
        if (model.Payload != null)
            CollectGeneral(model.Payload, formatters, payloads);
        if (model.Definition == GenericIdentity.Grouping)
            CollectFormatter(new FormatterModel(GroupingOwnerFile.Sequence(model.Arguments[1].Type), "System.Collections.Generic.IEnumerable", new[] { model.Arguments[1] }), formatters, payloads);
        if (model.Definition == GenericIdentity.Lookup)
            CollectFormatter(new FormatterModel("global::System.Linq.IGrouping<" + model.Arguments[0].Type + ", " + model.Arguments[1].Type + ">", GenericIdentity.Grouping, model.Arguments), formatters, payloads);
        if (model.Definition is "System.Collections.ObjectModel.ReadOnlyDictionary" or "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary")
            CollectFormatter(new FormatterModel("global::System.Collections.Generic.Dictionary<" + model.Arguments[0].Type + ", " + model.Arguments[1].Type + ">", "System.Collections.Generic.Dictionary", model.Arguments), formatters, payloads);
    }
}
