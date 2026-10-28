using System;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using UnityEngine;

namespace LuminPack.Unity
{
    public static unsafe partial class LuminUnity
    {
        /// <summary>Writes a nullable curve as wrap modes and explicit keyframe values, never its native handle.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">Curve or null.</param>
        public static void Write(ref LuminPackWriter writer, in AnimationCurve? value)
        {
            if (value is null) { WriteNull(ref writer); return; }
            Keyframe[] keys = value.keys;
            writer.EnsureAdditionalCapacity(checked(13 + keys.Length * 32));
            writer.WriteObjectHeader(3);
            writer.Write((int)value.preWrapMode);
            writer.Write((int)value.postWrapMode);
            writer.WriteCollectionHeader(keys.Length);
            foreach (Keyframe key in keys) Write(ref writer, key);
        }

        /// <summary>Validates a complete curve before applying keys/modes to a reused or fresh native owner.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Existing owner, replaced only when null or by a null record.</param>
        public static void Read(ref LuminPackReader reader, ref AnimationCurve? value)
        {
            var cursor = reader;
            if (!ReadObject(ref cursor, 3)) { value = null; reader = cursor; return; }
            cursor.EnsureReadable(cursor.Position, 12);
            int pre = cursor.Read<int>(), post = cursor.Read<int>(), count = cursor.Read<int>();
            int bytes = PayloadBytes(count, 32);
            cursor.EnsureReadable(cursor.Position, bytes);
            var keys = new Keyframe[count];
            for (int index = 0; index < count; index++) Read(ref cursor, ref keys[index]);
            AnimationCurve result = value ?? new AnimationCurve();
            result.keys = keys;
            result.preWrapMode = (WrapMode)pre;
            result.postWrapMode = (WrapMode)post;
            value = result;
            reader = cursor;
        }

        /// <summary>Adds the exact explicit curve record size.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Curve or null.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in AnimationCurve? value)
            => evaluator.Add(value is null ? 1 : checked(13 + value.length * 32));

        /// <summary>Writes gradient interpolation, color space, color keys and alpha keys without native pointers.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">Gradient or null.</param>
        public static void Write(ref LuminPackWriter writer, in Gradient? value)
        {
            if (value is null) { WriteNull(ref writer); return; }
            GradientColorKey[] colors = value.colorKeys;
            GradientAlphaKey[] alphas = value.alphaKeys;
            writer.EnsureAdditionalCapacity(checked(17 + colors.Length * 20 + alphas.Length * 8));
            writer.WriteObjectHeader(4);
            writer.Write((int)value.mode);
            writer.Write((int)value.colorSpace);
            writer.WriteCollectionHeader(colors.Length);
            foreach (GradientColorKey key in colors) Write(ref writer, key);
            writer.WriteCollectionHeader(alphas.Length);
            foreach (GradientAlphaKey key in alphas) Write(ref writer, key);
        }

        /// <summary>Checks both complete key arrays before allocating or changing a reused gradient.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Existing gradient or fresh destination.</param>
        public static void Read(ref LuminPackReader reader, ref Gradient? value)
        {
            var cursor = reader;
            if (!ReadObject(ref cursor, 4)) { value = null; reader = cursor; return; }
            cursor.EnsureReadable(cursor.Position, 12);
            int mode = cursor.Read<int>(), colorSpace = cursor.Read<int>(), colorCount = cursor.Read<int>();
            int colorBytes = PayloadBytes(colorCount, 20);
            cursor.EnsureReadable(cursor.Position, checked(colorBytes + sizeof(int)));
            int alphaHeader = checked(cursor.Position + colorBytes);
            int alphaCount = MemoryMarshal.Read<int>(cursor.Buffer.Slice(alphaHeader, sizeof(int)));
            int alphaBytes = PayloadBytes(alphaCount, 8);
            cursor.EnsureReadable(checked(alphaHeader + sizeof(int)), alphaBytes);
            var colors = new GradientColorKey[colorCount];
            var alphas = new GradientAlphaKey[alphaCount];
            for (int index = 0; index < colorCount; index++) Read(ref cursor, ref colors[index]);
            if (cursor.Read<int>() != alphaCount) LuminPackExceptionHelper.ThrowInvalidOperationException("Gradient input changed during deserialization.");
            for (int index = 0; index < alphaCount; index++) Read(ref cursor, ref alphas[index]);
            Gradient result = value ?? new Gradient();
            result.SetKeys(colors, alphas);
            result.mode = (GradientMode)mode;
            result.colorSpace = (ColorSpace)colorSpace;
            value = result;
            reader = cursor;
        }

        /// <summary>Adds the exact explicit gradient record size.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Gradient or null.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Gradient? value)
            => evaluator.Add(value is null ? 1 : checked(17 + value.colorKeys.Length * 20 + value.alphaKeys.Length * 8));

        /// <summary>Writes the four public inset values of a nullable native RectOffset owner.</summary>
        /// <param name="writer">Output cursor.</param><param name="value">Offset or null.</param>
        public static void Write(ref LuminPackWriter writer, in RectOffset? value)
        {
            if (value is null) { WriteNull(ref writer); return; }
            writer.EnsureAdditionalCapacity(17);
            writer.WriteObjectHeader(4);
            writer.Write(value.left); writer.Write(value.right); writer.Write(value.top); writer.Write(value.bottom);
        }

        /// <summary>Reads all four insets before updating an existing native owner.</summary>
        /// <param name="reader">Bounded input.</param><param name="value">Existing offset or fresh destination.</param>
        public static void Read(ref LuminPackReader reader, ref RectOffset? value)
        {
            var cursor = reader;
            if (!ReadObject(ref cursor, 4)) { value = null; reader = cursor; return; }
            cursor.EnsureReadable(cursor.Position, 16);
            int left = cursor.Read<int>(), right = cursor.Read<int>(), top = cursor.Read<int>(), bottom = cursor.Read<int>();
            RectOffset result = value ?? new RectOffset();
            result.left = left; result.right = right; result.top = top; result.bottom = bottom;
            value = result;
            reader = cursor;
        }

        /// <summary>Adds the exact inset record size.</summary>
        /// <param name="evaluator">Size accumulator.</param><param name="value">Offset or null.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in RectOffset? value) => evaluator.Add(value is null ? 1 : 17);

        /// <summary>Writes one complete null object record.</summary><param name="writer">Output cursor.</param>
        private static void WriteNull(ref LuminPackWriter writer)
        { writer.EnsureAdditionalCapacity(1); writer.WriteNullObjectHeader(); }

        /// <summary>Checks the object marker before exposing the input cursor.</summary>
        /// <param name="reader">Local bounded cursor.</param><param name="fields">Expected field count.</param>
        /// <returns>False for null, true for the exact supported record.</returns>
        private static bool ReadObject(ref LuminPackReader reader, byte fields)
        {
            reader.EnsureReadable(reader.Position, 1);
            if (!reader.TryReadObjectHead(out byte actual)) return false;
            if (actual != fields) LuminPackExceptionHelper.ThrowInvalidOperationException("Unexpected Unity object field count.");
            return true;
        }

        /// <summary>Validates an encoded fixed-element array count before allocating its payload.</summary>
        /// <param name="count">Non-null encoded count.</param><param name="width">Element width.</param><returns>Checked payload bytes.</returns>
        private static int PayloadBytes(int count, int width)
        {
            if (count < 0 || count > int.MaxValue / width) LuminPackExceptionHelper.ThrowInvalidOperationException("Invalid Unity key-array length.");
            return count * width;
        }
    }
}
