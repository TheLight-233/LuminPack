using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks shared JSON sequence loops, concurrent emptiness queries and iterator failure state.</summary>
internal static partial class SerializerJsonEnumerableWriteRegression
{
    /// <summary>Executes independent byte and iterator-state comparisons in every generation mode and profile.</summary>
    /// <param name="references">The actual platform and runtime metadata references.</param>
    internal static void Run(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("JsonSequences" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(BuildSource(mode), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            CheckElementFormatters(output);
            using var image = new MemoryStream(); var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("SequenceEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed shared JSON sequence bytes, iterator failure state and concurrent controls in four profiles and four generation modes.");
    }

    /// <summary>Checks that every queue uses the ordinary element formatter.</summary>
    /// <param name="output">The generated consumer compilation.</param>
    private static void CheckElementFormatters(Compilation output)
    {
        int selected = 0, excluded = 0;
        foreach (var tree in output.SyntaxTrees.Skip(1))
        foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.ValueText != "RootWriteJson" || method.ParameterList.Parameters.Count != 2) continue;
            var symbol = output.GetSemanticModel(tree).GetDeclaredSymbol(method);
            if (symbol?.Parameters[1].Type is not INamedTypeSymbol type ||
                type.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.Queue<T>") continue;
            bool integer = type.TypeArguments[0].SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64;
            bool usesEntry = method.ToString().Contains(".WriteEnumerationElement", StringComparison.Ordinal);
            Require(!usesEntry, "Enumeration bypassed the ordinary element formatter: " + type);
            if (integer) selected++; else excluded++;
        }
        Require(selected >= 2 && excluded >= 4, "Enumeration policy selection was not exercised.");
    }

    /// <summary>Builds concrete calls for the shared sequence families without relying on generic registration.</summary>
    /// <param name="mode">The generation mode under test.</param>
    /// <returns>The executable consumer source.</returns>
    internal static string BuildSource(string mode)
    {
        string[] types = ["Queue<T>", "Stack<T>", "LinkedList<T>", "HashSet<T>", "SortedSet<T>",
            "ConcurrentQueue<T>", "ConcurrentStack<T>", "ConcurrentBag<T>", "BlockingCollection<T>",
            "Collection<T>", "ObservableCollection<T>", "ReadOnlyCollection<T>", "ReadOnlyObservableCollection<T>",
            "IEnumerable<T>", "ICollection<T>", "IReadOnlyCollection<T>", "IList<T>", "IReadOnlyList<T>", "ISet<T>"];
        var calls = new System.Text.StringBuilder();
        foreach (string element in new[] { "int", "long", "string" })
        foreach (string template in types)
        {
            string type = template.Replace("T", element, StringComparison.Ordinal);
            string items = element == "int" ? "ints" : element == "long" ? "longs" : "texts";
            string argument = template switch
            {
                "BlockingCollection<T>" => "new ConcurrentQueue<" + element + ">(" + items + ")",
                "Collection<T>" or "ReadOnlyCollection<T>" => "new List<" + element + ">(" + items + ")",
                "ReadOnlyObservableCollection<T>" => "new ObservableCollection<" + element + ">(" + items + ")",
                _ => items
            };
            string expression = template.StartsWith("I", StringComparison.Ordinal)
                ? "new " + (template == "ISet<T>" ? "HashSet<" : "List<") + element + ">(" + items + ")"
                : "new " + type + "(" + argument + ")";
            calls.AppendLine("{ " + type + " value = " + expression + "; string expected = Expected(value); buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<" + type + ", TProfile>(value, buffer); EqualText<TProfile>(buffer, expected); (value as IDisposable)?.Dispose(); }");
        }
        calls.Append(BuildImmutableCases());
        return Source.Replace("MODE", mode, StringComparison.Ordinal).Replace("SEQUENCE_CASES", calls.ToString(), StringComparison.Ordinal) + BoundarySource;
    }

    /// <summary>Reports a focused contract violation.</summary>
    /// <param name="condition">Whether the contract holds.</param>
    /// <param name="message">The failed condition.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private const string Source = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Concurrent;
        using System.Collections.ObjectModel;
        using System.Collections.Immutable;
        using System.Collections.Frozen;
        using System.Linq;
        using System.Text;
        using System.Text.Json;
        using System.Text.Encodings.Web;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public enum SequenceNumber { One = 1, Two = 2 }
        [LuminPackable] public sealed class SequenceValue { public int Number; }
        [LuminPackable] public sealed class SequenceGraph { public ConcurrentStack<int> Values; }
        public static partial class SequenceEntry
        {
            private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            public static void Check()
            {
                Profile<LuminPackUtf8LengthOptions>(); Profile<LuminPackUtf16LengthOptions>();
                Profile<LuminPackUtf8TokenOptions>(); Profile<LuminPackUtf16TokenOptions>();
            }
            public static void Profile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                using var buffer = new LuminBufferWriter(true);
                foreach (int count in new[] { 0, 1, 33, 5000 })
                {
                    int[] ints = Enumerable.Range(0,count).Select(i => i % 2 == 0 ? int.MinValue + i : int.MaxValue - i).ToArray();
                    long[] longs = Enumerable.Range(0,count).Select(i => i % 2 == 0 ? long.MinValue + i : long.MaxValue - i).ToArray();
                    string[] texts = Enumerable.Range(0,count).Select(i => i % 6 == 0 ? null : i % 6 == 1 ? "" : i % 6 == 2 ? "abcdef" : i % 6 == 3 ? "quote\"slash\\line\n\u96EA" : i % 6 == 4 ? "\u00E9\uD83D\uDE00" : new string('x',80)).ToArray();
                    SEQUENCE_CASES
                    var dict = new ConcurrentDictionary<int,int>(); foreach (int item in ints) dict[item] = item;
                    string dictText = "[" + string.Join(",",dict.Select(pair => "[" + pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]")) + "]";
                    buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<ConcurrentDictionary<int, int>, TProfile>(dict, buffer); EqualText<TProfile>(buffer,dictText);
                    var graph = new SequenceGraph { Values = new ConcurrentStack<int>(ints) };
                    buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<SequenceGraph, TProfile>(graph,buffer); EqualText<TProfile>(buffer,"{\"Values\":" + Expected(graph.Values) + "}");
                    buffer.ResetWrittenCount(); LuminPackSerializer.Serialize<SequenceGraph, TProfile>(graph,buffer);
                    SequenceGraph decoded = null; LuminPackSerializer.Deserialize<SequenceGraph, TProfile>(buffer.GetSpan(),ref decoded);
                    if (!decoded.Values.SequenceEqual(graph.Values)) throw new Exception("Nested binary stack control changed.");
                }
                for (int fault = 0; fault <= 4; fault++)
                {
                    string expected = FaultState<TProfile>(fault,false), actual = FaultState<TProfile>(fault,true);
                    if (expected != actual) throw new Exception("Iterator exception state changed: " + fault + " / " + expected + " / " + actual);
                }
                var nullableValues = new Queue<int?>(new int?[] { 1, null, -2 });
                buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<Queue<int?>, TProfile>(nullableValues,buffer); EqualText<TProfile>(buffer,"[1,null,-2]");
                var enumValues = new Queue<SequenceNumber>(new[] { SequenceNumber.One, SequenceNumber.Two });
                buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<Queue<SequenceNumber>, TProfile>(enumValues,buffer); EqualText<TProfile>(buffer,"[1,2]");
                var objectValues = new Queue<SequenceValue>(new[] { new SequenceValue { Number = 3 } });
                buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<Queue<SequenceValue>, TProfile>(objectValues,buffer); EqualText<TProfile>(buffer,"[{\"Number\":3}]");
                BoundaryProfile<TProfile>();
                ConcurrentQueue<int> nullQueue = null; buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<ConcurrentQueue<int>, TProfile>(nullQueue,buffer); EqualText<TProfile>(buffer,"null");
            }
            // The fixture's supplementary character is literal in LuminPack's established wire representation.
            private static string Expected<T>(IEnumerable<T> values) => JsonSerializer.Serialize(values.ToArray(), jsonOptions).Replace("\\uD83D\\uDE00", "\uD83D\uDE00");
            private static void EqualText<TProfile>(LuminBufferWriter buffer,string expected) where TProfile : struct, ILuminPackOptions
            {
                var encoding = default(TProfile).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
                if (!buffer.GetSpan().SequenceEqual(encoding.GetBytes(expected))) throw new Exception("Independent JSON bytes differ. Actual: " + encoding.GetString(buffer.GetSpan().Slice(0,Math.Min(buffer.GetSpan().Length,200))) + " Expected: " + expected.Substring(0,Math.Min(expected.Length,200)));
            }
            private static string FaultState<TProfile>(int fault,bool generated) where TProfile : struct, ILuminPackOptions
            {
                using var buffer = new LuminBufferWriter(true); var writer = new LuminPackJsonWriter(buffer);
                var sequence = new FaultSequence(fault); IEnumerable<int> value = sequence; Exception caught = null;
                try
                {
                    if (generated) LuminPackSerializer.SerializeJson<IEnumerable<int>, TProfile>(ref writer,in value);
                    else
                    {
                        writer.WriteArrayStart<TProfile>(); bool separate = false;
                        foreach (int item in value)
                        {
                            if (separate) writer.WriteByteRaw<TProfile>((byte)','); separate = true;
                            writer.SetFirstElement(true); writer.Write<TProfile>(item);
                        }
                        writer.WriteArrayEnd<TProfile>();
                    }
                }
                catch (Exception error) { caught = error; }
                if (caught != null && !ReferenceEquals(caught, sequence.Failure)) throw new Exception("Iterator exception identity changed.");
                return (caught != null) + ":" + writer.Position + ":" + writer.Depth + ":" + writer.IsFirstElement + ":" + Convert.ToBase64String(writer.Buffer.Slice(0,writer.Position).ToArray()) + ":" + string.Join(",",sequence.Events);
            }
        }
        public sealed class FaultSequence : IEnumerable<int>
        {
            public readonly Exception Failure = new Exception("Selected iterator failure."); public readonly List<string> Events = new List<string>(); private readonly int fault;
            public FaultSequence(int fault) { this.fault=fault; }
            public IEnumerator<int> GetEnumerator() { Events.Add("get"); if(fault==1)throw Failure;return new Cursor(this); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            private sealed class Cursor : IEnumerator<int>
            {
                private readonly FaultSequence owner; private int index=-1;
                public Cursor(FaultSequence owner) { this.owner=owner; }
                public bool MoveNext() { owner.Events.Add("move");if(++index==1 && owner.fault==2)throw owner.Failure;return index<3; }
                public int Current { get { owner.Events.Add("current");if(index==1 && owner.fault==3)throw owner.Failure;return index; } }
                object IEnumerator.Current => Current;
                public void Reset() => throw new NotSupportedException();
                public void Dispose() { owner.Events.Add("dispose");if(owner.fault==4)throw owner.Failure; }
            }
        }
        """;
}
