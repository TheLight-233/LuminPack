using System;
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
    public nint WriteValue;
    public nint ReadValue;
    public nint WriteValueJson;
    public nint ReadValueJson;
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
    public static void Register(
        Type type,
        nint writeValue,
        nint readValue,
        nint writeValueJson = 0,
        nint readValueJson = 0,
        nint calculateOffset = 0)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));
        if (writeValue == 0) throw new ArgumentNullException(nameof(writeValue));
        if (readValue == 0) throw new ArgumentNullException(nameof(readValue));

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
    public static bool TryGetValue(in nint methodTable, out LuminPackFormatterEntry entry)
        => s_entries.TryGetValue(in methodTable, out entry);
}