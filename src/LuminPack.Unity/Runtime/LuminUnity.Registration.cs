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

        /// <summary>Registers binary and size callbacks for Unity values without replacing an existing application formatter.</summary>
        /// <remarks>Call at application initialization. JSON is deliberately unsupported by these binary registrations.</remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            lock (RegistrationLock)
            {
                RegisterValue<Vector2>(&Write, &Read, &Size);
                RegisterValue<Vector3>(&Write, &Read, &Size);
                RegisterValue<Vector4>(&Write, &Read, &Size);
                RegisterValue<Vector2Int>(&Write, &Read, &Size);
                RegisterValue<Vector3Int>(&Write, &Read, &Size);
                RegisterValue<Quaternion>(&Write, &Read, &Size);
                RegisterValue<Color>(&Write, &Read, &Size);
                RegisterValue<Color32>(&Write, &Read, &Size);
                RegisterValue<Rect>(&Write, &Read, &Size);
                RegisterValue<RectInt>(&Write, &Read, &Size);
                RegisterValue<Bounds>(&Write, &Read, &Size);
                RegisterValue<BoundsInt>(&Write, &Read, &Size);
                RegisterValue<Ray>(&Write, &Read, &Size);
                RegisterValue<Ray2D>(&Write, &Read, &Size);
                RegisterValue<Plane>(&Write, &Read, &Size);
                RegisterValue<Matrix4x4>(&Write, &Read, &Size);
                RegisterValue<LayerMask>(&Write, &Read, &Size);
                RegisterValue<Keyframe>(&Write, &Read, &Size);
                RegisterValue<GradientColorKey>(&Write, &Read, &Size);
                RegisterValue<GradientAlphaKey>(&Write, &Read, &Size);
                RegisterValue<RangeInt>(&Write, &Read, &Size);
                RegisterValue<AnimationCurve?>(&Write, &Read, &Size);
                RegisterValue<Gradient?>(&Write, &Read, &Size);
                RegisterValue<RectOffset?>(&Write, &Read, &Size);
            }
        }

        /// <summary>Installs one existing static codec through the ordinary explicit-registration API.</summary>
        /// <typeparam name="T">The concrete Unity value type.</typeparam>
        /// <param name="write">The complete binary writer.</param>
        /// <param name="read">The complete binary reader.</param>
        /// <param name="size">The exact size evaluator.</param>
        private static void RegisterValue<T>(delegate*<ref LuminPackWriter, in T, void> write,
            delegate*<ref LuminPackReader, ref T, void> read, delegate*<ref LuminPackEvaluator, in T, void> size)
        {
            nint key = LuminPackMarshal.GetMethodTable(typeof(T));
            if (LuminPackFormatterRegistry.TryGetValue(in key, out _)) return;
            LuminPackFormatterRegistry.Register(typeof(T), (nint)write, (nint)read, calculateOffset: (nint)size);
        }
    }
}
