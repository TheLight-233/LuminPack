"""Run the manual CI 57 hardware probes and the isolated, gated sorted-set comparison."""
import csv
import json
import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path
from statistics import median

CASES = {
    "main": ("Reference_SortedSetString",),
    **{scene: ("SortedSet_String", "ImmutableSortedSet_String", "SortedSet_Object")
       for scene in ("small", "regular", "changing", "large")},
}
MATRIX = Path("benchmarks/LuminPack.MatrixBenchmark")
FLAGS = ["-c", "Release", "-f", "net10.0", "-p:LuminPackLegacyGeneratorReference=false",
         "-p:LuminPackUseSatori=false", "-p:GeneratePackageOnBuild=false"]
OUTPUT = Path("bin/Release/net10.0")


def prepare(repo):
    source = repo / MATRIX / "Program.cs"
    text = source.read_text(encoding="utf-8-sig")
    changes = {
        "            : Fixtures.CollectionCases();": """            : Fixtures.CollectionCases();
        var names = Environment.GetEnvironmentVariable("MATRIX_CASES")!.Split(',');
        fixtures = fixtures.Where(fixture => names.Contains(fixture.Name)).ToArray();
        if (fixtures.Length != names.Length) throw new InvalidOperationException("Missing filtered fixture.");""",
        '                    Add("write", codec.Write);': "",
    }
    for target, replacement in changes.items():
        if text.count(target) != 1:
            raise ValueError(f"Unexpected fixture selector in {source}")
        text = text.replace(target, replacement)
    source.write_text(text, encoding="utf-8")


def probes(repo, out, core, include_allocation):
    modes = [("copy", ["--medium"])]
    if include_allocation:
        modes.extend([("allocation", []), ("linked-list", [])])
    for name, args in modes:
        directory = out / (name + "-project")
        directory.mkdir()
        shutil.copyfile(repo / ".github/scripts" / f"ci57-{name}-probe.cs", directory / "Program.cs")
        project = directory / "Probe.csproj"
        project.write_text("""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<ServerGarbageCollection>true</ServerGarbageCollection></PropertyGroup><ItemGroup>
<Reference Include="LuminPack.Core" HintPath="$(CI57_PROBE_CORE)" /></ItemGroup></Project>""", encoding="utf-8")
        env = dict(os.environ, CI57_PROBE_CORE=str(core))
        subprocess.run(["dotnet", "build", project, "-c", "Release"], cwd=repo, env=env, check=True)
        with (out / f"{name}.jsonl").open("w") as log:
            subprocess.run(["dotnet", directory / OUTPUT / "Probe.dll", *args], env=env, stdout=log, check=True)


def paired(stock, gated, out):
    samples = {}
    for pair in range(5):
        for scene, cases in CASES.items():
            variants = [("stock", stock), ("gated", gated)]
            for name, repo in variants[::1 if pair % 2 == 0 else -1]:
                runner = repo / MATRIX / OUTPUT / "LuminPack.MatrixBenchmark.dll"
                output = out / f"{name}-{scene}-{pair}.jsonl"
                subprocess.run(["dotnet", runner, "binary", scene, "9", output], check=True,
                               env=dict(os.environ, MATRIX_CASES=",".join(cases)))
                for row in map(json.loads, output.read_text(encoding="utf-8-sig").splitlines()):
                    if row["Kind"] == "Sample":
                        key = (scene, row["Case"], row["Operation"], name, row["Library"], pair)
                        samples.setdefault(key, []).append((row["Nanoseconds"], row["AllocatedBytes"]))
    with (out / "pairs.csv").open("w", newline="") as fp:
        writer = csv.writer(fp)
        writer.writerow(["scene", "case", "operation", "pair", "stock_ns", "gated_ns", "gated_over_stock",
                         "stock_over_nino", "gated_over_nino", "stock_over_memorypack", "gated_over_memorypack",
                         "stock_B", "gated_B"])
        for scene, cases in CASES.items():
            for case in cases:
                for op in ("fresh", "reuse"):
                    for pair in range(5):
                        def sample(variant, library):
                            return samples.get((scene, case, op, variant, library, pair), [])
                        def floor(variant, library):
                            values = sample(variant, library)
                            if library == "LuminPack" and len(values) != 9:
                                raise ValueError((scene, case, op, variant, pair))
                            return min((ns for ns, _ in values), default=float("nan"))
                        a, b = (floor(v, "LuminPack") for v in ("stock", "gated"))
                        writer.writerow([scene, case, op, pair, a, b, b / a,
                                         *[floor(v, "LuminPack") / floor(v, ref)
                                           for ref in ("Nino", "MemoryPack") for v in ("stock", "gated")],
                                         *[median(n for _, n in sample(v, "LuminPack")) for v in ("stock", "gated")]])


def main():
    stock, gated, out = (Path(arg).resolve() for arg in sys.argv[1:])
    out.mkdir(parents=True, exist_ok=True)
    with (out / "host.txt").open("w") as log:
        print(platform.platform(), platform.machine(), file=log, flush=True)
        subprocess.run(["dotnet", "--info"], stdout=log, check=True)
        subprocess.run(["lscpu"] if sys.platform == "linux" else ["sysctl", "-n", "machdep.cpu.brand_string"], stdout=log, check=True)
    target = sys.platform == "linux" and platform.machine().lower() in ("x86_64", "amd64")
    if not target:
        core = stock / "src/LuminPack.Core"
        subprocess.run(["dotnet", "build", core, *FLAGS], cwd=stock, check=True)
        probes(stock, out, core / OUTPUT / "LuminPack.Core.dll", include_allocation=False)
        return
    subprocess.run(["git", "apply", stock / ".github/scripts/ci57-sorted-set-gated.patch"], cwd=gated, check=True)
    for repo in (stock, gated):
        prepare(repo)
        subprocess.run(["dotnet", "build", MATRIX, *FLAGS], cwd=repo, check=True)
    probes(stock, out, stock / MATRIX / OUTPUT / "LuminPack.Core.dll", include_allocation=True)
    paired(stock, gated, out)


if __name__ == "__main__":
    main()
