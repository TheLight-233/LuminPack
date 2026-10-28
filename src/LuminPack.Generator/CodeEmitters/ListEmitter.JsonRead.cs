using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

public static partial class ListEmitter

{

    /// <summary>Appends JSON reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonEnumerableDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string type = fieldData.TypeName;
        string baseType = GetCollectionBaseType(type);
        string elementType = GetFirstGeneric(type);
        bool stack = baseType == "global::System.Collections.Generic.Stack";
        bool readOnly = baseType is "global::System.Collections.ObjectModel.ReadOnlyCollection" or
            "global::System.Collections.ObjectModel.ReadOnlyObservableCollection";
        bool interfaceType = baseType is "global::System.Collections.Generic.IEnumerable" or
            "global::System.Collections.Generic.ICollection" or
            "global::System.Collections.Generic.IReadOnlyCollection" or
            "global::System.Collections.Generic.IList" or
            "global::System.Collections.Generic.IReadOnlyList";
        bool setInterface = baseType is "global::System.Collections.Generic.ISet" or
            "global::System.Collections.Generic.IReadOnlySet";

        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine();

        if (stack || readOnly || interfaceType || setInterface)
        {
            sb.AppendLine("            var tempList = new global::System.Collections.Generic.List<" + elementType + ">();");
        }
        else if (baseType == "global::System.Collections.Concurrent.BlockingCollection")
        {
            sb.AppendLine("            value = new " + type + "();");
        }
        else
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                value = new " + type + "();");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Clear();");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        if (stack || readOnly || interfaceType || setInterface)
        {
            sb.AppendLine("                tempList.Add(item);");
        }
        else if (baseType == "global::System.Collections.Generic.LinkedList")
        {
            sb.AppendLine("                value.AddLast(item);");
        }
        else if (baseType == "global::System.Collections.Generic.Queue" ||
                 baseType == "global::System.Collections.Concurrent.ConcurrentQueue")
        {
            sb.AppendLine("                value.Enqueue(item);");
        }
        else if (baseType == "global::System.Collections.Concurrent.ConcurrentStack")
        {
            sb.AppendLine("                value.Push(item);");
        }
        else
        {
            sb.AppendLine("                value.Add(item);");
        }
        sb.AppendLine("            }");

        if (stack)
        {
            sb.AppendLine("            value = new " + type + "();");
            sb.AppendLine("            for (int i = tempList.Count - 1; i >= 0; i--)");
            sb.AppendLine("            {");
            sb.AppendLine("                value.Push(tempList[i]);");
            sb.AppendLine("            }");
        }
        else if (readOnly)
        {
            if (baseType == "global::System.Collections.ObjectModel.ReadOnlyCollection")
            {
                sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyCollection<" + elementType + ">(tempList);");
            }
            else
            {
                sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyObservableCollection<" + elementType + ">(new global::System.Collections.ObjectModel.ObservableCollection<" + elementType + ">(tempList));");
            }
        }
        else if (interfaceType)
        {
            sb.AppendLine("            value = tempList;");
        }
        else if (setInterface)
        {
            sb.AppendLine("            value = new global::System.Collections.Generic.HashSet<" + elementType + ">(tempList);");
        }
    }

    /// <summary>Appends JSON reading statements for list values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new global::System.Collections.Generic.List<" + elementType + ">();");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value.Clear();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                reader.ReadValue(ref item);");
        sb.AppendLine("                value.Add(item);");
        sb.AppendLine("            }");
    }
}
