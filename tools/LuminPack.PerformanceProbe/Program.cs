using BenchmarkDotNet.Running;

if (args.Length >= 3 && args[0] == "--ratios-union")
{
    LibraryRatioProbe.RunUnion(int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture), args.Skip(2).ToArray());
    return;
}

if (args.Length >= 2 && args[0] == "--ratios-json")
{
    LibraryRatioProbe.RunJson(args.Skip(1).ToArray());
    return;
}

if (args is ["--verify-json-output", var jsonOutputDirectory])
{
    foreach (int length in new[] { 16, 30_000 })
    {
        var type = BenchmarkImage.Load(jsonOutputDirectory).GetType("LuminPackBenchmark.JsonBufferWriterBenchmark", true)!;
        object fixture = Activator.CreateInstance(type)!;
        type.GetProperty("Length")!.SetValue(fixture, length);
        try { type.GetMethod("Setup")!.Invoke(fixture, null); }
        finally { type.GetMethod("Cleanup")!.Invoke(fixture, null); }
    }
    Console.WriteLine("Verified small and large JSON direct-output bytes against the buffered path; no latency was measured.");
    return;
}

if (args is ["--verify-json", var jsonBaseline, var jsonCandidate])
{
    JsonWireVerification.Run(jsonBaseline, jsonCandidate);
    return;
}

if (args is ["--verify-simple", var simpleBaseline, var simpleCandidate])
{
    SimpleWireVerification.Run(simpleBaseline, simpleCandidate);
    return;
}

if (args is ["--native-summary", var disassemblyPath])
{
    NativeCodeSummary.Write(disassemblyPath);
    return;
}

if (args is ["--verify-character", var baselineImage, var candidateImage])
{
    CharacterWireVerification.Run(baselineImage, candidateImage);
    return;
}

if (args is ["--codegen", var codegenDirectory])
{
    SerializerCodegenProbe.Run(codegenDirectory);
    return;
}

if (args is ["--codegen-character", var characterDirectory])
{
    SerializerCodegenProbe.Run(characterDirectory, character: true);
    return;
}

if (args is ["--codegen-character-utf8", var utf8Directory])
{
    SerializerCodegenProbe.Run(utf8Directory, character: true, utf8: true);
    return;
}

if (args is ["--codegen-strings", var stringsDirectory])
{
    SerializerCodegenProbe.RunStringCases(stringsDirectory);
    return;
}

if (args.Length >= 2 && args[0] is "--ratios" or "--ratios-monomorphic" or "--ratios-write")
{
    LibraryRatioProbe.Run(args[0] == "--ratios-monomorphic", character: false, writeOnly: args[0] == "--ratios-write", args.Skip(1).ToArray());
    return;
}

if (args.Length >= 2 && args[0] == "--ratios-character")
{
    LibraryRatioProbe.Run(monomorphic: false, character: true, writeOnly: false, args.Skip(1).ToArray());
    return;
}

if (args.Length >= 3 && args[0] == "--ratios-strings")
{
    LibraryRatioProbe.RunStrings(args[1], args.Skip(2).ToArray());
    return;
}

if (args.Length >= 2 && args[0] == "--ratios-dictionary-sides")
{
    LibraryRatioProbe.RunDictionarySides(args.Skip(1).ToArray());
    return;
}

if (args is ["--codegen-dictionary-sides", var dictionaryDirectory])
{
    var type = BenchmarkImage.Load(dictionaryDirectory).GetType("LuminPackBenchmark.DictionaryRawSideBenchmark", throwOnError: true)!;
    object instance = Activator.CreateInstance(type)!;
    type.GetMethod("Setup")!.Invoke(instance, null);
    foreach (string name in new[] { "LuminPackKey", "LuminPackValue" }) type.GetMethod(name)!.Invoke(instance, null);
    type.GetMethod("Cleanup")!.Invoke(instance, null);
    return;
}

if (args.Length >= 3 && args[0] == "--ratios-field-groups")
{
    LibraryRatioProbe.RunFieldGroups(args[1], args.Skip(2).ToArray());
    return;
}

if (args is ["--codegen-field-groups", var fieldDirectory])
{
    foreach (string shape in new[] { "Primitives", "PaddedStructs", "Gapped" })
    {
        var type = BenchmarkImage.Load(fieldDirectory).GetType("LuminPackBenchmark.ClassFieldCopyBenchmark", throwOnError: true)!;
        object instance = Activator.CreateInstance(type)!;
        type.GetProperty("Shape")!.SetValue(instance, shape);
        type.GetMethod("Setup")!.Invoke(instance, null);
        type.GetMethod("LuminPackWrite")!.Invoke(instance, null);
        type.GetMethod("LuminPackRead")!.Invoke(instance, null);
        type.GetMethod("Cleanup")!.Invoke(instance, null);
    }
    return;
}

if (args.Length is 3 or 4 && args[0] == "--il")
{
    SerializerIlInspection.Write(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}

if (args.Length < 4 || args[0] != "--baseline" || args[2] != "--candidate")
{
    Console.Error.WriteLine("Usage: --baseline <retained-build-directory> --candidate <current-build-directory> [BenchmarkDotNet options]");
    Console.Error.WriteLine("       --il <build-directory> <payload-type-name-fragment> [exact-method-name]");
    Console.Error.WriteLine("       --ratios <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --ratios-write <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --ratios-character <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --ratios-strings <shape> <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --ratios-dictionary-sides <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --codegen-dictionary-sides <build-directory>");
    Console.Error.WriteLine("       --ratios-field-groups <shape> <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --codegen-field-groups <build-directory>");
    Console.Error.WriteLine("       --codegen <build-directory>");
    Console.Error.WriteLine("       --codegen-character <build-directory>");
    Console.Error.WriteLine("       --codegen-character-utf8 <build-directory>");
    Console.Error.WriteLine("       --codegen-strings <build-directory>");
    Console.Error.WriteLine("       --verify-character <baseline-build-directory> <candidate-build-directory>");
    Console.Error.WriteLine("       --verify-simple <baseline-build-directory> <candidate-build-directory>");
    Console.Error.WriteLine("       --verify-json <baseline-build-directory> <candidate-build-directory>");
    Console.Error.WriteLine("       --verify-json-output <build-directory>");
    Console.Error.WriteLine("       --ratios-json <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --ratios-union <fixed-percent> <build-directory> [additional-build-directory ...]");
    Console.Error.WriteLine("       --native-summary <JIT-disassembly-file>");
    Environment.ExitCode = 1;
    return;
}

Environment.SetEnvironmentVariable("LUMINPACK_PERF_BASELINE", Path.GetFullPath(args[1]));
Environment.SetEnvironmentVariable("LUMINPACK_PERF_CANDIDATE", Path.GetFullPath(args[3]));
BenchmarkSwitcher.FromAssembly(typeof(SerializerComparison).Assembly).Run(args.Skip(4).ToArray());
