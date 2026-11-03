using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace LuminPack.SourceGenerator;

/// <summary>Runs independent generator work items on several threads.</summary>
/// <remarks>Roslyn runs a generator step on one thread, so per-file binding and per-type analysis would otherwise use one
/// core. Work items write only their own result slot and shared caches that are thread-safe; callers merge results in
/// their original order, so generated output does not depend on scheduling.</remarks>
internal static class ParallelWork
{
    /// <summary>Whether the host's garbage collector lets parallel generation pay off.</summary>
    private static readonly bool Enabled = Environment.ProcessorCount > 1 && !UsesDynamicServerHeaps();

    /// <summary>Runs <paramref name="body"/> for every index and rethrows the first failure unwrapped.</summary>
    /// <param name="count">The number of work items.</param>
    /// <param name="body">The work for one index.</param>
    /// <param name="cancellationToken">Stops scheduling further items.</param>
    internal static void For(int count, Action<int> body, CancellationToken cancellationToken = default)
    {
        if (count <= 1 || !Enabled)
        {
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                body(index);
            }
            return;
        }
        try
        {
            Parallel.For(0, count, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount }, body);
        }
        catch (AggregateException exception) when (exception.InnerExceptions.Count != 0)
        {
            ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
        }
    }

    /// <summary>Detects Server GC with dynamic heap adaptation (DATAS), the compiler server's default since .NET 9.</summary>
    /// <returns>Whether that collector is active.</returns>
    /// <remarks>Generation allocates tens of gigabytes on large projects. Measured on the unit-test project under Server GC,
    /// parallel generation took 1,196 s with DATAS (1,110 s of it in GC pauses) against 252 s without DATAS and about 630 s
    /// sequentially with DATAS, so work stays sequential while DATAS is active. The setting is read through reflection
    /// because this assembly targets netstandard2.0; a runtime without it has no DATAS.</remarks>
    private static bool UsesDynamicServerHeaps()
    {
        if (!GCSettings.IsServerGC) return false;
        try
        {
            var read = typeof(GC).GetMethod("GetConfigurationVariables", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            return read?.Invoke(null, null) is IReadOnlyDictionary<string, object> variables &&
                variables.TryGetValue("GCDynamicAdaptationMode", out var mode) && Convert.ToInt64(mode) != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
