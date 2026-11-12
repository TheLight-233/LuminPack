using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Shares one generic formatter owner between closed collections whose elements fall into the same categories.</summary>
/// <remarks>
/// <para>Each element is classified as unmanaged (the closed owner copies its storage), string (the owner keeps its string kernel)
/// or element (the owner calls the element's own formatter). A category becomes a type parameter: an unmanaged one with the
/// <c>unmanaged</c> constraint, an element one together with a struct strategy parameter that the element's generated owner
/// implements through the runtime formatter interfaces and that the owner calls as <c>default(TFormatter)</c>. Strings stay literal. Polymorphic, Boolean and nullable elements keep
/// their specialized closed owners.</para>
/// <para>The shared owner is rendered by the family's own template with placeholder type names, so the algorithm is the closed
/// algorithm. Two closed owners share it only when their placeholder renders are identical. The closed owner keeps its state
/// extensions and becomes forwarders that publication resolves to an alias of the generic instantiation.</para>
/// </remarks>
internal static class GenericFormatterReuse
{
    /// <summary>The hint prefix of shared generic owners and strategy implementations.</summary>
    internal const string HintPrefix = "LuminPack.SharedFormatters/Generic/";

    private const string JsonSuffix = "Json.json.g.cs";

    /// <summary>Whether a shared source holds JSON strategy members, which publication resolves as JSON operations.</summary>
    /// <param name="hint">The shared source hint.</param>
    /// <returns>Whether the source belongs to the JSON output.</returns>
    internal static bool IsJson(string hint) => hint.StartsWith(HintPrefix, StringComparison.Ordinal) && hint.EndsWith(JsonSuffix, StringComparison.Ordinal);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, Func<string>> ClosedSources = new();

    /// <summary>Renders the closed read of a bound owner for callers that copy and check its body.</summary>
    /// <param name="source">A published shared source.</param>
    /// <returns>The closed owner source when <paramref name="source"/> is a binding, otherwise null.</returns>
    /// <remarks>Bounded untrusted roots insert their frozen limits into a copy of the owner's read body, which a binding's
    /// forwarders do not contain.</remarks>
    internal static string? ClosedSource(string source) => ClosedSources.TryGetValue(source, out Func<string>? render) ? render() : null;

    /// <summary>The placeholder render in progress on this thread.</summary>
    [ThreadStatic] private static Context? s_current;

    /// <summary>The element category of one generic argument.</summary>
    private enum Category
    {
        None,
        Unmanaged,
        Text,
        Element,
    }

    /// <summary>A placeholder render: the generic spelling, its contract and the strategy of each placeholder.</summary>
    private sealed class Context
    {
        internal readonly string GenericKey;
        internal readonly FormatterModel Contract;
        internal readonly Dictionary<string, string> Real = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Unmanaged = new(StringComparer.Ordinal);

        internal Context(string genericKey, FormatterModel contract)
        {
            GenericKey = genericKey;
            Contract = contract;
        }
    }

    /// <summary>Returns the placeholder contract while it renders.</summary>
    /// <param name="type">The requested formatter spelling.</param>
    /// <returns>The placeholder contract, or null.</returns>
    internal static FormatterModel? FindPlaceholder(string type) =>
        s_current is { } current && GeneratedNames.PayloadKey(type) == current.GenericKey ? current.Contract : null;

    /// <summary>Maps a placeholder to the closed type it stands for while it renders, for facts that read the real type.</summary>
    /// <param name="type">A type spelling.</param>
    /// <returns>The closed type, or the spelling itself.</returns>
    internal static string Real(string type) => s_current is { } current && current.Real.TryGetValue(type, out string? real) ? real : type;

    /// <summary>Whether a spelling is an unmanaged placeholder of the render in progress.</summary>
    /// <param name="type">A type spelling.</param>
    /// <returns>Whether the binary operation copies the placeholder's storage.</returns>
    internal static bool IsUnmanagedPlaceholder(string type) => s_current?.Unmanaged.Contains(type) == true;

    /// <summary>One shared family.</summary>
    private sealed class Family
    {
        internal readonly string Definition, Name, File;
        internal readonly string[] Parameters;
        internal readonly bool[] NotNull;

        internal Family(string definition, string name, string file, string[] parameters, bool[] notNull)
        {
            Definition = definition;
            Name = name;
            File = file;
            Parameters = parameters;
            NotNull = notNull;
        }

        internal string Spell(string[] arguments) => Definition == FormatterDefinitions.Array ? arguments[0] + "[]" :
            "global::" + Definition + "<" + string.Join(", ", arguments) + ">";
    }

    private static readonly Family ArrayFamily = new(FormatterDefinitions.Array, "Array", "LuminPackGen.Array.g.cs", new[] { "T" }, new[] { false });

    /// <summary>A closed owner planned for sharing.</summary>
    private sealed class Plan
    {
        internal Family Family = null!;
        internal FormatterModel Closed = null!;
        internal Category[] Categories = null!;
        internal string BaseName = null!;
        internal GeneratedOperations Operations;
        internal Func<GeneratedOperations, string> RenderClosed = null!;
        internal Func<SharedFormatterSet, string, string[], FormatterModel, GeneratedOperations, string> RenderGeneric = null!;
        internal string Text = string.Empty;
        internal string Name = string.Empty;
    }

    /// <summary>Collects the plans of one publication and renders their files.</summary>
    internal sealed class Publication
    {
        private readonly SharedFormatterSet _set;
        private readonly int _mode;
        private readonly List<Plan> _plans = new();
        private readonly HashSet<string> _ordinary = new(StringComparer.Ordinal);

        /// <summary>Starts a publication.</summary>
        /// <param name="set">The shared formatter facts.</param>
        internal Publication(SharedFormatterSet set)
        {
            _set = set;
            _mode = set.Options?.FormatterInterfaces ?? 0;
        }

        /// <summary>Plans a closed dictionary owner.</summary>
        /// <param name="declaredType">The closed dictionary spelling.</param>
        /// <param name="operations">The owner's operations.</param>
        /// <param name="renderClosed">Renders the closed owner with the given operations.</param>
        /// <param name="family">The dictionary family descriptor.</param>
        /// <returns>Whether the owner is shared and must not be rendered closed.</returns>
        internal bool TryDictionary(string declaredType, GeneratedOperations operations, Func<GeneratedOperations, string> renderClosed,
            DictionaryFormatter family, DictionaryOwnerFact? fact = null)
        {
            if (_mode == 0 || Open(declaredType, new[] { "TKey", "TValue" }, new[] { true, false }) is not { } shape)
                return false;
            FormatterModel? model = _set.FindFormatter(declaredType);
            // A fact of raw, string or forwarded collection entries carries spellings instead of a general contract.
            if (model == null && fact.HasValue && fact.Value.Element == null && Argument(fact.Value.Key) is { } keyModel &&
                Argument(fact.Value.Value, fact.Value.ForwardedValue) is { } valueModel)
                model = new FormatterModel(declaredType, shape.Definition, new[] { keyModel, valueModel });
            if (model is not { Arguments.Length: 2 })
                return false;
            var categories = new[] { Classify(model.Arguments[0]), Classify(model.Arguments[1]) };
            if (categories[0] == Category.None || categories[1] == Category.None ||
                categories[0] == Category.Text && categories[1] == Category.Text)
                return false;
            // The closed publisher renders raw and string entries with the family's entry template and any other entry
            // through its general element template.
            bool entries = categories[0] != Category.Element && categories[1] != Category.Element;
            _plans.Add(new Plan
            {
                Family = shape,
                Closed = model,
                Categories = categories,
                Operations = operations,
                RenderClosed = renderClosed,
                BaseName = shape.Name + "Of" + Spell(categories[0]) + "And" + Spell(categories[1]),
                RenderGeneric = (set, spelling, arguments, contract, selected) => entries
                    ? family.Entries(set, new DictionaryOwnerFact(family, spelling, arguments[0], arguments[1]), selected)
                    : (family.Payload ?? family.Entries)(set, new DictionaryOwnerFact(family, spelling, arguments[0], arguments[1], contract: contract), selected),
            });
            return true;
        }

        /// <summary>Plans a closed single-rank array or registered one-argument sequence owner.</summary>
        /// <param name="declaredType">The closed collection spelling.</param>
        /// <param name="operations">The owner's operations.</param>
        /// <param name="renderClosed">Renders the closed owner with the given operations.</param>
        /// <param name="fact">The sequence fact when the owner has no general contract.</param>
        /// <returns>Whether the owner is shared and must not be rendered closed.</returns>
        internal bool TrySequence(string declaredType, GeneratedOperations operations, Func<GeneratedOperations, string> renderClosed,
            SequenceOwnerFact? fact = null)
        {
            if (_mode == 0 || fact is { ArrayRank: > 1 })
                return false;
            string key = GeneratedNames.PayloadKey(declaredType);
            Family? shape;
            SequenceFormatter? sequence = null;
            if (key.EndsWith("[]", StringComparison.Ordinal))
                shape = key.IndexOf(',') < 0 && key.Substring(0, key.Length - 2).IndexOf('[') < 0 ? ArrayFamily : null;
            else if ((shape = Open(declaredType, new[] { "T" }, new[] { false })) != null)
                sequence = FormatterRegistry.FindSequence(shape.Definition);
            if (shape == null || shape != ArrayFamily && sequence == null)
                return false;
            FormatterModel? model = _set.FindFormatter(declaredType);
            if (model == null && fact.HasValue)
            {
                SequenceOwnerFact entries = fact.Value;
                // A scalar fact always renders the raw template, whatever the element's own formatter.
                FormatterModel? element = entries.Scalar != null ? entries.Scalar == "string" ? Argument(entries.Scalar) :
                    new FormatterModel(entries.Scalar, FormatterDefinitions.Scalar, raw: true, rawCollection: true) :
                    entries.ForwardedType != null ? Argument(entries.ForwardedType, forwarded: true) :
                    entries.Element != null ? _set.FindFormatter(entries.Element.GlobalName) : null;
                if (element != null)
                    model = new FormatterModel(declaredType, shape.Definition, new[] { element }, rank: shape == ArrayFamily ? 1 : 0);
            }
            if (model is not { Arguments.Length: 1 })
                return false;
            Category category = ClassifySequence(model.Arguments[0]);
            if (category is Category.None or Category.Text)
                return false;
            _plans.Add(new Plan
            {
                Family = shape,
                Closed = model,
                Categories = new[] { category },
                Operations = operations,
                RenderClosed = renderClosed,
                BaseName = shape.Name + "Of" + Spell(category),
                // The closed publisher renders a raw element with the family's scalar template and any other element with its
                // general element template.
                RenderGeneric = (set, spelling, arguments, contract, selected) => sequence == null
                    ? category == Category.Unmanaged
                        ? ArrayScalarOwnerFile.Render(set, SequenceOwnerFact.ForScalar(FormatterRegistry.Array, spelling, arguments[0]), selected)
                        : ContiguousOwnerFile.Render(set, contract, selected, ContiguousShape.Array)
                    : category == Category.Unmanaged
                        ? sequence.Scalar(set, SequenceOwnerFact.ForScalar(sequence, spelling, arguments[0]), selected)
                        : sequence.General(set, SequenceOwnerFact.ForForwarding(sequence, spelling, arguments[0]), selected),
            });
            return true;
        }

        /// <summary>Describes the open family of a closed generic collection with the given type parameters.</summary>
        /// <param name="declaredType">The closed collection spelling.</param>
        /// <param name="parameters">The placeholder type parameters.</param>
        /// <param name="notNull">Whether each parameter needs the <c>notnull</c> constraint.</param>
        /// <returns>The family, or null for a nested or differently sized generic.</returns>
        private static Family? Open(string declaredType, string[] parameters, bool[] notNull)
        {
            string key = GeneratedNames.PayloadKey(declaredType);
            int open = key.IndexOf('<');
            if (open <= 0 || key[key.Length - 1] != '>' || GeneratedText.Split(key.Substring(open + 1, key.Length - open - 2)).Length != parameters.Length)
                return null;
            string definition = key.Substring(0, open);
            if (definition.IndexOf('>') >= 0)
                return null;
            return new Family(definition, definition.Substring(definition.LastIndexOf('.') + 1),
                "LuminPackGen." + PublicationNames.Mangle(key) + ".g.cs", parameters, notNull);
        }

        /// <summary>Models an entry spelling of a fact without a general contract.</summary>
        private FormatterModel? Argument(string type, bool forwarded = false)
        {
            if (type == "string")
                return new FormatterModel("string", FormatterDefinitions.Scalar);
            if (GenericIdentity.IsRaw(type))
                return new FormatterModel(type, FormatterDefinitions.Scalar, raw: true, rawCollection: true);
            return _set.FindFormatter(type) ?? (forwarded ? new FormatterModel(type, "$forwarded") : null);
        }

        /// <summary>Renders the shared owners, the closed bindings and the strategy implementations.</summary>
        /// <returns>Hint and source pairs; a plan whose placeholder render fails keeps its closed owner.</returns>
        internal IEnumerable<KeyValuePair<string, string>> Render()
        {
            if (_plans.Count == 0)
                yield break;
            // Groups the plans by their placeholder render and operations, in plan order.
            var groups = new Dictionary<string, List<Plan>>(StringComparer.Ordinal);
            var order = new List<List<Plan>>();
            foreach (Plan plan in _plans)
            {
                string? text = RenderPlaceholder(plan);
                if (text == null)
                {
                    yield return Closed(plan);
                    continue;
                }
                plan.Text = text;
                string identity = ((int)plan.Operations).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + text;
                if (!groups.TryGetValue(identity, out List<Plan>? group))
                {
                    groups.Add(identity, group = new List<Plan>());
                    order.Add(group);
                }
                group.Add(plan);
            }
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            var strategies = new SortedDictionary<string, (string Type, GeneratedOperations Operations)>(StringComparer.Ordinal);
            var shared = new List<KeyValuePair<string, string>>();
            foreach (List<Plan> group in order)
            {
                // A shape used by one closed owner gains nothing from a generic owner, its binding and its strategies.
                if (group.Count < 2)
                {
                    yield return Closed(group[0]);
                    continue;
                }
                Plan first = group[0];
                used.TryGetValue(first.BaseName, out int count);
                string name = count == 0 ? first.BaseName : first.BaseName + (count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string text = count == 0 ? first.Text : first.Text.Replace("LuminRoots_" + first.BaseName + "<", "LuminRoots_" + name + "<");
                var bound = new List<(Plan Plan, string Binding, SortedDictionary<string, (string Type, GeneratedOperations Operations)> Required)>();
                foreach (Plan plan in group)
                {
                    plan.Name = name;
                    plan.Text = text;
                    var required = new SortedDictionary<string, (string Type, GeneratedOperations Operations)>(StringComparer.Ordinal);
                    if (RenderBinding(plan, Instantiation(plan, required)) is { } binding)
                        bound.Add((plan, binding, required));
                    else
                        yield return Closed(plan);
                }
                if (bound.Count < 2)
                {
                    foreach (var item in bound)
                        yield return Closed(item.Plan);
                    continue;
                }
                used[first.BaseName] = count + 1;
                shared.Add(new KeyValuePair<string, string>(HintPrefix + first.Family.File + "/" + name + ".g.cs", text));
                foreach (var item in bound)
                {
                    Plan closedPlan = item.Plan;
                    ClosedSources.Add(item.Binding, () => closedPlan.RenderClosed(GeneratedOperations.BinaryRead));
                    foreach (var strategy in item.Required)
                        strategies[strategy.Key] = strategies.TryGetValue(strategy.Key, out var prior)
                            ? (strategy.Value.Type, prior.Operations | strategy.Value.Operations) : strategy.Value;
                    shared.Add(new KeyValuePair<string, string>("LuminPack.SharedFormatters/Bound_" +
                        ReadableTypeName.Of(GeneratedNames.PayloadKey(closedPlan.Closed.Type)) + ".g.cs", item.Binding));
                }
            }
            foreach (var source in shared)
                yield return source;
            if (strategies.Count != 0)
            {
                string binary = RenderStrategies(strategies, json: false), json = RenderStrategies(strategies, json: true);
                if (binary.Length != 0)
                    yield return new KeyValuePair<string, string>(HintPrefix + "LuminPackGen.FormatterStrategies.g.cs/Binary.g.cs", binary);
                if (json.Length != 0)
                    yield return new KeyValuePair<string, string>(HintPrefix + "LuminPackGen.FormatterStrategies.g.cs/" + JsonSuffix, json);
            }

            static KeyValuePair<string, string> Closed(Plan plan) => new("LuminPack.SharedFormatters/Closed_" +
                ReadableTypeName.Of(GeneratedNames.PayloadKey(plan.Closed.Type)) + ".g.cs", plan.RenderClosed(plan.Operations));
        }

        /// <summary>Renders the generic owner with placeholders, or returns null when the render still names a closed type.</summary>
        private string? RenderPlaceholder(Plan plan)
        {
            Family family = plan.Family;
            int count = family.Parameters.Length;
            var arguments = new string[count];
            var placeholders = new FormatterModel[count];
            var parameters = new List<string>();
            var constraints = new List<string>();
            var strategies = new List<(string Placeholder, string Strategy)>();
            for (int index = 0; index < count; index++)
            {
                FormatterModel argument = plan.Closed.Arguments[index];
                string parameter = family.Parameters[index];
                switch (plan.Categories[index])
                {
                    case Category.Text:
                        arguments[index] = "string";
                        placeholders[index] = argument;
                        continue;
                    case Category.Unmanaged:
                        arguments[index] = parameter;
                        placeholders[index] = new FormatterModel(parameter, argument.Definition, raw: true, rawCollection: argument.RawStorage);
                        parameters.Add(parameter);
                        constraints.Add("where " + parameter + " : unmanaged");
                        GeneratedOperations json = plan.Operations & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead);
                        if (json != GeneratedOperations.None)
                            strategies.Add((parameter, parameter + "Formatter"));
                        break;
                    default:
                        arguments[index] = parameter;
                        placeholders[index] = new FormatterModel(parameter, "$strategy", ordinaryObject: argument.OrdinaryObject);
                        parameters.Add(parameter);
                        if (family.NotNull[index])
                            constraints.Add("where " + parameter + " : notnull");
                        strategies.Add((parameter, parameter + "Formatter"));
                        break;
                }
            }
            foreach (var (placeholder, strategy) in strategies)
            {
                parameters.Add(strategy);
                GeneratedOperations required = plan.Categories[Array.IndexOf(family.Parameters, placeholder)] == Category.Unmanaged
                    ? plan.Operations & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead) : plan.Operations;
                constraints.Add("where " + strategy + " : struct, " + string.Join(", ", Interfaces(placeholder, required)));
            }
            string spelling = family.Spell(arguments);
            var contract = new FormatterModel(spelling, plan.Closed.Definition, placeholders, plan.Closed.Raw, plan.Closed.RawCollection,
                rank: plan.Closed.Rank, reuseArraySlots: plan.Closed.ReuseArraySlots, capabilities: plan.Closed.Capabilities);
            var context = new Context(GeneratedNames.PayloadKey(spelling), contract);
            for (int index = 0; index < count; index++)
                if (plan.Categories[index] != Category.Text)
                {
                    context.Real[family.Parameters[index]] = plan.Closed.Arguments[index].Type;
                    if (plan.Categories[index] == Category.Unmanaged)
                        context.Unmanaged.Add(family.Parameters[index]);
                }
            string declaration = "internal static partial class LuminRoots_" + plan.BaseName + "<" + string.Join(", ", parameters) + "> " +
                string.Join(" ", constraints);
            string text;
            Context? prior = s_current;
            FormatterOwnerFile.Projection? projection = FormatterOwnerFile.Current;
            s_current = context;
            FormatterOwnerFile.Current = new FormatterOwnerFile.Projection(declaration, null);
            try
            {
                text = plan.RenderGeneric(_set, spelling, arguments, contract, plan.Operations);
            }
            finally
            {
                s_current = prior;
                FormatterOwnerFile.Current = projection;
            }
            if (text.Length == 0)
                return null;
            text = NoInliningRoots(text);
            string state = "global::LuminPack.Binary_" + _set.Extension + "_";
            // The template names its own roots after the placeholder spelling; inside the generic owner they are unqualified.
            string own = ReadableTypeName.Of(GeneratedNames.PayloadKey(spelling));
            text = text.Replace("global::LuminPack.LuminPackSerializer.LuminRoots_" + own + ".", string.Empty)
                .Replace("LuminPackSerializer.LuminRoots_" + own + ".", string.Empty).Replace("LuminRoots_" + own + ".", string.Empty);
            if (text.Contains(own))
                return Reject(plan, "own name", text);
            foreach (var (placeholder, strategy) in strategies)
                text = text.Replace(state + ReadableTypeName.Of(placeholder) + ".", "default(" + strategy + ").");
            // An owner named after a placeholder spelling, such as the concrete dictionary a dictionary interface forwards to, has
            // no generic counterpart.
            if (PlaceholderOwner.IsMatch(text))
                return Reject(plan, "placeholder owner", text);
            // A placeholder that still reaches a closed owner, or a closed element spelling, cannot be shared.
            foreach (string parameter in family.Parameters)
                if (text.Contains(state + ReadableTypeName.Of(parameter) + "."))
                    return Reject(plan, "closed placeholder owner " + parameter, text);
            for (int index = 0; index < count; index++)
            {
                string real = plan.Closed.Arguments[index].Type;
                if (plan.Categories[index] != Category.Text && real.StartsWith("global::", StringComparison.Ordinal) && text.Contains(real))
                    return Reject(plan, "closed spelling " + real, text);
            }
            return text;
        }

        /// <summary>Spells the generic instantiation of a plan and records the strategies it needs.</summary>
        private string Instantiation(Plan plan, SortedDictionary<string, (string Type, GeneratedOperations Operations)> strategies)
        {
            var arguments = new List<string>();
            var formatters = new List<string>();
            for (int index = 0; index < plan.Categories.Length; index++)
            {
                if (plan.Categories[index] == Category.Text)
                    continue;
                string type = plan.Closed.Arguments[index].Type;
                arguments.Add(type);
                GeneratedOperations required = plan.Categories[index] == Category.Unmanaged
                    ? plan.Operations & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead) : plan.Operations;
                if (required == GeneratedOperations.None)
                    continue;
                string readable = Readable(type);
                formatters.Add("global::LuminPack.LuminPackSerializer." + readable + "Formatter");
                strategies[readable] = strategies.TryGetValue(readable, out var prior) ? (type, prior.Operations | required) : (type, required);
                if (plan.Closed.Arguments[index].OrdinaryObject)
                    _ordinary.Add(readable);
            }
            arguments.AddRange(formatters);
            return "global::LuminPack.LuminPackSerializer." + plan.Name + "Formatter<" + string.Join(", ", arguments) + ">";
        }

        /// <summary>Renders the closed owner's state extensions with roots that forward to the generic instantiation.</summary>
        private string? RenderBinding(Plan plan, string instantiation)
        {
            var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < plan.Categories.Length; index++)
                if (plan.Categories[index] != Category.Text)
                    substitutions[plan.Family.Parameters[index]] = plan.Closed.Arguments[index].Type;
            var lines = new List<string>();
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            var forwarded = new HashSet<string>(StringComparer.Ordinal);
            string root = "LuminRoots_" + plan.Name;
            foreach (GeneratedText.Method method in GeneratedText.ReadMethods(plan.Text))
            {
                if (method.Owner != root || !method.Name.StartsWith("Root", StringComparison.Ordinal) ||
                    !method.Signature.TrimStart().StartsWith("internal ", StringComparison.Ordinal))
                    continue;
                string signature = Words.Replace(method.Signature, match =>
                {
                    aliases.Add(match.Value);
                    return substitutions.TryGetValue(match.Value, out string? closed) ? closed : match.Value;
                });
                var arguments = new string[method.Parameters.Length];
                for (int index = 0; index < arguments.Length; index++)
                {
                    string parameter = method.Parameters[index];
                    string name = GeneratedText.ParameterName(parameter);
                    string modifiers = Regex.Replace(parameter, @"^(?:scoped\s+)?", string.Empty);
                    arguments[index] = modifiers.StartsWith("ref ", StringComparison.Ordinal) ? "ref " + name :
                        modifiers.StartsWith("in ", StringComparison.Ordinal) ? "in " + name :
                        modifiers.StartsWith("out ", StringComparison.Ordinal) ? "out " + name : name;
                }
                string generics = method.Generics.Length == 0 ? string.Empty : "<" + string.Join(", ", method.Generics) + ">";
                forwarded.Add(method.Name);
                lines.Add("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
                foreach (string line in signature.Split('\n'))
                    lines.Add(line.Trim());
                lines.Add("    => " + instantiation + "." + method.Name.Substring(4).Replace("Union", "Payload").Replace("Member", "Members") + generics + "(" + string.Join(", ", arguments) + ");");
                lines.Add(string.Empty);
            }
            FormatterOwnerFile.Projection? projection = FormatterOwnerFile.Current;
            FormatterOwnerFile.Current = new FormatterOwnerFile.Projection(null, lines) { Aliases = aliases };
            string text;
            try
            {
                text = plan.RenderClosed(plan.Operations);
            }
            finally
            {
                FormatterOwnerFile.Current = projection;
            }
            // Entries and extensions that reach a closed root the shared owner lacks keep the closed owner.
            string closed = "LuminRoots_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(plan.Closed.Type)) + ".";
            for (int index = text.IndexOf(closed, StringComparison.Ordinal); index >= 0; index = text.IndexOf(closed, index + 1, StringComparison.Ordinal))
            {
                int start = index + closed.Length, end = start;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                    end++;
                if (!forwarded.Contains(text.Substring(start, end - start)))
                    return null;
            }
            return text;
        }

        /// <summary>Implements the formatter interfaces on each element owner.</summary>
        /// <remarks>Each member makes the element call the closed collection owner makes, which publication resolves to the
        /// element's final operation exactly as for the closed owner. JSON members are published in a JSON document so their
        /// state calls resolve to the JSON operations.</remarks>
        private string RenderStrategies(SortedDictionary<string, (string Type, GeneratedOperations Operations)> strategies, bool json)
        {
            var text = new StringBuilder(4096);
            text.Append("namespace LuminPack\n{\n    public static partial class LuminPackSerializer\n    {\n");
            const string inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
            const string modifier = "void ";
            string scoped = _set.Options?.CSharpVersion is int version && version >= 1100 ? "scoped " : string.Empty;
            GeneratedOperations selected = json ? GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead :
                GeneratedOperations.BinaryWrite | GeneratedOperations.BinaryRead;
            bool first = true;
            foreach (var strategy in strategies)
            {
                GeneratedOperations operations = strategy.Value.Operations & selected;
                if (operations == GeneratedOperations.None)
                    continue;
                if (!first)
                    text.Append('\n');
                first = false;
                string type = strategy.Value.Type, state = "global::LuminPack.Binary_" + _set.Extension + "_" + strategy.Key;
                text.Append("        internal static partial class ").Append(strategy.Key).Append("Formatter : ")
                    .Append(string.Join(", ", Interfaces(type, operations))).Append("\n        {\n");
                void Member(GeneratedOperations operation, string contract, string method, string stateType, string parameters, string arguments,
                    string? prefix = null)
                {
                    if ((operations & operation) == 0)
                        return;
                    text.Append("            ").Append(inline).Append('\n');
                    text.Append("            ").Append(modifier).Append("global::LuminPack.Code.").Append(contract).Append('<').Append(type).Append(">.")
                        .Append(method).Append("<TOptions>(").Append(scoped).Append("ref ").Append(stateType).Append(parameters).Append(')');
                    if (prefix == null)
                        text.Append("\n                => ").Append(state).Append('.').Append(method).Append("<TOptions>(").Append(arguments).Append(");\n");
                    else
                        text.Append("\n            {\n                ").Append(prefix).Append("\n                ").Append(state).Append('.').Append(method)
                            .Append("<TOptions>(").Append(arguments).Append(");\n            }\n");
                }
                Member(GeneratedOperations.BinaryWrite, "ILuminPackWriter", "Write", "global::LuminPack.Core.LuminPackWriter writer", ", in " + type + " value", "ref writer, in value");
                // As the closed element reads do, a null marker clears an ordinary object before its own read consumes the marker.
                Member(GeneratedOperations.BinaryRead, "ILuminPackReader", "Read", "global::LuminPack.Core.LuminPackReader reader", ", " + scoped + "ref " + type + " value",
                    "ref reader, ref value", _ordinary.Contains(strategy.Key) ? "if (reader.PeekIsNullObject()) value = default!;" : null);
                Member(GeneratedOperations.JsonWrite, "ILuminPackJsonWriter", "Write", "global::LuminPack.Core.LuminPackJsonWriter writer", ", in " + type + " value", "ref writer, value");
                Member(GeneratedOperations.JsonRead, "ILuminPackJsonReader", "Read", "global::LuminPack.Core.LuminPackJsonReader reader", ", " + scoped + "ref " + type + " value", "ref reader, ref value");
                text.Append("        }\n");
            }
            text.Append("    }\n}\n");
            return first ? string.Empty : text.ToString();
        }

        /// <summary>The element owner name the state extensions and formatter owner share.</summary>
        private string Readable(string type)
        {
            string owner = GeneratedNames.Owner(_set.Extension!, type);
            return owner.Substring(("Binary_" + _set.Extension + "_").Length);
        }
    }

    /// <summary>Rejects a placeholder render; the reason documents the closed spelling that keeps the owner closed.</summary>
    private static string? Reject(Plan plan, string reason, string text) => null;

    /// <summary>Keeps every root of a shared collection owner out of its callers.</summary>
    /// <param name="text">The placeholder owner render.</param>
    /// <returns>The render with <c>NoInlining</c> on each binary and JSON root.</returns>
    /// <remarks>LuminPack trades one call into a collection for inlining inside it: an inlined collection root would spend the
    /// caller's inline budget on the loop and leave the element calls in the loop uninlined. Local helpers and private
    /// continuations keep their own attributes.</remarks>
    private static string NoInliningRoots(string text)
    {
        const string attribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";
        string[] lines = text.Split('\n');
        var keep = new bool[lines.Length];
        var replace = new string?[lines.Length];
        for (int index = 0; index < lines.Length; index++)
        {
            keep[index] = true;
            string trimmed = lines[index].TrimStart();
            if (!trimmed.StartsWith("internal static void Root", StringComparison.Ordinal))
                continue;
            string indent = lines[index].Substring(0, lines[index].Length - trimmed.Length);
            bool attributed = false;
            for (int prior = index - 1; prior >= 0 && lines[prior].TrimStart().StartsWith("[", StringComparison.Ordinal); prior--)
                if (lines[prior].Contains("MethodImpl"))
                {
                    if (attributed)
                        keep[prior] = false;
                    else
                        replace[prior] = indent + attribute;
                    attributed = true;
                }
            if (!attributed)
                replace[index] = indent + attribute + "\n" + lines[index];
        }
        var result = new StringBuilder(text.Length + 512);
        for (int index = 0; index < lines.Length; index++)
        {
            if (!keep[index])
                continue;
            result.Append(replace[index] ?? lines[index]);
            if (index != lines.Length - 1)
                result.Append('\n');
        }
        return result.ToString();
    }

    private static readonly Regex PlaceholderOwner = new(@"\b(?:Binary|LuminRoots)_\w*?(?:Of|And)(?:TKey|TValue|T)(?:And|Array|\b)", RegexOptions.Compiled);

    private static readonly Regex Words = new(@"(?<![\w@.:])[A-Za-z_@][\w@]*", RegexOptions.Compiled);

    /// <summary>Lists the runtime interfaces of the requested operations.</summary>
    private static IEnumerable<string> Interfaces(string type, GeneratedOperations operations)
    {
        if ((operations & GeneratedOperations.BinaryWrite) != 0)
            yield return "global::LuminPack.Code.ILuminPackWriter<" + type + ">";
        if ((operations & GeneratedOperations.BinaryRead) != 0)
            yield return "global::LuminPack.Code.ILuminPackReader<" + type + ">";
        if ((operations & GeneratedOperations.JsonWrite) != 0)
            yield return "global::LuminPack.Code.ILuminPackJsonWriter<" + type + ">";
        if ((operations & GeneratedOperations.JsonRead) != 0)
            yield return "global::LuminPack.Code.ILuminPackJsonReader<" + type + ">";
    }

    private static string Spell(Category category) => category switch
    {
        Category.Unmanaged => "Unmanaged",
        Category.Text => "String",
        _ => "Element",
    };

    private static readonly Regex Boolean = new(@"\bbool\b", RegexOptions.Compiled);

    /// <summary>Classifies a contiguous element: unmanaged storage keeps the closed raw block, an element keeps the closed
    /// element-wise traversal; tagged or packed storage and nested arrays keep their closed owners.</summary>
    private static Category ClassifySequence(FormatterModel model)
    {
        Category category = Classify(model);
        if (category == Category.None || category == Category.Text)
            return category;
        // The closed sequence routes copy any raw-collection element as one block; other raw or laid-out elements use the
        // tagged or packed transfer, which stays closed.
        if (model.RawCollection)
            return Category.Unmanaged;
        if (model.Layout != null || model.Raw || model.Definition == FormatterDefinitions.Array)
            return Category.None;
        return Category.Element;
    }

    /// <summary>Classifies one closed argument; the unmanaged rule is the closed owners' native-entry rule.</summary>
    private static Category Classify(FormatterModel model)
    {
        if (model.Type == "string")
            return Category.Text;
        string key = GeneratedNames.PayloadKey(model.Type);
        if (Boolean.IsMatch(key) || key == "object" || model.Payload is UnionModel || model.Rank > 1 ||
            model.Definition is FormatterDefinitions.Registered or FormatterDefinitions.GenericUnion or FormatterDefinitions.Object ||
            model.Definition == GenericIdentity.Nullable || key.IndexOf('*') >= 0 || model.Type.EndsWith("?", StringComparison.Ordinal))
            return Category.None;
        // Tagged and packed storage plans and other raw values that are not plain storage copies keep their closed layout rules.
        if (model.Layout != null)
            return Category.None;
        if (model.Raw)
            return model.Payload == null || model.RawStorage ? Category.Unmanaged : Category.None;
        return Category.Element;
    }
}
