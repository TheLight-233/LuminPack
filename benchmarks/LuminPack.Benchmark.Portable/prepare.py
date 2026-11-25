"""Export the same managed benchmark models for all four language runners."""

import argparse
from pathlib import Path
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[2]
CODEGEN = ROOT / "tools/LuminPack.Codegen/LuminPack.Codegen.csproj"
FIXTURES = ROOT / "tests/LuminPack.Tests.Portable/LuminPack.Tests.Portable.csproj"
MODELS = {"simple": "LuminPackBenchmark.SimpleClass", "serialize": "LuminPackBenchmark.CharacterSaveData"}


def run(*command, capture=False):
    return subprocess.run(command, cwd=ROOT, check=True, capture_output=capture, text=True)


def build(project, artifacts, export=False):
    settings = [f"-p:ArtifactsPath={artifacts}", "-p:LuminPackUseSatori=false"]
    if export:
        settings.append("-p:LuminPackExportContract=true")
    run("dotnet", "build", project, "-c", "Release", *settings)
    result = run("dotnet", "msbuild", project, "-getProperty:TargetPath", "-p:Configuration=Release", *settings,
                 capture=True)
    return Path(result.stdout.strip())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("models", type=Path, help="Destination for the shared fixtures and projected contracts.")
    parser.add_argument("--generated", action="store_true", help="Also generate Go and TypeScript benchmark modules.")
    parser.add_argument("--cpp", action="store_true", help="Also generate C++ benchmark headers.")
    parser.add_argument("--codegen-dll", type=Path, help="Reuse a compiled LuminPack.Codegen assembly.")
    parser.add_argument("--managed-dll", type=Path, help="Reuse a compiled fixture producer with contract export enabled.")
    args = parser.parse_args()
    models = args.models.resolve()
    models.mkdir(parents=True, exist_ok=True)
    builds = models.with_name(models.name + "-build")
    codegen = args.codegen_dll.resolve() if args.codegen_dll else build(CODEGEN, builds / "codegen")
    producer = args.managed_dll.resolve() if args.managed_dll else build(FIXTURES, builds / "producer", export=True)
    run("dotnet", producer, "write", models)
    manifest = models / "manifest.json"
    run("dotnet", codegen, "export", producer.with_name("LuminPack.Benchmark.dll"), manifest)
    for name, root in MODELS.items():
        run("dotnet", codegen, "project", manifest, root, models / f"{name}.manifest.json")
        if args.generated:
            go = models / "go"
            ts = models / "typescript"
            go.mkdir(exist_ok=True)
            ts.mkdir(exist_ok=True)
            run("dotnet", codegen, "generate", "--language", "go", manifest, root, go / f"{name}.go")
            run("dotnet", codegen, "generate", "--language", "typescript", "--runtime-import",
                "../../../src/LuminPack.TypeScript/dist/index.js", manifest, root, ts / f"{name}.ts")
        if args.cpp:
            run("dotnet", codegen, "generate", "--language", "cpp", manifest, root, models / f"{name}-benchmark.hpp")
            shutil.copyfile(models / f"{name}.utf16.json", models / f"{name}.json")


if __name__ == "__main__":
    main()
