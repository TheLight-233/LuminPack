using System.Reflection;
using System.Runtime.Loader;

/// <summary>Loads one complete retained build without sharing its runtime caches with another image.</summary>
internal static class BenchmarkImage
{
    /// <summary>Loads the benchmark assembly and resolves its dependencies from the same directory.</summary>
    /// <param name="directory">A complete benchmark output directory.</param>
    /// <param name="assemblyFile">The benchmark assembly file within that directory.</param>
    /// <returns>The isolated benchmark assembly.</returns>
    internal static Assembly Load(string directory, string assemblyFile = "LuminPackBenchmark.dll")
    {
        directory = Path.GetFullPath(directory);
        var context = new AssemblyLoadContext(directory);
        context.Resolving += (_, name) => context.LoadFromAssemblyPath(Path.Combine(directory, name.Name + ".dll"));
        return context.LoadFromAssemblyPath(Path.Combine(directory, assemblyFile));
    }
}
