using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Formatter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
	public static ConditionalWeakTable<Compilation, HashSet<string>> AnalyzedTypes = new ConditionalWeakTable<Compilation, HashSet<string>>();

	public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
	{
		StringBuilder stringBuilder = new StringBuilder();
		HashSet<string> orCreateValue = AnalyzedTypes.GetOrCreateValue(compilation);
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> hashSet2 = new HashSet<string>(StringComparer.Ordinal);
		stringBuilder.AppendLine();
		foreach (LuminLocalFieldData localField in data.localFields)
		{
			bool flag = false;
			foreach (string genericParameter in data.GenericParameters)
			{
				if (localField.TypeName.Contains(genericParameter))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			(Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) formatter = FormatterDiscovery.GetFormatter(localField.TypeName);
			if (formatter.Item1 != null && formatter.Item2 != null && TryAddAnalyzedType(orCreateValue, localField.TypeName) && hashSet.Add(localField.TypeName))
			{
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue(ref this LuminPackWriter writer, scoped in " + localField.TypeName + " value)") : ("        public static void WriteValue(ref this LuminPackWriter writer, in " + localField.TypeName + " value)"));
				stringBuilder.AppendLine("        {");
				formatter.Item1(localField, stringBuilder);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue(ref this LuminPackReader reader, scoped ref " + localField.TypeName + " value)") : ("        public static void ReadValue(ref this LuminPackReader reader, ref " + localField.TypeName + " value)"));
				stringBuilder.AppendLine("        {");
				formatter.Item2(localField, stringBuilder);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
				if (IsExactListType(localField.TypeName))
				{
					GenerateFreshListDeserializeExtension(stringBuilder, localField.TypeName, localField, metaInfo);
				}
				else if (IsExactDictionaryType(localField.TypeName))
				{
					GenerateFreshDictionaryDeserializeExtension(stringBuilder, localField.TypeName, localField, metaInfo);
				}
				(Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) compressFormatter = FormatterDiscovery.GetCompressFormatter(localField.TypeName);
				if (compressFormatter.Item1 != null && TryAddAnalyzedType(orCreateValue, "\0compress:" + localField.TypeName))
				{
					GenerateWithCompressExtension(stringBuilder, localField.TypeName, localField, compressFormatter, metaInfo);
					hashSet2.Add(localField.TypeName);
				}
			}
		}
		Queue<string> queue = new Queue<string>();
		foreach (string item in hashSet2)
		{
			string collectionElementTypeName = GetCollectionElementTypeName(item);
			if (collectionElementTypeName != null)
			{
				queue.Enqueue(collectionElementTypeName);
			}
		}
		HashSet<string> hashSet3 = new HashSet<string>(StringComparer.Ordinal);
		while (queue.Count > 0)
		{
			string text = queue.Dequeue();
			if (!hashSet3.Add(text))
			{
				continue;
			}
			LuminLocalFieldData luminLocalFieldData = new LuminLocalFieldData
			{
				TypeName = text,
				Name = "value"
			};
			(Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) formatter2 = FormatterDiscovery.GetFormatter(text);
			(Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) compressFormatter2 = FormatterDiscovery.GetCompressFormatter(text);
			if (formatter2.Item1 != null && formatter2.Item2 != null && TryAddAnalyzedType(orCreateValue, text) && hashSet.Add(text))
			{
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue(ref this LuminPackWriter writer, scoped in " + text + " value)") : ("        public static void WriteValue(ref this LuminPackWriter writer, in " + text + " value)"));
				stringBuilder.AppendLine("        {");
				formatter2.Item1(luminLocalFieldData, stringBuilder);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue(ref this LuminPackReader reader, scoped ref " + text + " value)") : ("        public static void ReadValue(ref this LuminPackReader reader, ref " + text + " value)"));
				stringBuilder.AppendLine("        {");
				formatter2.Item2(luminLocalFieldData, stringBuilder);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
				if (IsExactListType(text))
				{
					GenerateFreshListDeserializeExtension(stringBuilder, text, luminLocalFieldData, metaInfo);
				}
				else if (IsExactDictionaryType(text))
				{
					GenerateFreshDictionaryDeserializeExtension(stringBuilder, text, luminLocalFieldData, metaInfo);
				}
			}
			if (compressFormatter2.Item1 != null && hashSet2.Add(text) && TryAddAnalyzedType(orCreateValue, "\0compress:" + text))
			{
				GenerateWithCompressExtension(stringBuilder, text, luminLocalFieldData, compressFormatter2, metaInfo);
			}
			string collectionElementTypeName2 = GetCollectionElementTypeName(text);
			if (collectionElementTypeName2 != null)
			{
				queue.Enqueue(collectionElementTypeName2);
			}
		}
		string text2 = data.className + "Parser";
		string text3 = data.classFullName;
		if (data.isGeneric)
		{
			text2 = text2 + "<" + data.GenericParameters.FirstOrDefault();
			for (int i = 1; i < data.GenericParameters.Count; i++)
			{
				text2 = text2 + "," + data.GenericParameters[i];
			}
			text2 += ">";
		}
		string text4 = string.Join(", ", data.GenericParameters);
		if (!text3.Contains(".") && data.classNameSpace != "<global namespace>")
		{
			text3 = "global::" + data.classNameSpace + "." + data.classFullName;
		}
		if (TryAddAnalyzedType(orCreateValue, text3) && hashSet.Add(text3))
		{
			stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
			stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
			if (data.isGeneric)
			{
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue<" + text4 + ">(ref this LuminPackWriter writer, scoped in " + text3 + " value)") : ("        public static void WriteValue<" + text4 + ">(ref this LuminPackWriter writer, in " + text3 + " value)"));
				foreach (GenericParameterConstraint genericConstraint in data.GenericConstraints)
				{
					if (genericConstraint.IsUnmanaged || genericConstraint.IsClass || genericConstraint.IsStruct || genericConstraint.IsNotNull || genericConstraint.HasDefault || genericConstraint.HasNewConstructor || genericConstraint.Constraints.Count != 0)
					{
						stringBuilder.Append("            ");
						stringBuilder.Append("where ");
						stringBuilder.Append(genericConstraint.ParameterName);
						stringBuilder.Append(" : ");
						List<string> list = new List<string>();
						if (genericConstraint.IsUnmanaged)
						{
							list.Add("unmanaged");
						}
						if (genericConstraint.IsClass)
						{
							list.Add("class");
						}
						if (genericConstraint.IsStruct)
						{
							list.Add("struct");
						}
						if (genericConstraint.IsNotNull)
						{
							list.Add("notnull");
						}
						if (genericConstraint.HasNewConstructor)
						{
							list.Add("new()");
						}
						if (genericConstraint.HasDefault)
						{
							list.Add("default");
						}
						list.AddRange(genericConstraint.Constraints);
						stringBuilder.Append(string.Join(", ", list));
						stringBuilder.AppendLine();
					}
				}
			}
			else
			{
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue(ref this LuminPackWriter writer, scoped in " + text3 + " value)") : ("        public static void WriteValue(ref this LuminPackWriter writer, in " + text3 + " value)"));
			}
			stringBuilder.AppendLine("        {");
			GenerateMyselfSerialize(data, stringBuilder);
			stringBuilder.AppendLine("        }");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
			if (metaInfo.IsNet8 && metaInfo.AllowUnsafe)
			{
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.SkipLocalsInit]");
			}
			stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
			if (data.isGeneric)
			{
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue<" + text4 + ">(ref this LuminPackReader reader, scoped ref " + text3 + " value)") : ("        public static void ReadValue<" + text4 + ">(ref this LuminPackReader reader, ref " + text3 + " value)"));
				foreach (GenericParameterConstraint genericConstraint2 in data.GenericConstraints)
				{
					if (genericConstraint2.IsUnmanaged || genericConstraint2.IsClass || genericConstraint2.IsStruct || genericConstraint2.IsNotNull || genericConstraint2.HasDefault || genericConstraint2.HasNewConstructor || genericConstraint2.Constraints.Count != 0)
					{
						stringBuilder.Append("            ");
						stringBuilder.Append("where ");
						stringBuilder.Append(genericConstraint2.ParameterName);
						stringBuilder.Append(" : ");
						List<string> list2 = new List<string>();
						if (genericConstraint2.IsUnmanaged)
						{
							list2.Add("unmanaged");
						}
						if (genericConstraint2.IsClass)
						{
							list2.Add("class");
						}
						if (genericConstraint2.IsStruct)
						{
							list2.Add("struct");
						}
						if (genericConstraint2.IsNotNull)
						{
							list2.Add("notnull");
						}
						if (genericConstraint2.HasNewConstructor)
						{
							list2.Add("new()");
						}
						if (genericConstraint2.HasDefault)
						{
							list2.Add("default");
						}
						list2.AddRange(genericConstraint2.Constraints);
						stringBuilder.Append(string.Join(", ", list2));
						stringBuilder.AppendLine();
					}
				}
			}
			else
			{
				stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue(ref this LuminPackReader reader, scoped ref " + text3 + " value)") : ("        public static void ReadValue(ref this LuminPackReader reader, ref " + text3 + " value)"));
			}
			stringBuilder.AppendLine("        {");
			GenerateMyselfDeserialize(data, stringBuilder);
			stringBuilder.AppendLine("        }");
			stringBuilder.AppendLine();
			if (!data.isUnion)
			{
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				if (data.isGeneric)
				{
					stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WritePolymorphismValue<" + text4 + ">(ref this LuminPackWriter writer, scoped in " + text3 + " value)") : ("        public static void WritePolymorphismValue<" + text4 + ">(ref this LuminPackWriter writer, in " + text3 + " value)"));
					foreach (GenericParameterConstraint genericConstraint3 in data.GenericConstraints)
					{
						if (genericConstraint3.IsUnmanaged || genericConstraint3.IsClass || genericConstraint3.IsStruct || genericConstraint3.IsNotNull || genericConstraint3.HasDefault || genericConstraint3.HasNewConstructor || genericConstraint3.Constraints.Count != 0)
						{
							stringBuilder.Append("            ");
							stringBuilder.Append("where ");
							stringBuilder.Append(genericConstraint3.ParameterName);
							stringBuilder.Append(" : ");
							List<string> list3 = new List<string>();
							if (genericConstraint3.IsUnmanaged)
							{
								list3.Add("unmanaged");
							}
							if (genericConstraint3.IsClass)
							{
								list3.Add("class");
							}
							if (genericConstraint3.IsStruct)
							{
								list3.Add("struct");
							}
							if (genericConstraint3.IsNotNull)
							{
								list3.Add("notnull");
							}
							if (genericConstraint3.HasNewConstructor)
							{
								list3.Add("new()");
							}
							if (genericConstraint3.HasDefault)
							{
								list3.Add("default");
							}
							list3.AddRange(genericConstraint3.Constraints);
							stringBuilder.Append(string.Join(", ", list3));
							stringBuilder.AppendLine();
						}
					}
				}
				else
				{
					stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void WritePolymorphismValue(ref this LuminPackWriter writer, scoped in " + text3 + " value)") : ("        public static void WritePolymorphismValue(ref this LuminPackWriter writer, in " + text3 + " value)"));
				}
				stringBuilder.AppendLine("        {");
				GenerateMyselfSerialize(data, stringBuilder, polymorphism: true);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				if (metaInfo.IsNet8 && metaInfo.AllowUnsafe)
				{
					stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.SkipLocalsInit]");
				}
				stringBuilder.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				if (data.isGeneric)
				{
					stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadPolymorphismValue<" + text4 + ">(ref this LuminPackReader reader, scoped ref " + text3 + " value)") : ("        public static void ReadPolymorphismValue<" + text4 + ">(ref this LuminPackReader reader, ref " + text3 + " value)"));
					foreach (GenericParameterConstraint genericConstraint4 in data.GenericConstraints)
					{
						if (genericConstraint4.IsUnmanaged || genericConstraint4.IsClass || genericConstraint4.IsStruct || genericConstraint4.IsNotNull || genericConstraint4.HasDefault || genericConstraint4.HasNewConstructor || genericConstraint4.Constraints.Count != 0)
						{
							stringBuilder.Append("            ");
							stringBuilder.Append("where ");
							stringBuilder.Append(genericConstraint4.ParameterName);
							stringBuilder.Append(" : ");
							List<string> list4 = new List<string>();
							if (genericConstraint4.IsUnmanaged)
							{
								list4.Add("unmanaged");
							}
							if (genericConstraint4.IsClass)
							{
								list4.Add("class");
							}
							if (genericConstraint4.IsStruct)
							{
								list4.Add("struct");
							}
							if (genericConstraint4.IsNotNull)
							{
								list4.Add("notnull");
							}
							if (genericConstraint4.HasNewConstructor)
							{
								list4.Add("new()");
							}
							if (genericConstraint4.HasDefault)
							{
								list4.Add("default");
							}
							list4.AddRange(genericConstraint4.Constraints);
							stringBuilder.Append(string.Join(", ", list4));
							stringBuilder.AppendLine();
						}
					}
				}
				else
				{
					stringBuilder.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadPolymorphismValue(ref this LuminPackReader reader, scoped ref " + text3 + " value)") : ("        public static void ReadPolymorphismValue(ref this LuminPackReader reader, ref " + text3 + " value)"));
				}
				stringBuilder.AppendLine("        {");
				GenerateMyselfDeserialize(data, stringBuilder, polymorphism: true);
				stringBuilder.AppendLine("        }");
				stringBuilder.AppendLine();
			}
			lock (orCreateValue)
			{
				LuminPackCodeGenerator.GenerateLocalClassStructure(stringBuilder, data, orCreateValue);
				foreach (LuminDataField item2 in data.fields.Where((LuminDataField x) => x.ClassFields.Count > 0))
				{
					LuminPackCodeGenerator.GeneratorUnsafeAccessorMethod(stringBuilder, item2, item2.ClassFields, orCreateValue);
				}
			}
		}
		return GenerateExtension(stringBuilder);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryAddAnalyzedType(HashSet<string> analyzedTypes, string typeName)
	{
		lock (analyzedTypes)
		{
			return analyzedTypes.Add(typeName);
		}
	}

	private static string GenerateExtension(StringBuilder sb)
	{
		string value = sb.ToString();
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
		stringBuilder.AppendLine("using global::LuminPack.Parsers;");
		stringBuilder.AppendLine("using global::LuminPack.Utility;");
		stringBuilder.AppendLine("using global::LuminPack.Attribute;");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("#nullable enable");
		stringBuilder.AppendLine("namespace LuminPack.Generated");
		stringBuilder.AppendLine("{");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("    public static partial class LuminPackExtensions");
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
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		ListFormatter.GenerateFreshDeserializeCode(localData, sb);
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
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
		sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadFreshValue(ref this LuminPackReader reader, scoped ref " + typeName + " value)") : ("        public static void ReadFreshValue(ref this LuminPackReader reader, ref " + typeName + " value)"));
		sb.AppendLine("        {");
		DictionaryFormatter.GenerateFreshDeserializeCode(localData, sb);
		sb.AppendLine("        }");
		sb.AppendLine();
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
		string firstGeneric = FormatterDiscovery.GetFirstGeneric(typeName);
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
			LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
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
			LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
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

	private static string SanitizeAssemblyName(string assemblyName)
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

	public static string GenerateParsersRegistry(Compilation compilation)
	{
		if (string.IsNullOrEmpty(compilation.AssemblyName))
		{
			return string.Empty;
		}
		string text = SanitizeAssemblyName(compilation.AssemblyName);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("using global::System;");
		stringBuilder.AppendLine("using global::System.Collections.Generic;");
		stringBuilder.AppendLine("using global::System.Runtime.CompilerServices;");
		stringBuilder.AppendLine("using global::LuminPack;");
		stringBuilder.AppendLine();
		if (text != "Unknown")
		{
			stringBuilder.AppendLine("namespace LuminPackRegisters." + text);
			stringBuilder.AppendLine("{");
		}
		stringBuilder.AppendLine("    public static class GeneratedParsersRegistry");
		stringBuilder.AppendLine("    {");
		stringBuilder.AppendLine(GenerateParserTypeList(compilation, text));
		stringBuilder.AppendLine("    }");
		if (text != "Unknown")
		{
			stringBuilder.AppendLine("}");
		}
		return stringBuilder.ToString();
	}

	internal static string GenerateParserTypeList(Compilation compilation, string sanitizedAssemblyName)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("        public static readonly List<(Type TargetType, Type ParserType)> ParserTypes = new()");
		stringBuilder.AppendLine("        {");
		foreach (INamedTypeSymbol luminPackableType in GetLuminPackableTypes(compilation))
		{
			if (luminPackableType != null && TypeMetaChecker.CheckGeneratorType(luminPackableType) != GeneratorType.NonGenerator)
			{
				string text = BuildCorrectOpenGenericTypeName(luminPackableType);
				string text2 = BuildCorrectOpenGenericParserTypeName(luminPackableType);
				stringBuilder.AppendLine("            (typeof(" + text + "), typeof(global::LuminPack.Generated." + text2 + ")),");
			}
		}
		stringBuilder.AppendLine("        };");
		return stringBuilder.ToString();
	}

	private static string BuildCorrectOpenGenericTypeName(INamedTypeSymbol typeSymbol)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("global::");
		INamespaceSymbol containingNamespace = ((ISymbol)typeSymbol).ContainingNamespace;
		string text = ((containingNamespace != null) ? ((ISymbol)containingNamespace).ToDisplayString((SymbolDisplayFormat)null) : null);
		if (!string.IsNullOrEmpty(text) && text != "<global namespace>")
		{
			stringBuilder.Append(text);
			stringBuilder.Append(".");
		}
		List<string> list = new List<string>();
		for (INamedTypeSymbol val = typeSymbol; val != null; val = ((ISymbol)val).ContainingType)
		{
			string text2 = ((ISymbol)val).Name;
			if (val.IsGenericType)
			{
				int length = val.TypeParameters.Length;
				text2 += ((length == 1) ? "<>" : ("<" + new string(',', length - 1) + ">"));
			}
			list.Insert(0, text2);
		}
		stringBuilder.Append(string.Join(".", list));
		return stringBuilder.ToString();
	}

	private static string BuildCorrectOpenGenericParserTypeName(INamedTypeSymbol typeSymbol)
	{
		StringBuilder stringBuilder = new StringBuilder();
		INamespaceSymbol containingNamespace = ((ISymbol)typeSymbol).ContainingNamespace;
		string text = ((containingNamespace != null) ? ((ISymbol)containingNamespace).ToDisplayString((SymbolDisplayFormat)null) : null);
		if (!string.IsNullOrEmpty(text) && text != "<global namespace>")
		{
			stringBuilder.Append(text);
			stringBuilder.Append("_");
		}
		List<string> list = new List<string>();
		for (INamedTypeSymbol val = typeSymbol; val != null; val = ((ISymbol)val).ContainingType)
		{
			list.Insert(0, ((ISymbol)val).Name);
		}
		stringBuilder.Append(string.Join("_", list));
		stringBuilder.Append("Parser");
		if (typeSymbol.IsGenericType)
		{
			int length = typeSymbol.TypeParameters.Length;
			stringBuilder.Append((length == 1) ? "<>" : ("<" + new string(',', length - 1) + ">"));
		}
		return stringBuilder.ToString();
	}

	private static IEnumerable<INamedTypeSymbol> GetLuminPackableTypes(Compilation compilation)
	{
		INamedTypeSymbol luminPackableAttribute = compilation.GetTypeByMetadataName("LuminPack.Attribute.LuminPackableAttribute");
		if (luminPackableAttribute == null)
		{
			yield break;
		}
		foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
		{
			SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, false);
			IEnumerable<TypeDeclarationSyntax> enumerable = from syntax in syntaxTree.GetRoot(default(CancellationToken)).DescendantNodes((Func<SyntaxNode, bool>)null, false).OfType<TypeDeclarationSyntax>()
				where (syntax is ClassDeclarationSyntax || syntax is StructDeclarationSyntax || syntax is InterfaceDeclarationSyntax || syntax is RecordDeclarationSyntax) ? true : false
				select syntax;
			foreach (TypeDeclarationSyntax item in enumerable)
			{
				ISymbol declaredSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, (SyntaxNode)(object)item, default(CancellationToken));
				INamedTypeSymbol val = (INamedTypeSymbol)(object)((declaredSymbol is INamedTypeSymbol) ? declaredSymbol : null);
				if (val != null && ((ISymbol)val).GetAttributes().Any((AttributeData attr) => SymbolEqualityComparer.Default.Equals((ISymbol)(object)attr.AttributeClass, (ISymbol)(object)luminPackableAttribute)))
				{
					yield return val;
				}
			}
		}
	}
}
