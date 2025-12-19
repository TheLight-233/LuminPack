using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Formatter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
    public static ConditionalWeakTable<Compilation, HashSet<string>> AnalyzedTypes = new();
    private static object _sync = new();
    
    
    public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
    {
        StringBuilder sb = new StringBuilder();
        
        var analyzedTypes = AnalyzedTypes.GetOrCreateValue(compilation);
        
        HashSet<string> currentGenerationTypes = new HashSet<string>();
        
        sb.AppendLine();

        foreach (var v in data.localFields)
        {
            #region Generic

            bool containsGeneric = false;
            foreach (var genericParameter in data.GenericParameters)
            {
                if (v.TypeName.Contains(genericParameter))
                {
                    containsGeneric = true;
                    break;
                }
            }
            if (containsGeneric)
                continue;

            #endregion
            
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
        string genericParameters = string.Join(", ", data.GenericParameters);
        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        if (analyzedTypes.Add(classGlobalName) && 
            currentGenerationTypes.Add(classGlobalName))
        {
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");

            if (data.isGeneric)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WriteValue<{genericParameters}>(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                    : $"        public static void WriteValue<{genericParameters}>(ref this LuminPackWriter writer, in {classGlobalName} value)");
                foreach (var constraint in data.GenericConstraints)
                {
                
                    if (constraint.IsUnmanaged is false && 
                        constraint.IsClass is false && 
                        constraint.IsStruct is false &&
                        constraint.IsNotNull is false &&
                        constraint.HasDefault is false &&
                        constraint.HasNewConstructor is false &&
                        constraint.Constraints.Count is 0) continue;
                    sb.Append("            ");
                    sb.Append("where ");
                    sb.Append(constraint.ParameterName);
                    sb.Append(" : ");

                    var constraints = new List<string>();

                    // 特殊约束
                    if (constraint.IsUnmanaged) constraints.Add("unmanaged");
                    if (constraint.IsClass) constraints.Add("class");
                    if (constraint.IsStruct) constraints.Add("struct");
                    if (constraint.IsNotNull) constraints.Add("notnull");
                    if (constraint.HasNewConstructor) constraints.Add("new()");
                    if (constraint.HasDefault) constraints.Add("default");

                    // 类型约束（如 IComparable）
                    constraints.AddRange(constraint.Constraints);

                    sb.Append(string.Join(", ", constraints));
                
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WriteValue(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                    : $"        public static void WriteValue(ref this LuminPackWriter writer, in {classGlobalName} value)");
            }
            sb.AppendLine("        {");
            GenerateMyselfSerialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            
            if (metaInfo.IsNet8 && metaInfo.AllowUnsafe) 
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.SkipLocalsInit]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");

            if (data.isGeneric)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadValue<{genericParameters}>(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                    : $"        public static void ReadValue<{genericParameters}>(ref this LuminPackReader reader, ref {classGlobalName} value)");
                foreach (var constraint in data.GenericConstraints)
                {
                
                    if (constraint.IsUnmanaged is false && 
                        constraint.IsClass is false && 
                        constraint.IsStruct is false &&
                        constraint.IsNotNull is false &&
                        constraint.HasDefault is false &&
                        constraint.HasNewConstructor is false &&
                        constraint.Constraints.Count is 0) continue;
                    sb.Append("            ");
                    sb.Append("where ");
                    sb.Append(constraint.ParameterName);
                    sb.Append(" : ");

                    var constraints = new List<string>();

                    // 特殊约束
                    if (constraint.IsUnmanaged) constraints.Add("unmanaged");
                    if (constraint.IsClass) constraints.Add("class");
                    if (constraint.IsStruct) constraints.Add("struct");
                    if (constraint.IsNotNull) constraints.Add("notnull");
                    if (constraint.HasNewConstructor) constraints.Add("new()");
                    if (constraint.HasDefault) constraints.Add("default");

                    // 类型约束（如 IComparable）
                    constraints.AddRange(constraint.Constraints);

                    sb.Append(string.Join(", ", constraints));
                
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadValue(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                    : $"        public static void ReadValue(ref this LuminPackReader reader, ref {classGlobalName} value)");

            }
            sb.AppendLine("        {");
            GenerateMyselfDeserialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();

            #region Polymorpshim

            if (data.isUnion) goto Local;
                
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");

            if (data.isGeneric)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WritePolymorphismValue<{genericParameters}>(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                    : $"        public static void WritePolymorphismValue<{genericParameters}>(ref this LuminPackWriter writer, in {classGlobalName} value)");
                foreach (var constraint in data.GenericConstraints)
                {
                
                    if (constraint.IsUnmanaged is false && 
                        constraint.IsClass is false && 
                        constraint.IsStruct is false &&
                        constraint.IsNotNull is false &&
                        constraint.HasDefault is false &&
                        constraint.HasNewConstructor is false &&
                        constraint.Constraints.Count is 0) continue;
                    sb.Append("            ");
                    sb.Append("where ");
                    sb.Append(constraint.ParameterName);
                    sb.Append(" : ");

                    var constraints = new List<string>();

                    // 特殊约束
                    if (constraint.IsUnmanaged) constraints.Add("unmanaged");
                    if (constraint.IsClass) constraints.Add("class");
                    if (constraint.IsStruct) constraints.Add("struct");
                    if (constraint.IsNotNull) constraints.Add("notnull");
                    if (constraint.HasNewConstructor) constraints.Add("new()");
                    if (constraint.HasDefault) constraints.Add("default");

                    // 类型约束（如 IComparable）
                    constraints.AddRange(constraint.Constraints);

                    sb.Append(string.Join(", ", constraints));
                
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WritePolymorphismValue(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                    : $"        public static void WritePolymorphismValue(ref this LuminPackWriter writer, in {classGlobalName} value)");
            }
            
            sb.AppendLine("        {");
            GenerateMyselfSerialize(data, sb, polymorphism: true);
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            if (metaInfo.IsNet8 && metaInfo.AllowUnsafe) 
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.SkipLocalsInit]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");

            if (data.isGeneric)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadPolymorphismValue<{genericParameters}>(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                    : $"        public static void ReadPolymorphismValue<{genericParameters}>(ref this LuminPackReader reader, ref {classGlobalName} value)");
                foreach (var constraint in data.GenericConstraints)
                {
                
                    if (constraint.IsUnmanaged is false && 
                        constraint.IsClass is false && 
                        constraint.IsStruct is false &&
                        constraint.IsNotNull is false &&
                        constraint.HasDefault is false &&
                        constraint.HasNewConstructor is false &&
                        constraint.Constraints.Count is 0) continue;
                    sb.Append("            ");
                    sb.Append("where ");
                    sb.Append(constraint.ParameterName);
                    sb.Append(" : ");

                    var constraints = new List<string>();

                    // 特殊约束
                    if (constraint.IsUnmanaged) constraints.Add("unmanaged");
                    if (constraint.IsClass) constraints.Add("class");
                    if (constraint.IsStruct) constraints.Add("struct");
                    if (constraint.IsNotNull) constraints.Add("notnull");
                    if (constraint.HasNewConstructor) constraints.Add("new()");
                    if (constraint.HasDefault) constraints.Add("default");

                    // 类型约束（如 IComparable）
                    constraints.AddRange(constraint.Constraints);

                    sb.Append(string.Join(", ", constraints));
                
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadPolymorphismValue(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                    : $"        public static void ReadPolymorphismValue(ref this LuminPackReader reader, ref {classGlobalName} value)");
            }
            
            sb.AppendLine("        {");
            GenerateMyselfDeserialize(data, sb, polymorphism: true);
            sb.AppendLine("        }");
            sb.AppendLine();

            #endregion
                
            Local:
            LuminPackCodeGenerator.GenerateLocalClassStructure(sb, data, analyzedTypes);
                
            foreach (var filed in data.fields.Where(x => x.ClassFields.Count > 0))
            {
                LuminPackCodeGenerator.GeneratorUnsafeAccessorMethod(sb, filed, filed.ClassFields, analyzedTypes);
            }
        }

        #endregion
        
        return GenerateExtension(sb, compilation);
    }
    
    private static string GenerateExtension(StringBuilder sb, Compilation compilation)
    {
        lock (_sync)
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
    }
    
    private static void GenerateMyselfSerialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateSerializeCode(data, sb, true, polymorphism); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateSerializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateSerializeCode(data, sb); break;
        }
    }
    
    private static void GenerateMyselfDeserialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateDeserializeCode(data, sb, polymorphism); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateDeserializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateDeserializeCode(data, sb); break;
        }
    }
    
    /// <summary>
    /// 清理程序集名称，将非字母数字字符替换为下划线，生成合法的 C# 标识符
    /// </summary>
    private static string SanitizeAssemblyName(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return "Unknown";
        
        var sb = new StringBuilder(assemblyName.Length);
        
        for (int i = 0; i < assemblyName.Length; i++)
        {
            char c = assemblyName[i];
            
            // 第一个字符必须是字母或下划线
            if (i == 0)
            {
                if (char.IsLetter(c) || c == '_')
                    sb.Append(c);
                else if (char.IsDigit(c))
                    sb.Append('_').Append(c); // 数字开头加前缀
                else
                    sb.Append('_');
            }
            else
            {
                // 后续字符可以是字母、数字或下划线
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
        }
        
        return sb.ToString();
    }
    
    public static string GenerateParsersRegistry(Compilation compilation)
    {
        if (string.IsNullOrEmpty(compilation.AssemblyName))
            return string.Empty;
        
        // 清理程序集名称为合法标识符
        var sanitizedAssemblyName = SanitizeAssemblyName(compilation.AssemblyName);
        
        var sb = new StringBuilder();
    
        // 添加 using 语句
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack;");
        sb.AppendLine();
        
        // 如果程序集名为 Unknown，不生成命名空间
        if (sanitizedAssemblyName != "Unknown")
        {
            sb.AppendLine($"namespace {LuminPackSourceGenerator.LUMIN_REGISTERS_NAMESPACE}.{sanitizedAssemblyName}");
            sb.AppendLine("{");
        }
        
        sb.AppendLine("    public static class GeneratedParsersRegistry");
        sb.AppendLine("    {");
        sb.AppendLine(GenerateParserTypeList(compilation, sanitizedAssemblyName));
        sb.AppendLine("    }");
        
        if (sanitizedAssemblyName != "Unknown")
        {
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }
    

    
    private static string GenerateParserTypeList(Compilation compilation, string sanitizedAssemblyName)
    {
        var sb = new StringBuilder();

        if (TypeMetaChecker.IsUnityProject(compilation))
        {
            // Unity 项目：使用 Unity 的初始化特性
            sb.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
        }
        else
        {
            // 纯 .NET 项目：使用 ModuleInitializer
            sb.AppendLine("#if NET5_0_OR_GREATER");
            sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializerAttribute]");
            sb.AppendLine("#else");
            sb.AppendLine("        // 需要手动调用初始化");
            sb.AppendLine("        // 请在程序入口调用 LuminPackSerializer.Initialize(GeneratedParsersRegistry.ParserTypes)");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");
        
        // 如果程序集名为 Unknown，使用全局命名空间
        if (sanitizedAssemblyName == "Unknown")
        {
            sb.AppendLine($"            global::LuminPack.LuminPackSerializer.Initialize(global::GeneratedParsersRegistry.ParserTypes);");
        }
        else
        {
            sb.AppendLine($"            global::LuminPack.LuminPackSerializer.Initialize(global::{LuminPackSourceGenerator.LUMIN_REGISTERS_NAMESPACE}.{sanitizedAssemblyName}.GeneratedParsersRegistry.ParserTypes);");
        }
        
        sb.AppendLine("        }");
        
        sb.AppendLine();

        sb.AppendLine("        public static readonly List<(Type TargetType, Type ParserType)> ParserTypes = new()");
        sb.AppendLine("        {");

        foreach (var typeSymbol in GetLuminPackableTypes(compilation))
        {
            if (typeSymbol is null) continue;
            if (TypeMetaChecker.CheckGeneratorType(typeSymbol) is GeneratorType.NonGenerator) continue;

            var targetTypeName = BuildCorrectOpenGenericTypeName(typeSymbol);
            var parserTypeName = BuildCorrectOpenGenericParserTypeName(typeSymbol);

            sb.AppendLine($"            (typeof({targetTypeName}), typeof(global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{parserTypeName})),");
        }

        sb.AppendLine("        };");

        return sb.ToString();
    }

    /// <summary>
    /// 构建正确的开放泛型类型名：global::Namespace.OuterClass.InnerClass<>
    /// </summary>
    private static string BuildCorrectOpenGenericTypeName(INamedTypeSymbol typeSymbol)
    {
        var sb = new StringBuilder();
        sb.Append("global::");

        // 1. 添加命名空间（仅最外层一次）
        var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>")
        {
            sb.Append(namespaceName);
            sb.Append(".");
        }

        // 2. 构建完整的类型链（从最外层到当前类型）
        var typeNames = new List<string>();
        var current = typeSymbol;
    
        while (current != null)
        {
            string typeName = current.Name;
        
            // 如果是泛型，添加开放泛型参数
            if (current.IsGenericType)
            {
                int paramCount = current.TypeParameters.Length;
                typeName += paramCount == 1 ? "<>" : $"<{new string(',', paramCount - 1)}>";
            }
        
            typeNames.Insert(0, typeName);
            current = current.ContainingType; // 移动到外层类型
        }

        // 3. 用点连接所有类型
        sb.Append(string.Join(".", typeNames));
    
        return sb.ToString();
    }

    /// <summary>
    /// 构建正确的开放泛型 Parser 类型名：Namespace_OuterClass_InnerClassParser<>
    /// </summary>
    private static string BuildCorrectOpenGenericParserTypeName(INamedTypeSymbol typeSymbol)
    {
        var sb = new StringBuilder();

        // 1. 添加命名空间
        var namespaceName = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(namespaceName) && namespaceName != "<global namespace>")
        {
            sb.Append(namespaceName);
            sb.Append("_");
        }

        // 2. 构建完整的类型链
        var typeNames = new List<string>();
        var current = typeSymbol;
    
        while (current != null)
        {
            typeNames.Insert(0, current.Name);
            current = current.ContainingType;
        }

        // 3. 用下划线连接，并添加 Parser 后缀
        sb.Append(string.Join("_", typeNames));
        sb.Append("Parser");

        // 4. 如果是泛型，添加开放泛型参数
        if (typeSymbol.IsGenericType)
        {
            int paramCount = typeSymbol.TypeParameters.Length;
            sb.Append(paramCount == 1 ? "<>" : $"<{new string(',', paramCount - 1)}>");
        }

        return sb.ToString();
    }
    
    
    // 获取所有标记了 LuminPackableAttribute 的类型
    static IEnumerable<INamedTypeSymbol> GetLuminPackableTypes(Compilation compilation)
    {
        var luminPackableAttribute = compilation.GetTypeByMetadataName(LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
    
        if (luminPackableAttribute == null)
            yield break;

        // 遍历当前编译中的所有语法树
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
        
            // 查找所有类型声明
            var typeDeclarations = tree.GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(syntax => syntax 
                    is ClassDeclarationSyntax 
                    or StructDeclarationSyntax
                    or InterfaceDeclarationSyntax
                    or RecordDeclarationSyntax);
            
            foreach (var typeDecl in typeDeclarations)
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
            
                if (typeSymbol != null && 
                    typeSymbol.GetAttributes()
                        .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, luminPackableAttribute)))
                {
                    yield return typeSymbol;
                }
            }
        }
    }
}