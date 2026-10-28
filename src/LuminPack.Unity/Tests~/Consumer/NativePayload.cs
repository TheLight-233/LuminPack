using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using UnityEngine;

namespace LuminPack.Unity.ContractFixture
{
    /// <summary>Exercises generated ordinary fields and collections containing optional native-owner codecs.</summary>
    [LuminPackable]
    public partial class NativePayload
    {
        public AnimationCurve? Curve;
        public Gradient? Gradient;
        public RectOffset? Offset;
        public AnimationCurve?[]? Curves;
        public List<Gradient?>? Gradients;
    }

    /// <summary>Runs a precompiled generated consumer inside the engine owning the native objects.</summary>
    public static class ContractEntry
    {
        /// <summary>Checks native values, nested arrays/lists, nulls and repeated owner reuse through generated entry points.</summary>
        public static void Run()
        {
            var source = new NativePayload
            {
                Curve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 5)) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.PingPong },
                Gradient = new Gradient { colorSpace = ColorSpace.Linear },
                Offset = new RectOffset(1, 2, 3, 4)
            };
            source.Curves = new[] { source.Curve, null };
            source.Gradients = new List<Gradient?> { source.Gradient, null };
            byte[] bytes = LuminPackSerializer.Serialize(source);
            NativePayload? restored = null;
            LuminPackSerializer.Deserialize(bytes, ref restored);
            if (restored?.Curve == null || restored.Gradient == null || restored.Offset == null || restored.Curves?[1] != null || restored.Gradients?[1] != null)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Generated Unity native-owner graph did not round trip.");
            if (restored!.Curve!.Evaluate(0.5f) != source.Curve.Evaluate(0.5f) || restored.Offset!.bottom != 4 || restored.Gradient!.colorSpace != ColorSpace.Linear)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Generated native object fields changed.");
            var retained = restored.Curve;
            LuminPackSerializer.Deserialize(bytes, ref restored);
            if (!ReferenceEquals(retained, restored.Curve)) LuminPackExceptionHelper.ThrowInvalidOperationException("Generated native owner reuse failed.");
            Debug.Log("LUMINPACK_UNITY_GENERATOR_OK: native owners, arrays, lists and reuse.");
        }
    }
}
