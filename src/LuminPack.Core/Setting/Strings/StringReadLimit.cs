using LuminPack.Core;

namespace LuminPack.Option;

/// <summary>Checks the next string's character quota without moving the reader.</summary>
/// <param name="reader">The current record.</param>
/// <param name="maximum">The UTF-16 code unit limit.</param>
internal delegate void StringLengthCheck(scoped ref LuminPackReader reader, int maximum);

/// <summary>Supplies a non-consuming character limit check for a built-in binary string codec.</summary>
internal interface ILuminPackStringReadLimit
{
    /// <summary>Gets the check for this codec's record framing.</summary>
    StringLengthCheck Check { get; }
}

/// <summary>Resolves the check once for each compiled string profile.</summary>
/// <typeparam name="TCodec">The selected codec or profile.</typeparam>
internal static class StringReadLimit<TCodec> where TCodec : struct, ILuminPackStringCodec
{
    /// <summary>Uses the built-in framing check or the custom codec's bounded skip contract.</summary>
    internal static readonly StringLengthCheck Check = default(TCodec) is ILuminPackStringReadLimit bounded ? bounded.Check : CheckCustom;

    /// <summary>Checks a custom record on a copy to preserve the caller's cursor.</summary>
    /// <param name="reader">The original record.</param>
    /// <param name="maximum">The UTF-16 code unit limit.</param>
    private static void CheckCustom(scoped ref LuminPackReader reader, int maximum)
    {
        var copy = reader;
        default(TCodec).Skip(ref copy, maximum);
    }
}
