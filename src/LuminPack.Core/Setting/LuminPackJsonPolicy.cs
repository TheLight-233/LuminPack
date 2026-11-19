using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Utility;

namespace LuminPack.Option;

/// <summary>Provides checks shared by compiled JSON policies and custom profiles.</summary>
public static class LuminPackJsonPolicy
{
    /// <summary>Checks the document depth against a profile's limit after entering a container.</summary>
    /// <param name="depth">The depth after consuming the opening token.</param>
    /// <param name="maximum">A positive bound, or a nonpositive value for unlimited depth.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckDepth(int depth, int maximum)
    {
        if (maximum > 0 && depth > maximum)
            LuminPackExceptionHelper.ThrowFormatException("JSON nesting depth exceeds the configured maximum");
    }

    /// <summary>Checks the bytes of a UTF-8 JSON string for the strict grammar.</summary>
    /// <param name="text">The encoded string payload.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ValidateUtf8(scoped ReadOnlySpan<byte> text)
    {
        if (!StringSerializer.IsValidUtf8(text))
            LuminPackExceptionHelper.ThrowFormatException("Invalid UTF-8 in JSON string");
    }
}
