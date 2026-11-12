using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Emits static element operations without specializing the containing collection for its element type.</summary>
internal static class FormatterElementEmitter
{
    /// <summary>Names an element's concrete formatter owner.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Owner(SharedFormatterSet set, FormatterModel model) => GenericOwnerFile.Owner(set.Extension!, model.Type);
    /// <summary>Writes an element through its established scalar or static formatter operation.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "expression">The addressable value expression.</param>
    /// <param name = "state">The operation-state variable name.</param>
    /// <param name = "profile">The compiled profile type parameter name.</param>
    internal static void Write(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression, string state = "writer", string profile = "TLuminProfile")
    {
        w.Line(state + ".CheckBuffer();");
        // A packable payload is copied raw only when its own formatter is a plain storage copy, matching the owner read.
        if (model.Raw && (model.Payload == null || model.RawStorage))
        {
            w.Line(state + ".EnsureAdditionalCapacity(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">());");
            w.Line(state + ".Write(in " + expression + ");");
        }
        else if (model.Type == "string")
            w.Line(state + ".Write<" + profile + ">(" + expression + ");");
        else
            w.Line(Owner(set, model) + ".Write<" + profile + ">(ref " + state + ", in " + expression + ");");
    }

    /// <summary>Reads one element while retaining ordinary-object null replacement and Boolean-domain checks.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "expression">The addressable value expression.</param>
    /// <param name = "state">The operation-state variable name.</param>
    /// <param name = "profile">The compiled profile type parameter name.</param>
    internal static void Read(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression, string state = "reader", string profile = "TLuminProfile")
    {
        if (model.Type == "bool")
        {
            w.Line(expression + " = global::LuminPack.Security.LuminPackInputPolicy.ReadBoolean(ref " + state + ");");
            return;
        }

        // A shared owner's unmanaged argument is read as its storage, as the closed scalar owner reads it.
        if (GenericFormatterReuse.IsUnmanagedPlaceholder(model.Type))
        {
            w.Line(state + ".Read(ref " + expression + ");");
            return;
        }

        // As in the original element read, a null marker clears the slot and the element's own read still runs: it raises the
        // deserializing callbacks before it consumes the marker.
        if (model.OrdinaryObject)
        {
            w.Line("if (" + state + ".PeekIsNullObject())");
            w.Indented(() => w.Line(expression + " = default!;"));
        }

        ForwardedElementRead.Emit(w, model.Type, state, expression, Owner(set, model) + ".Read<" + profile + ">(ref " + state + ", ref " + expression + ");");
    }

    /// <summary>Returns a minimum standalone record width without assuming that generic unmanaged arguments use raw storage.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Minimum(FormatterModel model) => model.Raw ? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">()" : model.Layout != null && model.Definition is GenericIdentity.ValueTuple or FormatterDefinitions.PayloadReference ? model.Layout.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
}
