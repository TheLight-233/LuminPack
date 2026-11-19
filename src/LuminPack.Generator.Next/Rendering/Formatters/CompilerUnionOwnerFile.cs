using System.Globalization;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Emits typed binary and JSON owners for a closed C# Union struct.</summary>
internal static class CompilerUnionOwnerFile
{
    /// <summary>Publishes the requested Union operations and their shared case map.</summary>
    /// <param name="set">The case formatter owners and generation options.</param>
    /// <param name="model">The closed Union and its cases in tag order.</param>
    /// <param name="operations">The binary and JSON operations to emit.</param>
    /// <returns>The generated owner source.</returns>
    internal static string Render(SharedFormatterSet set, FormatterModel model, GeneratedOperations operations)
    {
        const string mapType = "global::LuminPack.Utility.LuminUnionMap<int>";
        string name = ReadableTypeName.Of(GeneratedNames.PayloadKey(model.Type));
        bool wide = model.Arguments.Length > 249;
        return SpecialOwnerFile.Render(set, operations, name, model.Type, false,
            (writer, context) => Write(writer, context.Profile, json: false),
            (writer, context) => Read(writer, context, json: false),
            (writer, context) => Write(writer, context.Profile, json: true),
            (writer, context) => Read(writer, context, json: true),
            (operations & (GeneratedOperations.BinaryWrite | GeneratedOperations.JsonWrite)) == 0 ? null : EmitCaseMap);

        void EmitCaseMap(CodeWriter writer)
        {
            writer.Line("private static readonly " + mapType + " CaseMap = CreateCaseMap();");
            writer.Line("private static " + mapType + " CreateCaseMap()");
            writer.Open();
            writer.Line("var map = new " + mapType + "(" + Number(model.Arguments.Length) + ");");
            string marshal = writer.A("LuminPackMarshal");
            for (int index = 0; index < model.Arguments.Length; index++)
                writer.Line("map.TryRegister(" + marshal + ".GetMethodTable(typeof(" + model.Arguments[index].Type + ")), " + Number(index) + ");");
            writer.Line("return map;");
            writer.Close();
            writer.Blank();
            writer.Line("private static int ResolveCase(object caseValue)");
            writer.Open();
            writer.Line("var caseType = caseValue.GetType();");
            writer.Line("if (CaseMap.TryGetValue(" + marshal + ".GetMethodTable(caseType), out var slot))");
            writer.Indented(() => writer.Line("return slot;"));
            for (int index = 0; index < model.Arguments.Length; index++)
            {
                writer.Line("if (caseValue is " + model.Arguments[index].Type + ")");
                writer.Indented(() => writer.Line("return " + Number(index) + ";"));
            }
            writer.Line(writer.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(caseType, typeof(" + model.Type + "));");
            writer.Line("return -1;");
            writer.Close();
        }

        void Write(CodeWriter writer, string profile, bool json)
        {
            writer.Line("object? caseValue = value.Value;");
            writer.Line("if (caseValue is null)");
            writer.Open();
            if (!json)
                writer.Line("writer.EnsureAdditionalCapacity(1);");
            writer.Line(json ? "writer.WriteNull<" + profile + ">();" : "writer.WriteNullUnionHeader();");
            writer.Line("return;");
            writer.Close();
            writer.Line("var tag = ResolveCase(caseValue);");
            if (json)
            {
                writer.Line("writer.WriteObjectStart<" + profile + ">();");
                JsonProperty(writer, profile, "Type");
                writer.Line("writer.Write<" + profile + ">(tag);");
                JsonProperty(writer, profile, "Value");
            }
            else
            {
                writer.Line("writer.EnsureAdditionalCapacity(" + (wide ? "tag < " + writer.A("LuminPackCode") + ".WideTag ? 1 : 3" : "1") + ");");
                writer.Line("writer." + (wide ? "WriteWideUnionHeader" : "WriteUnionHeader") + "((ushort)tag);");
            }
            writer.Line("switch (tag)");
            writer.Open();
            for (int index = 0; index < model.Arguments.Length; index++)
            {
                FormatterModel item = model.Arguments[index];
                writer.Line("case " + Number(index) + ":");
                writer.Open();
                writer.Line("var member = (" + item.Type + ")caseValue;");
                if (json)
                    writer.Line(FormatterElementEmitter.Owner(set, item) + ".Write<" + profile + ">(ref writer, in member);");
                else
                    FormatterElementEmitter.Write(writer, set, item, "member", profile: profile);
                writer.Line("break;");
                writer.Close();
            }
            writer.Close();
            if (json)
                writer.Line("writer.WriteObjectEnd<" + profile + ">();");
        }

        void Read(CodeWriter writer, SpecialOwnerContext context, bool json)
        {
            if (json)
                UnionEmitter.OpenJsonCases(writer, context.JsonReader);
            else
            {
                writer.Line("if (!reader." + (wide ? "TryPeekWideUnionHeader" : "TryPeekUnionHeader") + "(out var tag))");
                writer.Open();
                writer.Line("value = default;");
                writer.Line("return;");
                writer.Close();
                writer.Line("switch (tag)");
                writer.Open();
            }
            for (int index = 0; index < model.Arguments.Length; index++)
            {
                FormatterModel item = model.Arguments[index];
                writer.Line("case " + Number(index) + ":");
                writer.Open();
                writer.Line(item.Type + " member = default!;");
                if (json)
                    writer.Line(FormatterElementEmitter.Owner(set, item) + ".Read<" + context.Profile + ">(ref reader, ref member);");
                else
                    FormatterElementEmitter.Read(writer, set, item, "member", profile: context.Profile, fresh: true);
                writer.Line("value = new " + model.Type + "(member);");
                writer.Line("break;");
                writer.Close();
            }
            writer.Line("default:");
            writer.Indented(() =>
            {
                writer.Line(writer.A("LuminPackExceptionHelper") + ".ThrowNotFoundInUnionType(tag, typeof(" + model.Type + "));");
                writer.Line("break;");
            });
            writer.Close();
            if (json)
                writer.Close();
        }
    }

    /// <summary>Writes one of the two Union envelope properties in the profile's encoding.</summary>
    /// <param name="writer">The generated source writer.</param>
    /// <param name="profile">The compiled profile type parameter.</param>
    /// <param name="name">The suffix of the Union property constant.</param>
    private static void JsonProperty(CodeWriter writer, string profile, string name)
    {
        string constants = writer.A("LuminPackConstUtf8");
        writer.Line("if (default(" + profile + ").JsonByteCount(1) == 1)");
        writer.Indented(() => writer.Line("writer.WritePropertyName<" + profile + ">(" + constants + "." + name + "U8);"));
        writer.Line("else");
        writer.Indented(() => writer.Line("writer.WritePropertyName<" + profile + ">(" + constants + "." + name + "U16);"));
    }

    /// <summary>Formats a case index for generated C# source.</summary>
    /// <param name="index">The zero-based case index.</param>
    /// <returns>The invariant decimal tag.</returns>
    private static string Number(int index) => index.ToString(CultureInfo.InvariantCulture);
}
