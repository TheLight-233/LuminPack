using BenchmarkDotNet.Running;
using LuminPack.Attribute;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]

/// <summary>Runs the selected benchmark classes, or lets the user pick one interactively.</summary>
internal static class Program
{
    /// <summary>Starts BenchmarkDotNet.</summary>
    /// <param name="args">BenchmarkDotNet arguments, for example <c>--filter *SimpleBenchmark*</c>.</param>
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
