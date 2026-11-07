using System.ComponentModel;
using LuminPack.Code;

namespace LuminPack.Platforms;

/// <summary>Shares explicit binary registration across independent platform modules.</summary>
/// <remarks>Register during application initialization. The registry retains typed delegates for the payload type's lifetime.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PlatformRegistration
{
    private static readonly object Sync = new();

    /// <summary>Installs a binary codec only when no application entry already exists.</summary>
    /// <typeparam name="T">The platform payload.</typeparam>
    /// <param name="write">The typed binary writer.</param><param name="read">The typed binary reader.</param>
    /// <param name="codec">The explicit codec also made available to generated Custom value wrappers.</param>
    public static void Add<T>(LuminPackWriteDelegate<T> write,
        LuminPackReadDelegate<T> read, LuminPackCodec<T> codec)
    {
        lock (Sync)
        {
            nint key = LuminPackMarshal.GetMethodTable(typeof(T));
            if (!LuminPackFormatterRegistry.TryGetValue(in key, out _))
                LuminPackFormatterRegistry.Register(typeof(T), write, read);
            LuminPackValue<T>.Configure(codec);
            nint wrapperKey = LuminPackMarshal.GetMethodTable(typeof(LuminPackValue<T>));
            if (!LuminPackFormatterRegistry.TryGetValue(in wrapperKey, out _))
            {
                LuminPackFormatterRegistry.Register(typeof(LuminPackValue<T>),
                    (LuminPackWriteDelegate<LuminPackValue<T>>)LuminPackValue<T>.Serialize,
                    (LuminPackReadDelegate<LuminPackValue<T>>)LuminPackValue<T>.Deserialize,
                    (LuminPackJsonWriteDelegate<LuminPackValue<T>>)LuminPackValue<T>.SerializeJson,
                    (LuminPackJsonReadDelegate<LuminPackValue<T>>)LuminPackValue<T>.DeserializeJson);
            }
        }
    }
}
