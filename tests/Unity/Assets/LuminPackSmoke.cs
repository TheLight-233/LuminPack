using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using UnityEngine;

[assembly: LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Full)]

/// <summary>Executes generated code in Unity Mono and the actual IL2CPP Player.</summary>
public static class LuminPackSmoke
{
    /// <summary>Runs the same smoke graph in editor tests and the built Player.</summary>
    public static void Verify()
    {
        VerifyLayouts();
        var value = new UnityMessage
        {
            Id = 42,
            Name = "\u00E9cafe\u4E2D\uD83D\uDE00",
            Child = new UnityLeaf { Value = 7 },
            Choice = new UnitySelected { Value = 17 },
            Items = new List<UnityPortable> { new UnityPortable { Tag = 3, Value = 123456789012345 } }
        };
        byte[] bytes = LuminPackSerializer.Serialize(value);
        UnityMessage copy = LuminPackSerializer.Deserialize<UnityMessage>(bytes);
        UnityLeaf child = copy.Child;
        List<UnityPortable> items = copy.Items;
        LuminPackSerializer.Deserialize(bytes, ref copy);
        if (copy.Id != 42 || copy.Name != value.Name || copy.Child.Value != 7 ||
            !ReferenceEquals(child, copy.Child) || !ReferenceEquals(items, copy.Items) ||
            copy.Items[0].Value != value.Items[0].Value || LuminPackSerializer.Serialize(copy.Items).Length != 13 ||
            !(copy.Choice is UnitySelected selected) || selected.Value != 17)
            throw new InvalidOperationException("Unity ordinary reuse or portable storage failed.");
        string json = LuminPackSerializer.SerializeJson(value);
        if (LuminPackSerializer.DeserializeJson<UnityMessage>(json).Name != value.Name)
            throw new InvalidOperationException("Unity JSON read failed.");
        var original = new UnityVersionOne { Id = 11, Name = "name" };
        UnityVersionTwo evolved = LuminPackSerializer.Deserialize<UnityVersionTwo>(LuminPackSerializer.Serialize(original));
        if (evolved.Id != 11 || evolved.DisplayName != "name")
            throw new InvalidOperationException("Unity named version matching failed.");
        var tracked = new UnityTracked();
        tracked.Next = tracked;
        UnityTracked trackedCopy = LuminPackSerializer.Deserialize<UnityTracked>(LuminPackSerializer.Serialize(tracked));
        if (!ReferenceEquals(trackedCopy, trackedCopy.Next))
            throw new InvalidOperationException("Unity reference tracking failed.");
    }

    /// <summary>Reads real 32/64-bit wire models and verifies all three explicit storage policies.</summary>
    private static void VerifyLayouts()
    {
        string text = "\u00E9\u4E2D\uD83D\uDE00";
        byte[] utf8 = LuminPackSerializer.Serialize<string, LuminPackUtf8LengthOptions>(text);
        if (LuminPackSerializer.Deserialize<string, LuminPackUtf8LengthOptions>(utf8) != text)
            throw new InvalidOperationException("The compatibility UTF-8 decoder lost Unicode scalars.");
        bool rejected = false;
        try { LuminPackSerializer.Deserialize<string, LuminPackUtf8LengthOptions>(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 65 }); }
        catch (Exception) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("The compatibility decoder accepted an inconsistent character count.");
        const long expected = 123456789012345;
        var tuples = new[] { (Tag: (byte)7, Value: expected, Tail: 9) };
        var tupleCopy = LuminPackSerializer.Deserialize<(byte Tag, long Value, int Tail)[]>(LuminPackSerializer.Serialize(tuples));
        if (tupleCopy.Length != 1 || tupleCopy[0].Tag != 7 || tupleCopy[0].Value != expected || tupleCopy[0].Tail != 9)
            throw new InvalidOperationException("Automatic ValueTuple layouts did not round trip.");
        for (byte tag = 1; tag <= 2; tag++)
        {
            int width = tag == 1 ? 16 : 12;
            byte[] wire = new byte[5 + width];
            wire[0] = 1;
            wire[4] = tag;
            wire[5] = 7;
            Array.Copy(BitConverter.GetBytes(expected), 0, wire, 5 + (tag == 1 ? 8 : 4), 8);
            var copy = LuminPackSerializer.Deserialize<UnityTagged[]>(wire);
            if (copy.Length != 1 || copy[0].Tag != 7 || copy[0].Value != expected)
                throw new InvalidOperationException("Tagged 32/64-bit input was not converted correctly.");
        }
        var packed = new UnityPortable { Tag = 7, Value = expected };
        if (LuminPackSerializer.Serialize(packed).Length != 9)
            throw new InvalidOperationException("Packed storage contains native padding.");
        byte[] native = LuminPackSerializer.Serialize(new UnityNative { Tag = 7, Value = expected });
        if (native.Length != System.Runtime.CompilerServices.Unsafe.SizeOf<UnityNative>())
            throw new InvalidOperationException("Native storage acquired layout framing.");
        var guid = new UnityPackedGuid { Tag = 7, Id = new Guid("00112233-4455-6677-8899-aabbccddeeff") };
        byte[] bytes = LuminPackSerializer.Serialize(guid);
        if (bytes.Length != 17 || LuminPackSerializer.Deserialize<UnityPackedGuid>(bytes).Id != guid.Id)
            throw new InvalidOperationException("Packed framework storage lost data.");
    }

    /// <summary>Executes and terminates the headless Player with a meaningful exit status.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RunPlayer()
    {
        if (Application.isEditor || !Application.isBatchMode) return;
        try
        {
            string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string expected = Environment.GetEnvironmentVariable("LUMINPACK_UNITY_ARCH");
            if (!string.IsNullOrEmpty(expected) && architecture != expected)
                throw new InvalidOperationException("The Unity Player is running with the wrong architecture.");
            Debug.Log("LUMINPACK_UNITY_ARCH=" + architecture);
            Verify();
            Debug.Log("LUMINPACK_UNITY_SMOKE_PASSED");
            Application.Quit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Application.Quit(1);
        }
    }
}

/// <summary>Provides a graph with reusable reference and collection storage.</summary>
[LuminPackable]
public sealed class UnityMessage
{
    public int Id;
    public string Name;
    public UnityLeaf Child;
    public UnityChoice Choice;
    public List<UnityPortable> Items;
}

/// <summary>Provides a reusable child.</summary>
[LuminPackable]
public sealed class UnityLeaf { public int Value; }

/// <summary>Exercises ordinary virtual Union dispatch in the built Player.</summary>
[LuminPackable, LuminPackUnion(1, typeof(UnitySelected))]
public abstract partial class UnityChoice { }

/// <summary>Provides the Player's selected virtual case.</summary>
[LuminPackable]
public sealed partial class UnitySelected : UnityChoice { public int Value; }

/// <summary>Requires the assembly-scoped portable scalar loop.</summary>
[LuminPackable]
public struct UnityPortable { public byte Tag; public long Value; }

/// <summary>Overrides the assembly packed policy with automatic tagged storage.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Tagged)]
public struct UnityTagged { public byte Tag; public long Value; }

/// <summary>Keeps the native ABI explicitly.</summary>
[LuminPackable, LuminPackOption(StructLayout = LuminPackStructLayout.Native)]
public struct UnityNative { public byte Tag; public long Value; }

/// <summary>Expands opaque framework values without their reference-assembly placeholders.</summary>
[LuminPackable]
public struct UnityPackedGuid { public byte Tag; public Guid Id; }

/// <summary>Provides an original named schema.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed class UnityVersionOne { public int Id; public string Name; }

/// <summary>Provides a renamed and reordered named schema.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public sealed class UnityVersionTwo { [LuminPackOldName("Name")] public string DisplayName; public int Id; }

/// <summary>Exercises cyclic object reconstruction.</summary>
[LuminPackable(GeneratorType.CircleReference)]
public sealed class UnityTracked { [LuminPackOrder(0)] public UnityTracked Next; }
