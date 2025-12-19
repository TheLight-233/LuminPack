using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class LuminPackRegistryGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// 清理程序集名称,将非字母数字字符替换为下划线,生成合法的 C# 标识符
        /// </summary>
        private static string SanitizeAssemblyName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return "Unknown";
            
            var sb = new System.Text.StringBuilder(assemblyName.Length);
            
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
        
        /// <summary>
        /// 检查程序集中是否存在标记了 LuminPackable 特性的类型
        /// </summary>
        private static bool HasLuminPackableTypes(Compilation compilation)
        {
            var luminPackableAttribute = compilation.GetTypeByMetadataName(
                LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE
            );
            
            if (luminPackableAttribute == null)
                return false;

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
                        return true; // 找到至少一个标记的类型
                    }
                }
            }
            
            return false;
        }
        
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(
                context.CompilationProvider,
                (spc, compilation) =>
                {
                    var serializerType = compilation.GetTypeByMetadataName("LuminPack.LuminPackSerializer");
                    if (serializerType == null)
                    {
                        return;
                    }
                    
                    // 检查程序集中是否有标记了 [LuminPackable] 的类型
                    if (!HasLuminPackableTypes(compilation))
                    {
                        return; // 没有需要序列化的类型，跳过生成
                    }
                    
                    // 使用清理后的程序集名称检查已存在的注册类
                    var sanitizedAssemblyName = SanitizeAssemblyName(compilation.AssemblyName ?? "");
                    
                    // 如果程序集名为 Unknown，使用全局命名空间
                    string registryFullName;
                    if (sanitizedAssemblyName == "Unknown")
                    {
                        registryFullName = "GeneratedParsersRegistry";
                    }
                    else
                    {
                        registryFullName = $"{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{sanitizedAssemblyName}.GeneratedParsersRegistry";
                    }
                    
                    var existingRegistry = compilation.GetTypeByMetadataName(registryFullName);
                    if (existingRegistry != null)
                    {
                        return;
                    }

                    var registryCode = LuminPackExtensionGenerator.GenerateParsersRegistry(compilation);
                    if (!string.IsNullOrEmpty(registryCode))
                    {
                        spc.AddSource("GeneratedParsersRegistry.g.cs", registryCode);
                    }
                });
        }
    }
}