using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    /// <summary>Retains one existing compiled-profile convenience body and removes redundant default forwarding shells.</summary>
    /// <param name="source">The original concrete convenience fragment.</param>
    /// <param name="metadata">The current consumer syntax settings.</param>
    /// <returns>The existing implementation members without their default-profile trampolines.</returns>
    internal static string SelectFacades(string source, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText("class Container {" + source + "}", metadata.ParseOptions).GetCompilationUnitRoot();
        var members = root.Members.OfType<ClassDeclarationSyntax>().Single().Members.Where(member =>
            member is not MethodDeclarationSyntax method || method.TypeParameterList != null ||
            method.Identifier.ValueText is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync"));
        return string.Join("\n", members.Select(member => member.ToFullString()));
    }
}
