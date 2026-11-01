using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerCompiledOptionRegression
{
    /// <summary>Checks scalar proof selection, nested accounting and callback invalidation in both emission tiers.</summary>
    /// <param name="parse">The consumer language settings.</param>
    /// <param name="references">The actual runtime metadata.</param>
    private static void VerifyFixedInput(CSharpParseOptions parse, MetadataReference[] references)
    {
        VerifyStringInput(parse, references);
        const string source = """
            using System;
            using System.IO;
            using LuminPack;
            using LuminPack.Attribute;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofScalar
            {
                public int Number;
                public bool Flag;
                public long Stamp;
                public static int Callbacks;
                /*CALLBACK*/
            }
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofHolder { public ProofScalar Child; public bool Tail; }
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofConstructed
            {
                public int Number { get; }
                public bool Flag { get; }
                [LuminPackConstructor]
                public ProofConstructed(int number, bool flag) { Number=number; Flag=flag; }
            }
            public static class Entry
            {
                public static void Check()
                {
                    var value = new ProofScalar { Number=17, Flag=true, Stamp=123456789 };
                    byte[] bytes = LuminPackSerializer.Serialize(value);
                    var target = new ProofScalar();
                    var previous = target;
                    if (LuminPackSerializer.Deserialize(bytes, ref target) != bytes.Length || target.Stamp != value.Stamp ||
                        !target.Flag || !ReferenceEquals(previous, target) || ProofScalar.Callbacks != EXPECT_CALLBACK)
                        throw new Exception("The fixed root changed decoding, reuse or callbacks.");
                    bytes[61] = 2;
                    target.Number = 91; target.Flag = false;
                    bool rejected = false;
                    try { LuminPackSerializer.Deserialize(bytes, ref target); }
                    catch (InvalidDataException) { rejected = true; }
                    if (!rejected || target.Number != 91 || target.Flag)
                        throw new Exception("A fixed root published a noncanonical Boolean.");
                    var holder = new ProofHolder { Child = value, Tail = true };
                    bytes = LuminPackSerializer.Serialize(holder);
                    ProofHolder copy = null;
                    LuminPackSerializer.Deserialize(bytes, ref copy);
                    if (copy.Child.Stamp != value.Stamp || !copy.Tail)
                        throw new Exception("A nested fixed root lost its enclosing accounting contract.");
                    bytes[62] = 2;
                    copy = new ProofHolder(); rejected = false;
                    try { LuminPackSerializer.Deserialize(bytes, ref copy); }
                    catch (InvalidDataException) { rejected = true; }
                    if (!rejected || copy.Child != null) throw new Exception("An invalid nested scalar was published.");
                    bytes = LuminPackSerializer.Serialize((ProofScalar)null);
                    LuminPackSerializer.Deserialize(bytes, ref target);
                    if (!ReferenceEquals(previous, target)) throw new Exception("Null root behavior changed.");
                    var constructed = new ProofConstructed(23, true);
                    bytes = LuminPackSerializer.Serialize(constructed);
                    ProofConstructed constructedCopy = null;
                    LuminPackSerializer.Deserialize(bytes, ref constructedCopy);
                    var originalConstructed = constructedCopy;
                    LuminPackSerializer.Deserialize(bytes, ref constructedCopy);
                    if (constructedCopy.Number != 23 || !constructedCopy.Flag || !ReferenceEquals(originalConstructed, constructedCopy))
                        throw new Exception("The fixed proof changed constructor or readonly storage reuse.");
                }
            }
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            var tree = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode).Replace("EXPECT_CALLBACK", "0"), parse, "FixedInput.cs");
            var input = TestCompilation.Create("FixedInput" + mode, [tree], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            VerifyFixedInputOutput(ref driver, input, parse, true);
            var callback = CSharpSyntaxTree.ParseText(source.Replace("MODE", mode).Replace("EXPECT_CALLBACK", "1")
                .Replace("/*CALLBACK*/", "[LuminPackOnDeserialized] public void ReadCompleted() { Callbacks++; }"), parse, "FixedInput.cs");
            VerifyFixedInputOutput(ref driver, input.ReplaceSyntaxTree(tree, callback), parse, false);
            VerifyFixedInputOutput(ref driver, input, parse, true);
        }
    }

    /// <summary>Checks the original string profiles, field scopes and failure publication on proven roots.</summary>
    /// <param name="parse">The consumer language settings.</param>
    /// <param name="references">The actual runtime metadata.</param>
    private static void VerifyStringInput(CSharpParseOptions parse, MetadataReference[] references)
    {
        const string model = """
            [LuminPackable, LuminPackOption(UntrustedData=true, StringEncoding=LuminPackStringEncoding.ENCODING, StringRecording=LuminPackStringRecording.RECORDING)]
            public class ProofTextINDEX { public int Number; public string Text; public bool Flag; public string More; }
            """;
        const string check = """
            foreach (string text in new string[] { null, "", "ascii", "caf\u00e9", "\u4e2d\ud83d\ude00" })
            {
                string expectedINDEX = EXPECTED;
                var valueINDEX = new ProofTextINDEX { Number=17, Text=text, Flag=true, More=text };
                byte[] bytesINDEX = LuminPackSerializer.Serialize(valueINDEX);
                ProofTextINDEX targetINDEX = null;
                if (LuminPackSerializer.Deserialize(bytesINDEX, ref targetINDEX) != bytesINDEX.Length || targetINDEX.Number != 17 ||
                    targetINDEX.Text != expectedINDEX || targetINDEX.More != expectedINDEX || !targetINDEX.Flag)
                    throw new Exception("The proven string decoder changed a declared profile.");
                var originalINDEX = targetINDEX;
                var originalTextINDEX = targetINDEX.Text;
                LuminPackSerializer.Deserialize(bytesINDEX, ref targetINDEX);
                if (!ReferenceEquals(originalINDEX, targetINDEX) || !ReferenceEquals(originalTextINDEX, targetINDEX.Text))
                    throw new Exception("The proven string decoder stopped reusing storage.");
                for (int length=0; length<bytesINDEX.Length; length++)
                {
                    bool rejected=false;
                    try { LuminPackSerializer.Deserialize(bytesINDEX.AsSpan(0,length), ref targetINDEX); }
                    catch (Exception) { rejected=true; }
                    if (!rejected) throw new Exception("A truncated proven string frame was accepted.");
                }
                byte[] forgedINDEX = (byte[])bytesINDEX.Clone(); forgedINDEX[56]=0;
                targetINDEX = new ProofTextINDEX { Number=91 };
                bool failedINDEX=false;
                try { LuminPackSerializer.Deserialize(forgedINDEX, ref targetINDEX); }
                catch (InvalidDataException) { failedINDEX=true; }
                if (!failedINDEX || targetINDEX.Number != 91) throw new Exception("An invalid marker was published.");
            }
            """;
        var declarations = new System.Text.StringBuilder();
        var checks = new System.Text.StringBuilder();
        for (int index = 0; index < 6; index++)
        {
            string encoding = index % 2 == 0 ? "UTF8" : "UTF16";
            string recording = index < 2 ? "Token" : index < 4 ? "Length" : "VarInt";
            declarations.Append(model.Replace("INDEX", index.ToString()).Replace("ENCODING", encoding).Replace("RECORDING", recording));
            checks.Append(check.Replace("INDEX", index.ToString()).Replace("EXPECTED", index < 2 ? "text ?? string.Empty" : "text"));
        }
        const string scoped = """
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofTextScoped
            {
                [LuminPackOption(StringEncoding=LuminPackStringEncoding.UTF8, StringRecording=LuminPackStringRecording.VarInt)] public string Text;
                public bool Flag;
            }
            [LuminPackable, LuminPackOption(UntrustedData=true, MaxStringLength=4)]
            public class ProofTextLimited { public string Text; }
            [LuminPackable, LuminPackOption(UntrustedData=true)]
            public class ProofTextCallback
            {
                public string Text;
                public static int Calls;
                [LuminPackOnDeserialized] public void Complete() { Calls++; }
            }
            """;
        const string scopedCheck = """
            var scopedValue = new ProofTextScoped { Text="\u4e2d\ud83d\ude00", Flag=true };
            byte[] scopedBytes = LuminPackSerializer.Serialize(scopedValue);
            ProofTextScoped scopedCopy = null;
            LuminPackSerializer.Deserialize(scopedBytes, ref scopedCopy);
            if (scopedCopy.Text != scopedValue.Text || !scopedCopy.Flag) throw new Exception("A member profile was lost.");
            scopedBytes[scopedBytes.Length-1]=2;
            bool scopedFailed=false;
            try { LuminPackSerializer.Deserialize(scopedBytes, ref scopedCopy); }
            catch (InvalidDataException) { scopedFailed=true; }
            if (!scopedFailed) throw new Exception("A Boolean after a string bypassed validation.");
            byte[] limitedBytes = LuminPackSerializer.Serialize(new ProofTextLimited { Text="four" });
            ProofTextLimited limited = null;
            LuminPackSerializer.Deserialize(limitedBytes, ref limited);
            if (limited.Text != "four") throw new Exception("A conservative proof rejected valid guarded input.");
            byte[] callbackBytes = LuminPackSerializer.Serialize(new ProofTextCallback { Text="callback" });
            ProofTextCallback callback = null;
            LuminPackSerializer.Deserialize(callbackBytes, ref callback);
            if (callback.Text != "callback" || ProofTextCallback.Calls != 1) throw new Exception("String callbacks changed.");
            """;
        foreach (string mode in new[] { "Full", "Minimal" })
        {
            string source = "using System; using System.IO; using LuminPack; using LuminPack.Attribute; using LuminPack.Option; " +
                "[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode." + mode + ")]" + declarations + scoped +
                "public static class Entry { public static void Check() { " + checks + scopedCheck + " } }";
            Execute("ProvenString" + mode, source, parse, references);
        }
    }

    /// <summary>Compiles and executes retained output while comparing its complete source with a fresh driver.</summary>
    /// <param name="driver">The retained generator state.</param>
    /// <param name="input">The current scalar contract.</param>
    /// <param name="parse">The source language settings.</param>
    /// <param name="proven">Whether callback-free fixed admission should be available.</param>
    private static void VerifyFixedInputOutput(ref GeneratorDriver driver, Compilation input, CSharpParseOptions parse, bool proven)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        var errors = diagnostics.Concat(output.GetDiagnostics()).Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
        string[] sources = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal).ToArray();
        if (sources.Where(text => text.StartsWith("ProofScalar.Extension", StringComparison.Ordinal))
            .Any(text => text.Contains(".TryOpenFixedFrame(")) != proven)
            throw new InvalidOperationException("The fixed proof did not track the callback contract.");
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
        fresh = fresh.RunGenerators(input);
        var reference = fresh.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
            .Select(file => file.HintName + "\n" + file.SourceText).Order(StringComparer.Ordinal);
        if (!sources.SequenceEqual(reference)) throw new InvalidOperationException("The retained fixed proof differs from fresh output.");
        using var image = new MemoryStream();
        var emit = output.Emit(image);
        if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        Assembly.Load(image.ToArray()).GetType("Entry")!.GetMethod("Check")!.Invoke(null, null);
    }
}
