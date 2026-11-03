using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Stores explicitly typed fixed records after all reservation and layout proofs have finished.</summary>
internal sealed class BinaryReservedStoreProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;

    /// <summary>Resolves canonical enum widths without assuming a target process architecture.</summary>
    /// <param name="compilation">The consumer's current type definitions and references.</param>
    internal BinaryReservedStoreProjection(Compilation compilation) => _compilation = compilation;

    /// <summary>Determines the constant primitive or enum width when its canonical spelling resolves.</summary>
    /// <param name="type">The explicit field storage type.</param>
    /// <returns>The fixed width, or zero when a target-independent width is not known.</returns>
    private int Width(TypeSyntax type)
    {
        string name = type.ToString().Replace("global::", string.Empty);
        var code = name switch
        {
            "bool" or "System.Boolean" => SpecialType.System_Boolean,
            "byte" or "System.Byte" => SpecialType.System_Byte,
            "sbyte" or "System.SByte" => SpecialType.System_SByte,
            "char" or "System.Char" => SpecialType.System_Char,
            "short" or "System.Int16" => SpecialType.System_Int16,
            "ushort" or "System.UInt16" => SpecialType.System_UInt16,
            "int" or "System.Int32" => SpecialType.System_Int32,
            "uint" or "System.UInt32" => SpecialType.System_UInt32,
            "float" or "System.Single" => SpecialType.System_Single,
            "long" or "System.Int64" => SpecialType.System_Int64,
            "ulong" or "System.UInt64" => SpecialType.System_UInt64,
            "double" or "System.Double" => SpecialType.System_Double,
            "decimal" or "System.Decimal" => SpecialType.System_Decimal,
            _ => _compilation.GetTypeByMetadataName(name)?.EnumUnderlyingType?.SpecialType ?? SpecialType.None
        };
        return code switch
        {
            SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte => 1,
            SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 => 2,
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Single => 4,
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Double => 8,
            SpecialType.System_Decimal => 16,
            _ => 0
        };
    }

    /// <summary>Expands a fixed store without depending on two nested helper imports.</summary>
    /// <param name="node">The original complete cursor assignment.</param>
    /// <returns>The same fixed operation with the original wide-value fallback on 32-bit targets.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is not AssignmentExpressionSyntax
            { Left: IdentifierNameSyntax destination, Right: RefExpressionSyntax
                { Expression: InvocationExpressionSyntax call } } ||
            call.Expression is not MemberAccessExpressionSyntax
                { Name: GenericNameSyntax generic } access || generic.Identifier.ValueText != "WriteReserved" ||
            access.Expression.ToString() is not ("LuminPackWriter" or "global::LuminPack.Core.LuminPackWriter") ||
            generic.TypeArgumentList.Arguments.Count != 1 || call.ArgumentList.Arguments.Count != 2 ||
            call.ArgumentList.Arguments[0] is not { RefKindKeyword.RawKind: (int)SyntaxKind.RefKeyword,
                Expression: IdentifierNameSyntax cursor } || cursor.Identifier.ValueText != destination.Identifier.ValueText)
            return base.VisitExpressionStatement(node);
        string type = generic.TypeArgumentList.Arguments[0].ToString();
        string target = cursor.ToString();
        string value = call.ArgumentList.Arguments[1].Expression.ToString();
        const string utility = "global::System.Runtime.CompilerServices.Unsafe";
        string direct = utility + ".WriteUnaligned<" + type + ">(ref " + target + ", " + value + "); " +
            target + " = ref " + utility + ".Add(ref " + target + ", " + utility + ".SizeOf<" + type + ">());";
        int width = Width(generic.TypeArgumentList.Arguments[0]);
        if (width is > 0 and <= 4) return SyntaxFactory.ParseStatement("{ " + direct + " }").WithTriviaFrom(node);
        string condition = utility + ".SizeOf<global::System.IntPtr>() == 8";
        if (width == 0) condition += " || " + utility + ".SizeOf<" + type + ">() <= 4";
        return SyntaxFactory.ParseStatement("if (" + condition + ") { " + direct + " } else { " + node.WithoutTrivia() + " }")
            .WithTriviaFrom(node);
    }
}
