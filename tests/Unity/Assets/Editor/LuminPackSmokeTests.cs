using NUnit.Framework;

/// <summary>Runs the generated graph in Unity's editor test runner.</summary>
public class LuminPackSmokeTests
{
    /// <summary>Checks binary, JSON, reuse, portable batches, versioning and reference tracking in Mono.</summary>
    [Test]
    public void GeneratedOperationsExecuteInUnity() => LuminPackSmoke.Verify();
}
