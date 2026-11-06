using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Shares optional exclusive builder rentals without changing insertion or publication order.</summary>
internal static class ImmutableBuilderEmitter
{
    /// <summary>Checks both exact methods needed to rent and return the existing result family's builder.</summary>
    /// <param name="compilation">The consumer's actual runtime and framework metadata.</param>
    /// <param name="family">The concrete factory name without its Immutable prefix.</param>
    /// <param name="arity">The number of element contracts.</param>
    /// <returns>Whether both matching ownership operations can be emitted.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminImmutableBuilders");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Immutable.Immutable" + family + "`" + arity);
        if (access is null || access.DeclaredAccessibility != Accessibility.Public || collection is null)
            return false;
        bool Matches(IMethodSymbol method, bool rent)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != arity ||
                method.ReturnsByRef || method.ReturnsByRefReadonly || method.TypeParameters.Any(parameter =>
                    !parameter.ConstraintTypes.IsEmpty || parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint ||
                    parameter.HasUnmanagedTypeConstraint || parameter.HasConstructorConstraint ||
                    (parameter.HasNotNullConstraint && !(arity == 2 && parameter.Ordinal == 0))))
                return false;
            var builder = collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray()).GetTypeMembers("Builder").SingleOrDefault();
            return builder is not null && (rent
                ? method.Parameters.IsEmpty && SymbolEqualityComparer.Default.Equals(method.ReturnType, builder)
                : method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, builder));
        }
        return access.GetMembers("Rent" + family).OfType<IMethodSymbol>().Any(method => Matches(method, true)) &&
            access.GetMembers("Return" + family).OfType<IMethodSymbol>().Any(method => Matches(method, false));
    }

    /// <summary>Starts the original builder body inside a matched rental lifetime when supported.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="enabled">Whether both optional runtime operations are available.</param>
    /// <param name="family">The concrete factory name without its Immutable prefix.</param>
    /// <param name="arguments">The closed generic arguments.</param>
    /// <param name="indent">The enclosing indentation.</param>
    internal static void Begin(StringBuilder sb, bool enabled, string family, string arguments, string indent)
    {
        sb.AppendLine(indent + "var builder = " + (enabled ? "global::LuminPack.Utility.LuminImmutableBuilders.Rent" + family : "global::System.Collections.Immutable.Immutable" + family + ".CreateBuilder") + "<" + arguments + ">();");
        if (enabled)
        {
            sb.AppendLine(indent + "try");
            sb.AppendLine(indent + "{"); }
    }

    /// <summary>Returns the cleared builder after publication or any reader/comparer failure.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="enabled">Whether the body acquired a matched rental.</param>
    /// <param name="family">The concrete factory name without its Immutable prefix.</param>
    /// <param name="indent">The enclosing indentation.</param>
    internal static void End(StringBuilder sb, bool enabled, string family, string indent)
    {
        if (!enabled)
            return;
        sb.AppendLine(indent + "}");
        sb.AppendLine(indent + "finally { global::LuminPack.Utility.LuminImmutableBuilders.Return" + family + "(builder); }");
    }
}
