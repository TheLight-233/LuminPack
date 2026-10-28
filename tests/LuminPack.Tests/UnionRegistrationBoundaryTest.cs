using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks publication boundaries of externally registered Union operations.</summary>
public sealed class UnionRegistrationBoundaryTest
{
    /// <summary>Rejects invalid registration metadata and preserves the first complete valid entry.</summary>
    [Fact]
    public unsafe void InvalidRegistrationsDoNotPublishEntries()
    {
        // Minimal generation discovers the Union contract through an actual serializer operation.
        RegistrationAuditRoot? root = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(root), ref root);
        Assert.Null(root);
        Assert.Throws<ArgumentException>(() => Register(-1));
        Assert.Throws<ArgumentException>(() => Register(65536));
        Assert.Throws<ArgumentException>(() => Register(1));
        Assert.Throws<ArgumentException>(() => RegistrationAuditRoot.Register<string>(78, &Write, &Read, &WriteJson, &ReadJson));
        Assert.Throws<ArgumentException>(() => RegistrationAuditRoot.Register<RegistrationAuditCase>(78, null, &Read, &WriteJson, &ReadJson));
        Assert.False(RegistrationAuditRoot.TryGetRegisteredFormatter(78, out _));
        Register(77);
        Assert.Throws<ArgumentException>(() => Register(77));
        Assert.True(RegistrationAuditRoot.TryGetRegisteredFormatter(77, out LuminPackFormatter entry));
        Assert.Equal(77, entry.Tag);
        Assert.NotEqual(0, entry.WriteValue);
        Assert.NotEqual(0, entry.ReadValue);
        Assert.NotEqual(0, entry.WriteValueJson);
        Assert.NotEqual(0, entry.ReadValueJson);
    }

    /// <summary>Submits one complete pointer registration for a compatible member.</summary>
    /// <param name="tag">The proposed wire tag.</param>
    private static unsafe void Register(int tag) => RegistrationAuditRoot.Register<RegistrationAuditCase>(tag, &Write, &Read, &WriteJson, &ReadJson);

    /// <summary>Provides the binary write signature without invoking the callback.</summary>
    /// <param name="writer">The supplied writer.</param>
    /// <param name="value">The supplied root.</param>
    private static void Write(ref LuminPackWriter writer, ref RegistrationAuditRoot value) { }
    /// <summary>Provides the binary read signature without invoking the callback.</summary>
    /// <param name="reader">The supplied reader.</param>
    /// <param name="value">The supplied root storage.</param>
    private static void Read(ref LuminPackReader reader, ref RegistrationAuditRoot value) { }
    /// <summary>Provides the JSON write signature without invoking the callback.</summary>
    /// <param name="writer">The supplied writer.</param>
    /// <param name="value">The supplied root.</param>
    private static void WriteJson(ref LuminPackJsonWriter writer, ref RegistrationAuditRoot value) { }
    /// <summary>Provides the JSON read signature without invoking the callback.</summary>
    /// <param name="reader">The supplied reader.</param>
    /// <param name="value">The supplied root storage.</param>
    private static void ReadJson(ref LuminPackJsonReader reader, ref RegistrationAuditRoot value) { }
}

/// <summary>Supplies a root with one statically reserved tag.</summary>
[LuminPackable]
[LuminPackUnion(1, typeof(RegistrationAuditCase))]
public abstract partial class RegistrationAuditRoot { }

/// <summary>Supplies a compatible statically generated member.</summary>
[LuminPackable]
public sealed partial class RegistrationAuditCase : RegistrationAuditRoot
{
    /// <summary>Stores a fixed binary field.</summary>
    public int Number;
}
