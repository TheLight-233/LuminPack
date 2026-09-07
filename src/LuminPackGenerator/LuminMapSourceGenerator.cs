using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class LuminMapSourceGenerator : IIncrementalGenerator
{
    private const string LuminMapToAttr  = "LuminPack.Attribute.LuminMapToAttribute";
    private const string LuminMapperAttr = "LuminPack.Attribute.LuminMapperAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var metaProvider = context.ParseOptionsProvider
            .Combine(context.CompilationProvider)
            .Select((pair, _) =>
            {
                var (opts, compilation) = pair;
                var cs   = (CSharpParseOptions)opts;
                var net8 = cs.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER");
                var net9_OR_GREATER = cs.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER");
                var allowUnsafe = compilation.Options is CSharpCompilationOptions csharpOptions
                    ? csharpOptions.AllowUnsafe
                    : false;
                var registerMode = LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation);
                return new MetaInfo(cs, cs.LanguageVersion, net8, net9_OR_GREATER, allowUnsafe, registerMode);
            });

        var generationMode = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LuminPackGenerationMode", out string? value);
                return LuminPackGenerationTierResolver.FromProperty(value);
            });

        var autoMapDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (ctx, _) =>
                {
                    var symbol = GetTypeWithAttribute(ctx, LuminMapToAttr);
                    return symbol == null
                        ? null
                        : LuminAutoMapAnalyzer.Analyze(symbol, ctx.SemanticModel.Compilation);
                })
            .Where(static x => x != null)!;

        context.RegisterSourceOutput(
            autoMapDeclarations.Combine(metaProvider),
            static (spc, pair) =>
            {
                var (info, meta) = pair;
                if (info == null || info.Pairs.Count == 0) return;
                try
                {
                    var code = LuminAutoMapCodeGenerator.GenerateAutoMapperClass(info, meta);
                    if (string.IsNullOrEmpty(code)) return;
                    spc.AddSource($"{SafeFileName(info.SourceFullName)}Mapper.g.cs", code);
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GeneratorFailure,
                        Location.None,
                        ex.GetType().FullName + ": " + ex.Message + " | " +
                        (ex.StackTrace ?? string.Empty).Replace("\r", " ").Replace("\n", " ")));
                }
            });

        var manualMapDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) =>
                    node is ClassDeclarationSyntax cls
                    && cls.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))
                    && cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)),
                static (ctx, _) =>
                {
                    var symbol = GetTypeWithAttribute(ctx, LuminMapperAttr);
                    return symbol == null
                        ? null
                        : LuminMapAnalyzer.Analyze(symbol, ctx.SemanticModel.Compilation);
                })
            .Where(static x => x != null)!;

        context.RegisterSourceOutput(
            manualMapDeclarations.Combine(metaProvider),
            static (spc, pair) =>
            {
                var (info, meta) = pair;
                if (info.Methods.Count == 0) return;
                try
                {
                    var code = LuminMapCodeGenerator.GenerateMapperImpl(info, meta);
                    if (string.IsNullOrEmpty(code)) return;
                    spc.AddSource($"{SafeFileName(info.ClassFullName)}.Mapper.g.cs", code);
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GeneratorFailure,
                        Location.None,
                        ex.ToString()));
                }
            });

        var allAuto   = autoMapDeclarations.Collect();
        var allManual = manualMapDeclarations.Collect();

        context.RegisterSourceOutput(
            allAuto.Combine(allManual)
                   .Combine(context.CompilationProvider)
                   .Combine(metaProvider)
                   .Combine(generationMode),
            static (spc, pair) =>
            {
                var ((((autoInfos, manualInfos), compilation), meta), mode) = pair;

                // Unity auto-references managed plug-ins from every compilation. Merely
                // finding LuminPack in metadata therefore is not evidence that the target
                // assembly uses it; emitting support there pollutes dependent assemblies
                // with duplicate extension overloads.
                if (!CompilationTypeAnalysisCache.GetOrCreate(compilation).UsesLuminPack)
                    return;

                // 没有 [LuminPackable] 类型且没有任何 Mapper 声明，不生成
                bool hasMappers  = !autoInfos.IsEmpty || !manualInfos.IsEmpty;

                try
                {
                    // Validate [LuminPackable(GeneratorType.Custom)] types and emit the automatic
                    // LuminPackSerializer.Register registration module initializer.
                    var customModels = CustomFormatterAnalyzer.AnalyzeAll(compilation, spc.ReportDiagnostic);
                    bool hasValidCustom = customModels.Any(static m => !m.HasErrors && m.RegisterTier > 0);
                    if (hasValidCustom && (!meta.AllowUnsafe || meta.RegisterMode == LuminPackRegisterMode.Disabled))
                    {
                        foreach (var model in customModels.Where(static m => !m.HasErrors && m.RegisterTier > 0))
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.CustomRegistrationSkippedNoUnsafe,
                                model.Type.Locations.FirstOrDefault() ?? Location.None,
                                model.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        }
                    }
                    else if (hasValidCustom)
                    {
                        var customCode = CustomFormatterRegistrationGenerator.Generate(
                            customModels,
                            compilation,
                            meta,
                            TypeMetaChecker.IsUnityProject(compilation));
                        if (!string.IsNullOrEmpty(customCode))
                            spc.AddSource("LuminPackCustomFormatterRegistry.g.cs", customCode);
                    }

                    if (hasMappers)
                    {
                        var code = GenerateMappersRegistry(autoInfos!, manualInfos, compilation, meta);
                        if (!string.IsNullOrEmpty(code))
                            spc.AddSource("GeneratedMappersRegistry.g.cs", code);
                    }

                    LuminPackGenerationTier propertyTier = mode;
                    LuminPackGenerationTier assemblyTier = LuminPackGenerationTierResolver.FromAssembly(compilation);
                    LuminPackGenerationTier effectiveTier = (LuminPackGenerationTier)Math.Max((int)propertyTier, (int)assemblyTier);

                    ReachabilityAnalysis? reachability = effectiveTier != LuminPackGenerationTier.Full
                        ? ReachabilityAnalysisCache.GetOrCreate(compilation, effectiveTier)
                        : null;

                    var formatterSupport = LuminPackExtensionGenerator.GenerateSerializerInvocationSupport(compilation, meta, reachability);
                    if (!string.IsNullOrEmpty(formatterSupport))
                        spc.AddSource("LuminPack.SerializerInvocation.Extension.g.cs", formatterSupport);

                    var serializer = LuminPackSerializerGenerator.GenerateSerializerClass(compilation, meta, reachability);
                    if (!string.IsNullOrEmpty(serializer))
                        spc.AddSource("LuminPackSerializer.g.cs", serializer);
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GeneratorFailure,
                        Location.None,
                        ex.GetType().FullName + ": " + ex.Message + " | " +
                        (ex.StackTrace ?? string.Empty).Replace("\r", " ").Replace("\n", " ")));
                }
            });
    }

    private static string GenerateMappersRegistry(
        System.Collections.Immutable.ImmutableArray<AutoMapSourceInfo> autoInfos,
        System.Collections.Immutable.ImmutableArray<LuminMapperClassInfo> manualInfos,
        Compilation compilation,
        MetaInfo meta)
    {
        bool   isUnity  = TypeMetaChecker.IsUnityProject(compilation);
        string asmRaw   = compilation.AssemblyName ?? "Unknown";
        string asmSafe  = SanitizeId(asmRaw);

        string registryFqn = asmSafe == "Unknown"
            ? "global::GeneratedMappersRegistry"
            : $"global::LuminPackRegisters.{asmSafe}.GeneratedMappersRegistry";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminMapper registry — do not modify");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack;");
        sb.AppendLine("using global::LuminPack.Mapping;");
        sb.AppendLine();

        bool hasNs = asmSafe != "Unknown";
        if (hasNs) { sb.AppendLine($"namespace LuminPackRegisters.{asmSafe}"); sb.AppendLine("{"); }

        sb.AppendLine("    public static class GeneratedMappersRegistry");
        sb.AppendLine("    {");

        if (isUnity)
        {
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("        [global::UnityEditor.InitializeOnLoadMethod]");
            sb.AppendLine("#endif");
            sb.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
        }
        else
        {
            sb.AppendLine("#if NET5_0_OR_GREATER");
            sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializerAttribute]");
            sb.AppendLine("#else");
            sb.AppendLine("        // 需要手动调用初始化");
            sb.AppendLine($"        // 请在程序入口调用 {registryFqn}.Initialize()");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");

        foreach (var info in autoInfos)
        {
            if (info == null || info.Pairs.Count == 0) continue;
            sb.AppendLine($"            // Auto: {info.SourceFullName}");
            sb.Append(LuminAutoMapCodeGenerator.GenerateAutoRegistrationLines(info));
        }

        foreach (var info in manualInfos)
        {
            if (info.Methods.Count == 0) continue;
            sb.AppendLine($"            // Manual: {info.ClassFullName}");
            sb.Append(LuminMapCodeGenerator.GenerateManualRegistrationLines(info));
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("    }");
        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    private static INamedTypeSymbol? GetTypeWithAttribute(
        GeneratorSyntaxContext context,
        string metadataName)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        if (symbol == null)
            return null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + metadataName)
                return symbol;
        }

        return null;
    }

    private static string SafeFileName(string fqn)
        => fqn.Replace("global::", "").Replace(".", "_").Replace("<", "_").Replace(">", "_");

    private static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0) sb.Append(char.IsLetter(c) || c == '_' ? c : char.IsDigit(c) ? '_' : '_');
            else sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.ToString();
    }

}
