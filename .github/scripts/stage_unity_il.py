"""Install the shared static-call rewriter into an isolated Unity validation project."""

import argparse
import json
from pathlib import Path
import shutil


def stage(project: Path) -> None:
    """Copy build-only sources and enable their matching generator contract."""
    repository = Path(__file__).resolve().parents[2]
    source = repository / "src/LuminPack.ILPostProcessor"
    destination = project / "Assets/LuminPack.CodeGen"
    destination.mkdir(parents=True, exist_ok=True)
    for directory in (source / "Core", source / "Unity"):
        for path in directory.iterdir():
            if path.suffix in (".cs", ".asmdef"):
                shutil.copy2(path, destination / path.name)
    response = project / "Assets/csc.rsp"
    contents = response.read_text(encoding="utf-8-sig") if response.exists() else "-unsafe\n"
    if "LUMINPACK_IL_REWRITE" not in contents:
        response.write_text(contents.rstrip() + "\n-define:LUMINPACK_IL_REWRITE\n", encoding="utf-8")
    manifest = project / "Packages/manifest.json"
    data = json.loads(manifest.read_text(encoding="utf-8-sig"))
    data.setdefault("dependencies", {}).setdefault("com.unity.nuget.mono-cecil", "1.11.4")
    manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("project", type=Path)
    stage(parser.parse_args().project.resolve())
