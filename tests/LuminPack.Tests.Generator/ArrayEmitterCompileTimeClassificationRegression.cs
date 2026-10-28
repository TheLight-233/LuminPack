using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Provides array emitter compile time classification regression functionality.</summary>
internal static class ArrayEmitterCompileTimeClassificationRegression
{
    /// <summary>Verifies run.</summary>
    /// <param name="references">The references used by this operation.</param>
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
        AssertClosedManaged(constrainedArray);

        LuminLocalFieldData openArray = GetField(compilation, "OpenArray`1", "Items");
        AssertClosedManaged(openArray);

        LuminLocalFieldData classConstrainedArray = GetField(compilation, "ClassConstrainedArray`1", "Items");
        AssertClosedManaged(classConstrainedArray);

        LuminLocalFieldData interfaceConstrainedArray = GetField(compilation, "InterfaceConstrainedArray`1", "Items");
        AssertClosedManaged(interfaceConstrainedArray);

        LuminLocalFieldData nullablePacketArray = GetField(compilation, "ClosedArrays", "NullablePackets");
        string nullableCompressedWrite = Generate(nullablePacketArray, ArrayEmitter.GenerateSerializeCodeWithCompress);
        AssertContains(nullableCompressedWrite, "DangerousWriteUnmanagedArray(ref index", "Nullable unmanaged arrays must keep their established uncompressed payload path.");
        AssertDoesNotContain(nullableCompressedWrite, "DangerousWriteUnmanagedArrayWithCompress", "Nullable unmanaged array compression would change the existing wire format.");
        AssertDoesNotContain(nullableCompressedWrite, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged arrays must be classified at generation time.");
    }

    /// <summary>Verifies assert bulk unmanaged.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertBulkUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ArrayEmitter.GenerateSerializeCode);
        string read = Generate(field, ArrayEmitter.GenerateDeserializeCode);
        AssertContains(write, ".Write<", scenario + " did not use the raw array writer.");
        AssertContains(read, "reader.ReadLength(", scenario + " did not validate the complete raw array payload.");
        AssertContains(read, "reader.Read(ref", scenario + " did not bulk-copy the validated raw array payload.");
        AssertDoesNotContain(write, "foreach", scenario + " expanded a raw array into element writes.");
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime type branch.");
        AssertDoesNotContain(read, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime type branch.");
        AssertDoesNotContain(write, "WriteNotNullObjectHeader", scenario + " unexpectedly emitted an element object header.");
    }

    /// <summary>Verifies assert closed managed.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
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

    /// <summary>Verifies assert fresh managed array always allocates.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    private static void AssertFreshManagedArrayAlwaysAllocates(LuminLocalFieldData field)
    {
        string read = Generate(field, ArrayEmitter.GenerateFreshDeserializeCode);
        AssertContains(read, "value = LuminPackMarshal.AllocateUninitializedArray<", "Fresh array readers must allocate their own result.");
        AssertContains(read, "LuminPackMarshal.GetNotNullArrayReference(value!)", "Fresh array readers must use the proven non-null data reference.");
        AssertDoesNotContain(read, "value is null || value.Length != length", "Fresh array readers must not carry the reusable-array branch.");
    }

    /// <summary>Obtains the field for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="containingType">The containing type used by this operation.</param>
    /// <param name="fieldName">The field name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Captures the source produced by the code emitter under test.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="generator">The generator used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string Generate(
        LuminLocalFieldData field,
        Action<LuminLocalFieldData, StringBuilder> generator)
    {
        var builder = new StringBuilder();
        generator(field, builder);
        return builder.ToString();
    }

    /// <summary>Verifies assert contains.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <param name="expected">The expected used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertContains(string text, string expected, string message)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }

    /// <summary>Verifies assert does not contain.</summary>
    /// <param name="text">The text used by this operation.</param>
    /// <param name="unexpected">The unexpected used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void AssertDoesNotContain(string text, string unexpected, string message)
    {
        if (text.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message + Environment.NewLine + text);
        }
    }
}
