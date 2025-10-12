using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
    const string LuminPackGlobalExtension = "LuminPack.GlobalExtension.631798131";
    
    public static ConditionalWeakTable<Compilation, HashSet<string>> AnalyzedTypes = new();
    
    public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
    {
        StringBuilder sb = new StringBuilder();
        
        var analyzedTypes = AnalyzedTypes.GetOrCreateValue(compilation);
        
        HashSet<string> currentGenerationTypes = new HashSet<string>();
        
        sb.AppendLine();

        if (analyzedTypes.Add(LuminPackGlobalExtension))
        {
            GeneratorGlobalMethod(sb);
        }
        
        foreach (var localField in data.localFields)
        {
            string fieldType = localField.TypeName;
            if (data.GenericParameters.Contains(fieldType))
                continue;
            
            if (!analyzedTypes.Contains(fieldType) && !currentGenerationTypes.Contains(fieldType))
            {
                currentGenerationTypes.Add(fieldType);
                analyzedTypes.Add(fieldType);
                
                // 生成Writer扩展方法
                sb.AppendLine($"    public static void WriteValue(ref this LuminPackWriter writer, scoped in {fieldType} value)");
                sb.AppendLine("    {");
                sb.AppendLine("        // TODO: 实现序列化逻辑");
                sb.AppendLine("    }");
                sb.AppendLine();
                
                // 生成Reader扩展方法
                sb.AppendLine($"    public static void ReadValue(ref this LuminPackReader reader, scoped ref {fieldType} value)");
                sb.AppendLine("    {");
                sb.AppendLine("        // TODO: 实现反序列化逻辑");
                sb.AppendLine("        value = default;");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }
        
        return GenerateExtension(sb);
    }
    
    private static string GenerateExtension(StringBuilder sb)
    {
        var extensionCode = sb.ToString();
        
        if (string.IsNullOrEmpty(extensionCode))
            return string.Empty;
            
        StringBuilder fullCode = new StringBuilder();
        
        fullCode.AppendLine("using LuminPack.Core;");
        fullCode.AppendLine();
        fullCode.AppendLine($"namespace {LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE};");
        fullCode.AppendLine();
        fullCode.AppendLine("public static partial class LuminPackExtensions");
        fullCode.AppendLine("{");
        fullCode.Append(extensionCode);
        fullCode.AppendLine("}");
        
        return fullCode.ToString();
    }

    private static void GeneratorGlobalMethod(StringBuilder sb)
    {
        sb.AppendLine("    public static void WriteValue<T>(ref this LuminPackWriter writer, scoped in T value)");
        sb.AppendLine("    {");
        sb.AppendLine("        LuminPackParseProvider.Cache<T>.Parser!.Serialize(ref writer, ref System.Runtime.CompilerServices.Unsafe.AsRef(in value));");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public static void ReadValue<T>(ref this LuminPackReader reader, scoped ref T value)");
        sb.AppendLine("    {");
        sb.AppendLine("        LuminPackParseProvider.Cache<T>.Parser!.Deserialize(ref reader, ref value);");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
    
}
