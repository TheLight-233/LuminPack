using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;
/// <summary>Checks immutable cached shuffle descriptors and the planned native dispatch boundary.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class LayoutPlannedHookTest : IDisposable
{
    private static int _calls;
    private static int _result;
    private static ulong _mask;
    private static LuminLayoutPlan _portablePlan;
    /// <summary>Starts without a published table.</summary>
    public LayoutPlannedHookTest() { LuminPackBurst.Clear(); _calls = 0; _result = 0; }
    /// <summary>Releases native publication.</summary>
    public void Dispose() => LuminPackBurst.Clear();
    /// <summary>Checks descriptor immutability, same plan across calls, pinning and the original byte-run tail on refusal.</summary>
    [Fact]
    public void ConverterCachesItsOwnPlanAndPreservesFallback()
    {
        int[] from = { 0, 1 }, to = { 1, 0 };
        var converter = new LuminLayout(from, 2, to, 3);
        from[0] = 1; to[0] = 2;
        byte[] input = new byte[1024], output = new byte[1536];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)i;
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, LayoutPlanned = &Inspect });
        for (int run = 0; run < 3; run++)
        {
            Array.Fill(output, (byte)0xA5);
            converter.Convert(ref input[0], ref output[0], 512);
            for (int i = 0; i < 512; i++)
            {
                Assert.Equal(input[i * 2], output[i * 3 + 1]);
                Assert.Equal(input[i * 2 + 1], output[i * 3]);
                Assert.Equal(0xA5, output[i * 3 + 2]);
            }
        }
        Assert.Equal(3, _calls);
        Assert.NotEqual(0UL, _mask);
    }
    /// <summary>Rejects out-of-range native completion without altering the established prefix rules.</summary>
    [Fact]
    public void PlannedResultMustStayWithinCompleteElements()
    {
        var converter = new LuminLayout(new[] { 0, 1 }, 2, new[] { 1, 0 }, 3);
        byte[] input = new byte[1024], output = new byte[1536];
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, LayoutPlanned = &Inspect });
        _result = 513;
        Assert.Throws<InvalidOperationException>(() => converter.Convert(ref input[0], ref output[0], 512));
    }
    /// <summary>Checks the separate portable entry point uses cached direction-specific plans and declines small native batches.</summary>
    [Fact]
    public void PortablePackAndUnpackPublishTheirOriginalPlans()
    {
        var values = new PortableValue[512];
        byte[] wire = new byte[512 * 5];
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, LayoutPlanned = &InspectPortable });
        Assert.Equal(0, LuminPortable<PortableValue, PortableDescription>.Pack(ref values[0], 32, ref wire[0]));
        Assert.Equal(0, _calls);
        for (int run = 0; run < 2; run++)
        {
            Assert.Equal(0, LuminPortable<PortableValue, PortableDescription>.Pack(ref values[0], 512, ref wire[0]));
            Assert.Equal(8, _portablePlan.SourceStride);
            Assert.Equal(5, _portablePlan.DestinationStride);
            Assert.Equal(1, _portablePlan.Mode);
            Assert.Equal(2, _portablePlan.PerBlock);
            Assert.Equal(4, _portablePlan.Reserve);
            Assert.Equal(0x0706050400UL, _portablePlan.Mask0Low & 0xFFFFFFFFFFUL);
            Assert.Equal(0, LuminPortable<PortableValue, PortableDescription>.Unpack(ref wire[0], 512, ref values[0]));
            Assert.Equal(5, _portablePlan.SourceStride);
            Assert.Equal(8, _portablePlan.DestinationStride);
            Assert.Equal(0x04030201FFFFFF00UL, _portablePlan.Mask0Low);
        }
        Assert.Equal(4, _calls);
    }
    /// <summary>Captures a pinned plan and refuses the batch before writing.</summary>
    /// <param name="source">Complete input.</param><param name="destination">Complete output.</param><param name="count">Complete elements.</param><param name="plan">Pinned descriptor.</param><returns>Zero to retain the original scalar completion.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int InspectPortable(byte* source, byte* destination, int count, LuminLayoutPlan* plan)
    {
        _calls++;
        _portablePlan = *plan;
        return 0;
    }
    /// <summary>Provides one padded storage element.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private struct PortableValue
    {
        [FieldOffset(0)] public byte Tag;
        [FieldOffset(4)] public int Value;
    }
    /// <summary>Describes actual storage offsets in packed field order.</summary>
    private readonly struct PortableDescription : ILuminPortableLayout
    {
        /// <summary>Gets the packed width.</summary>
        public int WireSize => 5;
        /// <summary>Supplies the explicit storage byte offsets.</summary><param name="storageOffsets">Complete descriptor.</param>
        public void Describe(Span<int> storageOffsets)
        {
            storageOffsets[0] = 0;
            for (int index = 1; index < 5; index++) storageOffsets[index] = index + 3;
        }
    }
    /// <summary>Inspects pinned immutable plan bytes while collecting, then refuses to retain Core's original byte-run tail.</summary>
    /// <param name="source">Complete input.</param><param name="destination">Complete output.</param><param name="count">Complete elements.</param><param name="plan">Pinned descriptor.</param><returns>The configured completion result.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Inspect(byte* source, byte* destination, int count, LuminLayoutPlan* plan)
    {
        _calls++;
        ulong before = plan->Mask0Low;
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        if (before != plan->Mask0Low || plan->Mode != 1 || plan->SourceStride != 2 || plan->DestinationStride != 3) return count + 1;
        if (_calls > 1 && _mask != before) return count + 1;
        _mask = before;
        return _result;
    }
}


