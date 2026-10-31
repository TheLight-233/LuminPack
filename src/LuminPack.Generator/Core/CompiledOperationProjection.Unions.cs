using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal sealed partial class CompiledOperationProjection
{
    /// <summary>Uses ordinary virtual slots for each declared profile, avoiding generic virtual method dispatch.</summary>
    /// <param name="members">The generated type's member list.</param>
    /// <param name="method">The original hidden virtual or interface slot.</param>
    /// <param name="owner">The original generated partial type.</param>
    /// <param name="writer">Whether this slot writes a binary or JSON payload.</param>
    private void AppendUnionSlots(List<MemberDeclarationSyntax> members, MethodDeclarationSyntax method, TypeDeclarationSyntax owner, bool writer)
    {
        string operation = method.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ? "__LuminPackUnionSerializeJson_" :
            method.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal) ? "__LuminPackUnionCalculateOffset_" : "__LuminPackUnionSerialize_";
        string suffix = method.Identifier.ValueText.Substring(operation.Length);
        var root = _types.Values.OfType<INamedTypeSymbol>().FirstOrDefault(type =>
            (type.IsAbstract || type.TypeKind == TypeKind.Interface) && LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type.OriginalDefinition) == suffix)?.OriginalDefinition;
        bool json = method.ParameterList.Parameters[0].Type.ToString().Contains("Json");
        var declared = (root == null ? _default : CompiledOptions.Resolve(root, _compilation, importedScope: false)).UnionProfile(json);
        var profiles = CompiledOptions.UnionProfiles(json).ToArray();
        string state = method.ParameterList.Parameters[0].Identifier.ValueText;
        foreach (var profile in profiles)
        {
            string name = method.Identifier.ValueText + (profile.Identity == declared.Identity ? "" : "_Options_" + profile.Identity);
            var body = new Body(profile.TypeName, method.ParameterList.Parameters[0].Type.ToString(), writer, state, false, null).Visit(method.Body ?? (SyntaxNode)method.ExpressionBody);
            var slot = method.WithIdentifier(SyntaxFactory.Identifier(name));
            slot = method.Body != null ? slot.WithBody((BlockSyntax)body) : slot.WithExpressionBody((ArrowExpressionClauseSyntax)body);
            members.Add(slot.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
        }
        if (method.Modifiers.Any(SyntaxKind.OverrideKeyword) || owner is not InterfaceDeclarationSyntax && !method.Modifiers.Any(SyntaxKind.VirtualKeyword)) return;
        string rootName = root == null ? owner.Identifier.ValueText : FormatterTypeName.Get(root);
        var source = new System.Text.StringBuilder();
        string profileParameter = "__LuminProfile";
        while (owner.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Any(type =>
            type.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == profileParameter) == true)) profileParameter += "_";
        source.AppendLine("/// <summary>Selects the compiled wire profile through an ordinary virtual slot.</summary>");
        source.AppendLine("/// <typeparam name=\"" + profileParameter + "\">The stateless wire profile.</typeparam>");
        source.AppendLine("/// <param name=\"writer\">The existing operation state.</param>");
        source.AppendLine("/// <param name=\"value\">The original Union reference.</param>");
        source.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        source.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("public static void ").Append(method.Identifier.ValueText).Append("_Compiled<").Append(profileParameter).Append(">(ref ").Append(method.ParameterList.Parameters[0].Type).Append(" writer, ")
            .Append(rootName).Append(" value) where ").Append(profileParameter).AppendLine(" : struct, global::LuminPack.Option.ILuminPackOptions {");
        // Write slots use the tiered dispatch; offset calculation keeps the original virtual call.
        var tier = root != null && operation != "__LuminPackUnionCalculateOffset_" ? UnionDispatchTierPlan.Get(root, _compilation) : null;
        foreach (var profile in profiles)
        {
            string name = method.Identifier.ValueText + (profile.Identity == declared.Identity ? "" : "_Options_" + profile.Identity);
            source.Append("if (default(").Append(profileParameter).Append(").StringEncoding == (global::LuminPack.Option.LuminPackStringEncoding)").Append(profile.Encoding);
            if (!json) source.Append(" && default(").Append(profileParameter).Append(").StringRecording == (global::LuminPack.Option.LuminPackStringRecording)").Append(profile.Recording);
            source.Append(") ").AppendLine(tier?.Dispatch(name) ?? "{ value." + name + "(ref writer); return; }");
        }
        source.AppendLine("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(\"The compiled profile has an invalid string encoding or recording mode.\"); }");
        members.Add(SyntaxFactory.ParseMemberDeclaration(source.ToString()).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
    }

}
