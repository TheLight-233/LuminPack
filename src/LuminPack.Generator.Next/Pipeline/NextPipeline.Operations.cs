using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;

namespace LuminPack.SourceGenerator.Next.Pipeline;

internal static partial class NextPipeline
{
    /// <summary>The operations each payload and each shared formatter owner keeps.</summary>
    /// <remarks>Each closed call keeps its operation on the payload identities it reaches, and each reached declaration passes
    /// its operations on to the identities it reaches in turn. An operation only calls the same operation of the payloads and
    /// owners it reaches, so every kept operation finds its callees: a shared owner keeps an operation when a payload or root
    /// that keeps it requires the owner. A payload that keeps nothing publishes no file.</remarks>
    private sealed class OperationPlan
    {
        private static readonly GeneratedOperations[] Single =
            { GeneratedOperations.BinaryWrite, GeneratedOperations.BinaryRead, GeneratedOperations.JsonWrite, GeneratedOperations.JsonRead };

        private readonly Dictionary<string, GeneratedOperations>? _reached;

        /// <summary>The operations every payload keeps.</summary>
        private readonly GeneratedOperations _whole;

        /// <summary>The operations each shared owner keeps.</summary>
        internal readonly OwnerOperations Owners;

        private OperationPlan(GeneratedOperations whole, Dictionary<string, GeneratedOperations>? reached, OwnerOperations owners)
        {
            _whole = whole;
            _reached = reached;
            Owners = owners;
        }

        /// <summary>Selects the operations of one payload.</summary>
        /// <param name="model">The normalized payload model.</param>
        /// <returns>The operations its files implement.</returns>
        internal GeneratedOperations For(TypeModel model) =>
            _reached == null ? _whole :
            _reached.TryGetValue(model.Identity.MetadataName, out GeneratedOperations reached) ? _whole | reached : _whole;

        /// <summary>Closes the called operations over the reached payload identities and shared owners.</summary>
        /// <param name="payloads">The normalized declaration results.</param>
        /// <param name="calls">The bound serializer calls.</param>
        /// <param name="whole">The operations every payload keeps regardless of calls.</param>
        /// <param name="options">The normalized project settings.</param>
        /// <returns>The operation plan.</returns>
        internal static OperationPlan Create(ImmutableArray<ModeledPayload> payloads, ImmutableArray<SerializerCallModel> calls,
            GeneratedOperations whole, GeneratorOptions options)
        {
            if (whole == GeneratedOperations.All)
                return new OperationPlan(whole, null, new OwnerOperations(whole));

            var byPayload = new Dictionary<PayloadBinding, GeneratedOperations>();
            GeneratedOperations called = GeneratedOperations.None;
            foreach (SerializerCallModel call in calls)
            {
                GeneratedOperations operation = OperationUsageAnalysis.Of(call) & ~whole;
                if (operation == GeneratedOperations.None)
                    continue;
                called |= operation;
                byPayload.TryGetValue(call.Binding, out GeneratedOperations previous);
                if ((previous & operation) == 0)
                    byPayload[call.Binding] = previous | operation;
            }
            if (byPayload.Count == 0)
                return new OperationPlan(whole, null, new OwnerOperations(whole));
            var edges = new Dictionary<string, OperationReach>(payloads.Length, StringComparer.Ordinal);
            var derived = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (ModeledPayload payload in payloads)
                if (payload.Result.IsComplete && payload.Result.Model is { } model)
                {
                    string key = model.Identity.MetadataName;
                    edges[key] = payload.Reach;
                    foreach (string baseKey in payload.Reach.Bases)
                    {
                        if (!derived.TryGetValue(baseKey, out List<string>? cases))
                            derived[baseKey] = cases = new List<string>();
                        cases.Add(key);
                    }
                }
            var reached = new Dictionary<string, GeneratedOperations>(edges.Count, StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (var call in byPayload)
                Follow(call.Key.Reach.Keys, call.Value);
            // Every payload and owner already retains the global mask. Only payload-specific bits need to traverse edges.
            while (pending.Count != 0)
            {
                string key = pending.Dequeue();
                GeneratedOperations operations = reached[key];
                if (edges.TryGetValue(key, out OperationReach? reach))
                    Follow(reach.Keys, operations);
                if (derived.TryGetValue(key, out List<string>? cases))
                    foreach (string item in cases)
                        Raise(item, operations);
            }
            var plan = new OperationPlan(whole, reached, new OwnerOperations(whole));

            // A shared owner keeps each operation of the payloads and roots that require it.
            var owners = new Dictionary<string, GeneratedOperations>(StringComparer.Ordinal);
            var models = new List<TypeModel>(edges.Count);
            var roots = new List<MemberModel>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (GeneratedOperations operation in Single)
            {
                if ((called & operation) == 0)
                    continue;
                models.Clear();
                roots.Clear();
                keys.Clear();
                foreach (ModeledPayload payload in payloads)
                    if (payload.Result.IsComplete && payload.Result.Model is { } model && (plan.For(model) & operation) != 0)
                        models.Add(model);
                foreach (var call in byPayload)
                    if (call.Key.Root != null && (call.Value & operation) != 0)
                        roots.Add(call.Key.Root);
                SharedFormatterSet.CollectKeys(models, options, roots, keys);
                foreach (string key in keys)
                {
                    owners.TryGetValue(key, out GeneratedOperations previous);
                    owners[key] = previous | operation;
                }
            }
            return new OperationPlan(whole, reached, new OwnerOperations(whole, owners));

            void Follow(string[] keys, GeneratedOperations operations)
            {
                foreach (string key in keys)
                    Raise(key, operations);
            }

            void Raise(string key, GeneratedOperations operations)
            {
                reached.TryGetValue(key, out GeneratedOperations previous);
                GeneratedOperations next = previous | operations;
                if (next == previous)
                    return;
                reached[key] = next;
                pending.Enqueue(key);
            }
        }
    }
}
