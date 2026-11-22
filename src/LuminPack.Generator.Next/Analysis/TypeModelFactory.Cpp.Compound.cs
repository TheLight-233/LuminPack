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
    /// <summary>Reuses the CLR layout analysis for headerless framework values in the opt-in C++ projection.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="options">The normalized project facts.</param>
    /// <returns>A per-export lookup of identical modeled CLR storage layouts.</returns>
    internal static Func<FormatterModel, FormatterStorageModel?> CreateCppRawCompoundStorage(Compilation compilation, GeneratorOptions options)
    {
        Compilation view = Complete(compilation);
        Session session = Sessions.GetValue(view, item => new Session(item, options));
        if (!session.Options.Equals(options))
            session = new Session(view, options);

        var layouts = new Dictionary<string, FormatterStorageModel?>(StringComparer.Ordinal);
        return formatter =>
        {
            if (!layouts.TryGetValue(formatter.Type, out FormatterStorageModel? storage))
                layouts.Add(formatter.Type, storage = session.CppRawCompoundStorage(formatter));
            return storage;
        };
    }

    private sealed partial class Session
    {
        /// <summary>Proves one untagged storage layout under all of the existing native models.</summary>
        /// <param name="formatter">The closed framework formatter.</param>
        /// <returns>The unique nonoverlapping CLR storage layout, or null when the layouts differ.</returns>
        internal FormatterStorageModel? CppRawCompoundStorage(FormatterModel formatter)
        {
            if (!formatter.RawCollection || formatter.Layout != null || CppCompoundStorageType(formatter) is not { } value ||
                AnalyzePortable(value, strict: true) is not { WireSize: > 0 } plan || HasVolatile(plan))
                return null;

            plan.Models = BuildModels(plan);
            if (plan.Models.Length != 1)
                return null;
            PortableStorageModel model = plan.Models[0];
            if (model.Size <= 0 || model.Offsets.Length != plan.WireSize ||
                model.Offsets.Any(offset => (uint)offset >= (uint)model.Size) || model.Offsets.Distinct().Count() != plan.WireSize)
                return null;
            return NormalizeStorage(plan);
        }

        /// <summary>Resolves framework and scalar symbols from their normalized formatter arguments.</summary>
        /// <param name="formatter">A closed framework or scalar formatter.</param>
        /// <returns>The resolved storage symbol, or null for an unsupported component.</returns>
        private ITypeSymbol? CppCompoundStorageType(FormatterModel formatter)
        {
            if (formatter.Definition is GenericIdentity.ValueTuple or GenericIdentity.KeyValuePair)
            {
                var arguments = new ITypeSymbol[formatter.Arguments.Length];
                for (int index = 0; index < arguments.Length; index++)
                {
                    if (CppCompoundStorageType(formatter.Arguments[index]) is not { } argument)
                        return null;
                    arguments[index] = argument;
                }
                return _compilation.GetTypeByMetadataName(formatter.Definition + "`" + arguments.Length.ToString(CultureInfo.InvariantCulture))?.Construct(arguments);
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
