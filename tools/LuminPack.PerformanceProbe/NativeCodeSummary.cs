using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>Summarizes static native-code structure from a CoreCLR JIT disassembly file.</summary>
internal static class NativeCodeSummary
{
    /// <summary>Prints instruction, frame, and barrier call-site counts without running the inspected code.</summary>
    /// <param name="path">A JIT disassembly file produced by the code-generation probe.</param>
    internal static void Write(string path)
    {
        var methods = new List<object>();
        foreach (string section in Regex.Split(File.ReadAllText(path), @"(?=; Assembly listing for method )"))
        {
            if (!section.StartsWith("; Assembly listing for method ", StringComparison.Ordinal)) continue;
            var lines = section.Split('\n').Select(line => line.Trim()).ToArray();
            var instructions = lines.Where(line => Regex.IsMatch(line, @"^[a-z][a-z0-9]*\s")).ToArray();
            int end = section.IndexOf("G_M000_IG02:", StringComparison.Ordinal);
            string prologue = end >= 0 ? section[..end] : string.Empty;
            var frame = Regex.Match(prologue, @"sub\s+rsp,\s+(0[xX][0-9a-fA-F]+|\d+)");
            string frameSize = frame.Groups[1].Value;
            int subtraction = !frame.Success ? 0 : frameSize.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(frameSize[2..], 16) : int.Parse(frameSize, System.Globalization.CultureInfo.InvariantCulture);
            int pushes = Regex.Matches(prologue, @"(?m)^\s*push\s+").Count;
            var bytes = Regex.Match(section, @"; Total bytes of code (\d+)");
            methods.Add(new
            {
                signature = lines[0]["; Assembly listing for method ".Length..],
                nativeBytes = bytes.Success ? int.Parse(bytes.Groups[1].Value) : 0,
                instructions = instructions.Length,
                prologueStackBytes = subtraction + pushes * 8,
                savedRegisters = Regex.Matches(prologue, @"(?m)^\s*push\s+(?:rbx|rbp|rsi|rdi|r1[2-5])\s*$").Count,
                blocks = Regex.Matches(section, @"(?m)^G_M\d+_IG\d+:").Count,
                calls = instructions.Count(line => line.StartsWith("call ", StringComparison.Ordinal)),
                bitnessQueries = instructions.Count(line => line.Contains("System.Environment:get_Is64BitProcess", StringComparison.Ordinal) ||
                    line.Contains("System.IntPtr:get_Size", StringComparison.Ordinal)),
                checkedReferenceStores = instructions.Count(line => line.Contains("CORINFO_HELP_CHECKED_ASSIGN_REF", StringComparison.Ordinal)),
                heapReferenceStores = instructions.Count(line => line.Contains("CORINFO_HELP_ASSIGN_REF", StringComparison.Ordinal)),
                covariantArrayStores = instructions.Count(line => line.Contains("CORINFO_HELP_ARRADDR_ST", StringComparison.Ordinal)),
                frameMoveSites = instructions.Count(line => Regex.IsMatch(line, @"^(v?mov\w*)\s") && Regex.IsMatch(line, @"\[(?:rsp|rbp)")),
                addressCalculations = instructions.Count(line => line.StartsWith("lea ", StringComparison.Ordinal))
            });
        }
        Console.WriteLine(JsonSerializer.Serialize(methods, new JsonSerializerOptions { WriteIndented = true }));
    }
}
