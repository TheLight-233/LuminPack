using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Platforms;
using Microsoft.Extensions.Primitives;

namespace LuminPack.AspNetCore;

/// <summary>Provides explicit HTTP value records without serializing framework implementation storage.</summary>
public static unsafe partial class LuminAspNetCore
{
    /// <summary>Limits the number of string values accepted by one explicit header/query value record.</summary>
    public const int MaximumStringValues = 65_536;

    /// <summary>Gets the counted, null-element-preserving HTTP string collection codec.</summary>
    public static LuminPackCodec<StringValues> StringValuesCodec { get; } = new(Write, Read);

    /// <summary>Writes a count and complete Core string records, preserving null elements and ordering.</summary>
    /// <param name="writer">The Core output cursor.</param><param name="value">The header/query values.</param>
    public static void Write(ref LuminPackWriter writer, in StringValues value)
    {
        if (value.Count > MaximumStringValues) LuminPackExceptionHelper.ThrowArgumentOutOfRangeException(nameof(value), "Too many HTTP string values.");
        var evaluator = new LuminPackEvaluator();
        int size = sizeof(int);
        for (int index = 0; index < value.Count; index++)
        {
            string? item = value[index];
            size = checked(size + evaluator.GetStringLength(ref item));
        }
        writer.EnsureAdditionalCapacity(size);
        writer.WriteCollectionHeader(value.Count);
        for (int index = 0; index < value.Count; index++) writer.Write(value[index]);
    }

    /// <summary>Checks a bounded count and parses all strings before publishing a value or cursor.</summary>
    /// <param name="reader">The bounded Core input.</param><param name="value">The destination.</param>
    public static void Read(ref LuminPackReader reader, ref StringValues value)
    {
        var pending = reader;
        int minimum = default(LuminPackWriter).StringRecordLength();
        int count = pending.ReadLength(minimum);
        if ((uint)count > MaximumStringValues) LuminPackExceptionHelper.ThrowInvalidDataException("HTTP string value count is outside the supported range.");
        string?[] values = new string?[count];
        for (int index = 0; index < count; index++) pending.Read(ref values[index]);
        StringValues candidate = new(values);
        value = candidate;
        reader = pending;
    }

    /// <summary>Registers explicit framework value codecs for generic fallback and generated Custom wrappers.</summary>
    public static void Register()
    {
        PlatformRegistration.Add<StringValues>(&Write, &Read, StringValuesCodec);
        RegisterTextValues();
    }

    /// <summary>Registers the scalar HTTP text values defined by the text codec partial.</summary>
    static partial void RegisterTextValues();
}
