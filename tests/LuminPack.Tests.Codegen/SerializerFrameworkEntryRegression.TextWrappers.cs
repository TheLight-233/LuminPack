using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerFrameworkEntryRegression
{
    /// <summary>Checks compact names together with text identifiers and lazy payloads in both formats.</summary>
    /// <param name="references">The actual target metadata references.</param>
    internal static void RunTextWrappers(MetadataReference[] references)
    {
        VerifyTypeNamePattern();
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("TextWrappers" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(TextWrapperSource.Replace("MODE", mode, StringComparison.Ordinal), parse, "TextWrappers.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            var driver = Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Execute(output);
            if (mode == "Minimal") VerifyTypeNameCapability(compilation, references, parse, driver);
        }
        Console.WriteLine("Passed type-name wire forms and allocations, culture/URI/lazy graph round trips, failure publication and runtime capability edits in four tiers.");
    }

    /// <summary>Replaces the optional writer capability and compares retained generation and execution with a fresh driver.</summary>
    /// <param name="compilation">The complete consumer.</param>
    /// <param name="references">The original references.</param>
    /// <param name="parse">The target parser configuration.</param>
    /// <param name="driver">The populated driver.</param>
    private static void VerifyTypeNameCapability(CSharpCompilation compilation, MetadataReference[] references, CSharpParseOptions parse, GeneratorDriver driver)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter")!.ContainingAssembly;
        var original = references.OfType<PortableExecutableReference>().Single(reference => SymbolEqualityComparer.Default.Equals(compilation.GetAssemblyOrModuleSymbol(reference), owner));
        var changed = compilation.ReplaceReference(original, MetadataReference.CreateFromImage(FrameworkRuntimeMetadata.WithoutMethods(original.FilePath!, "WriteTypeName", "ResolveCulture")));
        string? expected = null;
        foreach (var current in new[] { compilation, changed, compilation })
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(current, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            Require(Trees(driver).Any(text => text.Contains("writer.WriteTypeName", StringComparison.Ordinal)) == ReferenceEquals(current, compilation), "Stale type-name writer capability.");
            Require(Trees(driver).Any(text => text.Contains("LuminPackCultureInfoCache.ResolveCulture", StringComparison.Ordinal)) == ReferenceEquals(current, compilation), "Stale culture cache capability.");
            Require(Trees(driver).SequenceEqual(Trees(Create(parse).RunGenerators(current))), "Runtime metadata edits retained stale type-name operations.");
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            string result = (string)Assembly.Load(image.ToArray()).GetType("FrameworkEntry")!.GetMethod("Evidence")!.Invoke(null, null)!;
            expected ??= result;
            if (result != expected)
                throw new InvalidOperationException("Text wrapper capability changed wire, exception or cursor observations: " +
                    expected.Split('\n').Zip(result.Split('\n')).First(lines => lines.First != lines.Second));
        }
    }

    private const string TextWrapperSource = """
        using System;
        using System.Linq;
        using System.Reflection;
        using System.Globalization;
        using System.Collections.Generic;
        using System.Buffers;
        using System.Text;
        using System.Text.RegularExpressions;
        using LuminPack;
        using LuminPack.Attribute;
        using LuminPack.Core;
        using LuminPack.Option;
        using LuminPack.Utility;
        [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
        public readonly struct TextStrict8:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF8;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public readonly struct TextStrict16:ILuminPackOptions {public LuminPackStringEncoding StringEncoding=>LuminPackStringEncoding.UTF16;public LuminPackStringRecording StringRecording=>LuminPackStringRecording.Length;public StandardFormat StandardFormat=>new('G');public int MaxJsonDepth=>1024;public bool ValidateJsonSyntax=>true;}
        public sealed class NameType:TypeDelegator
        {
            readonly string name;public static int Gets;public NameType(string name):base(typeof(int)){this.name=name;}
            public override string AssemblyQualifiedName {get{Gets++;return name;}}
        }
        [LuminPackable] public sealed class TextEnvelope
        {
            public Type First;public Type Second;public Type[] Types;public CultureInfo Culture;public Uri Uri;
            public Lazy<int> Number;public Lazy<string> Text;public Lazy<Type> Nested;public int Tail;
        }
        public static class FrameworkEntry
        {
            static readonly Regex Qualifiers=new(@", Version=\d+.\d+.\d+.\d+, Culture=[\w-]+, PublicKeyToken=(?:null|[a-f0-9]{16})");
            public static void Check()
            {
                Profile<LuminPackUtf8LengthOptions>();Profile<LuminPackUtf16LengthOptions>();Profile<LuminPackUtf8TokenOptions>();Profile<LuminPackUtf16TokenOptions>();
                Profile<LuminPackUtf8VarIntOptions>();Profile<LuminPackUtf16VarIntOptions>();Profile<TextStrict8>();Profile<TextStrict16>();
                foreach (string name in new[] { "en-US", "fr-FR", "en-us", "", "zh-Hans" })
                {
                    CultureInfo current=CultureInfo.GetCultureInfo(name);
                    byte[] bytes=LuminPackSerializer.Serialize(current);
                    for(int round=0;round<4;round++)
                    {
                        if(round==2) CultureInfo.InvariantCulture.ClearCachedData();
                        LuminPackSerializer.Deserialize(bytes,ref current);
                        Require(ReferenceEquals(current,CultureInfo.GetCultureInfo(current.Name)),"Stale binary culture after cache invalidation");
                        LuminPackSerializer.DeserializeJson("\""+name+"\"",ref current);
                        Require(ReferenceEquals(current,CultureInfo.GetCultureInfo(name)),"Stale JSON culture after cache invalidation");
                    }
                }
            }
            static void Profile<P>()where P:struct,ILuminPackOptions
            {
                using var output=new LuminBufferWriter(true);using var reference=new LuminBufferWriter(true);
                var names=new List<string>{typeof(int).AssemblyQualifiedName,typeof(Dictionary<string,List<int[]>>).AssemblyQualifiedName,"Plain","A\"B, Version=1a2b3c4, Culture=neutral, PublicKeyToken=null",", Version=1234567, Culture=-, PublicKeyToken=null", "\u4E2D, Version=\u0661.\u0662.\u0663.\u0664, Culture=\u0301, PublicKeyToken=0123456789abcdef",new string('x',600)+", Version=1.2.3.4, Culture=neutral, PublicKeyToken=null"};
                foreach(string name in names)
                {
                    Type item=new NameType(name);string compact=Qualifiers.Replace(name,"");
                    reference.ResetCore();var binary=new LuminPackWriter(reference);binary.WriteObjectHeader(0);binary.Write<P>(compact);byte[] expected=binary.GetSpan().ToArray();
                    output.ResetCore();NameType.Gets=0;LuminPackSerializer.Serialize<Type, P>(item,output);Require(NameType.Gets==1&&output.GetSpan().SequenceEqual(expected),"Binary type-name wire/getter");
                    reference.ResetCore();var json=new LuminPackJsonWriter(reference);json.Write<P>(compact);expected=json.GetSpan().ToArray();
                    output.ResetCore();NameType.Gets=0;LuminPackSerializer.SerializeJson<Type, P>(item,output);Require(NameType.Gets==1&&output.GetSpan().SequenceEqual(expected),"JSON type-name wire/getter");
                    for(int i=0;i<64;i++){output.ResetCore();LuminPackSerializer.Serialize<Type, P>(item,output);output.ResetCore();LuminPackSerializer.SerializeJson<Type, P>(item,output);}
                    long start=GC.GetAllocatedBytesForCurrentThread();
                    for(int i=0;i<32;i++){output.ResetCore();LuminPackSerializer.Serialize<Type, P>(item,output);output.ResetCore();LuminPackSerializer.SerializeJson<Type, P>(item,output);}
                    Require(GC.GetAllocatedBytesForCurrentThread()==start,"Type writes allocated temporary heap storage");
                }
                foreach(var culture in new[]{CultureInfo.InvariantCulture,CultureInfo.GetCultureInfo("en-US"),new CultureInfo("fr-FR")})
                foreach(string uri in new[]{"relative/path?x=1","https://example.com/a%20b?x=1#t","mailto:a@example.com"})
                {
                    var value=new TextEnvelope{First=typeof(int),Second=typeof(List<>),Types=new[]{typeof(Dictionary<string,int[]>),typeof(int[,])},Culture=culture,Uri=new Uri(uri,UriKind.RelativeOrAbsolute),Number=new Lazy<int>(17),Text=new Lazy<string>("value"),Nested=new Lazy<Type>(typeof(string)),Tail=731};
                    output.ResetCore();LuminPackSerializer.Serialize<TextEnvelope, P>(value,output);byte[] bytes=output.GetSpan().ToArray();TextEnvelope result=null;
                    LuminPackSerializer.Deserialize<TextEnvelope, P>(bytes,ref result);Verify(result,value);LuminPackSerializer.Deserialize<TextEnvelope, P>(bytes,ref result);Verify(result,value);
                    output.ResetCore();LuminPackSerializer.SerializeJson<TextEnvelope, P>(value,output);bytes=output.GetSpan().ToArray();result=null;
                    LuminPackSerializer.DeserializeJson<TextEnvelope, P>(bytes,ref result);Verify(result,value);LuminPackSerializer.DeserializeJson<TextEnvelope, P>(bytes,ref result);Verify(result,value);
                }
            }
            static void Verify(TextEnvelope value,TextEnvelope original)
            {
                Require(value.First==original.First&&value.Second==original.Second&&value.Types.SequenceEqual(original.Types)&&value.Culture.Name==original.Culture.Name&&value.Uri.OriginalString==original.Uri.OriginalString&&value.Number.Value==17&&value.Text.Value=="value"&&value.Nested.Value==typeof(string)&&value.Tail==731,"Text wrapper graph");
            }
            public static string Evidence()
            {
                var result=new StringBuilder();Evidence<LuminPackUtf8LengthOptions>(result);Evidence<LuminPackUtf16LengthOptions>(result);Evidence<LuminPackUtf8TokenOptions>(result);Evidence<LuminPackUtf16TokenOptions>(result);Evidence<LuminPackUtf8VarIntOptions>(result);Evidence<LuminPackUtf16VarIntOptions>(result);Evidence<TextStrict8>(result);Evidence<TextStrict16>(result);return result.ToString();
            }
            static void Evidence<P>(StringBuilder result)where P:struct,ILuminPackOptions
            {
                foreach(string name in new[]{null,"",typeof(int).AssemblyQualifiedName,"A\0B, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null","\uD800, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null","\u00E9\u4E2D, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null",new string('x',600)+", Version=1.2.3.4, Culture=neutral, PublicKeyToken=null"})
                foreach(int width in new[]{0,1,2,4,8,16,32,64,128,1024,4096})foreach(bool json in new[]{false,true})result.AppendLine(Observe<P>(new NameType(name),width,json));
                ReadEvidence<P>(result);
                foreach(bool json in new[]{false,true})
                {
                    Lazy<int> lazy=new(()=>throw new FormatException("Factory failure"));using var output=new LuminBufferWriter(true);string error="none";
                    if(json){var writer=new LuminPackJsonWriter(output);try{LuminPackSerializer.SerializeJson<Lazy<int>, P>(ref writer,in lazy);}catch(Exception e){error=e.GetType().Name;}result.AppendLine("factory:"+json+":"+error+":"+writer.Position+":"+Convert.ToBase64String(writer.GetSpan()));}
                    else{var writer=new LuminPackWriter(output);try{LuminPackSerializer.Serialize<Lazy<int>, P>(ref writer,in lazy);}catch(Exception e){error=e.GetType().Name;}result.AppendLine("factory:"+json+":"+error+":"+writer.Position+":"+Convert.ToBase64String(writer.GetSpan()));}
                }
            }
            static void ReadEvidence<P>(StringBuilder result)where P:struct,ILuminPackOptions
            {
                using var output=new LuminBufferWriter(true);
                Type type=typeof(int);CultureInfo culture=CultureInfo.GetCultureInfo("en-US");Uri uri=new Uri("relative/path",UriKind.Relative);Lazy<int> lazy=new(17);
                for(int kind=0;kind<4;kind++)
                {
                    output.ResetCore();
                    if(kind==0)LuminPackSerializer.Serialize<Type, P>(type,output);else if(kind==1)LuminPackSerializer.Serialize<CultureInfo, P>(culture,output);else if(kind==2)LuminPackSerializer.Serialize<Uri, P>(uri,output);else LuminPackSerializer.Serialize<Lazy<int>, P>(lazy,output);
                    byte[] bytes=output.GetSpan().ToArray();
                    for(int length=0;length<=bytes.Length;length++)ReadOne<P>(result,kind,bytes.AsSpan(0,length).ToArray(),false);
                    string[] texts=kind switch
                    {
                        0=>new[]{"null","\"\"","\"System.Int32, System.Private.CoreLib\"","\"missing type\"","{}","1"},
                        1=>new[]{"null","\"\"","\"en-US\"","\"not a culture name!\"","{}","1"},
                        2=>new[]{"null","\"\"","\"relative/path\"","\"http://[\"","{}","1"},
                        _=>new[]{"null","{}","{\"Value\":1}","{\"extra\":true,\"Value\":2,\"Value\":3}","{\"Value\":\"x\"}","{\"Value\":}","{\"Value\":1"}
                    };
                    foreach(string text in texts)ReadOne<P>(result,kind,(default(P).StringEncoding==LuminPackStringEncoding.UTF8?Encoding.UTF8:Encoding.Unicode).GetBytes(text),true);
                }
            }
            static void ReadOne<P>(StringBuilder result,int kind,byte[] bytes,bool json)where P:struct,ILuminPackOptions
            {
                Type type=typeof(string);CultureInfo culture=CultureInfo.GetCultureInfo("fr-FR");Uri uri=new Uri("original",UriKind.Relative);Lazy<int> lazy=new(91);object original=kind==0?(object)type:kind==1?culture:kind==2?uri:lazy;string error="none";int consumed=-1;
                try
                {
                    if(kind==0)consumed=json?LuminPackSerializer.DeserializeJson<Type, P>(bytes,ref type):LuminPackSerializer.Deserialize<Type, P>(bytes,ref type);
                    else if(kind==1)consumed=json?LuminPackSerializer.DeserializeJson<CultureInfo, P>(bytes,ref culture):LuminPackSerializer.Deserialize<CultureInfo, P>(bytes,ref culture);
                    else if(kind==2)consumed=json?LuminPackSerializer.DeserializeJson<Uri, P>(bytes,ref uri):LuminPackSerializer.Deserialize<Uri, P>(bytes,ref uri);
                    else consumed=json?LuminPackSerializer.DeserializeJson<Lazy<int>, P>(bytes,ref lazy):LuminPackSerializer.Deserialize<Lazy<int>, P>(bytes,ref lazy);
                }
                catch(Exception e){error=e.GetType().Name;}
                object current=kind==0?(object)type:kind==1?culture:kind==2?uri:lazy;string state=kind==0?type?.AssemblyQualifiedName:kind==1?culture?.Name:kind==2?uri?.OriginalString:lazy?.Value.ToString();
                result.AppendLine("read:"+typeof(P).Name+":"+kind+":"+json+":"+Convert.ToBase64String(bytes)+":"+error+":"+consumed+":"+ReferenceEquals(original,current)+":"+state);
            }
            static unsafe string Observe<P>(Type value,int width,bool json)where P:struct,ILuminPackOptions
            {
                byte[] bytes=Enumerable.Repeat((byte)0xCC,width+16).ToArray();string error="none";int cursor;NameType.Gets=0;
                fixed(byte* pointer=bytes)
                {
                    Span<byte> span=new(pointer,width);
                    if(json){var writer=new LuminPackJsonWriter(ref span,new());try{LuminPackSerializer.SerializeJson<Type, P>(ref writer,in value);}catch(Exception e){error=e.GetType().Name;}cursor=writer.Position;}
                    else{var writer=new LuminPackWriter(ref span);try{LuminPackSerializer.Serialize<Type, P>(ref writer,in value);}catch(Exception e){error=e.GetType().Name;}cursor=writer.Position;}
                }
                return typeof(P).Name+":"+json+":"+width+":"+error+":"+cursor+":"+NameType.Gets+":"+Convert.ToBase64String(bytes,0,cursor)+":"+Convert.ToBase64String(bytes,width,16);
            }
            static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        }
        """;
}
