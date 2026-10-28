using System;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using UnityEngine;

namespace LuminPack.Unity
{
    /// <summary>Provides explicit field-ordered binary codecs for common Unity value types.</summary>
    /// <remarks>These opt-in codecs do not serialize native object handles or silently replace generated nested contracts.</remarks>
    public static unsafe partial class LuminUnity
    {
        /// <summary>Writes the 8-byte field-ordered Vector2 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Vector2 value)
        {
            writer.EnsureAdditionalCapacity(8);
            writer.Write(value.x);
            writer.Write(value.y);
        }

        /// <summary>Reads a complete Vector2 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Vector2 value)
        {
            reader.EnsureReadable(reader.Position, 8);
            Vector2 result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            value = result;
        }

        /// <summary>Adds the exact 8-byte binary size for Vector2.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Vector2 value) => evaluator.Add(8);

        /// <summary>Writes the 12-byte field-ordered Vector3 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Vector3 value)
        {
            writer.EnsureAdditionalCapacity(12);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        /// <summary>Reads a complete Vector3 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Vector3 value)
        {
            reader.EnsureReadable(reader.Position, 12);
            Vector3 result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.z = field2;
            value = result;
        }

        /// <summary>Adds the exact 12-byte binary size for Vector3.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Vector3 value) => evaluator.Add(12);

        /// <summary>Writes the 16-byte field-ordered Vector4 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Vector4 value)
        {
            writer.EnsureAdditionalCapacity(16);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        /// <summary>Reads a complete Vector4 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Vector4 value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Vector4 result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.z = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.w = field3;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Vector4.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Vector4 value) => evaluator.Add(16);

        /// <summary>Writes the 8-byte field-ordered Vector2Int value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Vector2Int value)
        {
            writer.EnsureAdditionalCapacity(8);
            writer.Write(value.x);
            writer.Write(value.y);
        }

        /// <summary>Reads a complete Vector2Int before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Vector2Int value)
        {
            reader.EnsureReadable(reader.Position, 8);
            Vector2Int result = default;
            int field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            int field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            value = result;
        }

        /// <summary>Adds the exact 8-byte binary size for Vector2Int.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Vector2Int value) => evaluator.Add(8);

        /// <summary>Writes the 12-byte field-ordered Vector3Int value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Vector3Int value)
        {
            writer.EnsureAdditionalCapacity(12);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        /// <summary>Reads a complete Vector3Int before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Vector3Int value)
        {
            reader.EnsureReadable(reader.Position, 12);
            Vector3Int result = default;
            int field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            int field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            int field2 = default;
            reader.Read(ref field2);
            result.z = field2;
            value = result;
        }

        /// <summary>Adds the exact 12-byte binary size for Vector3Int.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Vector3Int value) => evaluator.Add(12);

        /// <summary>Writes the 16-byte field-ordered Quaternion value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Quaternion value)
        {
            writer.EnsureAdditionalCapacity(16);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        /// <summary>Reads a complete Quaternion before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Quaternion value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Quaternion result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.z = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.w = field3;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Quaternion.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Quaternion value) => evaluator.Add(16);

        /// <summary>Writes the 16-byte field-ordered Color value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Color value)
        {
            writer.EnsureAdditionalCapacity(16);
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        /// <summary>Reads a complete Color before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Color value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Color result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.r = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.g = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.b = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.a = field3;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Color.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Color value) => evaluator.Add(16);

        /// <summary>Writes the 4-byte field-ordered Color32 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Color32 value)
        {
            writer.EnsureAdditionalCapacity(4);
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        /// <summary>Reads a complete Color32 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Color32 value)
        {
            reader.EnsureReadable(reader.Position, 4);
            Color32 result = default;
            byte field0 = default;
            reader.Read(ref field0);
            result.r = field0;
            byte field1 = default;
            reader.Read(ref field1);
            result.g = field1;
            byte field2 = default;
            reader.Read(ref field2);
            result.b = field2;
            byte field3 = default;
            reader.Read(ref field3);
            result.a = field3;
            value = result;
        }

        /// <summary>Adds the exact 4-byte binary size for Color32.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Color32 value) => evaluator.Add(4);

        /// <summary>Writes the 16-byte field-ordered Rect value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Rect value)
        {
            writer.EnsureAdditionalCapacity(16);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.width);
            writer.Write(value.height);
        }

        /// <summary>Reads a complete Rect before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Rect value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Rect result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.width = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.height = field3;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Rect.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Rect value) => evaluator.Add(16);

        /// <summary>Writes the 16-byte field-ordered RectInt value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in RectInt value)
        {
            writer.EnsureAdditionalCapacity(16);
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.width);
            writer.Write(value.height);
        }

        /// <summary>Reads a complete RectInt before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref RectInt value)
        {
            reader.EnsureReadable(reader.Position, 16);
            RectInt result = default;
            int field0 = default;
            reader.Read(ref field0);
            result.x = field0;
            int field1 = default;
            reader.Read(ref field1);
            result.y = field1;
            int field2 = default;
            reader.Read(ref field2);
            result.width = field2;
            int field3 = default;
            reader.Read(ref field3);
            result.height = field3;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for RectInt.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in RectInt value) => evaluator.Add(16);

        /// <summary>Writes the 24-byte field-ordered Bounds value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Bounds value)
        {
            writer.EnsureAdditionalCapacity(24);
            Write(ref writer, value.center);
            Write(ref writer, value.extents);
        }

        /// <summary>Reads a complete Bounds before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Bounds value)
        {
            reader.EnsureReadable(reader.Position, 24);
            Bounds result = default;
            Vector3 field0 = default;
            Read(ref reader, ref field0);
            result.center = field0;
            Vector3 field1 = default;
            Read(ref reader, ref field1);
            result.extents = field1;
            value = result;
        }

        /// <summary>Adds the exact 24-byte binary size for Bounds.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Bounds value) => evaluator.Add(24);

        /// <summary>Writes the 24-byte field-ordered BoundsInt value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in BoundsInt value)
        {
            writer.EnsureAdditionalCapacity(24);
            Write(ref writer, value.position);
            Write(ref writer, value.size);
        }

        /// <summary>Reads a complete BoundsInt before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref BoundsInt value)
        {
            reader.EnsureReadable(reader.Position, 24);
            BoundsInt result = default;
            Vector3Int field0 = default;
            Read(ref reader, ref field0);
            result.position = field0;
            Vector3Int field1 = default;
            Read(ref reader, ref field1);
            result.size = field1;
            value = result;
        }

        /// <summary>Adds the exact 24-byte binary size for BoundsInt.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in BoundsInt value) => evaluator.Add(24);

        /// <summary>Writes the 24-byte field-ordered Ray value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Ray value)
        {
            writer.EnsureAdditionalCapacity(24);
            Write(ref writer, value.origin);
            Write(ref writer, value.direction);
        }

        /// <summary>Reads a complete Ray before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Ray value)
        {
            reader.EnsureReadable(reader.Position, 24);
            Ray result = default;
            Vector3 field0 = default;
            Read(ref reader, ref field0);
            result.origin = field0;
            Vector3 field1 = default;
            Read(ref reader, ref field1);
            // Direction setters normalize; reconstruct the documented pair of vectors without changing serialized bits.
            var storage = new RayStorage { Origin = field0, Direction = field1 };
            if (MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref result, 1)).Length != 24)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Unsupported Unity Ray storage width.");
            result = MemoryMarshal.Cast<RayStorage, Ray>(MemoryMarshal.CreateReadOnlySpan(ref storage, 1))[0];
            value = result;
        }

        /// <summary>Adds the exact 24-byte binary size for Ray.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Ray value) => evaluator.Add(24);

        /// <summary>Writes the 16-byte field-ordered Ray2D value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Ray2D value)
        {
            writer.EnsureAdditionalCapacity(16);
            Write(ref writer, value.origin);
            Write(ref writer, value.direction);
        }

        /// <summary>Reads a complete Ray2D before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Ray2D value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Ray2D result = default;
            Vector2 field0 = default;
            Read(ref reader, ref field0);
            result.origin = field0;
            Vector2 field1 = default;
            Read(ref reader, ref field1);
            // Direction setters normalize; reconstruct the documented pair of vectors without changing serialized bits.
            var storage = new Ray2DStorage { Origin = field0, Direction = field1 };
            if (MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref result, 1)).Length != 16)
                LuminPackExceptionHelper.ThrowInvalidOperationException("Unsupported Unity Ray2D storage width.");
            result = MemoryMarshal.Cast<Ray2DStorage, Ray2D>(MemoryMarshal.CreateReadOnlySpan(ref storage, 1))[0];
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Ray2D.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Ray2D value) => evaluator.Add(16);

        /// <summary>Writes the 16-byte field-ordered Plane value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Plane value)
        {
            writer.EnsureAdditionalCapacity(16);
            Write(ref writer, value.normal);
            writer.Write(value.distance);
        }

        /// <summary>Reads a complete Plane before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Plane value)
        {
            reader.EnsureReadable(reader.Position, 16);
            Plane result = default;
            Vector3 field0 = default;
            Read(ref reader, ref field0);
            result.normal = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.distance = field1;
            value = result;
        }

        /// <summary>Adds the exact 16-byte binary size for Plane.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Plane value) => evaluator.Add(16);

        /// <summary>Writes the 64-byte field-ordered Matrix4x4 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Matrix4x4 value)
        {
            writer.EnsureAdditionalCapacity(64);
            writer.Write(value.m00);
            writer.Write(value.m10);
            writer.Write(value.m20);
            writer.Write(value.m30);
            writer.Write(value.m01);
            writer.Write(value.m11);
            writer.Write(value.m21);
            writer.Write(value.m31);
            writer.Write(value.m02);
            writer.Write(value.m12);
            writer.Write(value.m22);
            writer.Write(value.m32);
            writer.Write(value.m03);
            writer.Write(value.m13);
            writer.Write(value.m23);
            writer.Write(value.m33);
        }

        /// <summary>Reads a complete Matrix4x4 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Matrix4x4 value)
        {
            reader.EnsureReadable(reader.Position, 64);
            Matrix4x4 result = default;
            float field0 = default;
            reader.Read(ref field0);
            result.m00 = field0;
            float field1 = default;
            reader.Read(ref field1);
            result.m10 = field1;
            float field2 = default;
            reader.Read(ref field2);
            result.m20 = field2;
            float field3 = default;
            reader.Read(ref field3);
            result.m30 = field3;
            float field4 = default;
            reader.Read(ref field4);
            result.m01 = field4;
            float field5 = default;
            reader.Read(ref field5);
            result.m11 = field5;
            float field6 = default;
            reader.Read(ref field6);
            result.m21 = field6;
            float field7 = default;
            reader.Read(ref field7);
            result.m31 = field7;
            float field8 = default;
            reader.Read(ref field8);
            result.m02 = field8;
            float field9 = default;
            reader.Read(ref field9);
            result.m12 = field9;
            float field10 = default;
            reader.Read(ref field10);
            result.m22 = field10;
            float field11 = default;
            reader.Read(ref field11);
            result.m32 = field11;
            float field12 = default;
            reader.Read(ref field12);
            result.m03 = field12;
            float field13 = default;
            reader.Read(ref field13);
            result.m13 = field13;
            float field14 = default;
            reader.Read(ref field14);
            result.m23 = field14;
            float field15 = default;
            reader.Read(ref field15);
            result.m33 = field15;
            value = result;
        }

        /// <summary>Adds the exact 64-byte binary size for Matrix4x4.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in Matrix4x4 value) => evaluator.Add(64);

        /// <summary>Writes the 4-byte field-ordered LayerMask value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in LayerMask value)
        {
            writer.EnsureAdditionalCapacity(4);
            writer.Write(value.value);
        }

        /// <summary>Reads a complete LayerMask before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref LayerMask value)
        {
            reader.EnsureReadable(reader.Position, 4);
            LayerMask result = default;
            int field0 = default;
            reader.Read(ref field0);
            result.value = field0;
            value = result;
        }

        /// <summary>Adds the exact 4-byte binary size for LayerMask.</summary>
        /// <param name="evaluator">The caller-owned size accumulator.</param>
        /// <param name="value">The value whose fixed size is added.</param>
        public static void Size(ref LuminPackEvaluator evaluator, in LayerMask value) => evaluator.Add(4);

    }
}
