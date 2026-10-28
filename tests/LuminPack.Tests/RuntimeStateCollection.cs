using Xunit;

namespace LuminPackUnitTest;

/// <summary>Runs legacy suites that drain shared pools without concurrent borrowers from other test collections.</summary>
[CollectionDefinition(nameof(RuntimeStateCollection), DisableParallelization = true)]
public sealed class RuntimeStateCollection
{
}
