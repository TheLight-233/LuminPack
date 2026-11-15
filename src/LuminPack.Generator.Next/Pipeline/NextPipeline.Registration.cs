using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Facade;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

namespace LuminPack.SourceGenerator.Next.Pipeline;

internal static partial class NextPipeline
{
    private static readonly Regex NamedStateEntry = new(
        @"(?m)^[ \t]*internal static void (Serialize|Deserialize|SerializeJson|DeserializeJson)(_\w+)<", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Binds the already selected concrete operations to typed registration delegates.</summary>
    /// <param name="sources">The raw contributions before state forwarding is compacted.</param>
    /// <param name="payloads">The modeled declarations and Custom registrations.</param>
    /// <param name="calls">The closed serializer uses.</param>
    /// <param name="formatters">The closed shared formatter owners.</param>
    /// <param name="shared">The shared owners with unambiguous published root names.</param>
    /// <param name="plan">The operations retained by each owner and declaration.</param>
    /// <param name="options">The consuming assembly settings.</param>
    /// <param name="policy">The consuming assembly policy.</param>
    private static void AddFormatterRegistration(List<GeneratedSource> sources, ImmutableArray<ModeledPayload> payloads,
        ImmutableArray<SerializerCallModel> calls, SharedFormatterSet formatters, HashSet<string> shared, OperationPlan plan,
        GeneratorOptions options, WirePolicy policy)
    {
        var custom = new List<string>();
        var generated = new SortedDictionary<string, (string Type, string BinarySuffix, string JsonSuffix, string Arguments, GeneratedOperations Operations)>(StringComparer.Ordinal);
        var entries = new Dictionary<string, GeneratedOperations>(StringComparer.Ordinal);
        foreach (GeneratedSource source in sources)
            foreach (Match match in NamedStateEntry.Matches(source.Text))
            {
                GeneratedOperations operation = match.Groups[1].Value switch
                {
                    "Serialize" => GeneratedOperations.BinaryWrite,
                    "Deserialize" => GeneratedOperations.BinaryRead,
                    "SerializeJson" => GeneratedOperations.JsonWrite,
                    _ => GeneratedOperations.JsonRead,
                };
                string suffix = match.Groups[2].Value;
                entries.TryGetValue(suffix, out GeneratedOperations existing);
                entries[suffix] = existing | operation;
            }
        var open = new Dictionary<string, TypeModel>(StringComparer.Ordinal);
        foreach (ModeledPayload payload in payloads)
        {
            if (payload.CustomRegistration != null)
                custom.Add(payload.CustomRegistration);
            if (!payload.Result.IsComplete || payload.Result.Model is not { } model)
                continue;
            string type = model.Identity.GlobalName;
            if (model.Identity.IsGeneric)
            {
                open[type] = model;
                continue;
            }
            if (model is UnionModel && RenderModelFactory.Create(model, options) is { } union)
                custom.Add("global::LuminPack.Code.LuminPackUnionFormatter<" + type + ">.ReserveTags(new int[] { " +
                    string.Join(", ", union.UnionCases.Select(item => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).Distinct()) + " });");
            AddPayload(model, type);
        }

        foreach (SerializerCallModel call in calls)
            if (call.Definition is { } definition && open.TryGetValue(definition, out TypeModel? model))
                AddPayload(model, call.Payload, arguments: call.TypeArguments + ", ");

        foreach (FormatterModel formatter in formatters.GeneralOwners ?? Array.Empty<FormatterModel>())
        {
            if (formatter.Definition == FormatterDefinitions.PayloadReference)
            {
                if (formatter.PublishPayload && formatter.Payload is { } closed)
                    AddPayload(closed, formatter.Type, plan.Owners.For(GeneratedNames.PayloadKey(formatter.Type)));
                continue;
            }
            if (formatter.Definition == FormatterDefinitions.GenericUnion && formatter.Underlying is { } unionBinding)
            {
                string[] parts = unionBinding.Split('|');
                Add(formatter.Type, "_" + parts[0], parts[1] + ", ", plan.Owners.For(GeneratedNames.PayloadKey(formatter.Type)));
            }
            else
                AddOwner(formatter.Type);
        }
        foreach (string owner in formatters.Owners ?? Array.Empty<string>())
            AddOwner(owner);
        foreach (string scalar in formatters.Scalars ?? Array.Empty<string>())
            AddOwner(scalar.Split('=')[0]);
        foreach (SequenceOwnerFact sequence in formatters.SequenceOwners ?? Array.Empty<SequenceOwnerFact>())
            AddOwner(sequence.DeclaredType);
        foreach (DictionaryOwnerFact dictionary in formatters.DictionaryOwners ?? Array.Empty<DictionaryOwnerFact>())
            AddOwner(dictionary.DeclaredType);

        sources.Add(new GeneratedSource("LuminPackGen.CustomFormatterRegistry.g.cs",
            CustomRegistrationSource.Render(custom, generated.Values, ProfileNames.Type(policy.Strings), ProfileNames.Type(policy.JsonInput),
                options.AssemblyName, options.KnownFormatters), GenerationKind.Formatter));

        void AddPayload(TypeModel model, string type, GeneratedOperations? selected = null, string arguments = "")
        {
            GeneratedOperations operations = selected ?? plan.For(model);
            if (operations == GeneratedOperations.None)
                return;
            string suffix = "_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Identity.GlobalName));
            bool named = options.Interceptors || model is UnionModel;
            if (model is StructModel { StoragePlan: not null })
            {
                // The binary storage owner uses a named entry even on the overload backend.
                Add(type, suffix, arguments, operations, named ? suffix : string.Empty);
                return;
            }
            if (RenderModelFactory.Create(model, options) != null)
                Add(type, named ? suffix : string.Empty, arguments, operations);
        }

        void AddOwner(string type)
        {
            string key = GeneratedNames.PayloadKey(type);
            string suffix = "_" + ReadableTypeName.Of(key);
            if (!shared.Contains(key) || options.KnownFormatters && IsKnown(key, options.PointerCursor || options.SpanCursor) ||
                !entries.TryGetValue(suffix, out GeneratedOperations emitted))
                return;
            string spelling = key.IndexOf('.') >= 0 && !type.StartsWith("global::", StringComparison.Ordinal) ? "global::" + type : type;
            Add(spelling, suffix, "", plan.Owners.For(key) & emitted);
        }

        void Add(string type, string suffix, string arguments, GeneratedOperations operations, string? jsonSuffix = null)
        {
            if (operations == GeneratedOperations.None)
                return;
            string key = GeneratedNames.CanonicalKey(type);
            if (!generated.ContainsKey(key))
                generated.Add(key, (type, suffix, jsonSuffix ?? suffix, arguments, operations));
        }
    }

    /// <summary>Matches the fixed scalar types registered by Core.</summary>
    /// <param name="type">The normalized type spelling.</param>
    /// <param name="portable">Whether Core is the netstandard2.1 asset.</param>
    /// <returns>Whether Core owns the registration.</returns>
    private static bool IsKnown(string type, bool portable)
    {
        if (type is "string" or "sbyte" or "byte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
            "float" or "double" or "decimal" or "bool" or "char" or "nint" or "nuint")
            return true;
        if (portable && type is ("System.Half" or "System.Int128" or "System.UInt128" or "System.DateOnly" or "System.TimeOnly" or "System.Text.Rune"))
            return false;
        return FrameworkScalarBody.Contains("global::" + type);
    }
}
