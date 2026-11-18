using System;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Selects each formatter's registered default implementation from its normalized closed contract.</summary>
internal static class DefaultFormatterOwnerFile
{
    /// <summary>Renders a contract after recursively collecting its dependencies.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        if (model.Definition == FormatterDefinitions.Scalar)
            return ScalarOwnerFile.Render(set, model.Type + (model.Underlying == null ? "" : "=" + model.Underlying), operations);
        if (model.Definition == FormatterDefinitions.FrameworkScalar)
            return FrameworkScalar(set, model, operations);
        if (model.Definition == FormatterDefinitions.Object)
            return Object(set, model, operations);
        if (model.Definition == FormatterDefinitions.Registered)
            return Registered(set, model, operations);
        if (model.Definition == FormatterDefinitions.GenericUnion)
            return GenericUnion(set, model, operations);
        if (model.Definition == FormatterDefinitions.CompilerUnion)
            return CompilerUnionOwnerFile.Render(set, model, operations);
        if (model.Definition == FormatterDefinitions.Array)
        {
            if (model.Rank > 1)
                return RectangularArrayOwnerFile.Render(set, model, operations);
            FormatterModel element = model.Arguments[0];
            if (element.RawCollection)
                return ArrayScalarOwnerFile.Render(set, SequenceOwnerFact.ForScalar(FormatterRegistry.Array, model.Type, element.Type), operations);
            if (element.Definition == FormatterDefinitions.Array)
                return ArrayForwardingOwnerFile.Render(set, SequenceOwnerFact.ForForwarding(FormatterRegistry.Array, model.Type, element.Type), operations);
            return ContiguousOwnerFile.Render(set, model, operations, ContiguousShape.Array);
        }

        if (FormatterRegistry.FindSequence(model.Definition) is { } sequence)
        {
            FormatterModel element = model.Arguments[0];
            if (element.Raw)
                return sequence.Scalar(set, SequenceOwnerFact.ForScalar(sequence, model.Type, element.Type), operations);
            return sequence.General(set, SequenceOwnerFact.ForForwarding(sequence, model.Type, element.Type), operations);
        }

        if (FormatterRegistry.FindDictionary(model.Type) is { } dictionary)
        {
            var fact = new DictionaryOwnerFact(dictionary, model.Type, model.Arguments[0].Type, model.Arguments[1].Type, contract: model);
            return dictionary.RenderDefault(set, fact, operations);
        }

        if (model.Definition == GenericIdentity.Grouping || model.Definition == GenericIdentity.Lookup)
            return GroupingOwnerFile.Render(model.Definition, Array.ConvertAll(model.Arguments, item => item.Type), model.Type, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)), set, operations);
        if (FormatterDefinitions.TryContiguous(model.Definition, out ContiguousShape shape))
            return ContiguousOwnerFile.Render(set, model, operations, shape);
        if (FormatterDefinitions.HasGeneric(model.Definition))
            return ClosedGenericOwnerFile.Render(set, model, operations);
        return FormatterRegistry.RenderStandalone(GeneratedNames.PayloadKey(model.Type), set, operations);
    }

    /// <summary>Renders the raw binary scalar and its existing JSON representation.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    private static string FrameworkScalar(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        return SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)), model.Type, false, (w, _) =>
        {
            w.Line("writer.EnsureAdditionalCapacity(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">());");
            w.Line("writer.Write(in value);");
        }, (w, _) => w.Line("reader.Read(ref value);"), (w, _) => FrameworkScalarBody.Emit(w, model, true),
            (w, _) => FrameworkScalarBody.Emit(w, model, false, set.Options?.QuotedScalarReader == true), w =>
        {
            w.Line("private static string ReadText<TOptions>(ref global::LuminPack.Core.LuminPackJsonReader reader)");
            w.Line("    where TOptions : struct, global::LuminPack.Option.ILuminPackOptions");
            w.Open();
            w.Line("string? text = null;");
            w.Line("reader.Read<TOptions>(ref text);");
            w.Line("return text!;");
            w.Close();
        });
    }

    /// <summary>Renders the established empty ordinary object representation of System.Object.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    private static string Object(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations) => SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)), model.Type, true, (w, _) =>
    {
        w.Line("writer.EnsureAdditionalCapacity(1);");
        w.Line("if (value is null) writer.WriteNullObjectHeader();");
        w.Line("else writer.WriteObjectHeader(0);");
    }, (w, _) =>
    {
        w.Line("if (!reader.TryReadObjectHead()) value = null;");
        w.Line("else value ??= new object();");
    }, (w, _) =>
    {
        w.Line("if (value is null) writer.WriteNull<TLuminProfile>();");
        w.Line("else");
        w.Open();
        w.Line("writer.WriteObjectStart<TLuminProfile>();");
        w.Line("writer.WriteObjectEnd<TLuminProfile>();");
        w.Close();
    }, (w, _) =>
    {
        w.Line("if (reader.IsNull()) value = null;");
        w.Line("else");
        w.Open();
        w.Line("reader.Skip<TLuminProfile>();");
        w.Line("value ??= new object();");
        w.Close();
    });
    /// <summary>Calls existing typed registered delegates while preserving the caller's operation state.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    private static string Registered(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations) => SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)), model.Type, false, (w, _) => Body(w, false, true, set.Options.RegisterMode != RegisterMode.Disabled, model.Type), (w, _) => Body(w, false, false, set.Options.RegisterMode != RegisterMode.Disabled, model.Type), (w, _) => Body(w, true, true, set.Options.RegisterMode != RegisterMode.Disabled, model.Type), (w, _) => Body(w, true, false, set.Options.RegisterMode != RegisterMode.Disabled, model.Type));
    /// <summary>Renders a closed generic Union owner over the definition's slots and read roots.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <returns>The resulting text.</returns>
    private static string GenericUnion(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        string[] parts = model.Underlying!.Split('|');
        var closed = model.Payload as UnionModel;
        string Call(string entry, bool write) => "global::LuminPack.LuminPackSerializer." + entry + "_" + parts[0] + "<" + parts[1] +
            ", TLuminProfile>(ref " + (write ? "writer, in" : "reader, ref") + " value);";
        return SpecialOwnerFile.Render(set, operations, ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type)), model.Type, true,
            (w, _) =>
            {
                if (closed is { ClosedWriteSlot: not null })
                    UnionEmitter.EmitClosedWriteDispatch(closed, w);
                else
                    w.Line(Call("Serialize", true));
            }, (w, _) => w.Line(Call("Deserialize", false)),
            (w, _) => w.Line(Call("SerializeJson", true)), (w, _) => w.Line(Call("DeserializeJson", false)));
    }

    /// <summary>Emits one registered operation without introducing a generated per-payload cache.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "json">The json used by this operation.</param>
    /// <param name = "write">Whether to emit the write operation.</param>
    /// <param name = "registration">The registration used by this operation.</param>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    private static void Body(CodeWriter w, bool json, bool write, bool registration, string type)
    {
        if (!registration)
        {
            w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(" + type + "));");
            return;
        }

        // The concrete type is inferred by the forwarding local function's generic parameter.
        string callback = (write ? "WriteValue" : "ReadValue") + (json ? "Json" : "");
        string state = write ? "writer" : "reader";
        string modifier = write ? "in" : "ref";
        string method = "global::LuminPack.Code.LuminPackFormatterRegistry<TLuminProfile>." + (json ? "TryGetCachedJsonValue" : "TryGetCachedValue");
        w.Line("Invoke(ref " + state + ", " + modifier + " value);");
        w.Line("static void Invoke<T>(ref global::LuminPack.Core.LuminPack" + (json ? "Json" : "") + (write ? "Writer" : "Reader") + " state, " + modifier + " T input)");
        w.Open();
        w.Line("global::LuminPack.LuminPackSerializer.EnsureFormatterRegistration();");
        w.Line(method + "<T>(out var registration);");
        w.Line("if (registration." + callback + " is not global::LuminPack.Code.LuminPack" + (json ? "Json" : "") + (write ? "Write" : "Read") + "Delegate<T> operation)");
        w.Open();
        w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowMessage(\"No formatter is registered for the requested type and operation.\");");
        w.Line("return;");
        w.Close();
        w.Line("operation(ref state, " + modifier + " input);");
        w.Close();
    }
}
