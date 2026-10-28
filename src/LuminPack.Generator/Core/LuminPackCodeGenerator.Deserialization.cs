using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        if (PortableStructPlan.RequiresClosedDispatch(data.TypeSymbol))
        {
            sb.AppendLine("            global::LuminPack.LuminPackSerializer.FormatterDispatch.Deserialize<" + FormatterTypeName.Get(data.TypeSymbol) + ">(ref reader, ref value);");
            return;
        }
        var portable = PortableStructPlan.TryCreate(data.TypeSymbol);
        if (portable != null)
        {
            PortableStructEmitter.AppendValue(sb, portable, "value", FormatterTypeName.Get(data.TypeSymbol), write: false);
            return;
        }
        string concreteTypeName = data.classFullName;
        if (!concreteTypeName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            concreteTypeName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
        }
        if (IsPureUnmanagedStruct(data))
        {
            sb.AppendLine("            global::LuminPack.Core.LuminPackReader.Read(ref reader, ref value);");
            foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
            {
                sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
            }
            sb.AppendLine();
            return;
        }
        bool canConstructDirectly = data.RentPoolMethod == null && (data.SelectedConstructor == null || data.SelectedConstructor.Parameters.Count == 0);
        // A pooled reference type rents its instance up front, so a fresh read fills the rented object in place exactly
        // like a read into an existing instance (owner-requested 2026-10-28).
        bool rentsInstance = !data.isValueType && data.RentPoolMethod != null;
        if (!data.isValueType && (canConstructDirectly || rentsInstance))
        {
            if (!polymorphism)
                sb.AppendLine("            if (reader.Read<byte>() == 255) return;");
            string allocation = rentsInstance ? concreteTypeName + "." + ((ISymbol)data.RentPoolMethod!).Name + "()" : "new " + concreteTypeName + "()";
            sb.AppendLine("            var instance = value ??= " + allocation + ";");
            if (data.fields.Any(field => !CanDeserializeDirectlyIntoConstructedObject(field)))
                sb.AppendLine("            var local = LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref instance);");
            for (int index = 0; index < data.fields.Count; index++)
            {
                var field = data.fields[index];
                string Storage(LuminDataField member) => (CanDeserializeDirectlyIntoConstructedObject(member) ? "instance." : "local.") + member.Identifier;
                int last = index;
                if (!field.IsCompress && IsMergeableField(field))
                    while (last + 1 < data.fields.Count && !data.fields[last + 1].IsCompress && IsMergeableField(data.fields[last + 1])) last++;
                if (TryGenerateCursorFixedGroup(sb, data.fields, index, last, Storage, "            ", write: false, allowBulkCopy: !polymorphism))
                    index = last;
                else AppendCursorFieldRead(sb, field, Storage(field), "            ", 0, fresh: false, allowBulkCopy: !polymorphism);
            }
            foreach (var callback in data.callBackMethods.Where(item => item.Item2 == SerializeCallBackType.OnDeserialized))
                sb.AppendLine(callback.Item3 ? "            " + concreteTypeName + "." + callback.Item1 + "();" : "            value?." + callback.Item1 + "();");
            return;
        }

        sb.AppendLine();

        if (!canConstructDirectly || data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
        {
            sb.AppendLine("            // Declare locals to hold the field values.");
            foreach (LuminLocalFieldData localField in data.localFields)
            {
                if (canConstructDirectly && data.fields.Any(field => field.Name == localField.Name && CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    continue;
                }
                string nullableSuffix3 = (localField.IsValue ? "" : "?");
                sb.AppendLine("            " + localField.TypeName + nullableSuffix3 + " " + localField.Name + "Temp = default!;");
            }
            sb.AppendLine();
        }
        if (!polymorphism && !data.isValueType)
        {
            sb.AppendLine("            if (reader.Read<byte>() == 255)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        if (!polymorphism && data.isValueType) sb.AppendLine("            reader.Advance(1);");
        if (!data.isValueType || HasReusableMembers(data.fields))
        {
            AppendExistingObjectRead(sb, data, concreteTypeName, polymorphism);
        }
        if (canConstructDirectly)
            sb.AppendLine("            var result = new " + concreteTypeName + "();");
        foreach (var field in data.fields)
        {
            string target = canConstructDirectly && CanDeserializeDirectlyIntoConstructedObject(field)
                ? "result." + field.Identifier : field.Name + "Temp";
            AppendCursorFieldRead(sb, field, target, "            ", 0, fresh: true, allowBulkCopy: !polymorphism);
        }
        sb.AppendLine();
        sb.AppendLine("            // Construct the object and populate its members.");
        if (canConstructDirectly)
        {
            if (data.isValueType || polymorphism)
            {
                sb.AppendLine("            value = result;");
            }
            else
            {
                // Deserialize-into-existing: when a root instance is supplied, preserve its identity and
                // commit members into it only on success so a parse failure never publishes a partially
                // initialized object into value.
                sb.AppendLine("            if (value is null)");
                sb.AppendLine("            {");
                sb.AppendLine("                value = result;");
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                foreach (LuminDataField field in data.fields.Where(static field => CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    sb.AppendLine("                value." + field.Identifier + " = result." + field.Identifier + ";");
                }
                sb.AppendLine("            }");
            }
            if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
            {
                if (data.isValueType)
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                }
                else
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                }
                foreach (LuminDataField field in data.fields.Where(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
                }
            }
        }
        else
        {
            List<string> constructorArgumentExpressions = new List<string>();
            if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
            {
                foreach (ConstructorParameter param in data.SelectedConstructor.Parameters)
                {
                    LuminDataField nestedField3 = data.fields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
                    if (nestedField3 != null)
                    {
                        constructorArgumentExpressions.Add(nestedField3.Name + "Temp!");
                    }
                    else
                    {
                        constructorArgumentExpressions.Add("default");
                    }
                }
            }
            string constructorArguments = string.Join(", ", constructorArgumentExpressions);
            List<LuminDataField> publicFieldAssignments = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate && !f.isProperty).ToList();
            List<LuminDataField> privateFieldAssignments = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && (f.IsPrivate || f.isProperty)).ToList();
            if (data.RentPoolMethod != null)
            {
                if (data.RentPoolMethod.ReturnsByRef)
                {
                    sb.AppendLine("            value = ref " + concreteTypeName + "." + ((ISymbol)data.RentPoolMethod).Name + "()!;");
                }
                else
                {
                    sb.AppendLine("            value = " + concreteTypeName + "." + ((ISymbol)data.RentPoolMethod).Name + "();");
                }
                sb.AppendLine("            // Populate every field on the pooled instance.");
                if (data.isValueType)
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                }
                else
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                }
                foreach (LuminDataField field in data.fields)
                {
                    sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
                }
            }
            else
            {
                if (publicFieldAssignments.Count > 0)
                {
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine("            value = new " + concreteTypeName + "(" + constructorArguments + ")");
                    }
                    else
                    {
                        sb.AppendLine("            value = new " + concreteTypeName + "()");
                    }
                    sb.AppendLine("            {");
                    foreach (LuminDataField item2 in publicFieldAssignments)
                    {
                        sb.AppendLine("                " + item2.Identifier + " = " + item2.Name + "Temp!,");
                    }
                    sb.AppendLine("            };");
                }
                else if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                {
                    sb.AppendLine("            value = new " + concreteTypeName + "(" + constructorArguments + ");");
                }
                else
                {
                    sb.AppendLine("            value = new " + concreteTypeName + "();");
                }
                if (privateFieldAssignments.Count > 0)
                {
                    sb.AppendLine("            // Assign private fields.");
                    if (data.isValueType)
                    {
                        sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                    }
                    else
                    {
                        sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                    }
                    foreach (LuminDataField item3 in privateFieldAssignments)
                    {
                        sb.AppendLine("            local." + item3.Identifier + " = " + item3.Name + "Temp!;");
                    }
                }
            }
        }
        foreach (var item4 in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item4.Item3 ? ("            " + concreteTypeName + "." + item4.Item1 + "();") : ("            value?." + item4.Item1 + "();"));
        }
        sb.AppendLine();
    }
}
