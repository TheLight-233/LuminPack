using System;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPack.Parsers;

[Preserve]
public sealed class UnmanagedParsers<T> : LuminPackParser<T> 
    where T : unmanaged
{
    private delegate void SerializeJsonDelegate(ref LuminPackJsonWriter writer, scoped ref T value);
    private delegate void DeserializeJsonDelegate(ref LuminPackJsonReader reader, scoped ref T value);
    
    [Preserve]
    private static readonly SerializeJsonDelegate s_serializeJson;
    
    [Preserve]
    private static readonly DeserializeJsonDelegate s_deserializeJson;
    
    [Preserve]
    private static UnmanagedParsers<T> Instance { get; } = 
        new UnmanagedParsers<T>();
    
    #region Serialize Methods
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeEnum(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var intValue = Unsafe.As<T, int>(ref value);
        writer.WriteInt(intValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeByte(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteByte(Unsafe.As<T, byte>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeSByte(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteSByte(Unsafe.As<T, sbyte>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeShort(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteShort(Unsafe.As<T, short>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUShort(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteUShort(Unsafe.As<T, ushort>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeInt(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteInt(Unsafe.As<T, int>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUInt(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteUInt(Unsafe.As<T, uint>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeLong(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteLong(Unsafe.As<T, long>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeULong(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteULong(Unsafe.As<T, ulong>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeFloat(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteFloat(Unsafe.As<T, float>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDouble(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteDouble(Unsafe.As<T, double>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDecimal(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteDecimal(Unsafe.As<T, decimal>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeChar(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteChar(Unsafe.As<T, char>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeBool(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteBool(Unsafe.As<T, bool>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeIntPtr(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ptr = Unsafe.As<T, IntPtr>(ref value);
        writer.WriteLong(ptr.ToInt64());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUIntPtr(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ptr = Unsafe.As<T, UIntPtr>(ref value);
        writer.WriteULong(ptr.ToUInt64());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeGuid(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var guid = Unsafe.As<T, Guid>(ref value);
        writer.WriteString(guid.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateTime(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dt = Unsafe.As<T, DateTime>(ref value);
        writer.WriteString(dt.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateTimeOffset(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dto = Unsafe.As<T, DateTimeOffset>(ref value);
        writer.WriteString(dto.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeTimeSpan(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ts = Unsafe.As<T, TimeSpan>(ref value);
        writer.WriteString(ts.ToString());
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeHalf(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var half = Unsafe.As<T, Half>(ref value);
        writer.WriteFloat((float)half);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeInt128(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var int128 = Unsafe.As<T, Int128>(ref value);
        writer.WriteString(int128.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUInt128(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var uint128 = Unsafe.As<T, UInt128>(ref value);
        writer.WriteString(uint128.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateOnly(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dateOnly = Unsafe.As<T, DateOnly>(ref value);
        writer.WriteString(dateOnly.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeTimeOnly(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var timeOnly = Unsafe.As<T, TimeOnly>(ref value);
        writer.WriteString(timeOnly.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeRune(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var rune = Unsafe.As<T, System.Text.Rune>(ref value);
        writer.WriteInt(rune.Value);
    }
#endif
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeComplex(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var complex = Unsafe.As<T, Complex>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("Real");
        writer.WriteDouble(complex.Real);
        writer.WritePropertyName("Imaginary");
        writer.WriteDouble(complex.Imaginary);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializePlane(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var plane = Unsafe.As<T, Plane>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("Normal");
        SerializeVector3(ref writer, ref plane.Normal);
        writer.WritePropertyName("D");
        writer.WriteFloat(plane.D);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeQuaternion(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var quaternion = Unsafe.As<T, Quaternion>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("X");
        writer.WriteFloat(quaternion.X);
        writer.WritePropertyName("Y");
        writer.WriteFloat(quaternion.Y);
        writer.WritePropertyName("Z");
        writer.WriteFloat(quaternion.Z);
        writer.WritePropertyName("W");
        writer.WriteFloat(quaternion.W);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMatrix3x2(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var matrix = Unsafe.As<T, Matrix3x2>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(matrix.M11);
        writer.WriteFloat(matrix.M12);
        writer.WriteFloat(matrix.M21);
        writer.WriteFloat(matrix.M22);
        writer.WriteFloat(matrix.M31);
        writer.WriteFloat(matrix.M32);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMatrix4x4(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var matrix = Unsafe.As<T, Matrix4x4>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(matrix.M11);
        writer.WriteFloat(matrix.M12);
        writer.WriteFloat(matrix.M13);
        writer.WriteFloat(matrix.M14);
        writer.WriteFloat(matrix.M21);
        writer.WriteFloat(matrix.M22);
        writer.WriteFloat(matrix.M23);
        writer.WriteFloat(matrix.M24);
        writer.WriteFloat(matrix.M31);
        writer.WriteFloat(matrix.M32);
        writer.WriteFloat(matrix.M33);
        writer.WriteFloat(matrix.M34);
        writer.WriteFloat(matrix.M41);
        writer.WriteFloat(matrix.M42);
        writer.WriteFloat(matrix.M43);
        writer.WriteFloat(matrix.M44);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector2(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector2>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector3(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector3>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteArrayEnd();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector3(ref LuminPackJsonWriter writer, scoped ref Vector3 vector)
    {
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector4(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector4>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteFloat(vector.W);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeGeneric(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteValue(ref value);
    }

    #endregion

    #region Deserialize Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeEnum(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var intValue = reader.ReadInt();
        value = Unsafe.As<int, T>(ref intValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeByte(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadByte();
        value = Unsafe.As<byte, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeSByte(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadSByte();
        value = Unsafe.As<sbyte, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeShort(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadShort();
        value = Unsafe.As<short, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUShort(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadUShort();
        value = Unsafe.As<ushort, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeInt(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadInt();
        value = Unsafe.As<int, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUInt(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadUInt();
        value = Unsafe.As<uint, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeLong(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadLong();
        value = Unsafe.As<long, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeULong(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadULong();
        value = Unsafe.As<ulong, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeFloat(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadFloat();
        value = Unsafe.As<float, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDouble(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadDouble();
        value = Unsafe.As<double, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDecimal(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadDecimal();
        value = Unsafe.As<decimal, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeChar(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadChar();
        value = Unsafe.As<char, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeBool(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.GetBoolean();
        value = Unsafe.As<bool, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeIntPtr(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new IntPtr(reader.ReadLong());
        value = Unsafe.As<IntPtr, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUIntPtr(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new UIntPtr(reader.ReadULong());
        value = Unsafe.As<UIntPtr, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeGuid(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = Guid.Parse(str);
        value = Unsafe.As<Guid, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateTime(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        value = Unsafe.As<DateTime, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateTimeOffset(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateTimeOffset.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        value = Unsafe.As<DateTimeOffset, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeTimeSpan(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = TimeSpan.Parse(str);
        value = Unsafe.As<TimeSpan, T>(ref result);
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeHalf(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = (Half)reader.ReadFloat();
        value = Unsafe.As<Half, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeInt128(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = Int128.Parse(str);
        value = Unsafe.As<Int128, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUInt128(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = UInt128.Parse(str);
        value = Unsafe.As<UInt128, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateOnly(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateOnly.Parse(str);
        value = Unsafe.As<DateOnly, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeTimeOnly(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = TimeOnly.Parse(str);
        value = Unsafe.As<TimeOnly, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeRune(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new System.Text.Rune(reader.ReadInt());
        value = Unsafe.As<System.Text.Rune, T>(ref result);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeComplex(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        double real = 0, imaginary = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("Real"u8))
                {
                    real = reader.ReadDouble();
                }
                else if (propertyName.SequenceEqual("Imaginary"u8))
                {
                    imaginary = reader.ReadDouble();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Complex(real, imaginary);
        value = Unsafe.As<Complex, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializePlane(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        Vector3 normal = default;
        float d = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("Normal"u8))
                {
                    DeserializeVector3(ref reader, ref normal);
                }
                else if (propertyName.SequenceEqual("D"u8))
                {
                    d = reader.ReadFloat();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Plane(normal, d);
        value = Unsafe.As<Plane, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeQuaternion(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        float x = 0, y = 0, z = 0, w = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("X"u8))
                {
                    x = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("Y"u8))
                {
                    y = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("Z"u8))
                {
                    z = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("W"u8))
                {
                    w = reader.ReadFloat();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Quaternion(x, y, z, w);
        value = Unsafe.As<Quaternion, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeMatrix3x2(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var m11 = reader.ReadFloat();
        var m12 = reader.ReadFloat();
        var m21 = reader.ReadFloat();
        var m22 = reader.ReadFloat();
        var m31 = reader.ReadFloat();
        var m32 = reader.ReadFloat();
        reader.Read();
        
        var result = new Matrix3x2(m11, m12, m21, m22, m31, m32);
        value = Unsafe.As<Matrix3x2, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeMatrix4x4(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var m11 = reader.ReadFloat();
        var m12 = reader.ReadFloat();
        var m13 = reader.ReadFloat();
        var m14 = reader.ReadFloat();
        var m21 = reader.ReadFloat();
        var m22 = reader.ReadFloat();
        var m23 = reader.ReadFloat();
        var m24 = reader.ReadFloat();
        var m31 = reader.ReadFloat();
        var m32 = reader.ReadFloat();
        var m33 = reader.ReadFloat();
        var m34 = reader.ReadFloat();
        var m41 = reader.ReadFloat();
        var m42 = reader.ReadFloat();
        var m43 = reader.ReadFloat();
        var m44 = reader.ReadFloat();
        reader.Read();
        
        var result = new Matrix4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
        value = Unsafe.As<Matrix4x4, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector2(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector2(x, y);
        value = Unsafe.As<Vector2, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector3(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector3(x, y, z);
        value = Unsafe.As<Vector3, T>(ref result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector3(ref LuminPackJsonReader reader, scoped ref Vector3 value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        reader.Read();
        
        value = new Vector3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector4(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        var w = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector4(x, y, z, w);
        value = Unsafe.As<Vector4, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeGeneric(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.ReadValue(ref value);
    }

    #endregion
    
    static UnmanagedParsers()
    {
        var type = typeof(T);
        
        if (type.IsEnum)
        {
            s_serializeJson = SerializeEnum;
            s_deserializeJson = DeserializeEnum;
        }
        else if (type == typeof(byte))
        {
            s_serializeJson = SerializeByte;
            s_deserializeJson = DeserializeByte;
        }
        else if (type == typeof(sbyte))
        {
            s_serializeJson = SerializeSByte;
            s_deserializeJson = DeserializeSByte;
        }
        else if (type == typeof(short))
        {
            s_serializeJson = SerializeShort;
            s_deserializeJson = DeserializeShort;
        }
        else if (type == typeof(ushort))
        {
            s_serializeJson = SerializeUShort;
            s_deserializeJson = DeserializeUShort;
        }
        else if (type == typeof(int))
        {
            s_serializeJson = SerializeInt;
            s_deserializeJson = DeserializeInt;
        }
        else if (type == typeof(uint))
        {
            s_serializeJson = SerializeUInt;
            s_deserializeJson = DeserializeUInt;
        }
        else if (type == typeof(long))
        {
            s_serializeJson = SerializeLong;
            s_deserializeJson = DeserializeLong;
        }
        else if (type == typeof(ulong))
        {
            s_serializeJson = SerializeULong;
            s_deserializeJson = DeserializeULong;
        }
        else if (type == typeof(float))
        {
            s_serializeJson = SerializeFloat;
            s_deserializeJson = DeserializeFloat;
        }
        else if (type == typeof(double))
        {
            s_serializeJson = SerializeDouble;
            s_deserializeJson = DeserializeDouble;
        }
        else if (type == typeof(decimal))
        {
            s_serializeJson = SerializeDecimal;
            s_deserializeJson = DeserializeDecimal;
        }
        else if (type == typeof(char))
        {
            s_serializeJson = SerializeChar;
            s_deserializeJson = DeserializeChar;
        }
        else if (type == typeof(bool))
        {
            s_serializeJson = SerializeBool;
            s_deserializeJson = DeserializeBool;
        }
        else if (type == typeof(IntPtr))
        {
            s_serializeJson = SerializeIntPtr;
            s_deserializeJson = DeserializeIntPtr;
        }
        else if (type == typeof(UIntPtr))
        {
            s_serializeJson = SerializeUIntPtr;
            s_deserializeJson = DeserializeUIntPtr;
        }
        else if (type == typeof(Guid))
        {
            s_serializeJson = SerializeGuid;
            s_deserializeJson = DeserializeGuid;
        }
        else if (type == typeof(DateTime))
        {
            s_serializeJson = SerializeDateTime;
            s_deserializeJson = DeserializeDateTime;
        }
        else if (type == typeof(DateTimeOffset))
        {
            s_serializeJson = SerializeDateTimeOffset;
            s_deserializeJson = DeserializeDateTimeOffset;
        }
        else if (type == typeof(TimeSpan))
        {
            s_serializeJson = SerializeTimeSpan;
            s_deserializeJson = DeserializeTimeSpan;
        }
#if NET8_0_OR_GREATER
        else if (type == typeof(Half))
        {
            s_serializeJson = SerializeHalf;
            s_deserializeJson = DeserializeHalf;
        }
        else if (type == typeof(Int128))
        {
            s_serializeJson = SerializeInt128;
            s_deserializeJson = DeserializeInt128;
        }
        else if (type == typeof(UInt128))
        {
            s_serializeJson = SerializeUInt128;
            s_deserializeJson = DeserializeUInt128;
        }
        else if (type == typeof(DateOnly))
        {
            s_serializeJson = SerializeDateOnly;
            s_deserializeJson = DeserializeDateOnly;
        }
        else if (type == typeof(TimeOnly))
        {
            s_serializeJson = SerializeTimeOnly;
            s_deserializeJson = DeserializeTimeOnly;
        }
        else if (type == typeof(System.Text.Rune))
        {
            s_serializeJson = SerializeRune;
            s_deserializeJson = DeserializeRune;
        }
#endif
        else if (type == typeof(Complex))
        {
            s_serializeJson = SerializeComplex;
            s_deserializeJson = DeserializeComplex;
        }
        else if (type == typeof(Plane))
        {
            s_serializeJson = SerializePlane;
            s_deserializeJson = DeserializePlane;
        }
        else if (type == typeof(Quaternion))
        {
            s_serializeJson = SerializeQuaternion;
            s_deserializeJson = DeserializeQuaternion;
        }
        else if (type == typeof(Matrix3x2))
        {
            s_serializeJson = SerializeMatrix3x2;
            s_deserializeJson = DeserializeMatrix3x2;
        }
        else if (type == typeof(Matrix4x4))
        {
            s_serializeJson = SerializeMatrix4x4;
            s_deserializeJson = DeserializeMatrix4x4;
        }
        else if (type == typeof(Vector2))
        {
            s_serializeJson = SerializeVector2;
            s_deserializeJson = DeserializeVector2;
        }
        else if (type == typeof(Vector3))
        {
            s_serializeJson = SerializeVector3;
            s_deserializeJson = DeserializeVector3;
        }
        else if (type == typeof(Vector4))
        {
            s_serializeJson = SerializeVector4;
            s_deserializeJson = DeserializeVector4;
        }
        else
        {
            s_serializeJson = SerializeGeneric;
            s_deserializeJson = DeserializeGeneric;
        }
        
        LuminPackParseProvider.RegisterParsers(Instance);
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T value)
    {
        Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);
        writer.Advance(Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T value)
    {
        value = Unsafe.ReadUnaligned<T>(ref reader.GetCurrentSpanReference());
        reader.Advance(Unsafe.SizeOf<T>());
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T value)
    {
        evaluator += Unsafe.SizeOf<T>();
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        s_serializeJson(ref writer, ref value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T value)
    {
        s_deserializeJson(ref reader, ref value);
    }
}

[Preserve]
public sealed class DangerousUnmanagedParsers<T> : LuminPackParser<T> 
{
    private delegate void SerializeJsonDelegate(ref LuminPackJsonWriter writer, scoped ref T value);
    private delegate void DeserializeJsonDelegate(ref LuminPackJsonReader reader, scoped ref T value);
    
    [Preserve]
    private static readonly SerializeJsonDelegate s_serializeJson;
    
    [Preserve]
    private static readonly DeserializeJsonDelegate s_deserializeJson;
    
    [Preserve]
    private static DangerousUnmanagedParsers<T> Instance { get; } = 
        new DangerousUnmanagedParsers<T>();
    
    #region Serialize Methods
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeEnum(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var intValue = Unsafe.As<T, int>(ref value);
        writer.WriteInt(intValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeByte(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteByte(Unsafe.As<T, byte>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeSByte(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteSByte(Unsafe.As<T, sbyte>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeShort(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteShort(Unsafe.As<T, short>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUShort(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteUShort(Unsafe.As<T, ushort>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeInt(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteInt(Unsafe.As<T, int>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUInt(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteUInt(Unsafe.As<T, uint>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeLong(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteLong(Unsafe.As<T, long>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeULong(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteULong(Unsafe.As<T, ulong>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeFloat(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteFloat(Unsafe.As<T, float>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDouble(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteDouble(Unsafe.As<T, double>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDecimal(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteDecimal(Unsafe.As<T, decimal>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeChar(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteChar(Unsafe.As<T, char>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeBool(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteBool(Unsafe.As<T, bool>(ref value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeIntPtr(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ptr = Unsafe.As<T, IntPtr>(ref value);
        writer.WriteLong(ptr.ToInt64());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUIntPtr(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ptr = Unsafe.As<T, UIntPtr>(ref value);
        writer.WriteULong(ptr.ToUInt64());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeGuid(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var guid = Unsafe.As<T, Guid>(ref value);
        writer.WriteString(guid.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateTime(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dt = Unsafe.As<T, DateTime>(ref value);
        writer.WriteString(dt.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateTimeOffset(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dto = Unsafe.As<T, DateTimeOffset>(ref value);
        writer.WriteString(dto.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeTimeSpan(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var ts = Unsafe.As<T, TimeSpan>(ref value);
        writer.WriteString(ts.ToString());
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeHalf(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var half = Unsafe.As<T, Half>(ref value);
        writer.WriteFloat((float)half);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeInt128(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var int128 = Unsafe.As<T, Int128>(ref value);
        writer.WriteString(int128.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeUInt128(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var uint128 = Unsafe.As<T, UInt128>(ref value);
        writer.WriteString(uint128.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeDateOnly(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var dateOnly = Unsafe.As<T, DateOnly>(ref value);
        writer.WriteString(dateOnly.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeTimeOnly(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var timeOnly = Unsafe.As<T, TimeOnly>(ref value);
        writer.WriteString(timeOnly.ToString("O"));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeRune(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var rune = Unsafe.As<T, System.Text.Rune>(ref value);
        writer.WriteInt(rune.Value);
    }
#endif
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeComplex(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var complex = Unsafe.As<T, Complex>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("Real");
        writer.WriteDouble(complex.Real);
        writer.WritePropertyName("Imaginary");
        writer.WriteDouble(complex.Imaginary);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializePlane(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var plane = Unsafe.As<T, Plane>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("Normal");
        SerializeVector3(ref writer, ref plane.Normal);
        writer.WritePropertyName("D");
        writer.WriteFloat(plane.D);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeQuaternion(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var quaternion = Unsafe.As<T, Quaternion>(ref value);
        writer.WriteObjectStart();
        writer.WritePropertyName("X");
        writer.WriteFloat(quaternion.X);
        writer.WritePropertyName("Y");
        writer.WriteFloat(quaternion.Y);
        writer.WritePropertyName("Z");
        writer.WriteFloat(quaternion.Z);
        writer.WritePropertyName("W");
        writer.WriteFloat(quaternion.W);
        writer.WriteObjectEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMatrix3x2(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var matrix = Unsafe.As<T, Matrix3x2>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(matrix.M11);
        writer.WriteFloat(matrix.M12);
        writer.WriteFloat(matrix.M21);
        writer.WriteFloat(matrix.M22);
        writer.WriteFloat(matrix.M31);
        writer.WriteFloat(matrix.M32);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeMatrix4x4(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var matrix = Unsafe.As<T, Matrix4x4>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(matrix.M11);
        writer.WriteFloat(matrix.M12);
        writer.WriteFloat(matrix.M13);
        writer.WriteFloat(matrix.M14);
        writer.WriteFloat(matrix.M21);
        writer.WriteFloat(matrix.M22);
        writer.WriteFloat(matrix.M23);
        writer.WriteFloat(matrix.M24);
        writer.WriteFloat(matrix.M31);
        writer.WriteFloat(matrix.M32);
        writer.WriteFloat(matrix.M33);
        writer.WriteFloat(matrix.M34);
        writer.WriteFloat(matrix.M41);
        writer.WriteFloat(matrix.M42);
        writer.WriteFloat(matrix.M43);
        writer.WriteFloat(matrix.M44);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector2(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector2>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector3(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector3>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteArrayEnd();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector3(ref LuminPackJsonWriter writer, scoped ref Vector3 vector)
    {
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeVector4(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        var vector = Unsafe.As<T, Vector4>(ref value);
        writer.WriteArrayStart();
        writer.WriteFloat(vector.X);
        writer.WriteFloat(vector.Y);
        writer.WriteFloat(vector.Z);
        writer.WriteFloat(vector.W);
        writer.WriteArrayEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SerializeGeneric(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        writer.WriteValue(ref value);
    }

    #endregion

    #region Deserialize Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeEnum(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var intValue = reader.ReadInt();
        value = Unsafe.As<int, T>(ref intValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeByte(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadByte();
        value = Unsafe.As<byte, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeSByte(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadSByte();
        value = Unsafe.As<sbyte, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeShort(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadShort();
        value = Unsafe.As<short, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUShort(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadUShort();
        value = Unsafe.As<ushort, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeInt(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadInt();
        value = Unsafe.As<int, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUInt(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadUInt();
        value = Unsafe.As<uint, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeLong(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadLong();
        value = Unsafe.As<long, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeULong(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadULong();
        value = Unsafe.As<ulong, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeFloat(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadFloat();
        value = Unsafe.As<float, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDouble(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadDouble();
        value = Unsafe.As<double, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDecimal(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadDecimal();
        value = Unsafe.As<decimal, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeChar(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.ReadChar();
        value = Unsafe.As<char, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeBool(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = reader.GetBoolean();
        value = Unsafe.As<bool, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeIntPtr(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new IntPtr(reader.ReadLong());
        value = Unsafe.As<IntPtr, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUIntPtr(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new UIntPtr(reader.ReadULong());
        value = Unsafe.As<UIntPtr, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeGuid(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = Guid.Parse(str);
        value = Unsafe.As<Guid, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateTime(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateTime.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        value = Unsafe.As<DateTime, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateTimeOffset(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateTimeOffset.Parse(str, null, System.Globalization.DateTimeStyles.RoundtripKind);
        value = Unsafe.As<DateTimeOffset, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeTimeSpan(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = TimeSpan.Parse(str);
        value = Unsafe.As<TimeSpan, T>(ref result);
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeHalf(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = (Half)reader.ReadFloat();
        value = Unsafe.As<Half, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeInt128(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = Int128.Parse(str);
        value = Unsafe.As<Int128, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeUInt128(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = UInt128.Parse(str);
        value = Unsafe.As<UInt128, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeDateOnly(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = DateOnly.Parse(str);
        value = Unsafe.As<DateOnly, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeTimeOnly(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var str = reader.ReadString();
        var result = TimeOnly.Parse(str);
        value = Unsafe.As<TimeOnly, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeRune(ref LuminPackJsonReader reader, scoped ref T value)
    {
        var result = new System.Text.Rune(reader.ReadInt());
        value = Unsafe.As<System.Text.Rune, T>(ref result);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeComplex(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        double real = 0, imaginary = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("Real"u8))
                {
                    real = reader.ReadDouble();
                }
                else if (propertyName.SequenceEqual("Imaginary"u8))
                {
                    imaginary = reader.ReadDouble();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Complex(real, imaginary);
        value = Unsafe.As<Complex, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializePlane(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        Vector3 normal = default;
        float d = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("Normal"u8))
                {
                    DeserializeVector3(ref reader, ref normal);
                }
                else if (propertyName.SequenceEqual("D"u8))
                {
                    d = reader.ReadFloat();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Plane(normal, d);
        value = Unsafe.As<Plane, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeQuaternion(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeObjectStart();
        float x = 0, y = 0, z = 0, w = 0;
        
        while (reader.Read() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.PropertyName)
            {
                var propertyName = reader.ReadStringUtf8();
                reader.Read();
                
                if (propertyName.SequenceEqual("X"u8))
                {
                    x = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("Y"u8))
                {
                    y = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("Z"u8))
                {
                    z = reader.ReadFloat();
                }
                else if (propertyName.SequenceEqual("W"u8))
                {
                    w = reader.ReadFloat();
                }
                else
                {
                    reader.Skip();
                }
            }
        }
        
        var result = new Quaternion(x, y, z, w);
        value = Unsafe.As<Quaternion, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeMatrix3x2(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var m11 = reader.ReadFloat();
        var m12 = reader.ReadFloat();
        var m21 = reader.ReadFloat();
        var m22 = reader.ReadFloat();
        var m31 = reader.ReadFloat();
        var m32 = reader.ReadFloat();
        reader.Read();
        
        var result = new Matrix3x2(m11, m12, m21, m22, m31, m32);
        value = Unsafe.As<Matrix3x2, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeMatrix4x4(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var m11 = reader.ReadFloat();
        var m12 = reader.ReadFloat();
        var m13 = reader.ReadFloat();
        var m14 = reader.ReadFloat();
        var m21 = reader.ReadFloat();
        var m22 = reader.ReadFloat();
        var m23 = reader.ReadFloat();
        var m24 = reader.ReadFloat();
        var m31 = reader.ReadFloat();
        var m32 = reader.ReadFloat();
        var m33 = reader.ReadFloat();
        var m34 = reader.ReadFloat();
        var m41 = reader.ReadFloat();
        var m42 = reader.ReadFloat();
        var m43 = reader.ReadFloat();
        var m44 = reader.ReadFloat();
        reader.Read();
        
        var result = new Matrix4x4(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
        value = Unsafe.As<Matrix4x4, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector2(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector2(x, y);
        value = Unsafe.As<Vector2, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector3(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector3(x, y, z);
        value = Unsafe.As<Vector3, T>(ref result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector3(ref LuminPackJsonReader reader, scoped ref Vector3 value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        reader.Read();
        
        value = new Vector3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeVector4(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.TryConsumeArrayStart();
        var x = reader.ReadFloat();
        var y = reader.ReadFloat();
        var z = reader.ReadFloat();
        var w = reader.ReadFloat();
        reader.Read();
        
        var result = new Vector4(x, y, z, w);
        value = Unsafe.As<Vector4, T>(ref result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeserializeGeneric(ref LuminPackJsonReader reader, scoped ref T value)
    {
        reader.ReadValue(ref value);
    }

    #endregion
    
    static DangerousUnmanagedParsers()
    {
        var type = typeof(T);
        
        if (type.IsEnum)
        {
            s_serializeJson = SerializeEnum;
            s_deserializeJson = DeserializeEnum;
        }
        else if (type == typeof(byte))
        {
            s_serializeJson = SerializeByte;
            s_deserializeJson = DeserializeByte;
        }
        else if (type == typeof(sbyte))
        {
            s_serializeJson = SerializeSByte;
            s_deserializeJson = DeserializeSByte;
        }
        else if (type == typeof(short))
        {
            s_serializeJson = SerializeShort;
            s_deserializeJson = DeserializeShort;
        }
        else if (type == typeof(ushort))
        {
            s_serializeJson = SerializeUShort;
            s_deserializeJson = DeserializeUShort;
        }
        else if (type == typeof(int))
        {
            s_serializeJson = SerializeInt;
            s_deserializeJson = DeserializeInt;
        }
        else if (type == typeof(uint))
        {
            s_serializeJson = SerializeUInt;
            s_deserializeJson = DeserializeUInt;
        }
        else if (type == typeof(long))
        {
            s_serializeJson = SerializeLong;
            s_deserializeJson = DeserializeLong;
        }
        else if (type == typeof(ulong))
        {
            s_serializeJson = SerializeULong;
            s_deserializeJson = DeserializeULong;
        }
        else if (type == typeof(float))
        {
            s_serializeJson = SerializeFloat;
            s_deserializeJson = DeserializeFloat;
        }
        else if (type == typeof(double))
        {
            s_serializeJson = SerializeDouble;
            s_deserializeJson = DeserializeDouble;
        }
        else if (type == typeof(decimal))
        {
            s_serializeJson = SerializeDecimal;
            s_deserializeJson = DeserializeDecimal;
        }
        else if (type == typeof(char))
        {
            s_serializeJson = SerializeChar;
            s_deserializeJson = DeserializeChar;
        }
        else if (type == typeof(bool))
        {
            s_serializeJson = SerializeBool;
            s_deserializeJson = DeserializeBool;
        }
        else if (type == typeof(IntPtr))
        {
            s_serializeJson = SerializeIntPtr;
            s_deserializeJson = DeserializeIntPtr;
        }
        else if (type == typeof(UIntPtr))
        {
            s_serializeJson = SerializeUIntPtr;
            s_deserializeJson = DeserializeUIntPtr;
        }
        else if (type == typeof(Guid))
        {
            s_serializeJson = SerializeGuid;
            s_deserializeJson = DeserializeGuid;
        }
        else if (type == typeof(DateTime))
        {
            s_serializeJson = SerializeDateTime;
            s_deserializeJson = DeserializeDateTime;
        }
        else if (type == typeof(DateTimeOffset))
        {
            s_serializeJson = SerializeDateTimeOffset;
            s_deserializeJson = DeserializeDateTimeOffset;
        }
        else if (type == typeof(TimeSpan))
        {
            s_serializeJson = SerializeTimeSpan;
            s_deserializeJson = DeserializeTimeSpan;
        }
#if NET8_0_OR_GREATER
        else if (type == typeof(Half))
        {
            s_serializeJson = SerializeHalf;
            s_deserializeJson = DeserializeHalf;
        }
        else if (type == typeof(Int128))
        {
            s_serializeJson = SerializeInt128;
            s_deserializeJson = DeserializeInt128;
        }
        else if (type == typeof(UInt128))
        {
            s_serializeJson = SerializeUInt128;
            s_deserializeJson = DeserializeUInt128;
        }
        else if (type == typeof(DateOnly))
        {
            s_serializeJson = SerializeDateOnly;
            s_deserializeJson = DeserializeDateOnly;
        }
        else if (type == typeof(TimeOnly))
        {
            s_serializeJson = SerializeTimeOnly;
            s_deserializeJson = DeserializeTimeOnly;
        }
        else if (type == typeof(System.Text.Rune))
        {
            s_serializeJson = SerializeRune;
            s_deserializeJson = DeserializeRune;
        }
#endif
        else if (type == typeof(Complex))
        {
            s_serializeJson = SerializeComplex;
            s_deserializeJson = DeserializeComplex;
        }
        else if (type == typeof(Plane))
        {
            s_serializeJson = SerializePlane;
            s_deserializeJson = DeserializePlane;
        }
        else if (type == typeof(Quaternion))
        {
            s_serializeJson = SerializeQuaternion;
            s_deserializeJson = DeserializeQuaternion;
        }
        else if (type == typeof(Matrix3x2))
        {
            s_serializeJson = SerializeMatrix3x2;
            s_deserializeJson = DeserializeMatrix3x2;
        }
        else if (type == typeof(Matrix4x4))
        {
            s_serializeJson = SerializeMatrix4x4;
            s_deserializeJson = DeserializeMatrix4x4;
        }
        else if (type == typeof(Vector2))
        {
            s_serializeJson = SerializeVector2;
            s_deserializeJson = DeserializeVector2;
        }
        else if (type == typeof(Vector3))
        {
            s_serializeJson = SerializeVector3;
            s_deserializeJson = DeserializeVector3;
        }
        else if (type == typeof(Vector4))
        {
            s_serializeJson = SerializeVector4;
            s_deserializeJson = DeserializeVector4;
        }
        else
        {
            s_serializeJson = SerializeGeneric;
            s_deserializeJson = DeserializeGeneric;
        }
        
        LuminPackParseProvider.RegisterParsers(Instance);
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Serialize(ref LuminPackWriter writer, scoped ref T value)
    {
        Unsafe.WriteUnaligned(ref writer.GetCurrentSpanReference(), value);
        writer.Advance(Unsafe.SizeOf<T>());
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Deserialize(ref LuminPackReader reader, scoped ref T value)
    {
        value = Unsafe.ReadUnaligned<T>(ref reader.GetCurrentSpanReference());
        reader.Advance(Unsafe.SizeOf<T>());
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref T value)
    {
        evaluator += Unsafe.SizeOf<T>();
    }
    
    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void SerializeJson(ref LuminPackJsonWriter writer, scoped ref T value)
    {
        s_serializeJson(ref writer, ref value);
    }

    [Preserve]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void DeserializeJson(ref LuminPackJsonReader reader, scoped ref T value)
    {
        s_deserializeJson(ref reader, ref value);
    }
}