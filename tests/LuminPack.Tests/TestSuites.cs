using Xunit;

namespace LuminPackUnitTest;
/// <summary>Runs shared scalar and boundary matrices without per-feature runner classes.</summary>
[Collection(nameof(RuntimeStateCollection))]
public partial class LuminPackXUnitTests
{
    /// <summary>Verifies run.</summary>
    /// <param name = "runner">The runner used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static List<string> Run(Action<List<string>> runner)
    {
        var results = new List<string>();
        runner(results);
        return results;
    }

    /// <summary>Verifies assert suite passed.</summary>
    /// <param name = "results">The results used by this operation.</param>
    private static void AssertSuitePassed(List<string> results)
    {
        var failures = results.Where(static result => !result.StartsWith("✓")).ToArray();
        Assert.True(failures.Length == 0, "Suite failures (" + failures.Length + "):\n" + string.Join("\n", failures));
    }

    /// <summary>Compresses the input using the LuminPack compression format.</summary>
    [Fact]
    public void CompressionSafetyPerf() => AssertSuitePassed(Run(CompressionSafetyPerfTest.Run));
    /// <summary>Reads er boundary validation from the current input.</summary>
    [Fact]
    public void ReaderBoundaryValidation() => AssertSuitePassed(Run(ReaderBoundaryValidationTest.Run));
    /// <summary>Checks the shared typed scalar boundary matrix.</summary>
    [Fact]
    public void ScalarBoundaries() => AssertSuitePassed(Run(PrimitivesSerializationTest.TestScalarRoundTrips));
}
