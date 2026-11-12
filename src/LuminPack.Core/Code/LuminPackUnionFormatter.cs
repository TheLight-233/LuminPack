using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

/// <summary>Owns the registered Union cases for one root across generated consumers.</summary>
/// <typeparam name="TUnion">The Union root.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LuminPackUnionFormatter<TUnion>
{
    private static readonly LuminPackFormatterTable s_entries = new();
    private static readonly HashSet<int> s_generatedTags = new();

    private static volatile bool s_initialized;

    /// <summary>Reserves the wire tags emitted by the generator.</summary>
    /// <param name="tags">The generated tags.</param>
    /// <returns>True after the reservations have been published.</returns>
    public static bool ReserveTags(int[] tags)
    {
        lock (s_entries)
        {
            foreach (int tag in tags)
            {
                if (s_entries.TryGetTag(tag, out _))
                    LuminPackExceptionHelper.ThrowArgumentException("This Union tag is already registered.");
                s_generatedTags.Add(tag);
            }
            s_initialized = true;
        }
        return true;
    }

    /// <summary>Registers the four operations of an additional Union case.</summary>
    /// <typeparam name="TMember">The concrete case.</typeparam>
    /// <param name="tag">The case's wire tag.</param>
    /// <param name="writeValue">The binary operation, including its Union tag.</param>
    /// <param name="readValue">The binary payload reader.</param>
    /// <param name="writeValueJson">The JSON operation, including its Union envelope.</param>
    /// <param name="readValueJson">The JSON payload reader.</param>
    public static void Register<TMember>(int tag, LuminPackUnionWriteDelegate<TUnion> writeValue,
        LuminPackReadDelegate<TUnion> readValue, LuminPackUnionJsonWriteDelegate<TUnion> writeValueJson,
        LuminPackJsonReadDelegate<TUnion> readValueJson)
    {
        if ((uint)tag > ushort.MaxValue)
            LuminPackExceptionHelper.ThrowArgumentException("A Union tag must fit in UInt16.");
        if (!typeof(TUnion).IsAssignableFrom(typeof(TMember)))
            LuminPackExceptionHelper.ThrowArgumentException("The registered member must implement or derive from the Union root.");
        if (writeValue == null || readValue == null || writeValueJson == null || readValueJson == null)
            LuminPackExceptionHelper.ThrowArgumentException("All four Union operations are required.");
        if (!s_initialized)
        {
            RuntimeHelpers.RunClassConstructor(typeof(TUnion).TypeHandle);
            s_initialized = true;
        }
        lock (s_entries)
        {
            if (s_generatedTags.Contains(tag))
                LuminPackExceptionHelper.ThrowArgumentException("This Union tag belongs to a generated case.");
            if (s_entries.TryGetTag(tag, out _))
                LuminPackExceptionHelper.ThrowArgumentException("This Union tag is already registered.");
            if (!s_entries.TryAdd(typeof(TMember), new LuminPackFormatter
                {
                    Tag = tag,
                    WriteValue = writeValue,
                    ReadValue = readValue,
                    WriteValueJson = writeValueJson,
                    ReadValueJson = readValueJson,
                }))
                LuminPackExceptionHelper.ThrowArgumentException("A formatter for this union member is already registered.");
        }
    }

    /// <summary>Finds a registered case by wire tag.</summary>
    /// <param name="tag">The wire tag.</param>
    /// <param name="formatter">Receives the registered operations.</param>
    /// <returns>Whether this tag has a live registration.</returns>
    public static bool TryGetFormatter(int tag, out LuminPackFormatter formatter)
    {
        lock (s_entries)
            return s_entries.TryGetTag(tag, out formatter);
    }

    /// <summary>Finds a registered case by its concrete runtime type.</summary>
    /// <param name="type">The concrete case.</param>
    /// <param name="formatter">Receives the registered operations.</param>
    /// <returns>Whether this type has a live registration.</returns>
    public static bool TryGetFormatter(Type type, out LuminPackFormatter formatter)
    {
        lock (s_entries)
            return s_entries.TryGetValue(LuminPackMarshal.GetMethodTable(type), out formatter);
    }
}
