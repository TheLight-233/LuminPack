using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class ArrayEmitterCompileTimeClassificationRegression
{
    public static void Run(MetadataReference[] references)
    {
        const string source = """
            using LuminPack.Attribute;

            [LuminPackable]
            public struct UserPacket
            {
                public int Id;
                public float Weight;
            }

            public sealed class ManagedNode
            {
                public string Name = string.Empty;
            }

            public sealed class ClosedArrays
            {
                public UserPacket[] Packets = System.Array.Empty<UserPacket>();
                public UserPacket?[] NullablePackets = System.Array.Empty<UserPacket?>();
                public ManagedNode[] Nodes = System.Array.Empty<ManagedNode>();
                public System.Runtime.InteropServices.CriticalHandle[] Handles = System.Array.Empty<System.Runtime.InteropServices.CriticalHandle>();
            }

            public sealed class ConstrainedArray<T> where T : unmanaged
            {
                public T[] Items = System.Array.Empty<T>();
            }

            public sealed class OpenArray<T>
            {
                public T[] Items = System.Array.Empty<T>();
            }

            public sealed class ClassConstrainedArray<T> where T : class
            {
                public T[] Items = System.Array.Empty<T>();
            }

            public interface IMarker
            {
            }

            public sealed class InterfaceConstrainedArray<T> where T : IMarker
            {
                public T[] Items = System.Array.Empty<T>();
            }
            """;

        var compilation = CSharpCompilation.Create(
            "ArrayFormatterCompileTimeClassification",
            new[] { CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        Diagnostic[] errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(
                "Array formatter classification fixture did not compile: " +
                string.Join(" | ", errors.Select(static diagnostic => diagnostic.ToString())));
        }

        LuminLocalFieldData packetArray = GetField(compilation, "ClosedArrays", "Packets");
        AssertBulkUnmanaged(packetArray, "[LuminPackable] unmanaged struct[]");

        LuminLocalFieldData managedArray = GetField(compilation, "ClosedArrays", "Nodes");
        AssertClosedManaged(managedArray);
        AssertFreshManagedArrayAlwaysAllocates(managedArray);

        // Roslyn's closed-type fact is authoritative even if a legacy textual
        // known-type table contains a stale entry for a reference type.
        LuminLocalFieldData legacyKnownReferenceArray = GetField(compilation, "ClosedArrays", "Handles");
        AssertClosedManaged(legacyKnownReferenceArray);

        LuminLocalFieldData constrainedArray = GetField(compilation, "ConstrainedArray`1", "Items");
        AssertBulkUnmanaged(constrainedArray, "where T : unmanaged array");

        LuminLocalFieldData openArray = GetField(compilation, "OpenArray`1", "Items");
        AssertContains(
            Generate(openArray, ArrayEmitter.GenerateSerializeCode),
            "RuntimeHelpers.IsReferenceOrContainsReferences<T>()",
            "An unconstrained T[] must retain the runtime fallback.");

        LuminLocalFieldData classConstrainedArray = GetField(compilation, "ClassConstrainedArray`1", "Items");
        AssertClosedManaged(classConstrainedArray);

        LuminLocalFieldData interfaceConstrainedArray = GetField(compilation, "InterfaceConstrainedArray`1", "Items");
        AssertContains(
            Generate(interfaceConstrainedArray, ArrayEmitter.GenerateSerializeCode),
            "RuntimeHelpers.IsReferenceOrContainsReferences<T>()",
            "An interface-constrained T[] must retain the runtime fallback because value-type implementations are legal.");

        LuminLocalFieldData nullablePacketArray = GetField(compilation, "ClosedArrays", "NullablePackets");
        string nullableCompressedWrite = Generate(nullablePacketArray, ArrayEmitter.GenerateSerializeCodeWithCompress);
        AssertContains(nullableCompressedWrite, "DangerousWriteUnmanagedArray(ref index", "Nullable unmanaged arrays must keep their established uncompressed payload path.");
        AssertDoesNotContain(nullableCompressedWrite, "DangerousWriteUnmanagedArrayWithCompress", "Nullable unmanaged array compression would change the existing wire format.");
        AssertDoesNotContain(nullableCompressedWrite, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged arrays must be classified at generation time.");
    }

    private static void AssertBulkUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ArrayEmitter.GenerateSerializeCode);
        string read = Generate(field, ArrayEmitter.GenerateDeserializeCode);
        AssertContains(write, "DangerousWriteUnmanagedArray(ref index", scenario + " did not use the raw array writer.");
        AssertContains(read, "DangerousReadUnmanagedArray(ref index", scenario + " did not use the raw array reader.");
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime type branch.");
        AssertDoesNotContain(read, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime type branch.");
        AssertDoesNotContain(write, "WriteNotNullObjectHeader", scenario + " unexpectedly emitted an element object header.");
    }

    private static void AssertClosedManaged(LuminLocalFieldData field)
    {
        string write = Generate(field, ArrayEmitter.GenerateSerializeCode);
        string read = Generate(field, ArrayEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteCollectionHeader(ref index, value.Length);", "Closed managed arrays must use the element loop path.");
        AssertContains(read, "reader.ReadValue(ref", "Closed managed arrays must deserialize elements one by one.");
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed managed arrays retained a runtime type branch.");
        AssertDoesNotContain(read, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed managed arrays retained a runtime type branch.");
        AssertDoesNotContain(write, "DangerousWriteUnmanagedArray", "Closed managed arrays were incorrectly classified as raw-copy arrays.");
    }

    private static void AssertFreshManagedArrayAlwaysAllocates(LuminLocalFieldData field)
    {
        string read = Generate(field, ArrayEmitter.GenerateFreshDeserializeCode);
        AssertContains(read, "value = LuminPackMarshal.AllocateUninitializedArray<", "Fresh array readers must allocate their own result.");
        AssertContains(read, "LuminPackMarshal.GetNotNullArrayReference(value!)", "Fresh array readers must use the proven non-null data reference.");
        AssertDoesNotContain(read, "value is null || value.Length != length", "Fresh array readers must not carry the reusable-array branch.");
    }

    private static LuminLocalFieldData GetField(Compilation compilation, string containingType, string fieldName)
    {
        INamedTypeSymbol type = compilation.GetTypeByMetadataName(containingType)
            ?? throw new InvalidOperationException("Missing fixture type " + containingType + ".");
        IFieldSymbol field = type.GetMembers(fieldName).OfType<IFieldSymbol>().Single();
        return new LuminLocalFieldData
        {
            TypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeSymbol = field.Type,
            Name = field.Name,
            IsValue = field.Type.IsValueType
        };
    }

    private static string Generate(
        LuminLocalFieldData field,
        Action<LuminLocalFieldData, StringBuilder> generator)
    {
        var builder = new StringBuilder();
        generator(field, builder);
        return builder.ToString();
    }

    private static void AssertContains(string text, string expected, string message)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }

    private static void AssertDoesNotContain(string text, string unexpected, string message)
    {
        if (text.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }
}
