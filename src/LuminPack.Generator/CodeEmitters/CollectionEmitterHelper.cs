using System.Text;


namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>Provides collection emitter helper functionality.</summary>
internal static class CollectionEmitterHelper
{
    /// <summary>Writes the backing list of an ObjectModel wrapper as one contiguous record.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="fieldData">The wrapper field; its first type argument is the element.</param>
    /// <remarks>Raw elements are copied as one block; the bytes equal the wrapper's own enumeration.</remarks>
    public static void AppendStaticListSerialize(StringBuilder sb, LuminPack.Code.LuminLocalFieldData fieldData)
    {
        CollectionEmitterKit.AppendSpanWrite(sb, EmitterTypeTraits.GetFirstTypeArgument(fieldData), CodeEmitterRegistry.GetFirstGeneric(fieldData.TypeName),
            "global::LuminPack.Code.LuminPackMarshal.GetListSpan(list, list.Count)", "                ");
    }
}
