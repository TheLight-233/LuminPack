using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;

namespace LuminPack.Code;

/// <summary>Owns the built-in scalar and string formatter registrations shared by consuming assemblies.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackKnownFormatters
{
    /// <summary>Initializes and returns the built-in formatter table for a closed profile.</summary>
    /// <typeparam name="TOptions">The profile used by the built-in operations.</typeparam>
    /// <returns>The table containing the built-in delegates.</returns>
    public static LuminPackFormatterTable Register<TOptions>() where TOptions : struct, ILuminPackOptions
        => Cache<TOptions>.Table;

    /// <summary>Publishes the complete built-in table once per profile.</summary>
    /// <typeparam name="TOptions">The profile used by the built-in operations.</typeparam>
    private static class Cache<TOptions> where TOptions : struct, ILuminPackOptions
    {
        internal static readonly LuminPackFormatterTable Table = Create();

        /// <summary>Registers the built-in operations using their existing wire representations.</summary>
        /// <returns>The populated formatter table.</returns>
        private static LuminPackFormatterTable Create()
        {
            var table = new LuminPackFormatterTable();
            RegisterValue<sbyte>(table,
                static (ref LuminPackJsonWriter writer, in sbyte value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref sbyte value) => value = reader.Read<sbyte, TOptions>());
            RegisterValue<byte>(table,
                static (ref LuminPackJsonWriter writer, in byte value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref byte value) => value = reader.Read<byte, TOptions>());
            RegisterValue<short>(table,
                static (ref LuminPackJsonWriter writer, in short value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref short value) => value = reader.Read<short, TOptions>());
            RegisterValue<ushort>(table,
                static (ref LuminPackJsonWriter writer, in ushort value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref ushort value) => value = reader.Read<ushort, TOptions>());
            RegisterValue<int>(table,
                static (ref LuminPackJsonWriter writer, in int value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref int value) => value = reader.Read<int, TOptions>());
            RegisterValue<uint>(table,
                static (ref LuminPackJsonWriter writer, in uint value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref uint value) => value = reader.Read<uint, TOptions>());
            RegisterValue<long>(table,
                static (ref LuminPackJsonWriter writer, in long value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref long value) => value = reader.Read<long, TOptions>());
            RegisterValue<ulong>(table,
                static (ref LuminPackJsonWriter writer, in ulong value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref ulong value) => value = reader.Read<ulong, TOptions>());
            RegisterValue<float>(table,
                static (ref LuminPackJsonWriter writer, in float value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref float value) => value = reader.Read<float, TOptions>());
            RegisterValue<double>(table,
                static (ref LuminPackJsonWriter writer, in double value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref double value) => value = reader.Read<double, TOptions>());
            RegisterValue<decimal>(table,
                static (ref LuminPackJsonWriter writer, in decimal value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref decimal value) => value = reader.Read<decimal, TOptions>());
            RegisterValue<char>(table,
                static (ref LuminPackJsonWriter writer, in char value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref char value) => value = reader.Read<char, TOptions>());
            RegisterValue<bool>(table,
                static (ref LuminPackJsonWriter writer, in bool value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref bool value) => value = reader.Read<bool, LuminPackDefaultOptions>());
            RegisterValue<nint>(table,
                static (ref LuminPackJsonWriter writer, in nint value) => writer.Write<TOptions>((long)value),
                static (ref LuminPackJsonReader reader, ref nint value) => value = (nint)reader.Read<long, TOptions>());
            RegisterValue<nuint>(table,
                static (ref LuminPackJsonWriter writer, in nuint value) => writer.Write<TOptions>((ulong)value),
                static (ref LuminPackJsonReader reader, ref nuint value) => value = (nuint)reader.Read<ulong, TOptions>());
            RegisterValue<Guid>(table,
                static (ref LuminPackJsonWriter writer, in Guid value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref Guid value) => value = Guid.Parse(reader.Read<string, TOptions>()));
            RegisterValue<DateTime>(table,
                static (ref LuminPackJsonWriter writer, in DateTime value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref DateTime value) => value = DateTime.Parse(reader.Read<string, TOptions>(), null, DateTimeStyles.RoundtripKind));
            RegisterValue<DateTimeOffset>(table,
                static (ref LuminPackJsonWriter writer, in DateTimeOffset value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref DateTimeOffset value) => value = DateTimeOffset.Parse(reader.Read<string, TOptions>(), null, DateTimeStyles.RoundtripKind));
            RegisterValue<TimeSpan>(table,
                static (ref LuminPackJsonWriter writer, in TimeSpan value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref TimeSpan value) => value = TimeSpan.Parse(reader.Read<string, TOptions>()));
#if NET8_0_OR_GREATER
            RegisterValue<Half>(table,
                static (ref LuminPackJsonWriter writer, in Half value) => writer.Write<TOptions>((float)value),
                static (ref LuminPackJsonReader reader, ref Half value) => value = (Half)reader.Read<float, TOptions>());
            RegisterValue<Int128>(table,
                static (ref LuminPackJsonWriter writer, in Int128 value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref Int128 value) => value = Int128.Parse(reader.Read<string, TOptions>(), CultureInfo.InvariantCulture));
            RegisterValue<UInt128>(table,
                static (ref LuminPackJsonWriter writer, in UInt128 value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref UInt128 value) => value = UInt128.Parse(reader.Read<string, TOptions>(), CultureInfo.InvariantCulture));
            RegisterValue<DateOnly>(table,
                static (ref LuminPackJsonWriter writer, in DateOnly value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref DateOnly value) => value = ReadDateOnly<TOptions>(ref reader));
            RegisterValue<TimeOnly>(table,
                static (ref LuminPackJsonWriter writer, in TimeOnly value) => writer.Write<TOptions>(value),
                static (ref LuminPackJsonReader reader, ref TimeOnly value) => value = TimeOnly.Parse(reader.Read<string, TOptions>()));
            RegisterValue<Rune>(table,
                static (ref LuminPackJsonWriter writer, in Rune value) => writer.Write<TOptions>(value.Value),
                static (ref LuminPackJsonReader reader, ref Rune value) => value = new Rune(reader.Read<int, TOptions>()));
#endif
            RegisterValue<Complex>(table, WriteComplex<TOptions>, ReadComplex<TOptions>);
            RegisterValue<Plane>(table, WritePlane<TOptions>, ReadPlane<TOptions>);
            RegisterValue<Quaternion>(table, WriteQuaternion<TOptions>, ReadQuaternion<TOptions>);
            RegisterValue<Matrix3x2>(table, WriteMatrix3x2<TOptions>, ReadMatrix3x2<TOptions>);
            RegisterValue<Matrix4x4>(table, WriteMatrix4x4<TOptions>, ReadMatrix4x4<TOptions>);
            RegisterValue<Vector2>(table, WriteVector2<TOptions>, ReadVector2<TOptions>);
            RegisterValue<Vector3>(table, WriteVector3<TOptions>, ReadVector3<TOptions>);
            RegisterValue<Vector4>(table, WriteVector4<TOptions>, ReadVector4<TOptions>);
            table.RegisterGenerated<GCHandle>(WriteValue<GCHandle>, ReadValue<GCHandle>, null, null);
            table.RegisterGenerated<string?>(
                static (ref LuminPackWriter writer, in string? value) => writer.Write<TOptions>(value),
                static (ref LuminPackReader reader, ref string? value) => reader.Read<TOptions>(ref value),
                static (ref LuminPackJsonWriter writer, in string? value) => writer.Write<TOptions>(value),
                ReadString<TOptions>);
            return table;
        }
    }

    /// <summary>Registers the raw binary and typed JSON operations of one built-in value.</summary>
    /// <typeparam name="T">The exact unmanaged value type.</typeparam>
    /// <param name="table">The built-in formatter table.</param>
    /// <param name="writeJson">The established JSON writer.</param>
    /// <param name="readJson">The established JSON reader.</param>
    private static void RegisterValue<T>(LuminPackFormatterTable table, LuminPackJsonWriteDelegate<T> writeJson, LuminPackJsonReadDelegate<T> readJson)
        where T : unmanaged => table.RegisterGenerated<T>(WriteValue<T>, ReadValue<T>, writeJson, readJson);

    /// <summary>Reserves and writes one built-in unmanaged value.</summary>
    /// <typeparam name="T">The exact unmanaged value type.</typeparam>
    /// <param name="writer">The advancing binary writer.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteValue<T>(ref LuminPackWriter writer, in T value) where T : unmanaged
    {
        writer.EnsureAdditionalCapacity(Unsafe.SizeOf<T>());
        writer.Write(in value);
    }

    /// <summary>Reads a built-in unmanaged value through the checked scalar reader.</summary>
    /// <typeparam name="T">The exact unmanaged value type.</typeparam>
    /// <param name="reader">The advancing binary reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadValue<T>(ref LuminPackReader reader, ref T value) where T : unmanaged => reader.Read(ref value);

    /// <summary>Reads an already selected JSON string or null token.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination string, which may be reused.</param>
    private static void ReadString<TOptions>(ref LuminPackJsonReader reader, ref string? value)
        where TOptions : struct, ILuminPackOptions
    {
        if (reader.CurrentTokenType == LuminPackJsonReader.JsonTokenType.Null)
        {
            value = null;
            return;
        }

        if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
            LuminPackExceptionHelper.ThrowInvalidOperationException("Expected string token");
        reader.Read<TOptions>(ref value!);
    }

#if NET8_0_OR_GREATER
    /// <summary>Parses the canonical date text from the selected JSON token.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <returns>The parsed date.</returns>
    private static DateOnly ReadDateOnly<TOptions>(ref LuminPackJsonReader reader)
        where TOptions : struct, ILuminPackOptions
    {
        if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
            return DateOnly.ParseExact(reader.Read<string, TOptions>(), "O", CultureInfo.InvariantCulture);
        if (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF16)
        {
            var text = reader.ReadStringUtf16<TOptions>();
            reader.CompleteValue();
            return DateOnly.ParseExact(text, "O", CultureInfo.InvariantCulture);
        }

        var utf8 = reader.ReadStringUtf8<TOptions>();
        reader.CompleteValue();
        if (utf8.Length == 10 && (utf8[0] | utf8[1] | utf8[2] | utf8[3] | utf8[4] | utf8[5] | utf8[6] | utf8[7] | utf8[8] | utf8[9]) <= 127)
        {
            Span<char> text = stackalloc char[10];
            for (int index = 0; index < text.Length; index++)
                text[index] = (char)utf8[index];
            return DateOnly.ParseExact(text, "O", CultureInfo.InvariantCulture);
        }
        return DateOnly.ParseExact(Encoding.UTF8.GetString(utf8), "O", CultureInfo.InvariantCulture);
    }
#endif

    /// <summary>Writes the fixed JSON component array of a Matrix3x2.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteMatrix3x2<TOptions>(ref LuminPackJsonWriter writer, in Matrix3x2 value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.M11);
        writer.Write<TOptions>(value.M12);
        writer.Write<TOptions>(value.M21);
        writer.Write<TOptions>(value.M22);
        writer.Write<TOptions>(value.M31);
        writer.Write<TOptions>(value.M32);
        writer.WriteArrayEnd<TOptions>();
    }

    /// <summary>Reads the fixed JSON component array of a Matrix3x2.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadMatrix3x2<TOptions>(ref LuminPackJsonReader reader, ref Matrix3x2 value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeArrayStart<TOptions>();
        var component0 = reader.ReadNextFloatValue<TOptions>();
        var component1 = reader.ReadNextFloatValue<TOptions>();
        var component2 = reader.ReadNextFloatValue<TOptions>();
        var component3 = reader.ReadNextFloatValue<TOptions>();
        var component4 = reader.ReadNextFloatValue<TOptions>();
        var component5 = reader.ReadNextFloatValue<TOptions>();
        reader.ConsumeArrayEnd<TOptions>();
        value = new Matrix3x2(
            component0, component1, component2, component3,
            component4, component5);
    }

    /// <summary>Writes the fixed JSON component array of a Matrix4x4.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteMatrix4x4<TOptions>(ref LuminPackJsonWriter writer, in Matrix4x4 value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.M11);
        writer.Write<TOptions>(value.M12);
        writer.Write<TOptions>(value.M13);
        writer.Write<TOptions>(value.M14);
        writer.Write<TOptions>(value.M21);
        writer.Write<TOptions>(value.M22);
        writer.Write<TOptions>(value.M23);
        writer.Write<TOptions>(value.M24);
        writer.Write<TOptions>(value.M31);
        writer.Write<TOptions>(value.M32);
        writer.Write<TOptions>(value.M33);
        writer.Write<TOptions>(value.M34);
        writer.Write<TOptions>(value.M41);
        writer.Write<TOptions>(value.M42);
        writer.Write<TOptions>(value.M43);
        writer.Write<TOptions>(value.M44);
        writer.WriteArrayEnd<TOptions>();
    }

    /// <summary>Reads the fixed JSON component array of a Matrix4x4.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadMatrix4x4<TOptions>(ref LuminPackJsonReader reader, ref Matrix4x4 value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeArrayStart<TOptions>();
        var component0 = reader.ReadNextFloatValue<TOptions>();
        var component1 = reader.ReadNextFloatValue<TOptions>();
        var component2 = reader.ReadNextFloatValue<TOptions>();
        var component3 = reader.ReadNextFloatValue<TOptions>();
        var component4 = reader.ReadNextFloatValue<TOptions>();
        var component5 = reader.ReadNextFloatValue<TOptions>();
        var component6 = reader.ReadNextFloatValue<TOptions>();
        var component7 = reader.ReadNextFloatValue<TOptions>();
        var component8 = reader.ReadNextFloatValue<TOptions>();
        var component9 = reader.ReadNextFloatValue<TOptions>();
        var component10 = reader.ReadNextFloatValue<TOptions>();
        var component11 = reader.ReadNextFloatValue<TOptions>();
        var component12 = reader.ReadNextFloatValue<TOptions>();
        var component13 = reader.ReadNextFloatValue<TOptions>();
        var component14 = reader.ReadNextFloatValue<TOptions>();
        var component15 = reader.ReadNextFloatValue<TOptions>();
        reader.ConsumeArrayEnd<TOptions>();
        value = new Matrix4x4(
            component0, component1, component2, component3,
            component4, component5, component6, component7,
            component8, component9, component10, component11,
            component12, component13, component14, component15);
    }

    /// <summary>Writes the fixed JSON component array of a Vector2.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteVector2<TOptions>(ref LuminPackJsonWriter writer, in Vector2 value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.X);
        writer.Write<TOptions>(value.Y);
        writer.WriteArrayEnd<TOptions>();
    }

    /// <summary>Reads the fixed JSON component array of a Vector2.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadVector2<TOptions>(ref LuminPackJsonReader reader, ref Vector2 value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeArrayStart<TOptions>();
        var component0 = reader.ReadNextFloatValue<TOptions>();
        var component1 = reader.ReadNextFloatValue<TOptions>();
        reader.ConsumeArrayEnd<TOptions>();
        value = new Vector2(component0, component1);
    }

    /// <summary>Writes the fixed JSON component array of a Vector3.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteVector3<TOptions>(ref LuminPackJsonWriter writer, in Vector3 value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.X);
        writer.Write<TOptions>(value.Y);
        writer.Write<TOptions>(value.Z);
        writer.WriteArrayEnd<TOptions>();
    }

    /// <summary>Reads the fixed JSON component array of a Vector3.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadVector3<TOptions>(ref LuminPackJsonReader reader, ref Vector3 value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeArrayStart<TOptions>();
        var component0 = reader.ReadNextFloatValue<TOptions>();
        var component1 = reader.ReadNextFloatValue<TOptions>();
        var component2 = reader.ReadNextFloatValue<TOptions>();
        reader.ConsumeArrayEnd<TOptions>();
        value = new Vector3(component0, component1, component2);
    }

    /// <summary>Writes the fixed JSON component array of a Vector4.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteVector4<TOptions>(ref LuminPackJsonWriter writer, in Vector4 value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.X);
        writer.Write<TOptions>(value.Y);
        writer.Write<TOptions>(value.Z);
        writer.Write<TOptions>(value.W);
        writer.WriteArrayEnd<TOptions>();
    }

    /// <summary>Reads the fixed JSON component array of a Vector4.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadVector4<TOptions>(ref LuminPackJsonReader reader, ref Vector4 value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeArrayStart<TOptions>();
        var component0 = reader.ReadNextFloatValue<TOptions>();
        var component1 = reader.ReadNextFloatValue<TOptions>();
        var component2 = reader.ReadNextFloatValue<TOptions>();
        var component3 = reader.ReadNextFloatValue<TOptions>();
        reader.ConsumeArrayEnd<TOptions>();
        value = new Vector4(component0, component1, component2, component3);
    }

    /// <summary>Writes the named JSON components of a Complex.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteComplex<TOptions>(ref LuminPackJsonWriter writer, in Complex value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteObjectStart<TOptions>();
        writer.WritePropertyName<TOptions>("Real");
        writer.Write<TOptions>(value.Real);
        writer.WritePropertyName<TOptions>("Imaginary");
        writer.Write<TOptions>(value.Imaginary);
        writer.WriteObjectEnd<TOptions>();
    }

    /// <summary>Reads the named JSON components of a Complex.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadComplex<TOptions>(ref LuminPackJsonReader reader, ref Complex value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeObjectStart<TOptions>();
        double real = default;
        double imaginary = default;
        while (reader.Read<TOptions>() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
                continue;
            string name = reader.Read<string, TOptions>();
            reader.Read<TOptions>();
            switch (name)
            {
                case "Real":
                    real = reader.Read<double, TOptions>();
                    break;
                case "Imaginary":
                    imaginary = reader.Read<double, TOptions>();
                    break;
                default:
                    reader.Skip<TOptions>();
                    break;
            }
        }
        value = new Complex(real, imaginary);
    }

    /// <summary>Writes the named JSON components of a Plane.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WritePlane<TOptions>(ref LuminPackJsonWriter writer, in Plane value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteObjectStart<TOptions>();
        writer.WritePropertyName<TOptions>("Normal");
        writer.WriteArrayStart<TOptions>();
        writer.Write<TOptions>(value.Normal.X);
        writer.Write<TOptions>(value.Normal.Y);
        writer.Write<TOptions>(value.Normal.Z);
        writer.WriteArrayEnd<TOptions>();
        writer.WritePropertyName<TOptions>("D");
        writer.Write<TOptions>(value.D);
        writer.WriteObjectEnd<TOptions>();
    }

    /// <summary>Reads the named JSON components of a Plane.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadPlane<TOptions>(ref LuminPackJsonReader reader, ref Plane value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeObjectStart<TOptions>();
        Vector3 normal = default;
        float d = default;
        while (reader.Read<TOptions>() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
                continue;
            string name = reader.Read<string, TOptions>();
            reader.Read<TOptions>();
            switch (name)
            {
                case "Normal":
                    ReadVector3<TOptions>(ref reader, ref normal);
                    break;
                case "D":
                    d = reader.Read<float, TOptions>();
                    break;
                default:
                    reader.Skip<TOptions>();
                    break;
            }
        }
        value = new Plane(normal, d);
    }

    /// <summary>Writes the named JSON components of a Quaternion.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="writer">The advancing JSON writer.</param>
    /// <param name="value">The input value.</param>
    private static void WriteQuaternion<TOptions>(ref LuminPackJsonWriter writer, in Quaternion value)
        where TOptions : struct, ILuminPackOptions
    {
        writer.WriteObjectStart<TOptions>();
        writer.WritePropertyName<TOptions>("X");
        writer.Write<TOptions>(value.X);
        writer.WritePropertyName<TOptions>("Y");
        writer.Write<TOptions>(value.Y);
        writer.WritePropertyName<TOptions>("Z");
        writer.Write<TOptions>(value.Z);
        writer.WritePropertyName<TOptions>("W");
        writer.Write<TOptions>(value.W);
        writer.WriteObjectEnd<TOptions>();
    }

    /// <summary>Reads the named JSON components of a Quaternion.</summary>
    /// <typeparam name="TOptions">The compiled JSON profile.</typeparam>
    /// <param name="reader">The advancing JSON reader.</param>
    /// <param name="value">The destination value.</param>
    private static void ReadQuaternion<TOptions>(ref LuminPackJsonReader reader, ref Quaternion value)
        where TOptions : struct, ILuminPackOptions
    {
        reader.TryConsumeObjectStart<TOptions>();
        float x = default;
        float y = default;
        float z = default;
        float w = default;
        while (reader.Read<TOptions>() && reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.ObjectEnd)
        {
            if (reader.CurrentTokenType != LuminPackJsonReader.JsonTokenType.String)
                continue;
            string name = reader.Read<string, TOptions>();
            reader.Read<TOptions>();
            switch (name)
            {
                case "X":
                    x = reader.Read<float, TOptions>();
                    break;
                case "Y":
                    y = reader.Read<float, TOptions>();
                    break;
                case "Z":
                    z = reader.Read<float, TOptions>();
                    break;
                case "W":
                    w = reader.Read<float, TOptions>();
                    break;
                default:
                    reader.Skip<TOptions>();
                    break;
            }
        }
        value = new Quaternion(x, y, z, w);
    }
}
