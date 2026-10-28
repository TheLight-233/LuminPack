using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>Provides collection emitter compile time classification regression functionality.</summary>
internal static class CollectionEmitterCompileTimeClassificationRegression
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

            public sealed class ClosedCollections
            {
                public System.Collections.Generic.List<UserPacket> PacketList = new();
                public System.Collections.Generic.Stack<UserPacket> PacketStack = new();
                public System.Collections.Generic.Queue<UserPacket> PacketQueue = new();
                public System.Collections.Concurrent.ConcurrentStack<UserPacket> PacketConcurrentStack = new();

                public System.Collections.Generic.List<UserPacket?> NullablePacketList = new();
                public System.Collections.Generic.HashSet<UserPacket> PacketSet = new();
                public System.Collections.Generic.HashSet<int> PrimitiveSet = new();
                public System.Collections.Generic.LinkedList<int> PrimitiveLinkedList = new();
                public System.Collections.Generic.LinkedList<ManagedNode> NodeLinkedList = new();
                public System.Collections.Generic.HashSet<UserPacket?> NullablePacketSet = new();
                public System.Collections.Generic.HashSet<ManagedNode> NodeSet = new();
                public System.Collections.Generic.ISet<UserPacket> PacketSetInterface = new System.Collections.Generic.HashSet<UserPacket>();
                public System.Collections.Generic.IReadOnlySet<ManagedNode> NodeSetInterface = new System.Collections.Generic.HashSet<ManagedNode>();

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

        AssertListManaged(GetField(compilation, "ConstrainedCollections`1", "List"), "unresolved unmanaged list contract");
        AssertStackManaged(GetField(compilation, "ConstrainedCollections`1", "Stack"), "unresolved unmanaged stack contract");
        AssertQueueManaged(GetField(compilation, "ConstrainedCollections`1", "Queue"), "unresolved unmanaged queue contract");
        AssertConcurrentStackClear(GetField(compilation, "ConstrainedCollections`1", "ConcurrentStack"), "false", "where T : unmanaged concurrent stack");

        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "List"), ListEmitter.GenerateSerializeCode, ListEmitter.GenerateDeserializeCode, "open List<T>");
        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "Stack"), StackEmitter.GenerateSerializeCode, StackEmitter.GenerateDeserializeCode, "open Stack<T>");
        AssertOpenFallback(GetField(compilation, "OpenCollections`1", "Queue"), QueueEmitter.GenerateSerializeCode, QueueEmitter.GenerateDeserializeCode, "open Queue<T>");
        AssertConcurrentStackClear(GetField(compilation, "OpenCollections`1", "ConcurrentStack"), "true", "unresolved concurrent stack storage");

        LuminLocalFieldData nullableList = GetField(compilation, "ClosedCollections", "NullablePacketList");
        string nullableWrite = Generate(nullableList, ListEmitter.GenerateSerializeCodeWithCompress);
        string nullableRead = Generate(nullableList, ListEmitter.GenerateDeserializeCodeWithCompress);
        AssertContains(nullableWrite, "DangerousWriteUnmanagedSpan(ref index", "Nullable unmanaged lists must keep their established uncompressed payload.");
        AssertContains(nullableRead, "DangerousReadUnmanagedSpan(ref index", "Nullable unmanaged lists must keep their established uncompressed payload.");
        AssertDoesNotContain(nullableWrite, "DangerousWriteUnmanagedSpanWithCompress", "Nullable unmanaged list compression would change the wire protocol.");
        AssertDoesNotContain(nullableWrite, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged lists retained a runtime branch.");
        AssertDoesNotContain(nullableRead, "RuntimeHelpers.IsReferenceOrContainsReferences", "Closed nullable unmanaged lists retained a runtime branch.");

        string setRead = Generate(GetField(compilation, "ClosedCollections", "PacketSetInterface"), InterfaceSetEmitter.GenerateDeserializeCode);
        AssertContains(setRead, "RebuildHashSetBuckets(setView, length)", "Interface sets lost their approved bucket construction.");
        AssertContains(setRead, "reader.ReadLength(", "Raw interface sets lost their complete payload validation.");
        AssertContains(setRead, "reader.ReadValue(ref entry.Value!)", "Raw interface sets no longer fill entry storage directly.");
        AssertDoesNotContain(setRead, "set.Add(", "Interface sets retained duplicate comparisons.");
        string managedSetRead = Generate(GetField(compilation, "ClosedCollections", "NodeSetInterface"), InterfaceReadOnlySetEmitter.GenerateDeserializeCode);
        AssertContains(managedSetRead, "reader.ReadValue(ref item)", "Managed set elements must read into fresh local storage.");
        AssertContains(managedSetRead, ".Value = item", "Managed set elements were not committed to their entry after reading.");
        AssertDoesNotContain(managedSetRead, "set.Add(", "Read-only set interfaces retained duplicate comparisons.");
        string setSize = Generate(GetField(compilation, "ClosedCollections", "PrimitiveSet"), CollectionEvaluatorEmitter.GenerateEnumerableCalculateOffsetCode);
        AssertContains(setSize, "checked(value.Count *", "Raw HashSet size evaluation lost its constant-time payload width.");
        AssertDoesNotContain(setSize, "foreach", "Raw HashSet size evaluation still traverses the set.");
        string linkedSize = Generate(GetField(compilation, "ClosedCollections", "PrimitiveLinkedList"), CollectionEvaluatorEmitter.GenerateEnumerableCalculateOffsetCode);
        AssertContains(linkedSize, "checked(value.Count *", "Known-scalar LinkedList size evaluation lost its fixed payload width.");
        AssertDoesNotContain(linkedSize, "foreach", "Known-scalar LinkedList size evaluation still walks the nodes.");
        AssertContains(Generate(GetField(compilation, "ClosedCollections", "NodeLinkedList"), CollectionEvaluatorEmitter.GenerateEnumerableCalculateOffsetCode),
            "foreach", "Managed linked elements lost their formatter-specific size calculation.");
        AssertContains(Generate(GetField(compilation, "ClosedCollections", "NullablePacketSet"), CollectionEvaluatorEmitter.GenerateEnumerableCalculateOffsetCode),
            "foreach", "Nullable element framing cannot use its complete native storage width.");
        AssertContains(Generate(GetField(compilation, "ClosedCollections", "NodeSet"), CollectionEvaluatorEmitter.GenerateEnumerableCalculateOffsetCode),
            "foreach", "Managed set elements lost their formatter-specific size calculation.");
    }

    /// <summary>Verifies assert list unmanaged.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertListUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ListEmitter.GenerateSerializeCode);
        string read = Generate(field, ListEmitter.GenerateDeserializeCode);
        AssertContains(write, ".Write<", scenario + " did not use the raw list writer.");
        AssertContains(read, "reader.ReadLength(", scenario + " did not validate the complete raw list payload.");
        AssertContains(read, "reader.Read(ref", scenario + " did not bulk-copy the validated raw list payload.");
        AssertDoesNotContain(write, "for (", scenario + " expanded a raw list into element writes.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert list managed.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertListManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, ListEmitter.GenerateSerializeCode);
        string read = Generate(field, ListEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in span[i]!);", scenario + " did not use concrete element writes.");
        AssertContains(read, "reader.ReadValue(ref", scenario + " did not use concrete element reads.");
        AssertDoesNotContain(write, "DangerousWriteUnmanagedSpan", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert stack unmanaged.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertStackUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, StackEmitter.GenerateSerializeCode);
        string read = Generate(field, StackEmitter.GenerateDeserializeCode);
        AssertContains(write, "DangerousWriteUnmanagedSpan(ref index", scenario + " did not use the raw stack writer on modern runtimes.");
        AssertContains(read, "DangerousReadUnmanagedSpan(ref index", scenario + " did not use the raw stack reader on modern runtimes.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert stack managed.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertStackManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, StackEmitter.GenerateSerializeCode);
        string read = Generate(field, StackEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in item!);", scenario + " did not use concrete stack element writes.");
        AssertContains(read, "reader.ReadValue(ref item!);", scenario + " did not use concrete stack element reads.");
        AssertDoesNotContain(write, "DangerousWriteUnmanagedSpan", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert queue unmanaged.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertQueueUnmanaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, QueueEmitter.GenerateSerializeCode);
        string read = Generate(field, QueueEmitter.GenerateDeserializeCode);
        AssertContains(write, "LuminPackMarshal.CopyBlockUnaligned", scenario + " did not use the raw queue writer.");
        AssertContains(read, "LuminPackMarshal.CopyBlockUnaligned", scenario + " did not use the raw queue reader.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert queue managed.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertQueueManaged(LuminLocalFieldData field, string scenario)
    {
        string write = Generate(field, QueueEmitter.GenerateSerializeCode);
        string read = Generate(field, QueueEmitter.GenerateDeserializeCode);
        AssertContains(write, "writer.WriteValue(in item);", scenario + " did not use concrete queue element writes.");
        AssertContains(read, "reader.ReadValue(ref", scenario + " did not use concrete queue element reads.");
        AssertDoesNotContain(write, "LuminPackMarshal.CopyBlockUnaligned", scenario + " was incorrectly raw-copied.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert concurrent stack clear.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="expected">The expected used by this operation.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertConcurrentStackClear(
        LuminLocalFieldData field, string expected, string scenario)
    {
        string write = Generate(field, ConcurrentStackEmitter.GenerateSerializeCode);
        AssertContains(write, "clearArray: " + expected, scenario + " did not use a generation-time ArrayPool clearing decision.");
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime ArrayPool clearing branch.");
    }

    /// <summary>Verifies assert open fallback.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="writeGenerator">The write generator used by this operation.</param>
    /// <param name="readGenerator">The read generator used by this operation.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertOpenFallback(
        LuminLocalFieldData field,
        Action<LuminLocalFieldData, StringBuilder> writeGenerator,
        Action<LuminLocalFieldData, StringBuilder> readGenerator,
        string scenario)
    {
        string write = Generate(field, writeGenerator);
        string read = Generate(field, readGenerator);
        AssertContains(write, "writer.WriteValue(", scenario + " must retain its element formatter contract.");
        AssertContains(read, "reader.ReadValue(", scenario + " must retain its element formatter contract.");
        AssertNoRuntimeClassification(write, read, scenario);
    }

    /// <summary>Verifies assert no runtime classification.</summary>
    /// <param name="write">The write used by this operation.</param>
    /// <param name="read">The read used by this operation.</param>
    /// <param name="scenario">The scenario used by this operation.</param>
    private static void AssertNoRuntimeClassification(string write, string read, string scenario)
    {
        AssertDoesNotContain(write, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime write branch.");
        AssertDoesNotContain(read, "RuntimeHelpers.IsReferenceOrContainsReferences", scenario + " retained a runtime read branch.");
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
