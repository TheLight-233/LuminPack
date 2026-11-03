using System.Diagnostics;

if (args is ["--verify-runtime"])
{
    if (GC.GetConfigurationVariables().TryGetValue("SatoriGC", out var enabled) && enabled is true)
        return 0;

    Console.Error.WriteLine("The isolated compiler runtime did not activate Satori GC.");
    return 1;
}

string compiler = Environment.GetEnvironmentVariable("LUMINPACK_SATORI_CSC") ?? string.Empty;
string muxer = Path.Combine(AppContext.BaseDirectory, "satori", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
if (!File.Exists(compiler) || !File.Exists(muxer))
{
    Console.Error.WriteLine("Satori or the SDK C# compiler is missing. Restore and rebuild, or set LuminPackUseSatori=false.");
    return 1;
}

var start = new ProcessStartInfo(muxer) { UseShellExecute = false };
start.ArgumentList.Add(Path.GetFullPath(compiler));
foreach (string argument in args)
    start.ArgumentList.Add(argument);
start.Environment["DOTNET_gcServer"] = "0";
start.Environment["DOTNET_GCDynamicAdaptationMode"] = "0";

using var process = Process.Start(start)!;
process.WaitForExit();
return process.ExitCode;
