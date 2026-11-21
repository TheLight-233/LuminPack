using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Exercises exact immutable interface writers and the retained custom traversal in every generation tier.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void RunInterfaces(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var compilation = CSharpCompilation.Create("ImmutableInterfaces" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(InterfaceSource.Replace("MODE", mode, StringComparison.Ordinal) + UncachedQueueSource, parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release)).WithLegacyPackableSyntax();
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            System.Reflection.Assembly.Load(image.ToArray()).GetType("InterfaceEntry")!.GetMethod("Check")!.Invoke(null, null);
            var roots = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(source => source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => method.Identifier.ValueText == "RootWriteJson" && method.ParameterList.Parameters.Count == 2 &&
                    method.ParameterList.Parameters[1].Type!.ToString().Contains(".IImmutable", StringComparison.Ordinal)).ToArray();
            Require(roots.Length >= 5 && roots.All(method => method.ToString().Contains("WriteKnownElements", StringComparison.Ordinal)),
                "An immutable interface omitted its concrete writer entries.");
            Require(roots.All(method => method.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any(local =>
                local.Identifier.ValueText.StartsWith("WriteKnownElements", StringComparison.Ordinal) && local.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                local.AttributeLists.ToString().Contains("NoInlining", StringComparison.Ordinal))), "Concrete iterator state lost its native boundary.");
        }
        CheckTraversalCapabilities(references);
        Console.WriteLine("Passed immutable interface writes, custom fallbacks, queue traversal, JSON tree lease cleanup, metadata changes and four generation tiers.");
    }

    private const string InterfaceSource = """
        global using System.Linq;
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Reflection;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public sealed class CollisionComparer : IEqualityComparer<int>
        {
            public static readonly CollisionComparer Instance = new CollisionComparer();
            public bool Equals(int x, int y) => x == y;
            public int GetHashCode(int value) => value % 3;
        }
        [LuminPackable] public sealed class EntryItem : IComparable<EntryItem>
        {
            public int Id;
            public string Text;
            public static readonly List<int> Events = new List<int>(4096);
            public static int ThrowOn = -1;
            public static readonly Exception Failure = new Exception("Element callback failure.");
            public int CompareTo(EntryItem other) => other is null ? 1 : Id.CompareTo(other.Id);
            public override int GetHashCode() => Id % 3;
            [LuminPackOnSerializing] public void Before() { Events.Add(Id); if (Id == ThrowOn) throw Failure; }
        }
        public class TraceProxy : DispatchProxy
        {
            public object Target;
            public int Enumerations, Disposals;
            protected override object Invoke(MethodInfo method, object[] args)
            {
                if (method.Name == "GetEnumerator")
                {
                    Enumerations++;
                    if (Target is IEnumerable<KeyValuePair<int, EntryItem>> pairs)
                        return new TraceEnumerator<KeyValuePair<int, EntryItem>>(pairs.GetEnumerator(), this);
                    return new TraceEnumerator<EntryItem>(((IEnumerable<EntryItem>)Target).GetEnumerator(), this);
                }
                try { return method.Invoke(Target, args); }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }
            public static T Wrap<T>(T target) where T : class
            {
                var proxy = DispatchProxy.Create<T, TraceProxy>(); ((TraceProxy)(object)proxy).Target = target; return proxy;
            }
        }
        public sealed class TraceEnumerator<T> : IEnumerator<T>
        {
            private readonly IEnumerator<T> inner;
            private readonly TraceProxy owner;
            public TraceEnumerator(IEnumerator<T> inner, TraceProxy owner) { this.inner = inner; this.owner = owner; }
            public T Current => inner.Current;
            object IEnumerator.Current => Current;
            public bool MoveNext() => inner.MoveNext();
            public void Reset() => inner.Reset();
            public void Dispose() { owner.Disposals++; inner.Dispose(); }
        }
        [LuminPackable] public sealed class InterfaceGraph
        {
            public IImmutableList<EntryItem> List;
            public IImmutableSet<EntryItem> Set;
            public IImmutableQueue<EntryItem> Queue;
            public IImmutableStack<EntryItem> Stack;
            public IImmutableDictionary<int, EntryItem> Dictionary;
        }
        public static class InterfaceEntry
        {
            public static void Check()
            {
                UncachedQueueCases.Check();
                CheckProfile<LuminPackUtf8LengthOptions>();
                CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();
                CheckProfile<LuminPackUtf16TokenOptions>();
            }
            private static void CheckProfile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                foreach (int length in new[] { 0, 1, 33 })
                {
                    var items = Enumerable.Range(0, length).Select(i => new EntryItem { Id = i, Text = i == 3 ? new string('x', 40000) : "a\"\\\n\u65e5\ud83d\ude00" }).ToArray();
                    if (items.Length > 5) items[5] = null;
                    var pairs = items.Select((item, index) => new KeyValuePair<int, EntryItem>(index, item));
                    CheckList<TProfile>(ImmutableList.CreateRange(items));
                    CheckList<TProfile>(ImmutableArray.CreateRange(items));
                    CheckSet<TProfile>(ImmutableHashSet.CreateRange(items));
                    CheckSet<TProfile>(ImmutableSortedSet.CreateRange(items));
                    CheckQueue<TProfile>(ImmutableQueue.CreateRange(items));
                    CheckStack<TProfile>(ImmutableStack.CreateRange(items));
                    CheckDictionary<TProfile>(ImmutableDictionary.CreateRange(CollisionComparer.Instance, pairs));
                    CheckDictionary<TProfile>(ImmutableSortedDictionary.CreateRange(pairs));
                    CheckConcreteAndGraph<TProfile>(items);
                    if (length != 0)
                    {
                        CheckList<TProfile>(TraceProxy.Wrap<IImmutableList<EntryItem>>(ImmutableList.CreateRange(items)));
                        CheckSet<TProfile>(TraceProxy.Wrap<IImmutableSet<EntryItem>>(ImmutableHashSet.CreateRange(items)));
                        CheckQueue<TProfile>(TraceProxy.Wrap<IImmutableQueue<EntryItem>>(ImmutableQueue.CreateRange(items)));
                        CheckStack<TProfile>(TraceProxy.Wrap<IImmutableStack<EntryItem>>(ImmutableStack.CreateRange(items)));
                        CheckDictionary<TProfile>(TraceProxy.Wrap<IImmutableDictionary<int, EntryItem>>(ImmutableDictionary.CreateRange(pairs)));
                    }
                }
                CheckList<TProfile>(null); CheckSet<TProfile>(null); CheckQueue<TProfile>(null); CheckStack<TProfile>(null); CheckDictionary<TProfile>(null);
                IImmutableList<EntryItem> invalid = default(ImmutableArray<EntryItem>);
                using var buffer = new LuminBufferWriter(true);
                for (int format = 0; format < 2; format++)
                {
                    buffer.ResetWrittenCount(); bool failed = false;
                    Exception expectedError = null;
                    try { if (format == 0) ((IEnumerable<EntryItem>)invalid).GetEnumerator(); else _ = invalid.Count; }
                    catch (Exception error) { expectedError = error; }
                    try { if (format == 0) LuminPackSerializer.Serialize<IImmutableList<EntryItem>, TProfile>(invalid, buffer); else LuminPackSerializer.SerializeJson<IImmutableList<EntryItem>, TProfile>(invalid, buffer); }
                    catch (Exception error) { failed = error.GetType() == expectedError?.GetType() && error.Message == expectedError.Message; }
                    if (!failed) throw new Exception("A default boxed array changed interface failure behavior.");
                }
            }
            private static void CheckConcreteAndGraph<TProfile>(EntryItem[] items) where TProfile : struct, ILuminPackOptions
            {
                var set = ImmutableHashSet.CreateRange(items);
                var dictionary = ImmutableDictionary.CreateRange(CollisionComparer.Instance, items.Select((item, index) => new KeyValuePair<int, EntryItem>(index, item)));
                var graph = new InterfaceGraph { List = ImmutableArray.CreateRange(items), Set = set,
                    Queue = ImmutableQueue.CreateRange(items), Stack = ImmutableStack.CreateRange(items), Dictionary = dictionary };
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<InterfaceGraph, TProfile>(graph, buffer); else LuminPackSerializer.Serialize<InterfaceGraph, TProfile>(graph, buffer); };
                    write();
                    InterfaceGraph result = null;
                    if (json) LuminPackSerializer.DeserializeJson<InterfaceGraph, TProfile>(buffer.GetSpan(), ref result); else LuminPackSerializer.Deserialize<InterfaceGraph, TProfile>(buffer.GetSpan(), ref result);
                    if (result.List.Count != items.Length || result.Set.Count != items.Length || result.Dictionary.Count != items.Length || !result.Queue.Select(i => i?.Id).SequenceEqual(graph.Queue.Select(i => i?.Id)) || !result.Stack.Select(i => i?.Id).SequenceEqual(graph.Stack.Select(i => i?.Id)))
                        throw new Exception("Nested immutable interface state or ordering changed.");
                    CheckAllocation(write, "Nested immutable interface graph");
                }
                Action writeSet = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); LuminPackSerializer.SerializeJson<ImmutableHashSet<EntryItem>, TProfile>(set, buffer); };
                writeSet(); byte[] setBytes = buffer.GetSpan().ToArray();
                buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<IImmutableSet<EntryItem>, TProfile>((IImmutableSet<EntryItem>)set, buffer);
                if (!buffer.GetSpan().SequenceEqual(setBytes)) throw new Exception("Concrete and interface set JSON differ.");
                CheckAllocation(writeSet, "Concrete hash set JSON");
                Action writeDictionary = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); LuminPackSerializer.SerializeJson<ImmutableDictionary<int, EntryItem>, TProfile>(dictionary, buffer); };
                writeDictionary(); byte[] dictionaryBytes = buffer.GetSpan().ToArray();
                buffer.ResetWrittenCount(); LuminPackSerializer.SerializeJson<IImmutableDictionary<int, EntryItem>, TProfile>((IImmutableDictionary<int, EntryItem>)dictionary, buffer);
                if (!buffer.GetSpan().SequenceEqual(dictionaryBytes)) throw new Exception("Concrete and interface dictionary JSON differ.");
                CheckAllocation(writeDictionary, "Concrete hash dictionary JSON");
            }
            private static void CheckAllocation(Action write, string label)
            {
                for (int warm = 0; warm < 128; warm++) write();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int repeat = 0; repeat < 16; repeat++) write();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated != 0) throw new Exception(label + " allocated " + allocated + " bytes.");
            }
            private static void CheckList<TProfile>(IImmutableList<EntryItem> value) where TProfile : struct, ILuminPackOptions
            {
                var items = value?.ToArray();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    EntryItem.ThrowOn = -1; EntryItem.Events.Clear();
                    byte[] expected;
                    using (var oracle = new LuminBufferWriter(true))
                    {
                        if (json) LuminPackSerializer.SerializeJson<EntryItem[], TProfile>(items, oracle); else LuminPackSerializer.Serialize<EntryItem[], TProfile>(items, oracle);
                        expected = oracle.GetSpan().ToArray();
                    }
                    int[] events = EntryItem.Events.ToArray();
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<IImmutableList<EntryItem>, TProfile>(value, buffer); else LuminPackSerializer.Serialize<IImmutableList<EntryItem>, TProfile>(value, buffer); };
                    write();
                    if (!buffer.GetSpan().SequenceEqual(expected) || !EntryItem.Events.SequenceEqual(events)) throw new Exception("List bytes, options or callbacks differ.");
                    if (value is TraceProxy custom)
                    {
                        if (custom.Enumerations == 0 || custom.Enumerations != custom.Disposals) throw new Exception("Custom List enumeration or cleanup was bypassed.");
                    }
                    else
                    {
                        for (int warm = 0; warm < 128; warm++) write();
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int repeat = 0; repeat < 16; repeat++) write();
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        if (allocated != 0) throw new Exception("List retained writer allocated " + allocated + " bytes; JSON " + json + "; " + value?.GetType());
                    }
                    if (events.Length != 0)
                    {
                        EntryItem.ThrowOn = events[events.Length / 2]; bool failed = false;
                        try { write(); } catch (Exception error) { if (!ReferenceEquals(error, EntryItem.Failure)) throw; failed = true; }
                        finally { EntryItem.ThrowOn = -1; }
                        if (!failed) throw new Exception("An element failure was suppressed.");
                        if (value is TraceProxy proxy && proxy.Enumerations != proxy.Disposals) throw new Exception("Failed custom List traversal leaked its enumerator.");
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("List could not write after failure.");
                    }
                }
            }
            private static void CheckSet<TProfile>(IImmutableSet<EntryItem> value) where TProfile : struct, ILuminPackOptions
            {
                var items = value?.ToArray();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    EntryItem.ThrowOn = -1; EntryItem.Events.Clear();
                    byte[] expected;
                    using (var oracle = new LuminBufferWriter(true))
                    {
                        if (json) LuminPackSerializer.SerializeJson<EntryItem[], TProfile>(items, oracle); else LuminPackSerializer.Serialize<EntryItem[], TProfile>(items, oracle);
                        expected = oracle.GetSpan().ToArray();
                    }
                    int[] events = EntryItem.Events.ToArray();
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<IImmutableSet<EntryItem>, TProfile>(value, buffer); else LuminPackSerializer.Serialize<IImmutableSet<EntryItem>, TProfile>(value, buffer); };
                    write();
                    if (!buffer.GetSpan().SequenceEqual(expected) || !EntryItem.Events.SequenceEqual(events)) throw new Exception("Set bytes, options or callbacks differ.");
                    if (value is TraceProxy custom)
                    {
                        if (custom.Enumerations == 0 || custom.Enumerations != custom.Disposals) throw new Exception("Custom Set enumeration or cleanup was bypassed.");
                    }
                    else
                    {
                        for (int warm = 0; warm < 128; warm++) write();
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int repeat = 0; repeat < 16; repeat++) write();
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        if (allocated != 0) throw new Exception("Set retained writer allocated " + allocated + " bytes; JSON " + json + "; " + value?.GetType());
                    }
                    if (events.Length != 0)
                    {
                        EntryItem.ThrowOn = events[events.Length / 2]; bool failed = false;
                        try { write(); } catch (Exception error) { if (!ReferenceEquals(error, EntryItem.Failure)) throw; failed = true; }
                        finally { EntryItem.ThrowOn = -1; }
                        if (!failed) throw new Exception("An element failure was suppressed.");
                        if (value is TraceProxy proxy && proxy.Enumerations != proxy.Disposals) throw new Exception("Failed custom Set traversal leaked its enumerator.");
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Set could not write after failure.");
                    }
                }
            }
            private static void CheckQueue<TProfile>(IImmutableQueue<EntryItem> value) where TProfile : struct, ILuminPackOptions
            {
                var items = value?.ToArray();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    EntryItem.ThrowOn = -1; EntryItem.Events.Clear();
                    byte[] expected;
                    using (var oracle = new LuminBufferWriter(true))
                    {
                        if (json) LuminPackSerializer.SerializeJson<EntryItem[], TProfile>(items, oracle); else LuminPackSerializer.Serialize<EntryItem[], TProfile>(items, oracle);
                        expected = oracle.GetSpan().ToArray();
                    }
                    int[] events = EntryItem.Events.ToArray();
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<IImmutableQueue<EntryItem>, TProfile>(value, buffer); else LuminPackSerializer.Serialize<IImmutableQueue<EntryItem>, TProfile>(value, buffer); };
                    write();
                    if (!buffer.GetSpan().SequenceEqual(expected) || !EntryItem.Events.SequenceEqual(events)) throw new Exception("Queue bytes, options or callbacks differ.");
                    if (value is TraceProxy custom)
                    {
                        if (custom.Enumerations == 0 || custom.Enumerations != custom.Disposals) throw new Exception("Custom Queue enumeration or cleanup was bypassed.");
                    }
                    else
                    {
                        for (int warm = 0; warm < 128; warm++) write();
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int repeat = 0; repeat < 16; repeat++) write();
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        if (allocated != 0) throw new Exception("Queue retained writer allocated " + allocated + " bytes; JSON " + json + "; " + value?.GetType());
                    }
                    if (events.Length != 0)
                    {
                        EntryItem.ThrowOn = events[events.Length / 2]; bool failed = false;
                        try { write(); } catch (Exception error) { if (!ReferenceEquals(error, EntryItem.Failure)) throw; failed = true; }
                        finally { EntryItem.ThrowOn = -1; }
                        if (!failed) throw new Exception("An element failure was suppressed.");
                        if (value is TraceProxy proxy && proxy.Enumerations != proxy.Disposals) throw new Exception("Failed custom Queue traversal leaked its enumerator.");
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Queue could not write after failure.");
                    }
                }
            }
            private static void CheckStack<TProfile>(IImmutableStack<EntryItem> value) where TProfile : struct, ILuminPackOptions
            {
                var items = value?.ToArray();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    EntryItem.ThrowOn = -1; EntryItem.Events.Clear();
                    byte[] expected;
                    using (var oracle = new LuminBufferWriter(true))
                    {
                        if (json) LuminPackSerializer.SerializeJson<EntryItem[], TProfile>(items, oracle); else LuminPackSerializer.Serialize<EntryItem[], TProfile>(items, oracle);
                        expected = oracle.GetSpan().ToArray();
                    }
                    int[] events = EntryItem.Events.ToArray();
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<IImmutableStack<EntryItem>, TProfile>(value, buffer); else LuminPackSerializer.Serialize<IImmutableStack<EntryItem>, TProfile>(value, buffer); };
                    write();
                    if (!buffer.GetSpan().SequenceEqual(expected) || !EntryItem.Events.SequenceEqual(events)) throw new Exception("Stack bytes, options or callbacks differ.");
                    if (value is TraceProxy custom)
                    {
                        if (custom.Enumerations == 0 || custom.Enumerations != custom.Disposals) throw new Exception("Custom Stack enumeration or cleanup was bypassed.");
                    }
                    else
                    {
                        for (int warm = 0; warm < 128; warm++) write();
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int repeat = 0; repeat < 16; repeat++) write();
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        if (allocated != 0) throw new Exception("Stack retained writer allocated " + allocated + " bytes; JSON " + json + "; " + value?.GetType());
                    }
                    if (events.Length != 0)
                    {
                        EntryItem.ThrowOn = events[events.Length / 2]; bool failed = false;
                        try { write(); } catch (Exception error) { if (!ReferenceEquals(error, EntryItem.Failure)) throw; failed = true; }
                        finally { EntryItem.ThrowOn = -1; }
                        if (!failed) throw new Exception("An element failure was suppressed.");
                        if (value is TraceProxy proxy && proxy.Enumerations != proxy.Disposals) throw new Exception("Failed custom Stack traversal leaked its enumerator.");
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Stack could not write after failure.");
                    }
                }
            }
            private static void CheckDictionary<TProfile>(IImmutableDictionary<int, EntryItem> value) where TProfile : struct, ILuminPackOptions
            {
                var items = value?.ToArray();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    EntryItem.ThrowOn = -1; EntryItem.Events.Clear();
                    byte[] expected;
                    using (var oracle = new LuminBufferWriter(true))
                    {
                        if (json) LuminPackSerializer.SerializeJson<KeyValuePair<int, EntryItem>[], TProfile>(items, oracle); else LuminPackSerializer.Serialize<KeyValuePair<int, EntryItem>[], TProfile>(items, oracle);
                        expected = oracle.GetSpan().ToArray();
                    }
                    int[] events = EntryItem.Events.ToArray();
                    Action write = () => { buffer.ResetWrittenCount(); EntryItem.Events.Clear(); if (json) LuminPackSerializer.SerializeJson<IImmutableDictionary<int, EntryItem>, TProfile>(value, buffer); else LuminPackSerializer.Serialize<IImmutableDictionary<int, EntryItem>, TProfile>(value, buffer); };
                    write();
                    if (!buffer.GetSpan().SequenceEqual(expected) || !EntryItem.Events.SequenceEqual(events)) throw new Exception("Dictionary bytes, options or callbacks differ.");
                    if (value is TraceProxy custom)
                    {
                        if (custom.Enumerations == 0 || custom.Enumerations != custom.Disposals) throw new Exception("Custom Dictionary enumeration or cleanup was bypassed.");
                    }
                    else
                    {
                        for (int warm = 0; warm < 128; warm++) write();
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        for (int repeat = 0; repeat < 16; repeat++) write();
                        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                        if (allocated != 0) throw new Exception("Dictionary retained writer allocated " + allocated + " bytes; JSON " + json + "; " + value?.GetType());
                    }
                    if (events.Length != 0)
                    {
                        EntryItem.ThrowOn = events[events.Length / 2]; bool failed = false;
                        try { write(); } catch (Exception error) { if (!ReferenceEquals(error, EntryItem.Failure)) throw; failed = true; }
                        finally { EntryItem.ThrowOn = -1; }
                        if (!failed) throw new Exception("An element failure was suppressed.");
                        if (value is TraceProxy proxy && proxy.Enumerations != proxy.Disposals) throw new Exception("Failed custom Dictionary traversal leaked its enumerator.");
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Dictionary could not write after failure.");
                    }
                }
            }
        }
        """;
}
