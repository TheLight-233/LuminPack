using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the top-first staging used by an ordinary stack's JSON reader.</summary>
internal static class StackJsonStaging
{
    /// <summary>Stages decoded values until the complete array can populate the result in reverse order.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="sequence">The stack type spelling.</param>
    /// <param name="element">The element type spelling.</param>
    /// <param name="owner">The existing element reader.</param>
    /// <param name="reader">The JSON reader type name.</param>
    /// <param name="clear">Whether the returned pool array can contain references.</param>
    /// <param name="bulkRestore">Whether the runtime can restore the final stack storage.</param>
    internal static void Emit(CodeWriter w, string sequence, string element, string owner, string reader, bool clear, bool bulkRestore)
    {
        string pool = "global::System.Buffers.ArrayPool<" + element + ">.Shared";
        string release = pool + ".Return(buffer, clearArray: " + (clear ? "true" : "false") + ");";
        const string marshal = "global::LuminPack.Code.LuminPackMarshal";
        const string memory = "global::System.Runtime.CompilerServices.Unsafe";
        w.Line("var buffer = " + pool + ".Rent(16);");
        w.Line("int count = 0;");
        w.Line("try");
        w.Open();
        w.Line("while (reader.Read<TLuminProfile>())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd)");
        w.Indented(() => w.Line("break;"));
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd)");
        w.Indented(() => w.Line("continue;"));
        w.Line("if (count == buffer.Length)");
        w.Open();
        w.Line("var larger = " + pool + ".Rent(checked(count * 2));");
        w.Line("global::System.Array.Copy(buffer, larger, count);");
        w.Line(release);
        w.Line("buffer = larger;");
        w.Close();
        w.Line(element + " item = default!;");
        w.Line(owner + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line(memory + ".Add(ref " + marshal + ".GetNotNullArrayReference(buffer), (nint)(uint)count++) = item;");
        w.Close();
        w.Line("value = new " + sequence + "(count);");
        if (bulkRestore)
            w.Line("global::LuminPack.Code.LuminPackCollectionAccess.RestoreStackFromTop(value, " + marshal + ".CreateReadOnlySpan(ref " + marshal + ".GetNotNullArrayReference(buffer), count));");
        else
        {
            w.Line("if (count != 0)");
            w.Open();
            w.Line("ref var first = ref " + marshal + ".GetNotNullArrayReference(buffer);");
            w.Line("do");
            w.Open();
            w.Line("value.Push(" + memory + ".Add(ref first, (nint)(uint)--count));");
            w.Close();
            w.Line("while (count != 0);");
            w.Close();
        }
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line(release);
        w.Close();
    }
}
