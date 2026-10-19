using System.Reflection;
using System.Runtime.Loader;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks mechanism shape and optionally compares execution with a pre-refactor analyzer.</summary>
internal static class SerializerMechanismRegression
{
    /// <summary>Protects private storage views and both Union dispatch mechanisms against historical drift.</summary>
    /// <param name="references">Runtime and platform references.</param>
    /// <param name="historicalAnalyzer">An optional analyzer built from the recorded historical baseline.</param>
    /// <param name="historicalRuntime">The matching legacy runtime, required after the approved options API change.</param>
    internal static void Run(MetadataReference[] references, string? historicalAnalyzer = null, string? historicalRuntime = null)
    {
        var current = Inspect(references, [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminMapSourceGenerator().AsSourceGenerator()], "CurrentMechanisms");
        Require(current.SparseSize == current.SparseBytes, "Named version metadata was omitted from current size evaluation.");
        if (historicalAnalyzer != null)
        {
            var historical = Assembly.LoadFile(Path.GetFullPath(historicalAnalyzer));
            var generators = new[] { "LuminPackSourceGenerator", "LuminMapSourceGenerator" }
                .Select(name => ((IIncrementalGenerator)Activator.CreateInstance(historical.GetType("LuminPack.SourceGenerator." + name, throwOnError: true)!)!).AsSourceGenerator()).ToArray();
            Require(historicalRuntime != null, "A historical runtime is required to execute the former runtime-options API.");
            var baselineReferences = references.Where(reference => Path.GetFileName(reference.Display) != "LuminPack.dll")
                .Append(MetadataReference.CreateFromFile(Path.GetFullPath(historicalRuntime!))).ToArray();
            var baseline = Inspect(baselineReferences, generators, "HistoricalMechanisms", historicalRuntime);
            Require(current.Wire == baseline.Wire, "Binary bytes, JSON text, callback order, or size results differ from the historical analyzer.");
            Require(current.UnionBodies.SequenceEqual(baseline.UnionBodies), "A Union virtual body or generic cached-slot switch differs from the historical analyzer.\n" +
                string.Join("\n", current.UnionBodies.Zip(baseline.UnionBodies).Where(pair => pair.First != pair.Second)
                    .Take(1).Select(pair => "Current: " + pair.First + "\nHistorical: " + pair.Second)));
            Require(baseline.SparseSize + 1 == baseline.SparseBytes,
                "The historical sparse-slot baseline no longer reproduces its recorded missing length-table byte.");
            Require(current.SparseBytes != baseline.SparseBytes,
                "The explicitly authorized named VersionTolerant format was not exercised.");
            Console.WriteLine("Passed historical mechanism parity for unchanged formats and JSON; separately verified authorized named VersionTolerant evolution and exact size.");
        }
        else Console.WriteLine("Passed Local + As storage views, ordinary Union virtual dispatch, and generic Union virtual plus cached-slot switches.");
    }

    /// <summary>Compiles and executes one analyzer version against the same private and polymorphic graph.</summary>
    /// <param name="references">Runtime references.</param>
    /// <param name="generators">The analyzer pair under review.</param>
    /// <param name="name">The isolated output assembly name.</param>
    /// <param name="historicalRuntime">An optional isolated runtime for the legacy API fixture.</param>
    /// <returns>Observable serialization output and canonical hidden Union bodies.</returns>
    private static (string Wire, string[] UnionBodies, int SparseSize, int SparseBytes) Inspect(MetadataReference[] references, ISourceGenerator[] generators, string name, string? historicalRuntime = null)
    {
        string source = """
            using System;
            using System.Collections.Generic;
            using System.Runtime.InteropServices;
            using LuminPack;
            using LuminPack.Attribute;
            public class StorageBase
            {
                private string _basePadding = "base padding";
                public int BaseValue = 17;
                public int Secret() => BaseValue;
            }
            [LuminPackable] public sealed class Storage : StorageBase
            {
                private string _padding = "derived padding";
                [LuminPackInclude] private int Hidden { get; set; } = 23;
                public string Text { get; set; } = "property";
                public int Tail = 31;
                public int Computed { get => throw new Exception("Computed getter ran."); set => throw new Exception("Computed setter ran."); }
                public int HiddenValue() => Hidden;
            }
            [LuminPackable, LuminPackUnion(3, typeof(Case))] public abstract partial class Root
            {
                [LuminPackOnSerializing] public void Before() => Entry.Events.Add("before");
                [LuminPackOnSerialized] public void After() => Entry.Events.Add("after");
            }
            [LuminPackable] public sealed partial class Case : Root { public Storage First = new(); }
            [LuminPackable, LuminPackUnion(9, typeof(GenericCase<string>)), LuminPackUnion(7, typeof(GenericCase<int>))]
            public abstract partial class GenericRoot<T> { }
            [LuminPackable] public partial class GenericCase<T> : GenericRoot<T> { public T Item; public List<T> Items; }
            [LuminPackable(GeneratorType.VersionTolerant)] public class VersionFull
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(1)] public string Middle;
                [LuminPackOrder(2)] public int Last;
            }
            [LuminPackable(GeneratorType.VersionTolerant)] public class VersionGap
            {
                [LuminPackOrder(0)] public int First;
                [LuminPackOrder(2)] public int Last;
            }
            [LuminPackable(GeneratorType.CircleReference)] public class Node
            {
                [LuminPackOrder(0)] public string Name;
                [LuminPackOrder(1)] public Node Next;
                [LuminPackOrder(2)] public Node Alias;
            }
            [LuminPackable, StructLayout(LayoutKind.Explicit, Size = 16)] public struct RawValue
            {
                [FieldOffset(0)] public byte Tag;
                [FieldOffset(8)] public long Number;
            }
            [LuminPackable] public struct ManagedValue { public int Number; public string Text; }
            [LuminPackable] public struct ConstrainedValue<T> where T : unmanaged { public byte Tag; public T Item; }
            public enum WideEnum : ulong { High = 0xF000000000000001UL }
            [LuminPackable] public sealed class ValueGraph
            {
                public RawValue Raw;
                public ManagedValue Managed;
                public ConstrainedValue<int> Constrained;
                public ManagedValue? Optional;
                public WideEnum Enum;
                public int? Missing;
                public int? Present;
                public Queue<int> Queue;
                public Stack<int> Stack;
            }
            public static class Entry
            {
                public static readonly List<string> Events = new();
                public static string Capture()
                {
                    var results = new List<string>();
                    var storage = new Storage();
                    var bytes = LuminPackSerializer.Serialize(storage);
                    var restored = LuminPackSerializer.Deserialize<Storage>(bytes);
                    if (restored.Secret() != 17 || restored.HiddenValue() != 23 || restored.Text != "property" || restored.Tail != 31)
                        throw new Exception("Private mirror storage was not restored.");
                    Add(storage, results);
                    Root root = new Case(); Add(root, results);
                    Root empty = null; Add(empty, results);
                    GenericRoot<int> integer = new GenericCase<int> { Item = 41, Items = new() { 43, 47 } }; Add(integer, results);
                    GenericRoot<string> text = new GenericCase<string> { Item = "open", Items = new() { "case", null, "" } }; Add(text, results);
                    if (LuminPackSerializer.Serialize(integer)[0] != 7 || LuminPackSerializer.Serialize(text)[0] != 9)
                        throw new Exception("Closed generic tags changed.");
                    var full = new VersionFull { First = 53, Middle = "skip middle", Last = 59 };
                    var gap = LuminPackSerializer.Deserialize<VersionGap>(LuminPackSerializer.Serialize(full));
                    if (gap.First != 53 || gap.Last != 59) throw new Exception("Version field-length skipping changed.");
                    var added = LuminPackSerializer.Deserialize<VersionFull>(LuminPackSerializer.Serialize(gap));
                    if (added.Middle != null || added.Last != 59) throw new Exception("Version absent-slot defaults changed.");
                    Add(full, results, recordSize: false, recordBinary: false); Add(gap, results, recordSize: false, recordBinary: false);
                    var node = new Node { Name = "root" };
                    node.Next = new Node { Name = "child", Next = node }; node.Alias = node.Next;
                    var cycle = LuminPackSerializer.Deserialize<Node>(LuminPackSerializer.Serialize(node));
                    if (!ReferenceEquals(cycle.Next.Next, cycle) || !ReferenceEquals(cycle.Next, cycle.Alias))
                        throw new Exception("Binary cycle or shared-reference identity changed.");
                    var jsonCycle = LuminPackSerializer.DeserializeJson<Node>(LuminPackSerializer.SerializeJson(node));
                    if (!ReferenceEquals(jsonCycle.Next.Next, jsonCycle) || !ReferenceEquals(jsonCycle.Next, jsonCycle.Alias))
                        throw new Exception("JSON cycle or shared-reference identity changed.");
                    Add(node, results);
                    var values = new ValueGraph { Raw = new RawValue { Tag = 61, Number = 67 }, Managed = new ManagedValue { Number = 71, Text = "managed" },
                        Constrained = new ConstrainedValue<int> { Tag = 7, Item = 103 },
                        Optional = new ManagedValue { Number = 73, Text = "nullable" }, Enum = WideEnum.High, Missing = null, Present = 79,
                        Queue = new Queue<int>(new[] { 83, 89 }), Stack = new Stack<int>(new[] { 97, 101 }) };
                    var valueCopy = LuminPackSerializer.Deserialize<ValueGraph>(LuminPackSerializer.Serialize(values));
                    if (valueCopy.Raw.Number != 67 || valueCopy.Managed.Text != "managed" || valueCopy.Optional.Value.Number != 73 ||
                        valueCopy.Enum != WideEnum.High || valueCopy.Missing != null || valueCopy.Present != 79 || valueCopy.Queue.Dequeue() != 83 || valueCopy.Stack.Pop() != 101)
                        throw new Exception("Value, nullable, enum, or collection ordering changed.");
                    Add(values, results);
                    Add(values.Constrained, results);
                    results.Add(string.Join(",", Events));
                    return string.Join("|", results);
                }
                public static int SparseSize() => LuminPackSerializer.Sizeof(new VersionGap { First = 53, Last = 59 });
                public static int SparseBytes() => LuminPackSerializer.Serialize(new VersionGap { First = 53, Last = 59 }).Length;
                private static void Add<T>(T value, List<string> results, bool recordSize = true, bool recordBinary = true)
                {
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    if (recordBinary) results.Add(Convert.ToHexString(bytes));
                    if (recordSize) results.Add(LuminPackSerializer.Sizeof(value).ToString());
                    JSON_PROFILE_LOOP
                }
            }
            """;
        string profileLoop = historicalRuntime == null ? """
            AddJson<LuminPack.Option.LuminPackDefaultOptions>();
            AddJson<LuminPack.Option.LuminPackUtf16LengthOptions>();
            void AddJson<TProfile>() where TProfile : struct, LuminPack.Option.ILuminPackOptions
            {
                string json = LuminPackSerializer.SerializeJson<T, TProfile>(value);
                var restored = LuminPackSerializer.DeserializeJson<T, TProfile>(json);
                results.Add(LuminPackSerializer.SerializeJson<T, TProfile>(restored));
            }
            """ : """
            foreach (var encoding in new[] { LuminPack.Option.LuminPackStringEncoding.UTF8, LuminPack.Option.LuminPackStringEncoding.UTF16 })
            {
                var options = new LuminPack.Option.LuminPackSerializerOption { StringEncoding = encoding };
                string json = LuminPackSerializer.SerializeJson(value, options);
                var restored = LuminPackSerializer.DeserializeJson<T>(json, options);
                results.Add(LuminPackSerializer.SerializeJson(restored, options));
            }
            """;
        source = source.Replace("JSON_PROFILE_LOOP", profileLoop, StringComparison.Ordinal);
        var parse = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Parse,
            preprocessorSymbols: ["NET5_0_OR_GREATER", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER"]);
        var input = CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source, parse, "Mechanisms.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Require(driver.GetRunResult().Results.All(result => result.Exception == null), string.Join(Environment.NewLine, driver.GetRunResult().Results.Select(result => result.Exception)));
        var errors = output.GetDiagnostics().Concat(diagnostics).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Require(errors.Length == 0, string.Join(Environment.NewLine, errors));
        string generated = string.Join(Environment.NewLine, output.SyntaxTrees.Skip(1));
        Require(generated.Contains("LuminPackMarshal.As<", StringComparison.Ordinal) && generated.Contains("Hidden", StringComparison.Ordinal) &&
            !generated.Contains("UnsafeAccessorAttribute", StringComparison.Ordinal), "Private storage lost its Local + As path.");
        var methods = output.SyntaxTrees.Skip(1).SelectMany(tree => tree.GetRoot().DescendantNodes()).OfType<MethodDeclarationSyntax>()
            .Where(method => method.Identifier.ValueText.StartsWith("__LuminPackUnion", StringComparison.Ordinal)).ToArray();
        var ordinary = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "Case" }).ToArray();
        Require(ordinary.Length == (historicalRuntime == null ? 10 : 3) && ordinary.All(method => method.TypeParameterList == null && method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
            !method.DescendantNodes().OfType<SwitchStatementSyntax>().Any()), "An ordinary Union override acquired a switch or lost virtual dispatch.");
        var generic = methods.Where(method => method.Parent is ClassDeclarationSyntax { Identifier.ValueText: "GenericCase" }).ToArray();
        Require(generic.Length == (historicalRuntime == null ? 10 : 3) && generic.All(method => method.TypeParameterList == null && method.Modifiers.Any(SyntaxKind.OverrideKeyword) &&
            method.DescendantNodes().OfType<SwitchStatementSyntax>().Any(statement => statement.Expression.ToString().StartsWith("__LuminPackUnionDispatchSlot_", StringComparison.Ordinal))),
            "A generic Union override lost its cached-slot switch.");
        var bodies = methods.Where(method => method.TypeParameterList == null && !method.Identifier.ValueText.Contains("_Options_", StringComparison.Ordinal))
            .Select(method => ((TypeDeclarationSyntax)method.Parent!).Identifier.ValueText + ":" + method.Identifier.ValueText + ":" +
            new ApiOwnerNames().Visit(method.Body)!.NormalizeWhitespace().ToFullString()).Order(StringComparer.Ordinal).ToArray();
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        Require(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var context = new AssemblyLoadContext(name, isCollectible: true);
        if (historicalRuntime != null)
            context.LoadFromAssemblyPath(Path.GetFullPath(historicalRuntime));
        try
        {
            image.Position = 0;
            var assembly = context.LoadFromStream(image);
            var entry = assembly.GetType("Entry")!;
            return ((string)entry.GetMethod("Capture")!.Invoke(null, null)!, bodies,
                (int)entry.GetMethod("SparseSize")!.Invoke(null, null)!, (int)entry.GetMethod("SparseBytes")!.Invoke(null, null)!);
        }
        finally { context.Unload(); }
    }

    /// <summary>Normalizes API ownership and class-reference input passing while retaining dispatch and framing.</summary>
    private sealed class ApiOwnerNames : CSharpSyntaxRewriter
    {
        /// <summary>Maps payload API owners and operation spellings without changing dispatch or framing syntax.</summary>
        /// <param name="node">A generated invocation.</param>
        /// <returns>The canonical invocation or the unchanged call.</returns>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (node.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax compiledCall } compiledAccess)
            {
                var arguments = SyntaxFactory.SeparatedList(compiledCall.TypeArgumentList.Arguments.Where(argument =>
                    !argument.ToString().StartsWith("global::LuminPack.Option.LuminPack", StringComparison.Ordinal)));
                node = node.WithExpression(compiledAccess.WithName(arguments.Count == 0 ? SyntaxFactory.IdentifierName(compiledCall.Identifier) :
                    compiledCall.WithTypeArgumentList(compiledCall.TypeArgumentList.WithArguments(arguments))));
            }
            if (node.Expression is not MemberAccessExpressionSyntax access ||
                !(access.Expression.ToString().Contains("LuminPackExtensions_", StringComparison.Ordinal) ||
                  access.Expression.ToString() == "global::LuminPack.LuminPackSerializer")) return base.VisitInvocationExpression(node);
            string name = access.Name.Identifier.ValueText switch
            {
                "WritePolymorphismValue" => "SerializeUnion",
                "WriteValue" => "WritePayload",
                "Serialize" or "SerializeJson" => "WritePayload",
                "CalculateOffset" => "Sizeof",
                _ => access.Name.Identifier.ValueText
            };
            var canonical = node.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("PayloadApi"), access.Name.WithIdentifier(SyntaxFactory.Identifier(name))));
            if (canonical.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax profileCall } &&
                profileCall.TypeArgumentList.Arguments.Last().ToString().Contains("LuminPack.Option.", StringComparison.Ordinal))
            {
                var arguments = profileCall.TypeArgumentList.Arguments.RemoveAt(profileCall.TypeArgumentList.Arguments.Count - 1);
                canonical = canonical.WithExpression(((MemberAccessExpressionSyntax)canonical.Expression).WithName(arguments.Count == 0
                    ? SyntaxFactory.IdentifierName(name) : profileCall.WithTypeArgumentList(profileCall.TypeArgumentList.WithArguments(arguments))));
            }
            // Omitting `in` still binds value-type `in` parameters. Class payloads now
            // carry the same object reference in a register instead of a local stack slot.
            if (name == "SerializeUnion" && canonical.ArgumentList.Arguments.Count == 2 &&
                canonical.ArgumentList.Arguments[1].RefKindKeyword.IsKind(SyntaxKind.InKeyword))
            {
                var arguments = canonical.ArgumentList.Arguments;
                canonical = canonical.WithArgumentList(canonical.ArgumentList.WithArguments(arguments.Replace(arguments[1], arguments[1].WithRefKindKeyword(default))));
            }
            return canonical;
        }

        /// <summary>Normalizes the explicitly approved replacement of the runtime encoding snapshot with a compiled constant.</summary>
        /// <param name="node">An encoding property access in the canonical Union body.</param>
        /// <returns>The original option spelling for historical mechanism comparison.</returns>
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => node.Expression is DefaultExpressionSyntax && node.Name.Identifier.ValueText == "StringEncoding"
                ? SyntaxFactory.ParseExpression("writer.Option.StringEncoding") : base.VisitMemberAccessExpression(node);
    }

    /// <summary>Reports a mechanism or historical parity failure.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
