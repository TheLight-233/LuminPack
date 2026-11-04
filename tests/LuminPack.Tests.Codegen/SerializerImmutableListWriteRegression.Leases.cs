using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Checks sorted-list fallback leases and fixed-pair output in both formats.</summary>
    /// <param name="references">The platform and runtime compilation references.</param>
    internal static void RunLeases(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var compilation = CSharpCompilation.Create("CollectionLeases" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(LeaseSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            foreach (bool forceFallback in new[] { false, true })
            {
                Compilation tested = output;
                if (forceFallback)
                    foreach (var tree in output.SyntaxTrees)
                    {
                        string text = tree.GetText().ToString();
                        string forced = text.Replace("global::LuminPack.Code.LuminPackCollectionAccess.TryGetSortedListStorage", "global::LeaseEntry.RejectSortedStorage", StringComparison.Ordinal);
                        if (forced != text) tested = tested.ReplaceSyntaxTree(tree, tree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(forced)));
                    }
                using var image = new MemoryStream(); var result = tested.Emit(image);
                Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
                System.Reflection.Assembly.Load(image.ToArray()).GetType("LeaseEntry")!.GetMethod("Check")!.Invoke(null, null);
            }
        }
        Console.WriteLine("Passed sorted-list staging lease return, clearing and fixed-pair output in four profiles and four generation modes, including forced storage fallback.");
    }

    private const string LeaseSource = """
        global using System.Linq;
        using System;
        using System.Buffers;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable(GeneratorType.Custom)] public sealed class LeasePayload
        {
            public int Number;
            public string Text;
            public static bool Fail;
            public static readonly Exception Failure = new Exception("Selected element failure.");
            public void Before() { if (Fail) throw Failure; }
            public static void Serialize(ref global::LuminPack.Core.LuminPackWriter writer, in LeasePayload value) { value.Before(); writer.Write(value.Number); }
            public static void Deserialize(ref global::LuminPack.Core.LuminPackReader reader, ref LeasePayload value) { value = new LeasePayload { Number = reader.Read<int>() }; }
            public static void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, in LeasePayload value) { value.Before(); writer.Write(value.Number); }
            public static void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref LeasePayload value) { value = new LeasePayload { Number = reader.Read<int, LuminPackDefaultOptions>() }; }
        }
        public sealed class DerivedSortedList : SortedList<int, LeasePayload> { }
        public static class LeaseEntry
        {
            public static bool RejectSortedStorage<TKey, TValue>(SortedList<TKey, TValue> value, out TKey[] keys, out TValue[] values) where TKey : notnull { keys = null; values = null; return false; }
            public static void Check()
            {
                CheckProfile<LuminPackUtf8LengthOptions>();
                CheckProfile<LuminPackUtf16LengthOptions>();
                CheckProfile<LuminPackUtf8TokenOptions>();
                CheckProfile<LuminPackUtf16TokenOptions>();
            }
            private static void CheckProfile<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                unsafe { LuminPackSerializer.Register<LeasePayload, TProfile>(&LeasePayload.Serialize, &LeasePayload.Deserialize, &LeasePayload.SerializeJson, &LeasePayload.DeserializeJson); }
                CheckScalarPairs<TProfile>();
                CheckStringPairs<TProfile>();
                const int count = 33;
                var pairs = Enumerable.Range(0, count).Select(i => new KeyValuePair<int, LeasePayload>(i, new LeasePayload { Number = i, Text = "value" })).ToArray();
                var immutable = ImmutableDictionary.CreateRange(pairs);
                var immutableSorted = ImmutableSortedDictionary.CreateRange(pairs);
                var sorted = new SortedDictionary<int, LeasePayload>(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));
                SortedList<int, LeasePayload> list = new DerivedSortedList();
                foreach (var pair in pairs) list.Add(pair.Key, pair.Value);
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    Action[] writes = {
                        () => { buffer.ResetCore(); if (json) LuminPackSerializer.SerializeJson<TProfile>(immutable, buffer); else LuminPackSerializer.Serialize<TProfile>(immutable, buffer); },
                        () => { buffer.ResetCore(); if (json) LuminPackSerializer.SerializeJson<TProfile>(immutableSorted, buffer); else LuminPackSerializer.Serialize<TProfile>(immutableSorted, buffer); },
                        () => { buffer.ResetCore(); if (json) LuminPackSerializer.SerializeJson<TProfile>(sorted, buffer); else LuminPackSerializer.Serialize<TProfile>(sorted, buffer); },
                        () => { buffer.ResetCore(); if (json) LuminPackSerializer.SerializeJson<TProfile>(list, buffer); else LuminPackSerializer.Serialize<TProfile>(list, buffer); }
                    };
                    string[] labels = { "ImmutableDictionary", "ImmutableSortedDictionary", "SortedDictionary", "SortedList fallback" };
                    var failures = new List<string>();
                    for (int i = 0; i < writes.Length; i++)
                    {
                        Action write = writes[i]; LeasePayload.Fail = false; write();
                        byte[] expected = buffer.GetSpan().ToArray();
                        for (int attempt = 0; i == 3 && attempt < 3; attempt++)
                        {
                            LeasePayload.Fail = true;
                            try
                            {
                                CheckReturn<int>(write, count); CheckReturn<LeasePayload>(write, count);
                            }
                            catch (Exception error) { failures.Add(labels[i] + ": " + error.Message); break; }
                            finally { LeasePayload.Fail = false; }
                        }
                        write();
                        if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("A successful record changed after an element failure.");
                    }
                    if (failures.Count != 0) throw new Exception("JSON=" + json + "; " + string.Join("; ", failures));
                }
            }
            private static void CheckStringPairs<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                var value = new SortedList<string, string>(StringComparer.Ordinal) { ["a"] = "text", ["b"] = null, ["c"] = "\"\\\n\u6c34\uD83D\uDE80" };
                using var buffer = new LuminBufferWriter(true);
                foreach (bool json in new[] { false, true })
                {
                    buffer.ResetCore();
                    SortedList<string, string> result = null;
                    if (json) { LuminPackSerializer.SerializeJson<TProfile>(value, buffer); LuminPackSerializer.DeserializeJson<TProfile>(buffer.GetSpan(), ref result); }
                    else { LuminPackSerializer.Serialize<TProfile>(value, buffer); LuminPackSerializer.Deserialize<TProfile>(buffer.GetSpan(), ref result); }
                    var expected = !json && default(TProfile).StringRecording == LuminPackStringRecording.Token
                        ? value.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Value ?? string.Empty)) : value;
                    if (!result.SequenceEqual(expected)) throw new Exception("String-pair output changed across the storage fallback.");
                }
            }
            private static void CheckScalarPairs<TProfile>() where TProfile : struct, ILuminPackOptions
            {
                foreach (int count in new[] { 0, 1, 33, 5000 })
                {
                    var pairs = Enumerable.Range(0, count).Select(i => new KeyValuePair<int, int>(i, i * 3)).ToArray();
                    var immutable = ImmutableDictionary.CreateRange(pairs);
                    var immutableSorted = ImmutableSortedDictionary.CreateRange(pairs);
                    var sorted = new SortedDictionary<int, int>(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));
                    var list = new SortedList<int, int>(sorted);
                    IEnumerable<KeyValuePair<int, int>>[] entries = { immutable, immutableSorted, sorted, list };
                    using var buffer = new LuminBufferWriter(true);
                    foreach (bool json in new[] { false, true })
                    {
                        Action[] writes = {
                            () => { if (json) LuminPackSerializer.SerializeJson<TProfile>(immutable, buffer); else LuminPackSerializer.Serialize<TProfile>(immutable, buffer); },
                            () => { if (json) LuminPackSerializer.SerializeJson<TProfile>(immutableSorted, buffer); else LuminPackSerializer.Serialize<TProfile>(immutableSorted, buffer); },
                            () => { if (json) LuminPackSerializer.SerializeJson<TProfile>(sorted, buffer); else LuminPackSerializer.Serialize<TProfile>(sorted, buffer); },
                            () => { if (json) LuminPackSerializer.SerializeJson<TProfile>(list, buffer); else LuminPackSerializer.Serialize<TProfile>(list, buffer); }
                        };
                        for (int index = 0; index < writes.Length; index++)
                        {
                            byte[] expected;
                            if (json)
                            {
                                string text = "[" + string.Join(",", entries[index].Select(pair => "[" + pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]")) + "]";
                                var encoding = default(TProfile).StringEncoding == LuminPackStringEncoding.UTF16 ? System.Text.Encoding.Unicode : System.Text.Encoding.UTF8;
                                expected = encoding.GetBytes(text);
                            }
                            else
                            {
                                expected = new byte[4 + count * 8];
                                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(expected, count);
                                int offset = 4;
                                foreach (var pair in entries[index])
                                {
                                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset), pair.Key);
                                    System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(offset + 4), pair.Value);
                                    offset += 8;
                                }
                            }
                            buffer.ResetCore(); writes[index]();
                            if (!buffer.GetSpan().SequenceEqual(expected)) throw new Exception("Fixed-pair output changed during staging or buffer growth.");
                        }
                    }
                }
            }
            private static void CheckReturn<T>(Action write, int count)
            {
                var pool = ArrayPool<T>.Shared;
                T[] expected = pool.Rent(count); pool.Return(expected, clearArray: true);
                bool failed = false;
                try { write(); }
                catch (Exception error) { if (!ReferenceEquals(error, LeasePayload.Failure)) throw; failed = true; }
                T[] actual = pool.Rent(count);
                try
                {
                    if (!failed) throw new Exception("The element callback did not fail.");
                    if (!ReferenceEquals(actual, expected)) throw new Exception("The failed writer did not return its lease.");
                    if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>() && actual.Any(item => !EqualityComparer<T>.Default.Equals(item, default)))
                        throw new Exception("Returned staging storage retained element references.");
                }
                finally { pool.Return(actual, clearArray: true); }
            }
        }
        """;
}
