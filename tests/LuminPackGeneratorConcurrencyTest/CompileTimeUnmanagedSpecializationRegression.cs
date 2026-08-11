using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class CompileTimeUnmanagedSpecializationRegression
{
    public static void Run(MetadataReference[] references)
    {
        const string source = """
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Runtime.InteropServices;

            public struct UnmanagedValue
            {
                public int Number;
            }

            public struct ManagedValue
            {
                public string Text;
            }

            public sealed class ReferenceEnvelope<T>
            {
                public T Value = default!;
            }

            public interface IMarker
            {
            }

            public sealed class Probe<TUnmanaged, TStruct, TClass, TInterface>
                where TUnmanaged : unmanaged
                where TStruct : struct
                where TClass : class
                where TInterface : IMarker
            {
                public int? ClosedUnmanagedNullable;
                public ManagedValue? ClosedManagedNullable;
                public TUnmanaged? ConstrainedUnmanagedNullable;
                public TStruct? OpenNullable;

                public int[] ClosedUnmanagedArray = default!;
                public string[] ClosedManagedArray = default!;
                public CriticalHandle[] ClosedReferenceTableTrapArray = default!;
                public TUnmanaged[] ConstrainedUnmanagedArray = default!;
                public TStruct[] OpenArray = default!;
                public TClass[] ClassConstrainedArray = default!;
                public TInterface[] InterfaceConstrainedArray = default!;
                public ReferenceEnvelope<TStruct>[] OpenReferenceEnvelopeArray = default!;

                public ImmutableQueue<int> ClosedUnmanagedImmutable = default!;
                public ImmutableQueue<string> ClosedManagedImmutable = default!;
                public ImmutableQueue<CriticalHandle> ClosedReferenceTableTrapImmutable = default!;
                public ImmutableQueue<TUnmanaged> ConstrainedUnmanagedImmutable = default!;
                public ImmutableQueue<TStruct> OpenImmutable = default!;
                public ImmutableQueue<TClass> ClassConstrainedImmutable = default!;
                public ImmutableQueue<TInterface> InterfaceConstrainedImmutable = default!;
                public ImmutableQueue<ReferenceEnvelope<TStruct>> OpenReferenceEnvelopeImmutable = default!;

                public List<UnmanagedValue> ClosedUnmanagedStructList = default!;
                public List<ManagedValue> ClosedManagedStructList = default!;
                public List<TUnmanaged> ConstrainedUnmanagedStructList = default!;
                public List<TStruct> OpenStructList = default!;

                public UnmanagedValue[] ClosedUnmanagedStructArray = default!;
                public ManagedValue[] ClosedManagedStructArray = default!;
                public TUnmanaged[] ConstrainedUnmanagedStructArray = default!;
                public TStruct[] OpenStructArray = default!;
            }
            """;

        var compilation = CSharpCompilation.Create(
            "CompileTimeUnmanagedSpecialization",
            new[] { CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
        }

        var probe = compilation.GetTypeByMetadataName("Probe`4")
            ?? throw new InvalidOperationException("Could not resolve the specialization probe type.");
        var fields = probe.GetMembers().OfType<IFieldSymbol>()
            .ToDictionary(static field => field.Name, StringComparer.Ordinal);

        VerifyNullable(fields["ClosedUnmanagedNullable"], expected: ExpectedKind.Unmanaged);
        VerifyNullable(fields["ClosedManagedNullable"], expected: ExpectedKind.Managed);
        VerifyNullable(fields["ConstrainedUnmanagedNullable"], expected: ExpectedKind.Unmanaged);
        VerifyNullable(fields["OpenNullable"], expected: ExpectedKind.RuntimeFallback);

        VerifyArrayEvaluator(fields["ClosedUnmanagedArray"], expected: ExpectedKind.Unmanaged);
        VerifyArrayEvaluator(fields["ClosedManagedArray"], expected: ExpectedKind.Managed);
        VerifyArrayEvaluator(fields["ClosedReferenceTableTrapArray"], expected: ExpectedKind.Managed);
        VerifyArrayEvaluator(fields["ConstrainedUnmanagedArray"], expected: ExpectedKind.Unmanaged);
        VerifyArrayEvaluator(fields["OpenArray"], expected: ExpectedKind.RuntimeFallback);
        VerifyArrayEvaluator(fields["ClassConstrainedArray"], expected: ExpectedKind.Managed);
        VerifyArrayEvaluator(fields["InterfaceConstrainedArray"], expected: ExpectedKind.RuntimeFallback);
        VerifyArrayEvaluator(fields["OpenReferenceEnvelopeArray"], expected: ExpectedKind.Managed);

        VerifyImmutableQueue(fields["ClosedUnmanagedImmutable"], expected: ExpectedKind.Unmanaged);
        VerifyImmutableQueue(fields["ClosedManagedImmutable"], expected: ExpectedKind.Managed);
        VerifyImmutableQueue(fields["ClosedReferenceTableTrapImmutable"], expected: ExpectedKind.Managed);
        VerifyImmutableQueue(fields["ConstrainedUnmanagedImmutable"], expected: ExpectedKind.Unmanaged);
        VerifyImmutableQueue(fields["OpenImmutable"], expected: ExpectedKind.RuntimeFallback);
        VerifyImmutableQueue(fields["ClassConstrainedImmutable"], expected: ExpectedKind.Managed);
        VerifyImmutableQueue(fields["InterfaceConstrainedImmutable"], expected: ExpectedKind.RuntimeFallback);
        VerifyImmutableQueue(fields["OpenReferenceEnvelopeImmutable"], expected: ExpectedKind.Managed);

        VerifyObjectCollectionEvaluator(fields["ClosedUnmanagedStructList"], LuminFiledType.List, ExpectedKind.Unmanaged);
        VerifyObjectCollectionEvaluator(fields["ClosedManagedStructList"], LuminFiledType.List, ExpectedKind.Managed);
        VerifyObjectCollectionEvaluator(fields["ConstrainedUnmanagedStructList"], LuminFiledType.List, ExpectedKind.Unmanaged);
        VerifyObjectCollectionEvaluator(fields["OpenStructList"], LuminFiledType.List, ExpectedKind.RuntimeFallback);

        VerifyObjectCollectionEvaluator(fields["ClosedUnmanagedStructArray"], LuminFiledType.Array, ExpectedKind.Unmanaged);
        VerifyObjectCollectionEvaluator(fields["ClosedManagedStructArray"], LuminFiledType.Array, ExpectedKind.Managed);
        VerifyObjectCollectionEvaluator(fields["ConstrainedUnmanagedStructArray"], LuminFiledType.Array, ExpectedKind.Unmanaged);
        VerifyObjectCollectionEvaluator(fields["OpenStructArray"], LuminFiledType.Array, ExpectedKind.RuntimeFallback);

        VerifyCoreRoslynOverridesKnownValueText(
            compilation.GetSpecialType(SpecialType.System_Int32),
            ((IArrayTypeSymbol)fields["ClosedReferenceTableTrapArray"].Type).ElementType);
    }

    private static void VerifyNullable(IFieldSymbol field, ExpectedKind expected)
    {
        var data = CreateFieldData(field);
        var serialize = new StringBuilder();
        var deserialize = new StringBuilder();
        var calculate = new StringBuilder();
        NullableEmitter.GenerateSerializeCode(data, serialize);
        NullableEmitter.GenerateDeserializeCode(data, deserialize);
        NullableEmitter.GenerateCalculateOffsetCode(data, calculate);
        string generated = serialize.ToString() + deserialize + calculate;

        VerifyRuntimeFallback(generated, expected, field.Name);
        switch (expected)
        {
            case ExpectedKind.Unmanaged:
                Require(generated, "DangerousWriteUnmanaged", field.Name);
                Require(generated, "DangerousReadUnmanaged", field.Name);
                Require(generated, "Unsafe.SizeOf", field.Name);
                Reject(generated, "writer.WriteValue(in valRef)", field.Name);
                break;
            case ExpectedKind.Managed:
                Reject(generated, "DangerousWriteUnmanaged", field.Name);
                Reject(generated, "DangerousReadUnmanaged", field.Name);
                Require(generated, "writer.WriteValue(in valRef)", field.Name);
                Require(generated, "reader.ReadValue(ref valRef)", field.Name);
                Require(generated, "evaluator.CalculateOffset(ref val)", field.Name);
                break;
        }
    }

    private static void VerifyArrayEvaluator(IFieldSymbol field, ExpectedKind expected)
    {
        var generated = new StringBuilder();
        CollectionEvaluatorEmitter.GenerateArrayCalculateOffsetCode(CreateFieldData(field), generated);
        string text = generated.ToString();

        VerifyRuntimeFallback(text, expected, field.Name);
        switch (expected)
        {
            case ExpectedKind.Unmanaged:
                Require(text, "Unsafe.SizeOf", field.Name);
                Reject(text, "foreach (ref var item", field.Name);
                break;
            case ExpectedKind.Managed:
                Reject(text, "Unsafe.SizeOf", field.Name);
                Require(text, "foreach (ref var item", field.Name);
                break;
        }
    }

    private static void VerifyImmutableQueue(IFieldSymbol field, ExpectedKind expected)
    {
        var generated = new StringBuilder();
        ImmutableQueueEmitter.GenerateDeserializeCode(CreateFieldData(field), generated);
        string text = generated.ToString();

        switch (expected)
        {
            case ExpectedKind.Unmanaged:
                Require(text, "clearArray: false", field.Name);
                Reject(text, "IsReferenceOrContainsReferences", field.Name);
                break;
            case ExpectedKind.Managed:
                Require(text, "clearArray: true", field.Name);
                Reject(text, "IsReferenceOrContainsReferences", field.Name);
                break;
            case ExpectedKind.RuntimeFallback:
                Require(text, "IsReferenceOrContainsReferences", field.Name);
                break;
        }
    }

    private static void VerifyObjectCollectionEvaluator(
        IFieldSymbol field,
        LuminFiledType collectionKind,
        ExpectedKind expected)
    {
        ITypeSymbol elementType = field.Type switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: > 0 } named => named.TypeArguments[0],
            _ => throw new InvalidOperationException($"{field.Name} is not a supported collection probe.")
        };
        string collectionTypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var collectionField = new LuminDataField
        {
            Type = collectionKind,
            Name = field.Name,
            FullTypeName = collectionTypeName,
            TypeName = collectionTypeName,
            TypeSymbol = field.Type,
            FieldType = LuminDataType.Reference,
            GenericType = new List<LuminGenericsType> { LuminGenericsType.Struct },
            ClassName = elementTypeName
        };
        var owner = new LuminDataInfo
        {
            classFullName = "CompileTimeCollectionEvaluatorProbe",
            classNameSpace = "<global namespace>",
            isValueType = false
        };
        owner.fields.Add(collectionField);

        var generated = new StringBuilder();
        LuminPackCodeGenerator.GenerateCalculateOffsetCode(owner, generated);
        string text = generated.ToString();

        VerifyRuntimeFallback(text, expected, field.Name);
        Reject(text, "Unsafe.SizeOf<" + collectionTypeName + ">", field.Name);
        switch (expected)
        {
            case ExpectedKind.Unmanaged:
                Require(text, "Unsafe.SizeOf<" + elementTypeName + ">", field.Name);
                Reject(text, "for (int i", field.Name);
                break;
            case ExpectedKind.Managed:
                Reject(text, "Unsafe.SizeOf", field.Name);
                Require(text, "for (int i", field.Name);
                break;
            case ExpectedKind.RuntimeFallback:
                Require(text, "IsReferenceOrContainsReferences<" + elementTypeName + ">", field.Name);
                Require(text, "Unsafe.SizeOf<" + elementTypeName + ">", field.Name);
                Require(text, "for (int i", field.Name);
                break;
        }
    }

    private static void VerifyCoreRoslynOverridesKnownValueText(
        ITypeSymbol intType,
        ITypeSymbol referenceTrapType)
    {
        var owner = new LuminDataInfo
        {
            classFullName = "CoreKnownValueTrapProbe",
            classNameSpace = "<global namespace>",
            isValueType = false
        };
        owner.fields.Add(new LuminDataField
        {
            Type = LuminFiledType.Int,
            Name = "Prefix",
            TypeName = "int",
            FullTypeName = "int",
            TypeSymbol = intType,
            FieldType = LuminDataType.Value
        });
        owner.fields.Add(new LuminDataField
        {
            Type = LuminFiledType.Class,
            Name = "CoreReferenceTrap",
            TypeName = referenceTrapType.ToDisplayString(),
            FullTypeName = referenceTrapType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeSymbol = referenceTrapType,
            FieldType = LuminDataType.Reference,
            ClassName = referenceTrapType.Name
        });

        var generated = new StringBuilder();
        LuminPackCodeGenerator.GenerateCalculateOffsetCode(owner, generated);
        string text = generated.ToString();

        Require(text, "value.CoreReferenceTrap", "Core CriticalHandle classification");
    }

    private static LuminLocalFieldData CreateFieldData(IFieldSymbol field)
    {
        return new LuminLocalFieldData
        {
            TypeName = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            TypeSymbol = field.Type,
            Name = field.Name,
            IsValue = field.Type.IsValueType
        };
    }

    private static void VerifyRuntimeFallback(string generated, ExpectedKind expected, string scenario)
    {
        if (expected == ExpectedKind.RuntimeFallback)
        {
            Require(generated, "IsReferenceOrContainsReferences", scenario);
        }
        else
        {
            Reject(generated, "IsReferenceOrContainsReferences", scenario);
        }
    }

    private static void Require(string generated, string expected, string scenario)
    {
        if (!generated.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{scenario} did not emit expected fragment: {expected}");
        }
    }

    private static void Reject(string generated, string unexpected, string scenario)
    {
        if (generated.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{scenario} retained unexpected fragment: {unexpected}");
        }
    }

    private enum ExpectedKind
    {
        Unmanaged,
        Managed,
        RuntimeFallback
    }
}
