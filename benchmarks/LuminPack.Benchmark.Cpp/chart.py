"""Render native benchmark CSVs in the main README's light and dark chart style."""

import argparse
import csv
import math
import re
import runpy
import statistics
from collections import defaultdict
from html import escape
from pathlib import Path


README_CHARTS = runpy.run_path(str(Path(__file__).resolve().parents[1] / "make-charts.py"))
RENDER = README_CHARTS["render"]
PALETTE = README_CHARTS["COLORS"]
FORMATS = {"binary": "Binary", "json-utf8": "UTF-8 JSON", "json-utf16": "UTF-16 JSON"}
LIBRARIES = {
    "luminpack": ("LuminPack", "LuminPack"), "struct_pack": ("struct_pack", "MemoryPack"),
    "bitsery": ("Bitsery", "Nino"), "msgpack-cxx": ("MessagePack-C++", "MessagePack"),
    "cereal": ("cereal", "RE:Dox"), "rapidjson": ("RapidJSON", "System.Text.Json"),
    "simdjson": ("simdjson", "MemoryPack"), "jsoncons": ("jsoncons", "MessagePack"),
    "nlohmann-json": ("nlohmann/json", "Newtonsoft.Json"),
}
COLORS = {name: PALETTE[color] for name, color in LIBRARIES.values()}


def load_runs(arguments):
    datasets = defaultdict(list)
    for argument in arguments:
        compiler, separator, filename = argument.partition("=")
        if not separator or not compiler.strip() or not filename:
            raise ValueError("Use the input form TOOLCHAIN=CSV.")
        with Path(filename).open(encoding="utf-8-sig", newline="") as source:
            rows = list(csv.DictReader(source))
        run = {(row["format"], row["model"], row["direction"], row["library"]): row for row in rows}
        if not run or len(run) != len(rows):
            raise ValueError(f"Expected unique benchmark rows in {filename}.")
        if datasets[compiler] and datasets[compiler][0].keys() != run.keys():
            raise ValueError(f"Runs for {compiler} must contain the same benchmark groups.")
        datasets[compiler].append(run)
    return datasets


def summarize(runs):
    result = {}
    for key in runs[0]:
        samples = [run[key] for run in runs]
        times = [float(row["median_ns"]) for row in samples]
        reference = (*key[:3], "luminpack")
        luminpack_times = [float(run[reference]["median_ns"]) for run in runs]
        if any(value <= 0 or not math.isfinite(value) for value in times + luminpack_times):
            raise ValueError(f"Invalid benchmark samples for {key}.")
        ratios = [time / luminpack_time for time, luminpack_time in zip(times, luminpack_times)]
        result[key] = dict(median_ns=statistics.median(times), ratio_to_luminpack=statistics.median(ratios),
                           ratio_min=min(ratios), ratio_max=max(ratios),
                           cv_max=max(float(row["cv_percent"]) for row in samples),
                           wire_bytes=int(samples[0]["wire_bytes"]),
                           allocations=statistics.median(float(row["allocations"]) for row in samples),
                           allocated_bytes=statistics.median(float(row["allocated_bytes"]) for row in samples),
                           runs=len(runs))
    return result


def write_charts(output, compiler, current):
    charts = []
    for fmt, model in dict.fromkeys(key[:2] for key in current):
        panels, values = [], {}
        for direction, label in (("write", "Serialize"), ("read", "Deserialize")):
            entries = []
            for library, (name, _) in LIBRARIES.items():
                method = (fmt, model, direction, library)
                if method not in current:
                    continue
                summary = current[method]
                entries.append((name, method))
                values[method] = dict(mean=summary["median_ns"],
                                      ratio=None if library == "luminpack" else summary["ratio_to_luminpack"])
            if entries:
                panels.append((label, entries))
        title = f"{compiler} · {model} · {FORMATS[fmt]}"
        file = re.sub(r"[^a-z0-9-]+", "-", f"{compiler}-{model}-{fmt}".lower())
        for theme in ("light", "dark"):
            svg = RENDER(title, panels, values, theme, colors=COLORS,
                         subtitle="median time per operation · lower is better",
                         footer="Ratios are relative to LuminPack in the same run. Bars show the median of run medians.")
            (output / f"{file}-{theme}.svg").write_text(svg, encoding="utf-8")
        charts.append(f'<picture><source media="(prefers-color-scheme: dark)" srcset="{file}-dark.svg">'
                      f'<img src="{file}-light.svg" alt="{escape(title, quote=True)}"></picture>')
    return charts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--current", metavar="TOOLCHAIN=CSV", action="append", required=True)
    options = parser.parse_args()
    current = load_runs(options.current)
    options.output.mkdir(parents=True, exist_ok=True)
    groups, sections, links = [], [], []
    for index, (compiler, runs) in enumerate(current.items()):
        summary = summarize(runs)
        anchor = f"toolchain-{index}"
        links.append(f'<a href="#{anchor}">{escape(compiler)}</a>')
        charts = write_charts(options.output, compiler, summary)
        sections.append(f'<section><h2 id="{anchor}">{escape(compiler)}</h2>' + "\n".join(charts) + "</section>")
        for (fmt, model, direction, library), metrics in summary.items():
            groups.append(dict(toolchain=compiler, format=fmt, model=model, direction=direction, library=library, **metrics))
    with (options.output / "summary.csv").open("w", newline="", encoding="utf-8") as target:
        writer = csv.DictWriter(target, fieldnames=groups[0])
        writer.writeheader()
        writer.writerows(groups)
    (options.output / "index.html").write_text(
        '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">'
        '<title>LuminPack C++ benchmarks</title><style>:root{color-scheme:light dark}'
        'body{max-width:950px;margin:2rem auto;padding:0 1rem;font:16px system-ui}'
        'img{width:100%;display:block;margin:1rem 0}</style><h1>LuminPack C++ benchmarks</h1>'
        '<p><a href="summary.csv">Timing, dispersion, wire sizes and allocations</a></p>'
        '<nav aria-label="Toolchains">' + " · ".join(links) + "</nav>"
        + "\n".join(sections) + "</html>", encoding="utf-8")


if __name__ == "__main__":
    main()
