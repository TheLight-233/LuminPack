using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

internal static partial class TypeModelFactory
{
    /// <summary>Reuses the CLR layout analysis for raw tuples in the opt-in C++ projection.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The normalized project facts.</param>
    /// <returns>A per-export proof for a dense tuple in declaration order.</returns>
    internal static Func<FormatterModel, bool> CreateCppDenseTupleProof(Compilation compilation, GeneratorOptions options)
    {
        Compilation view = Complete(compilation);
        Session session = Sessions.GetValue(view, item => new Session(item, options));
        if (!session.Options.Equals(options))
            session = new Session(view, options);

        var dense = new Dictionary<string, bool>(StringComparer.Ordinal);
        return formatter =>
        {
            if (!dense.TryGetValue(formatter.Type, out bool proven))
                dense.Add(formatter.Type, proven = session.CppDenseTuple(formatter));
            return proven;
        };
    }

    private sealed partial class Session
    {
        /// <summary>Proves that the raw CLR tuple is the exact concatenation of its scalar leaves in all models.</summary>
        /// <param name="formatter">The closed tuple formatter.</param>
        /// <returns>Whether all modeled storage is dense and in declaration order.</returns>
        internal bool CppDenseTuple(FormatterModel formatter)
        {
            if (!formatter.Raw || CppTupleStorageType(formatter) is not { } tuple ||
                AnalyzePortable(tuple, strict: true) is not { WireSize: > 0 } plan || HasVolatile(plan))
                return false;

            PortableStorageModel[] models = BuildModels(plan);
            return models.Length == 1 && models[0].Size == plan.WireSize &&
                models[0].Offsets.SequenceEqual(Enumerable.Range(0, plan.WireSize));
        }

        /// <summary>Resolves the framework tuple and scalar symbols from their normalized formatter arguments.</summary>
        /// <param name="formatter">A closed tuple or scalar formatter.</param>
        /// <returns>The resolved storage symbol, or null for an unsupported component.</returns>
        private ITypeSymbol? CppTupleStorageType(FormatterModel formatter)
        {
            if (formatter.Definition == GenericIdentity.ValueTuple)
            {
                var arguments = new ITypeSymbol[formatter.Arguments.Length];
                for (int index = 0; index < arguments.Length; index++)
                {
                    if (CppTupleStorageType(formatter.Arguments[index]) is not { } argument)
                        return null;
                    arguments[index] = argument;
                }
                return _compilation.GetTypeByMetadataName("System.ValueTuple`" + arguments.Length.ToString(CultureInfo.InvariantCulture))?.Construct(arguments);
            }

            if (formatter.Definition != FormatterDefinitions.Scalar)
                return null;
            if (formatter.Underlying != null)
                return _compilation.GetTypeByMetadataName(formatter.Type.Replace("global::", string.Empty)) is { TypeKind: TypeKind.Enum } enumeration
                    ? enumeration : null;
            SpecialType scalar = formatter.Type switch
            {
                "bool" => SpecialType.System_Boolean,
                "char" => SpecialType.System_Char,
                "sbyte" => SpecialType.System_SByte,
                "byte" => SpecialType.System_Byte,
                "short" => SpecialType.System_Int16,
                "ushort" => SpecialType.System_UInt16,
                "int" => SpecialType.System_Int32,
                "uint" => SpecialType.System_UInt32,
                "long" => SpecialType.System_Int64,
                "ulong" => SpecialType.System_UInt64,
                "float" => SpecialType.System_Single,
                "double" => SpecialType.System_Double,
                _ => SpecialType.None,
            };
            return scalar == SpecialType.None ? null : _compilation.GetSpecialType(scalar);
        }
    }
}
