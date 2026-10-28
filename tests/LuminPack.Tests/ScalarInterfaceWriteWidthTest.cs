using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the same native scalar interface write mechanism across storage widths and profiles.</summary>
public sealed class ScalarInterfaceWriteWidthTest
{
    /// <summary>Preserves native Boolean collection bytes in every existing binary profile.</summary>
    [Fact]
    public void BooleanProfilesKeepTheirBytes()
    {
        CheckBoolean<LuminPackUtf8LengthOptions>();
        CheckBoolean<LuminPackUtf8TokenOptions>();
        CheckBoolean<LuminPackUtf16LengthOptions>();
        CheckBoolean<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Boolean entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckBoolean<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<bool>(static count => Enumerable.Range(0, count).Select(index => index % 2 == 0).ToArray(),
            static (IList<bool> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<bool> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<bool> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Byte collection bytes in every existing binary profile.</summary>
    [Fact]
    public void ByteProfilesKeepTheirBytes()
    {
        CheckByte<LuminPackUtf8LengthOptions>();
        CheckByte<LuminPackUtf8TokenOptions>();
        CheckByte<LuminPackUtf16LengthOptions>();
        CheckByte<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Byte entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckByte<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<byte>(static count => Enumerable.Range(0, count).Select(index => (byte)index).ToArray(),
            static (IList<byte> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<byte> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<byte> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native SByte collection bytes in every existing binary profile.</summary>
    [Fact]
    public void SByteProfilesKeepTheirBytes()
    {
        CheckSByte<LuminPackUtf8LengthOptions>();
        CheckSByte<LuminPackUtf8TokenOptions>();
        CheckSByte<LuminPackUtf16LengthOptions>();
        CheckSByte<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed SByte entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckSByte<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<sbyte>(static count => Enumerable.Range(0, count).Select(index => (sbyte)index).ToArray(),
            static (IList<sbyte> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<sbyte> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<sbyte> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Int16 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Int16ProfilesKeepTheirBytes()
    {
        CheckInt16<LuminPackUtf8LengthOptions>();
        CheckInt16<LuminPackUtf8TokenOptions>();
        CheckInt16<LuminPackUtf16LengthOptions>();
        CheckInt16<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Int16 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckInt16<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<short>(static count => Enumerable.Range(0, count).Select(index => (short)(index * 31)).ToArray(),
            static (IList<short> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<short> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<short> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native UInt16 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void UInt16ProfilesKeepTheirBytes()
    {
        CheckUInt16<LuminPackUtf8LengthOptions>();
        CheckUInt16<LuminPackUtf8TokenOptions>();
        CheckUInt16<LuminPackUtf16LengthOptions>();
        CheckUInt16<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed UInt16 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckUInt16<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<ushort>(static count => Enumerable.Range(0, count).Select(index => (ushort)(index * 31)).ToArray(),
            static (IList<ushort> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<ushort> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<ushort> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Char collection bytes in every existing binary profile.</summary>
    [Fact]
    public void CharProfilesKeepTheirBytes()
    {
        CheckChar<LuminPackUtf8LengthOptions>();
        CheckChar<LuminPackUtf8TokenOptions>();
        CheckChar<LuminPackUtf16LengthOptions>();
        CheckChar<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Char entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckChar<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<char>(static count => Enumerable.Range(0, count).Select(index => (char)(index+32)).ToArray(),
            static (IList<char> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<char> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<char> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Int32 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Int32ProfilesKeepTheirBytes()
    {
        CheckInt32<LuminPackUtf8LengthOptions>();
        CheckInt32<LuminPackUtf8TokenOptions>();
        CheckInt32<LuminPackUtf16LengthOptions>();
        CheckInt32<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Int32 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckInt32<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<int>(static count => Enumerable.Range(0, count).Select(index => index * 7919 - 50000).ToArray(),
            static (IList<int> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<int> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<int> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native UInt32 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void UInt32ProfilesKeepTheirBytes()
    {
        CheckUInt32<LuminPackUtf8LengthOptions>();
        CheckUInt32<LuminPackUtf8TokenOptions>();
        CheckUInt32<LuminPackUtf16LengthOptions>();
        CheckUInt32<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed UInt32 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckUInt32<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<uint>(static count => Enumerable.Range(0, count).Select(index => (uint)index * 7919).ToArray(),
            static (IList<uint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<uint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<uint> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Int64 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Int64ProfilesKeepTheirBytes()
    {
        CheckInt64<LuminPackUtf8LengthOptions>();
        CheckInt64<LuminPackUtf8TokenOptions>();
        CheckInt64<LuminPackUtf16LengthOptions>();
        CheckInt64<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Int64 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckInt64<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<long>(static count => Enumerable.Range(0, count).Select(index => (long)index * 7919 - 50000).ToArray(),
            static (IList<long> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<long> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<long> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native UInt64 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void UInt64ProfilesKeepTheirBytes()
    {
        CheckUInt64<LuminPackUtf8LengthOptions>();
        CheckUInt64<LuminPackUtf8TokenOptions>();
        CheckUInt64<LuminPackUtf16LengthOptions>();
        CheckUInt64<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed UInt64 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckUInt64<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<ulong>(static count => Enumerable.Range(0, count).Select(index => (ulong)index * 7919).ToArray(),
            static (IList<ulong> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<ulong> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<ulong> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Single collection bytes in every existing binary profile.</summary>
    [Fact]
    public void SingleProfilesKeepTheirBytes()
    {
        CheckSingle<LuminPackUtf8LengthOptions>();
        CheckSingle<LuminPackUtf8TokenOptions>();
        CheckSingle<LuminPackUtf16LengthOptions>();
        CheckSingle<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Single entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckSingle<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<float>(static count => Enumerable.Range(0, count).Select(index => index * 1.25f).ToArray(),
            static (IList<float> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<float> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<float> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Double collection bytes in every existing binary profile.</summary>
    [Fact]
    public void DoubleProfilesKeepTheirBytes()
    {
        CheckDouble<LuminPackUtf8LengthOptions>();
        CheckDouble<LuminPackUtf8TokenOptions>();
        CheckDouble<LuminPackUtf16LengthOptions>();
        CheckDouble<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Double entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckDouble<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<double>(static count => Enumerable.Range(0, count).Select(index => index * 1.25).ToArray(),
            static (IList<double> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<double> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<double> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Decimal collection bytes in every existing binary profile.</summary>
    [Fact]
    public void DecimalProfilesKeepTheirBytes()
    {
        CheckDecimal<LuminPackUtf8LengthOptions>();
        CheckDecimal<LuminPackUtf8TokenOptions>();
        CheckDecimal<LuminPackUtf16LengthOptions>();
        CheckDecimal<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Decimal entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckDecimal<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<decimal>(static count => Enumerable.Range(0, count).Select(index => (decimal)index * 7919 / 100).ToArray(),
            static (IList<decimal> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<decimal> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<decimal> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Guid collection bytes in every existing binary profile.</summary>
    [Fact]
    public void GuidProfilesKeepTheirBytes()
    {
        CheckGuid<LuminPackUtf8LengthOptions>();
        CheckGuid<LuminPackUtf8TokenOptions>();
        CheckGuid<LuminPackUtf16LengthOptions>();
        CheckGuid<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Guid entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckGuid<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<Guid>(static count => Enumerable.Range(0, count).Select(index => new Guid(index,17,29,3,5,7,11,13,17,19,23)).ToArray(),
            static (IList<Guid> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<Guid> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<Guid> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Half collection bytes in every existing binary profile.</summary>
    [Fact]
    public void HalfProfilesKeepTheirBytes()
    {
        CheckHalf<LuminPackUtf8LengthOptions>();
        CheckHalf<LuminPackUtf8TokenOptions>();
        CheckHalf<LuminPackUtf16LengthOptions>();
        CheckHalf<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Half entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckHalf<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<Half>(static count => Enumerable.Range(0, count).Select(index => (Half)(index * 1.25f)).ToArray(),
            static (IList<Half> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<Half> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<Half> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Int128 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Int128ProfilesKeepTheirBytes()
    {
        CheckInt128<LuminPackUtf8LengthOptions>();
        CheckInt128<LuminPackUtf8TokenOptions>();
        CheckInt128<LuminPackUtf16LengthOptions>();
        CheckInt128<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Int128 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckInt128<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<Int128>(static count => Enumerable.Range(0, count).Select(index => (Int128)index << 72).ToArray(),
            static (IList<Int128> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<Int128> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<Int128> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native UInt128 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void UInt128ProfilesKeepTheirBytes()
    {
        CheckUInt128<LuminPackUtf8LengthOptions>();
        CheckUInt128<LuminPackUtf8TokenOptions>();
        CheckUInt128<LuminPackUtf16LengthOptions>();
        CheckUInt128<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed UInt128 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckUInt128<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<UInt128>(static count => Enumerable.Range(0, count).Select(index => (UInt128)index << 72).ToArray(),
            static (IList<UInt128> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<UInt128> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<UInt128> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Vector2 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Vector2ProfilesKeepTheirBytes()
    {
        CheckVector2<LuminPackUtf8LengthOptions>();
        CheckVector2<LuminPackUtf8TokenOptions>();
        CheckVector2<LuminPackUtf16LengthOptions>();
        CheckVector2<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Vector2 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckVector2<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Vector2>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Vector2(index,index+1)).ToArray(),
            static (IList<System.Numerics.Vector2> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Vector2> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Vector2> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Vector3 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Vector3ProfilesKeepTheirBytes()
    {
        CheckVector3<LuminPackUtf8LengthOptions>();
        CheckVector3<LuminPackUtf8TokenOptions>();
        CheckVector3<LuminPackUtf16LengthOptions>();
        CheckVector3<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Vector3 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckVector3<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Vector3>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Vector3(index,index+1,index+2)).ToArray(),
            static (IList<System.Numerics.Vector3> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Vector3> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Vector3> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Vector4 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Vector4ProfilesKeepTheirBytes()
    {
        CheckVector4<LuminPackUtf8LengthOptions>();
        CheckVector4<LuminPackUtf8TokenOptions>();
        CheckVector4<LuminPackUtf16LengthOptions>();
        CheckVector4<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Vector4 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckVector4<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Vector4>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Vector4(index,index+1,index+2,index+3)).ToArray(),
            static (IList<System.Numerics.Vector4> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Vector4> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Vector4> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Matrix3x2 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Matrix3x2ProfilesKeepTheirBytes()
    {
        CheckMatrix3x2<LuminPackUtf8LengthOptions>();
        CheckMatrix3x2<LuminPackUtf8TokenOptions>();
        CheckMatrix3x2<LuminPackUtf16LengthOptions>();
        CheckMatrix3x2<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Matrix3x2 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckMatrix3x2<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Matrix3x2>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Matrix3x2(index,2,3,4,5,6)).ToArray(),
            static (IList<System.Numerics.Matrix3x2> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Matrix3x2> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Matrix3x2> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Matrix4x4 collection bytes in every existing binary profile.</summary>
    [Fact]
    public void Matrix4x4ProfilesKeepTheirBytes()
    {
        CheckMatrix4x4<LuminPackUtf8LengthOptions>();
        CheckMatrix4x4<LuminPackUtf8TokenOptions>();
        CheckMatrix4x4<LuminPackUtf16LengthOptions>();
        CheckMatrix4x4<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Matrix4x4 entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckMatrix4x4<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Matrix4x4>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Matrix4x4(index,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16)).ToArray(),
            static (IList<System.Numerics.Matrix4x4> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Matrix4x4> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Matrix4x4> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native TimeSpan collection bytes in every existing binary profile.</summary>
    [Fact]
    public void TimeSpanProfilesKeepTheirBytes()
    {
        CheckTimeSpan<LuminPackUtf8LengthOptions>();
        CheckTimeSpan<LuminPackUtf8TokenOptions>();
        CheckTimeSpan<LuminPackUtf16LengthOptions>();
        CheckTimeSpan<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed TimeSpan entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckTimeSpan<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<TimeSpan>(static count => Enumerable.Range(0, count).Select(index => TimeSpan.FromTicks(index * 7919L)).ToArray(),
            static (IList<TimeSpan> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<TimeSpan> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<TimeSpan> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native DateTime collection bytes in every existing binary profile.</summary>
    [Fact]
    public void DateTimeProfilesKeepTheirBytes()
    {
        CheckDateTime<LuminPackUtf8LengthOptions>();
        CheckDateTime<LuminPackUtf8TokenOptions>();
        CheckDateTime<LuminPackUtf16LengthOptions>();
        CheckDateTime<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed DateTime entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckDateTime<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<DateTime>(static count => Enumerable.Range(0, count).Select(index => new DateTime(index * 7919L,DateTimeKind.Utc)).ToArray(),
            static (IList<DateTime> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<DateTime> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<DateTime> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native DateOnly collection bytes in every existing binary profile.</summary>
    [Fact]
    public void DateOnlyProfilesKeepTheirBytes()
    {
        CheckDateOnly<LuminPackUtf8LengthOptions>();
        CheckDateOnly<LuminPackUtf8TokenOptions>();
        CheckDateOnly<LuminPackUtf16LengthOptions>();
        CheckDateOnly<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed DateOnly entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckDateOnly<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<DateOnly>(static count => Enumerable.Range(0, count).Select(index => DateOnly.FromDayNumber(index+1)).ToArray(),
            static (IList<DateOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<DateOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<DateOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native TimeOnly collection bytes in every existing binary profile.</summary>
    [Fact]
    public void TimeOnlyProfilesKeepTheirBytes()
    {
        CheckTimeOnly<LuminPackUtf8LengthOptions>();
        CheckTimeOnly<LuminPackUtf8TokenOptions>();
        CheckTimeOnly<LuminPackUtf16LengthOptions>();
        CheckTimeOnly<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed TimeOnly entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckTimeOnly<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<TimeOnly>(static count => Enumerable.Range(0, count).Select(index => new TimeOnly(index * 7919L)).ToArray(),
            static (IList<TimeOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<TimeOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<TimeOnly> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Rune collection bytes in every existing binary profile.</summary>
    [Fact]
    public void RuneProfilesKeepTheirBytes()
    {
        CheckRune<LuminPackUtf8LengthOptions>();
        CheckRune<LuminPackUtf8TokenOptions>();
        CheckRune<LuminPackUtf16LengthOptions>();
        CheckRune<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Rune entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckRune<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Text.Rune>(static count => Enumerable.Range(0, count).Select(index => new System.Text.Rune(index+32)).ToArray(),
            static (IList<System.Text.Rune> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Text.Rune> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Text.Rune> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native DateTimeOffset collection bytes in every existing binary profile.</summary>
    [Fact]
    public void DateTimeOffsetProfilesKeepTheirBytes()
    {
        CheckDateTimeOffset<LuminPackUtf8LengthOptions>();
        CheckDateTimeOffset<LuminPackUtf8TokenOptions>();
        CheckDateTimeOffset<LuminPackUtf16LengthOptions>();
        CheckDateTimeOffset<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed DateTimeOffset entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckDateTimeOffset<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<DateTimeOffset>(static count => Enumerable.Range(0, count).Select(index => new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.FromHours(9)).AddTicks(index * 7919L)).ToArray(),
            static (IList<DateTimeOffset> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<DateTimeOffset> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<DateTimeOffset> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Complex collection bytes in every existing binary profile.</summary>
    [Fact]
    public void ComplexProfilesKeepTheirBytes()
    {
        CheckComplex<LuminPackUtf8LengthOptions>();
        CheckComplex<LuminPackUtf8TokenOptions>();
        CheckComplex<LuminPackUtf16LengthOptions>();
        CheckComplex<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Complex entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckComplex<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Complex>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Complex(index,index+1)).ToArray(),
            static (IList<System.Numerics.Complex> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Complex> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Complex> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Plane collection bytes in every existing binary profile.</summary>
    [Fact]
    public void PlaneProfilesKeepTheirBytes()
    {
        CheckPlane<LuminPackUtf8LengthOptions>();
        CheckPlane<LuminPackUtf8TokenOptions>();
        CheckPlane<LuminPackUtf16LengthOptions>();
        CheckPlane<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Plane entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckPlane<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Plane>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Plane(index,2,3,4)).ToArray(),
            static (IList<System.Numerics.Plane> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Plane> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Plane> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native Quaternion collection bytes in every existing binary profile.</summary>
    [Fact]
    public void QuaternionProfilesKeepTheirBytes()
    {
        CheckQuaternion<LuminPackUtf8LengthOptions>();
        CheckQuaternion<LuminPackUtf8TokenOptions>();
        CheckQuaternion<LuminPackUtf16LengthOptions>();
        CheckQuaternion<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed Quaternion entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckQuaternion<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<System.Numerics.Quaternion>(static count => Enumerable.Range(0, count).Select(index => new System.Numerics.Quaternion(index,2,3,4)).ToArray(),
            static (IList<System.Numerics.Quaternion> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<System.Numerics.Quaternion> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<System.Numerics.Quaternion> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native IntPtr collection bytes in every existing binary profile.</summary>
    [Fact]
    public void IntPtrProfilesKeepTheirBytes()
    {
        CheckIntPtr<LuminPackUtf8LengthOptions>();
        CheckIntPtr<LuminPackUtf8TokenOptions>();
        CheckIntPtr<LuminPackUtf16LengthOptions>();
        CheckIntPtr<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed IntPtr entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckIntPtr<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<nint>(static count => Enumerable.Range(0, count).Select(index => (nint)(index * 7919 - 50000)).ToArray(),
            static (IList<nint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<nint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<nint> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Preserves native UIntPtr collection bytes in every existing binary profile.</summary>
    [Fact]
    public void UIntPtrProfilesKeepTheirBytes()
    {
        CheckUIntPtr<LuminPackUtf8LengthOptions>();
        CheckUIntPtr<LuminPackUtf8TokenOptions>();
        CheckUIntPtr<LuminPackUtf16LengthOptions>();
        CheckUIntPtr<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Selects statically closed UIntPtr entries without relying on generic payload dispatch.</summary>
    /// <typeparam name="TProfile">The original binary profile.</typeparam>
    private static void CheckUIntPtr<TProfile>() where TProfile : struct, ILuminPackOptions
        => CheckNative<nuint>(static count => Enumerable.Range(0, count).Select(index => (nuint)(index * 7919)).ToArray(),
            static (IList<nuint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (ISet<nuint> value) => LuminPackSerializer.Serialize<TProfile>(in value),
            static (IReadOnlySet<nuint> value) => LuminPackSerializer.Serialize<TProfile>(in value));

    /// <summary>Checks exact, sparse and colliding implementations through statically closed serializer callbacks.</summary>
    /// <typeparam name="T">The original native scalar type.</typeparam>
    /// <param name="create">Creates the original scalar values.</param>
    /// <param name="writeList">Writes the closed list interface.</param>
    /// <param name="writeSet">Writes the closed mutable set interface.</param>
    /// <param name="writeReadOnlySet">Writes the closed read-only set interface.</param>
    private static void CheckNative<T>(Func<int, T[]> create, Func<IList<T>, byte[]> writeList,
        Func<ISet<T>, byte[]> writeSet, Func<IReadOnlySet<T>, byte[]> writeReadOnlySet) where T : unmanaged
    {
        foreach (int count in new[] { 0, 1, 8, 97 })
        {
            var items = create(count);
            AssertWire(writeList(items.ToList()), items);
            AssertWire(writeList(items), items);
            foreach (var comparer in new IEqualityComparer<T>[] { EqualityComparer<T>.Default, new CollisionComparer<T>() })
            {
                var source = items.ToHashSet(comparer);
                source.RemoveWhere(item => EqualityComparer<T>.Default.Equals(item, items.Length == 0 ? default : items[0]));
                var expected = source.ToArray();
                AssertWire(writeSet(source), expected);
                AssertWire(writeReadOnlySet(source), expected);
            }
            Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(writeList(null!)));
        }
    }

    /// <summary>Checks the existing count header followed by the original scalar storage without reinterpretation.</summary>
    /// <typeparam name="T">The unmanaged element type.</typeparam>
    /// <param name="actual">The complete written record.</param>
    /// <param name="values">The collection's original enumeration order.</param>
    private static void AssertWire<T>(byte[] actual, T[] values) where T : unmanaged
    {
        var expected = new byte[4 + checked(values.Length * Unsafe.SizeOf<T>())];
        BinaryPrimitives.WriteInt32LittleEndian(expected, values.Length);
        MemoryMarshal.AsBytes(values.AsSpan()).CopyTo(expected.AsSpan(4));
        Assert.Equal(expected, actual);
    }

    /// <summary>Exercises sparse entry traversal independently of normal hash distribution.</summary>
    /// <typeparam name="T">The original value type.</typeparam>
    private sealed class CollisionComparer<T> : IEqualityComparer<T>
    {
        /// <summary>Compares values through the original default comparer.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>Whether the original values compare equal.</returns>
        public bool Equals(T? left, T? right) => EqualityComparer<T>.Default.Equals(left!, right!);

        /// <summary>Places every distinct value in the same collision bucket.</summary>
        /// <param name="value">The original value.</param>
        /// <returns>The common hash code.</returns>
        public int GetHashCode(T value) => 0;
    }
}
