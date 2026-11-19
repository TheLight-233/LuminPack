using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static partial class SerializerCodegenPerformanceRegression
{
    /// <summary>Rejects consumer-defined profile spellings before relying on built-in byte bounds.</summary>
    /// <param name="references">The actual platform and runtime references.</param>
    private static void VerifyProvenProfileIdentity(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        foreach (string profile in new[] { "length", "default", "fixed" })
        {
            string source = ProvenProfileFixture.Replace("GenerationMode.MODE", "GenerationMode." + mode);
            if (profile == "default") source = source.Replace("LuminPackUtf16LengthOptions", "LuminPackDefaultOptions");
            if (profile == "fixed") source = source.Replace("struct LuminPackUtf16LengthOptions : ILuminPackOptions", "struct LuminPackFixedOptions<T> : ILuminPackOptions where T:struct,ILuminPackOptions")
                .Replace("Record<LuminPack.Option.LuminPackUtf16LengthOptions>", "Record<LuminPack.Option.LuminPackFixedOptions<LuminPack.Option.LuminPackUtf16LengthOptions>>");
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var input = ProvenInput(source, parse, references);
            var driver = ProvenDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
            RequireProven(errors.Length == 0, string.Join("\n", errors.Select(item => item.ToString())));
            string generated = CaptureProven(driver);
            RequireProven(!generated.Contains("WriteOriginalLeaf", StringComparison.Ordinal) && !generated.Contains("WriteOriginalObject", StringComparison.Ordinal),
                "A consumer-defined profile entered a built-in complete-record proof.");
            RequireProven(ExecuteProven(output) == "Passed shadowed profile bytes, getter counts, checked record bounds and failed cursor/prefix observations.",
                "A shadowed built-in profile differs from the equivalent original custom profile.");
        }
        Console.WriteLine("Passed three shadowed profile identities across four tiers, including exact bytes, getter counts and failed prefix/cursor observations.");
    }

    private const string ProvenProfileFixture = """
        using System;
        using System.Buffers;
        using System.Linq;
        using System.Runtime.InteropServices;
        using System.Security.Cryptography;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        namespace LuminPack.Option
        {
            public readonly struct LuminPackUtf16LengthOptions : ILuminPackOptions
            {
                private global::LuminPack.Option.LuminPackUtf8LengthStrings ObservedCodec
                {
                    get
                    {
                        _ = StringEncoding;
                        return default;
                    }
                }

                public int RecordBound(string? value) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).RecordBound(value);

                public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                    ref ObservedCodec.WriteRecord(ref writer, ref cursor, value);

                public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                    ObservedCodec.Write(ref writer, value);

                public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
                    ObservedCodec.Read(ref reader, current);

                public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
                    ObservedCodec.ReadPair(ref reader, ref first, ref second);

                public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
                    ObservedCodec.Skip(ref reader, maxStringLength);

                public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
                    ObservedCodec.ReadSlots(ref reader, slots);

                public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

                public int Measure(string? value) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Measure(value);

                public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                    ObservedCodec.WriteReserved(ref writer, value);

                public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
                    ObservedCodec.WriteText(ref writer, text);

                public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

                public bool SupportsUtf16Runs() =>
                    false;

                public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WriteSlots(ref writer, values);

                public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WriteCheckedSlots(ref writer, values);

                public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
                    ObservedCodec.WritePairs(ref writer, keys, values);

                public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
                    ObservedCodec.WritePairRuns(ref writer, keys, values, allowRuns);

                public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                    ref ObservedCodec.WriteAtCursor(ref writer, ref cursor, value);

                public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteSlotPrefix(ref writer, values);

                public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WritePairPrefix(ref writer, pairs);

                public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekCharacterCount(ref reader, out count);

                public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
                    default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

                public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encoding;

                public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Recording;

                public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).BinaryProfile;

                public LuminPackStringEncoding StringEncoding { get { Observer.Gets++; return LuminPackStringEncoding.UTF8; } }
                public LuminPackStringRecording StringRecording { get { Observer.Gets++; return LuminPackStringRecording.Length; } }
                public StandardFormat StandardFormat => new('G');
                public int MaxJsonDepth => 64;
                public bool ValidateJsonSyntax => false;
            }
        }
        public readonly struct ControlProfile : ILuminPackOptions
        {
            private global::LuminPack.Option.LuminPackUtf8LengthStrings ObservedCodec
            {
                get
                {
                    _ = StringEncoding;
                    return default;
                }
            }

            public int RecordBound(string? value) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).RecordBound(value);

            public ref byte WriteRecord(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                ref ObservedCodec.WriteRecord(ref writer, ref cursor, value);

            public void Write(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                ObservedCodec.Write(ref writer, value);

            public string? Read(scoped ref global::LuminPack.Core.LuminPackReader reader, string? current) =>
                ObservedCodec.Read(ref reader, current);

            public void ReadPair(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped ref string? first, scoped ref string? second) =>
                ObservedCodec.ReadPair(ref reader, ref first, ref second);

            public void Skip(scoped ref global::LuminPack.Core.LuminPackReader reader, int maxStringLength) =>
                ObservedCodec.Skip(ref reader, maxStringLength);

            public void ReadSlots(scoped ref global::LuminPack.Core.LuminPackReader reader, scoped global::System.Span<string?> slots) =>
                ObservedCodec.ReadSlots(ref reader, slots);

            public global::System.Buffers.OperationStatus Encode(global::System.ReadOnlySpan<char> source, global::System.Span<byte> destination, out int charsRead, out int bytesWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encode(source, destination, out charsRead, out bytesWritten, replaceInvalidSequences, isFinalBlock);

            public int Measure(string? value) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Measure(value);

            public void WriteReserved(scoped ref global::LuminPack.Core.LuminPackWriter writer, string? value) =>
                ObservedCodec.WriteReserved(ref writer, value);

            public void WriteText(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<char> text) =>
                ObservedCodec.WriteText(ref writer, text);

            public global::System.Buffers.OperationStatus Decode(global::System.ReadOnlySpan<byte> source, global::System.Span<char> destination, out int bytesRead, out int charsWritten, bool replaceInvalidSequences, bool isFinalBlock) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Decode(source, destination, out bytesRead, out charsWritten, replaceInvalidSequences, isFinalBlock);

            public bool SupportsUtf16Runs() =>
                false;

            public void WriteSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                ObservedCodec.WriteSlots(ref writer, values);

            public void WriteCheckedSlots(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                ObservedCodec.WriteCheckedSlots(ref writer, values);

            public void WritePairs(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values) =>
                ObservedCodec.WritePairs(ref writer, keys, values);

            public void WritePairRuns(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> keys, scoped global::System.ReadOnlySpan<string?> values, bool allowRuns) =>
                ObservedCodec.WritePairRuns(ref writer, keys, values, allowRuns);

            public ref byte WriteAtCursor(scoped ref global::LuminPack.Core.LuminPackWriter writer, ref byte cursor, string? value) =>
                ref ObservedCodec.WriteAtCursor(ref writer, ref cursor, value);

            public int WriteSlotPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<string?> values) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WriteSlotPrefix(ref writer, values);

            public int WritePairPrefix(scoped ref global::LuminPack.Core.LuminPackWriter writer, scoped global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<string, string?>> pairs) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).WritePairPrefix(ref writer, pairs);

            public bool TryPeekCharacterCount(scoped ref global::LuminPack.Core.LuminPackReader reader, out int count) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekCharacterCount(ref reader, out count);

            public bool TryPeekUtf16Payload(scoped ref global::LuminPack.Core.LuminPackReader reader, int length, out int bytes, out bool latin1) =>
                default(global::LuminPack.Option.LuminPackUtf8LengthStrings).TryPeekUtf16Payload(ref reader, length, out bytes, out latin1);

            public global::LuminPack.Option.LuminPackStringEncoding Encoding => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Encoding;

            public global::LuminPack.Option.LuminPackStringRecording Recording => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).Recording;

            public byte BinaryProfile => default(global::LuminPack.Option.LuminPackUtf8LengthStrings).BinaryProfile;

            public LuminPackStringEncoding StringEncoding { get { Observer.Gets++; return LuminPackStringEncoding.UTF8; } }
            public LuminPackStringRecording StringRecording { get { Observer.Gets++; return LuminPackStringRecording.Length; } }
            public StandardFormat StandardFormat => new('G');
            public int MaxJsonDepth => 64;
            public bool ValidateJsonSyntax => false;
        }
        public static class Observer { public static int Gets; }
        [LuminPackable] public sealed class Payload { public byte Head; public string Text; public long Tail; }
        public static class Entry
        {
            public static string Check()
            {
                var value=new Payload {Head=37,Text=new string('\u4E2D',100),Tail=long.MinValue};
                foreach(bool array in new[]{false,true})
                foreach(int width in new[]{0,1,210,225,512})
                {
                    string left=Record<LuminPack.Option.LuminPackUtf16LengthOptions>(value,array,width);
                    string right=Record<ControlProfile>(value,array,width);
                    if(left!=right)throw new InvalidOperationException("Shadow profile differs from equivalent custom profile: "+array+"/"+width+" "+left+" versus "+right);
                }
                return "Passed shadowed profile bytes, getter counts, checked record bounds and failed cursor/prefix observations.";
            }
            private static unsafe string Record<P>(Payload source,bool array,int width) where P:struct,ILuminPackOptions
            {
                Observer.Gets=0;byte[] storage=Enumerable.Repeat((byte)0xCC,4096).ToArray();string error="none";int cursor;
                fixed(byte* pointer=storage)
                {
                    Span<byte> span=new(pointer,width);var writer=new LuminPackWriter(ref span);
                    try
                    {
                        if(array){Payload[] values=[source];LuminPackSerializer.Serialize<Payload[], P>(ref writer,in values);}
                        else LuminPackSerializer.Serialize<Payload, P>(ref writer,in source);
                    }
                    catch(Exception exception){error=exception.GetType().FullName;}
                    cursor=writer.Position;
                }
                return error+":"+cursor+":"+Observer.Gets+":"+Convert.ToHexString(SHA256.HashData(storage));
            }
        }
        """;
}
