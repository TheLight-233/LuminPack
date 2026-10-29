using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Describes concrete serializer overloads using the formatter's actual serialization contract.</summary>
internal static class LuminPackSerializerDocumentation
{
    /// <summary>Adds type links, member contracts, layout details, and overload usage to serializer XML comments.</summary>
    /// <param name="source">The generated serializer facade.</param>
    /// <param name="types">The concrete types for which overloads were emitted.</param>
    /// <param name="compilation">The compilation supplying the member symbols.</param>
    /// <param name="metadata">The consuming compilation's generation settings.</param>
    /// <returns>The serializer source with expanded XML documentation and unchanged executable tokens.</returns>
    internal static string Enhance(string source, ITypeSymbol[] types, Compilation compilation, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse)).GetCompilationUnitRoot();
        return Enhance(root, types, compilation, metadata);
    }

    /// <summary>Documents a projected state implementation without reparsing its complete method bodies.</summary>
    /// <param name="root">The normalized source with documentation parsing enabled.</param>
    /// <param name="types">Concrete payloads represented by the state methods.</param>
    /// <param name="compilation">Current serialization metadata inputs.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <returns>Rich XML comments and unchanged executable tokens.</returns>
    internal static string Enhance(CompilationUnitSyntax root, ITypeSymbol[] types, Compilation compilation, MetaInfo metadata)
    {
        var completed = (CompilationUnitSyntax)GeneratedSourceDocumentation.CompleteRoot(root);
        var tree = CSharpSyntaxTree.Create(completed, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse));
        return Enhance(tree.GetRoot(), tree.GetRoot().ToFullString(), types, compilation, metadata);
    }

    /// <summary>Applies contract documentation at method trivia boundaries.</summary>
    /// <param name="root">The fully documented source tree.</param>
    /// <param name="source">Its exact source text.</param>
    /// <param name="types">Concrete payload contracts.</param>
    /// <param name="compilation">Current semantic inputs.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <returns>The source with overload-specific remarks.</returns>
    private static string Enhance(SyntaxNode root, string source, ITypeSymbol[] types, Compilation compilation, MetaInfo metadata)
    {
        var typeLookup = types.ToDictionary(type => FormatterTypeName.Get(type), StringComparer.Ordinal);
        var contracts = new Dictionary<ITypeSymbol, XElement>(SymbolEqualityComparer.Default);
        var methods = new Dictionary<BaseMethodDeclarationSyntax, BaseMethodDeclarationSyntax>();

        foreach (var method in root.DescendantNodes(node => node is not BaseMethodDeclarationSyntax).OfType<MethodDeclarationSyntax>())
        {
            if (!method.Modifiers.Any(SyntaxKind.PublicKeyword) && !method.Modifiers.Any(SyntaxKind.InternalKeyword)) continue;
            string name = method.Identifier.ValueText;
            if (!(name.StartsWith("Serialize", StringComparison.Ordinal) || name.StartsWith("Deserialize", StringComparison.Ordinal) || name == "Sizeof")) continue;

            var valueParameter = method.ParameterList.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText is "value" or "input");
            ITypeSymbol type = null;
            if (valueParameter?.Type != null && !typeLookup.TryGetValue(valueParameter.Type.ToString(), out type))
                typeLookup.TryGetValue(valueParameter.Type.ToString().TrimEnd('?'), out type);
            if (type == null)
            {
                var task = method.ReturnType.DescendantNodesAndSelf().OfType<GenericNameSyntax>().FirstOrDefault(generic => generic.Identifier.ValueText == "ValueTask" && generic.TypeArgumentList.Arguments.Count == 1);
                if (task != null) typeLookup.TryGetValue(task.TypeArgumentList.Arguments[0].ToString(), out type);
            }

            var trivia = method.GetLeadingTrivia().Where(item => item.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)).ToArray();
            if (trivia.Length == 0) continue;
            string xml = Regex.Replace(string.Join(Environment.NewLine, trivia.Select(item => item.ToFullString())), "^[ \\t]*///[ ]?", "", RegexOptions.Multiline);
            var documentation = XElement.Parse("<documentation>" + xml + "</documentation>");
            var remarks = documentation.Element("remarks");
            if (remarks == null)
            {
                remarks = new XElement("remarks");
                documentation.Add(remarks);
            }

            if (type != null)
            {
                bool json = name.Contains("Json");
                var summary = documentation.Element("summary");
                if (summary != null)
                {
                    summary.ReplaceNodes(name == "Sizeof" ? "Calculates the binary payload size for " :
                        name.StartsWith("Deserialize", StringComparison.Ordinal) ? "Reads " : "Writes ",
                        TypeLink(type), name == "Sizeof" ? "." : json ? " using JSON." : " using the binary format.");
                }

                remarks.Add(new XElement("para", type.IsValueType ? "Value type." : "Reference type.", " ",
                    type.IsUnmanagedType ? "Contains no managed references." : "Uses managed reference handling."));
                if (!UsesOperationState(method))
                {
                    if (!contracts.TryGetValue(type, out var contract))
                    {
                        contract = BuildContract(type, compilation, metadata);
                        contracts.Add(type, contract);
                    }

                    foreach (var element in contract.Elements()) remarks.Add(new XElement(element));
                }

                var typeId = type.GetDocumentationCommentId();
                if (typeId != null) documentation.Add(new XElement("seealso", new XAttribute("cref", typeId)));
            }
            else if (method.TypeParameterList != null)
            {
                remarks.Add(new XElement("para", "Generic overload. The closed type argument ", new XElement("typeparamref", new XAttribute("name", "T")),
                    " selects the generated or registered formatter. Its member contract is determined by that formatter; no concrete field layout is assumed by this overload."));
            }

            AddOverloadUsage(remarks, method);
            string indentation = GeneratedSourceDocumentation.GetIndentation(method, source, out _);
            string content = string.Join(Environment.NewLine + indentation + "/// ", documentation.Nodes().Select(node => node.ToString()).SelectMany(text => text.Replace("\r\n", "\n").Split('\n')));
            methods.Add(method, GeneratedSourceDocumentation.WithDocumentation(method, "/// " + content + Environment.NewLine, indentation));
        }
        return root.ReplaceNodes(methods.Keys, (method, _) => methods[method]).ToFullString();
    }

    /// <summary>Documents a type's generation mode, physical layout, and ordered member contract.</summary>
    /// <param name="type">The concrete type described by the overload.</param>
    /// <param name="compilation">The compilation containing its symbols.</param>
    /// <param name="metadata">The settings used to analyze annotated members.</param>
    /// <returns>A container of XML elements that can be shared by overloads for this type.</returns>
    private static XElement BuildContract(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var contract = new XElement("contract");
        var named = type as INamedTypeSymbol;
        if (named == null || !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named))
        {
            contract.Add(new XElement("para", type is IArrayTypeSymbol ? "Built-in array formatter: collection header followed by elements in index order." :
                type.SpecialType == SpecialType.System_String ? "Built-in string formatter: content and null markers follow the selected string encoding and recording mode." :
                named?.IsTupleType == true || named?.Name is "Tuple" or "ValueTuple" ? "Built-in tuple formatter: components are processed in tuple position order." :
                named?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? "Built-in nullable formatter: the presence marker determines whether the underlying value follows." :
                "Built-in formatter. Collection contents, wrappers, or scalar values are handled by the formatter for this exact closed type; CLR layout alone does not define its wire representation."));
            return contract;
        }

        var data = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
        contract.Add(new XElement("para", "Generation mode: ", new XElement("c", data.generatorType.ToString()), "."));
        var layout = named.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.StructLayoutAttribute");
        string kind = layout != null && layout.ConstructorArguments.Length != 0 ? Convert.ToInt32(layout.ConstructorArguments[0].Value) switch
        {
            0 => "Sequential", 2 => "Explicit", 3 => "Auto", _ => "Runtime-defined"
        } : named.IsValueType ? "Sequential (CLR default)" : "Auto (CLR default)";
        var layoutDetails = new XElement("para", "CLR memory layout: ", new XElement("c", kind), ".");
        if (layout != null)
        {
            foreach (var setting in layout.NamedArguments) layoutDetails.Add(" ", new XElement("c", setting.Key + " = " + setting.Value.Value), ".");
        }
        layoutDetails.Add(" CLR memory offsets and padding are distinct from binary member order; JSON uses member names rather than byte offsets.");
        contract.Add(layoutDetails);

        if (data.generatorType == GeneratorType.Custom)
        {
            contract.Add(new XElement("para", "The payload is defined by the user-supplied static formatter and its registration. Automatic member serialization and fixed byte offsets are not inferred."));
            return contract;
        }
        if (data.isUnion)
        {
            contract.Add(new XElement("para", "Union payload: a member tag followed by that member's formatter payload. Known members:",
                new XElement("list", new XAttribute("type", "bullet"), data.UnionMembers.Select(member => new XElement("item", new XElement("description", "Tag ", member.Id, ": ", TypeLink(member.Type)))))));
            return contract;
        }

        bool rawCopy = global::LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.CanUseUnmanagedSerialization(named) && data.isValueType && data.generatorType == GeneratorType.Object;
        var portable = global::LuminPack.SourceGenerator.CodeEmitters.PortableStructPlan.TryCreate(named);
        contract.Add(new XElement("para", rawCopy ?
            "Binary payload is copied from the complete unmanaged storage, including CLR padding. Its size is Unsafe.SizeOf<T>(); offsets depend on the target runtime and any explicit FieldOffset attributes. The table describes storage members, not a packed wire layout." :
            portable != null ? "Binary payload contains declaration-ordered storage fields without CLR padding or an object header. Nested structs are recursively expanded; long fields always occupy eight bytes. JSON retains the filtered named member contract." :
            data.generatorType == GeneratorType.VersionTolerant ? "Binary members use stable UTF-8 names and a version-tolerant payload-length table. LuminPackOldName retains former identities; explicit order is optional. Field offsets depend on the preceding payloads." :
            data.generatorType == GeneratorType.CircleReference ? "Binary members follow LuminPackOrder and include object reference tracking. Reference headers and preceding payloads determine field offsets." :
            "Binary payload begins with an object header and follows the member order below. Variable-length members prevent a single fixed byte offset table."));

        var table = new XElement("list", new XAttribute("type", "table"),
            new XElement("listheader", new XElement("term", rawCopy || portable != null ? "Storage member" : "Serialized member"), new XElement("description", "Type and layout details")));
        if (rawCopy || portable != null)
        {
            foreach (var field in named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic))
            {
                var offset = field.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.InteropServices.FieldOffsetAttribute");
                table.Add(new XElement("item", new XElement("term", SymbolLink(field.AssociatedSymbol ?? field)),
                    new XElement("description", TypeLink(field.Type), "; CLR offset: ", offset?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? "runtime-defined", ".")));
            }
        }
        else
        {
            for (int index = 0; index < data.fields.Count; index++)
            {
                var field = data.fields[index];
                ISymbol member = FindMember(named, field.Name);
                var description = new XElement("description", TypeLink(field.TypeSymbol), "; binary order: ", index + 1,
                    "; ", field.IsPrivate ? "included non-public storage" : field.isProperty ? "property" : "field",
                    field.IsCompress ? "; compressed" : "", ".");
                table.Add(new XElement("item", new XElement("term", member == null ? new XElement("c", field.Name) : SymbolLink(member)), description));
            }
        }
        contract.Add(table);
        if (rawCopy || portable != null)
        {
            contract.Add(new XElement("para", "JSON serializes the filtered named members below; it does not copy CLR storage or padding."));
            contract.Add(new XElement("list", new XAttribute("type", "table"),
                new XElement("listheader", new XElement("term", "JSON member"), new XElement("description", "Member type")),
                data.fields.Select(field => new XElement("item", new XElement("term", new XElement("c", field.Name)), new XElement("description", TypeLink(field.TypeSymbol))))));
        }
        if (data.callBackMethods.Count != 0) contract.Add(new XElement("para", "Callbacks: ", string.Join(", ", data.callBackMethods.Select(callback => callback.Item1 + " (" + callback.Item2 + ")")), "."));
        return contract;
    }

    /// <summary>Determines whether a method is a per-operation state implementation instead of a caller-facing overload.</summary>
    /// <param name="method">The generated method whose documentation is being completed.</param>
    /// <returns><see langword="true" /> when the first parameter is one of the runtime operation states.</returns>
    private static bool UsesOperationState(MethodDeclarationSyntax method)
    {
        if (method.ParameterList.Parameters.Count == 0) return false;
        TypeSyntax type = method.ParameterList.Parameters[0].Type;
        while (type is RefTypeSyntax reference) type = reference.Type;
        string name = type?.ToString() ?? string.Empty;
        int separator = name.LastIndexOf('.');
        if (separator >= 0) name = name.Substring(separator + 1);
        return name == "LuminPackWriter" || name == "LuminPackReader" || name == "LuminPackEvaluator" ||
               name == "LuminPackJsonWriter" || name == "LuminPackJsonReader";
    }

    /// <summary>Documents the buffer, return-value, and lifetime semantics distinguishing an overload.</summary>
    /// <param name="remarks">The remarks element to extend.</param>
    /// <param name="method">The serializer overload being documented.</param>
    private static void AddOverloadUsage(XElement remarks, MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;
        bool json = method.Identifier.ValueText.Contains("Json");
        if (parameters.Count >= 2 && parameters[0].Identifier.ValueText is "writer" or "reader" or "evaluator" or "state")
            remarks.Add(new XElement("para", "Uses supplied state directly, preserving buffer position and reference tracking. Options are compile-time; caller owns initialization and completion."));
        if (method.Identifier.ValueText is "SerializeUnion" or "DeserializeUnion")
            remarks.Add(new XElement("para", "Processes a concrete Union case body. The Union tag replaces the ordinary object header and is handled by the hidden Union dispatch method."));
        if (method.Identifier.ValueText == "SerializeUnionAt")
            remarks.Add(new XElement("para", "Shared fixed-case body. A supplied narrow tag is written before the same ordered fields; a null tag means the original virtual wrapper already emitted it. Returns an absolute byte index. The caller publishes the result before state-observing boundaries; this method does not start or finish an operation."));
        if (parameters.Any(parameter => parameter.Type?.ToString() == "LuminBufferWriter"))
        {
            remarks.Add(new XElement("para", "Uses the caller-owned reusable buffer and its reference-tracking state. The caller must exclusively own the buffer for the duration of the operation."));
            if (method.Identifier.ValueText.StartsWith("Serialize", StringComparison.Ordinal))
                remarks.Add(new XElement("para", "Appends at the current absolute position without resetting payload or reference state. Call ResetForReuse before an independent session or after failure. Partial output and reference state remain after an exception; the cursor binding is always detached."));
        }
        if (parameters.Any(parameter => parameter.Type?.ToString().EndsWith("IBufferWriter<byte>", StringComparison.Ordinal) == true))
            remarks.Add(new XElement("para", "Writes to the supplied IBufferWriter<byte> and advances it by the produced byte count; the destination remains caller-owned."));
        if (method.Identifier.ValueText != "Sizeof" && parameters.Any(parameter => parameter.Identifier.ValueText == "value" && parameter.Modifiers.Any(SyntaxKind.RefKeyword)))
            remarks.Add(new XElement("para", "Populates the supplied value by reference; formatter-specific merge and reuse rules apply.",
                method.ReturnType.ToString() == "void" ? string.Empty : " The return value reports consumed bytes" +
                    (json ? " in the selected UTF-8 or UTF-16 input representation, including any transcoding performed by a character-input overload" : string.Empty) + "."));
        if (method.Identifier.ValueText.Contains("Async")) remarks.Add(new XElement("para", "Reads or writes the caller-owned stream asynchronously; cancellation is observed through cancellationToken."));
        if (method.Identifier.ValueText == "Sizeof") remarks.Add(new XElement("para", "Evaluates the binary size without producing a payload. This value does not describe JSON size or CLR object allocation size."));
    }

    /// <summary>Finds a serialized member on a type or one of its base types.</summary>
    /// <param name="type">The declaring type to search first.</param>
    /// <param name="name">The serialized member name.</param>
    /// <returns>The matching symbol, or null when no accessible metadata symbol is available.</returns>
    private static ISymbol FindMember(INamedTypeSymbol type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var member = current.GetMembers(name).FirstOrDefault(symbol => symbol is IFieldSymbol or IPropertySymbol);
            if (member != null) return member;
        }
        return null;
    }

    /// <summary>Builds a navigable C# XML documentation reference for a concrete type.</summary>
    /// <param name="type">The type to display.</param>
    /// <returns>A type link or a code element when the type has no documentation identifier.</returns>
    private static XElement TypeLink(ITypeSymbol type)
    {
        if (type == null) return new XElement("c", "formatter-defined");
        string id = type.GetDocumentationCommentId();
        // Display text retains nullable markers and tuple element names, including nested arguments.
        return id == null || type.NullableAnnotation == NullableAnnotation.Annotated || type is INamedTypeSymbol { IsGenericType: true } ? SymbolLink(type) :
            new XElement("see", new XAttribute("cref", id));
    }

    /// <summary>Builds a navigable symbol link while displaying its concrete C# name.</summary>
    /// <param name="symbol">The type, field, or property to display.</param>
    /// <returns>A see element with a compiler documentation identifier, or a code element.</returns>
    private static XElement SymbolLink(ISymbol symbol)
    {
        string text = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        string id = symbol.GetDocumentationCommentId();
        return id == null ? new XElement("c", text) : new XElement("see", new XAttribute("cref", id), text);
    }
}
