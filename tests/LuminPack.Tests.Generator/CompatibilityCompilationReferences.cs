using Microsoft.CodeAnalysis;

/// <summary>Provides matching reference metadata for older Roslyn and Unity fixture compilations.</summary>
internal static class CompatibilityCompilationReferences
{
    /// <summary>Loads the built pointer runtime with its .NET Standard reference pack and package dependencies.</summary>
    /// <returns>References consumable by C# 10 without newer runtime metadata features.</returns>
    internal static MetadataReference[] Create()
    {
        string? runtime = null;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "src", "LuminPack.Core", "bin", "Release", "netstandard2.1", "LuminPack.Core.dll");
            if (File.Exists(candidate)) { runtime = candidate; break; }
        }
        if (runtime == null)
            throw new InvalidOperationException("Build the Release netstandard2.1 runtime before checking compatibility generation.");
        string dotnetRoot = new FileInfo(typeof(object).Assembly.Location).Directory!.Parent!.Parent!.Parent!.FullName;
        string referenceDirectory = Path.Combine(dotnetRoot, "packs", "NETStandard.Library.Ref", "2.1.0", "ref", "netstandard2.1");
        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return Directory.GetFiles(referenceDirectory, "*.dll")
            .Append(Path.Combine(packages, "system.runtime.compilerservices.unsafe", "6.1.2", "lib", "netstandard2.0", "System.Runtime.CompilerServices.Unsafe.dll"))
            .Append(Path.Combine(packages, "system.collections.immutable", "9.0.8", "lib", "netstandard2.0", "System.Collections.Immutable.dll"))
            .Append(runtime).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToArray();
    }
}
