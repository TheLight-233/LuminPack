using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Shares a prepared dependency version between the two publishers for the exact current compilation.</summary>
internal sealed class CurrentSourceEmission<T> where T : class
{
    private readonly ConditionalWeakTable<Compilation, Dictionary<string, List<(MetaInfo Metadata, bool Json, Lazy<T> Output)>>> _values = new();

    /// <summary>Prepares one dependency version while both publishers are processing the same input.</summary>
    /// <param name="compilation">The input compilation before either publisher adds its sources.</param>
    /// <param name="name">The complete declaration identity, or the shared aggregate identity.</param>
    /// <param name="metadata">The exact generation settings.</param>
    /// <param name="json">Which of the two publishers is requesting the contribution.</param>
    /// <param name="prepare">The original incremental cache lookup.</param>
    /// <returns>The dependency version to evaluate without retaining strong references to compiler inputs.</returns>
    internal T Get(Compilation compilation, string name, MetaInfo metadata, bool json, Func<T> prepare)
    {
        var values = _values.GetValue(compilation, static _ => new());
        List<(MetaInfo Metadata, bool Json, Lazy<T> Output)> variants;
        Lazy<T> output;
        lock (values)
        {
            if (!values.TryGetValue(name, out variants))
                values.Add(name, variants = new());
            int index = variants.FindIndex(value => MetaInfoValueComparer.Instance.Equals(value.Metadata, metadata));
            if (index >= 0 && variants[index].Json != json)
            {
                output = variants[index].Output;
                variants.RemoveAt(index);
                if (variants.Count == 0)
                    values.Remove(name);
            }
            else
            {
                output = new Lazy<T>(prepare, LazyThreadSafetyMode.ExecutionAndPublication);
                if (index >= 0)
                    variants[index] = (metadata, json, output);
                else
                    variants.Add((metadata, json, output));
            }
        }
        try
        {
            return output.Value;
        }
        finally
        {
            if (!output.IsValueCreated)
                lock (values)
                    variants.RemoveAll(value => ReferenceEquals(value.Output, output));
        }
    }
}
