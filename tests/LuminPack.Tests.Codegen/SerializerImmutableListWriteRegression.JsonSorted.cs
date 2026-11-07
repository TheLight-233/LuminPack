using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks sorted-list JSON traversal against public enumeration, including mutations and compatibility fallback.</summary>
    /// <param name="references">The actual platform and LuminPack runtime references.</param>
    internal static void RunSortedJson(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var compilation = CSharpCompilation.Create("SortedJson" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(SortedJsonSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(output.SyntaxTrees.Any(tree => tree.GetText().ToString().Contains(".TryGetSortedListTraversal(", StringComparison.Ordinal)), "The referenced traversal API was not selected.");
            string? expectedState = null;
            foreach (bool fallback in new[] { false, true })
            {
                Compilation tested = output;
                if (fallback)
                    foreach (var tree in output.SyntaxTrees)
                    {
                        string text = tree.GetText().ToString();
                        string forced = text.Replace("global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListTraversal", "global::SortedJsonEntry.RejectTraversal", StringComparison.Ordinal);
                        if (text != forced) tested = tested.ReplaceSyntaxTree(tree, tree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(forced)));
                    }
                using var image = new MemoryStream(); var result = tested.Emit(image);
                Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
                var entry = System.Reflection.Assembly.Load(image.ToArray()).GetType("SortedJsonEntry")!;
                entry.GetMethod("Check")!.Invoke(null, [fallback]);
                string state = (string)entry.GetMethod("MutationState")!.Invoke(null, null)!;
                Require(expectedState is null || state == expectedState, "Mutation output or writer state differs between checked storage and public enumeration.");
                expectedState = state;
            }
        }
        Console.WriteLine("Passed sorted-list JSON pair capture, mutation timing, scalar/nested allocations and fallback in four profiles and four generation modes.");
    }

    private const string SortedJsonSource = """
        global using System.Linq;
        using System;
        using System.Collections.Generic;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class SortedEnvelope { public SortedList<int, int> Pairs; }
        [LuminPackable(GeneratorType.Custom)] public sealed class SortedMutationKey : IComparable<SortedMutationKey>
        {
            public int Number;
            public int CompareTo(SortedMutationKey other) => Number.CompareTo(other.Number);
            public static void Serialize(ref LuminPackWriter writer, in SortedMutationKey value) { SortedJsonEntry.Before("k", value.Number); writer.Write(value.Number); }
            public static void Deserialize(ref LuminPackReader reader, ref SortedMutationKey value) { value = new SortedMutationKey { Number = reader.Read<int>() }; }
            public static void SerializeJson(ref LuminPackJsonWriter writer, in SortedMutationKey value) { SortedJsonEntry.Before("k", value.Number); writer.Write(value.Number); }
            public static void DeserializeJson(ref LuminPackJsonReader reader, ref SortedMutationKey value) { value = new SortedMutationKey { Number = reader.Read<int, LuminPackDefaultOptions>() }; }
        }
        [LuminPackable(GeneratorType.Custom)] public sealed class SortedMutationValue
        {
            public int Number;
            public static void Serialize(ref LuminPackWriter writer, in SortedMutationValue value) { SortedJsonEntry.Before("v", value.Number); writer.Write(value.Number); }
            public static void Deserialize(ref LuminPackReader reader, ref SortedMutationValue value) { value = new SortedMutationValue { Number = reader.Read<int>() }; }
            public static void SerializeJson(ref LuminPackJsonWriter writer, in SortedMutationValue value) { SortedJsonEntry.Before("v", value.Number); writer.Write(value.Number); }
            public static void DeserializeJson(ref LuminPackJsonReader reader, ref SortedMutationValue value) { value = new SortedMutationValue { Number = reader.Read<int, LuminPackDefaultOptions>() }; }
        }
        public sealed class SortedDerived : SortedList<SortedMutationKey, SortedMutationValue> { }
        public static class SortedJsonEntry
        {
            private static SortedList<SortedMutationKey, SortedMutationValue> active;
            private static readonly List<string> events = new List<string>();
            private static readonly HashSet<Type> registered = new HashSet<Type>();
            private static readonly Exception failure = new Exception("Selected formatter failure.");
            private static int mutation, at;
            private static string component;
            private static bool changed;
            public static bool RejectTraversal<TKey, TValue>(SortedList<TKey, TValue> list, out ReadOnlySpan<TKey> keys, out ReadOnlySpan<TValue> values, out int version) where TKey : notnull { keys = default; values = default; version = 0; return false; }
            public static void Check(bool fallback)
            {
                CheckProfile<LuminPackUtf8LengthOptions>(fallback);
                CheckProfile<LuminPackUtf16LengthOptions>(fallback);
                CheckProfile<LuminPackUtf8TokenOptions>(fallback);
                CheckProfile<LuminPackUtf16TokenOptions>(fallback);
            }
            public static void Before(string part, int number)
            {
                events.Add(part + number);
                if (changed || component != part || number != at) return;
                changed = true;
                switch (mutation)
                {
                    case 1: active[active.Keys[0]] = new SortedMutationValue { Number = 99 }; break;
                    case 2: active.RemoveAt(0); break;
                    case 3: active.Clear(); break;
                    case 4: active.Add(new SortedMutationKey { Number = 9 }, new SortedMutationValue { Number = 9 }); break;
                    case 5: active.Capacity += 17; break;
                    case 6: throw failure;
                    case 7: active.TrimExcess(); break;
                }
            }
            private static void Reset(bool derived, bool reverse)
            {
                active = derived ? new SortedDerived() : new SortedList<SortedMutationKey, SortedMutationValue>(Comparer<SortedMutationKey>.Create((left, right) => reverse ? right.Number.CompareTo(left.Number) : left.Number.CompareTo(right.Number)));
                for (int i = 0; i < 3; i++) active.Add(new SortedMutationKey { Number = i }, new SortedMutationValue { Number = i });
                active.Capacity = 40;
                events.Clear(); changed = false;
            }
            private static Exception Observe(Action action)
            {
                try { action(); return null; } catch (Exception error) { return error; }
            }
            public static string MutationState()
                => MutationState<LuminPackUtf8LengthOptions>() + "\n" + MutationState<LuminPackUtf16LengthOptions>() + "\n" + MutationState<LuminPackUtf8TokenOptions>() + "\n" + MutationState<LuminPackUtf16TokenOptions>();
            private static string MutationState<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                Register<TProfile>();
                var results = new List<string>();
                using var buffer = new LuminBufferWriter(true);
                foreach (string part in new[] { "k", "v" })
                foreach (int trigger in new[] { 0, 1, 2 })
                for (int change = 0; change <= 7; change++)
                {
                    component = part; at = trigger; mutation = change; Reset(false, false); buffer.ResetCore();
                    var writer = new LuminPackJsonWriter(buffer);
                    string error = "success";
                    try { LuminPackSerializer.SerializeJson<TProfile>(ref writer, in active); }
                    catch (Exception exception) { error = exception.GetType().FullName; }
                    results.Add(error + ":" + writer.Position + ":" + writer.Depth + ":" + writer.IsFirstElement + ":" + Convert.ToBase64String(writer.Buffer.Slice(0, writer.Position).ToArray()) + ":" + string.Join(",", events));
                }
                return string.Join("\n", results);
            }
            private static void Register<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                if (!registered.Add(typeof(TProfile))) return;
                unsafe
                {
                    LuminPackSerializer.Register<SortedMutationKey, TProfile>(SortedMutationKey.Serialize, SortedMutationKey.Deserialize, SortedMutationKey.SerializeJson, SortedMutationKey.DeserializeJson);
                    LuminPackSerializer.Register<SortedMutationValue, TProfile>(SortedMutationValue.Serialize, SortedMutationValue.Deserialize, SortedMutationValue.SerializeJson, SortedMutationValue.DeserializeJson);
                }
            }
            private static void CheckProfile<TProfile>(bool fallback) where TProfile : struct, ILuminPackOptions
            {
                Register<TProfile>();
                using var buffer = new LuminBufferWriter(true);
                foreach (bool derived in new[] { false, true })
                foreach (bool reverse in new[] { false, true })
                foreach (string part in new[] { "k", "v" })
                foreach (int trigger in new[] { 0, 1, 2 })
                for (int change = 0; change <= 7; change++)
                {
                    component = part; at = trigger; mutation = change;
                    Reset(derived, reverse);
                    Exception expectedError = Observe(() => { foreach (var pair in active) { Before("k", pair.Key.Number); Before("v", pair.Value.Number); } });
                    string[] expectedEvents = events.ToArray();
                    Reset(derived, reverse); buffer.ResetCore();
                    Exception actualError = Observe(() => LuminPackSerializer.SerializeJson<TProfile>(active, buffer));
                    if (actualError?.GetType() != expectedError?.GetType() || !events.SequenceEqual(expectedEvents))
                        throw new Exception("Sorted-list mutation or pair capture differs from public enumeration: " + change + "/" + part + "/" + trigger);
                    if (expectedError == failure && actualError != failure) throw new Exception("A formatter exception was replaced.");
                }
                mutation = 0;
                foreach (int count in new[] { 0, 1, 33, 5000 })
                {
                    var list = new SortedList<int, int>();
                    for (int i = 0; i < count; i++) list.Add(i, i * 3);
                    string text = "[" + string.Join(",", list.Select(pair => "[" + pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]")) + "]";
                    var encoding = default(TProfile).StringEncoding == LuminPackStringEncoding.UTF16 ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8;
                    buffer.ResetCore(); LuminPackSerializer.SerializeJson<TProfile>(list, buffer);
                    if (!buffer.GetSpan().SequenceEqual(encoding.GetBytes(text))) throw new Exception("Sorted-list JSON bytes changed.");
                    SortedList<int, int> decoded = null;
                    LuminPackSerializer.DeserializeJson<TProfile>(buffer.GetSpan(), ref decoded);
                    if (!decoded.SequenceEqual(list)) throw new Exception("Sorted-list JSON round trip changed.");
                }
                var values = new SortedList<int, int> { [1] = 3, [2] = 6, [3] = 9 };
                var graph = new SortedEnvelope { Pairs = values };
                foreach (bool json in new[] { false, true })
                foreach (bool nested in new[] { false, true })
                {
                    Action write = () => { buffer.ResetCore(); if (nested) { if (json) LuminPackSerializer.SerializeJson<TProfile>(graph, buffer); else LuminPackSerializer.Serialize<TProfile>(graph, buffer); } else { if (json) LuminPackSerializer.SerializeJson<TProfile>(values, buffer); else LuminPackSerializer.Serialize<TProfile>(values, buffer); } };
                    for (int i = 0; i < 128; i++) write();
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 128; i++) write();
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    if (!fallback && allocated != 0) throw new Exception("Warmed sorted-list write allocated: " + allocated);
                }
            }
        }
        """;
}
