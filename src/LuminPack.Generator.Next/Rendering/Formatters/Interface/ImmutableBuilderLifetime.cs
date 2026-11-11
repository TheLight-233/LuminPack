using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Renders the matched rental lifetime of optional immutable builders.</summary>
internal static class ImmutableBuilderLifetime
{
    /// <summary>Starts the builder body through the established rental or public factory.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "family">The family used by this operation.</param>
    /// <param name = "arguments">The arguments used by this operation.</param>
    /// <param name = "rent">The rent used by this operation.</param>
    /// <param name = "variable">The variable used by this operation.</param>
    internal static void Begin(CodeWriter w, string family, string arguments, bool rent, string variable)
    {
        w.Line("var " + variable + " = " + (rent ? "global::LuminPack.Utility.LuminImmutableBuilders.Rent" + family : "global::System.Collections.Immutable.Immutable" + family + ".CreateBuilder") + "<" + arguments + ">();");
        if (rent)
        {
            w.Line("try");
            w.Open();
        }
    }

    /// <summary>Returns an exclusively leased builder after successful publication or any failure.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "family">The family used by this operation.</param>
    /// <param name = "rent">The rent used by this operation.</param>
    /// <param name = "variable">The variable used by this operation.</param>
    internal static void End(CodeWriter w, string family, bool rent, string variable)
    {
        if (!rent)
            return;
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::LuminPack.Utility.LuminImmutableBuilders.Return" + family + "(" + variable + ");");
        w.Close();
    }
}
