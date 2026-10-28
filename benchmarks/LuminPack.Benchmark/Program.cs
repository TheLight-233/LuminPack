using BenchmarkDotNet.Running;
using LuminPack.Attribute;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]

/// <summary>Runs the selected benchmark classes, or lets the user pick one interactively.</summary>
internal static class Program
{
    /// <summary>Starts BenchmarkDotNet.</summary>
    /// <param name="args">BenchmarkDotNet arguments, for example <c>--filter *SimpleBenchmark*</c>.</param>
    /// <returns>Zero for a successful invocation, or one for a benchmark failure or native host mismatch.</returns>
    private static int Main(string[] args)
    {
        if (args is ["--verify-host", var architecture, var vendor])
            return VerifyHost(architecture, vendor);

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return summaries.Any(summary => summary.HasCriticalValidationErrors ||
            summary.Reports.Any(report => !report.Success)) ? 1 : 0;
    }

    /// <summary>Checks the actual .NET process architecture and optional x86 CPU vendor before timing.</summary>
    /// <param name="architecture">The required native architecture, such as x64 or arm64.</param>
    /// <param name="vendor">The required CPUID vendor, or any to accept the detected vendor.</param>
    /// <returns>Zero when the native host matches, or one otherwise.</returns>
    private static int VerifyHost(string architecture, string vendor)
    {
        string actualArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
        string actualVendor = "non-x86";
        if (X86Base.IsSupported)
        {
            var cpu = X86Base.CpuId(0, 0);
            Span<byte> bytes = stackalloc byte[12];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, cpu.Ebx);
            BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], cpu.Edx);
            BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], cpu.Ecx);
            actualVendor = Encoding.ASCII.GetString(bytes);
        }

        Console.WriteLine($"Process architecture: {actualArchitecture}; OS architecture: {RuntimeInformation.OSArchitecture}; CPU vendor: {actualVendor}");
        Console.WriteLine($"{RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}");
        if (!string.Equals(actualArchitecture, architecture, StringComparison.OrdinalIgnoreCase) ||
            RuntimeInformation.ProcessArchitecture != RuntimeInformation.OSArchitecture ||
            (vendor != "any" && !string.Equals(actualVendor, vendor, StringComparison.Ordinal)))
        {
            Console.Error.WriteLine($"Expected a native {architecture} host with CPU vendor {vendor}.");
            return 1;
        }

        return 0;
    }
}
