using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using UnityEngine;

namespace LuminPack.Unity
{
    /// <summary>Provides explicit field-ordered binary codecs for common Unity value types.</summary>
    /// <remarks>These opt-in codecs do not serialize native object handles or silently replace generated nested contracts.
    /// Fixed batches access only individual scalar fields of at most four bytes; they never assume adjacent managed field layout.</remarks>
    public static partial class LuminUnity
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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Vector2 result = default;
            result.x = Unsafe.ReadUnaligned<float>(ref payload);
            result.y = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            reader.Advance(8);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Vector3 result = default;
            result.x = Unsafe.ReadUnaligned<float>(ref payload);
            result.y = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.z = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            reader.Advance(12);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Vector4 result = default;
            result.x = Unsafe.ReadUnaligned<float>(ref payload);
            result.y = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.z = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.w = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            reader.Advance(16);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Vector2Int result = default;
            result.x = Unsafe.ReadUnaligned<int>(ref payload);
            result.y = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 4));
            reader.Advance(8);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Vector3Int result = default;
            result.x = Unsafe.ReadUnaligned<int>(ref payload);
            result.y = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 4));
            result.z = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 8));
            reader.Advance(12);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Quaternion result = default;
            result.x = Unsafe.ReadUnaligned<float>(ref payload);
            result.y = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.z = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.w = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            reader.Advance(16);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Color result = default;
            result.r = Unsafe.ReadUnaligned<float>(ref payload);
            result.g = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.b = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.a = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            reader.Advance(16);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Color32 result = default;
            result.r = Unsafe.ReadUnaligned<byte>(ref payload);
            result.g = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref payload, 1));
            result.b = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref payload, 2));
            result.a = Unsafe.ReadUnaligned<byte>(ref Unsafe.Add(ref payload, 3));
            reader.Advance(4);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Rect result = default;
            result.x = Unsafe.ReadUnaligned<float>(ref payload);
            result.y = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.width = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.height = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            reader.Advance(16);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            RectInt result = default;
            result.x = Unsafe.ReadUnaligned<int>(ref payload);
            result.y = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 4));
            result.width = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 8));
            result.height = Unsafe.ReadUnaligned<int>(ref Unsafe.Add(ref payload, 12));
            reader.Advance(16);
            value = result;
        }

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

        /// <summary>Writes the 64-byte field-ordered Matrix4x4 value.</summary>
        /// <param name="writer">The existing output cursor.</param>
        /// <param name="value">The value to encode.</param>
        public static void Write(ref LuminPackWriter writer, in Matrix4x4 value)
        {
            writer.EnsureAdditionalCapacity(64);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(writer.Buffer), writer.Position);
            Unsafe.WriteUnaligned<float>(ref payload, value.m00);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 4), value.m10);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 8), value.m20);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 12), value.m30);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 16), value.m01);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 20), value.m11);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 24), value.m21);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 28), value.m31);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 32), value.m02);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 36), value.m12);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 40), value.m22);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 44), value.m32);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 48), value.m03);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 52), value.m13);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 56), value.m23);
            Unsafe.WriteUnaligned<float>(ref Unsafe.Add(ref payload, 60), value.m33);
            writer.Advance(64);
        }

        /// <summary>Reads a complete Matrix4x4 before assigning the destination.</summary>
        /// <param name="reader">The existing bounded input cursor.</param>
        /// <param name="value">The destination assigned on complete success.</param>
        public static void Read(ref LuminPackReader reader, ref Matrix4x4 value)
        {
            reader.EnsureReadable(reader.Position, 64);
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            Matrix4x4 result = default;
            result.m00 = Unsafe.ReadUnaligned<float>(ref payload);
            result.m10 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 4));
            result.m20 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 8));
            result.m30 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 12));
            result.m01 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 16));
            result.m11 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 20));
            result.m21 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 24));
            result.m31 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 28));
            result.m02 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 32));
            result.m12 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 36));
            result.m22 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 40));
            result.m32 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 44));
            result.m03 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 48));
            result.m13 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 52));
            result.m23 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 56));
            result.m33 = Unsafe.ReadUnaligned<float>(ref Unsafe.Add(ref payload, 60));
            reader.Advance(64);
            value = result;
        }

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
            ref byte payload = ref Unsafe.Add(ref MemoryMarshal.GetReference(reader.Buffer), reader.Position);
            LayerMask result = default;
            result.value = Unsafe.ReadUnaligned<int>(ref payload);
            reader.Advance(4);
            value = result;
        }
    }
}
