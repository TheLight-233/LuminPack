using System;
using LuminPack.Code;
using LuminPack.Core;
using UnityEngine;

namespace LuminPack.Unity
{
    [global::Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstruction]
    public static unsafe partial class LuminUnity
    {
        private static readonly object RegistrationLock = new object();

        /// <summary>Registers binary callbacks for Unity values without replacing an existing application formatter.</summary>
        /// <remarks>Call at application initialization. JSON is deliberately unsupported by these binary registrations.</remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            lock (RegistrationLock)
            {
                RegisterValue<Vector2>(&Write, &Read);
                RegisterValue<Vector3>(&Write, &Read);
                RegisterValue<Vector4>(&Write, &Read);
                RegisterValue<Vector2Int>(&Write, &Read);
                RegisterValue<Vector3Int>(&Write, &Read);
                RegisterValue<Quaternion>(&Write, &Read);
                RegisterValue<Color>(&Write, &Read);
                RegisterValue<Color32>(&Write, &Read);
                RegisterValue<Rect>(&Write, &Read);
                RegisterValue<RectInt>(&Write, &Read);
                RegisterValue<Bounds>(&Write, &Read);
                RegisterValue<BoundsInt>(&Write, &Read);
                RegisterValue<Ray>(&Write, &Read);
                RegisterValue<Ray2D>(&Write, &Read);
                RegisterValue<Plane>(&Write, &Read);
                RegisterValue<Matrix4x4>(&Write, &Read);
                RegisterValue<LayerMask>(&Write, &Read);
                RegisterValue<Keyframe>(&Write, &Read);
                RegisterValue<GradientColorKey>(&Write, &Read);
                RegisterValue<GradientAlphaKey>(&Write, &Read);
                RegisterValue<RangeInt>(&Write, &Read);
                RegisterValue<AnimationCurve?>(&Write, &Read);
                RegisterValue<Gradient?>(&Write, &Read);
                RegisterValue<RectOffset?>(&Write, &Read);
            }
        }

        /// <summary>Installs one existing static codec through the ordinary explicit-registration API.</summary>
        /// <typeparam name="T">The concrete Unity value type.</typeparam>
        /// <param name="write">The complete binary writer.</param>
        /// <param name="read">The complete binary reader.</param>
        private static void RegisterValue<T>(delegate*<ref LuminPackWriter, in T, void> write,
            delegate*<ref LuminPackReader, ref T, void> read)
        {
            nint key = LuminPackMarshal.GetMethodTable(typeof(T));
            if (LuminPackFormatterRegistry.TryGetValue(in key, out _)) return;
            LuminPackFormatterRegistry.Register(typeof(T), (nint)write, (nint)read);
        }
    }
}
