using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Retains the ordinary Union write and derives its reserved address kernel from the same field contract.</summary>
internal static class VariableUnionPayloadProjection
{
    /// <summary>Preserves the original state body and appends a separately reserved address-only kernel.</summary>
    /// <param name="method">The original Union payload entry.</param>
    /// <param name="members">The serializer declarations.</param>
    /// <param name="plan">The certified symbol-free case.</param>
    /// <param name="data">The canonical storage metadata used for adjacent-field proofs.</param>
    internal static void Append(MethodDeclarationSyntax method, List<MemberDeclarationSyntax> members,
        VariableUnionPayloadPlan.Case plan, LuminDataInfo data)
    {
        const string writerType = "global::LuminPack.Core.LuminPackWriter";
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        const string marshal = "global::LuminPack.Code.LuminPackMarshal";
        const string serializer = "global::LuminPack.LuminPackSerializer";
        var body = new StringBuilder();
        var bounds = new List<string>();
        for (int index = 0; index < plan.Fields.Count; index++)
        {
            var field = plan.Fields[index];
            string captured = "__partition" + index;
            if (field.Kind == 0) { bounds.Add(field.Bound("input." + field.Name, "__LuminProfile")); continue; }
            body.AppendLine("var " + captured + " = input." + field.Name + ";");
            if (field.Kind == 1)
            {
                bounds.Add("writer.RecordBound" + Profile(field) + "(" + captured + ")");
                continue;
            }
            string span = captured + "Items";
            body.AppendLine("global::System.ReadOnlySpan<" + field.Element + "> " + span + " = " + captured +
                " is null ? default : " + (field.Kind is 3 or 5 ? marshal + ".GetListSpan(" + captured + ")" : captured + ".AsSpan()") + ";");
            bounds.Add("4L + (long)" + span + ".Length * (" + (field.ObjectWidth ?? unsafeType + ".SizeOf<" + field.Element + ">()") + ")");
        }
        body.AppendLine("long __partitionBound = 0L + " + string.Join(" + ", bounds.DefaultIfEmpty("0L")) + ";");
        body.AppendLine("if (__partitionBound < 0 || __partitionBound > reservedLength) global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(\"A Union payload changed after partition sizing.\");");
        body.AppendLine("ref byte __cursor = ref writer.GetSpanReference(initialIndex);");
        for (int index = 0; index < plan.Fields.Count; index++)
        {
            var field = plan.Fields[index];
            string captured = "__partition" + index;
            if (field.Kind == 0)
            {
                int last = index;
                while (last + 1 < plan.Fields.Count && plan.Fields[last + 1].Kind == 0) last++;
                LuminPackCodeGenerator.AppendReservedFixedGroup(body, data.fields, index, last, item => "input." + item.Identifier, "");
                index = last;
            }
            else if (field.Kind == 1)
            {
                body.AppendLine("__cursor = ref writer.WriteReservedPartitionRecordString" + Profile(field) + "(ref __cursor, " + captured + ");");
            }
            else if (field.Kind is 2 or 3)
            {
                body.AppendLine("__cursor = ref " + unsafeType + ".Add(ref __cursor, " + writerType + ".WriteReservedSpan<" + field.Element + ">(ref __cursor, " + captured + " is null, " + captured + "Items));");
            }
            else
            {
                body.AppendLine("__cursor = ref " + writerType + ".WriteReserved(ref __cursor, " + captured + " is null ? -1 : " + captured + "Items.Length);");
                body.AppendLine("writer.CommitRecords(ref __cursor);");
                body.AppendLine("foreach (var __partitionItem" + index + " in " + captured + "Items) { writer.CheckBuffer(); " + serializer + ".Serialize(ref writer, in __partitionItem" + index + "); }");
                body.AppendLine("__cursor = ref writer.Cursor;");
            }
        }
        body.AppendLine("return checked((int)" + unsafeType + ".ByteOffset(ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(writer.Buffer), ref __cursor));");
        members.Add(method);
        var at = method.WithIdentifier(SyntaxFactory.Identifier("SerializeUnionPartitionAt"))
            .WithReturnType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))
            .WithBody(SyntaxFactory.ParseStatement("{" + body + "}") as BlockSyntax)
            .WithParameterList(method.ParameterList.AddParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("initialIndex")).WithType(SyntaxFactory.ParseTypeName("int")),
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("reservedLength")).WithType(SyntaxFactory.ParseTypeName("int"))));
        members.Add(at);
    }

    /// <summary>Selects an explicit field profile without preventing ambient profile propagation.</summary>
    /// <param name="field">The certified member.</param>
    /// <returns>A declaration-owned generic argument, or an empty argument list.</returns>
    private static string Profile(VariableUnionPayloadPlan.Field field) => field.Profile == null ? "" : "<" + field.Profile + ">";

    /// <summary>Applies the same raw UTF-16 policy to normal case writes and their exact-size evaluator.</summary>
    internal sealed class Strings : CSharpSyntaxRewriter
    {
        /// <summary>Rebinds only binary string kernels, leaving JSON and reader operations untouched.</summary>
        /// <param name="node">A generated invocation within an eligible case.</param>
        /// <returns>The corresponding raw-policy kernel or unchanged call.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (rewritten.Expression is not MemberAccessExpressionSyntax access) return rewritten;
            string replacement = access.Name.Identifier.ValueText switch
            {
                "WriteRecordString" => "WritePartitionRecordString",
                "WriteReservedRecordString" => "WriteReservedPartitionRecordString",
                "WriteReservedUtf16Record" => "WriteReservedPartitionUtf16Record",
                "GetStringLength" => "GetPartitionStringLength",
                _ => null
            };
            return replacement == null ? rewritten : rewritten.WithExpression(access.WithName(access.Name.WithIdentifier(SyntaxFactory.Identifier(replacement))));
        }
    }
}
