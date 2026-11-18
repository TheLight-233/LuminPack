"""CI 57 round 4: copy and allocation probes plus end-to-end Core policy variants on one host.

The matrix is built once. Each variant rebuilds only LuminPack.Core (net10.0) from a patched copy of the
two policy points and replaces the DLL in a copy of the matrix output, so every variant shares the same
generated code and references. Variants run in rotating order within each process round.
"""
import json
import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path

MATRIX = Path("benchmarks/LuminPack.MatrixBenchmark")
CORE = Path("src/LuminPack.Core")
COPY_FILE = CORE / "Utility/Memory/Simd/LuminPackMarshal.Copy.cs"
ALLOC_FILE = CORE / "Utility/Memory/LuminPackMarshal.cs"
FLAGS = ["-c", "Release", "-f", "net10.0", "-p:LuminPackLegacyGeneratorReference=false",
         "-p:LuminPackUseSatori=false", "-p:GeneratePackageOnBuild=false"]
OUTPUT = Path("bin/Release/net10.0")

COLLECTION_CASES = [
    "Array_Value", "List_Value", "Queue_Value", "Stack_Value", "IEnumerable_Value", "ICollection_Value", "IList_Value",
    "IReadOnlyCollection_Value", "IReadOnlyList_Value", "Memory_Value", "ReadOnlyMemory_Value", "ArraySegment_Value",
    "ReadOnlySequence_Value", "Array2D_Value", "ImmutableArray_Value", "ReadOnlyCollection_Value", "PriorityQueue_Value",
    "Dictionary_Value", "HashSet_Value", "List_Object", "List_String",
]
MAIN_CASES = [
    "Int32Array", "ListInt32", "MemoryInt32", "ReadOnlyMemoryInt32", "ArraySegmentInt32", "ReadOnlySequenceInt32",
    "ReadOnlyCollectionInt32", "IListInt32", "IReadOnlyCollectionInt32", "QueueInt32", "StackInt32", "Int32Matrix",
    "ImmutableArrayInt32", "PriorityQueueInt32Int32", "Int32", "String", "DictionaryInt32String", "Reference_ListString",
]
SCENES = ["main", "small", "regular", "large", "changing"]
PROCESSES = 3
ROUNDS = "9"

MEDIUM_CALL = "            if (!destinationIsNew && LuminKernelPolicy.UseMediumCopy(true, Vector256.IsHardwareAccelerated, byteCount))"
MEDIUM_BODY = "                CopyMediumBlock(ref destination, ref src, byteCount);"
SHORT_DOC = "    /// <summary>Copies at most 32 bytes with exact cached lanes and no long vector loop.</summary>"
WIDE_METHOD = '''    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CopyWideBlock(ref byte destination, ref byte source, uint count)
    {
        Vector512.LoadUnsafe(ref source).StoreUnsafe(ref destination);
        nuint last = count - (nuint)Vector512<byte>.Count;
        nuint address = (nuint)Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref destination);
        nuint offset = (~address & (nuint)(Vector512<byte>.Count - 1)) + 1;
        for (; offset + 3 * (nuint)Vector512<byte>.Count <= last; offset += 4 * (nuint)Vector512<byte>.Count)
        {
            var a = Vector512.LoadUnsafe(ref source, offset);
            var b = Vector512.LoadUnsafe(ref source, offset + 64);
            var c = Vector512.LoadUnsafe(ref source, offset + 128);
            var d = Vector512.LoadUnsafe(ref source, offset + 192);
            a.StoreUnsafe(ref destination, offset);
            b.StoreUnsafe(ref destination, offset + 64);
            c.StoreUnsafe(ref destination, offset + 128);
            d.StoreUnsafe(ref destination, offset + 192);
        }
        for (; offset <= last; offset += 64)
            Vector512.LoadUnsafe(ref source, offset).StoreUnsafe(ref destination, offset);
        Vector512.LoadUnsafe(ref source, last).StoreUnsafe(ref destination, last);
    }

'''
ALLOC_LINE = "        return GC.AllocateUninitializedArray<T>(length);"


def replace_once(text, old, new, name):
    if text.count(old) != 1:
        raise ValueError(f"Patch point for {name} is missing or repeated")
    return text.replace(old, new)


def variant_sources(name, copy_text, alloc_text):
    """Returns the two patched policy sources for a variant."""
    if name == "copy-runtime-2k":
        copy_text = replace_once(copy_text, MEDIUM_CALL, MEDIUM_CALL.replace("!destinationIsNew && ", "!destinationIsNew && byteCount <= 2048 && "), name)
    elif name == "copy-runtime":
        copy_text = replace_once(copy_text, MEDIUM_CALL, MEDIUM_CALL.replace("!destinationIsNew && ", "!destinationIsNew && byteCount == 0 && "), name)
    elif name == "copy-wide512":
        copy_text = replace_once(copy_text, MEDIUM_BODY,
                                 "                if (Vector512.IsHardwareAccelerated) CopyWideBlock(ref destination, ref src, byteCount); else\n" + MEDIUM_BODY, name)
        copy_text = replace_once(copy_text, SHORT_DOC, WIDE_METHOD + SHORT_DOC, name)
    elif name.startswith("alloc-new-below-"):
        limit = name.rsplit("-", 1)[1]
        limit = {"64k": "65536", "all": "int.MaxValue"}[limit]
        alloc_text = replace_once(alloc_text, ALLOC_LINE,
                                  f"        if ((long)length * Unsafe.SizeOf<T>() < {limit})\n            return new T[length];\n" + ALLOC_LINE, name)
    elif name != "stock":
        raise ValueError(name)
    return copy_text, alloc_text


VARIANTS = ["stock", "copy-runtime-2k", "copy-runtime", "copy-wide512", "alloc-new-below-64k", "alloc-new-below-all"]


def run(command, **kwargs):
    print("+", " ".join(map(str, command)), flush=True)
    subprocess.run(list(map(str, command)), check=True, **kwargs)


def cpu_name():
    try:
        if sys.platform == "linux":
            for line in subprocess.run(["lscpu"], capture_output=True, text=True).stdout.splitlines():
                if line.startswith("Model name"):
                    return line.split(":", 1)[1].strip()
        if sys.platform == "darwin":
            return subprocess.run(["sysctl", "-n", "machdep.cpu.brand_string"], capture_output=True, text=True).stdout.strip()
        if sys.platform == "win32":
            return subprocess.run(["powershell", "-NoProfile", "-Command", "(Get-CimInstance Win32_Processor).Name"],
                                  capture_output=True, text=True).stdout.strip()
    except OSError:
        pass
    return platform.processor()


def host_info(out):
    with (out / "host.txt").open("w", encoding="utf-8") as log:
        print(platform.platform(), platform.machine(), cpu_name(), file=log, flush=True)
        subprocess.run(["dotnet", "--info"], stdout=log, check=True)
        command = {"linux": ["lscpu"], "darwin": ["sysctl", "machdep.cpu", "hw"],
                   "win32": ["powershell", "-NoProfile", "-Command", "Get-CimInstance Win32_Processor | Format-List *"]}
        subprocess.run(command[sys.platform], stdout=log, stderr=subprocess.STDOUT, check=False)


def probes(repo, out, core_dll):
    project = out / "probe-project"
    project.mkdir()
    shutil.copyfile(repo / ".github/scripts/ci57-r4-memory-probe.cs", project / "Program.cs")
    (project / "Probe.csproj").write_text("""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<Nullable>disable</Nullable><ServerGarbageCollection>true</ServerGarbageCollection></PropertyGroup><ItemGroup>
<Reference Include="LuminPack.Core" HintPath="$(CI57_PROBE_CORE)" /></ItemGroup></Project>""", encoding="utf-8")
    env = dict(os.environ, CI57_PROBE_CORE=str(core_dll), CI57_CPU=cpu_name())
    run(["dotnet", "build", project / "Probe.csproj", "-c", "Release"], env=env)
    dll = project / OUTPUT / "Probe.dll"
    for name, args, extra in [("copy", ["copy"], {}), ("alloc-server", ["alloc"], {"DOTNET_gcServer": "1"}),
                              ("alloc-workstation", ["alloc"], {"DOTNET_gcServer": "0"})]:
        with (out / f"probe-{name}.jsonl").open("w", encoding="utf-8") as log:
            run(["dotnet", dll, *args], env=dict(env, **extra), stdout=log)


def prepare_matrix(repo):
    source = repo / MATRIX / "Program.cs"
    text = source.read_text(encoding="utf-8-sig")
    text = replace_once(text, "            : Fixtures.CollectionCases();", """            : Fixtures.CollectionCases();
        if (Environment.GetEnvironmentVariable("MATRIX_CASES") is { Length: > 0 } filter)
        {
            var names = filter.Split(',');
            fixtures = fixtures.Where(fixture => names.Contains(fixture.Name)).ToArray();
            if (fixtures.Length != names.Length) throw new InvalidOperationException("Missing filtered fixture.");
        }""", "matrix filter")
    source.write_text(text, encoding="utf-8")


def variants(repo, out):
    prepare_matrix(repo)
    run(["dotnet", "build", MATRIX, *FLAGS], cwd=repo)
    stock_bin = repo / MATRIX / OUTPUT
    copy_original = (repo / COPY_FILE).read_text(encoding="utf-8-sig")
    alloc_original = (repo / ALLOC_FILE).read_text(encoding="utf-8-sig")
    roots = {}
    try:
        for name in VARIANTS:
            copy_text, alloc_text = variant_sources(name, copy_original, alloc_original)
            (repo / COPY_FILE).write_text(copy_text, encoding="utf-8")
            (repo / ALLOC_FILE).write_text(alloc_text, encoding="utf-8")
            core_out = out / "variant-core" / name
            run(["dotnet", "build", CORE, *FLAGS, "-o", core_out], cwd=repo)
            root = out / "variant-bin" / name
            shutil.copytree(stock_bin, root)
            shutil.copyfile(core_out / "LuminPack.Core.dll", root / "LuminPack.Core.dll")
            roots[name] = root
    finally:
        (repo / COPY_FILE).write_text(copy_original, encoding="utf-8")
        (repo / ALLOC_FILE).write_text(alloc_original, encoding="utf-8")

    results = out / "variants"
    results.mkdir()
    order = list(VARIANTS)
    for process in range(PROCESSES):
        for scene in SCENES:
            cases = MAIN_CASES if scene == "main" else COLLECTION_CASES
            rotation = order[process % len(order):] + order[:process % len(order)]
            if (process + SCENES.index(scene)) % 2:
                rotation.reverse()
            for name in rotation:
                target = results / f"{name}-{scene}-{process + 1}.jsonl"
                run(["dotnet", roots[name] / "LuminPack.MatrixBenchmark.dll", "binary", scene, ROUNDS, target],
                    env=dict(os.environ, MATRIX_CASES=",".join(cases)))


def main():
    repo, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
    out.mkdir(parents=True, exist_ok=True)
    host_info(out)
    run(["dotnet", "build", CORE, *FLAGS], cwd=repo)
    probes(repo, out, repo / CORE / OUTPUT / "LuminPack.Core.dll")
    variants(repo, out)
    (out / "done.json").write_text(json.dumps({"variants": VARIANTS, "processes": PROCESSES}), encoding="utf-8")


if __name__ == "__main__":
    main()
