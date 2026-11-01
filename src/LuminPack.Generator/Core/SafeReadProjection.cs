using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Adds streaming guards to the original typed read templates without changing trusted plans.</summary>
internal sealed class SafeReadProjection
{
    private const string Policy = SafeOperationProjection.Policy;
    private readonly ITypeSymbol _type;
    private readonly CompiledOptions _options;
    private readonly SemanticModel _model;
    private readonly bool _objectScope;

    /// <summary>Captures the original template's semantic identities only during this uncached projection.</summary>
    /// <param name="type">The operation type.</param><param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">Generation settings.</param><param name="options">The declaration profile.</param>
    /// <param name="model">The temporary original-template semantic model.</param>
    internal SafeReadProjection(ITypeSymbol type, Compilation compilation, MetaInfo metadata, CompiledOptions options, SemanticModel model)
    {
        _type = type; _options = options; _model = model;
        _objectScope = type.IsReferenceType && type is INamedTypeSymbol named &&
            CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named) &&
            compilation.GetTypeByMetadataName("LuminPack.Security.LuminPackInputPolicy")?.GetMembers("EnterObject").Length > 0;
    }

    /// <summary>Finds external declarations through serialized members, closing recursive paths once.</summary>
    /// <param name="type">The concrete root.</param><param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">Generation settings.</param>
    /// <returns>Whether this root's graph needs streaming plans.</returns>
    internal static bool ContainsPolicy(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        bool Visit(ITypeSymbol current)
        {
            if (current == null || !seen.Add(current)) return false;
            if (CompiledOptions.Resolve(current, compilation).Input.Enabled) return true;
            if (current is IArrayTypeSymbol array) return Visit(array.ElementType);
            if (current is not INamedTypeSymbol named) return false;
            if (named.TypeArguments.Any(Visit)) return true;
            if (!CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named)) return false;
            return LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata).fields.Any(field => Visit(field.TypeSymbol));
        }
        return Visit(type);
    }

    /// <summary>Scopes resource accounting around the original complete read body.</summary>
    /// <param name="method">The unchanged typed read.</param><param name="json">Whether it reads JSON.</param>
    /// <param name="fixedProof">An optional compiler-created flag for a fully validated scalar/string root.</param>
    /// <param name="fixedMembers">The already validated member marker on a proven fixed root.</param>
    /// <returns>The instrumented body with original early returns and cleanup.</returns>
    internal BlockSyntax Rewrite(MethodDeclarationSyntax method, bool json, string fixedProof = null, int fixedMembers = -1)
    {
        string state = method.ParameterList.Parameters[0].Identifier.Text;
        string value = method.ParameterList.Parameters[1].Identifier.Text;
        var body = (BlockSyntax)new Calls(_model, state, _options.TypeName, json, fixedProof, fixedMembers).Visit(method.Body);
        if (IsTrivial(_type) || _type.SpecialType == SpecialType.System_String) return body;
        if (!json && IsFlatSequence(_type) && !method.Body.ToString().Contains(".CopyTo(ref"))
        {
            string done = Policy + ".CompleteDetachedCollection(ref " + state + ", " + SafeOperationProjection.Schema + ");";
            body = (BlockSyntax)new DetachedReturns(done).Visit(body);
            return body.WithStatements(body.Statements.Add(SyntaxFactory.ParseStatement(done)));
        }
        body = (BlockSyntax)new SuccessfulReturns().Visit(body);
        bool compact = !json && _objectScope;
        string enter = compact ? ".EnterObject(ref " : ".EnterValue(ref ";
        string exit = compact ? ".ExitObject(in __inputValueScope);" : ".ExitValue(__inputValueScope);";
        string completion = Policy + (compact ? ".CompleteObject(ref " : ".CompleteValue(ref ") + state + ", ref __inputValueScope);";
        if (_type is INamedTypeSymbol named && named.TypeArguments.Length > 0)
        {
            string simple = named.OriginalDefinition.MetadataName;
            string args = string.Join(", ", named.TypeArguments.Select(FormatterTypeName.Get));
            if (simple is "Dictionary`2" or "IDictionary`2" or "IReadOnlyDictionary`2" or "SortedDictionary`2" or "SortedList`2" or "ConcurrentDictionary`2" or "ImmutableDictionary`2" or "ImmutableSortedDictionary`2" or "FrozenDictionary`2")
                completion += Policy + ".CompleteMap<" + args + ">(" + value + ", __inputValueScope);";
            else if (simple is "HashSet`1" or "SortedSet`1" or "ISet`1" or "IReadOnlySet`1" or "ImmutableHashSet`1" or "ImmutableSortedSet`1" or "FrozenSet`1")
                completion += Policy + ".CompleteSet<" + args + ">(" + value + ", __inputValueScope);";
        }
        string scope = Policy + enter + state + ", " + SafeOperationProjection.Schema + ")";
        if (fixedProof != null) scope = fixedProof + " ? default(global::LuminPack.Security.LuminPackInputPolicy.ObjectScope) : " + scope;
        return (BlockSyntax)SyntaxFactory.ParseStatement("{ var __inputValueScope = " + scope + "; bool __inputValueDone = false; try { " +
            string.Join("\n", body.Statements) + " __inputValueDone = true; } finally { " + (fixedProof == null ? "" : "if (!" + fixedProof + ") ") +
            "{ try { if (__inputValueDone) { " + completion + " } } finally { " + Policy + exit + " } } } }");
    }

    /// <summary>Identifies scalars whose every storage bit pattern is already a valid value.</summary>
    /// <param name="type">The scalar type.</param><returns>Whether no per-value domain scope is needed.</returns>
    private static bool IsTrivial(ITypeSymbol type) => type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or
        SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Char || type.ToDisplayString() is "System.Guid" or "System.TimeSpan";

    /// <summary>Retains one bulk payload operation for flat reference-free sequence records.</summary>
    /// <param name="type">The original collection.</param><returns>Whether a count boundary owns its complete resource check.</returns>
    private static bool IsFlatSequence(ITypeSymbol type) => type is IArrayTypeSymbol { Rank: 1 } array ? array.ElementType.IsUnmanagedType :
        type is INamedTypeSymbol { TypeArguments.Length: 1 } named && named.TypeArguments[0].IsUnmanagedType &&
        named.OriginalDefinition.MetadataName is "List`1" or "Memory`1" or "ReadOnlyMemory`1" or "ArraySegment`1" or "ImmutableArray`1" or "ImmutableList`1";

    /// <summary>Completes successful original early returns without introducing a value-state snapshot.</summary>
    private sealed class DetachedReturns : CSharpSyntaxRewriter
    {
        private readonly string _completion;
        /// <summary>Captures the existing record's completion.</summary><param name="completion">The boundary call.</param>
        internal DetachedReturns(string completion) => _completion = completion;
        /// <summary>Preserves nested helper return ownership.</summary><param name="node">The local function.</param><returns>The original function.</returns>
        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
        /// <summary>Completes only this value's successful return.</summary><param name="node">The original return.</param><returns>The completed return.</returns>
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node) => SyntaxFactory.Block(SyntaxFactory.ParseStatement(_completion), node);
    }

    /// <summary>Marks only this operation's early returns as successfully completed values.</summary>
    private sealed class SuccessfulReturns : CSharpSyntaxRewriter
    {
        /// <summary>Leaves nested function ownership unchanged.</summary>
        /// <param name="node">The nested function.</param><returns>The unchanged function.</returns>
        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
        /// <summary>Retains successful null/empty early completion before mandatory cleanup.</summary>
        /// <param name="node">The original return.</param><returns>The marked original return.</returns>
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
            => SyntaxFactory.Block(SyntaxFactory.ParseStatement("__inputValueDone = true;"), node);
    }

    /// <summary>Redirects boundary kernels to checked adapters which call their existing implementations.</summary>
    private sealed class Calls : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;
        private readonly string _state, _profile;
        private readonly bool _json;
        private readonly string _fixedProof;
        private readonly int _fixedMembers;
        private const string ProvenVoidCall = "LuminPack.ProvenVoidCall";
        /// <summary>Captures the original reader and fixed profile.</summary>
        /// <param name="model">Original template bindings.</param><param name="state">Reader parameter.</param>
        /// <param name="profile">Declared profile.</param><param name="json">Whether this is JSON.</param>
        /// <param name="fixedProof">The optional proven-root flag.</param>
        /// <param name="fixedMembers">The fixed root's validated member count.</param>
        internal Calls(SemanticModel model, string state, string profile, bool json, string fixedProof = null, int fixedMembers = -1)
        { _model = model; _state = state; _profile = profile; _json = json; _fixedProof = fixedProof; _fixedMembers = fixedMembers; }

        /// <summary>Keeps proven and guarded void kernels in ordinary conditional statements.</summary>
        /// <param name="node">The original void call statement.</param>
        /// <returns>The selected original or guarded call without a void conditional expression.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var result = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
            var original = result.Expression.GetAnnotations(ProvenVoidCall).FirstOrDefault();
            if (original == null) return result;
            return SyntaxFactory.IfStatement(SyntaxFactory.IdentifierName(_fixedProof),
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(original.Data)),
                SyntaxFactory.ElseClause(result.WithExpression(result.Expression.WithoutAnnotations(ProvenVoidCall))));
        }

        /// <summary>Checks actual consumed lengths and tokens before allocation and publication.</summary>
        /// <param name="node">The original read call.</param><returns>The original or guarded call.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            // The complete runtime kernel has no generated string quota/domain adapters. Keep their original staged path.
            if (!_json && node.Expression is MemberAccessExpressionSyntax stableAccess &&
                stableAccess.Expression.ToString() == "global::LuminPack.Code.LuminPackMarshal" &&
                stableAccess.Name.Identifier.ValueText == "TryReadStableStringKeys")
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression).WithTriviaFrom(node);
            string propertyMatcher = node.Expression is MemberAccessExpressionSyntax propertyAccess ? propertyAccess.Name.Identifier.ValueText :
                node.Expression is SimpleNameSyntax propertyName ? propertyName.Identifier.ValueText : "";
            if (_json && propertyMatcher is "GetJsonPropertyIndexUtf8" or "GetJsonPropertyIndexUtf16")
                return SyntaxFactory.ParseExpression(Policy + ".CheckJsonPropertyIndex(" + result + ", " + SafeOperationProjection.Schema + ")").WithTriviaFrom(node);
            if (_json && node.Expression is MemberAccessExpressionSyntax ordered && ordered.Name.Identifier.ValueText == "TryReadOrderedJsonProperty" &&
                node.ArgumentList.Arguments.Count == 2 && (node.ArgumentList.Arguments[0].Expression.ToString() == _state ||
                    _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.ToDisplayString() == "LuminPack.Core.LuminPackJsonReader"))
                return SyntaxFactory.ParseExpression(Policy + ".TryReadOrderedJsonProperty<" + _profile + ">(" + result.ArgumentList.Arguments + ", " + SafeOperationProjection.Schema + ")").WithTriviaFrom(node);
            if (node.Expression is not MemberAccessExpressionSyntax access) return result;
            string readerName = access.Expression.ToString();
            if (readerName != _state && _model.GetTypeInfo(access.Expression).Type?.ToDisplayString() != (_json ? "LuminPack.Core.LuminPackJsonReader" : "LuminPack.Core.LuminPackReader")) return result;
            string name = access.Name.Identifier.ValueText;
            var symbol = _model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            string profile = access.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0 ? generic.TypeArgumentList.Arguments.Last().ToString() : _profile;
            string call = null;
            string args = string.Join(", ", result.ArgumentList.Arguments);
            bool profileCall = access.Name is GenericNameSyntax profileName && profileName.TypeArgumentList.Arguments.Last().ToString().IndexOf("Options", StringComparison.Ordinal) >= 0;
            if (!_json)
            {
                if (name == "ReadStrings" && result.ArgumentList.Arguments.Count == 1)
                    call = "ReadStrings<" + profile + ">(ref " + readerName + ", " + args + ")";
                else if (name == "ReadLength") call = "ReadLength(ref " + readerName + ", " + (args.Length == 0 ? "1" : args) + ", " + SafeOperationProjection.Schema + ")";
                else if (name == "TryReadCollectionHead") call = "TryReadCollectionHead(ref " + readerName + ", " + args + (result.ArgumentList.Arguments.Count == 1 ? ", 1" : "") + ", " + SafeOperationProjection.Schema + ")";
                else if (name == "PeekCollectionHeader") call = "PeekCollectionHeader(ref " + readerName + ", " + args + (result.ArgumentList.Arguments.Count == 1 ? ", 1" : "") + ", " + SafeOperationProjection.Schema + ")";
                else if (name == "PeekObjectHeader") call = "PeekObjectHeader(ref " + readerName + ")";
                else if (name == "TryReadObjectHead") call = "TryReadObjectHead(ref " + readerName + (args.Length == 0 ? "" : ", " + args) + ")";
                else if ((name == "ReadStringRecord" || name == "Read") && result.ArgumentList.Arguments.Count == 1 &&
                    result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
                    (name == "ReadStringRecord" || _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.SpecialType == SpecialType.System_String))
                    call = "ReadStringRecord<" + profile + ">(ref " + readerName + ", " + args + ")";
                else if ((name == "ReadStringRecord" || name == "Read") && (name == "ReadStringRecord" || symbol?.ReturnType.SpecialType == SpecialType.System_String ||
                    profileCall && result.ArgumentList.Arguments.Count == 1 && result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.None)))
                    call = "ReadString<" + profile + ">(ref " + readerName + (args.Length == 0 ? ", null" : ", " + args) + ")";
                else if (name == "Read" && access.Name is GenericNameSyntax fixedRead && fixedRead.TypeArgumentList.Arguments.Count == 1 && fixedRead.TypeArgumentList.Arguments[0].ToString() == "byte" && args.Length == 0)
                    call = "ReadMarker(ref " + readerName + ")";
            }
            else
            {
                string scalar = name switch {
                    "ReadBool" or "ReadBoolean" => "bool", "ReadByte" => "byte", "ReadSByte" => "sbyte",
                    "ReadShort" => "short", "ReadUShort" => "ushort", "ReadInt" => "int", "ReadUInt" => "uint",
                    "ReadLong" => "long", "ReadULong" => "ulong", "ReadFloat" => "float", "ReadDouble" => "double",
                    "ReadDecimal" => "decimal", "ReadChar" => "char", "ReadString" => "string", _ => null };
                if (scalar != null && args.Length == 0)
                    call = "ReadJsonValue<" + scalar + ", " + profile + ">(ref " + readerName + ")";
                else if (name == "Read" && result.ArgumentList.Arguments.Count == 1 &&
                    result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
                    _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.SpecialType == SpecialType.System_String)
                    call = "ReadJsonString<" + profile + ">(ref " + readerName + ", " + args + ")";
                else if (name == "Read" && (access.Name is not GenericNameSyntax || profileCall) && args.Length == 0)
                    call = "ReadJsonToken<" + profile + ">(ref " + readerName + ")";
                else if (name == "Read" && access.Name is GenericNameSyntax valueRead && args.Length == 0)
                    call = "ReadJsonValue<" + valueRead.TypeArgumentList.Arguments[0] + ", " + (valueRead.TypeArgumentList.Arguments.Count > 1 ? profile : _profile) + ">(ref " + readerName + ")";
                else if (name == "Skip") call = "RejectUnknownJson<" + profile + ">(ref " + readerName + ")";
                else if (name == "TryReadPropertyName") call = "TryReadJsonProperty<" + profile + ">(ref " + readerName + ", " + args + ")";
                else if (name == "ReadNextFloatValue") call = "ReadNextJsonFloat<" + profile + ">(ref " + readerName + ")";
                else if (name == "ReadStringUtf8") call = "ReadJsonNameUtf8<" + profile + ">(ref " + readerName + ")";
                else if (name == "ReadStringUtf16") call = "ReadJsonNameUtf16<" + profile + ">(ref " + readerName + ")";
                else if (name == "TryReadEntry")
                    return SyntaxFactory.ParseExpression("(" + Policy + ".CanUseTrustedJsonEntry && " + result + ")").WithTriviaFrom(node);
            }
            string proven = call == "ReadMarker(ref " + readerName + ")" && _fixedMembers >= 0
                ? "(byte)" + _fixedMembers.ToString(System.Globalization.CultureInfo.InvariantCulture) : result.ToString();
            if (call != null && _fixedProof != null && symbol?.ReturnsVoid == true)
                return SyntaxFactory.ParseExpression(Policy + "." + call).WithAdditionalAnnotations(new SyntaxAnnotation(ProvenVoidCall, result.ToString()));
            return call == null ? result : SyntaxFactory.ParseExpression(_fixedProof == null ? Policy + "." + call :
                "(" + _fixedProof + " ? " + proven + " : " + Policy + "." + call + ")").WithTriviaFrom(node);
        }
    }
}
