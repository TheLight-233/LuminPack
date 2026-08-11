using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class CollectionEmitterCompileTimeClassificationRegression
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

            public sealed class ClosedCollections
            {
                public System.Collections.Generic.List<UserPacket> PacketList = new();
                public System.Collections.Generic.Stack<UserPacket> PacketStack = new();
                public System.Collections.Generic.Queue<UserPacket> PacketQueue = new();
                public System.Collections.Concurrent.ConcurrentStack<UserPacket> PacketConcurrentStack = new();

                public System.Collections.Generic.List<UserPacket?> NullablePacketList = new();

                public System.Collections.Generic.List<ManagedNode> NodeList = new();
                public System.Collections.Generic.Stack<ManagedNode> NodeStack = new();
                public System.Collections.Generic.Queue<ManagedNode> NodeQueue = new();
                public System.Collections.Concurrent.ConcurrentStack<ManagedNode> NodeConcurrentStack = new();

                public System.Collections.Generic.List<System.Runtime.InteropServices.CriticalHandle> HandleList = new();
                public System.Collections.Generic.Stack<System.Runtime.InteropServices.CriticalHandle> HandleStack = new();
                public System.Collections.Generic.Queue<System.Runtime.InteropServices.CriticalHandle> HandleQueue = new();
                public System.Collections.Concurrent.ConcurrentStack<System.Runtime.InteropServices.CriticalHandle> HandleConcurrentStack = new();
            }

            public sealed class ConstrainedCollections<T> where T : unmanaged
            {
                public System.Collections.Generic.List<T> List = new();
                public System.Collections.Generic.Stack<T> Stack = new();
                public System.Collections.Generic.Queue<T> Queue = new();
                public System.Collections.Concurrent.ConcurrentStack<T> ConcurrentStack = new();
            }

            public sealed class OpenCollections<T>
            {
                public System.Collections.Generic.List<T> List = new();
                public System.Collections.Generic.Stack<T> Stack = new();
                public System.Collections.Generic.Queue<T> Queue = new();
                public System.Collections.Concurrent.ConcurrentStack<T> ConcurrentStack = new();
            }
            """;

        var compilation = CSharpCompilation.Create(
            "CollectionFormatterCompileTimeClassification",
            new[] { CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        Diagnostic[] errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(
                "Collection formatter classification fixture did not compile: " +
                string.Join(" | ", errors.Select(static diagnostic => diagnostic.ToString())));
        }

        AssertListUnmanaged(GetField(compilation, "ClosedCollections", "PacketList"), "closed [LuminPackable] unmanaged struct");
        AssertStackUnmanaged(GetField(compilation, "ClosedCollections", "PacketStack"), "closed [LuminPackable] unmanaged struct");
        AssertQueueUnmanaged(GetField(compilation, "ClosedCollections", "PacketQueue"), "closed [LuminPackable] unmanaged struct");
        AssertConcurrentStackClear(GetField(compilation, "ClosedCollections", "PacketConcurrentStack"), "false", "closed unmanaged concurrent stack");

        AssertListManaged(GetField(compilation, "ClosedCollections", "NodeList"), "closed managed class");
        AssertStackManaged(GetField(compilation, "ClosedCollections", "NodeStack"), "closed managed class");
        AssertQueueManaged(GetField(compilation, "ClosedCollections", "NodeQueue"), "closed managed class");
        AssertConcurrentStackClear(GetField(compilation, "ClosedCollections", "NodeConcurrentStack"), "true", "closed managed concurrent stack");

        // Roslyn's closed-type result remains authoritative even if a textual
        // compatibility table is accidentally polluted by a reference type.
        AssertListManaged(GetField(compilation, "ClosedCollections", "HandleList"), "CriticalHandle list");
        AssertStackManaged(GetField(compilation, "ClosedCollections", "HandleStack"), "CriticalHandle stack");
        AssertQueueManaged(GetField(compilation, "ClosedCollections", "HandleQueue"), "CriticalHandle queue");
        AssertConcurrentStackClear(GetField(compilation, "ClosedCollections", "HandleConcurrentStack"), "true", "CriticalHandle concurrent stack");

        AssertListUnmanaged(GetField(compilation, "ConstrainedCollections`1", "List"), "where T : unmanaged list");
        AssertStackUnmanaged(GetField(compilation, "ConstrainedCollections`1", "Stack"), "where T : unmanaged stack");
        AssertQueueUnmanaged(GetField(compilation, "ConstrainedCollections`1", "Queue"), "where T : unmanaged queue");
        AssertConcurrentStackClear(GetField(compilation, "ConstrainedCollections`1", "ConcurrentStack"), "false", "where T : unmanaged concurrent stack");

        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "List"), ListEmitter.GenerateSerializeCode, ListEmitter.GenerateDeserializeCode, "open List<T>");
        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "Stack"), StackEmitter.GenerateSerializeCode, StackEmitter.GenerateDeserializeCode, "open Stack<T>");
        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "Queue"), QueueEmitter.GenerateSerializeCode, QueueEmitter.GenerateDeserializeCode, "open Queue<T>");
        AssertContains(
            Generate(GetField(compilation, "OpenCollections`1", "ConcurrentStack"), ConcurrentStackEmitter.GenerateSerializeCode),
            "RuntimeHelpers.IsReferenceOrContainsReferences<T>()",
            "Open ConcurrentStack<T> must retain its runtime ArrayPool clearing decision.");

        LuminLocalFieldData nullableList = GetField(compilation, "ClosedCollections", "NullablePacketList");
        string nullableWrite = Generate(nullableList, ListEmitter.GenerateSerializeCodeWithCompress);
        string nullableRead = Generate(nullableList, ListEmitter.GenerateDeserializeCodeWithCompress);
        AssertContains(nullableWrite, "DangerousWriteUnmanagedSpan(ref index", "Nullable unmanaged lists must keep their established uncompressed payload.");
        AssertContains(nullableRead, "DangerousReadUnmanagedSpan(ref index", "Nullable unmanaged lists must keep their established uncompressed payload.");
        AssertDoesNotContain(nullableWrite, "DangerousWriteUnmanagedSpanWithCompress", "Nullable unmanaged list compression would change the wire protocol.");
        AssertDoesNotContain(nullableWrite, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged lists retained a runtime branch.");
        AssertDoesNotContain(nullableRead, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged lists retained a runtime branch.");
    }

    private static void AssertListUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ListEmitter.GenerateSerializeCode);
        string read = Generate(field, ListEmitter.GenerateDeserializeCode);
        AssertContains(write, "DangerousWriteUnmanagedSpan(ref index", scenario + " did not use the raw list writer.");
        AssertContains(read, "DangerousReadUnmanagedSpan(ref index", scenario + " did not use the raw list reader.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertListManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ListEmitter.GenerateSerializeCode);
        string read = Generate(field, ListEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in item!);", scenario + " did not use concrete element writes.");
        AssertContains(read, "reader.ReadValue(ref", scenario + " did not use concrete element reads.");
        AssertDoesNotContain(write, "DangerousWriteUnmanagedSpan", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertStackUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, StackEmitter.GenerateSerializeCode);
        string read = Generate(field, StackEmitter.GenerateDeserializeCode);
        AssertContains(write, "DangerousWriteUnmanagedSpan(ref index", scenario + " did not use the raw stack writer on modern runtimes.");
        AssertContains(read, "DangerousReadUnmanagedSpan(ref index", scenario + " did not use the raw stack reader on modern runtimes.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertStackManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, StackEmitter.GenerateSerializeCode);
        string read = Generate(field, StackEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in item!);", scenario + " did not use concrete stack element writes.");
        AssertContains(read, "reader.ReadValue(ref item!);", scenario + " did not use concrete stack element reads.");
        AssertDoesNotContain(write, "DangerousWriteUnmanagedSpan", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertQueueUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, QueueEmitter.GenerateSerializeCode);
        string read = Generate(field, QueueEmitter.GenerateDeserializeCode);
        AssertContains(write, "Unsafe.CopyBlockUnaligned", scenario + " did not use the raw queue writer.");
        AssertContains(read, "Unsafe.CopyBlockUnaligned", scenario + " did not use the raw queue reader.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertQueueManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, QueueEmitter.GenerateSerializeCode);
        string read = Generate(field, QueueEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in item);", scenario + " did not use concrete queue element writes.");
        AssertContains(read, "reader.ReadValue(ref", scenario + " did not use concrete queue element reads.");
        AssertDoesNotContain(write, "Unsafe.CopyBlockUnaligned", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    private static void AssertConcurrentStackClear(
        LuminLocalFieldData field, string expected, string scenario)
    {
        string write = Generate(field, ConcurrentStackEmitter.GenerateSerializeCode);
        AssertContains(write, "clearArray: " + expected, scenario + " did not use a generation-time ArrayPool clearing decision.");
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime ArrayPool clearing branch.");
    }

    private static void AssertOpenFallback(
        LuminLocalFieldData field,
        Action<LuminLocalFieldData, StringBuilder> writeGenerator,
        Action<LuminLocalFieldData, StringBuilder> readGenerator,
        string scenario)
    {
        AssertContains(
            Generate(field, writeGenerator),
            "RuntimeHelpers.IsReferenceOrContainsReferences<T>()",
            scenario + " must retain its runtime write fallback.");
        AssertContains(
            Generate(field, readGenerator),
            "RuntimeHelpers.IsReferenceOrContainsReferences<T>()",
            scenario + " must retain its runtime read fallback.");
    }

    private static void AssertNoRuntimeClassification(string write, string read, string scenario)
    {
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime write branch.");
        AssertDoesNotContain(read, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime read branch.");
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
