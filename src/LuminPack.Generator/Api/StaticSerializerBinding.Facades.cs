using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    /// <summary>Retains one existing compiled-profile convenience body and removes redundant default forwarding shells.</summary>
    /// <param name="root">The concrete convenience fragment before its final documentation and formatting.</param>
    /// <param name="apis">All emitted families in concrete emitter order.</param>
    /// <param name="directApis">Families reached by direct calls in user source.</param>
    /// <returns>The existing implementation members without their default-profile trampolines.</returns>
    internal static CompilationUnitSyntax SelectFacades(CompilationUnitSyntax root, SerializerApiKind apis, SerializerApiKind directApis)
    {
        var container = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        var members = container.Members.Where(member =>
            member is not MethodDeclarationSyntax method || method.TypeParameterList != null ||
            method.Identifier.ValueText is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync"));
        if (apis == directApis)
            return root.ReplaceNode(container, container.WithMembers(SyntaxFactory.List(members)));

        // Concrete emitters and the signature-only facade use the same bit order.
        int next = 1;
        var documented = members.Select(member =>
        {
            if (member is not MethodDeclarationSyntax method ||
                method.Identifier.ValueText is not ("Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync"))
                return member;
            while (next <= (int)SerializerApiKind.All && ((int)apis & next) == 0)
                next <<= 1;
            bool direct = ((int)directApis & next) != 0;
            next <<= 1;
            return direct ? member : member.WithAdditionalAnnotations(GeneratedSourceDocumentation.ImplementationEntry);
        });
        return root.ReplaceNode(container, container.WithMembers(SyntaxFactory.List(documented)));
    }
}
