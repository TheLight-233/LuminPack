using System;
using System.Buffers;
using System.Text;
using LuminPack;
using LuminPack.Code;
using UnityEngine;

namespace LuminPack.Unity.ContractFixture
{
    public static partial class ContractEntry
    {
        /// <summary>Checks explicit rejection of native-owner JSON for both encodings and preserves reused owners.</summary>
        private static void CheckUnsupportedJson()
        {
            var output = new ArrayBufferWriter<byte>();
            foreach (bool present in new[] { false, true })
            {
                AnimationCurve curve = present ? new AnimationCurve() : null!;
                Gradient gradient = present ? new Gradient() : null!;
                RectOffset offset = present ? new RectOffset() : null!;
                RejectJson(() => LuminPackSerializer.SerializeJson(curve));
                RejectJson(() => LuminPackSerializer.SerializeJson(gradient));
                RejectJson(() => LuminPackSerializer.SerializeJson(offset));
                RejectJson(() => LuminPackSerializer.SerializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>(curve, output));
                RejectJson(() => LuminPackSerializer.SerializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>(gradient, output));
                RejectJson(() => LuminPackSerializer.SerializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>(offset, output));
                var retainedCurve = curve;
                var retainedGradient = gradient;
                var retainedOffset = offset;
                foreach (string text in new[] { "null", "{}" })
                {
                    byte[] utf8 = Encoding.UTF8.GetBytes(text);
                    RejectJson(() => LuminPackSerializer.DeserializeJson(text, ref curve));
                    RejectJson(() => LuminPackSerializer.DeserializeJson(text, ref gradient));
                    RejectJson(() => LuminPackSerializer.DeserializeJson(text, ref offset));
                    RejectJson(() => LuminPackSerializer.DeserializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>((ReadOnlySpan<byte>)utf8, ref curve));
                    RejectJson(() => LuminPackSerializer.DeserializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>((ReadOnlySpan<byte>)utf8, ref gradient));
                    RejectJson(() => LuminPackSerializer.DeserializeJson<LuminPack.Option.LuminPackUtf8LengthOptions>((ReadOnlySpan<byte>)utf8, ref offset));
                    if (!ReferenceEquals(curve, retainedCurve) || !ReferenceEquals(gradient, retainedGradient) || !ReferenceEquals(offset, retainedOffset))
                        LuminPackExceptionHelper.ThrowInvalidOperationException("Unsupported JSON changed a native owner.");
                }
            }
        }

        /// <summary>Requires the configured unsupported-codec boundary rather than accepting an accidental native-handle fallback.</summary>
        /// <param name="operation">The concrete generated JSON call.</param>
        private static void RejectJson(Action operation)
        {
            try { operation(); }
            catch (NotSupportedException exception) when (exception.Message == "Unity native-owner JSON codecs are not configured.") { return; }
            LuminPackExceptionHelper.ThrowInvalidOperationException("Native-owner JSON did not reject its unsupported codec.");
        }
    }
}
