using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack.Core;
using LuminPack.Utility;
using Xunit;
namespace LuminPackUnitTest;
/// <summary>Checks compact-record hook integration against the actual netstandard runtime.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed unsafe class Latin1BurstHookTest : IDisposable
{
    private static int _checked, _narrowed, _widened, _compared;
    /// <summary>Starts without global native callbacks.</summary>
    public Latin1BurstHookTest() { LuminPackBurst.Clear(); _checked = _narrowed = _widened = _compared = 0; }
    /// <summary>Releases callback publication.</summary>
    public void Dispose() => LuminPackBurst.Clear();
    /// <summary>Checks snapshot ownership, actual record call sites, fallback and existing string identity.</summary>
    [Fact]
    public void RecordsUsePinnedCompactKernelsAndKeepFallback()
    {
        var table = new LuminPackBurst.KernelTable { Version = LuminPackBurst.AbiVersion, IsLatin1 = &Check, NarrowLatin1 = &Narrow, WidenLatin1 = &Widen, Latin1Equal = &Equal };
        LuminPackBurst.Register(table);
        table.NarrowLatin1 = null;
        foreach (string text in new[] { "", "a", "abcdefg", "abcdefghijk", new string('\u00E9', 129), "prefix\u4E2Dtail", "x\uD800y" })
        {
            Assert.Equal(text.AsSpan().IndexOfAnyInRange('\u0100', '\uFFFF') < 0, StringSerializer.IsLatin1(text));
            byte[] expected = new byte[text.Length * 2 + 8], actual = new byte[expected.Length];
            LuminPackBurst.Clear();
            int header = StringSerializer.WriteUtf16RecordPayload(ref expected[3], text);
            LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, IsLatin1 = &Check, NarrowLatin1 = &Narrow, WidenLatin1 = &Widen, Latin1Equal = &Equal });
            Assert.Equal(header, StringSerializer.WriteUtf16RecordPayload(ref actual[3], text));
            int payload = (header & 1) != 0 ? header >> 1 : header;
            Assert.Equal(expected.AsSpan(3, payload).ToArray(), actual.AsSpan(3, payload).ToArray());
            if ((header & 1) != 0 && text.Length != 0)
            {
                Assert.Equal(text, StringSerializer.CreateLatin1String(ref actual[3], text.Length));
                Assert.Same(text, StringSerializer.ReadLatin1PayloadCore(ref actual[3], text.Length, text));
                for (int index = 0; index < text.Length; index++)
                {
                    actual[3 + index] ^= 1;
                    Assert.NotSame(text, StringSerializer.ReadLatin1PayloadCore(ref actual[3], text.Length, text));
                    actual[3 + index] ^= 1;
                }
            }
        }
        Assert.True(_checked > 0 && _narrowed > 0 && _widened > 0 && _compared > 0);
        LuminPackBurst.Clear();
        Assert.False(LuminPackBurst.TryIsLatin1("fallback", out _));
    }
    /// <summary>Checks version-two tables ignore appended slots and retain their original semantics.</summary>
    [Fact]
    public void VersionTwoKeepsItsOriginalSlots()
    {
        LuminPackBurst.Register(new() { Version = 2, IsLatin1 = &Check, NarrowLatin1 = &Narrow, WidenLatin1 = &Widen, Latin1Equal = &Equal });
        Assert.False(LuminPackBurst.TryIsLatin1("a", out _));
        Assert.Equal(0, _checked);
        Assert.True(StringSerializer.IsLatin1("fallback"));
        Assert.Equal(0, _checked);
    }
    /// <summary>Checks complete source/destination owners stay pinned through collection.</summary>
    [Fact]
    public void NativeCallbacksCanCollectWithoutMovingTheirOwners()
    {
        LuminPackBurst.Register(new() { Version = LuminPackBurst.AbiVersion, NarrowLatin1 = &CollectAndNarrow });
        string text = new string('\u00FF', 1025);
        byte[] bytes = new byte[text.Length + 8];
        Assert.True(StringSerializer.TryNarrowLatin1(ref MemoryMarshal.GetReference(text.AsSpan()), ref bytes[3], text.Length));
        Assert.All(bytes.AsSpan(3, text.Length).ToArray(), b => Assert.Equal((byte)255, b));
        Assert.Equal(0, bytes[2]); Assert.Equal(0, bytes[text.Length + 3]);
    }
    /// <summary>Checks a complete code-unit range.</summary><param name="source">Pinned input.</param><param name="length">Characters.</param><returns>Whether representable.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Check(char* source, int length) { _checked++; for (int i = 0; i < length; i++) if (source[i] > 255) return 0; return 1; }
    /// <summary>Narrows until the first invalid code unit.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param><returns>Whether complete.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Narrow(char* source, byte* destination, int length) { _narrowed++; for (int i = 0; i < length; i++) { if (source[i] > 255) return 0; destination[i] = (byte)source[i]; } return 1; }
    /// <summary>Collects while the caller's ranges are pinned.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param><returns>Whether complete.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int CollectAndNarrow(char* source, byte* destination, int length) { GC.Collect(2, GCCollectionMode.Forced, true, true); for (int i = 0; i < length; i++) destination[i] = (byte)source[i]; return 1; }
    /// <summary>Widens the complete payload.</summary><param name="source">Input.</param><param name="destination">Output.</param><param name="length">Characters.</param>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void Widen(byte* source, char* destination, int length) { _widened++; for (int i = 0; i < length; i++) destination[i] = (char)source[i]; }
    /// <summary>Compares complete ranges.</summary><param name="source">Payload.</param><param name="candidate">Candidate.</param><param name="length">Characters.</param><returns>Whether equal.</returns>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static int Equal(byte* source, char* candidate, int length) { _compared++; for (int i = 0; i < length; i++) if (source[i] != candidate[i]) return 0; return 1; }
}

