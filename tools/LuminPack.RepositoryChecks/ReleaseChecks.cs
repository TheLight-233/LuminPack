using System.IO.Compression;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Inspects release artifacts without loading or executing packaged assemblies.</summary>
internal static class ReleaseChecks
{
    private static readonly Guid SourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");
    private static readonly PortableExecutableReference[] FrameworkReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private static readonly SymbolDisplayFormat ApiFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
            SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
            SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeExtensionThis,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Writes a deterministic public/protected API inventory for every packaged assembly and framework.</summary>
    /// <param name="packages">The directory containing candidate NuGet packages.</param>
    /// <param name="output">The inventory output directory.</param>
    /// <returns>Zero after all inventories are written.</returns>
    internal static int WriteApi(string packages, string output)
    {
        Directory.CreateDirectory(output);
        foreach (string path in Directory.GetFiles(packages, "*.nupkg").Order(StringComparer.Ordinal))
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".dll", StringComparison.Ordinal) &&
                         (entry.FullName.StartsWith("lib/", StringComparison.Ordinal) || entry.FullName.StartsWith("analyzers/", StringComparison.Ordinal))))
            {
                using var input = entry.Open();
                using var bytes = new MemoryStream();
                input.CopyTo(bytes);
                var reference = MetadataReference.CreateFromImage(bytes.ToArray());
                var compilation = CSharpCompilation.Create("ApiInventory", references: FrameworkReferences.Append(reference),
                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
                var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(reference)!;
                var lines = new SortedSet<string>(StringComparer.Ordinal);
                Visit(assembly.GlobalNamespace, lines);
                string name = Path.GetFileNameWithoutExtension(entry.FullName) + "." + entry.FullName[..entry.FullName.LastIndexOf('/')].Replace('/', '.') + ".txt";
                File.WriteAllLines(Path.Combine(output, name), lines);
                Console.WriteLine($"Public API: {name}: {lines.Count} declarations.");
            }
        }
        if (Directory.GetFiles(output, "*.txt").Length == 0) throw new InvalidDataException("No packaged APIs were found.");
        return 0;
    }

    /// <summary>Collects visible declarations, constraints, implemented interfaces and public attributes.</summary>
    /// <param name="symbol">The namespace, type or member being inspected.</param>
    /// <param name="lines">The deterministic inventory receiving declarations.</param>
    private static void Visit(ISymbol symbol, ISet<string> lines)
    {
        if (symbol is INamespaceSymbol space)
        {
            foreach (var member in space.GetMembers()) Visit(member, lines);
            return;
        }
        if (symbol.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return;
        string declaration = symbol.Kind + " " + symbol.ToDisplayString(ApiFormat);
        if (symbol is INamedTypeSymbol type)
            declaration += " : " + string.Join(", ", new[] { type.BaseType?.ToDisplayString(ApiFormat) }
                .Concat(type.Interfaces.Select(item => item.ToDisplayString(ApiFormat))).Where(item => item != null).Order(StringComparer.Ordinal));
        string attributes = string.Join(" ", symbol.GetAttributes().Select(attribute => attribute.ToString()).Order(StringComparer.Ordinal));
        if (symbol is IMethodSymbol method)
        {
            attributes += string.Join(" ", method.GetReturnTypeAttributes().Select(attribute => " return:" + attribute));
            attributes += string.Join(" ", method.Parameters.SelectMany(parameter => parameter.GetAttributes().Select(attribute => " parameter:" + parameter.Name + ":" + attribute)));
        }
        lines.Add(declaration + (attributes.Length == 0 ? "" : " [" + attributes + "]"));
        if (symbol is INamedTypeSymbol container)
            foreach (var member in container.GetMembers()) Visit(member, lines);
    }

    /// <summary>Checks package identity, dependency alignment, expected assets and portable SourceLink metadata.</summary>
    /// <param name="packages">The directory containing candidate packages and symbols.</param>
    /// <param name="version">The exact candidate version.</param>
    /// <param name="revision">The source commit recorded by the build.</param>
    /// <returns>Zero when all structural checks pass.</returns>
    internal static int Verify(string packages, string version, string revision)
    {
        int pdbs = 0;
        foreach (string id in new[] { "LuminPack", "LuminPack.Generator" })
        {
            using var archive = ZipFile.OpenRead(Path.Combine(packages, $"{id}.{version}.nupkg"));
            using var specification = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            var document = XDocument.Load(specification);
            var metadata = document.Root!.Elements().Single(element => element.Name.LocalName == "metadata");
            Require(metadata.Elements().Single(element => element.Name.LocalName == "id").Value == id, "Unexpected package identity.");
            Require(metadata.Elements().Single(element => element.Name.LocalName == "version").Value == version, "Unexpected package version.");
            var repository = metadata.Elements().Single(element => element.Name.LocalName == "repository");
            Require((string?)repository.Attribute("commit") == revision, "Package repository commit does not match the candidate.");
            if (id == "LuminPack")
            {
                using var symbols = ZipFile.OpenRead(Path.Combine(packages, $"{id}.{version}.snupkg"));
                foreach (string framework in new[] { "netstandard2.1", "net8.0", "net9.0", "net10.0", "net11.0" })
                {
                    Require(archive.GetEntry($"lib/{framework}/LuminPack.Core.dll") != null, $"Missing Core asset: {framework}.");
                    Require(symbols.GetEntry($"lib/{framework}/LuminPack.Core.pdb") != null, $"Missing Core symbols: {framework}.");
                }
                var dependency = metadata.Descendants().Where(element => element.Name.LocalName == "dependency" && (string?)element.Attribute("id") == "LuminPack.Generator").ToArray();
                Require(dependency.Length != 0 && dependency.All(element => ((string?)element.Attribute("version"))?.TrimStart('[').Split(',')[0].Trim() == version), "Generator dependency version is not aligned.");
            }
            else
            {
                foreach (string assembly in new[] { "LuminPack.Generator", "LuminPack.CodeFixes" })
                {
                    Require(archive.GetEntry($"analyzers/dotnet/cs/{assembly}.dll") != null, $"Missing analyzer: {assembly}.");
                    Require(archive.GetEntry($"analyzers/dotnet/cs/{assembly}.pdb") != null, $"Missing analyzer symbols: {assembly}.");
                }
            }
        }
        foreach (string path in Directory.GetFiles(packages).Where(path => path.EndsWith(".nupkg", StringComparison.Ordinal) || path.EndsWith(".snupkg", StringComparison.Ordinal)))
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".pdb", StringComparison.Ordinal)))
            {
                using var input = entry.Open();
                using var bytes = new MemoryStream();
                input.CopyTo(bytes);
                bytes.Position = 0;
                using var provider = MetadataReaderProvider.FromPortablePdbStream(bytes);
                var reader = provider.GetMetadataReader();
                var links = reader.CustomDebugInformation.Select(reader.GetCustomDebugInformation)
                    .Where(item => reader.GetGuid(item.Kind) == SourceLinkKind).ToArray();
                Require(links.Length == 1, $"Missing SourceLink mapping: {entry.FullName}.");
                using var mapping = JsonDocument.Parse(reader.GetBlobBytes(links[0].Value));
                var urls = mapping.RootElement.GetProperty("documents").EnumerateObject().Select(item => item.Value.GetString()!).ToArray();
                Require(urls.Length != 0 && urls.All(url => url.StartsWith("https://raw.githubusercontent.com/TheLight-233/LuminPack/", StringComparison.OrdinalIgnoreCase) && url.Contains('/' + revision + '/', StringComparison.Ordinal)), "SourceLink does not point at the candidate commit.");
                pdbs++;
            }
        }
        Require(pdbs >= 7, "Core framework symbols or analyzer symbols are missing.");
        Console.WriteLine($"Verified package identities, dependency versions, framework assets and {pdbs} SourceLink PDBs at {revision}.");
        return 0;
    }

    /// <summary>Reports an invalid release artifact.</summary>
    /// <param name="condition">The required artifact invariant.</param>
    /// <param name="message">The diagnostic describing a failed invariant.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    /// <summary>Checks that API inventories detect signature, setter, nullability, constant and constraint changes.</summary>
    /// <returns>Zero when the metadata-only inventory regression checks pass.</returns>
    internal static int SelfTest()
    {
        const string source = """
            #nullable enable
            public class Contract<T> where T : class
            {
                public const int Limit = 1;
                public string? Name { get; private set; }
                [System.Obsolete("original")]
                public void Read(int count = 1) { }
                protected void Reuse() { }
                private void Hidden() { }
                internal class Internal { public void Leak() { } }
            }
            """;
        var baseline = FixtureApi(source);
        Require(!baseline.Any(line => line.Contains("Hidden", StringComparison.Ordinal) || line.Contains("Leak", StringComparison.Ordinal)), "The inventory exposed private or internal implementation details.");
        Require(baseline.Any(line => line.Contains("Reuse", StringComparison.Ordinal)), "The inventory lost a protected API.");
        foreach (var (before, after) in new[]
        {
            ("int count", "long count"), ("private set", "set"), ("string? Name", "string Name"),
            ("Limit = 1", "Limit = 2"), ("where T : class", "where T : class, new()"), ("count = 1", "count = 2"), ("original", "changed")
        })
            Require(!baseline.SetEquals(FixtureApi(source.Replace(before, after, StringComparison.Ordinal))), $"The API inventory missed the change from {before} to {after}.");
        Console.WriteLine("Public API metadata regression checks passed (visibility, signatures, setters, nullability, constants, constraints and defaults).");
        return 0;
    }

    /// <summary>Compiles a small API fixture and reads its metadata without executing it.</summary>
    /// <param name="source">The fixture's C# source.</param>
    /// <returns>The fixture's visible metadata declarations.</returns>
    private static SortedSet<string> FixtureApi(string source)
    {
        var fixture = CSharpCompilation.Create("ReleaseApiFixture", [CSharpSyntaxTree.ParseText(source)], FrameworkReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var bytes = new MemoryStream();
        var result = fixture.Emit(bytes);
        Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var reference = MetadataReference.CreateFromImage(bytes.ToArray());
        var reader = CSharpCompilation.Create("ReleaseApiReader", references: FrameworkReferences.Append(reference),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));
        var assembly = (IAssemblySymbol)reader.GetAssemblyOrModuleSymbol(reference)!;
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        Visit(assembly.GlobalNamespace, lines);
        return lines;
    }
}
