using Xunit;

namespace LuminPackUnitTest;

/// <summary>Runs suites that drain shared pools, or measure registry revisions and thread allocations, without concurrent tests from other collections.</summary>
[CollectionDefinition(nameof(RuntimeStateCollection), DisableParallelization = true)]
public sealed class RuntimeStateCollection
{
}
