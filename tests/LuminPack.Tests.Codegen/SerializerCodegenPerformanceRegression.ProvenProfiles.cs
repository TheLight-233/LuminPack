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
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
            var input = ProvenInput(source, parse, references);
            var driver = ProvenDriver(parse).RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
            RequireProven(errors.Length == 0, string.Join("\n", errors.Select(item => item.ToString())));
            string generated = CaptureProven(driver);
            RequireProven(!generated.Contains("__writeOriginalLeaf", StringComparison.Ordinal) && !generated.Contains("__writeOriginalObject", StringComparison.Ordinal),
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
                public LuminPackStringEncoding StringEncoding { get { Observer.Gets++; return LuminPackStringEncoding.UTF8; } }
                public LuminPackStringRecording StringRecording { get { Observer.Gets++; return LuminPackStringRecording.Length; } }
                public StandardFormat StandardFormat => new('G');
                public int MaxJsonDepth => 64;
                public bool ValidateJsonSyntax => false;
            }
        }
        public readonly struct ControlProfile : ILuminPackOptions
        {
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
                        if(array){Payload[] values=[source];LuminPackSerializer.Serialize<P>(ref writer,in values);}
                        else LuminPackSerializer.Serialize<P>(ref writer,in source);
                    }
                    catch(Exception exception){error=exception.GetType().FullName;}
                    cursor=writer.Position;
                }
                return error+":"+cursor+":"+Observer.Gets+":"+Convert.ToHexString(SHA256.HashData(storage));
            }
        }
        """;
}
