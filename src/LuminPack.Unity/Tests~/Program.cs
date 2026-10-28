#if !UNITY_5_3_OR_NEWER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Unity;
using UnityEngine;

unsafe
{
    LuminUnity.Register();
    Check<Vector2>(new Vector2(1.25f, -2.5f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Vector3>(new Vector3(1.25f, -2.5f, 3.75f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Vector4>(new Vector4(1.25f, -2.5f, 3.75f, -0f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Vector2Int>(new Vector2Int(int.MinValue, int.MaxValue), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Vector3Int>(new Vector3Int(-1, 0, 1234567), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Quaternion>(new Quaternion(0.125f, -0.25f, 0.375f, 0.875f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Color>(new Color(0.125f, -1f, 2f, 0.75f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Color32>(new Color32(0, 1, 127, 255), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Rect>(new Rect(-1.25f, 2.5f, -3.75f, 4.125f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<RectInt>(new RectInt(-1, 2, -3, 4), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Bounds>(new Bounds(new Vector3(1,2,3), new Vector3(5,7,9)), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<BoundsInt>(new BoundsInt(new Vector3Int(1,2,3), new Vector3Int(-5,7,9)), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Ray>(new Ray(new Vector3(1,2,3), new Vector3(0.125f,0.375f,0.875f)), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Ray2D>(new Ray2D(new Vector2(1,2), new Vector2(0.125f,0.875f)), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Plane>(new Plane(new Vector3(0.125f,0.375f,0.875f), -1.25f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<Matrix4x4>(new Matrix4x4(new Vector4(1,2,3,4),new Vector4(5,6,7,8),new Vector4(9,10,11,12),new Vector4(13,14,15,16)), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<LayerMask>((LayerMask)(-2147483647), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    LuminUnity.Register();
    LuminUnity.Register();
    Check<Keyframe>(new Keyframe(0.25f, -2f, 3f, -4f, 0.125f, 0.875f) { weightedMode = WeightedMode.Both }, &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<GradientColorKey>(new GradientColorKey(new Color(1, 0.5f, 0.125f, 1), 0.375f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<GradientAlphaKey>(new GradientAlphaKey(0.125f, 0.75f), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Check<RangeInt>(new RangeInt(-3, 17), &LuminUnity.Write, &LuminUnity.Read, &LuminUnity.Size);
    Console.WriteLine("Passed 21 Unity value codecs, exact sizes, short-input rejection, registration, and canaries.");
}
/// <summary>Checks a complete external codec without running the source generator or rebuilding Core.</summary>
/// <typeparam name="T">The Unity value.</typeparam>
/// <param name="value">The test value.</param><param name="write">Binary writer.</param><param name="read">Binary reader.</param><param name="size">Exact evaluator.</param>
static unsafe void Check<T>(T value, delegate*<ref LuminPackWriter, in T, void> write,
    delegate*<ref LuminPackReader, ref T, void> read, delegate*<ref LuminPackEvaluator, in T, void> size) where T : unmanaged
{
    nint key = LuminPackMarshal.GetMethodTable(typeof(T));
    if (!LuminPackFormatterRegistry.TryGetValue(in key, out var entry) ||
        entry.WriteValue != (nint)write || entry.ReadValue != (nint)read || entry.CalculateOffset != (nint)size ||
        entry.WriteValueJson != 0 || entry.ReadValueJson != 0)
        LuminPackExceptionHelper.ThrowInvalidOperationException(typeof(T).Name + ": incorrect binary/size registration.");
    int length = 0;
    var evaluator = new LuminPackEvaluator(ref length);
    size(ref evaluator, value);
    byte[] bytes = Enumerable.Repeat((byte)0xA5, length + 16).ToArray();
    fixed (byte* data = bytes)
    {
        Span<byte> output = new(data + 3, length);
        var writer = new LuminPackWriter(ref output);
        write(ref writer, value);
        if (writer.Position != length) LuminPackExceptionHelper.ThrowInvalidOperationException(typeof(T).Name + ": size mismatch.");
        for (int prefix = 0; prefix < length; prefix++)
        {
            ReadOnlySpan<byte> shortened = new(data + 3, prefix);
            var reader = new LuminPackReader(ref shortened);
            T sentinel = value;
            byte[] before = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref sentinel, 1)).ToArray();
            bool rejected = false;
            try { read(ref reader, ref sentinel); } catch { rejected = true; }
            if (!rejected || reader.Position != 0 || !before.AsSpan().SequenceEqual(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref sentinel, 1))))
                LuminPackExceptionHelper.ThrowInvalidOperationException(typeof(T).Name + ": truncated input mutated state.");
        }
        ReadOnlySpan<byte> input = new(data + 3, length);
        var complete = new LuminPackReader(ref input);
        T restored = default;
        read(ref complete, ref restored);
        byte[] again = new byte[length];
        fixed (byte* repeat = again)
        {
            Span<byte> secondOutput = new(repeat, length);
            var secondWriter = new LuminPackWriter(ref secondOutput);
            write(ref secondWriter, restored);
        }
        if (!bytes.AsSpan(3, length).SequenceEqual(again)) LuminPackExceptionHelper.ThrowInvalidOperationException(typeof(T).Name + ": unstable field round trip.");
    }
    if (bytes.Take(3).Concat(bytes.Skip(3 + length)).Any(b => b != 0xA5)) LuminPackExceptionHelper.ThrowInvalidOperationException(typeof(T).Name + ": output canary changed.");
}
#endif
