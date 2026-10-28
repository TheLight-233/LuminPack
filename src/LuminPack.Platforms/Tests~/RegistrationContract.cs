using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Platforms.Tests;

/// <summary>Checks that module initialization preserves an application's explicit formatter.</summary>
public static unsafe class RegistrationContract
{
    private struct ExistingValue { }

    /// <summary>Retains an existing native entry while giving the explicit Custom wrapper its selected value schema.</summary>
    public static void Verify()
    {
        LuminPackFormatterRegistry.Register(typeof(ExistingValue),
            (nint)(delegate*<ref LuminPackWriter, in ExistingValue, void>)&WriteExisting,
            (nint)(delegate*<ref LuminPackReader, ref ExistingValue, void>)&Read,
            calculateOffset: (nint)(delegate*<ref LuminPackEvaluator, in ExistingValue, void>)&Size);
        var codec = new LuminPackCodec<ExistingValue>(WriteModule, Read, Size);
        PlatformRegistration.Add<ExistingValue>(&WriteModule, &Read, &Size, codec);
        nint key = LuminPackMarshal.GetMethodTable(typeof(ExistingValue));
        CodecContract.Assert(LuminPackFormatterRegistry.TryGetValue(in key, out var entry) &&
            entry.WriteValue == (nint)(delegate*<ref LuminPackWriter, in ExistingValue, void>)&WriteExisting,
            "Platform initialization replaced an application formatter.");
        Span<byte> output = stackalloc byte[1];
        var writer = new LuminPackWriter(ref output);
        LuminPackValue<ExistingValue> value = default;
        LuminPackValue<ExistingValue>.Serialize(ref writer, value);
        CodecContract.Assert(output[0] == 0x22, "The explicit Custom wrapper lost its selected module schema.");
    }

    /// <summary>Writes the application's distinct record.</summary>
    /// <param name="writer">The output.</param><param name="value">The sentinel.</param>
    private static void WriteExisting(ref LuminPackWriter writer, in ExistingValue value)
    {
        writer.EnsureAdditionalCapacity(1);
        writer.Write((byte)0x11);
    }

    /// <summary>Writes the module's distinct record.</summary>
    /// <param name="writer">The output.</param><param name="value">The sentinel.</param>
    private static void WriteModule(ref LuminPackWriter writer, in ExistingValue value)
    {
        writer.EnsureAdditionalCapacity(1);
        writer.Write((byte)0x22);
    }

    /// <summary>Consumes one complete sentinel byte.</summary>
    /// <param name="reader">The bounded input.</param><param name="value">The destination.</param>
    private static void Read(ref LuminPackReader reader, ref ExistingValue value)
    {
        byte marker = default;
        reader.Read(ref marker);
    }

    /// <summary>Adds the exact sentinel record size.</summary>
    /// <param name="evaluator">The size accumulator.</param><param name="value">The sentinel.</param>
    private static void Size(ref LuminPackEvaluator evaluator, in ExistingValue value) => evaluator.Add(1);
}
