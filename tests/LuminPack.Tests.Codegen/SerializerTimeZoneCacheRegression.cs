using System.Reflection;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>Checks concrete snapshot entry generation, profile propagation and nested operation boundaries.</summary>
internal static class SerializerTimeZoneCacheRegression
{
    /// <summary>Compiles and executes the time-zone contract in each generation tier.</summary>
    /// <param name="references">The runtime and platform compilation references.</param>
    internal static void Run(MetadataReference[] references)
    {
        const string source = """
            using System;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            [assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class TimeZoneEnvelope { public TimeZoneInfo Zone; public int Tail; }
            public static class TimeZoneCases
            {
                public static byte[] Write(TimeZoneInfo zone) => LuminPackSerializer.Serialize(zone);
                public static int Read(byte[] bytes, ref TimeZoneInfo zone) => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref zone);
                public static void Check()
                {
                    var zone = TimeZoneInfo.CreateCustomTimeZone("Codegen/Zone", TimeSpan.FromMinutes(345), "Display", "Standard");
                    byte[] record = Write(zone);
                    TimeZoneInfo copy = null;
                    if (Read(record, ref copy) != record.Length || copy.ToSerializedString() != zone.ToSerializedString())
                        throw new Exception("Concrete snapshot mismatch");
                    var container = new TimeZoneEnvelope { Zone = zone, Tail = 137 };
                    byte[] nested = LuminPackSerializer.Serialize<TimeZoneEnvelope, LuminPackUtf8LengthOptions>(container);
                    TimeZoneEnvelope result = null;
                    LuminPackSerializer.Deserialize<TimeZoneEnvelope, LuminPackUtf8LengthOptions>(nested.AsSpan(), ref result);
                    if (result.Tail != 137 || result.Zone.ToSerializedString() != zone.ToSerializedString())
                        throw new Exception("Nested UTF8 snapshot mismatch");
                    string json = LuminPackSerializer.SerializeJson<TimeZoneInfo, LuminPackUtf8LengthOptions>(zone);
                    copy = null;
                    LuminPackSerializer.DeserializeJson<TimeZoneInfo, LuminPackUtf8LengthOptions>(json, ref copy);
                    if (copy.ToSerializedString() != zone.ToSerializedString()) throw new Exception("JSON snapshot mismatch");
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview,
            preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER", "NET11_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("TimeZoneCache" + mode,
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse)], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                [new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            var methods = driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
                .SelectMany(result => result.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()).ToArray();
            var writer = methods.Single(method => method.Identifier.ValueText == "Serialize" &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 1 &&
                method.ParameterList.Parameters[0].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            Require(writer.Body!.ToString().Contains("writer.WriteTimeZoneText<TLuminProfile>", StringComparison.Ordinal) &&
                writer.Body.ToString().Contains("capacityReserved: true", StringComparison.Ordinal) &&
                writer.Body.ToString().Contains("default(TLuminProfile).StringRecording", StringComparison.Ordinal),
                "The exact snapshot entry lost its common framed core or compiled-profile guard in " + mode);
            var reader = methods.Single(method => method.Identifier.ValueText == "Deserialize" &&
                method.TypeParameterList?.Parameters.Count == 1 && method.ParameterList.Parameters.Count == 2 &&
                method.ParameterList.Parameters[0].Type!.ToString().StartsWith("ReadOnlySpan", StringComparison.Ordinal) &&
                method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            var readBody = reader.Body!;
            var sharedRead = readBody.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(call =>
                call.Expression is SimpleNameSyntax name && name.Identifier.ValueText.StartsWith("LuminFacade", StringComparison.Ordinal));
            if (sharedRead?.Expression is SimpleNameSyntax sharedName)
                readBody = methods.Single(method => method.Identifier.ValueText == sharedName.Identifier.ValueText).Body!;
            Require(!readBody.ToString().Contains("Lease", StringComparison.Ordinal) &&
                readBody.ToString().Contains("new LuminPackReader(ref buffer)", StringComparison.Ordinal),
                "A stateless snapshot read acquired reference-tracking state in " + mode);
            var root = methods.Single(method => method.Identifier.ValueText == "RootRead" &&
                method.ParameterList.Parameters.Count == 2 && method.ParameterList.Parameters[1].Type!.ToString().TrimEnd('?') == "global::System.TimeZoneInfo");
            Require(root.Body!.ToString().Contains("GetTimeZoneCandidate<TLuminProfile>", StringComparison.Ordinal) &&
                root.Body.ToString().Contains("TryReadTimeZoneInfo<TLuminProfile>", StringComparison.Ordinal) &&
                root.Body.ToString().Contains("LuminPackTimeZoneInfoCache.Parse", StringComparison.Ordinal),
                "A snapshot read lost its complete-content cache or original parse fallback in " + mode);
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("TimeZoneCases")!.GetMethod("Check")!.Invoke(null, null);
        }
        VerifyAsyncPinScope(references);
        VerifyReadCases(references);
        Console.WriteLine("Passed time-zone concrete entries, compiled profiles, nested contracts and all four generation tiers.");
    }

    /// <summary>Keeps compatibility-runtime pins inside synchronous scopes of newer async consumers.</summary>
    /// <param name="references">The runtime and platform compilation references.</param>
    private static void VerifyAsyncPinScope(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using LuminPack.Core;
            public static class AsyncPinCases
            {
                public static async ValueTask<int> ReadAsync(ReadOnlyMemory<byte> memory)
                {
                    await Task.Yield();
                    var buffer = memory.Span;
                    var reader = new LuminPackReader(ref buffer);
                    reader.Advance(0);
                    return reader.Position;
                }
                public static async ValueTask<int> ReadLocalAsync(ReadOnlyMemory<byte> memory)
                {
                    await Task.Yield();
                    static int Read(ReadOnlyMemory<byte> input)
                    {
                        var buffer = input.Span;
                        var reader = new LuminPackReader(ref buffer);
                        return reader.Position;
                    }
                    return Read(memory);
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp13).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        var root = CSharpSyntaxTree.ParseText(source, parse).GetRoot();
        var projection = typeof(LuminPackSourceGenerator).Assembly.GetType("LuminPack.Code.Core.PinnedReaderProjection", throwOnError: true)!;
        var rewriter = (CSharpSyntaxRewriter)Activator.CreateInstance(projection, nonPublic: true)!;
        var rewritten = rewriter.Visit(root)!;
        Require(rewritten.DescendantNodes().OfType<MethodDeclarationSyntax>().All(method => !method.Modifiers.Any(SyntaxKind.UnsafeKeyword)) &&
            rewritten.DescendantNodes().OfType<LocalFunctionStatementSyntax>().All(method => !method.Modifiers.Any(SyntaxKind.UnsafeKeyword)) &&
            rewritten.DescendantNodes().OfType<TryStatementSyntax>().Count(scope => scope.Finally?.ToString()
                .Contains("LuminBufferWriterPool.Return", StringComparison.Ordinal) == true) == 2,
            "The portable input lifetime escaped its synchronous scope or lost pooled cleanup.");
        var compilation = CSharpCompilation.Create("AsyncPinScopes", [CSharpSyntaxTree.Create((CSharpSyntaxNode)rewritten, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var image = new MemoryStream();
        var emitted = compilation.Emit(image);
        Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
    }

    /// <summary>Reports the exact generated or runtime contract that failed.</summary>
    /// <param name="condition">The required invariant.</param>
    /// <param name="message">The failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    /// <summary>Runs complete-content, failure-state and nested comparisons in every generation mode.</summary>
    /// <param name="references">The actual Runtime and platform references.</param>
    private static void VerifyReadCases(MetadataReference[] references)
    {
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
            var input = CSharpCompilation.Create("ZoneRead" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(ReadCasesSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            GeneratorDriver driver = CSharpGeneratorDriver.Create([new LuminPackSourceGenerator().AsSourceGenerator(), new LuminPackAggregateGenerator().AsSourceGenerator()], parseOptions: parse);
            driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(output.SyntaxTrees.Skip(1).Any(tree => tree.ToString().Contains("reader.ReadTimeZoneInfo<", StringComparison.Ordinal)), "The new reader was not selected.");
            using var image = new MemoryStream(); var result = output.Emit(image);
            Require(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("ZoneReadCases")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed time-zone binary/JSON cache collisions, boundaries, failure state and nested profiles in four modes.");
    }

    private const string ReadCasesSource = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Text;
        using System.Text.Json;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Code;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        [LuminPackable] public sealed class ZoneReadEnvelope { public TimeZoneInfo Zone; public int Tail; }
        public readonly struct StrictUtf8 : ILuminPackOptions
        {
            public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF8;
            public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
            public System.Buffers.StandardFormat StandardFormat => new('G');
            public bool ValidateJsonSyntax => true;
            public int MaxJsonDepth => 64;
        }
        public readonly struct StrictUtf16 : ILuminPackOptions
        {
            public LuminPackStringEncoding StringEncoding => LuminPackStringEncoding.UTF16;
            public LuminPackStringRecording StringRecording => LuminPackStringRecording.Length;
            public System.Buffers.StandardFormat StandardFormat => new('G');
            public bool ValidateJsonSyntax => true;
            public int MaxJsonDepth => 64;
        }
        public static class ZoneReadCases
        {
            private static readonly TimeZoneInfo Sentinel=TimeZoneInfo.CreateCustomTimeZone("sentinel",TimeSpan.Zero,"Sentinel","S");
            public static void Check()
            {
                Profile<LuminPackUtf8LengthOptions>();Profile<LuminPackUtf16LengthOptions>();
                Profile<LuminPackUtf8TokenOptions>();Profile<LuminPackUtf16TokenOptions>();
                Profile<LuminPackUtf8VarIntOptions>();Profile<LuminPackUtf16VarIntOptions>();
                Profile<StrictUtf8>();Profile<StrictUtf16>();
            }
            private static TimeZoneInfo Boundary(int length)
            {
                var zone=TimeZoneInfo.CreateCustomTimeZone("Edge",TimeSpan.Zero,"x","S");
                zone=TimeZoneInfo.CreateCustomTimeZone("Edge",TimeSpan.Zero,new string('x',length-zone.ToSerializedString().Length+1),"S");
                if(zone.ToSerializedString().Length!=length)throw new Exception("Boundary fixture length changed.");
                return zone;
            }
            private static void Profile<T>() where T:struct,ILuminPackOptions
            {
                var zones=new List<TimeZoneInfo>{TimeZoneInfo.Utc,Boundary(256),Boundary(257),
                    TimeZoneInfo.CreateCustomTimeZone("Unicode",TimeSpan.FromMinutes(345),new string('\u96EA',100),"\u00E9"),
                    TimeZoneInfo.CreateCustomTimeZone("Escaped",TimeSpan.Zero,"quote\"slash\\semicolon;[bracket]","S")};
                for(int i=0;i<6;i++)zones.Add(TimeZoneInfo.CreateCustomTimeZone("Collision"+i,TimeSpan.Zero,"Display"+i,"Standard"+i));
                var rules=new[]{TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2000,1,1),new DateTime(2099,12,31),TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1,1,1,2,0,0),3,1),TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1,1,1,2,0,0),11,1))};
                zones.Add(TimeZoneInfo.CreateCustomTimeZone("Rules",TimeSpan.FromHours(2),new string('r',400),"Standard","Daylight",rules));
                using var output=new LuminBufferWriter(true);
                foreach(var zone in zones)
                {
                    string text=zone.ToSerializedString();LuminPackTimeZoneInfoCache.Parse(text);
                    output.ResetWrittenCount();LuminPackSerializer.Serialize<TimeZoneInfo, T>(zone,output);byte[] binary=output.GetSpan().ToArray();
                    output.ResetWrittenCount();LuminPackSerializer.SerializeJson<TimeZoneInfo, T>(zone,output);byte[] json=output.GetSpan().ToArray();
                    Compare<T>(binary,false);Compare<T>(json,true);Compare<T>(JsonBytes<T>(JsonSerializer.Serialize(text)),true);
                    Compare<T>(JsonBytes<T>("\""+string.Concat(text.Select(c=>"\\u"+((int)c).ToString("x4")))+"\""),true);
                    foreach(var other in zones.Skip(5).Take(3))LuminPackTimeZoneInfoCache.Parse(other.ToSerializedString());
                    Compare<T>(binary,false);Compare<T>(json,true);
                    var graph=new ZoneReadEnvelope{Zone=zone,Tail=137};
                    output.ResetWrittenCount();LuminPackSerializer.Serialize<ZoneReadEnvelope, T>(graph,output);ZoneReadEnvelope decoded=null;
                    LuminPackSerializer.Deserialize<ZoneReadEnvelope, T>(output.GetSpan(),ref decoded);
                    if(decoded.Tail!=137||decoded.Zone.ToSerializedString()!=text)throw new Exception("Nested binary snapshot changed.");
                    output.ResetWrittenCount();LuminPackSerializer.SerializeJson<ZoneReadEnvelope, T>(graph,output);decoded=null;
                    LuminPackSerializer.DeserializeJson<ZoneReadEnvelope, T>(output.GetSpan(),ref decoded);
                    if(decoded.Tail!=137||decoded.Zone.ToSerializedString()!=text)throw new Exception("Nested JSON snapshot changed.");
                    if(zone==TimeZoneInfo.Utc||zone.Id=="Unicode")
                    {
                        for(int n=0;n<binary.Length;n++)Compare<T>(binary.Take(n).ToArray(),false);
                        for(int n=0;n<json.Length;n++)Compare<T>(json.Take(n).ToArray(),true);
                    }
                }
                foreach(string text in new[]{"", "invalid", "UTC;invalid;", "\uD800", "\uDC00", "x\0y"})
                {
                    if(default(T).StringEncoding!=LuminPackStringEncoding.UTF8||!text.Any(char.IsSurrogate))
                    {
                        output.ResetWrittenCount();var writer=new LuminPackWriter(output);writer.WriteObjectHeader(0);writer.Write<T>(text);
                        Compare<T>(writer.GetSpan().ToArray(),false);
                    }
                    Compare<T>(JsonBytes<T>(JsonSerializer.Serialize(text)),true);
                }
                TimeZoneInfo absent=null;output.ResetWrittenCount();LuminPackSerializer.Serialize<TimeZoneInfo, T>(absent,output);Compare<T>(output.GetSpan().ToArray(),false);
                foreach(string text in new[]{"null","\"\"","true","123","{}","[]","\"invalid\"","\"\\x\"","\"\\uD800\"","\"a\nb\""})Compare<T>(JsonBytes<T>(text),true);
                if(default(T).StringEncoding==LuminPackStringEncoding.UTF8)
                {
                    Compare<T>(new byte[]{34,0xC0,0xAF,34},true);
                    output.ResetWrittenCount();var writer=new LuminPackWriter(output);writer.WriteObjectHeader(0);writer.Write<T>("bad");
                    byte[] malformed=writer.GetSpan().ToArray();int payload=malformed.AsSpan().IndexOf("bad"u8);
                    if(payload<0)throw new Exception("The malformed UTF-8 fixture payload was not found.");
                    foreach(byte[] invalid in new[]{new byte[]{0xC0,0xAF,32},new byte[]{0xED,0xA0,0x80},new byte[]{0xE2,0x82,0xFF}})
                    {
                        invalid.CopyTo(malformed,payload);Compare<T>(malformed,false);
                    }
                }
                else Compare<T>(new byte[]{34,0,65},true);
                if(default(T).StringRecording==LuminPackStringRecording.VarInt)
                {
                    foreach(byte[] data in new[]{new byte[]{0,0x80},new byte[]{0,0x80,0},new byte[]{0,0xff,0xff,0xff,0xff,0x7f},new byte[]{0,1,0}})Compare<T>(data,false);
                }
            }
            private static byte[] JsonBytes<T>(string text) where T:struct,ILuminPackOptions
                => (default(T).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(text);
            private static void Compare<T>(byte[] input,bool json) where T:struct,ILuminPackOptions
            {
                foreach(bool selected in json?new[]{false,true}:new[]{false})
                {
                    string expected=State<T>(input,json,selected,false),actual=State<T>(input,json,selected,true);
                    if(expected!=actual)throw new Exception("Time-zone state mismatch: "+typeof(T).Name+" / "+json+" / "+selected+" / "+Convert.ToBase64String(input)+" / expected "+expected+" / actual "+actual);
                }
            }
            private static unsafe string State<T>(byte[] input,bool json,bool selected,bool generated) where T:struct,ILuminPackOptions
            {
                fixed(byte* address=input)
                {
                    ReadOnlySpan<byte> span=new ReadOnlySpan<byte>(address,input.Length);TimeZoneInfo value=Sentinel;string failure="success";
                    if(json)
                    {
                        using var options=new LuminPackReaderOptionalState();
                        var reader=new LuminPackJsonReader(ref span,options);
                        try
                        {
                            if(selected)reader.Read<T>();
                            if(generated)LuminPackSerializer.DeserializeJson<TimeZoneInfo, T>(ref reader,ref value);
                            else if(reader.IsNull())value=null;
                            else value=LuminPackTimeZoneInfoCache.Parse(reader.Read<string,T>());
                        }
                        catch(Exception error){failure=Failure(error);}
                        return failure+":"+reader.Position+":"+reader.Depth+":"+reader.CurrentTokenType+":"+Result(value);
                    }
                    else
                    {
                        var reader=new LuminPackReader(ref span);
                        try
                        {
                            if(generated)LuminPackSerializer.Deserialize<TimeZoneInfo, T>(ref reader,ref value);
                            else if(!reader.TryReadObjectHead())value=null;
                            else
                            {
                                var candidate=default(T).StringRecording==LuminPackStringRecording.Length?reader.GetTimeZoneCandidate<T>():null;
                                if(reader.TryReadTimeZoneInfo<T>(candidate,out var cached))value=cached;
                                else{string text=reader.Read<T>(candidate?.Text);value=string.IsNullOrEmpty(text)?null:LuminPackTimeZoneInfoCache.Parse(text,candidate);}
                            }
                        }
                        catch(Exception error){failure=Failure(error);}
                        return failure+":"+reader.Position+":"+Result(value);
                    }
                }
            }
            private static string Failure(Exception value)=>value.GetType().FullName+":"+(value as ArgumentException)?.ParamName+":"+value.Message;
            private static string Result(TimeZoneInfo value)=>value==null?"null":ReferenceEquals(value,Sentinel)?"unchanged":value.ToSerializedString();
        }
        """;
}
