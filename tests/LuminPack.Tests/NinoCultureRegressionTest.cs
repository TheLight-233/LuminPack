#if LUMINPACK_NEXT_GENERATOR
using System.Globalization;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies untrusted binary reads resolve only platform-enumerated culture names.</summary>
public class NinoCultureRegressionTest
{
    /// <summary>Checks installed cultures and case-insensitive names through the concrete root.</summary>
    [Fact]
    public void UntrustedCultureReadsKeepPlatformCulturesAndAliases()
    {
        CultureInfo? target = null;
        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.AllCultures).Append(CultureInfo.InvariantCulture))
        {
            byte[] bytes = LuminPackSerializer.Serialize(culture);
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Equal(culture.Name, target!.Name);
        }

        foreach (string name in new[] { "en-us", "zh-CN", "zh-TW" })
        {
            var expected = CultureInfo.GetCultureInfo(name);
            byte[] bytes = NameRecord(name);
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref target));
            Assert.Same(expected, target);
        }
    }

    /// <summary>Rejects a non-enumerated name at both a concrete root and a generated member.</summary>
    [Fact]
    public void UntrustedCultureReadsRejectNonEnumeratedNames()
    {
        byte[] bytes = NameRecord("en-US-x-test");
        CultureInfo? target = CultureInfo.GetCultureInfo("en-US");
        var original = target;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
        Assert.Same(original, target);

        byte[] memberBytes = [1, .. bytes];
        CultureRecord? record = null;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(memberBytes, ref record));
    }

    /// <summary>Checks an accepted trusted name cannot be admitted to an untrusted read by caching it first.</summary>
    [Fact]
    public void TrustedCultureReadDoesNotAuthorizeTheNameForUntrustedInput()
    {
        byte[] bytes = NameRecord("en-US-x-test");
        byte[] memberBytes = [1, .. bytes];
        TrustedCultureRecord? trusted = null;
        if (Record.Exception(() => CultureInfo.GetCultureInfo("en-US-x-test")) is CultureNotFoundException)
            return;

        Assert.Equal(memberBytes.Length, LuminPackSerializer.Deserialize(memberBytes, ref trusted));
        Assert.Equal(CultureInfo.GetCultureInfo("en-US-x-test").Name, trusted!.Culture!.Name);

        var target = trusted.Culture;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(bytes, ref target));
    }

    /// <summary>Encodes a culture's present marker and its existing binary string wire value.</summary>
    /// <param name="name">The wire culture name.</param>
    /// <returns>The binary culture record.</returns>
    private static byte[] NameRecord(string name) => [0, .. LuminPackSerializer.Serialize(name)];

    /// <summary>Provides an implicitly untrusted culture member.</summary>
    [LuminPackable]
    public sealed class CultureRecord
    {
        public CultureInfo? Culture;
    }

    /// <summary>Provides an explicitly trusted culture member.</summary>
    [LuminPackable, LuminPackOption(UntrustedData = false)]
    public sealed class TrustedCultureRecord
    {
        public CultureInfo? Culture;
    }
}
#endif
