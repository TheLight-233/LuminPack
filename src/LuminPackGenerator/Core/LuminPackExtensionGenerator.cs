using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
	public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
	{
		StringBuilder sb = new StringBuilder();
		HashSet<string> analyzedTypes = new HashSet<string>(StringComparer.Ordinal);
		CompilationTypeAnalysis analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
		INamedTypeSymbol owner = data.TypeSymbol;

		foreach (LuminLocalFieldData localField in data.localFields)
		{
			if (localField.TypeSymbol is null)
			{
				GenerateFormatterExtensions(sb, localField, metaInfo, analyzedTypes);
				continue;
			}

			foreach (LuminLocalFieldData formatterField in EnumerateFormatterFields(
				localField.TypeSymbol,
				new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default)))
			{
				if (!ContainsTypeParameter(formatterField.TypeSymbol) &&
					analysis.IsOwnedBy(formatterField.TypeSymbol, owner))
				{
					GenerateFormatterExtensions(sb, formatterField, metaInfo, analyzedTypes);
				}
			}
		}

		foreach (LuminLocalFieldData formatterField in EnumerateFormatterFields(data))
		{
			if (!ContainsTypeParameter(formatterField.TypeSymbol) &&
				analysis.IsOwnedBy(formatterField.TypeSymbol, owner))
			{
				GenerateFormatterExtensions(sb, formatterField, metaInfo, analyzedTypes);
			}
		}

		string typeName = GetGlobalTypeName(data);
		if (TryAddAnalyzedType(analyzedTypes, "root:" + typeName))
		{
			if (data.isUnion && data.isValueType)
			{
				LuminPackUnionCodeGenerator.GenerateCaseMapStruct(data, sb);
			}
			GenerateRootBinaryExtensions(sb, data, typeName, metaInfo);
			GenerateRootJsonExtensions(sb, data, typeName, metaInfo);
			if (!(data.isUnion && data.isValueType))
			{
				LuminPackCodeGenerator.GenerateLocalClassStructure(
					sb,
					data,
					analyzedTypes,
					layout => analysis.IsLayoutOwnedBy(layout.TypeSymbol, owner));
			}
			foreach (LuminDataField field in data.fields.Where(static x => x.ClassFields.Count > 0))
			{
				LuminPackCodeGenerator.GeneratorUnsafeAccessorMethod(sb, field, field.ClassFields, analyzedTypes);
			}
		}

		return GenerateExtension(sb, GetExtensionClassName(compilation));
	}

	public static string GenerateSerializerInvocationSupport(Compilation compilation, MetaInfo metaInfo, ReachabilityAnalysis? reachability = null)
	{
		CompilationTypeAnalysis analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
		var sb = new StringBuilder();
		var analyzedTypes = new HashSet<string>(StringComparer.Ordinal);

		// Full mode emits every no-owner type observed in the project.  Prune mode restricts the
		// source set to the reachable no-owner types and suppresses optional variants (compress /
		// fresh-read), which are only ever referenced through [LuminPackable] member graphs (owned
		// types) and are therefore never needed by a reachable no-owner formatter.
		IEnumerable<ITypeSymbol> sourceTypes = reachability is null
			? analysis.FormatterTypes.Where(type => !analysis.HasOwner(type))
			: reachability.ReachableNoOwnerTypes;

		foreach (ITypeSymbol type in sourceTypes
			.Where(static type => !ContainsTypeParameter(type))
			.Where(IsAotVisible)
			.Where(IsStaticFormatterCandidate)
			.OrderBy(static type => FormatterTypeName.Get(type), StringComparer.Ordinal))
		{
			GenerateFormatterExtensions(sb, new LuminLocalFieldData
			{
				TypeName = FormatterTypeName.Get(type),
				TypeSymbol = type,
				Name = "value",
				IsValue = type.IsValueType
			}, metaInfo, analyzedTypes, emitOptionalVariants: reachability is null);
		}

		AppendGenericDispatchExtensions(sb, compilation, metaInfo, GetExtensionClassName(compilation), reachability);

		return GenerateExtension(sb, GetExtensionClassName(compilation));
	}

	/// <summary>
	/// Emits the generic dispatch extensions (<c>WriteValue&lt;T&gt;</c>, <c>ReadValue&lt;T&gt;</c>,
	/// <c>CalculateOffset&lt;T&gt;</c> and their JSON counterparts). Each closed <c>T</c> carries its
	/// compact formatter id (0,1,2,3...) in <see cref="global::LuminPack.LuminPackTypeCache{T}"/>,
	/// written by this assembly's static constructor, and the generic method dispatches with a dense
	/// <c>switch(TypeId)</c> that the JIT lowers to a jump table. That replaces the old MethodTable
	/// lookup + frozen-map probe on the hot path. The <c>default</c> branch (TypeId still
	/// <see cref="uint.MaxValue"/>, i.e. no generated formatter) falls back to
	/// <see cref="global::LuminPack.Code.LuminPackFormatterRegistry"/> for types the generator did
	/// not cover (manual <c>LuminPackSerializer.Register</c>), calling the stored method pointer
	/// directly with the T-typed signature - no boxing.
	/// </summary>
	private static void AppendGenericDispatchExtensions(StringBuilder sb, Compilation compilation, MetaInfo metaInfo, string extensionClassName, ReachabilityAnalysis? reachability = null)
	{
		ITypeSymbol[] types = GetOrderedFormatterTypes(compilation, reachability);
		string scopedIn = metaInfo.IsNet8 ? "scoped in " : "in ";
		string scopedRef = metaInfo.IsNet8 ? "scoped ref " : "ref ";
		string unsafeAs = "global::System.Runtime.CompilerServices.Unsafe.As";
		string throwNoFormatter = "global::LuminPack.Code.LuminPackExceptionHelper.ThrowNoSourceGeneratedFormatter(typeof(T));";

		DispatchEntry[] dispatch = types
			.Select((type, index) => new DispatchEntry(
				index,
				FormatterTypeName.Get(type),
				CanRegisterFormatter(type, compilation, json: false),
				CanRegisterFormatter(type, compilation, json: true),
				CanRegisterCalculateFormatter(type, compilation)))
			.Where(static d => d.Binary || d.Json || d.Calculate)
			.ToArray();

		// The static ctor writes each per-type compact id before any generic dispatch extension can
		// run (the JIT runs a type's static ctor before its static methods). uint.MaxValue stays the
		// "not generated" sentinel, so ids can be dense 0,1,2,3... for a JIT jump table.
		sb.AppendLine("        static " + extensionClassName + "()");
		sb.AppendLine("        {");
		foreach (DispatchEntry d in dispatch)
		{
			sb.AppendLine("            global::LuminPack.LuminPackTypeCache<" + d.Name + ">.TypeId = " + d.Id + "u;");
		}
		sb.AppendLine("        }");
		sb.AppendLine();

		AppendGenericSwitch(sb, dispatch, "WriteValue", "ref this LuminPackWriter writer", scopedIn, "T value", metaInfo, op: "write", isJson: false, unsafeAs, throwNoFormatter);
		AppendGenericSwitch(sb, dispatch, "ReadValue", "ref this LuminPackReader reader", scopedRef, "T value", metaInfo, op: "read", isJson: false, unsafeAs, throwNoFormatter);
		AppendGenericSwitch(sb, dispatch, "CalculateOffset", "ref this LuminPackEvaluator evaluator", scopedRef, "T value", metaInfo, op: "calculate", isJson: false, unsafeAs, throwNoFormatter);
		AppendGenericSwitch(sb, dispatch, "WriteValue", "ref this global::LuminPack.Core.LuminPackJsonWriter writer", scopedIn, "T value", metaInfo, op: "write", isJson: true, unsafeAs, throwNoFormatter);
		AppendGenericSwitch(sb, dispatch, "ReadValue", "ref this global::LuminPack.Core.LuminPackJsonReader reader", scopedRef, "T value", metaInfo, op: "read", isJson: true, unsafeAs, throwNoFormatter);
	}

	private static void AppendGenericSwitch(
		StringBuilder sb,
		DispatchEntry[] dispatch,
		string methodName,
		string receiver,
		string paramModifier,
		string param,
		MetaInfo metaInfo,
		string op,
		bool isJson,
		string unsafeAs,
		string throwNoFormatter)
	{
		string cacheType = "global::LuminPack.LuminPackTypeCache";
		string registryTryGet = "global::LuminPack.Code.LuminPackFormatterRegistry.TryGetValue(global::LuminPack.Code.LuminPackMarshal.GetMethodTable(typeof(T)), out var __entry)";
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine("        public static void " + methodName + "<T>(" + receiver + ", " + paramModifier + param + ")");
		sb.AppendLine("        {");
		sb.AppendLine("            switch (" + cacheType + "<T>.TypeId)");
		sb.AppendLine("            {");
		foreach (DispatchEntry d in dispatch)
		{
			bool supported = op switch
			{
				"write" => isJson ? d.Json : d.Binary,
				"read" => isJson ? d.Json : d.Binary,
				"calculate" => d.Calculate,
				_ => false
			};
			sb.AppendLine("                case " + d.Id + "u:");
			sb.AppendLine("                {");
			if (supported)
			{
				switch (op)
				{
					case "write":
						sb.AppendLine("                    var v = " + unsafeAs + "<T, " + d.Name + ">(ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in value)); writer.WriteValue(in v); return;");
						break;
					case "read":
						sb.AppendLine("                    " + d.Name + " v = default!; reader.ReadValue(ref v); value = " + unsafeAs + "<" + d.Name + ", T>(ref v); return;");
						break;
					case "calculate":
						sb.AppendLine("                    var v = " + unsafeAs + "<T, " + d.Name + ">(ref value); evaluator.CalculateOffset(ref v); return;");
						break;
				}
			}
			else
			{
				sb.AppendLine("                    " + throwNoFormatter);
				sb.AppendLine("                    return;");
			}
			sb.AppendLine("                }");
		}
		sb.AppendLine("                default:");
		if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
		{
			// Without AllowUnsafeBlocks the registered method pointers cannot be invoked, so the
			// fallback registry is not emitted at all.
			sb.AppendLine("                {");
			sb.AppendLine("                    " + throwNoFormatter);
			sb.AppendLine("                    return;");
			sb.AppendLine("                }");
		}
		else
		{
		sb.AppendLine("                {");
		sb.AppendLine("                    if (!" + registryTryGet + ")");
		sb.AppendLine("                    {");
		sb.AppendLine("                        " + throwNoFormatter);
		sb.AppendLine("                        return;");
		sb.AppendLine("                    }");
		switch (op)
		{
			case "write":
				if (isJson)
				{
					sb.AppendLine("                    if (__entry.WriteValueJson == 0) { " + throwNoFormatter + " }");
					sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonWriter, in T, void>)__entry.WriteValueJson)(ref writer, in value); } return;");
				}
				else
				{
					sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackWriter, in T, void>)__entry.WriteValue)(ref writer, in value); } return;");
				}
				break;
			case "read":
				if (isJson)
				{
					sb.AppendLine("                    if (__entry.ReadValueJson == 0) { " + throwNoFormatter + " }");
					sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref T, void>)__entry.ReadValueJson)(ref reader, ref value); } return;");
				}
				else
				{
					sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackReader, ref T, void>)__entry.ReadValue)(ref reader, ref value); } return;");
				}
				break;
			case "calculate":
				sb.AppendLine("                    if (__entry.CalculateOffset == 0) { " + throwNoFormatter + " }");
				sb.AppendLine("                    unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackEvaluator, in T, void>)__entry.CalculateOffset)(ref evaluator, in value); } return;");
				break;
		}
		sb.AppendLine("                }");
		}
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private sealed class DispatchEntry
	{
		public DispatchEntry(int id, string name, bool binary, bool json, bool calculate)
		{
			Id = id;
			Name = name;
			Binary = binary;
			Json = json;
			Calculate = calculate;
		}

		public int Id { get; }
		public string Name { get; }
		public bool Binary { get; }
		public bool Json { get; }
		public bool Calculate { get; }
	}


	internal static ITypeSymbol[] GetOrderedFormatterTypes(Compilation compilation)
		=> GetOrderedFormatterTypes(compilation, reachability: null);

	internal static ITypeSymbol[] GetOrderedFormatterTypes(Compilation compilation, ReachabilityAnalysis? reachability)
	{
		CompilationTypeAnalysis analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);

		// Full mode lists every formatter type in the project.  Prune tiers list exactly the emitted
		// set (emitted owned graphs plus the reachable no-owner complement); the generic dispatch
		// switch and the serializer entry overloads must reference exactly the types for which a
		// concrete extension method is emitted.
		IEnumerable<ITypeSymbol> types = reachability is null
			? analysis.FormatterTypes
			: reachability.EmittedTypes;

		return types
			.Where(static type => !ContainsTypeParameter(type))
			.Where(IsAotVisible)
			.Where(IsStaticFormatterCandidate)
			.OrderBy(static type => FormatterTypeName.Get(type), StringComparer.Ordinal)
			.ToArray();
	}


	private static bool CanRegisterFormatter(ITypeSymbol type, Compilation compilation, bool json)
	{
		if (type.TypeKind == TypeKind.Enum ||
			type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
		{
			return true;
		}

		if (type is INamedTypeSymbol named && HasPackableAttribute(named))
		{
			return SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly);
		}

		var formatter = CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(type));
		return json
			? formatter.WriteJson is not null && formatter.ReadJson is not null
			: formatter.Write is not null && formatter.Read is not null;
	}

	private static bool CanRegisterCalculateFormatter(ITypeSymbol type, Compilation compilation)
	{
		if (type.TypeKind == TypeKind.Enum ||
			type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
		{
			return true;
		}

		if (type is INamedTypeSymbol named && HasPackableAttribute(named))
		{
			return SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly);
		}

		return EvaluatorEmitterRegistry.GetEmitter(FormatterTypeName.Get(type)) is not null;
	}
	private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(LuminDataInfo data)
	{
		HashSet<ITypeSymbol> visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
		foreach (LuminDataField field in data.fields)
		{
			foreach (LuminLocalFieldData formatterField in EnumerateFormatterFields(field.TypeSymbol, visited))
			{
				yield return formatterField;
			}
		}
	}

	private static string GetExtensionClassName(Compilation compilation)
	{
		return "LuminPackExtensions_" + SanitizeAssemblyName(compilation.AssemblyName ?? "Assembly");
	}

	private static bool HasPackableAttribute(INamedTypeSymbol type)
	{
		return type.GetAttributes().Any(static attribute =>
			attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);
	}

	private static IEnumerable<LuminLocalFieldData> EnumerateFormatterFields(ITypeSymbol? type, HashSet<ITypeSymbol> visited)
	{
		if (type is null || type is ITypeParameterSymbol || !visited.Add(type))
		{
			yield break;
		}

		if (type is IArrayTypeSymbol array)
		{
			foreach (LuminLocalFieldData element in EnumerateFormatterFields(array.ElementType, visited))
			{
				yield return element;
			}
		}
		else if (type is INamedTypeSymbol named)
		{
			foreach (ITypeSymbol argument in named.TypeArguments)
			{
				foreach (LuminLocalFieldData argumentField in EnumerateFormatterFields(argument, visited))
				{
					yield return argumentField;
				}
			}

			// These LINQ formatters serialize through concrete interface views which may
			// never appear explicitly in user syntax. Keep their exact closed helper types
			// in the formatter dependency graph so nested calls can bind statically.
			string definitionName = named.OriginalDefinition.ToDisplayString();
			if (definitionName == "System.Linq.ILookup<TKey, TElement>" ||
				definitionName == "System.Linq.IGrouping<TKey, TElement>")
			{
				foreach (INamedTypeSymbol enumerable in named.AllInterfaces.Where(static candidate =>
					candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
				{
					foreach (LuminLocalFieldData dependency in EnumerateFormatterFields(enumerable, visited))
					{
						yield return dependency;
					}
				}
			}
		}

		yield return new LuminLocalFieldData
		{
			TypeName = FormatterTypeName.Get(type),
			TypeSymbol = type,
			Name = "value",
			IsValue = type.IsValueType
		};
	}

	internal static bool ContainsTypeParameter(ITypeSymbol? type)
	{
		if (type is null || type is ITypeParameterSymbol)
		{
			return type is ITypeParameterSymbol;
		}

		return type switch
		{
			IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
			INamedTypeSymbol named => ContainsTypeParameter(named.ContainingType) || named.TypeArguments.Any(ContainsTypeParameter),
			_ => false
		};
	}

	internal static bool IsAotVisible(ITypeSymbol type)
	{
		if (type is IArrayTypeSymbol array)
		{
			return IsAotVisible(array.ElementType);
		}

		if (type is not INamedTypeSymbol named || named.IsAnonymousType)
		{
			return !type.IsAnonymousType;
		}

		if (named.TypeArguments.Any(argument => !IsAotVisible(argument)))
		{
			return false;
		}

		for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
		{
			if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
			{
				return false;
			}
		}

		return true;
	}

	internal static bool IsStaticFormatterCandidate(ITypeSymbol type)
	{
		if (type.TypeKind == TypeKind.Enum)
		{
			return true;
		}

		if (type is INamedTypeSymbol
			{
				OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
				TypeArguments.Length: 1
			} nullable)
		{
			return IsStaticFormatterCandidate(nullable.TypeArguments[0]);
		}

		// Arrays are only known when their leaf graph is known. Treating every
		// source-mentioned array as serializable made Unity package types such as
		// InputControl[] leak into generated formatter sets.
		if (type is IArrayTypeSymbol array)
		{
			return IsStaticFormatterCandidate(array.ElementType);
		}

		if (type is not INamedTypeSymbol named)
		{
			return false;
		}
		if (named.IsStatic)
		{
			return false;
		}

		if (named.ContainingType is not null && !HasPackableAttribute(named))
		{
			return false;
		}

		if (HasPackableAttribute(named))
		{
			return true;
		}

		var formatter = CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(type));
		bool registered = formatter.Write is not null || formatter.Read is not null ||
			formatter.WriteJson is not null || formatter.ReadJson is not null;
		if (!registered)
		{
			return false;
		}

		// Registered generic formatters are templates. Emit an extension only for
		// a closed construction whose entire argument graph is itself known or
		// [LuminPackable].
		return !named.IsUnboundGenericType && named.TypeArguments.All(IsStaticFormatterCandidate);
	}

	private static void GenerateFormatterExtensions(
		StringBuilder sb,
		LuminLocalFieldData field,
		MetaInfo metaInfo,
		HashSet<string> analyzedTypes,
		bool emitOptionalVariants = true)
	{
		string typeName = field.TypeName;
		bool isEnum = field.TypeSymbol?.TypeKind == TypeKind.Enum;
		bool isNullable = field.TypeSymbol is INamedTypeSymbol nullableSymbol &&
			nullableSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

		if (isEnum)
		{
			if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
			{
				return;
			}

			(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter =
				(EnumEmitter.GenerateSerializeCode, EnumEmitter.GenerateDeserializeCode);
			(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) jsonFormatter =
				(EnumEmitter.GenerateJsonSerializeCode, EnumEmitter.GenerateJsonDeserializeCode);
			AppendBinaryFormatterExtension(sb, typeName, field, formatter, metaInfo);
			AppendJsonFormatterExtension(sb, typeName, field, jsonFormatter, metaInfo);
			AppendCalculateFormatterExtension(sb, typeName, field, EnumEmitter.GenerateCalculateOffsetCode, metaInfo);
			return;
		}

		if (isNullable)
		{
			if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
			{
				return;
			}

			(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter =
				(NullableEmitter.GenerateSerializeCode, NullableEmitter.GenerateDeserializeCode);
			(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) jsonFormatter =
				(NullableEmitter.GenerateJsonSerializeCode, NullableEmitter.GenerateJsonDeserializeCode);
			AppendBinaryFormatterExtension(sb, typeName, field, formatter, metaInfo);
			AppendJsonFormatterExtension(sb, typeName, field, jsonFormatter, metaInfo);
			AppendCalculateFormatterExtension(sb, typeName, field, NullableEmitter.GenerateCalculateOffsetCode, metaInfo);
			return;
		}

		var staticFormatter = CodeEmitterRegistry.GetEmitter(typeName);
		if ((staticFormatter.Write is null || staticFormatter.Read is null) &&
			(staticFormatter.WriteJson is null || staticFormatter.ReadJson is null))
		{
			return;
		}

		if (!TryAddAnalyzedType(analyzedTypes, "formatter:" + typeName))
		{
			return;
		}

		if (staticFormatter.Write is not null && staticFormatter.Read is not null)
		{
			AppendBinaryFormatterExtension(sb, typeName, field, (staticFormatter.Write, staticFormatter.Read), metaInfo);
			if (emitOptionalVariants)
			{
				var compressed = CodeEmitterRegistry.GetCompressEmitter(typeName);
				if (compressed.Item1 is not null && compressed.Item2 is not null)
				{
					GenerateWithCompressExtension(sb, typeName, field, compressed, metaInfo);
				}
				if (IsExactListType(typeName))
				{
					GenerateFreshListDeserializeExtension(sb, typeName, field, metaInfo);
				}
				else if (IsExactDictionaryType(typeName))
				{
					GenerateFreshDictionaryDeserializeExtension(sb, typeName, field, metaInfo);
				}
				else if (IsExactManagedArrayType(field.TypeSymbol))
				{
					GenerateFreshArrayDeserializeExtension(sb, typeName, field, metaInfo);
				}
			}
		}

		if (staticFormatter.WriteJson is not null && staticFormatter.ReadJson is not null)
		{
			AppendJsonFormatterExtension(sb, typeName, field, (staticFormatter.WriteJson, staticFormatter.ReadJson), metaInfo);
		}

		Action<LuminLocalFieldData, StringBuilder> evaluatorFormatter = EvaluatorEmitterRegistry.GetEmitter(typeName);
		if (evaluatorFormatter is not null)
		{
			AppendCalculateFormatterExtension(sb, typeName, field, evaluatorFormatter, metaInfo);
		}
	}

	private static bool IsAotFormatterReady(string typeName)
	{
		if (typeName.EndsWith("[]", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Nullable<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Generic.Dictionary<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Concurrent.ConcurrentDictionary<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Generic.IList<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Generic.ISet<", StringComparison.Ordinal) ||
			typeName.StartsWith("global::System.Collections.Generic.IReadOnlySet<", StringComparison.Ordinal))
		{
			return true;
		}

		return typeName is
			"string" or "bool" or "byte" or "sbyte" or "short" or "ushort" or
			"int" or "uint" or "long" or "ulong" or "float" or "double" or
			"decimal" or "char" or "nint" or "nuint" or
			"global::System.String" or "global::System.Boolean" or "global::System.Byte" or "global::System.SByte" or
			"global::System.Int16" or "global::System.UInt16" or "global::System.Int32" or "global::System.UInt32" or
			"global::System.Int64" or "global::System.UInt64" or "global::System.Single" or "global::System.Double" or
			"global::System.Decimal" or "global::System.Char" or "global::System.Guid" or "global::System.DateTime" or
			"global::System.DateTimeOffset" or "global::System.TimeSpan" or "global::System.Half" or
			"global::System.Int128" or "global::System.UInt128" or "global::System.DateOnly" or
			"global::System.TimeOnly" or "global::System.Text.Rune" or
			"global::System.Numerics.BigInteger" or "global::System.Uri" or
			"global::System.Version" or "global::System.Collections.BitArray" or
			"global::System.Text.StringBuilder" or "global::System.Globalization.CultureInfo" or
			"global::System.TimeZoneInfo" or "global::System.Type";
	}

	private static void AppendBinaryFormatterExtension(StringBuilder sb, string typeName, LuminLocalFieldData field,
		(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter,
		MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(ShouldAggressivelyInlineBinaryWrite(field)
			? "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]"
			: "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void WriteValue(ref this LuminPackWriter writer, scoped in " + typeName + " value)"
			: "        public static void WriteValue(ref this LuminPackWriter writer, in " + typeName + " value)");
		sb.AppendLine("        {");
		formatter.Serialize(field, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void ReadValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)"
			: "        public static void ReadValue(ref this LuminPackReader reader, ref " + typeName + " value)");
		sb.AppendLine("        {");
		formatter.Deserialize(field, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static bool ShouldAggressivelyInlineBinaryWrite(LuminLocalFieldData field)
	{
		if (field.TypeSymbol is IArrayTypeSymbol array)
		{
			if (array.Rank != 1)
			{
				return true;
			}

			// Keep the closed unmanaged bulk copy in its caller. Managed arrays contain
			// a loop plus null/growth slow paths, which are cheaper behind one call.
			return array.ElementType.IsUnmanagedType;
		}

		if (field.TypeSymbol is not INamedTypeSymbol named)
		{
			return true;
		}

		string namespaceName = named.ContainingNamespace?.ToDisplayString() ?? string.Empty;
		if (namespaceName == "System.Collections.Generic" && named.Name == "Dictionary" &&
			named.TypeArguments.Length == 2)
		{
			return false;
		}

		if (namespaceName == "System.Collections.Generic" && named.Name == "List" &&
			named.TypeArguments.Length == 1)
		{
			return named.TypeArguments[0].IsUnmanagedType;
		}

		return true;
	}

	private static void AppendJsonFormatterExtension(StringBuilder sb, string typeName, LuminLocalFieldData field,
		(Action<LuminLocalFieldData, StringBuilder> Serialize, Action<LuminLocalFieldData, StringBuilder> Deserialize) formatter,
		MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void WriteValue(ref this global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + typeName + " value)"
			: "        public static void WriteValue(ref this global::LuminPack.Core.LuminPackJsonWriter writer, in " + typeName + " value)");
		sb.AppendLine("        {");
		formatter.Serialize(field, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void ReadValue(ref this global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + " value)"
			: "        public static void ReadValue(ref this global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + " value)");
		sb.AppendLine("        {");
		formatter.Deserialize(field, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void AppendCalculateFormatterExtension(
		StringBuilder sb,
		string typeName,
		LuminLocalFieldData field,
		Action<LuminLocalFieldData, StringBuilder> formatter,
		MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void CalculateOffset(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, scoped ref " + typeName + " value)"
			: "        public static void CalculateOffset(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, ref " + typeName + " value)");
		sb.AppendLine("        {");
		formatter(field, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static string GetGlobalTypeName(LuminDataInfo data)
	{
		string typeName = data.classFullName;
		return typeName.IndexOf('.') < 0 && data.classNameSpace != "<global namespace>"
			? "global::" + data.classNameSpace + "." + typeName
			: typeName;
	}

	private static void GenerateRootBinaryExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
	{
		AppendRootMethodHeader(sb, "WriteValue", "LuminPackWriter", typeName, data, metaInfo, read: false);
		GenerateMyselfSerialize(data, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
		AppendRootMethodHeader(sb, "ReadValue", "LuminPackReader", typeName, data, metaInfo, read: true);
		if (data.isUnion)
		{
			if (data.isValueType)
			{
				GenerateUnionDeserializeStruct(sb, data);
			}
			else
			{
				GenerateUnionDeserialize(data, sb, metaInfo);
			}
		}
		else
		{
			GenerateMyselfDeserialize(data, sb);
		}
		sb.AppendLine("        }");
		sb.AppendLine();

		// A union header replaces the concrete object's normal object header. Keep the
		// original polymorphism path as a distinct generated overload so virtual union
		// dispatch never enters the normal root serializer/deserializer.
		if (!data.isUnion)
		{
			AppendRootMethodHeader(sb, "WritePolymorphismValue", "LuminPackWriter", typeName, data, metaInfo, read: false);
			GenerateMyselfSerialize(data, sb, polymorphism: true);
			sb.AppendLine("        }");
			sb.AppendLine();
			AppendRootMethodHeader(sb, "ReadPolymorphismValue", "LuminPackReader", typeName, data, metaInfo, read: true);
			GenerateMyselfDeserialize(data, sb, polymorphism: true);
			sb.AppendLine("        }");
			sb.AppendLine();
		}

		GenerateRootCalculateOffsetExtension(sb, data, typeName, metaInfo);
	}

	private static void GenerateRootCalculateOffsetExtension(
		StringBuilder sb,
		LuminDataInfo data,
		string typeName,
		MetaInfo metaInfo)
	{
		string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
		string nullable = data.isValueType ? string.Empty : "?";
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine("        public static void CalculateOffset" + genericParameters +
			"(ref this global::LuminPack.Core.LuminPackEvaluator evaluator, " +
			(metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + nullable + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		if (data.isUnion)
		{
			if (data.isValueType)
			{
				LuminPackUnionCodeGenerator.GenerateCalculateOffsetCodeStruct(data, sb, typeName);
			}
			else
			{
				LuminPackUnionCodeGenerator.GenerateCalculateOffsetCode(data, sb, typeName);
			}
		}
		else
		{
			switch (data.generatorType)
			{
				case GeneratorType.Object:
					LuminPackCodeGenerator.GenerateCalculateOffsetCode(data, sb);
					break;
				case GeneratorType.CircleReference:
					LuminPackCircleReferenceCodeGenerator.GenerateCalculateOffsetCode(data, sb, typeName);
					break;
				case GeneratorType.VersionTolerant:
					LuminPackVersionTolerantCodeGenerator.GenerateCalculateOffsetCode(data, sb, typeName);
					break;
			}
		}
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateRootJsonExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
	{
		if (data.isUnion)
		{
			if (data.isValueType)
			{
				GenerateUnionJsonExtensionsStruct(sb, data, typeName, metaInfo);
			}
			else
			{
				GenerateUnionJsonExtensions(sb, data, typeName, metaInfo);
			}
			return;
		}

		string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
		string helperName = "Json_" + SanitizeAssemblyName(typeName);
		string helperTypeName = helperName + genericParameters;
		string nullable = data.isValueType ? string.Empty : "?";
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + typeName + nullable + " value)"
			: "        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, in " + typeName + nullable + " value)");
		AppendGenericConstraints(sb, data);
		if (data.generatorType == GeneratorType.CircleReference)
		{
			// The existing circle-reference algorithm needs a writable reference for private
			// field access. Keep the public extension readonly and pass a local copy through.
			sb.AppendLine("        {");
			sb.AppendLine("            var writableValue = value;");
			sb.AppendLine("            " + helperTypeName + ".WriteValue(ref writer, ref writableValue);");
			sb.AppendLine("        }");
		}
		else
		{
			sb.AppendLine("            => " + helperTypeName + ".WriteValue(ref writer, in value);");
		}
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(metaInfo.IsNet8
			? "        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullable + " value)"
			: "        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullable + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("            => " + helperTypeName + ".ReadValue(ref reader, ref value);");
		sb.AppendLine();
		sb.AppendLine("        private static class " + helperTypeName);
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		if (data.generatorType == GeneratorType.CircleReference)
		{
			LuminPackJsonCodeGenerator.GenerateStaticUtf8Fields(sb, data);
			LuminPackJsonCircleReferenceCodeGenerator.GenerateStaticUtf8FieldsForCircleReference(sb);
			var circleMethods = new StringBuilder();
			LuminPackJsonCircleReferenceCodeGenerator.GenerateJsonSerializeWithCircleReference(circleMethods, data, typeName, metaInfo);
			circleMethods.AppendLine();
			LuminPackJsonCircleReferenceCodeGenerator.GenerateJsonDeserializeWithCircleReference(circleMethods, data, typeName, metaInfo);
			string nullableSuffix = data.isValueType ? string.Empty : "?";
			string methods = circleMethods.ToString()
				.Replace("public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + typeName + nullableSuffix + " value)",
				"public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + typeName + nullableSuffix + " value)")
				.Replace("public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + typeName + nullableSuffix + " value)",
				"public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + typeName + nullableSuffix + " value)")
				.Replace("public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullableSuffix + " value)",
				"public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + typeName + nullableSuffix + " value)")
				.Replace("public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullableSuffix + " value)",
				"public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + typeName + nullableSuffix + " value)");
			sb.Append(methods);
		}
		else
		{
			LuminPackJsonCodeGenerator.GenerateStaticUtf8Fields(sb, data);
			LuminPackJsonCodeGenerator.GenerateJsonSerialize(sb, data, typeName, metaInfo);
			sb.AppendLine();
			LuminPackJsonCodeGenerator.GenerateJsonDeserialize(sb, data, typeName, metaInfo);
		}
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateUnionSerialize(LuminDataInfo data, StringBuilder sb)
	{
		if (!data.isValueType)
		{
			sb.AppendLine("            if (value is null)");
			sb.AppendLine("            {");
			sb.AppendLine("                ref int offset = ref writer.GetCurrentSpanOffset();");
			sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
			sb.AppendLine("                writer.Advance(1);");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
		}

		int maxTag = data.UnionMembers.Count == 0 ? 0 : data.UnionMembers.Max(static member => member.Id);
		foreach (LuminUnionMemberInfo member in data.UnionMembers)
		{
			string memberType = GetUnionMemberType(data, member);
			var compatibility = GetUnionGenericCompatibility(data, member);
			string pattern = compatibility.RequiresCast
				? "(object)value is " + memberType + " member" + member.Id
				: "value is " + memberType + " member" + member.Id;
			string condition = string.IsNullOrEmpty(compatibility.Condition)
				? pattern
				: compatibility.Condition + " && " + pattern;
			sb.AppendLine("            if (" + condition + ")");
			sb.AppendLine("            {");
			sb.AppendLine(maxTag < 250 && !data.IsWideTag
				? "                writer.WriteUnionHeader(" + member.Id + ");"
				: "                writer.WriteWideUnionHeader(" + member.Id + ");");
			sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in member" + member.Id + ");");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
		}
		sb.AppendLine("            global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(value!.GetType(), typeof(" + data.classFullName + ")); ");
	}

	private static void GenerateUnionDeserialize(LuminDataInfo data, StringBuilder sb, MetaInfo metaInfo)
	{
		int maxTag = data.UnionMembers.Count == 0 ? 0 : data.UnionMembers.Max(static member => member.Id);
		sb.AppendLine(maxTag < 250 && !data.IsWideTag
			? "            if (!reader.TryPeekUnionHeader(out var tag))"
			: "            if (!reader.TryPeekWideUnionHeader(out var tag))");
		sb.AppendLine("            {");
		sb.AppendLine("                value = default;");
		sb.AppendLine("                return;");
		sb.AppendLine("            }");
		sb.AppendLine("            switch (tag)");
		sb.AppendLine("            {");
		foreach (LuminUnionMemberInfo member in data.UnionMembers)
		{
			string memberType = GetUnionMemberType(data, member);
			var compatibility = GetUnionGenericCompatibility(data, member);
			sb.AppendLine("                case " + member.Id + ":");
			sb.AppendLine("                {");
			sb.AppendLine("                    " + memberType + " member = default!;");
			sb.AppendLine("                    reader.ReadPolymorphismValue(ref member);");
			sb.AppendLine(compatibility.RequiresCast
				? "                    value = (" + data.classFullName + ")(object)member;"
				: "                    value = member;");
			sb.AppendLine("                    return;");
			sb.AppendLine("                }");
		}
		if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
		{
			sb.AppendLine("                default:");
			sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
			sb.AppendLine("                    return;");
		}
		else
		{
		sb.AppendLine("                default:");
		sb.AppendLine("                {");
		sb.AppendLine("                    if (" + data.classFullName + ".TryGetRegisteredFormatter(tag, out var __registered) && __registered.ReadValue != 0)");
		sb.AppendLine("                    {");
		sb.AppendLine("                        " + data.classFullName + " __registeredValue = default!;");
		sb.AppendLine("                        unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackReader, ref " + data.classFullName + ", void>)__registered.ReadValue)(ref reader, ref __registeredValue); }");
		sb.AppendLine("                        value = __registeredValue;");
		sb.AppendLine("                        return;");
		sb.AppendLine("                    }");
		sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
		sb.AppendLine("                    return;");
		sb.AppendLine("                }");
		}
		sb.AppendLine("            }");
	}

	/// <summary>
	/// Root binary deserialize for a .NET 11 union struct: read the tag, read the case value with the
	/// concrete <c>ReadValue</c>, then reconstruct the union through its single-parameter case constructor.
	/// </summary>
	private static void GenerateUnionDeserializeStruct(StringBuilder sb, LuminDataInfo data)
	{
		int maxTag = data.UnionMembers.Count == 0 ? 0 : data.UnionMembers.Max(static member => member.Id);
		sb.AppendLine(maxTag < 250 && !data.IsWideTag
			? "            if (!reader.TryPeekUnionHeader(out var tag))"
			: "            if (!reader.TryPeekWideUnionHeader(out var tag))");
		sb.AppendLine("            {");
		sb.AppendLine("                value = default;");
		sb.AppendLine("                return;");
		sb.AppendLine("            }");
		sb.AppendLine("            switch (tag)");
		sb.AppendLine("            {");
		foreach (LuminUnionMemberInfo member in data.UnionMembers)
		{
			string memberType = GetUnionMemberType(data, member);
			sb.AppendLine("                case " + member.Id + ":");
			sb.AppendLine("                {");
			sb.AppendLine("                    " + memberType + " member = default!;");
			sb.AppendLine("                    reader.ReadValue(ref member);");
			sb.AppendLine("                    value = new " + data.classFullName + "(member);");
			sb.AppendLine("                    return;");
			sb.AppendLine("                }");
		}
		sb.AppendLine("                default:");
		sb.AppendLine("                    global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + data.classFullName + ")); ");
		sb.AppendLine("                    return;");
		sb.AppendLine("            }");
	}

	private static void GenerateUnionJsonExtensions(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
	{
		string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
		string nullable = data.isValueType ? string.Empty : "?";
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, " + (metaInfo.IsNet8 ? "scoped in " : "in ") + typeName + nullable + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		LuminPackUnionCodeGenerator.GenerateSerializeJsonCode(data, sb, typeName);
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, " + (metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + nullable + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		sb.AppendLine("            if (reader.IsNull()) { value = default; return; }");
		sb.AppendLine("            reader.TryConsumeObjectStart();");
		sb.AppendLine("            ushort tag = 0;");
		sb.AppendLine("            while (reader.Read())");
		sb.AppendLine("            {");
		sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) return;");
		sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String) continue;");
		sb.AppendLine("                var property = reader.ReadString();");
		sb.AppendLine("                if (!reader.Read()) break;");
		sb.AppendLine("                if (property == \"$type\") { tag = (ushort)reader.ReadInt(); continue; }");
		sb.AppendLine("                if (property != \"$value\") { reader.Skip(); continue; }");
		sb.AppendLine("                switch (tag)");
		sb.AppendLine("                {");
		foreach (LuminUnionMemberInfo member in data.UnionMembers)
		{
			string memberType = GetUnionMemberType(data, member);
			var compatibility = GetUnionGenericCompatibility(data, member);
			sb.AppendLine("                    case " + member.Id + ":");
			sb.AppendLine("                    {");
			sb.AppendLine("                        " + memberType + " member = default!;");
			sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref member);");
			sb.AppendLine(compatibility.RequiresCast
				? "                        value = (" + data.classFullName + ")(object)member;"
				: "                        value = member;");
			sb.AppendLine("                        break;");
			sb.AppendLine("                    }");
		}
		if (!metaInfo.AllowUnsafe || metaInfo.RegisterMode == LuminPackRegisterMode.Disabled)
		{
			sb.AppendLine("                    default: global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + ")); break;");
		}
		else
		{
		sb.AppendLine("                    default:");
		sb.AppendLine("                    {");
		sb.AppendLine("                        if (" + typeName + ".TryGetRegisteredFormatter(tag, out var __registered) && __registered.ReadValueJson != 0)");
		sb.AppendLine("                        {");
		sb.AppendLine("                            " + typeName + " __registeredValue = default!;");
		sb.AppendLine("                            unsafe { ((delegate*<ref global::LuminPack.Core.LuminPackJsonReader, ref " + typeName + ", void>)__registered.ReadValueJson)(ref reader, ref __registeredValue); }");
		sb.AppendLine("                            value = __registeredValue;");
		sb.AppendLine("                            break;");
		sb.AppendLine("                        }");
		sb.AppendLine("                        global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + "));");
		sb.AppendLine("                        break;");
		sb.AppendLine("                    }");
		}
		sb.AppendLine("                }");
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateUnionJsonExtensionsStruct(StringBuilder sb, LuminDataInfo data, string typeName, MetaInfo metaInfo)
	{
		string genericParameters = data.isGeneric ? "<" + string.Join(", ", data.GenericParameters) + ">" : string.Empty;
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        public static void WriteValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonWriter writer, " + (metaInfo.IsNet8 ? "scoped in " : "in ") + typeName + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		LuminPackUnionCodeGenerator.GenerateSerializeJsonCodeStruct(data, sb, typeName);
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        public static void ReadValue" + genericParameters + "(ref this global::LuminPack.Core.LuminPackJsonReader reader, " + (metaInfo.IsNet8 ? "scoped ref " : "ref ") + typeName + " value)");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		sb.AppendLine("            if (reader.IsNull()) { value = default; return; }");
		sb.AppendLine("            reader.TryConsumeObjectStart();");
		sb.AppendLine("            ushort tag = 0;");
		sb.AppendLine("            while (reader.Read())");
		sb.AppendLine("            {");
		sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) return;");
		sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String) continue;");
		sb.AppendLine("                var property = reader.ReadString();");
		sb.AppendLine("                if (!reader.Read()) break;");
		sb.AppendLine("                if (property == \"$type\") { tag = (ushort)reader.ReadInt(); continue; }");
		sb.AppendLine("                if (property != \"$value\") { reader.Skip(); continue; }");
		sb.AppendLine("                switch (tag)");
		sb.AppendLine("                {");
		foreach (LuminUnionMemberInfo member in data.UnionMembers)
		{
			string memberType = GetUnionMemberType(data, member);
			sb.AppendLine("                    case " + member.Id + ":");
			sb.AppendLine("                    {");
			sb.AppendLine("                        " + memberType + " member = default!;");
			sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref member);");
			sb.AppendLine("                        value = new " + data.classFullName + "(member);");
			sb.AppendLine("                        break;");
			sb.AppendLine("                    }");
		}
		sb.AppendLine("                    default: global::LuminPack.Code.LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof(" + typeName + ")); break;");
		sb.AppendLine("                }");
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static string GetUnionMemberType(LuminDataInfo data, LuminUnionMemberInfo member)
	{
		if (!member.Type.IsUnboundGenericType)
		{
			return FormatterTypeName.Get(member.Type);
		}

		string namespacePrefix = member.Type.ContainingNamespace.IsGlobalNamespace
			? "global::"
			: "global::" + member.Type.ContainingNamespace.ToDisplayString() + ".";
		return namespacePrefix + member.Type.Name + "<" + string.Join(", ", data.GenericParameters) + ">";
	}

	private static (string Condition, bool RequiresCast) GetUnionGenericCompatibility(LuminDataInfo data, LuminUnionMemberInfo member)
	{
		if (!data.isGeneric || data.TypeSymbol is not INamedTypeSymbol root)
		{
			return (string.Empty, false);
		}

		INamedTypeSymbol matchedRoot = FindUnionRootMatch(member.Type, root.OriginalDefinition);
		if (matchedRoot is null || matchedRoot.TypeArguments.Length != data.GenericParameters.Count)
		{
			// Be conservative for a malformed/indirect generic union declaration: do not emit
			// an impossible pattern conversion against AbstractRoot<T>.
			return (string.Empty, true);
		}

		var conditions = new List<string>();
		for (int index = 0; index < matchedRoot.TypeArguments.Length; index++)
		{
			ITypeSymbol argument = matchedRoot.TypeArguments[index];
			if (argument is ITypeParameterSymbol parameter && parameter.Name == data.GenericParameters[index])
			{
				continue;
			}

			conditions.Add("typeof(" + data.GenericParameters[index] + ") == typeof(" +
				FormatterTypeName.Get(argument) + ")");
		}

		return (string.Join(" && ", conditions), conditions.Count != 0);
	}

	private static INamedTypeSymbol FindUnionRootMatch(INamedTypeSymbol member, INamedTypeSymbol root)
	{
		if (root.TypeKind == TypeKind.Interface)
		{
			return member.AllInterfaces.FirstOrDefault(candidate =>
				SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, root));
		}

		for (INamedTypeSymbol current = member; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, root))
			{
				return current;
			}
		}

		return null;
	}

	private static void AppendRootMethodHeader(StringBuilder sb, string methodName, string ioType, string typeName, LuminDataInfo data, MetaInfo metaInfo, bool read)
	{
		string parameters = string.Join(", ", data.GenericParameters);
		string nullable = data.isValueType ? string.Empty : "?";
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		string modifier = read ? "ref " : "in ";
		string scoped = metaInfo.IsNet8 ? "scoped " : string.Empty;
		sb.AppendLine("        public static void " + methodName + (data.isGeneric ? "<" + parameters + ">" : string.Empty) + "(ref this " + ioType + " " + (read ? "reader" : "writer") + ", " + scoped + modifier + typeName + nullable + " " + (read ? "value" : "input") + ")");
		AppendGenericConstraints(sb, data);
		sb.AppendLine("        {");
		if (!read)
		{
			sb.AppendLine("            var value = input;");
		}
	}

	internal static void AppendGenericConstraints(StringBuilder sb, LuminDataInfo data)
	{
		foreach (GenericParameterConstraint constraint in data.GenericConstraints)
		{
			List<string> parts = new List<string>();
			if (constraint.IsUnmanaged) parts.Add("unmanaged");
			if (constraint.IsClass) parts.Add("class");
			if (constraint.IsStruct) parts.Add("struct");
			if (constraint.IsNotNull) parts.Add("notnull");
			parts.AddRange(constraint.Constraints);
			if (constraint.HasNewConstructor) parts.Add("new()");
			if (constraint.HasDefault) parts.Add("default");
			if (parts.Count > 0) sb.AppendLine("            where " + constraint.ParameterName + " : " + string.Join(", ", parts));
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryAddAnalyzedType(HashSet<string> analyzedTypes, string typeName)
	{
		lock (analyzedTypes)
		{
			return analyzedTypes.Add(typeName);
		}
	}

	private static string GenerateExtension(StringBuilder sb, string extensionClassName = "LuminPackExtensions")
	{
		string assemblySuffix = SanitizeAssemblyName(extensionClassName.StartsWith("LuminPackExtensions_", StringComparison.Ordinal)
				? extensionClassName.Substring("LuminPackExtensions_".Length)
				: extensionClassName);
		string extensionType = "global::LuminPack.Generated.LuminPackExtensions_" + assemblySuffix;
		string value = sb.ToString()
			.Replace("global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in ", "writer.WriteValue(in ")
			.Replace("global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref ", "reader.ReadValue(ref ")
			.Replace("writer.WriteValueWithCompress(", extensionType + ".WriteValueWithCompress(ref writer, in ")
			.Replace("reader.ReadValueWithCompress(ref ", extensionType + ".ReadValueWithCompress(ref reader, ref ")
			.Replace("reader.ReadFreshValue(ref ", extensionType + ".ReadFreshValue(ref reader, ref ");
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("using global::System;");
		stringBuilder.AppendLine("using global::System.Collections.Generic;");
		stringBuilder.AppendLine("using global::System.Runtime.CompilerServices;");
		stringBuilder.AppendLine("using global::System.Runtime.InteropServices;");
		stringBuilder.AppendLine("using global::System.Threading.Tasks;");
		stringBuilder.AppendLine("using global::LuminPack;");
		stringBuilder.AppendLine("using global::LuminPack.Code;");
		stringBuilder.AppendLine("using global::LuminPack.Core;");
		stringBuilder.AppendLine("using global::LuminPack.Utility;");
		stringBuilder.AppendLine("using global::LuminPack.Attribute;");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("#nullable enable");
		stringBuilder.AppendLine("namespace LuminPack.Generated");
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine();
		// Formatter overloads are an assembly implementation detail. Keeping the partial
		// extension container internal prevents identical scalar overloads emitted by a
		// referenced contract assembly from participating in the consumer's overload set.
		stringBuilder.AppendLine("    internal static partial class " + extensionClassName);
		stringBuilder.AppendLine("    {");
		stringBuilder.Append(value);
		stringBuilder.AppendLine("    }");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static void GenerateWithCompressExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, (Action<LuminLocalFieldData, StringBuilder> ser, Action<LuminLocalFieldData, StringBuilder> deser) compress, MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValueWithCompress(ref this LuminPackWriter writer, scoped in " + typeName + " value)") : ("        public static void WriteValueWithCompress(ref this LuminPackWriter writer, in " + typeName + " value)"));
		sb.AppendLine("        {");
		compress.ser(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValueWithCompress(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadValueWithCompress(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		compress.deser(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateFreshListDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
	{
		bool inlineBulkRead = localData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol listType &&
		                      listType.TypeArguments.Length == 1 &&
		                      listType.TypeArguments[0].IsUnmanagedType;
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(inlineBulkRead
			? "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]"
			: "        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		ListEmitter.GenerateFreshDeserializeCode(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static bool IsExactListType(string typeName)
	{
		if (typeName.EndsWith(">", StringComparison.Ordinal))
		{
			if (!typeName.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal))
			{
				return typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	private static void GenerateFreshDictionaryDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		DictionaryEmitter.GenerateFreshDeserializeCode(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateFreshArrayDeserializeExtension(StringBuilder sb, string typeName, LuminLocalFieldData localData, MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.NoInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		ArrayEmitter.GenerateFreshDeserializeCode(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static bool IsExactManagedArrayType(ITypeSymbol typeSymbol)
	{
		return typeSymbol is IArrayTypeSymbol { Rank: 1 } array &&
		       !array.ElementType.IsUnmanagedType && !ContainsTypeParameter(array.ElementType);
	}

	private static bool IsExactDictionaryType(string typeName)
	{
		if (typeName.EndsWith(">", StringComparison.Ordinal))
		{
			if (!typeName.StartsWith("global::System.Collections.Generic.Dictionary<", StringComparison.Ordinal))
			{
				return typeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);
			}
			return true;
		}
		return false;
	}

	private static string? GetCollectionElementTypeName(string typeName)
	{
		if (typeName.EndsWith("[]"))
		{
			return typeName.Substring(0, typeName.Length - 2);
		}
		string firstGeneric = CodeEmitterRegistry.GetFirstGeneric(typeName);
		if (!string.IsNullOrEmpty(firstGeneric) && (typeName.Contains("List<") || typeName.Contains("List`")))
		{
			return firstGeneric;
		}
		return null;
	}

	private static void GenerateMyselfSerialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
	{
		if (data.isUnion)
		{
			if (data.isValueType)
			{
				LuminPackUnionCodeGenerator.GenerateSerializeCodeStruct(data, sb);
			}
			else
			{
				LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
			}
			return;
		}
		switch (data.generatorType)
		{
		case GeneratorType.Object:
			LuminPackCodeGenerator.GenerateSerializeCode(data, sb, extension: true, polymorphism);
			break;
		case GeneratorType.CircleReference:
			LuminPackCircleReferenceCodeGenerator.GenerateSerializeCode(data, sb);
			break;
		case GeneratorType.VersionTolerant:
			LuminPackVersionTolerantCodeGenerator.GenerateSerializeCode(data, sb);
			break;
		}
	}

	private static void GenerateMyselfDeserialize(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
	{
		if (data.isUnion)
		{
			if (data.isValueType)
			{
				LuminPackUnionCodeGenerator.GenerateDeserializeCodeStruct(data, sb);
			}
			else
			{
				LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
			}
			return;
		}
		switch (data.generatorType)
		{
		case GeneratorType.Object:
			LuminPackCodeGenerator.GenerateDeserializeCode(data, sb, polymorphism);
			break;
		case GeneratorType.CircleReference:
			LuminPackCircleReferenceCodeGenerator.GenerateDeserializeCode(data, sb);
			break;
		case GeneratorType.VersionTolerant:
			LuminPackVersionTolerantCodeGenerator.GenerateDeserializeCode(data, sb);
			break;
		}
	}

	internal static string SanitizeAssemblyName(string assemblyName)
	{
		if (string.IsNullOrEmpty(assemblyName))
		{
			return "Unknown";
		}
		StringBuilder stringBuilder = new StringBuilder(assemblyName.Length);
		for (int i = 0; i < assemblyName.Length; i++)
		{
			char c = assemblyName[i];
			if (i == 0)
			{
				if (char.IsLetter(c) || c == '_')
				{
					stringBuilder.Append(c);
				}
				else if (char.IsDigit(c))
				{
					stringBuilder.Append('_').Append(c);
				}
				else
				{
					stringBuilder.Append('_');
				}
			}
			else if (char.IsLetterOrDigit(c) || c == '_')
			{
				stringBuilder.Append(c);
			}
			else
			{
				stringBuilder.Append('_');
			}
		}
		return stringBuilder.ToString();
	}

	}
