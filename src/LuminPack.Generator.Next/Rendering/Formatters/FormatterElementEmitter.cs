using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Emits static element operations without specializing the containing collection for its element type.</summary>
internal static class FormatterElementEmitter
{
    /// <summary>Finds an ordinary positional element with public fixed, string or raw-list fields and no callbacks.</summary>
    /// <param name="set">The shared formatter publication facts.</param>
    /// <param name="model">The closed element formatter contract.</param>
    /// <returns>The bounded collection element model when one of its fields is a raw list.</returns>
    internal static PayloadRenderModel? ReservableCollectionElement(SharedFormatterSet set, FormatterModel? model)
    {
        if (model?.Payload is not RecordModel { Derived: false, Versioned: false, Rejection: null } record ||
            set.Options == null || !set.Options.Net8 || RenderModelFactory.Create(record, set.Options) is not { } element ||
            element.HasCallbacks || element.Bounded != null || element.DeclaredProfile != null || element.Mirror != null ||
            element.TypeParameters.Length != 0)
            return null;
        return element.Members.Any(RawList) && element.Members.All(member => !member.Volatile && !member.Mirrored &&
            !member.Property && member.ScopeProfile == null && (member.IsFixed || member.Kind == RenderMemberKind.Text || RawList(member)))
            ? element : null;

        bool RawList(RenderMember member) => member.Nested == null && !member.FormatterCall &&
            (member.Kind == RenderMemberKind.List && GenericIdentity.IsRaw(member.Scalar) ||
                member.Kind == RenderMemberKind.Special && !member.Formatter.EndsWith(RenderMember.CompressedOwner.ToString(), StringComparison.Ordinal) &&
                set.FindFormatter(member.TypeName) is { Definition: "System.Collections.Generic.List", Arguments.Length: 1 } list &&
                list.Arguments[0] is { Raw: true, Layout: null } item && GenericIdentity.IsRaw(item.Type));
    }

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
    /// <param name = "checkBuffer">Whether the writer can still be beyond its growth threshold.</param>
    internal static void Write(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression, string state = "writer", string profile = "TLuminProfile", bool checkBuffer = true)
    {
        if (checkBuffer)
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
    /// <param name = "fresh">Whether the destination is a newly initialized default local.</param>
    internal static void Read(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression, string state = "reader",
        string profile = "TLuminProfile", bool fresh = false)
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

        // Clear a reused slot on a null marker before the element's own read runs its deserializing callbacks.
        if (model.OrdinaryObject && !fresh)
        {
            if (model.Definition == "$strategy")
            {
                // The generated comment keeps the ordinary and nonordinary shared owner shapes distinct.
                w.Line("// The concrete element strategy clears the slot before reading a null object.");
            }
            else
            {
                w.Line("if (" + state + ".PeekIsNullObject())");
                w.Indented(() => w.Line(expression + " = default!;"));
            }
        }

        ForwardedElementRead.Emit(w, model.Type, state, expression, Owner(set, model) + ".Read<" + profile + ">(ref " + state + ", ref " + expression + ");");
    }

    /// <summary>Returns a minimum standalone record width without assuming that generic unmanaged arguments use raw storage.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Minimum(FormatterModel model) => model.Raw ? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">()" : model.Layout != null && (model.Definition is GenericIdentity.ValueTuple or FormatterDefinitions.PayloadReference ||
        GenericFormatterReuse.HasPackedMinimum(model)) ? model.Layout.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
}
