using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>The serialization operations a project calls.</summary>
[Flags]
internal enum GeneratedOperations
{
    /// <summary>No operation is called.</summary>
    None = 0,
    /// <summary>Binary serialization.</summary>
    BinaryWrite = 1 << 0,
    /// <summary>Binary deserialization.</summary>
    BinaryRead = 1 << 1,
    /// <summary>JSON serialization.</summary>
    JsonWrite = 1 << 2,
    /// <summary>JSON deserialization.</summary>
    JsonRead = 1 << 3,
    /// <summary>Every operation.</summary>
    All = BinaryWrite | BinaryRead | JsonWrite | JsonRead,
}

/// <summary>Tracks the selected operation modules and the types each one needs.</summary>
internal sealed class GeneratedOperationUsage
{
    private readonly Dictionary<string, GeneratedOperations> _payloads = new(StringComparer.Ordinal);
    private GeneratedOperations _wholeModules;
    private static readonly GeneratedOperationUsage Complete = new(GeneratedOperations.All);

    /// <summary>Gets the operations needed in shared dispatch and facade files.</summary>
    internal GeneratedOperations Shared { get; private set; }

    /// <summary>Creates a selection with the given operations retained for every payload.</summary>
    /// <param name="wholeModules">The operations that cannot be limited to specific payloads.</param>
    private GeneratedOperationUsage(GeneratedOperations wholeModules)
    {
        _wholeModules = wholeModules;
        Shared = wholeModules;
    }

    /// <summary>Selects operations for a payload or for shared code when no payload is associated.</summary>
    /// <param name="payload">The canonical payload identity, or null for a shared contribution.</param>
    /// <returns>The operations needed in this contribution.</returns>
    internal GeneratedOperations ForPayload(string payload)
        => payload == null ? Shared : _wholeModules | (_payloads.TryGetValue(payload, out var used) ? used : GeneratedOperations.None);

    /// <summary>Selects operations from the same bound calls that choose concrete overloads.</summary>
    /// <param name="compilation">The consumer before generation.</param>
    /// <returns>The publication selection.</returns>
    internal static GeneratedOperationUsage For(Compilation compilation)
        => IsClosed(compilation) ? SerializerApiUsageAnalysis.GetOrCreate(compilation).OperationUsage : Complete;

    /// <summary>Closes each observed operation over the payloads it needs.</summary>
    /// <param name="compilation">The consumer before signature binding.</param>
    /// <param name="binding">The signature-aware compilation that owns the call symbols.</param>
    /// <param name="roots">Bound operation and payload pairs.</param>
    /// <param name="invocations">Source calls that can close generic serializer wrappers.</param>
    /// <param name="wholeModules">Operations that were observed without a provable payload.</param>
    /// <returns>The operations and their transitive formatter requirements.</returns>
    internal static GeneratedOperationUsage Build(Compilation compilation, Compilation binding,
        IEnumerable<(GeneratedOperations Operation, ITypeSymbol Payload)> roots, IReadOnlyList<IMethodSymbol> invocations,
        GeneratedOperations wholeModules)
    {
        if (!IsClosed(compilation))
            return Complete;
        wholeModules |= Forced(compilation);
        if (wholeModules == GeneratedOperations.All)
            return Complete;

        var usage = new GeneratedOperationUsage(wholeModules);
        var identities = new StableTypeIdentityFactory(binding);
        var resolver = new CurrentTypeResolver(compilation);
        foreach (var group in roots.GroupBy(root => root.Operation))
        {
            var operation = group.Key;
            usage.Shared |= operation;
            if ((wholeModules & operation) != 0)
                continue;
            var bound = ReachabilityAnalysisCache.ExpandApiPayloads(binding, group.Select(root => root.Payload), invocations);
            var current = bound.Select(type => identities.Create(type))
                .Select(identity => identity == null ? null : resolver.Resolve(identity)).ToArray();
            if (current.Any(type => type == null))
            {
                usage._wholeModules |= operation;
                continue;
            }
            var reached = ReachabilityAnalysisCache.ExpandOperationTypes(compilation, current);
            foreach (var type in reached)
            {
                string payload = FormatterTypeName.Get(type);
                usage._payloads.TryGetValue(payload, out var previous);
                usage._payloads[payload] = previous | operation;
            }
        }
        return usage;
    }

    /// <summary>Reads the assembly's compile-time overrides.</summary>
    /// <param name="compilation">The consumer before generation.</param>
    /// <returns>The independently forced operations.</returns>
    internal static GeneratedOperations Forced(Compilation compilation)
    {
        var options = compilation.Assembly.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute");
        var forced = GeneratedOperations.None;
        if (options != null)
            foreach (var argument in options.NamedArguments)
                if (argument.Value.Value is true)
                    forced |= argument.Key switch
                    {
                        "ForceGenerateSerialize" => GeneratedOperations.BinaryWrite,
                        "ForceGenerateDeserialize" => GeneratedOperations.BinaryRead,
                        "ForceGenerateSerializeJson" => GeneratedOperations.JsonWrite,
                        "ForceGenerateDeserializeJson" => GeneratedOperations.JsonRead,
                        _ => GeneratedOperations.None,
                    };
        return forced;
    }

    /// <summary>Maps a serializer facade method to its operation.</summary>
    /// <param name="name">The facade method name.</param>
    /// <returns>The operation, or every operation for registration and other members.</returns>
    internal static GeneratedOperations Of(string name) => name switch
    {
        "Serialize" or "SerializeAsync" => GeneratedOperations.BinaryWrite,
        "Deserialize" or "DeserializeAsync" => GeneratedOperations.BinaryRead,
        "SerializeJson" or "SerializeJsonAsync" => GeneratedOperations.JsonWrite,
        "DeserializeJson" or "DeserializeJsonAsync" => GeneratedOperations.JsonRead,
        _ => GeneratedOperations.All,
    };

    /// <summary>Maps a complete state operation to its generation module.</summary>
    /// <param name="receiver">The state receiver's bound type name.</param>
    /// <returns>The operation, or none for unrelated receivers.</returns>
    internal static GeneratedOperations OfState(string receiver) => receiver switch
    {
        "LuminPack.Core.LuminPackWriter" => GeneratedOperations.BinaryWrite,
        "LuminPack.Core.LuminPackReader" => GeneratedOperations.BinaryRead,
        "LuminPack.Core.LuminPackJsonWriter" => GeneratedOperations.JsonWrite,
        "LuminPack.Core.LuminPackJsonReader" => GeneratedOperations.JsonRead,
        _ => GeneratedOperations.None,
    };

    /// <summary>Recognizes the generated extension methods that user code can call directly.</summary>
    /// <param name="name">An identifier in user code.</param>
    /// <returns>Whether it names a generated extension method.</returns>
    internal static bool IsExtensionName(string name) => name is "WriteValue" or "ReadValue" or "WriteMemberValue" or
        "WritePolymorphismValue" or "ReadPolymorphismValue" or "ReadFreshValue" or "WriteValueWithCompress" or "ReadValueWithCompress" or
        "WriteValueJson" or "ReadValueJson" or "MeasureRecordWidth";

    /// <summary>Checks whether the consumer uses call-based operation selection.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether the compilation is an application or a Unity predefined assembly without friend access.</returns>
    internal static bool IsClosed(Compilation compilation)
    {
        bool application = compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or
            OutputKind.WindowsRuntimeApplication || compilation.AssemblyName is "Assembly-CSharp" or "Assembly-CSharp-Editor";
        return application && !compilation.Assembly.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
    }
}
