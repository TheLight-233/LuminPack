using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.Formatter;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static class LuminPackExtensionGenerator
{
    public static ConditionalWeakTable<Compilation, HashSet<string>> AnalyzedTypes = new();
    
    private static readonly Dictionary<string, (string[] TypeKeys, bool IsArray)> KnownTypesMap = new()
    {
        // 基础类型
        ["sbyte"] = (new[] { "sbyte", "global::System.SByte" }, false),
        ["sbyte[]"] = (new[] { "sbyte[]", "global::System.SByte[]" }, true),
        ["byte"] = (new[] { "byte", "global::System.Byte" }, false),
        ["byte[]"] = (new[] { "byte[]", "global::System.Byte[]" }, true),
        ["short"] = (new[] { "short", "global::System.Int16" }, false),
        ["short[]"] = (new[] { "short[]", "global::System.Int16[]" }, true),
        ["ushort"] = (new[] { "ushort", "global::System.UInt16" }, false),
        ["ushort[]"] = (new[] { "ushort[]", "global::System.UInt16[]" }, true),
        ["int"] = (new[] { "int", "global::System.Int32" }, false),
        ["int[]"] = (new[] { "int[]", "global::System.Int32[]" }, true),
        ["uint"] = (new[] { "uint", "global::System.UInt32" }, false),
        ["uint[]"] = (new[] { "uint[]", "global::System.UInt32[]" }, true),
        ["long"] = (new[] { "long", "global::System.Int64" }, false),
        ["long[]"] = (new[] { "long[]", "global::System.Int64[]" }, true),
        ["ulong"] = (new[] { "ulong", "global::System.UInt64" }, false),
        ["ulong[]"] = (new[] { "ulong[]", "global::System.UInt64[]" }, true),
        ["nint"] = (new[] { "nint", "global::System.IntPtr" }, false),
        ["nint[]"] = (new[] { "nint[]", "global::System.IntPtr[]" }, true),
        ["nuint"] = (new[] { "nuint", "global::System.UIntPtr" }, false),
        ["nuint[]"] = (new[] { "nuint[]", "global::System.UIntPtr[]" }, true),
        ["float"] = (new[] { "float", "global::System.Single" }, false),
        ["float[]"] = (new[] { "float[]", "global::System.Single[]" }, true),
        ["double"] = (new[] { "double", "global::System.Double" }, false),
        ["double[]"] = (new[] { "double[]", "global::System.Double[]" }, true),
        ["decimal"] = (new[] { "decimal", "global::System.Decimal" }, false),
        ["decimal[]"] = (new[] { "decimal[]", "global::System.Decimal[]" }, true),
        ["bool"] = (new[] { "bool", "global::System.Boolean" }, false),
        ["bool[]"] = (new[] { "bool[]", "global::System.Boolean[]" }, true),
        ["char"] = (new[] { "char", "global::System.Char" }, false),
        ["char[]"] = (new[] { "char[]", "global::System.Char[]" }, true),
        
        // 字符串类型
        ["string"] = (new[] { "string", "global::System.String" }, false),
        ["string[]"] = (new[] { "string[]", "global::System.String[]" }, true),
        
        // 系统类型
        ["global::System.Guid"] = (new[] { "global::System.Guid" }, false),
        ["global::System.Guid[]"] = (new[] { "global::System.Guid[]" }, true),
        ["global::System.DateTime"] = (new[] { "global::System.DateTime" }, false),
        ["global::System.DateTime[]"] = (new[] { "global::System.DateTime[]" }, true),
        ["global::System.DateTimeOffset"] = (new[] { "global::System.DateTimeOffset" }, false),
        ["global::System.DateTimeOffset[]"] = (new[] { "global::System.DateTimeOffset[]" }, true),
        ["global::System.TimeSpan"] = (new[] { "global::System.TimeSpan" }, false),
        ["global::System.TimeSpan[]"] = (new[] { "global::System.TimeSpan[]" }, true),
        ["global::System.Numerics.Complex"] = (new[] { "global::System.Numerics.Complex" }, false),
        ["global::System.Numerics.Complex[]"] = (new[] { "global::System.Numerics.Complex[]" }, true),
        ["global::System.Numerics.Plane"] = (new[] { "global::System.Numerics.Plane" }, false),
        ["global::System.Numerics.Plane[]"] = (new[] { "global::System.Numerics.Plane[]" }, true),
        ["global::System.Numerics.Quaternion"] = (new[] { "global::System.Numerics.Quaternion" }, false),
        ["global::System.Numerics.Quaternion[]"] = (new[] { "global::System.Numerics.Quaternion[]" }, true),
        ["global::System.Numerics.Matrix3x2"] = (new[] { "global::System.Numerics.Matrix3x2" }, false),
        ["global::System.Numerics.Matrix3x2[]"] = (new[] { "global::System.Numerics.Matrix3x2[]" }, true),
        ["global::System.Numerics.Matrix4x4"] = (new[] { "global::System.Numerics.Matrix4x4" }, false),
        ["global::System.Numerics.Matrix4x4[]"] = (new[] { "global::System.Numerics.Matrix4x4[]" }, true),
        ["global::System.Numerics.Vector2"] = (new[] { "global::System.Numerics.Vector2" }, false),
        ["global::System.Numerics.Vector2[]"] = (new[] { "global::System.Numerics.Vector2[]" }, true),
        ["global::System.Numerics.Vector3"] = (new[] { "global::System.Numerics.Vector3" }, false),
        ["global::System.Numerics.Vector3[]"] = (new[] { "global::System.Numerics.Vector3[]" }, true),
        ["global::System.Numerics.Vector4"] = (new[] { "global::System.Numerics.Vector4" }, false),
        ["global::System.Numerics.Vector4[]"] = (new[] { "global::System.Numerics.Vector4[]" }, true),
        
        // 特殊类型
        ["global::System.BitArray"] = (new[] { "global::System.BitArray" }, false),
        ["global::System.BitArray[]"] = (new[] { "global::System.BitArray[]" }, true),
        ["global::System.Globalization.CultureInfo"] = (new[] { "global::System.Globalization.CultureInfo" }, false),
        ["global::System.Globalization.CultureInfo[]"] = (new[] { "global::System.Globalization.CultureInfo[]" }, true),
        ["global::System.Uri"] = (new[] { "global::System.Uri" }, false),
        ["global::System.Uri[]"] = (new[] { "global::System.Uri[]" }, true),
        ["global::System.Version"] = (new[] { "global::System.Version" }, false),
        ["global::System.Version[]"] = (new[] { "global::System.Version[]" }, true),
        ["global::System.Numerics.BigInteger"] = (new[] { "global::System.Numerics.BigInteger" }, false),
        ["global::System.Numerics.BigInteger[]"] = (new[] { "global::System.Numerics.BigInteger[]" }, true),
    };

    // .NET 8特有类型
    private static readonly Dictionary<string, (string[] TypeKeys, bool IsArray)> Net8KnownTypesMap = new()
    {
        ["global::System.Half"] = (new[] { "global::System.Half" }, false),
        ["global::System.Half[]"] = (new[] { "global::System.Half[]" }, true),
        ["global::System.Int128"] = (new[] { "global::System.Int128" }, false),
        ["global::System.Int128[]"] = (new[] { "global::System.Int128[]" }, true),
        ["global::System.UInt128"] = (new[] { "global::System.UInt128" }, false),
        ["global::System.UInt128[]"] = (new[] { "global::System.UInt128[]" }, true),
        ["global::System.DateOnly"] = (new[] { "global::System.DateOnly" }, false),
        ["global::System.DateOnly[]"] = (new[] { "global::System.DateOnly[]" }, true),
        ["global::System.TimeOnly"] = (new[] { "global::System.TimeOnly" }, false),
        ["global::System.TimeOnly[]"] = (new[] { "global::System.TimeOnly[]" }, true),
        ["global::System.Text.Rune"] = (new[] { "global::System.Text.Rune" }, false),
        ["global::System.Text.Rune[]"] = (new[] { "global::System.Text.Rune[]" }, true),
    };
    
    public static string CodeGenerator(LuminDataInfo data, MetaInfo metaInfo, Compilation compilation)
    {
        StringBuilder sb = new StringBuilder();
        
        var analyzedTypes = AnalyzedTypes.GetOrCreateValue(compilation);
        
        HashSet<string> currentGenerationTypes = new HashSet<string>();
        
        sb.AppendLine();

        foreach (var v in data.localFields)
        {
            var formatter = FormatterDiscovery.GetFormatter(v.TypeName);
        
            if (formatter.Item1 != null && 
                formatter.Item2 != null &&
                analyzedTypes.Add(v.TypeName) && 
                currentGenerationTypes.Add(v.TypeName))
            {
                sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void WriteValue(ref this LuminPackWriter writer, scoped in {v.TypeName} value)"
                    : $"        public static void WriteValue(ref this LuminPackWriter writer, in {v.TypeName} value)");
                sb.AppendLine("        {");
                formatter.Item1(v, sb);
                sb.AppendLine("        }");
                sb.AppendLine();
            
                sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public static void ReadValue(ref this LuminPackReader reader, scoped ref {v.TypeName} value)"
                    : $"        public static void ReadValue(ref this LuminPackReader reader, ref {v.TypeName} value)");
                sb.AppendLine("        {");
                formatter.Item2(v, sb);
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }
        
        //GenerateKnownTypeExtensions(sb, analyzedTypes, currentGenerationTypes, metaInfo);
        
        #region 生成自己

        string classFullName = data.className + "Parser";
        string classGlobalName = data.classFullName;
        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for(var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }
            classFullName += ">";
        }

        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        if (analyzedTypes.Add(classGlobalName) && 
            currentGenerationTypes.Add(classGlobalName) &&
            !data.isGeneric)
        {
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            //sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine(metaInfo.IsNet8 
                ? $"        public static void WriteValue(ref this LuminPackWriter writer, scoped in {classGlobalName} value)"
                : $"        public static void WriteValue(ref this LuminPackWriter writer, in {classGlobalName} value)");
            sb.AppendLine("        {");
            GenerateMyselfSerialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();
            
            sb.AppendLine($"        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine(metaInfo.IsNet8 
                ? $"        public static void ReadValue(ref this LuminPackReader reader, scoped ref {classGlobalName} value)"
                : $"        public static void ReadValue(ref this LuminPackReader reader, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            GenerateMyselfDeserialize(data, sb);
            sb.AppendLine("        }");
            sb.AppendLine();

            if (data.generatorType is GeneratorType.Object)
            {
                LuminPackCodeGenerator.GenerateLocalClassStructure(sb, data, analyzedTypes);
                
                foreach (var filed in data.fields.Where(x => x.ClassFields.Count > 0))
                {
                    LuminPackCodeGenerator.GeneratorUnsafeAccessorMethod(sb, filed, filed.ClassFields, analyzedTypes);
                }
            }
        }

        #endregion
        
        return GenerateExtension(sb);
    }
    
    private static string GenerateExtension(StringBuilder sb)
    {
        var extensionCode = sb.ToString();
        
        if (string.IsNullOrEmpty(extensionCode))
            return string.Empty;
            
        StringBuilder fullCode = new StringBuilder();
        
        fullCode.AppendLine("using global::System;");
        fullCode.AppendLine("using global::System.Collections.Generic;");
        fullCode.AppendLine("using global::System.Runtime.CompilerServices;");
        fullCode.AppendLine("using global::System.Runtime.InteropServices;");
        fullCode.AppendLine("using global::System.Threading.Tasks;");
        fullCode.AppendLine("using global::LuminPack;");
        fullCode.AppendLine("using global::LuminPack.Data;");
        fullCode.AppendLine("using global::LuminPack.Code;");
        fullCode.AppendLine("using global::LuminPack.Core;");
        fullCode.AppendLine("using global::LuminPack.Parsers;");
        fullCode.AppendLine("using global::LuminPack.Utility;");
        fullCode.AppendLine("using global::LuminPack.Attribute;");
        fullCode.AppendLine();
        fullCode.AppendLine("#nullable enable");
        fullCode.AppendLine($"namespace {LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}");
        fullCode.AppendLine("{");
        fullCode.AppendLine();
        fullCode.AppendLine("    public static partial class LuminPackExtensions");
        fullCode.AppendLine("    {");
        fullCode.Append(extensionCode);
        fullCode.AppendLine("    }");
        fullCode.AppendLine("}");
        
        return fullCode.ToString();
    }
    
    private static void GenerateMyselfSerialize(LuminDataInfo data, StringBuilder sb)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateSerializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateSerializeCode(data, sb, true); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateSerializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateSerializeCode(data, sb); break;
        }
    }
    
    private static void GenerateMyselfDeserialize(LuminDataInfo data, StringBuilder sb)
    {
        if (data.isUnion)
        {
            LuminPackUnionCodeGenerator.GenerateDeserializeCode(data, sb);
            return;
        }
        
        switch (data.generatorType)
        {
            case GeneratorType.Object : LuminPackCodeGenerator.GenerateDeserializeCode(data, sb); break;
            case GeneratorType.CircleReference : LuminPackCircleReferenceCodeGenerator.GenerateDeserializeCode(data, sb); break;
            case GeneratorType.VersionTolerant : LuminPackVersionTolerantCodeGenerator.GenerateDeserializeCode(data, sb); break;
        }
    }
}
