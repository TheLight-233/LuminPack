using System;
using System.Buffers;
using LuminPack.Code;
using LuminPack.Core;
using UnityEngine;

namespace LuminPack.Unity
{
    public static partial class LuminUnity
    {
        /// <summary>Captures curve keys before output callbacks without allocating an array snapshot.</summary>
        /// <param name="writer">The output cursor.</param>
        /// <param name="value">The non-null curve, which must not be mutated concurrently.</param>
        private static void WriteCurveSnapshot(ref LuminPackWriter writer, AnimationCurve value)
        {
            int count = value.length;
            Keyframe[]? rented = null;
            Span<Keyframe> keys = count <= 16 ? stackalloc Keyframe[count] : (rented = ArrayPool<Keyframe>.Shared.Rent(count)).AsSpan(0, count);
            try
            {
#if UNITY_6000_2_OR_NEWER
                // Even an unsupported concurrent shrink must not expose uncleared scratch.
                if (count != 0)
                {
                    keys.Clear();
                    value.GetKeys(keys);
                }
#else
                for (int index = 0; index < count; index++)
                    keys[index] = value[index];
#endif
                writer.EnsureAdditionalCapacity(checked(13 + count * 32));
                writer.WriteObjectHeader(3);
                writer.Write((int)value.preWrapMode);
                writer.Write((int)value.postWrapMode);
                writer.WriteCollectionHeader(count);
                foreach (Keyframe key in keys)
                    Write(ref writer, key);
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<Keyframe>.Shared.Return(rented);
            }
        }

#if UNITY_6000_2_OR_NEWER
        /// <summary>Captures both gradient key sets before any output callback can modify the source.</summary>
        /// <param name="writer">The output cursor.</param>
        /// <param name="value">The non-null gradient, which must not be mutated concurrently.</param>
        private static void WriteGradientSnapshot(ref LuminPackWriter writer, Gradient value)
        {
            GradientColorKey[]? rentedColors = null;
            GradientAlphaKey[]? rentedAlphas = null;
            try
            {
                int colorCount = value.colorKeyCount;
                Span<GradientColorKey> colors = colorCount <= 16 ? stackalloc GradientColorKey[colorCount] : (rentedColors = ArrayPool<GradientColorKey>.Shared.Rent(colorCount)).AsSpan(0, colorCount);
                colors.Clear();
                value.GetColorKeys(colors);
                int alphaCount = value.alphaKeyCount;
                Span<GradientAlphaKey> alphas = alphaCount <= 16 ? stackalloc GradientAlphaKey[alphaCount] : (rentedAlphas = ArrayPool<GradientAlphaKey>.Shared.Rent(alphaCount)).AsSpan(0, alphaCount);
                alphas.Clear();
                value.GetAlphaKeys(alphas);
                writer.EnsureAdditionalCapacity(checked(17 + colorCount * 20 + alphaCount * 8));
                writer.WriteObjectHeader(4);
                writer.Write((int)value.mode);
                writer.Write((int)value.colorSpace);
                writer.WriteCollectionHeader(colorCount);
                foreach (GradientColorKey key in colors)
                    Write(ref writer, key);
                writer.WriteCollectionHeader(alphaCount);
                foreach (GradientAlphaKey key in alphas)
                    Write(ref writer, key);
            }
            finally
            {
                if (rentedAlphas is not null)
                    ArrayPool<GradientAlphaKey>.Shared.Return(rentedAlphas);
                if (rentedColors is not null)
                    ArrayPool<GradientColorKey>.Shared.Return(rentedColors);
            }
        }

        /// <summary>Stages validated curve keys without allocating an intermediate managed array.</summary>
        /// <param name="cursor">The local cursor after the validated curve header.</param>
        /// <param name="value">The existing native owner, or null for a fresh result.</param>
        /// <param name="pre">The encoded pre-wrap mode.</param>
        /// <param name="post">The encoded post-wrap mode.</param>
        /// <param name="count">The validated key count.</param>
        /// <returns>The owner after all staged keys and modes have been applied.</returns>
        private static AnimationCurve ReadCurveSnapshot(ref LuminPackReader cursor, AnimationCurve? value, int pre, int post, int count)
        {
            Keyframe[]? rented = null;
            Span<Keyframe> keys = count <= 16 ? stackalloc Keyframe[count] : (rented = ArrayPool<Keyframe>.Shared.Rent(count)).AsSpan(0, count);
            try
            {
                for (int index = 0; index < count; index++)
                    Read(ref cursor, ref keys[index]);
                AnimationCurve result = value ?? new AnimationCurve();
                result.SetKeys(keys);
                result.preWrapMode = (WrapMode)pre;
                result.postWrapMode = (WrapMode)post;
                return result;
            }
            finally
            {
                if (rented is not null)
                    ArrayPool<Keyframe>.Shared.Return(rented);
            }
        }

        /// <summary>Stages both validated gradient arrays before applying the existing combined key setter.</summary>
        /// <param name="cursor">The local cursor after the validated color-key header.</param>
        /// <param name="value">The existing native owner, or null for a fresh result.</param>
        /// <param name="mode">The encoded interpolation mode.</param>
        /// <param name="colorSpace">The encoded color space.</param>
        /// <param name="colorCount">The validated color-key count.</param>
        /// <param name="alphaCount">The validated alpha-key count.</param>
        /// <returns>The owner after both key arrays and metadata have been applied.</returns>
        private static Gradient ReadGradientSnapshot(ref LuminPackReader cursor, Gradient? value, int mode, int colorSpace, int colorCount, int alphaCount)
        {
            GradientColorKey[]? rentedColors = null;
            GradientAlphaKey[]? rentedAlphas = null;
            try
            {
                Span<GradientColorKey> colors = colorCount <= 16 ? stackalloc GradientColorKey[colorCount] : (rentedColors = ArrayPool<GradientColorKey>.Shared.Rent(colorCount)).AsSpan(0, colorCount);
                Span<GradientAlphaKey> alphas = alphaCount <= 16 ? stackalloc GradientAlphaKey[alphaCount] : (rentedAlphas = ArrayPool<GradientAlphaKey>.Shared.Rent(alphaCount)).AsSpan(0, alphaCount);
                for (int index = 0; index < colorCount; index++)
                    Read(ref cursor, ref colors[index]);
                if (cursor.Read<int>() != alphaCount)
                    LuminPackExceptionHelper.ThrowInvalidOperationException("Gradient input changed during deserialization.");
                for (int index = 0; index < alphaCount; index++)
                    Read(ref cursor, ref alphas[index]);
                Gradient result = value ?? new Gradient();
                result.SetKeys(colors, alphas);
                result.mode = (GradientMode)mode;
                result.colorSpace = (ColorSpace)colorSpace;
                return result;
            }
            finally
            {
                if (rentedAlphas is not null)
                    ArrayPool<GradientAlphaKey>.Shared.Return(rentedAlphas);
                if (rentedColors is not null)
                    ArrayPool<GradientColorKey>.Shared.Return(rentedColors);
            }
        }
#endif
    }
}
