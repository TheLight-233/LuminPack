using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static class LuminPackUnionCodeGenerator
{
    public static void UnionCodeGenerator(StringBuilder sb, LuminDataInfo data, MetaInfo metaInfo)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = data.className + "Parser";
        string classGlobalName = data.classFullName;
        string parserName = data.className + "Parser";
        
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
        
        sb.AppendLine($"        static {parserName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new {classFullName}());");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new ArrayParser<{classGlobalName}>());");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine($"        static readonly System.Collections.Generic.Dictionary<Type, ushort> _types = new({data.UnionMembers.Count})");
        sb.AppendLine("        {");
        foreach (var member in data.UnionMembers)
        {
            string memberType;
            if (member.Type.IsUnboundGenericType)
            {
                string nameSpaceName = member.Type.ContainingNamespace?.ToDisplayString() ?? "";
                if (nameSpaceName == "") 
                    memberType = member.Type.Name;
                else 
                    memberType = nameSpaceName + "." + member.Type.Name;
                memberType += $"<{data.GenericParameters.FirstOrDefault()}";
                for(var i = 1; i < data.GenericParameters.Count; i++)
                {
                    memberType += "," + data.GenericParameters[i];
                }
                memberType += ">";
            }
            else
                memberType = member.Type.ToDisplayString();
            
            sb.AppendLine($"            {{ typeof(global::{memberType}), {member.Id} }},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void Serialize(ref LuminPackWriter writer, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void Serialize(ref LuminPackWriter writer, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
            sb.AppendLine("                offset += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            if (_types.TryGetValue(value.GetType(), out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteUnionHeader(ref offset, tag);");
        sb.AppendLine();
        sb.AppendLine("                switch(tag)");
        sb.AppendLine("                {");
        
        foreach (var member in data.UnionMembers)
        {
            var localType = classGlobalName;
            
            string memberType;
            if (member.Type.IsUnboundGenericType)
            {
                string nameSpaceName = member.Type.ContainingNamespace?.ToDisplayString() ?? "";
                if (nameSpaceName == "") 
                    memberType = member.Type.Name;
                else 
                    memberType = "global::" + nameSpaceName + "." + member.Type.Name;
                memberType += $"<{data.GenericParameters.FirstOrDefault()}";
                for(var i = 1; i < data.GenericParameters.Count; i++)
                {
                    memberType += "," + data.GenericParameters[i];
                }
                memberType += ">";
            }
            else
                memberType = "global::" + member.Type.ToDisplayString();
            
            sb.AppendLine($"                    case {member.Id} : writer.WriteValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value)); break;");
        }
        
        sb.AppendLine($"                    default : break;");
        sb.AppendLine("                }");
        
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("            }");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("        }");
        
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void Deserialize(ref LuminPackReader reader, scoped ref {data.classFullName}{paraNullable} value)"
            : $"        public override void Deserialize(ref LuminPackReader reader, ref {data.classFullName}{paraNullable} value)");
        sb.AppendLine("        {");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("            ref int offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryPeekUnionHeader(ref offset, out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            switch(tag)");
        sb.AppendLine("            {");
        foreach (var member in data.UnionMembers)
        {
            
            string memberType;
            if (member.Type.IsUnboundGenericType)
            {
                string nameSpaceName = member.Type.ContainingNamespace?.ToDisplayString() ?? "";
                if (nameSpaceName == "") 
                    memberType = member.Type.Name;
                else 
                    memberType = "global::" + nameSpaceName + "." + member.Type.Name;
                memberType += $"<{data.GenericParameters.FirstOrDefault()}";
                for(var i = 1; i < data.GenericParameters.Count; i++)
                {
                    memberType += "," + data.GenericParameters[i];
                }
                memberType += ">";
            }
            else
                memberType = "global::" + member.Type.ToDisplayString();
            
            var localType = classGlobalName;
            sb.AppendLine($"                case {member.Id} : ");
            sb.AppendLine($"                    if (value is {memberType})");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        reader.ReadValue(ref LuminPackMarshal.As<{localType}, {memberType}>(ref value)!);");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    else");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        var tempValue = reader.ReadValue<{memberType}>();");
            sb.AppendLine($"                        value = LuminPackMarshal.As<{memberType}, {localType}>(ref tempValue!);");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    break;");
        }
        sb.AppendLine($"                default : ");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowInvalidTag(tag, typeof({classGlobalName}));");
        sb.AppendLine($"                    break;");
        sb.AppendLine("            }");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("        }");
        
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        sb.AppendLine("            if (_types.TryGetValue(value.GetType(), out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator.CalculateUnionHeader(tag);");
        sb.AppendLine();
        sb.AppendLine("                switch(tag)");
        sb.AppendLine("                {");
        
        foreach (var member in data.UnionMembers)
        {
            var localType = classGlobalName;
            string memberType;
            if (member.Type.IsUnboundGenericType)
            {
                string nameSpaceName = member.Type.ContainingNamespace?.ToDisplayString() ?? "";
                if (nameSpaceName == "") 
                    memberType = member.Type.Name;
                else 
                    memberType = "global::" + nameSpaceName + "." + member.Type.Name;
                memberType += $"<{data.GenericParameters.FirstOrDefault()}";
                for(var i = 1; i < data.GenericParameters.Count; i++)
                {
                    memberType += "," + data.GenericParameters[i];
                }
                memberType += ">";
                
            }
            else
                memberType = "global::" + member.Type.ToDisplayString();
            
            sb.AppendLine($"                    case {member.Id} : evaluator.CalculateValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value)); break;");
        }
        
        sb.AppendLine($"                    default : break;");
        sb.AppendLine("                }");
        
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine($"                LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("        }");
        
        sb.AppendLine();
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}