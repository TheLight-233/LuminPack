using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Specializes generated state calls using stateless compile-time option profiles.</summary>
internal sealed class CompiledOptionProjection : CSharpSyntaxRewriter
{
    private readonly CompiledOptions _options;
    private readonly string _state;
    private readonly HashSet<string> _methods;
    private readonly bool _replaceExisting;
    private readonly string _profileType;
    internal static readonly HashSet<string> WriterMethods = Names("GetStringLength GetStringWriteLength GetStringCapacityScale StringRecordLength WriteNullStringHeader WriteReservedNullStringHeader WriteReservedString WriteString WriteStringRecordTokenHeader WriteUtf16WithToken WriteUtf8WithToken");
    internal static readonly HashSet<string> ReaderMethods = Names("PeekIsNullString ReadString ReadStringAndAdvance ReadStringLength ReadStringListAndAdvance StringRecordLength TryReadStringHead");
    internal static readonly HashSet<string> EvaluatorMethods = Names("GetStringLength StringRecordLength");
    internal static readonly HashSet<string> JsonWriterMethods = Names("ReserveGeneratedIntegerPrefix WriteArrayEnd WriteArrayStart WriteBool WriteByte WriteByteRaw WriteChar WriteCommaIfNeeded WriteDateTime WriteDateTimeOffset WriteDecimal WriteDouble WriteFloat WriteFormattedValue WriteGeneratedInteger WriteGuid WriteInt WriteLong WriteNull WriteObjectEnd WriteObjectStart WritePropertyName WriteSByte WriteShort WriteString WriteTimeSpan WriteUInt WriteULong WriteUShort");
    internal static readonly HashSet<string> JsonReaderMethods = Names("ConsumeArrayEnd EnsureEndOfDocument Read ReadBoolean ReadByte ReadChar ReadDecimal ReadDouble ReadFloat ReadInt ReadLong ReadNextFloatValue ReadNull ReadSByte ReadShort ReadString ReadStringChoice ReadStringUtf8 ReadStringUtf16 ReadUInt ReadULong ReadUShort Skip SkipWhitespace TryConsumeArrayStart TryConsumeObjectStart TryReadPropertyName");

    /// <summary>Selects one state's profile-dependent runtime kernels.</summary>
    /// <param name="options">The complete immutable option profile.</param>
    /// <param name="state">The original generated state receiver.</param>
    /// <param name="methods">The kernels whose generic overloads consume that profile.</param>
    /// <param name="replaceExisting">Whether the root document must override nested document encoding.</param>
    /// <param name="profileType">An inherited member profile, or null for the containing declaration.</param>
    internal CompiledOptionProjection(CompiledOptions options, string state, HashSet<string> methods, bool replaceExisting = false, string profileType = null)
    { _options = options; _state = state; _methods = methods; _replaceExisting = replaceExisting; _profileType = profileType ?? options.TypeName; }

    /// <summary>Creates a fixed set of runtime kernel names.</summary>
    /// <param name="names">Space-separated identifiers.</param>
    /// <returns>An ordinal method-name set.</returns>
    private static HashSet<string> Names(string names) => new HashSet<string>(names.Split(' '), StringComparer.Ordinal);

    /// <summary>Turns constant header queries into literals and selects a specialized generic kernel.</summary>
    /// <param name="node">An emitted state call, never user-authored source.</param>
    /// <returns>The selected runtime operation or unchanged invocation.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is not MemberAccessExpressionSyntax access || !IsReceiver(access.Expression) ||
            !_methods.Contains(access.Name.Identifier.ValueText)) return base.VisitInvocationExpression(node);
        if (access.Name is GenericNameSyntax && !_replaceExisting) return base.VisitInvocationExpression(node);
        var arguments = access.Name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments : default;
        if (access.Name is GenericNameSyntax && _replaceExisting) arguments = arguments.RemoveAt(0);
        var name = SyntaxFactory.GenericName(access.Name.Identifier,
            SyntaxFactory.TypeArgumentList(arguments.Insert(0, SyntaxFactory.ParseTypeName(_profileType))));
        return base.VisitInvocationExpression(node.WithExpression(access.WithName(name)));
    }

    /// <summary>Replaces state-bound option metadata with declaration-time constants.</summary>
    /// <param name="node">A generated state property expression.</param>
    /// <returns>The compile-time value or unchanged expression.</returns>
    public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (IsReceiver(node.Expression))
        {
            if (node.Name.Identifier.ValueText == "StringCapacityScale")
                return SyntaxFactory.ParseExpression(node.Expression + ".GetStringCapacityScale<" + _profileType + ">()").WithTriviaFrom(node);
            if (node.Name.Identifier.ValueText == "Option")
                return SyntaxFactory.ParseExpression("default(" + _profileType + ")").WithTriviaFrom(node);
        }
        return base.VisitMemberAccessExpression(node);
    }

    /// <summary>Includes compiler-proven independent staging states without matching source field spellings.</summary>
    /// <param name="expression">A generated state receiver.</param>
    /// <returns>Whether this receiver uses the selected operation's state family.</returns>
    private bool IsReceiver(ExpressionSyntax expression)
    {
        if (expression.ToString() == _state) return true;
        if (expression is not IdentifierNameSyntax name) return false;
        string type = name.Identifier.GetAnnotations(OperationTemplateRewriter.StateAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
        return _methods == WriterMethods && type == "LuminPack.Core.LuminPackWriter" ||
            _methods == ReaderMethods && type == "LuminPack.Core.LuminPackReader" ||
            _methods == EvaluatorMethods && type == "LuminPack.Core.LuminPackEvaluator" ||
            _methods == JsonWriterMethods && type == "LuminPack.Core.LuminPackJsonWriter" ||
            _methods == JsonReaderMethods && type == "LuminPack.Core.LuminPackJsonReader";
    }

    /// <summary>Specializes the complete binary body after nested operations have preserved their own profiles.</summary>
    /// <param name="body">The fully expanded generated operation.</param>
    /// <param name="options">The containing declaration's profile.</param>
    /// <param name="state">The actual writer, reader or evaluator expression.</param>
    /// <param name="type">The fully qualified state type.</param>
    /// <param name="profileType">An explicit member profile, if present.</param>
    /// <returns>The same body with compile-time string operations.</returns>
    internal static BlockSyntax Apply(BlockSyntax body, CompiledOptions options, string state, string type, string profileType = null)
    {
        HashSet<string> methods = type.EndsWith("LuminPackWriter", StringComparison.Ordinal) ? WriterMethods :
            type.EndsWith("LuminPackReader", StringComparison.Ordinal) ? ReaderMethods :
            type.EndsWith("LuminPackEvaluator", StringComparison.Ordinal) ? EvaluatorMethods :
            type.EndsWith("LuminPackJsonWriter", StringComparison.Ordinal) ? JsonWriterMethods :
            type.EndsWith("LuminPackJsonReader", StringComparison.Ordinal) ? JsonReaderMethods : null;
        return methods == null ? body : (BlockSyntax)new CompiledOptionProjection(options, state, methods, profileType: profileType).Visit(body);
    }
}

/// <summary>Projects one already-emitted field body before its enclosing declaration selects defaults.</summary>
internal sealed class CompiledOptionFieldScope : IDisposable
{
    private readonly StringBuilder _builder;
    private readonly CompiledOptions _options;
    private readonly int _start;
    private readonly string _payloadType;

    /// <summary>Captures a field's output position and its declared profile.</summary>
    /// <param name="builder">The existing source builder.</param>
    /// <param name="options">The member's profile, or null for synthetic element metadata.</param>
    /// <param name="payloadType">The field type when its formatter call needs a compile-time scope marker.</param>
    internal CompiledOptionFieldScope(StringBuilder builder, CompiledOptions options, string payloadType = null)
    { _builder = builder; _options = options; _start = builder.Length; _payloadType = payloadType; }

    /// <summary>Specializes only emitted state calls; nested fields already specialized retain their profiles.</summary>
    public void Dispose()
    {
        if (_options?.IsDeclared != true || _builder.Length == _start) return;
        string source = _builder.ToString(_start, _builder.Length - _start);
        bool kernels = CompiledOptionProjection.WriterMethods.Concat(CompiledOptionProjection.ReaderMethods).Concat(CompiledOptionProjection.EvaluatorMethods)
            .Any(name => source.IndexOf("." + name + "(", StringComparison.Ordinal) >= 0) ||
            source.IndexOf(".StringCapacityScale", StringComparison.Ordinal) >= 0 || source.IndexOf(".Option.", StringComparison.Ordinal) >= 0;
        bool boundary = _payloadType != null && (source.IndexOf(".WriteValue(", StringComparison.Ordinal) >= 0 ||
            source.IndexOf(".ReadValue(", StringComparison.Ordinal) >= 0 || source.IndexOf(".ReadFreshValue(", StringComparison.Ordinal) >= 0 ||
            source.IndexOf(".CalculateOffset", StringComparison.Ordinal) >= 0);
        if (!kernels && !boundary) return;
        var body = SyntaxFactory.ParseStatement("{" + source + "}", options: new CSharpParseOptions(LanguageVersion.Preview));
        var projected = new CompiledOptionProjection(_options, "writer", CompiledOptionProjection.WriterMethods, profileType: _options.ScopeTypeName).Visit(body);
        projected = new CompiledOptionProjection(_options, "reader", CompiledOptionProjection.ReaderMethods, profileType: _options.ScopeTypeName).Visit(projected);
        projected = new CompiledOptionProjection(_options, "evaluator", CompiledOptionProjection.EvaluatorMethods, profileType: _options.ScopeTypeName).Visit(projected);
        if (_payloadType != null && _options.IsDeclared) projected = new MemberCalls(_options.ScopeTypeName, _payloadType).Visit(projected);
        var statements = ((BlockSyntax)projected).Statements;
        _builder.Length = _start;
        foreach (var statement in statements) _builder.Append(statement.ToFullString());
    }

    /// <summary>Marks one member's existing formatter boundary without adding an execution-time configuration change.</summary>
    private sealed class MemberCalls : CSharpSyntaxRewriter
    {
        private readonly string _profile;
        private readonly string _payload;
        /// <summary>Captures only immutable generated type spellings.</summary>
        /// <param name="profile">The member's stateless profile type.</param>
        /// <param name="payload">The original member type.</param>
        internal MemberCalls(string profile, string payload) { _profile = profile; _payload = payload; }
        /// <summary>Attaches a compiler-only profile identity to the original value alias.</summary>
        /// <param name="node">An emitted formatter invocation.</param>
        /// <returns>The same invocation with a temporary identity around its original argument.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is not MemberAccessExpressionSyntax access || access.Expression is not IdentifierNameSyntax receiver ||
                receiver.Identifier.ValueText is not ("writer" or "reader" or "evaluator") ||
                access.Name.Identifier.ValueText is not ("WriteValue" or "ReadValue" or "ReadFreshValue" or "CalculateOffset") || node.ArgumentList.Arguments.Count != 1)
                return base.VisitInvocationExpression(node);
            var argument = node.ArgumentList.Arguments[0];
            if (argument.Expression is InvocationExpressionSyntax previous && previous.Expression.ToString().Contains("LuminPackOptionScope")) return node;
            bool write = receiver.Identifier.ValueText == "writer";
            var marker = SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackOptionScope." + (write ? "Write" : "Read") + "<" + _profile + ", " + _payload + ">(" +
                (write ? "" : "ref ") + argument.Expression + ")");
            return node.WithArgumentList(node.ArgumentList.WithArguments(SyntaxFactory.SingletonSeparatedList(argument.WithExpression(marker))));
        }
    }
}
