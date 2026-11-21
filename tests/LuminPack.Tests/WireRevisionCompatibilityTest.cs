using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>Checks that one-pass version-tolerant records still read revision 1, and the reference-tracked record shape.</summary>
    public class WireRevisionCompatibilityTest
    {
        /// <summary>A revision-1 record written by the previous release: every payload length precedes the payloads.</summary>
        private const string VersionRevision1 = "FB0102040249640140044E616D65014C0656616C75657306522369345B5D054368696C64145040436F6D70617456657273696F6E4368696C640408101E07000000090000006E616D6503000000010000000200000003000000FB01020204466C6167014D055374616D7001460108016300000000000000";

        /// <summary>Reads a revision-1 record, including a nested record, and writes the current revision.</summary>
        [Fact]
        public void VersionRecordsReadRevisionOne()
        {
            CompatVersion? value = null;
            byte[] old = Convert.FromHexString(VersionRevision1);
            Assert.Equal(old.Length, LuminPackSerializer.Deserialize(old, ref value));
            Assert.Equal(7, value!.Id);
            Assert.Equal("name", value.Name);
            Assert.Equal(new[] { 1, 2, 3 }, value.Values);
            Assert.True(value.Child!.Flag);
            Assert.Equal(99, value.Child.Stamp);

            byte[] current = LuminPackSerializer.Serialize(value);
            Assert.Equal(251, current[0]);
#if LUMINPACK_NEXT_GENERATOR
            Assert.Equal(0x42, current[1]);
#else
            Assert.Equal(2, current[1]);
#endif
            CompatVersion? copy = null;
            Assert.Equal(current.Length, LuminPackSerializer.Deserialize(current, ref copy));
            Assert.Equal(new[] { 1, 2, 3 }, copy!.Values);
            Assert.Equal(99, copy.Child!.Stamp);
        }

        /// <summary>Writes a cycle with explicit reference ids and round-trips it.</summary>
        [Fact]
        public void ReferenceRecordsPreserveIdentity()
        {
            var value = new CompatNode { Id = 1, Text = "a", Next = new CompatNode { Id = 2, Text = "bb" } };
            value.Next.Next = value;

            byte[] current = LuminPackSerializer.Serialize(value);
#if LUMINPACK_NEXT_GENERATOR
            // The first reference header is followed by a complete version record.
            Assert.Equal(new byte[] { 3, 0, 251, 0x42, 3 }, current[..5]);
#else
            // Slot count, one width byte per slot (a raw width or 255 for a length-prefixed value), then reference id zero.
            Assert.Equal(new byte[] { 3, 4, 255, 255, 0 }, current[..5]);
#endif
            // The cycle closes with a back reference to id zero behind its four-byte length.
            Assert.Equal(new byte[] { 2, 0, 0, 0, 250, 0 }, current[^6..]);
            CompatNode? copy = null;
            Assert.Equal(current.Length, LuminPackSerializer.Deserialize(current, ref copy));
            Assert.Same(copy, copy!.Next!.Next);
            Assert.Equal("bb", copy.Next.Text);
            // Reading into the same graph again reuses its instances.
            var next = copy.Next;
            LuminPackSerializer.Deserialize(current, ref copy);
            Assert.Same(next, copy.Next);
        }

        /// <summary>A receiving model can skip unknown versioned members in a reference-tracked record.</summary>
        [Fact]
        public void ReferenceRecordsSkipUnknownMembers()
        {
            var wide = new CompatWideNode { Id = 3, Text = "known", Extra = "unknown", Tail = 17 };
            byte[] bytes = LuminPackSerializer.Serialize(wide);
            CompatNarrowNode? narrow = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref narrow));
            Assert.Equal(3, narrow!.Id);
            Assert.Equal("known", narrow.Text);
        }
    }
}

/// <summary>A version-tolerant model matching the recorded revision-1 fixture.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class CompatVersion { public int Id; public string Name = ""; public int[] Values = []; public CompatVersionChild? Child; }

/// <summary>A nested version-tolerant model of fixed-width members.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version)]
public partial class CompatVersionChild { public bool Flag; public long Stamp; }

/// <summary>A reference-tracked model with a self-referential member.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
public partial class CompatNode { [LuminPackOrder(0)] public int Id; [LuminPackOrder(1)] public string Text = ""; [LuminPackOrder(2)] public CompatNode? Next; }

/// <summary>A writer model with trailing slots unknown to <see cref="CompatNarrowNode"/>.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
public partial class CompatWideNode { [LuminPackOrder(0)] public int Id; [LuminPackOrder(1)] public string Text = ""; [LuminPackOrder(2)] public string Extra = ""; [LuminPackOrder(3)] public int Tail; }

/// <summary>A reader model with the first two slots of <see cref="CompatWideNode"/>.</summary>
[LuminPackable, LuminPackOption(GeneratorType = GeneratorType.Version, TrackReferences = true)]
public partial class CompatNarrowNode { [LuminPackOrder(0)] public int Id; [LuminPackOrder(1)] public string Text = ""; }
