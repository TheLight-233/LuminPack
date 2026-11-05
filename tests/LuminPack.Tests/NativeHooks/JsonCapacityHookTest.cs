using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;

/// <summary>Checks optional sizing slots against the actual portable asset using scalar ABI probes.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class JsonCapacityHookTest : IDisposable
{
    private static int _calls, _result;
    private static bool _collect;
    /// <summary>Starts from an unpublished table.</summary>
    public JsonCapacityHookTest() { LuminPackBurst.Clear(); _calls = 0; _collect = false; }
    /// <summary>Clears the registration after the test.</summary>
    public void Dispose() => LuminPackBurst.Clear();

    /// <summary>Checks old tables, snapshots, invalid results, complete pins and short-window bypass.</summary>
    /// <param name="utf16">Whether the input consists of UTF-16 code units.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HooksKeepFallbackAndPinnedReadOnlyWindows(bool utf16)
    {
        byte[] bytes = (utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes("[" + string.Join(",", Enumerable.Repeat("1", 129)) + "]");
        byte[] original = (byte[])bytes.Clone();
        foreach (uint version in new uint[] { 2, 3, LuminPackBurst.AbiVersion })
        {
            _result = 7; _calls = 0;
            var table = new LuminPackBurst.KernelTable { Version = version, JsonArrayHintUtf8 = &Probe8, JsonArrayHintUtf16 = &Probe16 };
            LuminPackBurst.Register(table);
            table.JsonArrayHintUtf8 = null; table.JsonArrayHintUtf16 = null;
            Assert.Equal(version < 4 ? 129 : 7, Hint(bytes, utf16));
            Assert.Equal(version < 4 ? 0 : 1, _calls);
        }
        foreach (int invalid in new[] { int.MaxValue, -2 })
        {
            _result = invalid;
            Assert.Equal(129, Hint(bytes, utf16));
        }
        _result = -1;
        Assert.Equal(-1, Hint(bytes, utf16));
        _result = 129; _collect = true;
        Assert.Equal(129, Hint(bytes, utf16));
        _collect = false;
        Assert.Equal(original, bytes);
        int calls = _calls;
        Assert.Equal(1, Hint((utf16 ? Encoding.Unicode : Encoding.UTF8).GetBytes("[1]"), utf16));
        Assert.Equal(calls, _calls);
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion });
        Assert.Equal(129, Hint(bytes, utf16));
        LuminPackBurst.Clear();
        Assert.Equal(129, Hint(bytes, utf16));
    }
    /// <summary>Runs a pinned structural hint.</summary><param name="bytes">Input.</param><param name="utf16">Encoding.</param><returns>The result.</returns>
    private static int Hint(byte[] bytes, bool utf16)
    {
        fixed (byte* pin = bytes)
        {
            ReadOnlySpan<byte> input = bytes;
            var reader = new LuminPackJsonReader(ref input, null);
            if (utf16) { reader.Read<LuminPackUtf16LengthOptions>(); return reader.GetArrayLengthHint<LuminPackUtf16LengthOptions>(); }
            reader.Read<LuminPackUtf8LengthOptions>(); return reader.GetArrayLengthHint<LuminPackUtf8LengthOptions>();
        }
    }
    /// <summary>Probes UTF-8 dispatch while optionally collecting pinned owners.</summary>
    /// <param name="source">Input.</param><param name="length">Bytes.</param><param name="flags">Mode.</param><returns>The selected test result.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Probe8(byte* source, int length, uint flags)
    {
        _calls++;
        if (_collect) GC.Collect(2, GCCollectionMode.Forced, true, true);
        return length > 0 && source[0] == '1' && source[length - 1] == ']' && flags == 0 ? _result : -2;
    }
    /// <summary>Probes UTF-16 dispatch while optionally collecting pinned owners.</summary>
    /// <param name="source">Input.</param><param name="length">Code units.</param><param name="flags">Mode.</param><returns>The selected test result.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Probe16(char* source, int length, uint flags)
    {
        _calls++;
        if (_collect) GC.Collect(2, GCCollectionMode.Forced, true, true);
        return length > 0 && source[0] == '1' && source[length - 1] == ']' && flags == 0 ? _result : -2;
    }
}
