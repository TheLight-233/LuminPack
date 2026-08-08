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
        public static readonly Dictionary<string, (Action<LuminLocalFieldData, StringBuilder> Write, Action<LuminLocalFieldData, StringBuilder> Read, Action<LuminLocalFieldData, StringBuilder> WriteJson, Action<LuminLocalFieldData, StringBuilder> ReadJson)> Formatters =
            new (StringComparer.Ordinal)
            {
                // 基本类型和别名
                ["sbyte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.SByte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["sbyte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.SByte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["byte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Byte"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["byte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Byte[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["short"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int16"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["short[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int16[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["ushort"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt16"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["ushort[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt16[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["int"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int32"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["int[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int32[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["uint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt32"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["uint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt32[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["long"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int64"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["long[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int64[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["ulong"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt64"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["ulong[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt64[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["float"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Single"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["float[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Single[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["double"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Double"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["double[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Double[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["decimal"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Decimal"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["decimal[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Decimal[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["bool"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Boolean"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["bool[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Boolean[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["char"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Char"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["char[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.Char[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["nint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.IntPtr"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["nint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.IntPtr[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["nuint"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.UIntPtr"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["nuint[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),
                ["global::System.UIntPtr[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                // 其他系统类型
                ["global::System.Guid"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Guid[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.DateTime"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.DateTime[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.DateTimeOffset"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.DateTimeOffset[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.TimeSpan"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.TimeSpan[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                // 数值类型
                ["global::System.Numerics.BigInteger"] = (BigIntegerFormatter.GenerateSerializeCode, BigIntegerFormatter.GenerateDeserializeCode, BigIntegerFormatter.GenerateJsonSerializeCode, BigIntegerFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Complex"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Complex[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Plane"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Plane[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Quaternion"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Quaternion[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Matrix3x2"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Matrix3x2[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Matrix4x4"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Matrix4x4[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector2"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector2[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector3"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector3[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector4"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector4[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                // .NET 8+ 类型
                ["global::System.Half"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Half[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Int128"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Int128[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.UInt128"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.UInt128[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.DateOnly"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.DateOnly[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.TimeOnly"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.TimeOnly[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                ["global::System.Text.Rune"] = (UnmanagedFormatter.GenerateSerializeCode, UnmanagedFormatter.GenerateDeserializeCode, UnmanagedFormatter.GenerateJsonSerializeCode, UnmanagedFormatter.GenerateJsonDeserializeCode),
                ["global::System.Text.Rune[]"] = (UnmanagedArrayFormatter.GenerateSerializeCode, UnmanagedArrayFormatter.GenerateDeserializeCode, ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode),

                // 字符串类型
                ["string"] = (StringFormatter.GenerateSerializeCode, StringFormatter.GenerateDeserializeCode, StringFormatter.GenerateJsonSerializeCode, StringFormatter.GenerateJsonDeserializeCode),
                ["global::System.String"] = (StringFormatter.GenerateSerializeCode, StringFormatter.GenerateDeserializeCode, StringFormatter.GenerateJsonSerializeCode, StringFormatter.GenerateJsonDeserializeCode),

                // URI类型
                ["global::System.Uri"] = (UriFormatter.GenerateSerializeCode, UriFormatter.GenerateDeserializeCode, UriFormatter.GenerateJsonSerializeCode, UriFormatter.GenerateJsonDeserializeCode),

                // 版本类型
                ["global::System.Version"] = (VersionFormatter.GenerateSerializeCode, VersionFormatter.GenerateDeserializeCode, VersionFormatter.GenerateJsonSerializeCode, VersionFormatter.GenerateJsonDeserializeCode),

                // 位数组类型
                ["global::System.Collections.BitArray"] = (BitArrayFormatter.GenerateSerializeCode, BitArrayFormatter.GenerateDeserializeCode, BitArrayFormatter.GenerateJsonSerializeCode, BitArrayFormatter.GenerateJsonDeserializeCode),

                // 可空类型
                ["global::System.Nullable"] = (NullableFormatter.GenerateSerializeCode, NullableFormatter.GenerateDeserializeCode, NullableFormatter.GenerateJsonSerializeCode, NullableFormatter.GenerateJsonDeserializeCode),

                // StringBuilder类型
                ["global::System.Text.StringBuilder"] = (StringBuilderFormatter.GenerateSerializeCode, StringBuilderFormatter.GenerateDeserializeCode, StringBuilderFormatter.GenerateJsonSerializeCode, StringBuilderFormatter.GenerateJsonDeserializeCode),

                // 文化信息类型
                ["global::System.Globalization.CultureInfo"] = (CultureInfoFormatter.GenerateSerializeCode, CultureInfoFormatter.GenerateDeserializeCode, CultureInfoFormatter.GenerateJsonSerializeCode, CultureInfoFormatter.GenerateJsonDeserializeCode),

                // 时区信息类型
                ["global::System.TimeZoneInfo"] = (TimeZoneInfoFormatter.GenerateSerializeCode, TimeZoneInfoFormatter.GenerateDeserializeCode, TimeZoneInfoFormatter.GenerateJsonSerializeCode, TimeZoneInfoFormatter.GenerateJsonDeserializeCode),

                // 类型信息
                ["global::System.Type"] = (TypeFormatter.GenerateSerializeCode, TypeFormatter.GenerateDeserializeCode, TypeFormatter.GenerateJsonSerializeCode, TypeFormatter.GenerateJsonDeserializeCode),

                ["global::System.Lazy"] = (LazyFormatter.GenerateSerializeCode, LazyFormatter.GenerateDeserializeCode, LazyFormatter.GenerateJsonSerializeCode, LazyFormatter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.KeyValuePair"] = (KeyValuePairFormatter.GenerateSerializeCode, KeyValuePairFormatter.GenerateDeserializeCode, KeyValuePairFormatter.GenerateJsonSerializeCode, KeyValuePairFormatter.GenerateJsonDeserializeCode),
                ["global::System.Tuple"] = (TupleFormatter.GenerateSerializeCode, TupleFormatter.GenerateDeserializeCode, TupleFormatter.GenerateJsonSerializeCode, TupleFormatter.GenerateJsonDeserializeCode),
                ["global::System.ValueTuple"] = (TupleFormatter.GenerateSerializeCode, TupleFormatter.GenerateDeserializeCode, TupleFormatter.GenerateJsonSerializeCode, TupleFormatter.GenerateJsonDeserializeCode),
                ["global::System.ArraySegment"] = (MemoryFormatter.GenerateSerializeCode, MemoryFormatter.GenerateDeserializeCode, MemoryFormatter.GenerateJsonSerializeCode, MemoryFormatter.GenerateJsonDeserializeCode),
                ["global::System.Memory"] = (MemoryFormatter.GenerateSerializeCode, MemoryFormatter.GenerateDeserializeCode, MemoryFormatter.GenerateJsonSerializeCode, MemoryFormatter.GenerateJsonDeserializeCode),
                ["global::System.ReadOnlyMemory"] = (MemoryFormatter.GenerateSerializeCode, MemoryFormatter.GenerateDeserializeCode, MemoryFormatter.GenerateJsonSerializeCode, MemoryFormatter.GenerateJsonDeserializeCode),
                ["global::System.Buffers.ReadOnlySequence"] = (MemoryFormatter.GenerateSerializeCode, MemoryFormatter.GenerateDeserializeCode, MemoryFormatter.GenerateJsonSerializeCode, MemoryFormatter.GenerateJsonDeserializeCode),

                // 集合类型
                ["global::System.Collections.Generic.List"] = (ListFormatter.GenerateSerializeCode, ListFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonSerializeCode, ListFormatter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.Dictionary"] = (DictionaryFormatter.GenerateSerializeCode, DictionaryFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonSerializeCode, DictionaryFormatter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentDictionary"] = (ConcurrentDictionaryFormatter.GenerateSerializeCode, ConcurrentDictionaryFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonDictionarySerializeCode, DictionaryFormatter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.SortedDictionary"] = (SortedDictionaryFormatter.GenerateSerializeCode, SortedDictionaryFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonDictionarySerializeCode, DictionaryFormatter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.Stack"] = (StackFormatter.GenerateSerializeCode, StackFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.Queue"] = (QueueFormatter.GenerateSerializeCode, QueueFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.LinkedList"] = (LinkedListFormatter.GenerateSerializeCode, LinkedListFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.HashSet"] = (HashSetFormatter.GenerateSerializeCode, HashSetFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.SortedSet"] = (SortedSetFormatter.GenerateSerializeCode, SortedSetFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.SortedList"] = (SortedListFormatter.GenerateSerializeCode, SortedListFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonDictionarySerializeCode, DictionaryFormatter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyDictionary"] = (ReadOnlyDictionaryFormatter.GenerateSerializeCode, ReadOnlyDictionaryFormatter.GenerateDeserializeCode, ReadOnlyDictionaryFormatter.GenerateJsonSerializeCode, ReadOnlyDictionaryFormatter.GenerateJsonDeserializeCode),
    
                // 并发集合
                ["global::System.Collections.Concurrent.BlockingCollection"] = (BlockingCollectionFormatter.GenerateSerializeCode, BlockingCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentBag"] = (ConcurrentBagFormatter.GenerateSerializeCode, ConcurrentBagFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentQueue"] = (ConcurrentQueueFormatter.GenerateSerializeCode, ConcurrentQueueFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentStack"] = (ConcurrentStackFormatter.GenerateSerializeCode, ConcurrentStackFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
    
                // 只读集合
                ["global::System.Collections.ObjectModel.Collection"] = (CollectionFormatter.GenerateSerializeCode, CollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ObservableCollection"] = (ObservableCollectionFormatter.GenerateSerializeCode, ObservableCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyCollection"] = (ReadOnlyCollectionFormatter.GenerateSerializeCode, ReadOnlyCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyObservableCollection"] = (ReadOnlyObservableCollectionFormatter.GenerateSerializeCode, ReadOnlyObservableCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
    
                // 优先级队列
                ["global::System.Collections.Generic.PriorityQueue"] = (PriorityQueueFormatter.GenerateSerializeCode, PriorityQueueFormatter.GenerateDeserializeCode, PriorityQueueFormatter.GenerateJsonSerializeCode, PriorityQueueFormatter.GenerateJsonDeserializeCode),
    
                // 不可变集合类型
                ["global::System.Collections.Immutable.ImmutableArray"] = (ImmutableArrayFormatter.GenerateSerializeCode, ImmutableArrayFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableList"] = (ImmutableListFormatter.GenerateSerializeCode, ImmutableListFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableQueue"] = (ImmutableQueueFormatter.GenerateSerializeCode, ImmutableQueueFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableStack"] = (ImmutableStackFormatter.GenerateSerializeCode, ImmutableStackFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableDictionary"] = (ImmutableDictionaryFormatter.GenerateSerializeCode, ImmutableDictionaryFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableHashSet"] = (ImmutableHashSetFormatter.GenerateSerializeCode, ImmutableHashSetFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedDictionary"] = (ImmutableSortedDictionaryFormatter.GenerateSerializeCode, ImmutableSortedDictionaryFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedSet"] = (ImmutableSortedSetFormatter.GenerateSerializeCode, ImmutableSortedSetFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),

                // 冻结集合类型 (.NET 8+)
                ["global::System.Collections.Frozen.FrozenDictionary"] = (FrozenDictionaryFormatter.GenerateSerializeCode, FrozenDictionaryFormatter.GenerateDeserializeCode, FrozenDictionaryFormatter.GenerateJsonSerializeCode, FrozenDictionaryFormatter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Frozen.FrozenSet"] = (FrozenSetFormatter.GenerateSerializeCode, FrozenSetFormatter.GenerateDeserializeCode, FrozenSetFormatter.GenerateJsonSerializeCode, FrozenSetFormatter.GenerateJsonDeserializeCode),

                // 接口类型
                ["global::System.Collections.Generic.IEnumerable"] = (InterfaceEnumerableFormatter.GenerateSerializeCode, InterfaceEnumerableFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.ICollection"] = (InterfaceCollectionFormatter.GenerateSerializeCode, InterfaceCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyCollection"] = (InterfaceReadOnlyCollectionFormatter.GenerateSerializeCode, InterfaceReadOnlyCollectionFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IList"] = (InterfaceListFormatter.GenerateSerializeCode, InterfaceListFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyList"] = (InterfaceReadOnlyListFormatter.GenerateSerializeCode, InterfaceReadOnlyListFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IDictionary"] = (InterfaceDictionaryFormatter.GenerateSerializeCode, InterfaceDictionaryFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonDictionarySerializeCode, DictionaryFormatter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyDictionary"] = (InterfaceReadOnlyDictionaryFormatter.GenerateSerializeCode, InterfaceReadOnlyDictionaryFormatter.GenerateDeserializeCode, DictionaryFormatter.GenerateJsonDictionarySerializeCode, DictionaryFormatter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Linq.ILookup"] = (InterfaceLookupFormatter.GenerateSerializeCode, InterfaceLookupFormatter.GenerateDeserializeCode, InterfaceLookupFormatter.GenerateJsonSerializeCode, InterfaceLookupFormatter.GenerateJsonDeserializeCode),
                ["global::System.Linq.IGrouping"] = (InterfaceGroupingFormatter.GenerateSerializeCode, InterfaceGroupingFormatter.GenerateDeserializeCode, InterfaceGroupingFormatter.GenerateJsonSerializeCode, InterfaceGroupingFormatter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.ISet"] = (InterfaceSetFormatter.GenerateSerializeCode, InterfaceSetFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlySet"] = (InterfaceReadOnlySetFormatter.GenerateSerializeCode, InterfaceReadOnlySetFormatter.GenerateDeserializeCode, ListFormatter.GenerateJsonEnumerableSerializeCode, ListFormatter.GenerateJsonEnumerableDeserializeCode),

                // 不可变集合接口
                ["global::System.Collections.Immutable.IImmutableList"] = (InterfaceImmutableListFormatter.GenerateSerializeCode, InterfaceImmutableListFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableQueue"] = (InterfaceImmutableQueueFormatter.GenerateSerializeCode, InterfaceImmutableQueueFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableStack"] = (InterfaceImmutableStackFormatter.GenerateSerializeCode, InterfaceImmutableStackFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableDictionary"] = (InterfaceImmutableDictionaryFormatter.GenerateSerializeCode, InterfaceImmutableDictionaryFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableSet"] = (InterfaceImmutableSetFormatter.GenerateSerializeCode, InterfaceImmutableSetFormatter.GenerateDeserializeCode, ImmutableCollectionFormatterHelper.GenerateJsonSerializeCode, ImmutableCollectionFormatterHelper.GenerateJsonDeserializeCode),
                
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
        public static (Action<LuminLocalFieldData, StringBuilder> Write, Action<LuminLocalFieldData, StringBuilder> Read, Action<LuminLocalFieldData, StringBuilder> WriteJson, Action<LuminLocalFieldData, StringBuilder> ReadJson) GetFormatter(string typeName)
        {
            
            if (typeName.EndsWith("[]"))
            {
                return (ArrayFormatter.GenerateSerializeCode, ArrayFormatter.GenerateDeserializeCode,
                    ArrayFormatter.GenerateJsonSerializeCode, ArrayFormatter.GenerateJsonDeserializeCode);
            }

            if (MultiDimensionalArrayFormatter.IsMultiDimensionalArray(typeName))
            {
                return (MultiDimensionalArrayFormatter.GenerateSerializeCode, MultiDimensionalArrayFormatter.GenerateDeserializeCode,
                    MultiDimensionalArrayFormatter.GenerateJsonSerializeCode, MultiDimensionalArrayFormatter.GenerateJsonDeserializeCode);
            }
            
            string baseTypeName = GetBaseTypeName(typeName);
            
            if (Formatters.TryGetValue(baseTypeName, out var formatter))
            {
                return formatter;
            }
            
            

            return (null, null, null, null);
        }

        /// <summary>
        /// Returns compress-variant formatter delegates for the given type name.
        /// Array / UnmanagedArray types → compress writer/reader APIs.
        /// List types with unmanaged element types → span compress APIs.
        /// Scalar unmanaged types → normal unmanaged (no compression at scalar level).
        /// Returns (null, null) when the type has no compress variant.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) GetCompressFormatter(string typeName)
        {
            // T[] — any array type
            if (typeName.EndsWith("[]"))
            {
                // Primitive/known-value arrays registered in Formatters use the
                // UnmanagedArrayFormatter compress path directly.
                string baseTypeName = typeName.Substring(0, typeName.Length - 2);
                if (KnownValueTypes.Contains(baseTypeName) ||
                    Formatters.ContainsKey(typeName))
                {
                    return (UnmanagedArrayFormatter.GenerateSerializeCodeWithCompress,
                            UnmanagedArrayFormatter.GenerateDeserializeCodeWithCompress);
                }
                // Generic / user-defined element arrays
                return (ArrayFormatter.GenerateSerializeCodeWithCompress,
                        ArrayFormatter.GenerateDeserializeCodeWithCompress);
            }

            // List<T>
            string baseName = GetBaseTypeName(typeName);
            if (baseName == "global::System.Collections.Generic.List")
            {
                return (ListFormatter.GenerateSerializeCodeWithCompress,
                        ListFormatter.GenerateDeserializeCodeWithCompress);
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

        /// <summary>
        /// 递归剥离 [] 层，判断最终叶类型是否在 KnownValueTypes 中。
        /// 用于 WithCompress 格式化器的元素循环：只有叶类型是 KnownValueType 时，
        /// 元素才能递归调用 WriteValueWithCompress；否则回退到普通 WriteValue。
        /// 例如：
        ///   Guid[]     → leaf = Guid     → KnownValueType → true
        ///   Guid[][]   → leaf = Guid     → KnownValueType → true
        ///   IFoo       → leaf = IFoo     → not known      → false
        ///   Transform[]→ leaf = Transform→ not known      → false
        /// </summary>
        public static bool IsLeafTypeKnownValueType(string typeName)
        {
            string leaf = typeName;
            while (leaf.EndsWith("[]"))
                leaf = leaf.Substring(0, leaf.Length - 2);
            return KnownValueTypes.Contains(leaf);
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

        public static string[] GetGenericArguments(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
                return Array.Empty<string>();

            int startIndex = typeString.IndexOf('<');
            int endIndex = typeString.LastIndexOf('>');
            if (startIndex < 0 || endIndex <= startIndex)
                return Array.Empty<string>();

            return SplitGenericParameters(typeString.Substring(startIndex + 1, endIndex - startIndex - 1));
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
