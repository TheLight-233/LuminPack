using System;
using System.Collections.Generic;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest
{
    /// <summary>
    /// Verifies the "deserialize into existing instance" contract of the ref-value
    /// Deserialize overloads: a non-null root is reused (identity preserved, no allocation),
    /// null roots are constructed, and null payloads do not touch an existing root.
    /// </summary>
    public class DeserializeMergeTest
    {
        [LuminPackable]
        public sealed class MergeTarget
        {
            public int A;
            public string Name = "";
            public List<int> Items = new();
        }

        private static byte[] Payload(int a, string name, params int[] items)
        {
            var value = new MergeTarget { A = a, Name = name, Items = new List<int>(items) };
            return LuminPackSerializer.Serialize(value);
        }

        [Fact]
        public void ReusesExistingRoot_IdentityPreserved()
        {
            var existing = new MergeTarget { A = 1, Name = "old", Items = new List<int> { 9 } };
            var identity = existing;

            byte[] buffer = Payload(42, "new", 1, 2, 3);
            ReadOnlySpan<byte> span = buffer;
            LuminPackSerializer.Deserialize<MergeTarget>(span, ref existing);

            Assert.Same(identity, existing); // reuse, not replace
            Assert.Equal(42, existing.A);
            Assert.Equal("new", existing.Name);
            Assert.Equal(new[] { 1, 2, 3 }, existing.Items);
        }

        [Fact]
        public void ConstructsWhenRootIsNull()
        {
            MergeTarget? existing = null;

            byte[] buffer = Payload(7, "created");
            ReadOnlySpan<byte> span = buffer;
            LuminPackSerializer.Deserialize<MergeTarget>(span, ref existing);

            Assert.NotNull(existing);
            Assert.Equal(7, existing.A);
            Assert.Equal("created", existing.Name);
        }

        [Fact]
        public void NullPayloadLeavesExistingRootUntouched()
        {
            var existing = new MergeTarget { A = 5, Name = "keep" };
            var identity = existing;

            byte[] buffer = LuminPackSerializer.Serialize<MergeTarget?>(null);
            ReadOnlySpan<byte> span = buffer;
            LuminPackSerializer.Deserialize<MergeTarget>(span, ref existing);

            Assert.Same(identity, existing);
            Assert.Equal(5, existing.A);
            Assert.Equal("keep", existing.Name);
        }
    }
}