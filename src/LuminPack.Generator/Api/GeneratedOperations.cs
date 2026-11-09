using System;
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

/// <summary>Decides which operations the generators emit for a project.</summary>
/// <remarks>Only an assembly that no other assembly can reference drops operations: an application, or one of Unity's predefined
/// player and editor assemblies. A library keeps every operation, because its consumers can call any generated member. In a
/// closed assembly the operations follow the project's calls: facade calls select their operation, calls on serializer states
/// select the state's operation, and registration, generated extension methods, interception and unresolved serializer
/// expressions keep every operation.</remarks>
internal static class GeneratedOperationUsage
{
    /// <summary>A fixed selection for measurement harnesses; null in normal generation.</summary>
    internal static GeneratedOperations? Override = null;

    /// <summary>Selects the operations to emit for a compilation.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>The operations to emit.</returns>
    internal static GeneratedOperations For(Compilation compilation)
    {
        if (Override is { } selected)
            return selected;
        if (!IsClosed(compilation) || InterceptionSupport.IsEnabled(compilation))
            return GeneratedOperations.All;
        return SerializerApiUsageAnalysis.GetOrCreate(compilation).Operations;
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

    /// <summary>Recognizes the generated extension methods that user code can call directly.</summary>
    /// <param name="name">An identifier in user code.</param>
    /// <returns>Whether it names a generated extension method.</returns>
    internal static bool IsExtensionName(string name) => name is "WriteValue" or "ReadValue" or "WriteMemberValue" or
        "WritePolymorphismValue" or "ReadPolymorphismValue" or "ReadFreshValue" or "WriteValueWithCompress" or "ReadValueWithCompress";

    /// <summary>Checks whether no other assembly can reference this one.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>Whether the assembly is an application or a Unity predefined assembly without friends.</returns>
    private static bool IsClosed(Compilation compilation)
    {
        bool application = compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or
            OutputKind.WindowsRuntimeApplication || compilation.AssemblyName is "Assembly-CSharp" or "Assembly-CSharp-Editor";
        return application && !compilation.Assembly.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
    }
}
