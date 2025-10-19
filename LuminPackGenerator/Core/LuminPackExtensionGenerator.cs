using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Formatter;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
    public static ConditionalWeakTable<Compilation, HashSet<string>> AnalyzedTypes = new();
    
    public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
    {
        StringBuilder sb = new StringBuilder();
        
        var analyzedTypes = AnalyzedTypes.GetOrCreateValue(compilation);
        
        HashSet<string> currentGenerationTypes = new HashSet<string>();
        
        sb.AppendLine();

        foreach (var v in data.localFields)
        {
            var formatter = FormatterDiscovery.GetFormatter(v.TypeName);
        
            if (formatter.Item1 != null && 
                formatter.Item2 != null &&
                analyzedTypes.Add(v.TypeName) && 
                currentGenerationTypes.Add(v.TypeName))
            {
                sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WriteValue(ref this LuminPackWriter writer, scoped in {v.TypeName} value)"
                    : $"        public static void WriteValue(ref this LuminPackWriter writer, in {v.TypeName} value)");
                sb.AppendLine("        {");
                formatter.Item1(v, sb);
                sb.AppendLine("        }");
                sb.AppendLine();
            
                sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadValue(ref this LuminPackReader reader, scoped ref {v.TypeName} value)"
                    : $"        public static void ReadValue(ref this LuminPackReader reader, ref {v.TypeName} value)");
                sb.AppendLine("        {");
                formatter.Item2(v, sb);
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }
        
        //GenerateKnownTypeExtensions(sb, analyzedTypes, currentGenerationTypes, metaInfo);
        
        #region 生成自己

        string classFullName = data.className + "Parser";
        string classGlobalName = data.classFullName;
        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for(var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }
            classFullName += ">";
        }

        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        if (analyzedTypes.Add(classGlobalName) && 
            currentGenerationTypes.Add(classGlobalName) &&
            !data.isGeneric)
        {
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine(metaInfo.IsNet8 
                ? $"        public static void WriteValue(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                : $"        public static void WriteValue(ref this LuminPackWriter writer, in {classGlobalName} value)");
            sb.AppendLine("        {");
            GenerateMyselfSerialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            if (metaInfo.IsNet8) 
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.SkipLocalsInit]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine(metaInfo.IsNet8 
                ? $"        public static void ReadValue(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                : $"        public static void ReadValue(ref this LuminPackReader reader, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            GenerateMyselfDeserialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();

            if (data.generatorType is GeneratorType.Object)
            {
                LuminPackCodeGenerator.GenerateLocalClassStructure(sb, data, analyzedTypes);
                
                foreach (var filed in data.fields.Where(x => x.ClassFields.Count > 0))
                {
                    LuminPackCodeGenerator.GeneratorUnsafeAccessorMethod(sb, filed, filed.ClassFields, analyzedTypes);
                }
            }
        }

        #endregion
        
        return GenerateExtension(sb);
    }
    
    private static string GenerateExtension(StringBuilder sb)
    {
        var extensionCode = sb.ToString();
        
        if (string.IsNullOrEmpty(extensionCode))
            return string.Empty;
            
        StringBuilder fullCode = new StringBuilder();
        
        fullCode.AppendLine("using global::System;");
        fullCode.AppendLine("using global::System.Collections.Generic;");
        fullCode.AppendLine("using global::System.Runtime.CompilerServices;");
        fullCode.AppendLine("using global::System.Runtime.InteropServices;");
        fullCode.AppendLine("using global::System.Threading.Tasks;");
        fullCode.AppendLine("using global::LuminPack;");
        fullCode.AppendLine("using global::LuminPack.Data;");
        fullCode.AppendLine("using global::LuminPack.Code;");
        fullCode.AppendLine("using global::LuminPack.Core;");
        fullCode.AppendLine("using global::LuminPack.Parsers;");
        fullCode.AppendLine("using global::LuminPack.Utility;");
        fullCode.AppendLine("using global::LuminPack.Attribute;");
        fullCode.AppendLine();
        fullCode.AppendLine("#nullable enable");
        fullCode.AppendLine($"namespace {LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}");
        fullCode.AppendLine("{");
        fullCode.AppendLine();
        fullCode.AppendLine("    public static partial class LuminPackExtensions");
        fullCode.AppendLine("    {");
        fullCode.Append(extensionCode);
        fullCode.AppendLine("    }");
        fullCode.AppendLine("}");
        
        return fullCode.ToString();
    }
    
    private static void GenerateMyselfSerialize(LuminDataInfo data, StringBuilder sb)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateSerializeCode(data, sb, true); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateSerializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateSerializeCode(data, sb); break;
        }
    }
    
    private static void GenerateMyselfDeserialize(LuminDataInfo data, StringBuilder sb)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateDeserializeCode(data, sb); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateDeserializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateDeserializeCode(data, sb); break;
        }
    }
}
