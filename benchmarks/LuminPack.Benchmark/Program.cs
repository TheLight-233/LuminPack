using BenchmarkDotNet.Running;
using LuminPack.Attribute;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

[assembly: LuminPackGeneratorOptions(LuminPackGenerationMode.Minimal)]

/// <summary>Runs benchmark classes and their separate native-host diagnostics.</summary>
internal static class Program
{
    /// <summary>Starts BenchmarkDotNet or a native-host diagnostic.</summary>
    /// <param name="args">BenchmarkDotNet arguments or a native-host diagnostic option.</param>
    /// <returns>Zero for a successful invocation, or one for a benchmark or diagnostic failure.</returns>
    private static int Main(string[] args)
    {
        if (args is ["--verify-host", var architecture, var vendor])
            return VerifyHost(architecture, vendor);
        if (args is ["--polymorphism-native-probe", var operation])
            return CapturePolymorphismNativeCode(operation);

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

    /// <summary>Exercises one existing polymorphism operation for a separate JIT disassembly capture.</summary>
    /// <param name="operation">The LuminPack or Nino read or write operation to exercise.</param>
    /// <returns>Zero when the selected operation completed, or one when it is unknown.</returns>
    private static int CapturePolymorphismNativeCode(string operation)
    {
        var benchmark = new LuminPackBenchmark.PolymorphismBenchmark();
        Action? action = operation switch
        {
            "lumin-write" => benchmark.LuminPackSerialize,
            "lumin-read" => benchmark.LuminPackDeserialize,
            "nino-write" => benchmark.NinoSerialize,
            "nino-read" => benchmark.NinoDeserialize,
            _ => null
        };
        if (action is null)
        {
            Console.Error.WriteLine($"Unknown polymorphism native probe operation: {operation}.");
            benchmark.CleanUp();
            return 1;
        }

        Console.Error.WriteLine($"Native probe: {operation}; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; server GC: {GCSettings.IsServerGC}");
        try
        {
            benchmark.SetUp();
            DescribePolymorphismRoots();
            var timer = Stopwatch.StartNew();
            long operations = 0;
            do
            {
                for (int i = 0; i < 256; i++) action();
                operations += 256;
            }
            while (timer.Elapsed.TotalSeconds < 8);

            Console.Error.WriteLine($"Completed {operations} operations in {timer.Elapsed.TotalSeconds:F3} seconds.");
            return 0;
        }
        finally
        {
            benchmark.CleanUp();
        }
    }

    /// <summary>Identifies the generated collection methods in the accompanying JIT listings.</summary>
    private static void DescribePolymorphismRoots()
    {
        foreach (Type type in typeof(Program).Assembly.GetTypes().Where(type =>
            type.IsNested && type.Name.StartsWith("__LuminRoots_", StringComparison.Ordinal)))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (method.Name is not ("RootRead" or "RootWrite")) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 2) continue;
                Type parameter = parameters[1].ParameterType;
                if (parameter.IsByRef) parameter = parameter.GetElementType()!;
                if (!parameter.IsGenericType || !parameter.GenericTypeArguments.Contains(typeof(LuminPackBenchmark.IFoo))) continue;

                int? ilBytes = method.GetMethodBody()?.GetILAsByteArray()?.Length;
                Console.Error.WriteLine($"Generated root: {type.FullName}:{method.Name}; parameter: {parameter}; IL bytes: {ilBytes}");
            }
        }
    }
}
