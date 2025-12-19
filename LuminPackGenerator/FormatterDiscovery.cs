using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.Formatter;

namespace LuminPack.SourceGenerator
{
    public static class FormatterDiscovery
    {
        public static readonly Dictionary<string, (Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>)> Formatters = 
            new (StringComparer.Ordinal)
            {
                // 基本类型和别名
                ["sbyte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.SByte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["sbyte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.SByte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["byte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Byte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["byte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Byte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["short"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Int16"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["short[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Int16[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["ushort"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.UInt16"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["ushort[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.UInt16[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["int"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Int32"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["int[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Int32[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["uint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.UInt32"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["uint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.UInt32[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["long"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Int64"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["long[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Int64[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["ulong"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.UInt64"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["ulong[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.UInt64[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["float"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Single"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["float[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Single[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["double"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Double"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["double[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Double[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["decimal"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Decimal"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["decimal[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Decimal[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["bool"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Boolean"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["bool[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Boolean[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["char"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Char"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["char[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.Char[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["nint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.IntPtr"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["nint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.IntPtr[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["nuint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.UIntPtr"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["nuint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
                ["global::System.UIntPtr[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                // 其他系统类型
                ["global::System.Guid"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Guid[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.DateTime"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.DateTime[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.DateTimeOffset"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.DateTimeOffset[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.TimeSpan"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.TimeSpan[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                // 数值类型
                ["global::System.Numerics.BigInteger"] = (BigIntegerFormatter.GenerateSerializeCode, BigIntegerFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Complex"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Complex[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Plane"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Plane[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Quaternion"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Quaternion[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Matrix3x2"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Matrix3x2[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Matrix4x4"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Matrix4x4[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Vector2"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Vector2[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Vector3"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Vector3[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Numerics.Vector4"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Numerics.Vector4[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                // .NET 8+ 类型
                ["global::System.Half"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Half[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Int128"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Int128[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.UInt128"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.UInt128[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.DateOnly"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.DateOnly[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.TimeOnly"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.TimeOnly[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                ["global::System.Text.Rune"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode),
                ["global::System.Text.Rune[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),

                // 字符串类型
                ["string"] = (StringFormatter.GenerateSerializeCode, StringFormatter.GenerateDeserializeCode),
                ["global::System.String"] = (StringFormatter.GenerateSerializeCode, StringFormatter.GenerateDeserializeCode),

                // URI类型
                ["global::System.Uri"] = (UriFormatter.GenerateSerializeCode, UriFormatter.GenerateDeserializeCode),

                // 版本类型
                ["global::System.Version"] = (VersionFormatter.GenerateSerializeCode, VersionFormatter.GenerateDeserializeCode),

                // 位数组类型
                ["global::System.Collections.BitArray"] = (BitArrayFormatter.GenerateSerializeCode, BitArrayFormatter.GenerateDeserializeCode),

                // 可空类型
                ["global::System.Nullable"] = (NullableFormatter.GenerateSerializeCode, NullableFormatter.GenerateDeserializeCode),

                // StringBuilder类型
                ["global::System.Text.StringBuilder"] = (StringBuilderFormatter.GenerateSerializeCode, StringBuilderFormatter.GenerateDeserializeCode),

                // 文化信息类型
                ["global::System.Globalization.CultureInfo"] = (CultureInfoFormatter.GenerateSerializeCode, CultureInfoFormatter.GenerateDeserializeCode),

                // 时区信息类型
                ["global::System.TimeZoneInfo"] = (TimeZoneInfoFormatter.GenerateSerializeCode, TimeZoneInfoFormatter.GenerateDeserializeCode),

                // 类型信息
                ["global::System.Type"] = (TypeFormatter.GenerateSerializeCode, TypeFormatter.GenerateDeserializeCode),

                // 集合类型
                ["global::System.Collections.Generic.List"] = (ListFormatter.GenerateSerializeCode, ListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.Dictionary"] = (DictionaryFormatter.GenerateSerializeCode, DictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentDictionary"] = (ConcurrentDictionaryFormatter.GenerateSerializeCode, ConcurrentDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.SortedDictionary"] = (SortedDictionaryFormatter.GenerateSerializeCode, SortedDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.Stack"] = (StackFormatter.GenerateSerializeCode, StackFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.Queue"] = (QueueFormatter.GenerateSerializeCode, QueueFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.LinkedList"] = (LinkedListFormatter.GenerateSerializeCode, LinkedListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.HashSet"] = (HashSetFormatter.GenerateSerializeCode, HashSetFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.SortedSet"] = (SortedSetFormatter.GenerateSerializeCode, SortedSetFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.SortedList"] = (SortedListFormatter.GenerateSerializeCode, SortedListFormatter.GenerateDeserializeCode),
    
                // 并发集合
                ["global::System.Collections.Concurrent.BlockingCollection"] = (BlockingCollectionFormatter.GenerateSerializeCode, BlockingCollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentBag"] = (ConcurrentBagFormatter.GenerateSerializeCode, ConcurrentBagFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentQueue"] = (ConcurrentQueueFormatter.GenerateSerializeCode, ConcurrentQueueFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentStack"] = (ConcurrentStackFormatter.GenerateSerializeCode, ConcurrentStackFormatter.GenerateDeserializeCode),
    
                // 只读集合
                ["global::System.Collections.ObjectModel.Collection"] = (CollectionFormatter.GenerateSerializeCode, CollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.ObjectModel.ObservableCollection"] = (ObservableCollectionFormatter.GenerateSerializeCode, ObservableCollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyCollection"] = (ReadOnlyCollectionFormatter.GenerateSerializeCode, ReadOnlyCollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyObservableCollection"] = (ReadOnlyObservableCollectionFormatter.GenerateSerializeCode, ReadOnlyObservableCollectionFormatter.GenerateDeserializeCode),
    
                // 优先级队列
                ["global::System.Collections.Generic.PriorityQueue"] = (PriorityQueueFormatter.GenerateSerializeCode, PriorityQueueFormatter.GenerateDeserializeCode),
    
                // 不可变集合类型
                ["global::System.Collections.Immutable.ImmutableArray"] = (ImmutableArrayFormatter.GenerateSerializeCode, ImmutableArrayFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableList"] = (ImmutableListFormatter.GenerateSerializeCode, ImmutableListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableQueue"] = (ImmutableQueueFormatter.GenerateSerializeCode, ImmutableQueueFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableStack"] = (ImmutableStackFormatter.GenerateSerializeCode, ImmutableStackFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableDictionary"] = (ImmutableDictionaryFormatter.GenerateSerializeCode, ImmutableDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableHashSet"] = (ImmutableHashSetFormatter.GenerateSerializeCode, ImmutableHashSetFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedDictionary"] = (ImmutableSortedDictionaryFormatter.GenerateSerializeCode, ImmutableSortedDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedSet"] = (ImmutableSortedSetFormatter.GenerateSerializeCode, ImmutableSortedSetFormatter.GenerateDeserializeCode),

                // 冻结集合类型 (.NET 8+)
                ["global::System.Collections.Frozen.FrozenDictionary"] = (FrozenDictionaryFormatter.GenerateSerializeCode, FrozenDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Frozen.FrozenSet"] = (FrozenSetFormatter.GenerateSerializeCode, FrozenSetFormatter.GenerateDeserializeCode),

                // 接口类型
                ["global::System.Collections.Generic.IEnumerable"] = (InterfaceEnumerableFormatter.GenerateSerializeCode, InterfaceEnumerableFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.ICollection"] = (InterfaceCollectionFormatter.GenerateSerializeCode, InterfaceCollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyCollection"] = (InterfaceReadOnlyCollectionFormatter.GenerateSerializeCode, InterfaceReadOnlyCollectionFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IList"] = (InterfaceListFormatter.GenerateSerializeCode, InterfaceListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyList"] = (InterfaceReadOnlyListFormatter.GenerateSerializeCode, InterfaceReadOnlyListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IDictionary"] = (InterfaceDictionaryFormatter.GenerateSerializeCode, InterfaceDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyDictionary"] = (InterfaceReadOnlyDictionaryFormatter.GenerateSerializeCode, InterfaceReadOnlyDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Linq.ILookup"] = (InterfaceLookupFormatter.GenerateSerializeCode, InterfaceLookupFormatter.GenerateDeserializeCode),
                ["global::System.Linq.IGrouping"] = (InterfaceGroupingFormatter.GenerateSerializeCode, InterfaceGroupingFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.ISet"] = (InterfaceSetFormatter.GenerateSerializeCode, InterfaceSetFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlySet"] = (InterfaceReadOnlySetFormatter.GenerateSerializeCode, InterfaceReadOnlySetFormatter.GenerateDeserializeCode),

                // 不可变集合接口
                ["global::System.Collections.Immutable.IImmutableList"] = (InterfaceImmutableListFormatter.GenerateSerializeCode, InterfaceImmutableListFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableQueue"] = (InterfaceImmutableQueueFormatter.GenerateSerializeCode, InterfaceImmutableQueueFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableStack"] = (InterfaceImmutableStackFormatter.GenerateSerializeCode, InterfaceImmutableStackFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableDictionary"] = (InterfaceImmutableDictionaryFormatter.GenerateSerializeCode, InterfaceImmutableDictionaryFormatter.GenerateDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableSet"] = (InterfaceImmutableSetFormatter.GenerateSerializeCode, InterfaceImmutableSetFormatter.GenerateDeserializeCode),
                
                // 数组类型
                ["global::System.Array"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode),
            };

        // KnownType信息
        public static readonly HashSet<string> KnownValueTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            // 基本类型
            "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
            "float", "double", "decimal", "bool", "char", "nint", "nuint",
    
            // 系统类型别名
            "global::System.SByte", "global::System.Byte", 
            "global::System.Int16", "global::System.UInt16",
            "global::System.Int32", "global::System.UInt32",
            "global::System.Int64", "global::System.UInt64",
            "global::System.Single", "global::System.Double", 
            "global::System.Decimal", "global::System.Boolean", 
            "global::System.Char",
            "global::System.IntPtr", "global::System.UIntPtr",
    
            // 系统值类型
            "global::System.Guid", "global::System.DateTime", 
            "global::System.DateTimeOffset", "global::System.TimeSpan",
    
            // 数值类型
            "global::System.Numerics.Complex", "global::System.Numerics.Plane", 
            "global::System.Numerics.Quaternion", "global::System.Numerics.Matrix3x2", 
            "global::System.Numerics.Matrix4x4", "global::System.Numerics.Vector2",
            "global::System.Numerics.Vector3", "global::System.Numerics.Vector4",
    
            // .NET 8+ 类型
            "global::System.Half", "global::System.Int128", "global::System.UInt128",
            "global::System.DateOnly", "global::System.TimeOnly", "global::System.Text.Rune",
    
            // 其他已知的Unmanaged类型
            "global::System.Runtime.InteropServices.GCHandle",
            "global::System.Runtime.InteropServices.CriticalHandle"
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) GetFormatter(string typeName)
        {
            
            if (typeName.EndsWith("[]"))
            {
                return (ArrayFormatter.GenerateSerializeCode, ArrayFormatter.GenerateDeserializeCode);
            }
            
            string baseTypeName = GetBaseTypeName(typeName);
            
            if (Formatters.TryGetValue(baseTypeName, out var formatter))
            {
                return formatter;
            }
            
            
    
            return (null, null);
        }

        private static string GetBaseTypeName(string fullTypeName)
        {
            int angleBracketIndex = fullTypeName.IndexOf('<');
            if (angleBracketIndex >= 0)
            {
                return fullTypeName.Substring(0, angleBracketIndex);
            }



            return fullTypeName;
        }

        public static string GetFirstGeneric(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
                return string.Empty;
            
            int startIndex = typeString.IndexOf('<');
            if (startIndex == -1)
                return string.Empty;
            
            int endIndex = typeString.LastIndexOf('>');
            if (endIndex == -1 || endIndex <= startIndex)
                return string.Empty;
            
            string genericPart = typeString.Substring(startIndex + 1, endIndex - startIndex - 1);
            
            string[] genericArgs = SplitGenericParameters(genericPart);
    
            if (genericArgs.Length > 0)
                return genericArgs[0].Trim();
    
            return string.Empty;
        }

        public static string GetSecondGeneric(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
                return string.Empty;
            
            int startIndex = typeString.IndexOf('<');
            if (startIndex == -1)
                return string.Empty;
            
            int endIndex = typeString.LastIndexOf('>');
            if (endIndex == -1 || endIndex <= startIndex)
                return string.Empty;
            
            string genericPart = typeString.Substring(startIndex + 1, endIndex - startIndex - 1);
            
            string[] genericArgs = SplitGenericParameters(genericPart);
    
            if (genericArgs.Length > 1)
                return genericArgs[1].Trim();
    
            return string.Empty;
        }

        private static string[] SplitGenericParameters(string genericPart)
        {
            List<string> parameters = new List<string>();
            int bracketCount = 0;
            int start = 0;
    
            for (int i = 0; i < genericPart.Length; i++)
            {
                char c = genericPart[i];
        
                if (c == '<')
                    bracketCount++;
                else if (c == '>')
                    bracketCount--;
                else if (c == ',' && bracketCount == 0)
                {
                    parameters.Add(genericPart.Substring(start, i - start));
                    start = i + 1;
                }
            }
            
            if (start < genericPart.Length)
                parameters.Add(genericPart.Substring(start));
    
            return parameters.ToArray();
        }
    }
}
