using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using SymbolDisplayFormat = Microsoft.CodeAnalysis.SymbolDisplayFormat;

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
        
        // 使用2的幂次作为哈希表大小 - 纯位运算，极致性能
        // 负载因子0.125 (1/8) 以最小化冲突
        int tableSize = data.UnionMembers.Count;
        tableSize--;
        tableSize |= tableSize >> 1;
        tableSize |= tableSize >> 2;
        tableSize |= tableSize >> 4;
        tableSize |= tableSize >> 8;
        tableSize |= tableSize >> 16;
        tableSize++;
        tableSize = tableSize * 8;
        
        // 计算 directTable 大小（基于最大 Tag）
        ushort maxTag = data.UnionMembers.Count > 0 
            ? data.UnionMembers.Max(m => m.Id) 
            : (ushort)0;
        int directTableSize = maxTag + 1;
        
        // 生成哈希表Entry结构
        sb.AppendLine("        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]");
        sb.AppendLine("        public unsafe struct HashEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            public void* MethodTable;");
        sb.AppendLine("            public ushort Tag;");
        sb.AppendLine("            public global::LuminPack.Internal.LuminUnionWriteDelegate? WriteDelegate;");
        sb.AppendLine("            public global::LuminPack.Internal.LuminUnionReadDelegate? ReadDelegate;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // 生成静态字段
        sb.AppendLine($"        public static HashEntry[] _hashTable = new HashEntry[{tableSize}];");
        sb.AppendLine($"        static HashEntry[] _directTable = new HashEntry[{directTableSize}];");
        sb.AppendLine($"        static object _hashTableLock = new object();");
        if (tableSize > 0) 
            sb.AppendLine($"        static uint _hashMask = {tableSize - 1}u;");
        else
            sb.AppendLine($"        static uint _hashMask = 0;");
        sb.AppendLine($"        static ushort _occupiedCount = 0;");
        sb.AppendLine();
        
        // 静态构造函数
        sb.AppendLine($"        static {parserName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new {classFullName}());");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new ArrayParser<{classGlobalName}>());");
        sb.AppendLine();
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine();
        for (int i = 0; i < data.UnionMembers.Count; i++)
        {
            var member = data.UnionMembers[i];
            string fullName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"                Register(typeof({fullName}), {member.Id}, null, null);");
        }
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // TryGetEntry 方法
        sb.AppendLine("        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        public static unsafe bool TryGetEntry(void* mt, out HashEntry entry)");
        sb.AppendLine("        {");
        sb.AppendLine("            const uint GOLDEN_RATIO = 2654435769u;");
        sb.AppendLine("            var ptr = (ulong)mt;");
        sb.AppendLine("            var hash = (uint)((ptr * GOLDEN_RATIO) >> 32) & _hashMask;");
        sb.AppendLine();
        sb.AppendLine("            ref var entryRef = ref _hashTable[hash];");
        sb.AppendLine("            if (entryRef.MethodTable == mt) { entry = entryRef; return true; }");
        sb.AppendLine();
        sb.AppendLine("            if (entryRef.MethodTable != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                var hash2 = ((uint)(((ptr >> 32) * GOLDEN_RATIO) >> 32) & _hashMask) | 1u;");
        sb.AppendLine();
        // 第 2 次探测
        sb.AppendLine("                hash = (hash + hash2) & _hashMask;");
        sb.AppendLine("                entryRef = ref _hashTable[hash];");
        sb.AppendLine("                if (entryRef.MethodTable == mt) { entry = entryRef; return true; }");
        sb.AppendLine();
        // 第 3 次探测
        sb.AppendLine("                hash = (hash + hash2) & _hashMask;");
        sb.AppendLine("                entryRef = ref _hashTable[hash];");
        sb.AppendLine("                if (entryRef.MethodTable == mt) { entry = entryRef; return true; }");
        sb.AppendLine();
        // 第 4 次探测
        sb.AppendLine("                hash = (hash + hash2) & _hashMask;");
        sb.AppendLine("                entryRef = ref _hashTable[hash];");
        sb.AppendLine("                if (entryRef.MethodTable == mt) { entry = entryRef; return true; }");
        sb.AppendLine("            }");
        sb.AppendLine();
        // 4 次未命中 → 线性扫 _directTable
        sb.AppendLine("            for (int i = 0; i < _directTable.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var dt = ref _directTable[i];");
        sb.AppendLine("                if (dt.MethodTable == mt) { entry = dt; return true; }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            entry = default;");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Serialize 方法
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
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine("                var valueMT = (void*)LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine("                if (TryGetEntry(valueMT, out var entry))");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteUnionHeader(ref offset, entry.Tag);");
        sb.AppendLine();
        sb.AppendLine("                    if (entry.WriteDelegate != null)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        entry.WriteDelegate(ref writer, value);");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        
        if (data.UnionMembers.Count <= 4)
        {
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
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
                    for(var j = 1; j < data.GenericParameters.Count; j++)
                    {
                        memberType += "," + data.GenericParameters[j];
                    }
                    memberType += ">";
                }
                else
                    memberType = "global::" + member.Type.ToDisplayString();
                
                if (i == 0)
                    sb.AppendLine($"                        if (entry.Tag == {member.Id}) writer.WriteValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value));");
                else
                    sb.AppendLine($"                        else if (entry.Tag == {member.Id}) writer.WriteValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value));");
            }
        }
        else
        {
            sb.AppendLine("                        switch(entry.Tag)");
            sb.AppendLine("                        {");
            
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
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
                    for(var j = 1; j < data.GenericParameters.Count; j++)
                    {
                        memberType += "," + data.GenericParameters[j];
                    }
                    memberType += ">";
                }
                else
                    memberType = "global::" + member.Type.ToDisplayString();
                
                sb.AppendLine($"                            case {member.Id}: writer.WriteValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value)); break;");
            }
            
            sb.AppendLine("                        }");
        }
        
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                }");
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
        
        // Deserialize 方法
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
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var entry = ref _directTable[tag];");
        sb.AppendLine("                if (entry.ReadDelegate != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    entry.ReadDelegate(ref reader, value);");
        sb.AppendLine("                    return;");
        sb.AppendLine("                }");
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
            sb.AppendLine($"                case {member.Id}: ");
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
        sb.AppendLine($"                default: ");
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
        
        // CalculateOffset
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
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine("                var valueMT = (void*)LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine("                if (TryGetEntry(valueMT, out var entry))");
        sb.AppendLine("                {");
        sb.AppendLine("                    evaluator.CalculateUnionHeader(entry.Tag);");
        sb.AppendLine();
        
        if (data.UnionMembers.Count <= 4)
        {
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
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
                    for(var j = 1; j < data.GenericParameters.Count; j++)
                    {
                        memberType += "," + data.GenericParameters[j];
                    }
                    memberType += ">";
                }
                else
                    memberType = "global::" + member.Type.ToDisplayString();
                
                if (i == 0)
                    sb.AppendLine($"                    if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value));");
                else
                    sb.AppendLine($"                    else if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value));");
            }
        }
        else
        {
            sb.AppendLine("                    switch(entry.Tag)");
            sb.AppendLine("                    {");
            
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
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
                    for(var j = 1; j < data.GenericParameters.Count; j++)
                    {
                        memberType += "," + data.GenericParameters[j];
                    }
                    memberType += ">";
                }
                else
                    memberType = "global::" + member.Type.ToDisplayString();
                
                sb.AppendLine($"                        case {member.Id}: evaluator.CalculateValue(LuminPackMarshal.As<{localType}, {memberType}>(ref value)); break;");
            }
            
            sb.AppendLine("                    }");
        }
        
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Register 方法
        sb.AppendLine("        public static unsafe void Register(System.Type type, ushort tag, global::LuminPack.Internal.LuminUnionWriteDelegate? writeDelegate, global::LuminPack.Internal.LuminUnionReadDelegate? readDelegate)");
        sb.AppendLine("        {");
        sb.AppendLine("            lock (_hashTableLock)");
        sb.AppendLine("            {");
        sb.AppendLine("                var mt = (void*)LuminPackMarshal.GetMethodTable(type);");
        sb.AppendLine("                var ptr = (ulong)mt;");
        sb.AppendLine("                const uint GOLDEN_RATIO = 2654435769u;");
        sb.AppendLine();
        
        // 扩容逻辑
        sb.AppendLine("                if (_occupiedCount >= _hashTable.Length / 8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var oldTable = _hashTable;");
        sb.AppendLine("                    var newSize = _hashTable.Length * 2;");
        sb.AppendLine("                    _hashTable = new HashEntry[newSize];");
        sb.AppendLine("                    _hashMask = (uint)(newSize - 1);");
        sb.AppendLine("                    _occupiedCount = 0;");
        sb.AppendLine();
        sb.AppendLine("                    foreach (var entry in oldTable)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (entry.MethodTable == null) continue;");
        sb.AppendLine();
        sb.AppendLine("                        var entryPtr = (ulong)entry.MethodTable;");
        sb.AppendLine("                        var hash = (uint)((entryPtr * GOLDEN_RATIO) >> 32) & _hashMask;");
        sb.AppendLine();
        sb.AppendLine("                        ref var slot = ref _hashTable[hash];");
        sb.AppendLine("                        if (slot.MethodTable != null)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            var hash2 = ((uint)(((entryPtr >> 32) * GOLDEN_RATIO) >> 32) & _hashMask) | 1u;");
        sb.AppendLine("                            do");
        sb.AppendLine("                            {");
        sb.AppendLine("                                hash = (hash + hash2) & _hashMask;");
        sb.AppendLine("                                slot = ref _hashTable[hash];");
        sb.AppendLine("                            }");
        sb.AppendLine("                            while (slot.MethodTable != null);");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine("                        slot = entry;");
        sb.AppendLine("                        _occupiedCount++;");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine();
        
        // 插入新条目到哈希表
        sb.AppendLine("                var newHash = (uint)((ptr * GOLDEN_RATIO) >> 32) & _hashMask;");
        sb.AppendLine();
        sb.AppendLine("                ref var newSlot = ref _hashTable[newHash];");
        sb.AppendLine("                if (newSlot.MethodTable != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var hash2 = ((uint)(((ptr >> 32) * GOLDEN_RATIO) >> 32) & _hashMask) | 1u;");
        sb.AppendLine("                    do");
        sb.AppendLine("                    {");
        sb.AppendLine("                        newHash = (newHash + hash2) & _hashMask;");
        sb.AppendLine("                        newSlot = ref _hashTable[newHash];");
        sb.AppendLine("                    }");
        sb.AppendLine("                    while (newSlot.MethodTable != null);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                newSlot = new HashEntry");
        sb.AppendLine("                {");
        sb.AppendLine("                    MethodTable = mt,");
        sb.AppendLine("                    Tag = tag,");
        sb.AppendLine("                    WriteDelegate = writeDelegate,");
        sb.AppendLine("                    ReadDelegate = readDelegate");
        sb.AppendLine("                };");
        sb.AppendLine("                _occupiedCount++;");
        sb.AppendLine();
        
        sb.AppendLine("                if (tag >= _directTable.Length)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var oldDirectTable = _directTable;");
        sb.AppendLine("                    var newDirectSize = tag + 1;");
        sb.AppendLine("                    _directTable = new HashEntry[newDirectSize];");
        sb.AppendLine("                    System.Array.Copy(oldDirectTable, _directTable, oldDirectTable.Length);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                ref var directSlot = ref _directTable[tag];");
        sb.AppendLine("                directSlot = new HashEntry");
        sb.AppendLine("                {");
        sb.AppendLine("                    MethodTable = mt,");
        sb.AppendLine("                    Tag = tag,");
        sb.AppendLine("                    WriteDelegate = writeDelegate,");
        sb.AppendLine("                    ReadDelegate = readDelegate");
        sb.AppendLine("                };");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}