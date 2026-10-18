using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Code;
using LuminPack.Internal;
using LuminPack.Option;

namespace LuminPack.Core;
public unsafe ref partial struct LuminPackEvaluator
{
    /// <summary>Obtains the string length for the supplied input.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int GetStringLength<TOptions>(scoped ref string? value)
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8), (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token)) switch
        {
            (true, true) => checked(CalculateStringUtf8(ref value) + 1),
            (true, false) => checked(CalculateStringUtf8(ref value) + sizeof(int) * 2),
            (false, true) => checked(CalculateStringUtf16(ref value) + sizeof(ushort)),
            (false, false) => checked(CalculateStringUtf16(ref value) + sizeof(int))};
    }

    /// <summary>Returns the size of the string header used by the current recording mode.</summary>
    /// <returns>The result of the operation.</returns>
    /// <typeparam name = "TOptions">The stateless serialization profile specialized by the runtime compiler.</typeparam>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int StringRecordLength<TOptions>()
        where TOptions : struct, global::LuminPack.Option.ILuminPackOptions
    {
        return (default(TOptions).StringRecording == global::LuminPack.Option.LuminPackStringRecording.Token) ? ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? 1 : sizeof(ushort)) : ((default(TOptions).StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8) ? sizeof(int) * 2 : sizeof(int));
    }
}
