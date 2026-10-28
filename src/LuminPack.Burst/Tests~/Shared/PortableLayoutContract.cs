using System;
using System.Runtime.InteropServices;
using LuminPack.Utility;

namespace LuminPack.Burst.Tests
{
/// <summary>Exercises the actual portable Core entry points and their original wide-plan eligibility.</summary>
public static class PortableLayoutContract
{
    /// <summary>Checks published pack/unpack hooks against independent field-order bytes with bounded tails.</summary>
    /// <param name="requireNative">Requires eligible large batches to enter a registered shuffle backend.</param>
    /// <returns>The checked direction and geometry count.</returns>
    public static int Verify(bool requireNative)
    {
        int checks = 0;
        foreach (int count in new[] { 0, 1, 31, 32, 511, 512, 513, 1000 })
        {
            checks += Check<Small, SmallLayout>(count, requireNative, true);
            checks += Check<Wide, WideLayout>(count, requireNative, true);
            checks += Check<Wide, ThreeRunLayout>(count, requireNative, false);
        }
        return checks;
    }

    /// <summary>Compares complete field bytes and guards while finishing only the refused suffix.</summary>
    /// <typeparam name="T">Storage shape.</typeparam><typeparam name="TLayout">Actual byte description.</typeparam>
    /// <param name="count">Complete elements.</param><param name="requireNative">Requires an eligible native prefix.</param><param name="eligible">Original portable planning eligibility.</param><returns>Two checked directions.</returns>
    private static int Check<T, TLayout>(int count, bool requireNative, bool eligible)
        where T : struct where TLayout : struct, ILuminPortableLayout
    {
        var input = new T[count + 2];
        var output = new T[count + 2];
        Span<byte> source = MemoryMarshal.AsBytes(input.AsSpan()), target = MemoryMarshal.AsBytes(output.AsSpan());
        int size = source.Length / input.Length, wire = default(TLayout).WireSize;
        for (int i = 0; i < source.Length; i++) source[i] = (byte)(i * 37 + 11);
        target.Fill(0xA5);
        byte[] packed = new byte[count * wire + 6];
        packed.AsSpan().Fill(0xA5);
        int[] offsets = new int[wire];
        default(TLayout).Describe(offsets);
        int done = LuminPortable<T, TLayout>.Pack(ref input[1], count, ref packed[3]);
        CheckPrefix(done, count, requireNative, eligible);
        for (int element = done; element < count; element++)
            for (int i = 0; i < wire; i++) packed[3 + element * wire + i] = source[(element + 1) * size + offsets[i]];
        for (int element = 0; element < count; element++)
            for (int i = 0; i < wire; i++) Require(packed[3 + element * wire + i] == source[(element + 1) * size + offsets[i]], "Portable packed field differs.");
        for (int i = 0; i < 3; i++) Require(packed[i] == 0xA5 && packed[packed.Length - 1 - i] == 0xA5, "Portable pack canary changed.");
        done = LuminPortable<T, TLayout>.Unpack(ref packed[3], count, ref output[1]);
        CheckPrefix(done, count, requireNative, eligible);
        for (int element = done; element < count; element++)
            for (int i = 0; i < wire; i++) target[(element + 1) * size + offsets[i]] = packed[3 + element * wire + i];
        for (int element = 0; element < count; element++)
            for (int i = 0; i < wire; i++) Require(target[(element + 1) * size + offsets[i]] == source[(element + 1) * size + offsets[i]], "Portable unpacked field differs.");
        for (int i = 0; i < size; i++) Require(target[i] == 0xA5 && target[target.Length - 1 - i] == 0xA5, "Portable unpack canary changed.");
        return 2;
    }

    /// <summary>Checks both native completion bounds and the original portable policy.</summary>
    /// <param name="done">Completed prefix.</param><param name="count">Complete range.</param><param name="requireNative">Requires native progress.</param><param name="eligible">Original eligibility.</param>
    private static void CheckPrefix(int done, int count, bool requireNative, bool eligible)
    {
        Require(done >= 0 && done <= count, "Portable completion is outside the range.");
        if (!eligible || !global::LuminPack.Code.LuminKernelPolicy.UseNativeLayout(count)) Require(done == 0, "Portable refusal policy changed.");
        else if (requireNative) Require(done > 0, "Eligible portable batch did not enter native SIMD.");
    }
    /// <summary>Reports a differential contract failure.</summary><param name="condition">Required condition.</param><param name="message">Failure detail.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(message);
    }
    /// <summary>Represents padded small storage.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)] private struct Small { }
    /// <summary>Represents padded two-vector storage.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 24)] private struct Wide { }
    /// <summary>Describes a five-byte portable element.</summary>
    private struct SmallLayout : ILuminPortableLayout
    {
        /// <summary>Gets the packed width.</summary>
        public int WireSize => 5;
        /// <summary>Supplies exact storage offsets.</summary><param name="storageOffsets">Complete destination.</param>
        public void Describe(Span<int> storageOffsets) { storageOffsets[0] = 0; for (int i = 1; i < 5; i++) storageOffsets[i] = i + 3; }
    }
    /// <summary>Describes a seventeen-byte element spanning five storage runs.</summary>
    private struct WideLayout : ILuminPortableLayout
    {
        /// <summary>Gets the packed width.</summary>
        public int WireSize => 17;
        /// <summary>Supplies exact storage offsets.</summary><param name="storageOffsets">Complete destination.</param>
        public void Describe(Span<int> storageOffsets)
        {
            new int[] { 0, 1, 4, 5, 6, 7, 8, 9, 12, 13, 16, 17, 18, 19, 20, 21, 22 }.AsSpan().CopyTo(storageOffsets);
        }
    }
    /// <summary>Describes an intentionally ineligible wide element with only three runs.</summary>
    private struct ThreeRunLayout : ILuminPortableLayout
    {
        /// <summary>Gets the packed width.</summary>
        public int WireSize => 12;
        /// <summary>Supplies exact storage offsets.</summary><param name="storageOffsets">Complete destination.</param>
        public void Describe(Span<int> storageOffsets) { for (int i = 0; i < 12; i++) storageOffsets[i] = i / 4 * 8 + i % 4; }
    }
}
}
