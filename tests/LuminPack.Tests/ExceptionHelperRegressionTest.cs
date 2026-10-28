using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exception identity, validation parameters, and immutable collection behavior during helper consolidation.</summary>
public class ExceptionHelperRegressionTest
{
    /// <summary>Checks that cleanup rethrows preserve the original exception instance, data, and throwing frame.</summary>
    [Fact]
    public void RethrowPreservesExceptionIdentityAndOriginalStack()
    {
        var expected = new InvalidOperationException("Original failure");
        expected.Data["context"] = 42;
        var actual = Assert.Throws<InvalidOperationException>(() => RethrowAfterCleanup(expected));
        Assert.Same(expected, actual);
        Assert.Equal(42, actual.Data["context"]);
        Assert.Contains(nameof(ThrowOriginal), actual.StackTrace);
    }

    /// <summary>Checks null-argument parameter names after formatter registration delegates to the helper.</summary>
    [Fact]
    public void FormatterRegistrationPreservesParameterNames()
    {
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(null!, 1, 1)).ParamName);
        Assert.Equal("writeValue", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(typeof(int), 0, 1)).ParamName);
        Assert.Equal("readValue", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(typeof(int), 1, 0)).ParamName);
    }

    /// <summary>Checks that frozen dictionary mutation and missing-key failures retain their standard exception types.</summary>
    [Fact]
    public void FrozenDictionaryPreservesValidationAndMutationFailures()
    {
        var map = LuminFrozenMap<string, int>.Empty;
        Assert.Equal("key", Assert.Throws<ArgumentNullException>(() => map.ContainsKey(null!)).ParamName);
        var missing = Assert.Throws<KeyNotFoundException>(() => { var value = map["missing"]; });
        Assert.Equal(new KeyNotFoundException().Message, missing.Message);
        Action[] mutations =
        [
            () => map.Add("key", 1),
            () => map.Add(new KeyValuePair<string, int>("key", 1)),
            () => map.Remove("key"),
            () => map.Remove(new KeyValuePair<string, int>("key", 1)),
            () => map.Clear(),
            () => ((IDictionary<string, int>)map)["key"] = 1
        ];
        foreach (var mutation in mutations)
        {
            Assert.Equal(new NotSupportedException().Message, Assert.Throws<NotSupportedException>(mutation).Message);
        }
    }

    /// <summary>Rethrows a captured test exception through the shared cleanup helper.</summary>
    /// <param name="exception">The exception whose identity and stack trace are checked.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RethrowAfterCleanup(Exception exception)
    {
        try
        {
            ThrowOriginal(exception);
        }
        catch (Exception caught)
        {
            LuminPackExceptionHelper.Rethrow(caught);
        }
    }

    /// <summary>Provides an identifiable original throwing frame for the stack-preservation regression.</summary>
    /// <param name="exception">The exception to throw.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOriginal(Exception exception) => throw exception;
}
