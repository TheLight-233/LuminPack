using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.Code;
using LuminPack.SourceGenerator.CodeEmitters;

namespace LuminPack.SourceGenerator
{
    /// <summary>Resolves binary and JSON emitters for supported scalar and collection shapes.</summary>
    public static class CodeEmitterRegistry
    {
        /// <summary>Stores the emitters.</summary>
        public static readonly Dictionary<string, (Action<LuminLocalFieldData, StringBuilder> Write, Action<LuminLocalFieldData, StringBuilder> Read, Action<LuminLocalFieldData, StringBuilder> WriteJson, Action<LuminLocalFieldData, StringBuilder> ReadJson)> Emitters =
            new(StringComparer.Ordinal)
            {
                // Primitive types and aliases.
                ["sbyte"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.SByte"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["sbyte[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.SByte[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["byte"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Byte"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["byte[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Byte[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["short"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int16"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["short[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int16[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["ushort"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt16"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["ushort[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt16[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["int"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int32"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["int[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int32[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["uint"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt32"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["uint[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt32[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["long"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int64"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["long[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int64[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["ulong"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt64"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["ulong[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt64[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["float"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Single"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["float[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Single[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["double"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Double"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["double[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Double[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["decimal"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Decimal"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["decimal[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Decimal[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["bool"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Boolean"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["bool[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Boolean[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["char"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Char"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["char[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.Char[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["nint"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.IntPtr"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["nint[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.IntPtr[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["nuint"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.UIntPtr"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["nuint[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),
                ["global::System.UIntPtr[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                // Other framework types.
                ["global::System.Guid"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Guid[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.DateTime"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.DateTime[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.DateTimeOffset"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.DateTimeOffset[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.TimeSpan"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.TimeSpan[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                // Numeric value types.
                ["global::System.Numerics.BigInteger"] = (BigIntegerEmitter.GenerateSerializeCode, BigIntegerEmitter.GenerateDeserializeCode, BigIntegerEmitter.GenerateJsonSerializeCode, BigIntegerEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Complex"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Complex[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Plane"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Plane[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Quaternion"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Quaternion[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Matrix3x2"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Matrix3x2[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Matrix4x4"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Matrix4x4[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector2"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector2[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector3"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector3[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Numerics.Vector4"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Numerics.Vector4[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                // Types available on .NET 8 and later.
                ["global::System.Half"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Half[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Int128"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Int128[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.UInt128"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.UInt128[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.DateOnly"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.DateOnly[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.TimeOnly"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.TimeOnly[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                ["global::System.Text.Rune"] = (UnmanagedEmitter.GenerateSerializeCode, UnmanagedEmitter.GenerateDeserializeCode, UnmanagedEmitter.GenerateJsonSerializeCode, UnmanagedEmitter.GenerateJsonDeserializeCode),
                ["global::System.Text.Rune[]"] = (UnmanagedArrayEmitter.GenerateSerializeCode, UnmanagedArrayEmitter.GenerateDeserializeCode, ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode),

                // String values.
                ["string"] = (StringEmitter.GenerateSerializeCode, StringEmitter.GenerateDeserializeCode, StringEmitter.GenerateJsonSerializeCode, StringEmitter.GenerateJsonDeserializeCode),
                ["global::System.String"] = (StringEmitter.GenerateSerializeCode, StringEmitter.GenerateDeserializeCode, StringEmitter.GenerateJsonSerializeCode, StringEmitter.GenerateJsonDeserializeCode),

                // URI values.
                ["global::System.Uri"] = (UriEmitter.GenerateSerializeCode, UriEmitter.GenerateDeserializeCode, UriEmitter.GenerateJsonSerializeCode, UriEmitter.GenerateJsonDeserializeCode),

                // Version values.
                ["global::System.Version"] = (VersionEmitter.GenerateSerializeCode, VersionEmitter.GenerateDeserializeCode, VersionEmitter.GenerateJsonSerializeCode, VersionEmitter.GenerateJsonDeserializeCode),

                // Bit arrays.
                ["global::System.Collections.BitArray"] = (BitArrayEmitter.GenerateSerializeCode, BitArrayEmitter.GenerateDeserializeCode, BitArrayEmitter.GenerateJsonSerializeCode, BitArrayEmitter.GenerateJsonDeserializeCode),

                // Nullable values.
                ["global::System.Nullable"] = (NullableEmitter.GenerateSerializeCode, NullableEmitter.GenerateDeserializeCode, NullableEmitter.GenerateJsonSerializeCode, NullableEmitter.GenerateJsonDeserializeCode),

                // StringBuilder values.
                ["global::System.Text.StringBuilder"] = (StringBuilderEmitter.GenerateSerializeCode, StringBuilderEmitter.GenerateDeserializeCode, StringBuilderEmitter.GenerateJsonSerializeCode, StringBuilderEmitter.GenerateJsonDeserializeCode),

                // Culture information.
                ["global::System.Globalization.CultureInfo"] = (CultureInfoEmitter.GenerateSerializeCode, CultureInfoEmitter.GenerateDeserializeCode, CultureInfoEmitter.GenerateJsonSerializeCode, CultureInfoEmitter.GenerateJsonDeserializeCode),

                // Time zone information.
                ["global::System.TimeZoneInfo"] = (TimeZoneInfoEmitter.GenerateSerializeCode, TimeZoneInfoEmitter.GenerateDeserializeCode, TimeZoneInfoEmitter.GenerateJsonSerializeCode, TimeZoneInfoEmitter.GenerateJsonDeserializeCode),

                // Type metadata.
                ["global::System.Type"] = (TypeEmitter.GenerateSerializeCode, TypeEmitter.GenerateDeserializeCode, TypeEmitter.GenerateJsonSerializeCode, TypeEmitter.GenerateJsonDeserializeCode),

                ["global::System.Lazy"] = (LazyEmitter.GenerateSerializeCode, LazyEmitter.GenerateDeserializeCode, LazyEmitter.GenerateJsonSerializeCode, LazyEmitter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.KeyValuePair"] = (KeyValuePairEmitter.GenerateSerializeCode, KeyValuePairEmitter.GenerateDeserializeCode, KeyValuePairEmitter.GenerateJsonSerializeCode, KeyValuePairEmitter.GenerateJsonDeserializeCode),
                ["global::System.Tuple"] = (TupleEmitter.GenerateSerializeCode, TupleEmitter.GenerateDeserializeCode, TupleEmitter.GenerateJsonSerializeCode, TupleEmitter.GenerateJsonDeserializeCode),
                ["global::System.ValueTuple"] = (TupleEmitter.GenerateSerializeCode, TupleEmitter.GenerateDeserializeCode, TupleEmitter.GenerateJsonSerializeCode, TupleEmitter.GenerateJsonDeserializeCode),
                ["global::System.ArraySegment"] = (MemoryEmitter.GenerateSerializeCode, MemoryEmitter.GenerateDeserializeCode, MemoryEmitter.GenerateJsonSerializeCode, MemoryEmitter.GenerateJsonDeserializeCode),
                ["global::System.Memory"] = (MemoryEmitter.GenerateSerializeCode, MemoryEmitter.GenerateDeserializeCode, MemoryEmitter.GenerateJsonSerializeCode, MemoryEmitter.GenerateJsonDeserializeCode),
                ["global::System.ReadOnlyMemory"] = (MemoryEmitter.GenerateSerializeCode, MemoryEmitter.GenerateDeserializeCode, MemoryEmitter.GenerateJsonSerializeCode, MemoryEmitter.GenerateJsonDeserializeCode),
                ["global::System.Buffers.ReadOnlySequence"] = (MemoryEmitter.GenerateSerializeCode, MemoryEmitter.GenerateDeserializeCode, MemoryEmitter.GenerateJsonSerializeCode, MemoryEmitter.GenerateJsonDeserializeCode),

                // Collection types.
                ["global::System.Collections.Generic.List"] = (ListEmitter.GenerateSerializeCode, ListEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonSerializeCode, ListEmitter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.Dictionary"] = (DictionaryEmitter.GenerateSerializeCode, DictionaryEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonSerializeCode, DictionaryEmitter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentDictionary"] = (ConcurrentDictionaryEmitter.GenerateSerializeCode, ConcurrentDictionaryEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonDictionarySerializeCode, DictionaryEmitter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.SortedDictionary"] = (SortedDictionaryEmitter.GenerateSerializeCode, SortedDictionaryEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonDictionarySerializeCode, DictionaryEmitter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.Stack"] = (StackEmitter.GenerateSerializeCode, StackEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.Queue"] = (QueueEmitter.GenerateSerializeCode, QueueEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.LinkedList"] = (LinkedListEmitter.GenerateSerializeCode, LinkedListEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.HashSet"] = (HashSetEmitter.GenerateSerializeCode, HashSetEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.SortedSet"] = (SortedSetEmitter.GenerateSerializeCode, SortedSetEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.SortedList"] = (SortedListEmitter.GenerateSerializeCode, SortedListEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonDictionarySerializeCode, DictionaryEmitter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyDictionary"] = (ReadOnlyDictionaryEmitter.GenerateSerializeCode, ReadOnlyDictionaryEmitter.GenerateDeserializeCode, ReadOnlyDictionaryEmitter.GenerateJsonSerializeCode, ReadOnlyDictionaryEmitter.GenerateJsonDeserializeCode),

                // Concurrent collections.
                ["global::System.Collections.Concurrent.BlockingCollection"] = (BlockingCollectionEmitter.GenerateSerializeCode, BlockingCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentBag"] = (ConcurrentBagEmitter.GenerateSerializeCode, ConcurrentBagEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentQueue"] = (ConcurrentQueueEmitter.GenerateSerializeCode, ConcurrentQueueEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Concurrent.ConcurrentStack"] = (ConcurrentStackEmitter.GenerateSerializeCode, ConcurrentStackEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),

                // Read-only collections.
                ["global::System.Collections.ObjectModel.Collection"] = (CollectionEmitter.GenerateSerializeCode, CollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ObservableCollection"] = (ObservableCollectionEmitter.GenerateSerializeCode, ObservableCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyCollection"] = (ReadOnlyCollectionEmitter.GenerateSerializeCode, ReadOnlyCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.ObjectModel.ReadOnlyObservableCollection"] = (ReadOnlyObservableCollectionEmitter.GenerateSerializeCode, ReadOnlyObservableCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),

                // Priority queues.
                ["global::System.Collections.Generic.PriorityQueue"] = (PriorityQueueEmitter.GenerateSerializeCode, PriorityQueueEmitter.GenerateDeserializeCode, PriorityQueueEmitter.GenerateJsonSerializeCode, PriorityQueueEmitter.GenerateJsonDeserializeCode),

                // Immutable collection types.
                ["global::System.Collections.Immutable.ImmutableArray"] = (ImmutableArrayEmitter.GenerateSerializeCode, ImmutableArrayEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableList"] = (ImmutableListEmitter.GenerateSerializeCode, ImmutableListEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableQueue"] = (ImmutableQueueEmitter.GenerateSerializeCode, ImmutableQueueEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableStack"] = (ImmutableStackEmitter.GenerateSerializeCode, ImmutableStackEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableDictionary"] = (ImmutableDictionaryEmitter.GenerateSerializeCode, ImmutableDictionaryEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableHashSet"] = (ImmutableHashSetEmitter.GenerateSerializeCode, ImmutableHashSetEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedDictionary"] = (ImmutableSortedDictionaryEmitter.GenerateSerializeCode, ImmutableSortedDictionaryEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ImmutableSortedSet"] = (ImmutableSortedSetEmitter.GenerateSerializeCode, ImmutableSortedSetEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.ReadOnlyCollectionBuilder"] = (ReadOnlyCollectionBuilderEmitter.GenerateSerializeCode, ReadOnlyCollectionBuilderEmitter.GenerateDeserializeCode, ReadOnlyCollectionBuilderEmitter.GenerateJsonSerializeCode, ReadOnlyCollectionBuilderEmitter.GenerateJsonDeserializeCode),

                // Frozen collections on .NET 8 and later.
                ["global::System.Collections.Frozen.FrozenDictionary"] = (FrozenDictionaryEmitter.GenerateSerializeCode, FrozenDictionaryEmitter.GenerateDeserializeCode, FrozenDictionaryEmitter.GenerateJsonSerializeCode, FrozenDictionaryEmitter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Frozen.FrozenSet"] = (FrozenSetEmitter.GenerateSerializeCode, FrozenSetEmitter.GenerateDeserializeCode, FrozenSetEmitter.GenerateJsonSerializeCode, FrozenSetEmitter.GenerateJsonDeserializeCode),

                // Interface types.
                ["global::System.Collections.Generic.IEnumerable"] = (InterfaceEnumerableEmitter.GenerateSerializeCode, InterfaceEnumerableEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.ICollection"] = (InterfaceCollectionEmitter.GenerateSerializeCode, InterfaceCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyCollection"] = (InterfaceReadOnlyCollectionEmitter.GenerateSerializeCode, InterfaceReadOnlyCollectionEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IList"] = (InterfaceListEmitter.GenerateSerializeCode, InterfaceListEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyList"] = (InterfaceReadOnlyListEmitter.GenerateSerializeCode, InterfaceReadOnlyListEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IDictionary"] = (InterfaceDictionaryEmitter.GenerateSerializeCode, InterfaceDictionaryEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonDictionarySerializeCode, DictionaryEmitter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlyDictionary"] = (InterfaceReadOnlyDictionaryEmitter.GenerateSerializeCode, InterfaceReadOnlyDictionaryEmitter.GenerateDeserializeCode, DictionaryEmitter.GenerateJsonDictionarySerializeCode, DictionaryEmitter.GenerateJsonDictionaryDeserializeCode),
                ["global::System.Linq.ILookup"] = (InterfaceLookupEmitter.GenerateSerializeCode, InterfaceLookupEmitter.GenerateDeserializeCode, InterfaceLookupEmitter.GenerateJsonSerializeCode, InterfaceLookupEmitter.GenerateJsonDeserializeCode),
                ["global::System.Linq.IGrouping"] = (InterfaceGroupingEmitter.GenerateSerializeCode, InterfaceGroupingEmitter.GenerateDeserializeCode, InterfaceGroupingEmitter.GenerateJsonSerializeCode, InterfaceGroupingEmitter.GenerateJsonDeserializeCode),
                ["global::System.Collections.Generic.ISet"] = (InterfaceSetEmitter.GenerateSerializeCode, InterfaceSetEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),
                ["global::System.Collections.Generic.IReadOnlySet"] = (InterfaceReadOnlySetEmitter.GenerateSerializeCode, InterfaceReadOnlySetEmitter.GenerateDeserializeCode, ListEmitter.GenerateJsonEnumerableSerializeCode, ListEmitter.GenerateJsonEnumerableDeserializeCode),

                // Immutable collection interfaces.
                ["global::System.Collections.Immutable.IImmutableList"] = (InterfaceImmutableListEmitter.GenerateSerializeCode, InterfaceImmutableListEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableQueue"] = (InterfaceImmutableQueueEmitter.GenerateSerializeCode, InterfaceImmutableQueueEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableStack"] = (InterfaceImmutableStackEmitter.GenerateSerializeCode, InterfaceImmutableStackEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableDictionary"] = (InterfaceImmutableDictionaryEmitter.GenerateSerializeCode, InterfaceImmutableDictionaryEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),
                ["global::System.Collections.Immutable.IImmutableSet"] = (InterfaceImmutableSetEmitter.GenerateSerializeCode, InterfaceImmutableSetEmitter.GenerateDeserializeCode, ImmutableCollectionEmitterHelper.GenerateJsonSerializeCode, ImmutableCollectionEmitterHelper.GenerateJsonDeserializeCode),

            };

        // Known type metadata.
        /// <summary>Stores the known value types.</summary>
        public static readonly HashSet<string> KnownValueTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            // Primitive types.
            "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
            "float", "double", "decimal", "bool", "char", "nint", "nuint",

            // Framework type aliases.
            "global::System.SByte", "global::System.Byte",
            "global::System.Int16", "global::System.UInt16",
            "global::System.Int32", "global::System.UInt32",
            "global::System.Int64", "global::System.UInt64",
            "global::System.Single", "global::System.Double",
            "global::System.Decimal", "global::System.Boolean",
            "global::System.Char",
            "global::System.IntPtr", "global::System.UIntPtr",

            // Framework unmanaged types.
            "global::System.Guid", "global::System.DateTime",
            "global::System.DateTimeOffset", "global::System.TimeSpan",

            // Numeric value types.
            "global::System.Numerics.Complex", "global::System.Numerics.Plane",
            "global::System.Numerics.Quaternion", "global::System.Numerics.Matrix3x2",
            "global::System.Numerics.Matrix4x4", "global::System.Numerics.Vector2",
            "global::System.Numerics.Vector3", "global::System.Numerics.Vector4",

            // Types available on .NET 8 and later.
            "global::System.Half", "global::System.Int128", "global::System.UInt128",
            "global::System.DateOnly", "global::System.TimeOnly", "global::System.Text.Rune",

            // Other known unmanaged types.
            "global::System.Runtime.InteropServices.GCHandle"
        };

        /// <summary>Obtains the emitter for the supplied input.</summary>
        /// <param name="typeName">The C# type name to use in generated source.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Action<LuminLocalFieldData, StringBuilder> Write, Action<LuminLocalFieldData, StringBuilder> Read, Action<LuminLocalFieldData, StringBuilder> WriteJson, Action<LuminLocalFieldData, StringBuilder> ReadJson) GetEmitter(string typeName)
        {

            if (typeName.EndsWith("[]"))
            {
                return (ArrayEmitter.GenerateSerializeCode, ArrayEmitter.GenerateDeserializeCode,
                    ArrayEmitter.GenerateJsonSerializeCode, ArrayEmitter.GenerateJsonDeserializeCode);
            }

            if (MultiDimensionalArrayEmitter.IsMultiDimensionalArray(typeName))
            {
                return (MultiDimensionalArrayEmitter.GenerateSerializeCode, MultiDimensionalArrayEmitter.GenerateDeserializeCode,
                    MultiDimensionalArrayEmitter.GenerateJsonSerializeCode, MultiDimensionalArrayEmitter.GenerateJsonDeserializeCode);
            }

            string baseTypeName = GetBaseTypeName(typeName);

            if (Emitters.TryGetValue(baseTypeName, out var emitter))
            {
                return emitter;
            }



            return (null, null, null, null);
        }

        /// <summary>
        /// Returns compression-aware code-emitter delegates for the given type name.
        /// Array / UnmanagedArray types → compress writer/reader APIs.
        /// List types with unmanaged element types → span compress APIs.
        /// Scalar unmanaged types → normal unmanaged (no compression at scalar level).
        /// Returns (null, null) when the type has no compress variant.
        /// </summary>
        /// <param name="typeName">The C# type name to use in generated source.</param>
        /// <returns>The result of the operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Action<LuminLocalFieldData, StringBuilder>, Action<LuminLocalFieldData, StringBuilder>) GetCompressEmitter(string typeName)
        {
            // T[] — any array type
            if (typeName.EndsWith("[]"))
            {
                // Primitive/known-value arrays registered in Emitters use the
                // UnmanagedArrayEmitter compress path directly.
                string baseTypeName = typeName.Substring(0, typeName.Length - 2);
                if (KnownValueTypes.Contains(baseTypeName) ||
                    Emitters.ContainsKey(typeName))
                {
                    return (UnmanagedArrayEmitter.GenerateSerializeCodeWithCompress,
                            UnmanagedArrayEmitter.GenerateDeserializeCodeWithCompress);
                }
                // Generic / user-defined element arrays
                return (ArrayEmitter.GenerateSerializeCodeWithCompress,
                        ArrayEmitter.GenerateDeserializeCodeWithCompress);
            }

            // List<T>
            string baseName = GetBaseTypeName(typeName);
            if (baseName == "global::System.Collections.Generic.List")
            {
                return (ListEmitter.GenerateSerializeCodeWithCompress,
                        ListEmitter.GenerateDeserializeCodeWithCompress);
            }

            return (null, null);
        }

        /// <summary>Obtains the base type name for the supplied input.</summary>
        /// <param name="fullTypeName">The full type name used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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
        /// Recursively removes array layers and checks the leaf type against KnownValueTypes.
        /// In WithCompress element loops, only known scalar leaf types
        /// may recursively call WriteValueWithCompress; other leaves use ordinary WriteValue.
        /// For example:
        ///   Guid[]     → leaf = Guid     → KnownValueType → true
        ///   Guid[][]   → leaf = Guid     → KnownValueType → true
        ///   IFoo       → leaf = IFoo     → not known      → false
        ///   Transform[]→ leaf = Transform→ not known      → false
        /// </summary>
        /// <param name="typeName">The C# type name to use in generated source.</param>
        /// <returns>Whether the requested condition or operation succeeded.</returns>
        public static bool IsLeafTypeKnownValueType(string typeName)
        {
            string leaf = typeName;
            while (leaf.EndsWith("[]"))
                leaf = leaf.Substring(0, leaf.Length - 2);
            return KnownValueTypes.Contains(leaf);
        }

        /// <summary>Obtains the first generic for the supplied input.</summary>
        /// <param name="typeString">The type string used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Obtains the second generic for the supplied input.</summary>
        /// <param name="typeString">The type string used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Obtains the generic arguments for the supplied input.</summary>
        /// <param name="typeString">The type string used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>Performs the split generic parameters step for the current operation.</summary>
        /// <param name="genericPart">The generic part used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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
