using LuminPack.Utility;

namespace LuminPack.Code;

/// <summary>
/// Method-pointer entry for one manually registered type. Each field is the raw address of a
/// static method (converted to <see cref="nint"/>); 0 means the operation is not supported.
/// The generic dispatch casts the pointer back to the T-typed function pointer and calls it
/// directly, so the value is never boxed.
/// </summary>
public struct LuminPackFormatterEntry
{
    /// <summary>Stores the write value.</summary>
    public nint WriteValue;
    /// <summary>Stores the read value.</summary>
    public nint ReadValue;
    /// <summary>Stores the write value json.</summary>
    public nint WriteValueJson;
    /// <summary>Stores the read value json.</summary>
    public nint ReadValueJson;
    /// <summary>Stores the calculate offset.</summary>
    public nint CalculateOffset;
}

/// <summary>
/// Global manual-registration table for types the source generator did not cover.
/// Consulted only from the generic dispatch default path, so generated types never pay for it.
/// Keyed by the type's MethodTable (<see cref="nint"/>), matching the ALC-safe scheme: a raw
/// MethodTable key never keeps an assembly alive across ALC unloads.
/// </summary>
public static class LuminPackFormatterRegistry
{
    private static readonly LuminCircleReferenceMap<nint, LuminPackFormatterEntry> s_entries = new(16);
    private static readonly object s_sync = new();

    /// <summary>
    /// Registers the raw method pointers for a type the source generator did not cover.
    /// <paramref name="writeValue"/> and <paramref name="readValue"/> are required; the others may
    /// be 0 to leave that operation unsupported.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="writeValue">The write value used by this operation.</param>
    /// <param name="readValue">The read value used by this operation.</param>
    /// <param name="writeValueJson">The write value json used by this operation.</param>
    /// <param name="readValueJson">The read value json used by this operation.</param>
    /// <param name="calculateOffset">The calculate offset used by this operation.</param>
    public static void Register(
        Type type,
        nint writeValue,
        nint readValue,
        nint writeValueJson = 0,
        nint readValueJson = 0,
        nint calculateOffset = 0)
    {
        if (type is null) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(type));
        if (writeValue == 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(writeValue));
        if (readValue == 0) global::LuminPack.Code.LuminPackExceptionHelper.ThrowArgumentNullException(nameof(readValue));

        var entry = new LuminPackFormatterEntry
        {
            WriteValue = writeValue,
            ReadValue = readValue,
            WriteValueJson = writeValueJson,
            ReadValueJson = readValueJson,
            CalculateOffset = calculateOffset
        };

        nint key = (nint)LuminPackMarshal.GetMethodTable(type);
        lock (s_sync)
        {
            if (!s_entries.TryAdd(in key, in entry))
            {
                LuminPackExceptionHelper.ThrowArgumentException("A formatter for this type is already registered.");
            }
        }
    }

    /// <summary>Looks up the registered entry for a MethodTable key.</summary>
    /// <param name="methodTable">The method table used by this operation.</param>
    /// <param name="entry">Receives the entry produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool TryGetValue(in nint methodTable, out LuminPackFormatterEntry entry)
        => s_entries.TryGetValue(in methodTable, out entry);
}
