using Newtonsoft.Json;

namespace LuminPack.NewtonsoftJson;

/// <summary>Uses an explicit Newtonsoft.Json contract for a LuminPack model at a JSON boundary.</summary>
/// <typeparam name="TModel">The application model.</typeparam>
/// <typeparam name="TContract">The external JSON contract type.</typeparam>
public sealed class LuminPackJsonConverter<TModel, TContract> : JsonConverter<TModel>
    where TModel : notnull
    where TContract : notnull
{
    private readonly Func<TModel, TContract> _toContract;
    private readonly Func<TContract, TModel> _fromContract;

    /// <summary>Creates a converter with the typed mappings for one external contract.</summary>
    /// <param name="toContract">Maps a nonnull model to a nonnull contract value.</param>
    /// <param name="fromContract">Maps a nonnull contract value to a nonnull model.</param>
    public LuminPackJsonConverter(Func<TModel, TContract> toContract, Func<TContract, TModel> fromContract)
    {
        if (typeof(TModel).IsAssignableFrom(typeof(TContract)))
            LuminPackExceptionHelper.ThrowOverlappingContract(nameof(toContract));

        _toContract = LuminPackExceptionHelper.RequireArgument(toContract, nameof(toContract));
        _fromContract = LuminPackExceptionHelper.RequireArgument(fromContract, nameof(fromContract));
    }

    /// <inheritdoc/>
    public override TModel? ReadJson(JsonReader reader, Type objectType, TModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            if (default(TModel) is null)
                return default;

            LuminPackExceptionHelper.ThrowJsonException("Expected a nonnull JSON contract value.");
        }

        TContract contract = LuminPackExceptionHelper.RequireJsonValue(serializer.Deserialize<TContract>(reader),
            "Expected a nonnull JSON contract value.");
        TModel model = LuminPackExceptionHelper.RequireJsonValue(_fromContract(contract),
            "The contract mapping returned a null model.");
        if (!objectType.IsInstanceOfType(model))
            LuminPackExceptionHelper.ThrowJsonException($"The contract mapping did not return a {objectType}.");

        return model;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, TModel? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        TContract contract = LuminPackExceptionHelper.RequireJsonValue(_toContract(value),
            "The model mapping returned a null JSON contract.");
        if (contract is TModel)
            LuminPackExceptionHelper.ThrowJsonException("The model mapping must produce a value outside the model hierarchy.");

        serializer.Serialize(writer, contract, typeof(TContract));
    }
}
