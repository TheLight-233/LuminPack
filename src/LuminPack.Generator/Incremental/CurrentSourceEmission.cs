using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Shares an aggregate dependency version among the source publishers for the exact current compilation.</summary>
internal sealed class CurrentSourceEmission
{
    private readonly ConditionalWeakTable<Compilation, List<(MetaInfo Metadata, GeneratorOutput Owners, Lazy<AggregateEmissionInput> Input)>> _values = new();

    /// <summary>Prepares one dependency version while the publishers are processing the same input.</summary>
    /// <param name="compilation">The input compilation before either publisher adds its sources.</param>
    /// <param name="metadata">The exact generation settings.</param>
    /// <param name="owners">The source groups requested by the current publisher.</param>
    /// <param name="prepare">The original incremental cache lookup.</param>
    /// <returns>The dependency version to evaluate without retaining strong references to compiler inputs.</returns>
    internal AggregateEmissionInput Get(Compilation compilation, MetaInfo metadata, GeneratorOutput owners, Func<AggregateEmissionInput> prepare)
    {
        var values = _values.GetValue(compilation, static _ => new());
        Lazy<AggregateEmissionInput> input;
        lock (values)
        {
            int index = values.FindIndex(value => MetaInfoValueComparer.Instance.Equals(value.Metadata, metadata));
            if (index >= 0 && (values[index].Owners & owners) == 0)
            {
                var previous = values[index];
                input = previous.Input;
                var seen = previous.Owners | owners;
                if (seen == GeneratorOutput.All)
                    values.RemoveAt(index);
                else
                    values[index] = (previous.Metadata, seen, input);
            }
            else
            {
                input = new Lazy<AggregateEmissionInput>(prepare, LazyThreadSafetyMode.ExecutionAndPublication);
                if (index >= 0)
                    values[index] = (metadata, owners, input);
                else
                    values.Add((metadata, owners, input));
            }
        }
        try
        {
            return input.Value;
        }
        finally
        {
            if (!input.IsValueCreated)
                lock (values)
                    values.RemoveAll(value => ReferenceEquals(value.Input, input));
        }
    }
}
