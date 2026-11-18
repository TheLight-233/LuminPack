using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Emits static element operations without specializing the containing collection for its element type.</summary>
internal static class FormatterElementEmitter
{
    private const string NullableStorage = "global::LuminPack.Code.LuminPackNullableRectangle";

    /// <summary>Writes a collection slot using its chosen native nullable or standalone element format.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The shared formatter publication facts.</param>
    /// <param name="model">The normalized element formatter.</param>
    /// <param name="expression">The element value expression.</param>
    /// <param name="state">The operation-state variable name.</param>
    /// <param name="profile">The compiled profile type parameter name.</param>
    /// <param name="checkBuffer">Whether the writer can still be beyond its growth threshold.</param>
    internal static void WriteCollectionElement(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression,
        string state = "writer", string profile = "TLuminProfile", bool checkBuffer = true)
    {
        if (!model.NativeNullableElement)
        {
            Write(w, set, model, expression, state, profile, checkBuffer);
            return;
        }

        if (checkBuffer)
            w.Line(state + ".CheckBuffer();");
        w.Line(state + ".EnsureAdditionalCapacity(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">());");
        w.Line(state + ".Advance(global::LuminPack.Core.LuminPackWriter.Write<" + model.Type + ">(ref " + state + ".Cursor, " + expression + "));");
    }

    /// <summary>Reads a collection slot using its chosen native nullable or standalone element format.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="set">The shared formatter publication facts.</param>
    /// <param name="model">The normalized element formatter.</param>
    /// <param name="expression">The addressable destination expression.</param>
    /// <param name="state">The operation-state variable name.</param>
    /// <param name="profile">The compiled profile type parameter name.</param>
    /// <param name="fresh">Whether the destination is a newly initialized default local.</param>
    internal static void ReadCollectionElement(CodeWriter w, SharedFormatterSet set, FormatterModel model, string expression,
        string state = "reader", string profile = "TLuminProfile", bool fresh = false)
    {
        if (model.NativeNullableElement)
            w.Line(NullableStorage + ".ReadNative<" + model.Arguments[0].Type + ">(ref " + state + ", ref " + expression + ");");
        else
            Read(w, set, model, expression, state, profile, fresh);
    }

    /// <summary>Validates the complete native nullable range before a direct collection transfer.</summary>
    /// <param name="w">The generated source writer.</param>
    /// <param name="model">The native nullable element contract.</param>
    /// <param name="count">The validated element-count expression.</param>
    /// <param name="state">The operation-state variable name.</param>
    internal static void ValidateNativeNullableCollection(CodeWriter w, FormatterModel model, string count, string state) =>
        w.Line(NullableStorage + ".ValidateNative<" + model.Arguments[0].Type + ">(ref " + state + ", " + count + ");");

    /// <summary>Returns the width lower bound of an individually written collection slot.</summary>
    /// <param name="model">The normalized element formatter.</param>
    /// <returns>The minimum readable byte count per slot.</returns>
    internal static string CollectionMinimum(FormatterModel model) => model.NativeNullableElement
        ? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">()" : Minimum(model);

    /// <summary>Finds the concrete fixed class body a contiguous array can write inside a proven window.</summary>
    /// <param name="set">The shared formatter facts and target.</param>
    /// <param name="model">The exact array element contract.</param>
    /// <returns>The fixed record model or null for an outlined element operation.</returns>
    internal static PayloadRenderModel? FixedObject(SharedFormatterSet set, FormatterModel model) =>
        model is { OrdinaryObject: true, Payload: RecordModel record } && set.Options?.Net8 == true &&
        RenderModelFactory.Create(record, set.Options) is { } rendered && BinaryEmitter.CanReserveFixedObject(rendered)
            ? rendered : null;

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

        // An equivalent fixed payload reader can share the slot's single checked object marker read.
        if (model.OrdinaryObject && !fresh)
        {
            int count = FixedSlotMemberCount(set, model);
            if (count != 0)
            {
                w.Line("if (global::LuminPack.Security.LuminPackInputPolicy.ReadNullMarker(ref " + state + ", " +
                    count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "))");
                w.Indented(() => w.Line(expression + " = default!;"));
                w.Line("else");
                w.Indented(() => w.Line(FixedSlotPayloadOwner(model.Type) + "(ref " + state + ", ref " + expression + "!);"));
                return;
            }
            if (model.Definition == "$strategy")
            {
                // The generated comment keeps the ordinary and nonordinary shared owner shapes distinct.
                w.Line("// The concrete element strategy clears the slot before reading a null object.");
            }
            else
            {
                string existing = CanSkipEmptySlotPeek(model) ? expression + " is not null && " : string.Empty;
                w.Line("if (" + existing + state + ".PeekIsNullObject())");
                w.Indented(() => w.Line(expression + " = default!;"));
            }
        }

        ForwardedElementRead.Emit(w, model.Type, state, expression, Owner(set, model) + ".Read<" + profile + ">(ref " + state + ", ref " + expression + ");");
    }

    /// <summary>Finds the checked marker for a slot whose fixed payload read preserves the object read's storage checks.</summary>
    /// <param name="set">The shared formatter facts.</param>
    /// <param name="model">The ordinary object element model.</param>
    /// <returns>The expected member count, or zero when the ordinary reader is required.</returns>
    internal static int FixedSlotMemberCount(SharedFormatterSet set, FormatterModel model) =>
        model.Payload is RecordModel { FixedUnionCase: true } record && set.Options?.Net8 == true &&
        RenderModelFactory.Create(record, set.Options) is { } rendered && BinaryEmitter.HasEquivalentFixedPayloadRead(rendered)
            ? rendered.Members.Length : 0;

    /// <summary>Checks whether a null destination needs no pre-emptive clearing before its own checked record read.</summary>
    /// <param name="model">The ordinary element reader contract.</param>
    /// <returns>Whether a callback-free positional reader can skip the peek for an empty slot.</returns>
    internal static bool CanSkipEmptySlotPeek(FormatterModel model) => model.Payload is RecordModel
    {
        Callbacks: null, Recursive: false, Rent: null, Constructor: null, SequenceBase: null, Versioned: false, Rejection: null,
    };

    /// <summary>Names the marker-free fixed payload operation after a checked object marker was consumed.</summary>
    /// <param name="type">The exact ordinary element type.</param>
    /// <returns>The existing payload reader.</returns>
    internal static string FixedSlotPayloadOwner(string type) => "global::LuminPack.LuminPackSerializer.LuminRoots_" +
        ReadableTypeName.Of(GeneratedNames.PayloadKey(type)) + ".RootReadUnion";

    /// <summary>Returns a minimum standalone record width without assuming that generic unmanaged arguments use raw storage.</summary>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Minimum(FormatterModel model) => model.Payload is UnionModel || model.Definition == FormatterDefinitions.GenericUnion ? "4" : model.Raw ? "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">()" : model.Layout != null && (model.Definition is GenericIdentity.ValueTuple or FormatterDefinitions.PayloadReference ||
        GenericFormatterReuse.HasPackedMinimum(model)) ? model.Layout.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture) : "1";
}
