using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits the staged JSON read used by callback-free concurrent stacks.</summary>
internal static class ConcurrentStackJsonReader
{
    /// <summary>Stages decoded elements and publishes the same prefix on read failure.</summary>
    /// <param name="w">The source writer.</param>
    /// <param name="stack">The concrete stack spelling.</param>
    /// <param name="element">The element type spelling.</param>
    /// <param name="owner">The JSON element formatter.</param>
    /// <param name="reader">The JSON reader type name.</param>
    /// <param name="clear">Whether the returned array can hold references.</param>
    internal static void Emit(CodeWriter w, string stack, string element, string owner, string reader, bool clear)
    {
        string pool = "global::System.Buffers.ArrayPool<" + element + ">.Shared";
        string returnItems = pool + ".Return(items, " + (clear ? "true" : "false") + ");";
        const string publish = "global::LuminPack.Code.LuminPackCollectionAccess.PublishConcurrentStack";
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + stack + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (!reader.Read<TLuminProfile>() || reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd)");
        w.Open();
        // A concurrent push between the two original clear calls must still be cleared on success.
        w.Line("value.Clear();");
        w.Line("return;");
        w.Close();
        w.Blank();
        w.Line("var items = " + pool + ".Rent(16);");
        w.Line("int count = 0;");
        w.Line("try");
        w.Open();
        w.Line("try");
        w.Open();
        w.Line("do");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ArrayEnd)");
        w.Indented(() => w.Line("break;"));
        w.Line("if (reader.CurrentTokenType == " + reader + ".JsonTokenType.ObjectEnd)");
        w.Indented(() => w.Line("continue;"));
        w.Line(element + " item = default!;");
        w.Line(owner + ".Read<TLuminProfile>(ref reader, ref item);");
        w.Line("if (count == items.Length)");
        w.Open();
        w.Line("var larger = " + pool + ".Rent(checked(count * 2));");
        w.Line("global::System.Array.Copy(items, larger, count);");
        w.Line(returnItems);
        w.Line("items = larger;");
        w.Close();
        w.Line("items[count++] = item;");
        w.Close();
        w.Line("while (reader.Read<TLuminProfile>());");
        w.Close();
        w.Line("catch (global::System.Exception error)");
        w.Open();
        w.Line(publish + "(value, items, count, false);");
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.Rethrow(error);");
        w.Close();
        w.Line(publish + "(value, items, count, true);");
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line(returnItems);
        w.Close();
    }
}
