using System.Runtime.CompilerServices;
using LuminPack.Code;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exception identity and validation parameters during helper consolidation.</summary>
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
        Action callback = () => { };
        Assert.Equal("type", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(null!, callback, callback)).ParamName);
        Assert.Equal("writeValue", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(typeof(int), null!, callback)).ParamName);
        Assert.Equal("readValue", Assert.Throws<ArgumentNullException>(() => LuminPackFormatterRegistry.Register(typeof(int), callback, null!)).ParamName);
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
