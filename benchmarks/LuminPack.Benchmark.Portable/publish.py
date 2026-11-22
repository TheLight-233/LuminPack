"""Install the chart artifact from the portable benchmark workflow in both READMEs."""

import argparse
import csv
import json
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
LANGUAGES = {"cpp": "C++", "java": "Java", "go": "Go", "typescript": "TypeScript"}
START = "<!-- portable-benchmark-charts:start -->"
END = "<!-- portable-benchmark-charts:end -->"
ASSETS = Path("docs/assets/benchmarks/portable")


def chart_section(path, source, commit, chinese):
    intro = (f"\u6570\u636e\u6765\u81ea [CI \u8fd0\u884c]({source})\uff08\u63d0\u4ea4 `{commit}`\uff09\u3002\u8017\u65f6\u8d8a\u4f4e\u8d8a\u597d\u3002" if chinese else
             f"Measured in [CI]({source}) (commit `{commit}`). Lower time is better.")
    rows = [intro, ""]
    for key, label in LANGUAGES.items():
        alt = f"{label} \u5e8f\u5217\u5316\u57fa\u51c6" if chinese else f"{label} serialization benchmarks"
        rows += [f"#### {label}", "", "<picture>",
                 f'  <source media="(prefers-color-scheme: dark)" srcset="{path}/{key}-dark.svg">',
                 f'  <img alt="{alt}" src="{path}/{key}-light.svg" width="100%">', "</picture>", ""]
    return "\n".join(rows).rstrip()


def publish(artifact):
    provenance = json.loads((artifact / "provenance.json").read_text(encoding="utf-8"))
    source, commit = provenance["source_url"], provenance["commit"]
    if (not re.fullmatch(r"https://github\.com/[\w.-]+/[\w.-]+/actions/runs/[1-9][0-9]*", source) or
            not re.fullmatch(r"[0-9a-f]{40}", commit)):
        raise ValueError("Expected chart provenance from a GitHub Actions benchmark run.")
    with (artifact / "summary.csv").open(encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if {row["language"] for row in rows} != LANGUAGES.keys() or any(row["source_url"] != source for row in rows):
        raise ValueError("The summary must identify all four languages and the same CI run.")

    images = [artifact / f"{language}-{theme}.svg" for language in LANGUAGES for theme in ("light", "dark")]
    for image in images:
        if ET.parse(image).getroot().tag != "{http://www.w3.org/2000/svg}svg":
            raise ValueError(f"Expected an SVG chart in {image}.")

    edited = {}
    for name, path, chinese in (("README.md", ASSETS, False), ("docs/README.cn.md", ASSETS.relative_to("docs"), True)):
        readme = ROOT / name
        text = readme.read_text(encoding="utf-8")
        if text.count(START) != 1 or text.count(END) != 1 or text.index(START) >= text.index(END):
            raise ValueError(f"Missing the portable chart section in {readme}.")
        before, rest = text.split(START, 1)
        _, after = rest.split(END, 1)
        section = chart_section(path.as_posix(), source, commit[:12], chinese)
        edited[readme] = f"{before}{START}\n\n{section}\n\n{END}{after}"

    target = ROOT / ASSETS
    target.mkdir(parents=True, exist_ok=True)
    for file in [*images, artifact / "summary.csv", artifact / "provenance.json"]:
        shutil.copyfile(file, target / file.name)
    for readme, text in edited.items():
        readme.write_text(text, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifact", type=Path, help="Extracted portable-benchmark-charts CI artifact.")
    publish(parser.parse_args().artifact)
