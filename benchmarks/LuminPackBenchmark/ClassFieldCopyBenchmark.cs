using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using MemoryPack;
using Nino.Core;

namespace LuminPackBenchmark;

/// <summary>Supplies a contiguous primitive field group on a reference type.</summary>
[LuminPackable, MemoryPackable, NinoType, StructLayout(LayoutKind.Sequential)]
public partial class PrimitiveFieldGroup
{
    /// <summary>Stores lane A.</summary>
    public int A;
    /// <summary>Stores lane B.</summary>
    public int B;
    /// <summary>Stores lane C.</summary>
    public int C;
    /// <summary>Stores lane D.</summary>
    public int D;
    /// <summary>Stores lane E.</summary>
    public int E;
    /// <summary>Stores lane F.</summary>
    public int F;
    /// <summary>Stores lane G.</summary>
    public int G;
    /// <summary>Stores lane H.</summary>
    public int H;
}

/// <summary>Retains padding inside an individual raw field value.</summary>
[LuminPackable, MemoryPackable, NinoType, StructLayout(LayoutKind.Explicit, Size = 16)]
public partial struct PaddedFieldValue
{
    /// <summary>Stores the first byte.</summary>
    [FieldOffset(0)] public byte Tag;
    /// <summary>Stores the aligned scalar.</summary>
    [FieldOffset(8)] public long Value;
}

/// <summary>Contains contiguous raw values whose internal padding remains wire data.</summary>
[LuminPackable, MemoryPackable, NinoType, StructLayout(LayoutKind.Sequential)]
public partial class StructFieldGroup
{
    /// <summary>Stores the first scalar.</summary>
    public long Head;
    /// <summary>Stores the first padded raw value.</summary>
    public PaddedFieldValue First;
    /// <summary>Stores the second padded raw value.</summary>
    public PaddedFieldValue Second;
    /// <summary>Stores the final scalar.</summary>
    public long Tail;
}

/// <summary>Contains alignment gaps that are absent from the established packed wire format.</summary>
[LuminPackable, MemoryPackable, NinoType, StructLayout(LayoutKind.Sequential)]
public partial class GappedFieldGroup
{
    /// <summary>Stores the first byte.</summary>
    public byte A;
    /// <summary>Stores the first aligned scalar.</summary>
    public long B;
    /// <summary>Stores the second byte.</summary>
    public byte C;
    /// <summary>Stores the second aligned scalar.</summary>
    public long D;
}

/// <summary>Compares class field groups with the same reference libraries and reusable write destinations.</summary>
[MemoryDiagnoser]
public class ClassFieldCopyBenchmark
{
    private readonly LuminBufferWriter _lumin = LuminBufferWriterPool.Rent();
    private readonly ArrayBufferWriter<byte> _memory = new(1024);
    private readonly NinoArrayBufferWriter _nino = new(1024);
    private Action _luminWrite = null!, _memoryWrite = null!, _ninoWrite = null!;
    private Action _luminRead = null!, _memoryRead = null!, _ninoRead = null!;
    private byte[] _lp = null!, _mp = null!, _np = null!;
    private object? _sink;

    /// <summary>Gets or sets the layout shape measured by this fixture.</summary>
    [Params("Primitives", "PaddedStructs", "Gapped")]
    public string Shape { get; set; } = "Primitives";

    /// <summary>Binds concrete entry calls and verifies all libraries preserve the complete field values.</summary>
    [GlobalSetup]
    public void Setup()
    {
        switch (Shape)
        {
            case "Primitives":
                var primitive = new PrimitiveFieldGroup { A = 3, B = 5, C = 7, D = 11, E = 13, F = 17, G = 19, H = 23 };
                _lp = LuminPackSerializer.Serialize(primitive);
                _mp = MemoryPackSerializer.Serialize(primitive);
                _np = NinoSerializer.Serialize(primitive);
                _luminWrite = () => LuminPackSerializer.Serialize(primitive, _lumin);
                _memoryWrite = () => { _memory.ResetWrittenCount(); MemoryPackSerializer.Serialize(_memory, primitive); };
                _ninoWrite = () => { _nino.ResetWrittenCount(); NinoSerializer.Serialize(primitive, _nino); };
                _luminRead = () => _sink = LuminPackSerializer.Deserialize<PrimitiveFieldGroup>(_lp);
                _memoryRead = () => _sink = MemoryPackSerializer.Deserialize<PrimitiveFieldGroup>(_mp);
                _ninoRead = () => _sink = NinoDeserializer.Deserialize<PrimitiveFieldGroup>(_np);
                break;
            case "PaddedStructs":
                var structs = new StructFieldGroup { Head = 29, First = CreateRaw(31), Second = CreateRaw(37), Tail = -41 };
                _lp = LuminPackSerializer.Serialize(structs);
                _mp = MemoryPackSerializer.Serialize(structs);
                _np = NinoSerializer.Serialize(structs);
                _luminWrite = () => LuminPackSerializer.Serialize(structs, _lumin);
                _memoryWrite = () => { _memory.ResetWrittenCount(); MemoryPackSerializer.Serialize(_memory, structs); };
                _ninoWrite = () => { _nino.ResetWrittenCount(); NinoSerializer.Serialize(structs, _nino); };
                _luminRead = () => _sink = LuminPackSerializer.Deserialize<StructFieldGroup>(_lp);
                _memoryRead = () => _sink = MemoryPackSerializer.Deserialize<StructFieldGroup>(_mp);
                _ninoRead = () => _sink = NinoDeserializer.Deserialize<StructFieldGroup>(_np);
                break;
            case "Gapped":
                var gapped = new GappedFieldGroup { A = 43, B = -47, C = 53, D = -59 };
                _lp = LuminPackSerializer.Serialize(gapped);
                _mp = MemoryPackSerializer.Serialize(gapped);
                _np = NinoSerializer.Serialize(gapped);
                _luminWrite = () => LuminPackSerializer.Serialize(gapped, _lumin);
                _memoryWrite = () => { _memory.ResetWrittenCount(); MemoryPackSerializer.Serialize(_memory, gapped); };
                _ninoWrite = () => { _nino.ResetWrittenCount(); NinoSerializer.Serialize(gapped, _nino); };
                _luminRead = () => _sink = LuminPackSerializer.Deserialize<GappedFieldGroup>(_lp);
                _memoryRead = () => _sink = MemoryPackSerializer.Deserialize<GappedFieldGroup>(_mp);
                _ninoRead = () => _sink = NinoDeserializer.Deserialize<GappedFieldGroup>(_np);
                break;
            default: throw new InvalidOperationException("Unknown field-copy shape.");
        }
        foreach (var read in new[] { _luminRead, _memoryRead, _ninoRead })
        {
            read();
            bool valid = _sink switch
            {
                PrimitiveFieldGroup value => value is { A: 3, B: 5, C: 7, D: 11, E: 13, F: 17, G: 19, H: 23 },
                StructFieldGroup value => value.Head == 29 && value.First.Value == 31 && value.Second.Value == 37 && value.Tail == -41,
                GappedFieldGroup value => value is { A: 43, B: -47, C: 53, D: -59 },
                _ => false
            };
            if (!valid) throw new InvalidOperationException("A field-copy fixture lost data.");
        }
    }

    /// <summary>Creates nonzero storage padding without changing its logical members.</summary>
    /// <param name="number">The logical payload value.</param>
    /// <returns>A fully initialized raw value.</returns>
    private static PaddedFieldValue CreateRaw(long number)
    {
        PaddedFieldValue value = default;
        Unsafe.InitBlockUnaligned(ref Unsafe.As<PaddedFieldValue, byte>(ref value), 0xA5, 16);
        value.Tag = (byte)number;
        value.Value = number;
        return value;
    }

    /// <summary>Releases the reusable destination and retains the last parsed result.</summary>
    [GlobalCleanup]
    public void Cleanup() { _lumin.Dispose(); GC.KeepAlive(_sink); }

    /// <summary>Serializes through the concrete LuminPack entry.</summary>
    [Benchmark] public void LuminPackWrite() => _luminWrite();
    /// <summary>Serializes through MemoryPack with reset inside the timed operation.</summary>
    [Benchmark] public void MemoryPackWrite() => _memoryWrite();
    /// <summary>Serializes through Nino with reset inside the timed operation.</summary>
    [Benchmark] public void NinoWrite() => _ninoWrite();
    /// <summary>Deserializes a fresh LuminPack object.</summary>
    [Benchmark] public void LuminPackRead() => _luminRead();
    /// <summary>Deserializes a fresh MemoryPack object.</summary>
    [Benchmark] public void MemoryPackRead() => _memoryRead();
    /// <summary>Deserializes a fresh Nino object.</summary>
    [Benchmark] public void NinoRead() => _ninoRead();
}
