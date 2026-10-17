using System.Text;


namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>Provides collection emitter helper functionality.</summary>
internal static class CollectionEmitterHelper
{
    // Underlying-list fast path emitted with static element calls.
    /// <summary>Appends binary writing statements for collection helper values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void AppendStaticListSerialize(StringBuilder sb)
    {
        sb.AppendLine("                var span = global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, list.Count);");
        sb.AppendLine("                writer.WriteCollectionHeader(ref index, span.Length);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                foreach (ref var item in span)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.CheckBuffer();");
        sb.AppendLine("                    writer.WriteValue(in item!);");
        sb.AppendLine("                }");
        sb.AppendLine("                writer.CheckBuffer();");
    }
}
