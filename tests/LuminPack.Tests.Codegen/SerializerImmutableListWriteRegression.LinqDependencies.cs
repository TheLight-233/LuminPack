using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static partial class SerializerImmutableListWriteRegression
{
    /// <summary>Retains implicit LINQ element sequences for roots and nested objects in every generation tier.</summary>
    /// <param name="references">The actual platform and runtime references.</param>
    internal static void RunLinqDependencies(MetadataReference[] references)
    {
        const string source = """
            using System;
            using System.Linq;
            using LuminPack;
            using LuminPack.Attribute;
            using LuminPack.Option;
            using LuminPack.Utility;
            [assembly:LuminPackGeneratorOptions(LuminPackGenerationMode.MODE)]
            [LuminPackable] public sealed class LinqEnvelope
            {
                public IGrouping<string,int> Group;
                public ILookup<string,int> Lookup;
                public int Tail;
            }
            public static class LinqDependencyEntry
            {
                public static void Check()
                {
                    Check<LuminPackUtf8LengthOptions>();Check<LuminPackUtf16LengthOptions>();
                    Check<LuminPackUtf8TokenOptions>();Check<LuminPackUtf16TokenOptions>();
                }
                private static void Check<P>()where P:struct,ILuminPackOptions
                {
                    using var output=new LuminBufferWriter(true);
                    int[] items={1,2,3};IGrouping<string,int> group=items.GroupBy(x=>"key").First();
                    ILookup<string,int> lookup=items.ToLookup(x=>"key");
                    foreach(bool json in new[]{false,true})
                    {
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<IGrouping<string, int>, P>(group,output);else LuminPackSerializer.Serialize<IGrouping<string, int>, P>(group,output);
                        IGrouping<string,int> result=null;
                        if(json)LuminPackSerializer.DeserializeJson<IGrouping<string, int>, P>(output.GetSpan(),ref result);else LuminPackSerializer.Deserialize<IGrouping<string, int>, P>(output.GetSpan(),ref result);
                        if(result.Key!="key"||!result.SequenceEqual(items))throw new Exception("Grouping dependencies changed.");
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<ILookup<string, int>, P>(lookup,output);else LuminPackSerializer.Serialize<ILookup<string, int>, P>(lookup,output);
                        ILookup<string,int> table=null;
                        if(json)LuminPackSerializer.DeserializeJson<ILookup<string, int>, P>(output.GetSpan(),ref table);else LuminPackSerializer.Deserialize<ILookup<string, int>, P>(output.GetSpan(),ref table);
                        if(table.Count!=1||!table["key"].SequenceEqual(items))throw new Exception("Lookup dependencies changed.");
                        var value=new LinqEnvelope{Group=group,Lookup=lookup,Tail=137};
                        output.ResetCore();if(json)LuminPackSerializer.SerializeJson<LinqEnvelope, P>(value,output);else LuminPackSerializer.Serialize<LinqEnvelope, P>(value,output);
                        LinqEnvelope envelope=null;
                        if(json)LuminPackSerializer.DeserializeJson<LinqEnvelope, P>(output.GetSpan(),ref envelope);else LuminPackSerializer.Deserialize<LinqEnvelope, P>(output.GetSpan(),ref envelope);
                        if(envelope.Tail!=137||!envelope.Group.SequenceEqual(items)||!envelope.Lookup["key"].SequenceEqual(items))throw new Exception("Nested LINQ dependencies changed.");
                    }
                }
            }
            """;
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: ["NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]).WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "LuminPack")]);
        foreach (string mode in new[] { "Full", "Medium", "Light", "Minimal" })
        {
            var compilation = CSharpCompilation.Create("LinqDependencies" + Guid.NewGuid().ToString("N"),
                [CSharpSyntaxTree.ParseText(source.Replace("MODE", mode, StringComparison.Ordinal), parse, "Entry.cs")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
            Create(parse).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Require(!diagnostics.Any(item => item.Severity == DiagnosticSeverity.Error), string.Join(Environment.NewLine, diagnostics));
            using var image = new MemoryStream();
            var emitted = output.Emit(image);
            Require(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(item => item.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(image.ToArray()).GetType("LinqDependencyEntry")!.GetMethod("Check")!.Invoke(null, null);
        }
        Console.WriteLine("Passed implicit LINQ formatter roots and object fields in four generation tiers and four profiles.");
    }
}
