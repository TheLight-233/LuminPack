using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LuminPack.SystemTextJson;

/// <summary>Uses an explicit System.Text.Json contract for a LuminPack model at a JSON boundary.</summary>
/// <typeparam name="TModel">The application model.</typeparam>
/// <typeparam name="TContract">The external JSON contract type.</typeparam>
public sealed class LuminPackJsonConverter<TModel, TContract> : JsonConverter<TModel>
    where TModel : notnull
    where TContract : notnull
{
    private readonly JsonTypeInfo<TContract> _contractTypeInfo;
    private readonly Func<TModel, TContract> _toContract;
    private readonly Func<TContract, TModel> _fromContract;

    /// <summary>Creates a converter with the metadata and mappings for one external contract.</summary>
    /// <param name="contractTypeInfo">The System.Text.Json metadata that controls the contract's JSON.</param>
    /// <param name="toContract">Maps a nonnull model to a nonnull contract value.</param>
    /// <param name="fromContract">Maps a nonnull contract value to a nonnull model.</param>
    public LuminPackJsonConverter(JsonTypeInfo<TContract> contractTypeInfo,
        Func<TModel, TContract> toContract, Func<TContract, TModel> fromContract)
    {
        if (typeof(TModel).IsAssignableFrom(typeof(TContract)))
            LuminPackExceptionHelper.ThrowOverlappingContract(nameof(contractTypeInfo));

        _contractTypeInfo = LuminPackExceptionHelper.RequireArgument(contractTypeInfo, nameof(contractTypeInfo));
        _toContract = LuminPackExceptionHelper.RequireArgument(toContract, nameof(toContract));
        _fromContract = LuminPackExceptionHelper.RequireArgument(fromContract, nameof(fromContract));
    }

    /// <inheritdoc/>
    public override TModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        TContract contract = LuminPackExceptionHelper.RequireJsonValue(JsonSerializer.Deserialize(ref reader, _contractTypeInfo),
            "Expected a nonnull JSON contract value.");
        return LuminPackExceptionHelper.RequireJsonValue(_fromContract(contract), "The contract mapping returned a null model.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, TModel value, JsonSerializerOptions options)
    {
        TContract contract = LuminPackExceptionHelper.RequireJsonValue(_toContract(value),
            "The model mapping returned a null JSON contract.");
        if (contract is TModel)
            LuminPackExceptionHelper.ThrowJsonException("The model mapping must produce a value outside the model hierarchy.");

        JsonSerializer.Serialize(writer, contract, _contractTypeInfo);
    }
}
