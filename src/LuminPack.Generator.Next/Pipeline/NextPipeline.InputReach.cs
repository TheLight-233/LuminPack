using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Pipeline;

internal static partial class NextPipeline
{
    /// <summary>Finds unbounded roots whose generated nested reads can reach a formatter requiring an input check.</summary>
    private sealed class TrustedReadReach
    {
        private readonly Dictionary<string, TypeModel> _payloads = new(StringComparer.Ordinal);
        private readonly HashSet<string> _visited = new(StringComparer.Ordinal);
        private readonly bool _checkCultureInfo;

        /// <summary>Stores the optional checked culture resolver capability.</summary>
        /// <param name="checkCultureInfo">Whether a culture formatter needs the checked input path.</param>
        private TrustedReadReach(bool checkCultureInfo) => _checkCultureInfo = checkCultureInfo;

        /// <summary>Indexes named reads when the project can reach a trusted payload or culture formatter.</summary>
        /// <param name="payloads">The retained declarations and implicit payloads.</param>
        /// <param name="checkCultureInfo">Whether the project retains culture reads with a separate checked path.</param>
        /// <returns>The dependency walk, or null when no read requires checking in a copy.</returns>
        internal static TrustedReadReach? Create(List<TypeModel> payloads, bool checkCultureInfo = false)
        {
            if (!checkCultureInfo && !payloads.Exists(item => item.Policy.Input == InputPolicy.Trusted))
                return null;

            var reach = new TrustedReadReach(checkCultureInfo);
            foreach (TypeModel item in payloads)
                reach._payloads[item.Identity.GlobalName] = item;
            return reach;
        }

        /// <summary>Checks whether any declared Union case uses trusted reads.</summary>
        /// <param name="model">The Union root.</param>
        /// <returns>Whether a case bypasses the root's domain checks.</returns>
        internal static bool HasTrustedCase(UnionModel model)
        {
            foreach (UnionCaseModel item in model.Cases)
                if ((item.Closed?.Payload ?? item.Model)?.Policy.Input == InputPolicy.Trusted)
                    return true;
            return false;
        }

        /// <summary>Walks nested records and collection dependencies of an untrusted root.</summary>
        /// <param name="root">The root whose reads are being generated.</param>
        /// <returns>Whether a modeled trusted read is reachable.</returns>
        internal bool Reaches(TypeModel root)
        {
            _visited.Clear();
            return Visit(root, root.Identity.GlobalName);
        }

        /// <summary>Checks whether a named target needs an unbounded checked copy.</summary>
        /// <param name="name">The target payload's fully qualified spelling.</param>
        /// <returns>Whether its ordinary entry can bypass a domain check.</returns>
        internal bool NeedsCopy(string name) => !_payloads.TryGetValue(name, out TypeModel? payload) || Reaches(payload);

        /// <summary>Visits an owned or inline payload once in the current walk.</summary>
        /// <param name="payload">The modeled payload.</param>
        /// <param name="name">Its canonical spelling.</param>
        /// <returns>Whether the payload or a nested read is trusted.</returns>
        private bool Visit(TypeModel payload, string name)
        {
            if (!_visited.Add(name))
                return false;
            if (payload.Policy.Input == InputPolicy.Trusted)
                return true;
            foreach (MemberModel member in payload.Members)
                if (VisitMember(member))
                    return true;
            return payload is RecordModel { CollectionBase: { } collectionBase } && VisitFormatter(collectionBase);
        }

        /// <summary>Follows a member's modeled read dependencies.</summary>
        /// <param name="member">The serialized member.</param>
        /// <returns>Whether a dependent read is trusted.</returns>
        private bool VisitMember(MemberModel member) => member switch
        {
            CultureInfoMember => _checkCultureInfo,
            PayloadMember payload => VisitNamed(payload.Type.GlobalName, payload.Nested),
            CollectionMember collection => VisitNamed(collection.Element.GlobalName, collection.Nested) ||
                collection.ForwardedElement != null && VisitMember(collection.ForwardedElement),
            DictionaryMember dictionary => VisitNamed(dictionary.Key.GlobalName, null) ||
                VisitNamed(dictionary.Value.GlobalName, dictionary.Nested) ||
                dictionary.ForwardedValue != null && VisitMember(dictionary.ForwardedValue),
            SpecialMember { Formatter: { } formatter } => VisitFormatter(formatter),
            _ => false,
        };

        /// <summary>Looks up a named target when the member carries no inline model.</summary>
        /// <param name="name">The target type spelling.</param>
        /// <param name="payload">The inline model, when available.</param>
        /// <returns>Whether the named dependency reaches a trusted read.</returns>
        private bool VisitNamed(string name, TypeModel? payload) =>
            _checkCultureInfo && name is "global::System.Globalization.CultureInfo" or "System.Globalization.CultureInfo" ||
            (payload != null || _payloads.TryGetValue(name, out payload)) && Visit(payload, name);

        /// <summary>Follows the payload and arguments of a shared formatter.</summary>
        /// <param name="formatter">The closed formatter contract.</param>
        /// <returns>Whether it reaches a trusted payload.</returns>
        private bool VisitFormatter(FormatterModel formatter)
        {
            if (VisitNamed(formatter.Type, formatter.Payload))
                return true;
            foreach (FormatterModel argument in formatter.Arguments)
                if (VisitFormatter(argument))
                    return true;
            return false;
        }
    }
}
