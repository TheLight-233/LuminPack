using LuminPack.Core;
using UnityEngine;
#pragma warning disable CS0618 // Preserve the editor tangent-mode field through Unity's public compatibility property.
namespace LuminPack.Unity
{
    public static unsafe partial class LuminUnity
    {
        /// <summary>Writes explicit Keyframe fields without layout padding.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">The value.</param>
        public static void Write(ref LuminPackWriter writer, in Keyframe value)
        {
            writer.EnsureAdditionalCapacity(32);
            writer.Write(value.time);
            writer.Write(value.value);
            writer.Write(value.inTangent);
            writer.Write(value.outTangent);
            writer.Write(value.tangentMode);
            writer.Write(value.weightedMode);
            writer.Write(value.inWeight);
            writer.Write(value.outWeight);
        }
        /// <summary>Checks complete Keyframe storage before staging its public fields.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Destination assigned on success.</param>
        public static void Read(ref LuminPackReader reader, ref Keyframe value)
        {
            reader.EnsureReadable(reader.Position, 32);
            Keyframe result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.time = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.value = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.inTangent = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.outTangent = field3;
            int field4 = default;
            reader.Read(ref field4);
            result.tangentMode = field4;
            WeightedMode field5 = default;
            reader.Read(ref field5);
            result.weightedMode = field5;
            float field6 = default;
            reader.Read(ref field6);
            result.inWeight = field6;
            float field7 = default;
            reader.Read(ref field7);
            result.outWeight = field7;
            value = result;
        }
        /// <summary>Adds the exact 32-byte Keyframe representation.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Value.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Keyframe value) => evaluator.Add(32);

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
        /// <summary>Adds the exact 20-byte GradientColorKey representation.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Value.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in GradientColorKey value) => evaluator.Add(20);

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
            GradientAlphaKey result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.alpha = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.time = field1;
            value = result;
        }
        /// <summary>Adds the exact 8-byte GradientAlphaKey representation.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Value.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in GradientAlphaKey value) => evaluator.Add(8);

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
            RangeInt result = default;
            int field0 = default;
            reader.Read(ref field0);
            result.start = field0;
            int field1 = default;
            reader.Read(ref field1);
            result.length = field1;
            value = result;
        }
        /// <summary>Adds the exact 8-byte RangeInt representation.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Value.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in RangeInt value) => evaluator.Add(8);

    }
}
#pragma warning restore CS0618
