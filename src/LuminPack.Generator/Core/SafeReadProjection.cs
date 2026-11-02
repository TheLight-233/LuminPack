using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Inserts untrusted-input checks at the actual read sites of an original binary read template.</summary>
/// <remarks>
/// The template keeps its body, construction and reuse. Counts, strings, object markers and Boolean domains are checked
/// where they are read, against limits frozen as constants. Recursive types count their nesting on the reader.
/// </remarks>
internal sealed class SafeReadProjection
{
    private const string Policy = SafeOperationProjection.Policy;
    private readonly ITypeSymbol _type;
    private readonly CompiledOptions _options;
    private readonly CompiledInputPolicy _input;
    private readonly SemanticModel _model;
    private readonly int _members = -1;
    private readonly bool _recursive;
    private readonly string _booleans;

    /// <summary>Captures the original template's semantic identities only during this uncached projection.</summary>
    /// <param name="type">The operation type.</param><param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">Generation settings.</param><param name="input">The root's frozen limits.</param>
    /// <param name="model">The temporary original-template semantic model.</param>
    internal SafeReadProjection(ITypeSymbol type, Compilation compilation, MetaInfo metadata, CompiledInputPolicy input, SemanticModel model)
    {
        _type = type; _options = CompiledOptions.Resolve(type, compilation); _input = input; _model = model;
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } named &&
            CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named))
        {
            var data = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
            if (named.IsReferenceType && !data.isUnion && data.generatorType == LuminPack.Code.GeneratorType.Object) _members = data.fields.Count;
            // Without a declared depth limit, recursion keeps only its stack guard.
            _recursive = input.Depth != int.MaxValue && RecursiveTypeAnalysis.IsRecursive(type, compilation);
        }
        var element = type is IArrayTypeSymbol { Rank: 1 } array ? array.ElementType :
            type is INamedTypeSymbol { TypeArguments.Length: 1 } list && list.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>" ? list.TypeArguments[0] : null;
        if (element?.SpecialType == SpecialType.System_Boolean) _booleans = Policy + ".CheckBooleans(";
    }

    /// <summary>Finds untrusted declarations through serialized members, closing recursive paths once.</summary>
    /// <param name="type">The concrete root.</param><param name="compilation">The consumer compilation.</param>
    /// <param name="metadata">Generation settings.</param>
    /// <returns>Whether this root's graph contains an untrusted boundary.</returns>
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

    /// <summary>Checks the original complete read body.</summary>
    /// <param name="method">The unchanged typed read.</param>
    /// <returns>The checked body with original early returns.</returns>
    internal BlockSyntax Rewrite(MethodDeclarationSyntax method)
    {
        string state = method.ParameterList.Parameters[0].Identifier.Text;
        string value = method.ParameterList.Parameters[1].Identifier.Text;
        var body = (BlockSyntax)new Calls(this, state).Visit(method.Body);
        var exit = new List<string>();
        if (_booleans != null) exit.Add(_booleans + value + ");");
        if (_recursive) exit.Add(Policy + ".ExitDepth(ref " + state + ");");
        if (exit.Count == 0) return body;
        // A null record is not a nested value: depth is entered after the object template's null test.
        int start = _recursive ? body.Statements.IndexOf(statement => statement.ToString().Contains(".ReadNullMarker(")) + 1 : 0;
        var completion = new CompletedReturns(string.Join(" ", exit));
        var statements = SyntaxFactory.List(body.Statements.Take(start).Concat(body.Statements.Skip(start).Select(statement => (StatementSyntax)completion.Visit(statement))))
            .AddRange(exit.Select(statement => SyntaxFactory.ParseStatement(statement)));
        if (_recursive)
            statements = statements.Insert(start, SyntaxFactory.ParseStatement(Policy + ".EnterDepth(ref " + state + ", " + Literal(_input.Depth) + ");"));
        return body.WithStatements(statements);
    }

    /// <summary>Formats a frozen limit.</summary>
    /// <param name="value">The limit.</param><returns>The invariant literal.</returns>
    private static string Literal(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Completes successful original early returns of this operation.</summary>
    private sealed class CompletedReturns : CSharpSyntaxRewriter
    {
        private readonly string _completion;
        /// <summary>Captures the completion statements.</summary><param name="completion">The statements run before returning.</param>
        internal CompletedReturns(string completion) => _completion = completion;
        /// <summary>Preserves nested helper return ownership.</summary><param name="node">The local function.</param><returns>The original function.</returns>
        public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
        /// <summary>Preserves lambda return ownership.</summary><param name="node">The lambda.</param><returns>The original lambda.</returns>
        public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;
        /// <summary>Preserves lambda return ownership.</summary><param name="node">The lambda.</param><returns>The original lambda.</returns>
        public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;
        /// <summary>Completes this operation's return.</summary><param name="node">The original return.</param><returns>The completed return.</returns>
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
            => SyntaxFactory.Block(((BlockSyntax)SyntaxFactory.ParseStatement("{" + _completion + "}")).Statements.Add(node));
    }

    /// <summary>Redirects read kernels to checked adapters which call their existing implementations.</summary>
    private sealed class Calls : CSharpSyntaxRewriter
    {
        private readonly SafeReadProjection _owner;
        private readonly string _state;
        private bool _markerSeen;

        /// <summary>Captures the original reader parameter.</summary>
        /// <param name="owner">The projection holding the type facts.</param><param name="state">Reader parameter.</param>
        internal Calls(SafeReadProjection owner, string state) { _owner = owner; _state = state; }

        /// <summary>Checks Boolean fields copied by a fixed field batch.</summary>
        /// <param name="node">An original statement block.</param><returns>The block with checked batches.</returns>
        /// <remarks>When every Boolean of a batch lies in one four- or eight-byte window, the batch copy tests their input bytes
        /// with one compile-time mask instead of reloading each copied field.</remarks>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var checks = new Dictionary<IfStatementSyntax, (List<string> Fields, string Batch)>();
            for (int index = 0; index < node.Statements.Count; index++)
            {
                if (node.Statements[index] is not IfStatementSyntax batch || batch.Statement is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax copy } ||
                    copy.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Read" } read || copy.ArgumentList.Arguments.Count != 2 ||
                    !copy.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || copy.ArgumentList.Arguments[0].Expression is not IdentifierNameSyntax storage)
                    continue;
                var booleans = new List<string>();
                var offsets = new List<int>();
                // The batch's first field initializes its storage reference; the condition proves every following offset.
                var declaration = node.Statements.Take(index).OfType<LocalDeclarationStatementSyntax>().Select(local => local.Declaration.Variables[0])
                    .LastOrDefault(variable => variable.Identifier.ValueText == storage.Identifier.ValueText);
                foreach (var cast in (declaration?.Initializer?.Value.DescendantNodesAndSelf() ?? Enumerable.Empty<SyntaxNode>()).OfType<InvocationExpressionSyntax>())
                    if (BooleanStorage(cast) is string field) { booleans.Add(field); offsets.Add(0); }
                foreach (var proof in batch.Condition.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Where(proof => proof.IsKind(SyntaxKind.EqualsExpression)))
                {
                    var cast = proof.Left.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Select(BooleanStorage).FirstOrDefault(field => field != null);
                    if (cast == null) continue;
                    booleans.Add(cast);
                    offsets.Add(int.TryParse(new string(proof.Right.ToString().Where(char.IsDigit).ToArray()), NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : -1);
                }
                if (booleans.Count == 0) continue;
                string windowed = null;
                if (int.TryParse(copy.ArgumentList.Arguments[1].Expression.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int length) && offsets.All(value => value >= 0 && value < length))
                {
                    int low = offsets.Min(), high = offsets.Max();
                    int width = length >= 8 && high - low < 8 ? 8 : length >= 4 && high - low < 4 ? 4 : 0;
                    if (width != 0)
                    {
                        int window = Math.Min(low, length - width);
                        ulong mask = offsets.Aggregate(0UL, (bits, value) => bits | 0xFEUL << (8 * (value - window)));
                        windowed = Policy + ".ReadBatch(ref " + read.Expression + ", ref " + storage.Identifier.ValueText + ", " + Literal(length) + ", " + Literal(window) + ", 0x" +
                            mask.ToString(width == 8 ? "X16" : "X8", CultureInfo.InvariantCulture) + (width == 8 ? "UL" : "U") + ");";
                    }
                }
                checks.Add(batch, (booleans, windowed));
            }
            var result = (BlockSyntax)base.VisitBlock(node);
            if (checks.Count == 0) return result;
            var statements = result.Statements.ToList();
            for (int index = 0; index < node.Statements.Count; index++)
            {
                if (node.Statements[index] is not IfStatementSyntax original || !checks.TryGetValue(original, out var batch)) continue;
                var rewritten = (IfStatementSyntax)statements[index];
                statements[index] = batch.Batch != null ? rewritten.WithStatement(SyntaxFactory.ParseStatement(batch.Batch)) :
                    rewritten.WithStatement(SyntaxFactory.Block(new[] { rewritten.Statement }.Concat(batch.Fields.Select(field =>
                        SyntaxFactory.ParseStatement(Policy + ".CheckBoolean(in " + field + ");")))));
            }
            return result.WithStatements(SyntaxFactory.List(statements));
        }

        /// <summary>Identifies a Boolean field reference reinterpreted as batch storage.</summary>
        /// <param name="node">An original invocation.</param><returns>The referenced field expression, or null.</returns>
        private string BooleanStorage(InvocationExpressionSyntax node)
        {
            if (node.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "As", TypeArgumentList.Arguments.Count: 2 } } ||
                node.ArgumentList.Arguments.Count != 1 || !node.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword)) return null;
            return _owner._model.GetSymbolInfo(node).Symbol is IMethodSymbol { TypeArguments.Length: 2 } method &&
                method.TypeArguments[0].SpecialType == SpecialType.System_Boolean && method.TypeArguments[1].SpecialType == SpecialType.System_Byte
                ? node.ArgumentList.Arguments[0].Expression.ToString() : null;
        }

        /// <summary>Reports a back reference to an object of another type as malformed input.</summary>
        /// <param name="node">An original cast.</param><returns>The original or checked conversion.</returns>
        public override SyntaxNode VisitCastExpression(CastExpressionSyntax node)
        {
            var result = (CastExpressionSyntax)base.VisitCastExpression(node);
            if (node.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetObjectReference" } }) return result;
            return SyntaxFactory.ParseExpression(Policy + ".ReadReference<" + result.Type + ">(" + result.Expression + ")").WithTriviaFrom(node);
        }

        /// <summary>Folds the object template's null test into one checked marker comparison.</summary>
        /// <param name="node">An original comparison.</param><returns>The original or checked comparison.</returns>
        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (_owner._members >= 0 && !_markerSeen && node.IsKind(SyntaxKind.EqualsExpression) && node.Right is LiteralExpressionSyntax { Token.ValueText: "255" } &&
                node.Left is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Read" } read } access } invocation &&
                invocation.ArgumentList.Arguments.Count == 0 && read.TypeArgumentList.Arguments.Count == 1 && read.TypeArgumentList.Arguments[0].ToString() == "byte" &&
                (access.Expression.ToString() == _state || _owner._model.GetTypeInfo(access.Expression).Type?.ToDisplayString() == "LuminPack.Core.LuminPackReader"))
            {
                _markerSeen = true;
                return SyntaxFactory.ParseExpression(Policy + ".ReadNullMarker(ref " + access.Expression + ", " + Literal(_owner._members) + ")").WithTriviaFrom(node);
            }
            return base.VisitBinaryExpression(node);
        }

        /// <summary>Checks actual consumed lengths, strings, markers and Boolean loads.</summary>
        /// <param name="node">The original read call.</param><returns>The original or checked call.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            var model = _owner._model;
            var symbol = model.GetSymbolInfo(node).Symbol as IMethodSymbol;
            string args = string.Join(", ", result.ArgumentList.Arguments);
            var input = _owner._input;
            // An unbounded limit is not checked at all, so those reads keep exactly their trusted call sites.
            bool strings = input.String != int.MaxValue, collections = input.Collection != int.MaxValue;
            if (strings && node.Expression is MemberAccessExpressionSyntax stableAccess && stableAccess.Name.Identifier.ValueText == "TryReadStableStringKeys" &&
                stableAccess.Expression.ToString() == "global::LuminPack.Code.LuminPackMarshal")
                // The bulk key kernel has no string limit; keep the per-element path.
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression).WithTriviaFrom(node);
            // A compressed payload's element count is not bounded by its input bytes; check its header before allocation.
            var arguments = result.ArgumentList.Arguments;
            if (collections && symbol is { IsStatic: true, Name: "Read" } && symbol.ContainingType?.ToDisplayString() == "LuminPack.Core.LuminPackReader" &&
                arguments.Count >= 5 && arguments[3].Expression.ToString() == "true" && arguments[4].Expression.ToString() == "true")
                return result.WithArgumentList(result.ArgumentList.WithArguments(arguments.Replace(arguments[0], arguments[0].WithExpression(
                    SyntaxFactory.ParseExpression(Policy + ".CheckCompressedCount(" + arguments[0].Expression + ", " + Literal(_owner._input.Collection) + ")")))));
            // Boolean loads from validated input storage.
            if (symbol != null && symbol.Name is "ReadUnaligned" or "Read" && symbol.ContainingType?.ToDisplayString() is
                "System.Runtime.CompilerServices.Unsafe" or "LuminPack.Core.LuminPackReader" && symbol.IsStatic)
            {
                if (symbol.ReturnType.SpecialType == SpecialType.System_Boolean && result.ArgumentList.Arguments.Count == 1 &&
                    result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                    return SyntaxFactory.ParseExpression(Policy + ".ReadBoolean(" + args + ")").WithTriviaFrom(node);
                if (symbol.Parameters.Length == 2 && symbol.Parameters[1].RefKind == RefKind.Out && symbol.Parameters[1].Type.SpecialType == SpecialType.System_Boolean)
                    return SyntaxFactory.ParseExpression(Policy + ".ReadBoolean(" + args + ")").WithTriviaFrom(node);
            }
            if (node.Expression is not MemberAccessExpressionSyntax access) return result;
            string readerName = access.Expression.ToString();
            if (readerName != _state && model.GetTypeInfo(access.Expression).Type?.ToDisplayString() != "LuminPack.Core.LuminPackReader") return result;
            string name = access.Name.Identifier.ValueText;
            string profile = access.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0 ? generic.TypeArgumentList.Arguments.Last().ToString() : _owner._options.TypeName;
            bool profileCall = access.Name is GenericNameSyntax profileName && profileName.TypeArgumentList.Arguments.Last().ToString().IndexOf("Options", StringComparison.Ordinal) >= 0;
            string collection = Literal(input.Collection), text = Literal(input.String);
            string call = null;
            if (name == "ReadStrings" && result.ArgumentList.Arguments.Count == 1)
                call = "ReadStrings<" + profile + ">(ref " + readerName + ", " + args + ", " + text + ")";
            else if (name == "ReadLength") call = "ReadLength(ref " + readerName + ", " + (args.Length == 0 ? "1" : args) + ", " + collection + ")";
            else if (name is "TryReadCollectionHead" or "PeekCollectionHeader")
                call = name + "(ref " + readerName + ", " + args + (result.ArgumentList.Arguments.Count == 1 ? ", 1" : "") + ", " + collection + ")";
            else if ((name == "ReadStringRecord" || name == "Read") && result.ArgumentList.Arguments.Count == 1 &&
                result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) &&
                (name == "ReadStringRecord" || model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.SpecialType == SpecialType.System_String))
                call = "ReadStringRecord<" + profile + ">(ref " + readerName + ", " + args + ", " + text + ")";
            else if ((name == "ReadStringRecord" || name == "Read") && (name == "ReadStringRecord" || symbol?.ReturnType.SpecialType == SpecialType.System_String ||
                profileCall && result.ArgumentList.Arguments.Count == 1 && result.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.None)))
                call = "ReadString<" + profile + ">(ref " + readerName + ", " + (args.Length == 0 ? "null" : args) + ", " + text + ")";
            else if (name == "Read" && access.Name is GenericNameSyntax scalar && scalar.TypeArgumentList.Arguments.Count == 1 && args.Length == 0)
            {
                string type = scalar.TypeArgumentList.Arguments[0].ToString();
                if (type is "bool" or "global::System.Boolean" or "System.Boolean") call = "ReadBoolean(ref " + readerName + ")";
                else if (type == "byte" && _owner._members >= 0 && !_markerSeen)
                {
                    // The object template reads its member marker first.
                    _markerSeen = true;
                    call = "ReadMarker(ref " + readerName + ", " + Literal(_owner._members) + ")";
                }
            }
            if (call != null && (!strings && call.StartsWith("ReadString", StringComparison.Ordinal) ||
                !collections && (call.StartsWith("ReadLength(", StringComparison.Ordinal) || call.StartsWith("TryReadCollectionHead(", StringComparison.Ordinal) ||
                    call.StartsWith("PeekCollectionHeader(", StringComparison.Ordinal))))
                call = null;
            return call == null ? result : SyntaxFactory.ParseExpression(Policy + "." + call).WithTriviaFrom(node);
        }
    }
}
