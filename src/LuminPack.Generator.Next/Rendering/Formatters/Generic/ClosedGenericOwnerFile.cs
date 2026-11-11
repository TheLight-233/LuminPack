using System;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders generic framework contracts using the static formatter of each argument.</summary>
internal static class ClosedGenericOwnerFile
{
    /// <summary>Renders all requested operations of a closed pair, nullable, lazy value or tuple.</summary>
    /// <param name = "set">The shared formatter publication facts.</param>
    /// <param name = "model">The normalized formatter or storage contract.</param>
    /// <param name = "operations">The binary and JSON operations to publish.</param>
    /// <returns>The generated source or selected source-ready name.</returns>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        string suffix = ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
        bool tuple = model.Definition is GenericIdentity.Tuple or GenericIdentity.ValueTuple;
        bool reference = model.Definition is GenericIdentity.Tuple or GenericIdentity.Lazy;
        bool nullable = model.Definition == GenericIdentity.Nullable;
        bool lazy = model.Definition == GenericIdentity.Lazy;
        string absent = nullable ? "!value.HasValue" : "value is null";
        string[] names = tuple ? Enumerable.Range(0, model.Arguments.Length).Select(i => i == 7 ? "Rest" : "Item" + (i + 1)).ToArray() : model.Definition == GenericIdentity.KeyValuePair ? new[]
        {
            "Key",
            "Value"
        }

        : new[]
        {
            "Value"
        };
        return SpecialOwnerFile.Render(set, operations, suffix, model.Type, reference, BinaryWrite, BinaryRead, JsonWrite, JsonRead, w =>
        {
            if (model.Layout != null)
                FormatterStorageEmitter.Support(w, model.Layout);
        });
        void BinaryWrite(CodeWriter w, SpecialOwnerContext c)
        {
            if (model.Definition == GenericIdentity.ValueTuple && model.Layout != null)
            {
                FormatterStorageEmitter.Value(w, model.Layout, "value", write: true);
                return;
            }

            if (model.Raw)
            {
                w.Line("writer.EnsureAdditionalCapacity(global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + model.Type + ">());");
                w.Line("writer.Write(in value);");
                return;
            }

            if (reference || nullable)
            {
                w.Line("writer.EnsureAdditionalCapacity(1);");
                w.Line("if (" + absent + ")");
                w.Open();
                w.Line("writer.WriteNullObjectHeader();");
                w.Line("return;");
                w.Close();
                w.Line("writer.WriteObjectHeader(" + (tuple ? model.Arguments.Length : 0) + ");");
            }

            for (int i = 0; i < model.Arguments.Length; i++)
            {
                FormatterModel argument = model.Arguments[i];
                w.Line(argument.Type + " item" + i + " = value." + names[i] + ";");
                FormatterElementEmitter.Write(w, set, argument, "item" + i);
            }
        }

        void BinaryRead(CodeWriter w, SpecialOwnerContext c)
        {
            if (model.Definition == GenericIdentity.ValueTuple && model.Layout != null)
            {
                FormatterStorageEmitter.Value(w, model.Layout, "value", write: false);
                return;
            }

            if (model.Raw)
            {
                w.Line("reader.Read(ref value);");
                return;
            }

            if (reference || nullable)
            {
                w.Line("if (!reader.TryReadObjectHead())");
                w.Open();
                w.Line("value = default!;");
                w.Line("return;");
                w.Close();
            }

            // As the original compound read, a string component starts from the existing component and keeps it when the text is
            // equal; the string operation keeps its own inlining budget.
            bool pair = model.Definition == GenericIdentity.KeyValuePair;
            bool compound = false;
            for (int i = 0; i < model.Arguments.Length; i++)
            {
                FormatterModel argument = model.Arguments[i];
                bool text = argument.Type == "string" && (tuple || pair);
                string seed = text ? (reference ? "value is null ? default! : value." + names[i] : "value." + names[i]) : "default!";
                w.Line(argument.Type + " item" + i + " = " + seed + ";");
                if (text)
                {
                    w.Line("item" + i + " = ReadCompoundString(ref reader, item" + i + ");");
                    compound = true;
                }
                else
                    FormatterElementEmitter.Read(w, set, argument, "item" + i);
            }

            Assign(w);
            if (compound)
            {
                w.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
                w.Line("static string ReadCompoundString(ref global::LuminPack.Core.LuminPackReader stringReader, string text) => stringReader.Read<TLuminProfile>(text)!;");
            }
        }

        void JsonWrite(CodeWriter w, SpecialOwnerContext c)
        {
            if (reference || nullable)
            {
                w.Line("if (" + absent + ")");
                w.Open();
                w.Line("writer.WriteNull<TLuminProfile>();");
                w.Line("return;");
                w.Close();
            }

            if (lazy)
            {
                w.Line("writer.WriteObjectStart<TLuminProfile>();");
                w.Line("writer.WritePropertyName<TLuminProfile>(\"Value\");");
            }
            else if (!nullable)
                w.Line("writer.WriteArrayStart<TLuminProfile>();");
            for (int i = 0; i < model.Arguments.Length; i++)
            {
                if (i != 0)
                {
                    w.Line("writer.WriteByteRaw<TLuminProfile>((byte)',');");
                    w.Line("writer.SetFirstElement(true);");
                }

                w.Line(model.Arguments[i].Type + " item" + i + " = value." + names[i] + ";");
                w.Line(FormatterElementEmitter.Owner(set, model.Arguments[i]) + ".Write<TLuminProfile>(ref writer, in item" + i + ");");
            }

            if (lazy)
                w.Line("writer.WriteObjectEnd<TLuminProfile>();");
            else if (!nullable)
                w.Line("writer.WriteArrayEnd<TLuminProfile>();");
        }

        void JsonRead(CodeWriter w, SpecialOwnerContext c)
        {
            if (reference || nullable)
            {
                w.Line("if (reader.IsNull())");
                w.Open();
                w.Line("value = default!;");
                w.Line("return;");
                w.Close();
            }

            for (int i = 0; i < model.Arguments.Length; i++)
                w.Line(model.Arguments[i].Type + " item" + i + " = default!;");
            if (lazy)
            {
                w.Line("reader.TryConsumeObjectStart<TLuminProfile>();");
                w.Line("bool hasValue = false;");
                w.Line("while (reader.Read<TLuminProfile>())");
                w.Open();
                w.Line("if (reader.CurrentTokenType == " + c.JsonReader + ".JsonTokenType.ObjectEnd) break;");
                w.Line("string? property = null;");
                w.Line("reader.Read<TLuminProfile>(ref property);");
                w.Line("reader.CompleteValue();");
                w.Line("reader.Read<TLuminProfile>();");
                w.Line("if (property == \"Value\")");
                w.Open();
                w.Line(FormatterElementEmitter.Owner(set, model.Arguments[0]) + ".Read<TLuminProfile>(ref reader, ref item0);");
                w.Line("hasValue = true;");
                w.Close();
                w.Line("else reader.Skip<TLuminProfile>();");
                w.Close();
                w.Line("if (!hasValue) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"A Lazy JSON object requires a Value property.\");");
            }
            else if (nullable)
                w.Line(FormatterElementEmitter.Owner(set, model.Arguments[0]) + ".Read<TLuminProfile>(ref reader, ref item0);");
            else
            {
                w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
                for (int i = 0; i < model.Arguments.Length; i++)
                {
                    w.Line("reader.Read<TLuminProfile>();");
                    w.Line(FormatterElementEmitter.Owner(set, model.Arguments[i]) + ".Read<TLuminProfile>(ref reader, ref item" + i + ");");
                }

                w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
            }

            Assign(w);
        }

        void Assign(CodeWriter w)
        {
            string values = string.Join(", ", Enumerable.Range(0, model.Arguments.Length).Select(i => "item" + i + "!"));
            w.Line(nullable ? "value = item0;" : "value = new " + model.Type + "(" + values + ");");
        }
    }
}
