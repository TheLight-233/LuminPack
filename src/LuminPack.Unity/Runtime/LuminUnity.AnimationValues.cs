using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using LuminPack.Core;
using UnityEngine;
#pragma warning disable CS0618 // Preserve the editor tangent-mode field through Unity's public compatibility property.
namespace LuminPack.Unity
{
    public static partial class LuminUnity
    {
        /// <summary>Writes explicit Keyframe fields without layout padding.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">The value.</param>
        public static void Write(ref LuminPackWriter writer, in Keyframe value)
        {
            writer.EnsureAdditionalCapacity(32);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(writer.Buffer), (nint)(uint)writer.Position);
            Unsafe.WriteUnaligned<float>(ref payload, value.time);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 4), value.value);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 8), value.inTangent);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 12), value.outTangent);
            Unsafe.WriteUnaligned<int>(ref Unsafe.Add(ref payload, 16), value.tangentMode);
            Unsafe.WriteUnaligned<WeightedMode>(ref Unsafe.Add(ref payload, 20), value.weightedMode);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 24), value.inWeight);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 28), value.outWeight);
            writer.Advance(32);
        }
        /// <summary>Checks complete Keyframe storage before staging its public fields.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Destination assigned on success.</param>
        public static void Read(ref LuminPackReader reader, ref Keyframe value)
        {
            reader.EnsureReadable(reader.Position, 32);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), (nint)(uint)reader.Position);
            Keyframe result = default;
            result.time = Unsafe.ReadUnaligned<float>(ref payload);
            result.value = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.inTangent = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.outTangent = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            result.tangentMode = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 16));
            result.weightedMode = Unsafe.ReadUnaligned<WeightedMode>(ref Unsafe.Add(ref payload, 20));
            result.inWeight = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 24));
            result.outWeight = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 28));
            reader.Advance(32);
            value = result;
        }
        /// <summary>Writes explicit GradientColorKey fields without layout padding.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">The value.</param>
        public static void Write(ref LuminPackWriter writer, in GradientColorKey value)
        {
            writer.EnsureAdditionalCapacity(20);
            Write(ref writer, value.color);
            writer.Write(value.time);
        }
        /// <summary>Checks complete GradientColorKey storage before staging its public fields.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Destination assigned on success.</param>
        public static void Read(ref LuminPackReader reader, ref GradientColorKey value)
        {
            reader.EnsureReadable(reader.Position, 20);
            GradientColorKey result = default;
            Color field0 = default;
            Read(ref reader, ref field0);
            result.color = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.time = field1;
            value = result;
        }
        /// <summary>Writes explicit GradientAlphaKey fields without layout padding.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">The value.</param>
        public static void Write(ref LuminPackWriter writer, in GradientAlphaKey value)
        {
            writer.EnsureAdditionalCapacity(8);
            writer.Write(value.alpha);
            writer.Write(value.time);
        }
        /// <summary>Checks complete GradientAlphaKey storage before staging its public fields.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Destination assigned on success.</param>
        public static void Read(ref LuminPackReader reader, ref GradientAlphaKey value)
        {
            reader.EnsureReadable(reader.Position, 8);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), (nint)(uint)reader.Position);
            GradientAlphaKey result = default;
            result.alpha = Unsafe.ReadUnaligned<float>(ref payload);
            result.time = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            reader.Advance(8);
            value = result;
        }
        /// <summary>Writes explicit RangeInt fields without layout padding.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">The value.</param>
        public static void Write(ref LuminPackWriter writer, in RangeInt value)
        {
            writer.EnsureAdditionalCapacity(8);
            writer.Write(value.start);
            writer.Write(value.length);
        }
        /// <summary>Checks complete RangeInt storage before staging its public fields.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Destination assigned on success.</param>
        public static void Read(ref LuminPackReader reader, ref RangeInt value)
        {
            reader.EnsureReadable(reader.Position, 8);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), (nint)(uint)reader.Position);
            RangeInt result = default;
            result.start = Unsafe.ReadUnaligned<int>(ref payload);
            result.length = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 4));
            reader.Advance(8);
            value = result;
        }
    }
}
#pragma warning restore CS0618
