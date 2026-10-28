using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Platforms;

/// <summary>Opts a generated DTO field or collection element into an explicitly registered platform value codec.</summary>
/// <typeparam name="T">The external platform type.</typeparam>
/// <remarks>The existing Custom formatter mechanism owns this value; there is no additional object frame or type identifier.</remarks>
[LuminPackable(GeneratorType.Custom)]
public struct LuminPackValue<T>
{
    private static LuminPackCodec<T>? s_codec;

    /// <summary>Stores the platform value.</summary>
    public T Value;

    /// <summary>Creates an explicit platform value for a generated DTO.</summary>
    /// <param name="value">The platform value.</param>
    public LuminPackValue(T value) => Value = value;

    /// <summary>Wraps an external value without changing its components.</summary>
    /// <param name="value">The external value.</param><returns>The explicit Custom contract.</returns>
    public static implicit operator LuminPackValue<T>(T value) => new(value);

    /// <summary>Obtains the wrapped external value.</summary>
    /// <param name="value">The Custom contract.</param><returns>The external value.</returns>
    public static implicit operator T(LuminPackValue<T> value) => value.Value;

    /// <summary>Retains the first module codec for this closed external type.</summary>
    /// <param name="codec">The module-owned explicit codec.</param>
    internal static void Configure(LuminPackCodec<T> codec) => Interlocked.CompareExchange(ref s_codec, codec, null);

    /// <summary>Gets the module codec, requiring explicit module initialization.</summary>
    /// <returns>The configured codec.</returns>
    private static LuminPackCodec<T> GetCodec() => s_codec ?? LuminPackExceptionHelper.ThrowInvalidOperationException<LuminPackCodec<T>>("Register the platform module before using its generated Custom value contract.");

    /// <summary>Writes the external value through the registered module codec.</summary>
    /// <param name="writer">The existing Core cursor.</param><param name="value">The explicit Custom value.</param>
    public static void Serialize(ref LuminPackWriter writer, in LuminPackValue<T> value) => GetCodec().Write(ref writer, value.Value);

    /// <summary>Reads and publishes the external value through its complete module operation.</summary>
    /// <param name="reader">The existing bounded Core cursor.</param><param name="value">The destination.</param>
    public static void Deserialize(ref LuminPackReader reader, ref LuminPackValue<T> value) => GetCodec().Read(ref reader, ref value.Value);

    /// <summary>Rejects JSON because these explicit platform contracts currently define only binary records.</summary>
    /// <param name="writer">The JSON cursor.</param><param name="value">The explicit value.</param>
    public static void SerializeJson(ref LuminPackJsonWriter writer, in LuminPackValue<T> value) => LuminPackExceptionHelper.ThrowNotSupportedException("Platform value JSON codecs are not configured.");

    /// <summary>Rejects unsupported JSON without inspecting external implementation storage.</summary>
    /// <param name="reader">The JSON cursor.</param><param name="value">The destination.</param>
    public static void DeserializeJson(ref LuminPackJsonReader reader, ref LuminPackValue<T> value) => LuminPackExceptionHelper.ThrowNotSupportedException("Platform value JSON codecs are not configured.");

    /// <summary>Adds the module's exact binary size.</summary>
    /// <param name="evaluator">The Core accumulator.</param><param name="value">The explicit value.</param>
    public static void CalculateOffset(ref LuminPackEvaluator evaluator, in LuminPackValue<T> value) => GetCodec().Size(ref evaluator, value.Value);
}
