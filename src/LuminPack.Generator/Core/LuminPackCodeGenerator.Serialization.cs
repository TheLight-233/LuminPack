using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{
    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="extension">The extension used by this operation.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb, bool extension = false, bool polymorphism = false)
    {
        if (PortableStructPlan.RequiresClosedDispatch(data.TypeSymbol))
        {
            sb.AppendLine("            global::LuminPack.LuminPackSerializer.FormatterDispatch.Serialize<" + FormatterTypeName.Get(data.TypeSymbol) + ">(ref writer, in value);");
            return;
        }
        var portable = PortableStructPlan.TryCreate(data.TypeSymbol);
        if (portable != null)
        {
            PortableStructEmitter.AppendValue(sb, portable, "value", FormatterTypeName.Get(data.TypeSymbol), write: true);
            return;
        }
        string concreteTypeName = data.classFullName;
        if (!concreteTypeName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            concreteTypeName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
        }
        if (IsPureUnmanagedStruct(data))
        {
            sb.AppendLine("            global::LuminPack.Core.LuminPackWriter.Write(ref writer, in value);");
            foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerialized))
            {
                sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
            }
            sb.AppendLine();
            return;
        }
        if (!data.isValueType && !polymorphism)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.Write((byte)255);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        if (data.fields.Any(field => field.IsPrivate || field.isProperty))
        {
            sb.AppendLine(extension
                ? "            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref Unsafe.AsRef(in value));"
                : "            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
        }
        GenerateCursorObjectWrite(data, sb, polymorphism);
        foreach (var callback in data.callBackMethods.Where(item => item.Item2 == SerializeCallBackType.OnSerialized))
            sb.AppendLine(callback.Item3 ? "            " + concreteTypeName + "." + callback.Item1 + "();" : "            value?." + callback.Item1 + "();");
    }
}
