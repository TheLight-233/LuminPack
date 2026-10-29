using System.ComponentModel;
using LuminPack.Code;
using LuminPack.Core;

namespace LuminPack.Platforms;

/// <summary>Shares explicit binary registration across independent platform modules.</summary>
/// <remarks>Register during application initialization. Registered method pointers must remain loaded for all captured calls.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static unsafe class PlatformRegistration
{
    private static readonly object Sync = new();

    /// <summary>Installs a binary codec only when no application entry already exists.</summary>
    /// <typeparam name="T">The platform payload.</typeparam>
    /// <param name="write">The static binary writer.</param><param name="read">The static binary reader.</param>
    /// <param name="codec">The explicit codec also made available to generated Custom value wrappers.</param>
    public static void Add<T>(delegate*<ref LuminPackWriter, in T, void> write,
        delegate*<ref LuminPackReader, ref T, void> read, LuminPackCodec<T> codec)
    {
        lock (Sync)
        {
            nint key = LuminPackMarshal.GetMethodTable(typeof(T));
            if (!LuminPackFormatterRegistry.TryGetValue(in key, out _))
                LuminPackFormatterRegistry.Register(typeof(T), (nint)write, (nint)read);
            LuminPackValue<T>.Configure(codec);
            nint wrapperKey = LuminPackMarshal.GetMethodTable(typeof(LuminPackValue<T>));
            if (!LuminPackFormatterRegistry.TryGetValue(in wrapperKey, out _))
            {
                LuminPackFormatterRegistry.Register(typeof(LuminPackValue<T>),
                    (nint)(delegate*<ref LuminPackWriter, in LuminPackValue<T>, void>)&LuminPackValue<T>.Serialize,
                    (nint)(delegate*<ref LuminPackReader, ref LuminPackValue<T>, void>)&LuminPackValue<T>.Deserialize,
                    (nint)(delegate*<ref LuminPackJsonWriter, in LuminPackValue<T>, void>)&LuminPackValue<T>.SerializeJson,
                    (nint)(delegate*<ref LuminPackJsonReader, ref LuminPackValue<T>, void>)&LuminPackValue<T>.DeserializeJson);
            }
        }
    }
}
