using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Stores declaration-time option constants without retaining compiler symbols.</summary>
internal sealed class CompiledOptions
{
    internal const string AttributeName = "LuminPack.Attribute.LuminPackOptionAttribute";
    internal readonly int Encoding;
    internal readonly int Recording;
    internal readonly char Format;
    internal readonly byte Precision;
    internal readonly int Depth;
    internal readonly bool Strict;
    internal readonly bool IsDeclared;
    internal string ScopeTypeName => IsDeclared ? "global::LuminPack.Option.LuminPackFixedOptions<" + TypeName + ">" : TypeName;
    internal int Mode => (Encoding == 1 ? 2 : 0) | (Recording == 0 ? 1 : 0);
    internal int HeaderLength => Recording == 1 ? Encoding == 0 ? 1 : 2 : Encoding == 0 ? 8 : 4;
    internal string Identity => "E" + Encoding + "R" + Recording + "F" + ((int)Format).ToString(CultureInfo.InvariantCulture) +
        "P" + Precision + "D" + (Depth < 0 ? "N" + -(long)Depth : Depth.ToString(CultureInfo.InvariantCulture)) + "V" + (Strict ? "1" : "0");
    private string StringsType => "global::LuminPack.Option." + (Mode == 1 ? "LuminPackDefaultOptions" :
        Mode == 0 ? "LuminPackUtf8TokenOptions" : Mode == 2 ? "LuminPackUtf16TokenOptions" : "LuminPackUtf16LengthOptions");
    private string FormatIdentity => "__LuminPackFormat_" + ((int)Format).ToString(CultureInfo.InvariantCulture) + "_" + Precision;
    private string DepthIdentity => "__LuminPackDepth_" + (Depth < 0 ? "N" + -(long)Depth : Depth.ToString(CultureInfo.InvariantCulture));
    internal string TypeName => Format == 'G' && Precision == byte.MaxValue && Depth == 1024 && !Strict ? StringsType :
        "global::LuminPack.Option.LuminPackOptions<" + StringsType + ", " +
        (Format == 'G' && Precision == byte.MaxValue ? "global::LuminPack.Option.LuminPackGeneralFormat" : "global::LuminPack.Generated." + FormatIdentity) + ", " +
        (Depth == 1024 ? "global::LuminPack.Option.LuminPackDefaultDepth" : "global::LuminPack.Generated." + DepthIdentity) + ", " +
        (Strict ? "global::LuminPack.Option.LuminPackStrictJson" : "global::LuminPack.Option.LuminPackTrustedJson") + ">";
    internal bool IsDefault => Encoding == 0 && Recording == 0 && Format == 'G' && Precision == byte.MaxValue && Depth == 1024 && !Strict;
    internal static readonly CompiledOptions Default = new CompiledOptions(0, 0, 'G', byte.MaxValue, 1024, false);

    /// <summary>Provides the finite wire profiles needed by ordinary Union write and size slots.</summary>
    /// <param name="json">Whether only the JSON encoding affects the slot.</param>
    /// <returns>The same slot family in declaring and consuming assemblies.</returns>
    internal static IEnumerable<CompiledOptions> UnionProfiles(bool json)
    {
        for (int encoding = 0; encoding != 2; encoding++)
            for (int recording = 0; recording != (json ? 1 : 2); recording++)
                yield return new CompiledOptions(encoding, recording, 'G', byte.MaxValue, 1024, false);
    }

    /// <summary>Discards read-only validation and formatting constants from a Union write-slot identity.</summary>
    /// <param name="json">Whether string recording is irrelevant to JSON output.</param>
    /// <returns>The stable wire-profile identity shared across assemblies.</returns>
    internal CompiledOptions UnionProfile(bool json)
        => new CompiledOptions(Encoding, json ? 0 : Recording, 'G', byte.MaxValue, 1024, false);

    /// <summary>Captures one immutable option combination.</summary>
    /// <param name="encoding">Zero for UTF-8 or one for UTF-16.</param>
    /// <param name="recording">Zero for lengths or one for tokens.</param>
    /// <param name="format">The standard-format symbol.</param>
    /// <param name="precision">The standard-format precision or 255 for the default.</param>
    /// <param name="depth">The complete JSON document depth bound.</param>
    /// <param name="strict">Whether JSON requires strict grammar.</param>
    /// <param name="declared">Whether a declaration explicitly owns this profile.</param>
    private CompiledOptions(int encoding, int recording, char format, byte precision, int depth, bool strict, bool declared = false)
    { Encoding = encoding; Recording = recording; Format = format; Precision = precision; Depth = depth; Strict = strict; IsDeclared = declared; }

    /// <summary>Resolves assembly, enclosing type and member declarations, inheriting only properties explicitly supplied.</summary>
    /// <param name="symbol">The type or member defining the serialized contract.</param>
    /// <param name="compilation">The current consuming compilation, used for built-in roots.</param>
    /// <param name="importedScope">Whether an assembly-marked imported type inherits the consuming assembly's options.</param>
    /// <returns>A symbol-free complete option profile.</returns>
    internal static CompiledOptions Resolve(ISymbol symbol, Compilation compilation, bool importedScope = true)
    {
        var assembly = symbol?.ContainingAssembly ?? compilation.Assembly;
        if (symbol is IArrayTypeSymbol || symbol is INamedTypeSymbol builtin &&
            builtin.ContainingNamespace.ToDisplayString().StartsWith("System", StringComparison.Ordinal)) assembly = compilation.Assembly;
        if (importedScope && symbol != null && !SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly) &&
            CompilationTypeAnalysisCache.GetOrCreate(compilation).IsAssemblyPackable(symbol as ITypeSymbol ?? symbol.ContainingType))
            assembly = compilation.Assembly;
        var result = Default.Apply(assembly.GetAttributes());
        var scopes = new Stack<ISymbol>();
        for (var current = symbol; current != null && current is not IAssemblySymbol && current is not INamespaceSymbol; current = current.ContainingSymbol)
            if (current is ITypeSymbol or IFieldSymbol or IPropertySymbol) scopes.Push(current);
        while (scopes.Count != 0) result = result.Apply(scopes.Pop().GetAttributes());
        return result;
    }

    /// <summary>Overlays the next member's explicit declarations on the profile inherited through its containing value.</summary>
    /// <param name="member">The original field or automatic property.</param>
    /// <param name="enclosing">The current serialized scope, including its assembly and containing members.</param>
    /// <returns>The complete profile for this field and any unmarked inline descendants.</returns>
    internal static CompiledOptions ResolveMember(ISymbol member, CompiledOptions enclosing)
    {
        var scopes = new Stack<INamedTypeSymbol>();
        for (var type = member.ContainingType; type != null; type = type.ContainingType) scopes.Push(type);
        while (scopes.Count != 0) enclosing = enclosing.Apply(scopes.Pop().GetAttributes());
        return enclosing.Apply(member.GetAttributes());
    }

    /// <summary>Validates constants before any generated upper bound can rely on their encoding widths.</summary>
    /// <param name="symbol">An assembly, type, field or property declaration.</param>
    /// <returns>Diagnostics at the original attribute syntax, without retaining compiler state.</returns>
    internal static IEnumerable<Diagnostic> Validate(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName))
            foreach (var argument in attribute.NamedArguments)
            {
                string error = null;
                if (argument.Key is "StringEncoding" or "StringRecording")
                {
                    int value = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture);
                    if (value is not (0 or 1)) error = "the value must be one of the declared encoding or recording enum members.";
                }
                if (argument.Key == "StandardFormat")
                {
                    string value = argument.Value.Value as string;
                    if (string.IsNullOrEmpty(value) || value.Length > 3 || value[0] > 127 || value.Length > 1 &&
                        (!byte.TryParse(value.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out byte precision) || precision > 99))
                        error = "use one ASCII format symbol followed by an optional precision from 0 through 99.";
                }
                if (error != null)
                    yield return Diagnostic.Create(DiagnosticDescriptors.InvalidCompiledOption,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? symbol.Locations.FirstOrDefault(), argument.Key, error);
            }
    }

    /// <summary>Overlays explicitly named attribute properties on an enclosing profile.</summary>
    /// <param name="attributes">Attributes attached to the next enclosing declaration.</param>
    /// <returns>The overlaid immutable profile.</returns>
    private CompiledOptions Apply(IEnumerable<AttributeData> attributes)
    {
        int encoding = Encoding, recording = Recording, depth = Depth;
        char format = Format; byte precision = Precision; bool strict = Strict;
        bool declared = IsDeclared;
        foreach (var attribute in attributes.Where(attribute => attribute.AttributeClass?.ToDisplayString() == AttributeName))
        {
            declared = true;
            foreach (var argument in attribute.NamedArguments)
                switch (argument.Key)
                {
                    case "StringEncoding": encoding = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture); break;
                    case "StringRecording": recording = Convert.ToInt32(argument.Value.Value, CultureInfo.InvariantCulture); break;
                    case "MaxJsonDepth": depth = (int)argument.Value.Value; break;
                    case "ValidateJsonSyntax": strict = (bool)argument.Value.Value; break;
                    case "StandardFormat":
                        string text = argument.Value.Value as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            format = text[0];
                            precision = text.Length == 1 ? byte.MaxValue : byte.TryParse(text.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out byte parsed) && parsed <= 99 ? parsed : byte.MaxValue;
                        }
                        break;
                }
        }
        return new CompiledOptions(encoding, recording, format, precision, depth, strict, declared);
    }

    /// <summary>Renders only individual nondefault constants, shared across all composed profile combinations.</summary>
    /// <returns>Stable component identities and their complete namespace declarations.</returns>
    internal IEnumerable<KeyValuePair<string, string>> Declarations()
    {
        if (Format != 'G' || Precision != byte.MaxValue)
            yield return new KeyValuePair<string, string>(FormatIdentity, "namespace LuminPack.Generated {\n" +
                "/// <summary>Provides a declaration-time standard-format constant.</summary>\npublic readonly struct " + FormatIdentity + " : global::LuminPack.Option.ILuminPackFormatOptions {\n" +
                "/// <inheritdoc/>\npublic global::System.Buffers.StandardFormat Value => new global::System.Buffers.StandardFormat((char)" + (int)Format + ", (byte)" + Precision + ");\n} }\n");
        if (Depth != 1024)
            yield return new KeyValuePair<string, string>(DepthIdentity, "namespace LuminPack.Generated {\n" +
                "/// <summary>Provides a declaration-time JSON depth bound.</summary>\npublic readonly struct " + DepthIdentity + " : global::LuminPack.Option.ILuminPackDepthOptions {\n" +
                "/// <inheritdoc/>\npublic int Value => " + Depth.ToString(CultureInfo.InvariantCulture) + ";\n} }\n");
    }
}
