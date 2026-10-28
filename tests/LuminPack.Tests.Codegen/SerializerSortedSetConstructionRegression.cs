using System.Reflection;
using System.Text;
using LuminPack.Core;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks sorted construction for every element contract, ordered reuse and optional runtime capability invalidation.</summary>
internal static class SerializerSortedSetConstructionRegression
{
    /// <summary>Compiles all four tiers and compares retained/fresh output when runtime capabilities and element contracts change.</summary>
    /// <param name="references">The platform and current runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = Compile(Source(mode), parse, references);
            var (driver, output) = Generate(compilation, parse);
            VerifySelection(driver, expected: true);
            VerifyReuseSelection(driver, setExpected: true, listExpected: true);
            Execute(output);
            if (mode != "Minimal") continue;

            byte[] runtime = File.ReadAllBytes(typeof(LuminPackWriter).Assembly.Location);
            int entry = runtime.AsSpan().IndexOf("CreateSortedSet\0"u8);
            Require(entry >= 0, "The accepted optional builder is missing from the runtime.");
            runtime[entry] = (byte)'X';
            var original = references.OfType<PortableExecutableReference>().Single(reference => reference.FilePath == typeof(LuminPackWriter).Assembly.Location);
            var compatibility = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(runtime));
            driver = driver.RunGeneratorsAndUpdateCompilation(compatibility, out var compatibilityOutput, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            VerifySelection(driver, expected: false);
            Execute(compatibilityOutput);
            var (fresh, _) = Generate(compatibility, parse);
            Require(Trees(driver).SequenceEqual(Trees(fresh), StringComparer.Ordinal), "Removing the optional runtime builder left stale generated calls.");

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            VerifySelection(driver, expected: true);
            var (restored, _) = Generate(compilation, parse);
            Require(Trees(driver).SequenceEqual(Trees(restored), StringComparer.Ordinal), "Restoring the optional runtime builder left stale fallback calls.");

            foreach (string capability in new[] { "CanRebuildSortedSet", "TryRebuildSortedSet", "CanAppendSortedListOrdered", "TryAppendSortedListOrdered" })
            {
                byte[] reduced = File.ReadAllBytes(typeof(LuminPackWriter).Assembly.Location);
                int position = reduced.AsSpan().IndexOf(Encoding.UTF8.GetBytes(capability + "\0"));
                Require(position >= 0, "The optional reuse capability is missing: " + capability);
                reduced[position] = (byte)'X';
                var withoutReuse = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(reduced));
                driver = driver.RunGeneratorsAndUpdateCompilation(withoutReuse, out var fallbackOutput, out diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                VerifyReuseSelection(driver, !capability.Contains("SortedSet", StringComparison.Ordinal), !capability.Contains("SortedList", StringComparison.Ordinal));
                Execute(fallbackOutput);
                var (fallbackFresh, _) = Generate(withoutReuse, parse);
                Require(Trees(driver).SequenceEqual(Trees(fallbackFresh), StringComparer.Ordinal), "Removing " + capability + " retained stale reuse code.");
                driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out diagnostics);
                Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
                VerifyReuseSelection(driver, setExpected: true, listExpected: true);
                Require(Trees(driver).SequenceEqual(Trees(restored), StringComparer.Ordinal), "Restoring " + capability + " retained stale fallback code.");
            }

            var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), CSharpSyntaxTree.ParseText(Source(mode).Replace("SortedSet<UserElement> User", "SortedSet<int> User", StringComparison.Ordinal), parse, "SortedSetCases.cs"));
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var editedOutput, out diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var (editedFresh, _) = Generate(edited, parse);
            Require(Trees(driver).SequenceEqual(Trees(editedFresh), StringComparer.Ordinal), "A framework element edit reused a stale user comparison contract.");
            using var image = new MemoryStream();
            var emitted = editedOutput.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        }
        Console.WriteLine("Passed sorted construction/reuse eligibility for framework, user, Custom, portable and noncomparable elements, four tiers, optional-helper removal/restoration and retained/fresh invalidation.");
    }

    /// <summary>Creates closed serializer calls for every supported comparable framework scalar and selected excluded contracts.</summary>
    /// <param name="mode">The generation policy.</param>
    /// <returns>The complete consumer model.</returns>
    private static string Source(string mode)
    {
        var source = new StringBuilder("""
            using System;
            using System.Linq;
            using System.Numerics;
            using System.Collections.Generic;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Core;
            using LuminPack.Option;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public struct UserElement:IComparable<UserElement> { public int Value;public int CompareTo(UserElement other)=>Value.CompareTo(other.Value); }
            [LuminPackable(GeneratorType.Custom)] public struct CustomElement:IComparable<CustomElement>
            {
                public int Value;
                public int CompareTo(CustomElement other)=>Value.CompareTo(other.Value);
                public static void Serialize(ref LuminPackWriter writer,in CustomElement value)=>writer.Write(value.Value);
                public static void Deserialize(ref LuminPackReader reader,ref CustomElement value)=>value.Value=reader.Read<int>();
            }
            [LuminPackable,LuminPackOption(StructLayout=LuminPackStructLayout.Packed)] public struct PortableElement:IComparable<PortableElement>
            {public byte Tag;public int Value;public int CompareTo(PortableElement other)=>Value.CompareTo(other.Value);}
            [LuminPackable] public sealed class SetHolder { public SortedSet<int> Set; }
            public static class SortedCases
            {
                public static int User(ReadOnlySpan<byte> bytes,ref SortedSet<UserElement> User)=>LuminPackSerializer.Deserialize(bytes,ref User);
                public static int Custom(ReadOnlySpan<byte> bytes,ref SortedSet<CustomElement> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                public static int Portable(ReadOnlySpan<byte> bytes,ref SortedSet<PortableElement> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                public static int Uncomparable(ReadOnlySpan<byte> bytes,ref SortedSet<Complex> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                public static int Open<T>(ReadOnlySpan<byte> bytes,ref SortedSet<T> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                public static byte[] Write(SortedSet<int> value)=>LuminPackSerializer.Serialize(value);
                public static byte[] Nested(SetHolder value)=>LuminPackSerializer.Serialize(value);
                public static string Json(SortedSet<int> value)=>LuminPackSerializer.SerializeJson(value);
                public static byte[] WriteList(SortedList<string,UserElement> value)=>LuminPackSerializer.Serialize(value);
                public static int ReadList(ReadOnlySpan<byte> bytes,ref SortedList<string,UserElement> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
                public static int IntegerKeys(ReadOnlySpan<byte> bytes,ref SortedList<int,string> value)=>LuminPackSerializer.Deserialize(bytes,ref value);
            """.Replace("MODE", mode, StringComparison.Ordinal));
        string[] types = Types();
        for (int index = 0; index < types.Length; index++)
            source.Append("public static int Read" + index + "(ReadOnlySpan<byte> bytes,ref SortedSet<" + types[index] + "> value)=>LuminPackSerializer.Deserialize(bytes,ref value);");
        source.Append("""
                public static void Check()
                {
                    var source=new SortedSet<int>{-1,3,7};byte[] bytes=Write(source);SortedSet<int> result=null;Read5(bytes,ref result);
                    if(!result.SetEquals(source))throw new Exception("Fresh sorted set changed");
                    var reverse=new SortedSet<int>(Comparer<int>.Create((a,b)=>b.CompareTo(a))){99};var identity=reverse;Read5(bytes,ref reverse);
                    if(!ReferenceEquals(identity,reverse)||!reverse.SequenceEqual(new[]{7,3,-1}))throw new Exception("Reuse changed");
                    var holder=new SetHolder{Set=source};byte[] nested=Nested(holder);SetHolder copy=null;LuminPackSerializer.Deserialize(nested,ref copy);
                    if(!copy.Set.SetEquals(source)||Json(source)!="[-1,3,7]")throw new Exception("Nested/JSON changed");
                    var map=new SortedList<string,UserElement>{{"a",new(){Value=1}},{"b",new(){Value=2}}};
                    byte[] mapBytes=WriteList(map);var mapTarget=new SortedList<string,UserElement>(8);var mapIdentity=mapTarget;
                    ReadList(mapBytes,ref mapTarget);
                    if(!ReferenceEquals(mapIdentity,mapTarget)||mapTarget.Count!=2||mapTarget["b"].Value!=2)throw new Exception("Ordered list reuse changed");
                    var malformed = new List<byte>(BitConverter.GetBytes(2));
                    malformed.AddRange(LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>((string)null));
                    malformed.AddRange(LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>("value"));
                    malformed.AddRange(LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>("other"));
                    malformed.AddRange(LuminPackSerializer.Serialize<LuminPackUtf16LengthOptions>("second"));
                    SortedList<string,string> invalid = null;
                    bool rejected = false;
                    try { LuminPackSerializer.Deserialize<LuminPackUtf16LengthOptions>(malformed.ToArray(),ref invalid); }
                    catch (ArgumentNullException error) { rejected = error.ParamName == "key"; }
                    if(!rejected || invalid != null)throw new Exception("Fresh sorted list published a null key");
                }
            }
            """);
        return source.ToString();
    }

    /// <summary>Lists the closed framework comparison families exercised by the generator.</summary>
    /// <returns>Supported framework type spellings.</returns>
    private static string[] Types() => ["bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "char", "float", "double", "decimal", "Guid", "Half", "Int128", "UInt128", "DateTime", "DateTimeOffset", "TimeSpan", "DateOnly", "TimeOnly", "string"];

    /// <summary>Checks that every closed element contract invokes the optional builder while the runtime exposes it.</summary>
    /// <param name="driver">The completed production generator.</param>
    /// <param name="expected">Whether the runtime currently exposes the builder.</param>
    /// <remarks>The builder checks order with one default-comparer comparison per adjacent pair, so user, Custom, portable and
    /// noncomparable elements are accepted since 2026-10-26; comparers other than the default keep their public path at run time.</remarks>
    private static void VerifySelection(GeneratorDriver driver, bool expected)
    {
        var selected = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(method => method.Body?.ToString().Contains("LuminTreeStorage.CreateSortedSet(", StringComparison.Ordinal) == true).ToArray();
        Require(expected ? selected.Length >= Types().Length : selected.Length == 0, "The optional sorted-set builder was not selected for the accepted families.");
        if (expected)
            foreach (string element in new[] { "UserElement", "CustomElement", "PortableElement", "Complex" })
                Require(selected.Any(method => method.ParameterList.ToString().Contains(element, StringComparison.Ordinal)), "The sorted-set builder skipped the " + element + " contract.");
        foreach (var method in selected)
            Require(method.ToString().Contains("finally", StringComparison.Ordinal), "The new builder lost pooled cleanup.");
    }

    /// <summary>Checks optional reuse calls for every element contract and excludes nonstring-key ordered list appends.</summary>
    /// <param name="driver">The completed generator.</param>
    /// <param name="setExpected">Whether both mutable set helpers are exposed.</param>
    /// <param name="listExpected">Whether both ordered list helpers are exposed.</param>
    private static void VerifyReuseSelection(GeneratorDriver driver, bool setExpected, bool listExpected)
    {
        var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
        var sets = methods.Where(method => method.Body?.ToString().Contains("LuminTreeStorage.TryRebuildSortedSet(", StringComparison.Ordinal) == true).ToArray();
        Require(setExpected ? sets.Length >= Types().Length : sets.Length == 0, "Mutable sorted-set helper selection changed.");
        foreach (var method in sets)
        {
            string body = method.Body!.ToString();
            Require(body.Contains(".Shared.Rent(", StringComparison.Ordinal) && body.Contains("finally", StringComparison.Ordinal) &&
                body.Contains("catch", StringComparison.Ordinal) && body.Contains("__completed", StringComparison.Ordinal), "Shared staging lost cleanup or failure-prefix recovery.");
        }
        var lists = methods.Where(method => method.Body?.ToString().Contains("LuminPackCollectionAccess.TryAppendSortedListOrdered(", StringComparison.Ordinal) == true).ToArray();
        Require(listExpected ? lists.Length > 0 : lists.Length == 0, "Ordered string-key list helper selection changed.");
        foreach (var method in methods.Where(method => method.Identifier.ValueText == "RootRead" &&
                     method.ParameterList.ToString().Contains("SortedList<int, string>", StringComparison.Ordinal)))
            Require(!method.ToString().Contains("ThrowArgumentNullException", StringComparison.Ordinal), "A nonnullable value key acquired a null-check branch.");
        foreach (var method in lists)
            Require(!method.ParameterList.ToString().Contains("SortedList<int", StringComparison.Ordinal), "Nonstring keys acquired ordered append.");
    }

    /// <summary>Creates an isolated consumer compilation for a selected policy.</summary>
    /// <param name="source">The complete source.</param>
    /// <param name="parse">Current language and target symbols.</param>
    /// <param name="references">The consumer runtime references.</param>
    /// <returns>The initial compilation.</returns>
    private static CSharpCompilation Compile(string source, CSharpParseOptions parse, MetadataReference[] references)
        => CSharpCompilation.Create("SortedSetCases" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(source, parse, "SortedSetCases.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    /// <summary>Runs both production generators and rejects generator failures.</summary>
    /// <param name="compilation">The complete consumer compilation.</param>
    /// <param name="parse">Current parsing options.</param>
    /// <returns>The retained driver and generated compilation.</returns>
    private static (GeneratorDriver Driver, Compilation Output) Generate(CSharpCompilation compilation, CSharpParseOptions parse)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
        return (driver, output);
    }

    /// <summary>Compiles and executes the generated consumer's fresh/reuse and independent operation checks.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    private static void Execute(Compilation output)
    {
        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(image.ToArray()).GetType("SortedCases")!.GetMethod("Check")!.Invoke(null, null);
    }

    /// <summary>Orders complete generated snapshots by stable hint identity.</summary>
    /// <param name="driver">The completed driver.</param>
    /// <returns>The complete output trees.</returns>
    private static string[] Trees(GeneratorDriver driver)
        => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(result => result.HintName + "\n" + result.SourceText).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Reports a failed generation or execution invariant.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The diagnostic detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
