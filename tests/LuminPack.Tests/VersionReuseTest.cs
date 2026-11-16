#if LUMINPACK_NEXT_GENERATOR
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks immutable Version reuse after a complete binary record.</summary>
public sealed class VersionReuseTest
{
    /// <summary>Keeps equal instances and publishes new instances for different components.</summary>
    [Fact]
    public void EqualComponentsKeepTheirInstance()
    {
        foreach (Version expected in new Version[] { new(1, 2), new(1, 2, 3), new(1, 2, 3, 4) })
        {
            byte[] bytes = LuminPackSerializer.Serialize(expected);
            Version? target = new Version(7, 8);
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Equal(expected, target);

            Version? original = target;
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Same(original, target);

            byte[] nullBytes = LuminPackSerializer.Serialize((Version?)null);
            LuminPackSerializer.Deserialize(nullBytes, ref target);
            Assert.Null(target);
        }
    }

    /// <summary>Retains matching versions in a binary member and preserves the destination on truncated roots.</summary>
    [Fact]
    public void MatchingMemberAndTruncatedRootKeepTheExistingVersion()
    {
        var original = new Version(1, 2, 3, 4);
        var envelope = new VersionReuseEnvelope { Number = original };
        byte[] encoded = LuminPackSerializer.Serialize(envelope);
        LuminPackSerializer.Deserialize(encoded, ref envelope);
        Assert.Same(original, envelope.Number);

        byte[] root = LuminPackSerializer.Serialize(original);
        for (int length = 0; length < root.Length; length++)
        {
            Version? target = original;
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(root.AsSpan(0, length), ref target));
            Assert.Same(original, target);
        }
    }
}

/// <summary>Provides an ordinary binary member for Version reuse.</summary>
[LuminPackable]
public sealed partial class VersionReuseEnvelope
{
    /// <summary>The version to decode into the existing object.</summary>
    public Version? Number;
}
#endif
