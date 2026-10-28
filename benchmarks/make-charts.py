"""Renders README benchmark charts (light and dark SVG) from a BenchmarkDotNet JSON report.

Usage: python benchmarks/make-charts.py <artifacts directory> <output directory> [report-github.md files...]
Reads JSON or '*-report-github.md' reports and writes <suite>-light.svg, <suite>-dark.svg and summary.json.
Only LuminPack, MemoryPack, MessagePack and the JSON libraries listed in SUITES are drawn.
"""
import glob
import json
import os
import sys
from xml.sax.saxutils import escape

SUITES = {
    "SimpleBenchmark": ("Simple class", [
        ("Serialize", [("LuminPack", "LuminPackSerialize"), ("MemoryPack", "MemoryPackSerialize"), ("MessagePack", "MessagePackSerialize")]),
        ("Deserialize", [("LuminPack", "LuminPackDeserialize"), ("MemoryPack", "MemoryPackDeserialize"), ("MessagePack", "MessagePackDeserialize")]),
    ]),
    "SerializeBenchmark": ("Game save (nested collections)", [
        ("Serialize", [("LuminPack", "LuminPackSerialize"), ("MemoryPack", "MemoryPackSerialize"), ("MessagePack", "MessagePackSerialize")]),
        ("Deserialize", [("LuminPack", "LuminPackDeserialize"), ("MemoryPack", "MemoryPackDeserialize"), ("MessagePack", "MessagePackDeserialize")]),
    ]),
    "PolymorphismBenchmark": ("Polymorphism (interfaces and abstract classes)", [
        ("Serialize", [("LuminPack", "LuminPackSerialize"), ("MemoryPack", "MemoryPackSerialize"), ("MessagePack", "MessagePackSerialize")]),
        ("Deserialize", [("LuminPack", "LuminPackDeserialize"), ("MemoryPack", "MemoryPackDeserialize"), ("MessagePack", "MessagePackDeserialize")]),
    ]),
    "JsonBenchmark": ("JSON (game save)", [
        ("Serialize", [("LuminPack", "LuminPackSerializeToJson"), ("System.Text.Json", "SystemTextJsonSerialize"),
                       ("Newtonsoft.Json", "NewtonsoftJsonSerialize"), ("RE:Dox", "ReDoXJsonSerialize")]),
        ("Deserialize", [("LuminPack", "LuminPackDeserializeFromJson"), ("System.Text.Json", "SystemTextJsonDeserialize"),
                         ("Newtonsoft.Json", "NewtonsoftJsonDeserialize"), ("RE:Dox", "ReDoXJsonDeserialize")]),
    ]),
}

COLORS = {"LuminPack": "#8B5CF6", "MemoryPack": "#3B82F6", "MessagePack": "#F59E0B",
          "System.Text.Json": "#10B981", "Newtonsoft.Json": "#EF4444", "RE:Dox": "#64748B"}

THEMES = {
    "light": {"bg": "#FFFFFF", "border": "#D0D7DE", "text": "#1F2328", "muted": "#59636E", "grid": "#EAEEF2"},
    "dark": {"bg": "#0D1117", "border": "#30363D", "text": "#E6EDF3", "muted": "#9198A1", "grid": "#21262D"},
}


def format_time(ns):
    """Formats nanoseconds with a readable unit."""
    if ns >= 1_000_000:
        return f"{ns / 1_000_000:.2f} ms"
    if ns >= 1_000:
        return f"{ns / 1_000:.2f} μs"
    return f"{ns:.1f} ns"


def format_bytes(value):
    """Formats an allocation size."""
    if value is None:
        return "-"
    if value >= 1024 * 1024:
        return f"{value / 1048576:.2f} MB"
    if value >= 1024:
        return f"{value / 1024:.2f} KB"
    return f"{int(value)} B"


def render(title, panels, results, theme):
    """Draws one suite as side-by-side Serialize and Deserialize panels."""
    t = THEMES[theme]
    rows = max(len(entries) for _, entries in panels)
    panel_width, label_width, bar_height, gap = 440, 128, 26, 12
    top, width = 78, 40 + panel_width * len(panels) + 30 * (len(panels) - 1)
    height = top + rows * (bar_height + gap) + 46
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" '
           'font-family="-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif">',
           f'<rect x="0.5" y="0.5" width="{width - 1}" height="{height - 1}" rx="10" fill="{t["bg"]}" stroke="{t["border"]}"/>',
           f'<text x="20" y="34" font-size="18" font-weight="600" fill="{t["text"]}">{escape(title)}</text>',
           f'<text x="{width - 20}" y="34" font-size="12" text-anchor="end" fill="{t["muted"]}">mean time per operation · lower is better</text>']
    for index, (name, entries) in enumerate(panels):
        x0 = 20 + index * (panel_width + 30)
        values = [results[method]["mean"] for _, method in entries]
        baseline, peak = values[0], max(values)
        bar_space = panel_width - label_width - 96
        out.append(f'<text x="{x0}" y="{top - 12}" font-size="14" font-weight="600" fill="{t["text"]}">{name}</text>')
        for row, ((library, method), value) in enumerate(zip(entries, values)):
            y = top + row * (bar_height + gap)
            length = max(3.0, bar_space * value / peak)
            weight = "700" if library == "LuminPack" else "400"
            out.append(f'<text x="{x0}" y="{y + bar_height * 0.68:.1f}" font-size="13" font-weight="{weight}" fill="{t["text"]}">{escape(library)}</text>')
            out.append(f'<rect x="{x0 + label_width}" y="{y}" width="{bar_space}" height="{bar_height}" rx="5" fill="{t["grid"]}"/>')
            out.append(f'<rect x="{x0 + label_width}" y="{y}" width="{length:.1f}" height="{bar_height}" rx="5" fill="{COLORS[library]}"/>')
            ratio = value / baseline
            label = format_time(value) + ("" if library == "LuminPack" else f"  ({ratio:.2f}x)")
            out.append(f'<text x="{x0 + label_width + bar_space + 8}" y="{y + bar_height * 0.68:.1f}" font-size="12" font-weight="{weight}" fill="{t["text"]}">{escape(label)}</text>')
    out.append(f'<text x="20" y="{height - 16}" font-size="11" fill="{t["muted"]}">Ratios are relative to LuminPack in the same panel. BenchmarkDotNet, same machine and run.</text>')
    out.append('</svg>')
    return "\n".join(out)


def parse_quantity(text, units):
    """Parses a BenchmarkDotNet cell such as '2.029 μs' or '40008 B' into the base unit."""
    text = text.strip().replace(",", "")
    if text in ("", "-", "NA", "?"):
        return None
    number, _, unit = text.partition(" ")
    return float(number) * units.get(unit, 1.0)


def read_markdown(path, results):
    """Reads the measured (non-Dry) rows of a '*-report-github.md' file."""
    suite = os.path.basename(path).split("-report")[0].split(".")[-1]
    header = None
    with open(path, encoding="utf-8") as stream:
        for line in stream:
            if not line.startswith("|"):
                continue
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if header is None:
                header = cells
                continue
            if set(cells[0]) <= {"-", ":", " "} or not cells[0]:
                continue
            row = dict(zip(header, cells))
            if row.get("Job") == "Dry":
                continue
            results[suite + "." + row["Method"]] = {
                "mean": parse_quantity(row["Mean"], {"ns": 1.0, "μs": 1e3, "us": 1e3, "ms": 1e6, "s": 1e9}),
                "allocated": parse_quantity(row.get("Allocated", "-"), {"B": 1.0, "KB": 1024.0, "MB": 1048576.0}),
            }


def main():
    source, target = sys.argv[1], sys.argv[2]
    results, environment = {}, None
    # Markdown reports may be given as files or found in a directory; JSON reports are read from a directory.
    markdown = [path for path in sys.argv[3:]] or glob.glob(os.path.join(source, "**", "*-report-github.md"), recursive=True)
    for path in markdown:
        read_markdown(path, results)
    reports = [path for path in glob.glob(os.path.join(source, "**", "*.json"), recursive=True)] if not sys.argv[3:] else []
    if not results and not reports:
        raise SystemExit("No BenchmarkDotNet report found under " + source)
    for path in reports:
        with open(path, encoding="utf-8") as stream:
            report = json.load(stream)
        environment = environment or report.get("HostEnvironmentInfo")
        for benchmark in report.get("Benchmarks", []):
            statistics = benchmark.get("Statistics") or {}
            if "Mean" not in statistics:
                continue
            key = benchmark["Type"].split(".")[-1] + "." + benchmark["Method"]
            results[key] = {"mean": statistics["Mean"], "error": statistics.get("StandardError"),
                            "allocated": (benchmark.get("Memory") or {}).get("BytesAllocatedPerOperation")}
    os.makedirs(target, exist_ok=True)
    summary = {"environment": environment, "suites": {}}
    for suite, (title, panels) in SUITES.items():
        scoped = {method: results[suite + "." + method] for _, entries in panels for _, method in entries if suite + "." + method in results}
        if len(scoped) != sum(len(entries) for _, entries in panels):
            print("skipping incomplete suite", suite)
            continue
        for theme in THEMES:
            with open(os.path.join(target, f"{suite}-{theme}.svg"), "w", encoding="utf-8") as stream:
                stream.write(render(title, panels, scoped, theme))
        summary["suites"][suite] = {name: [{"library": library, "mean": format_time(scoped[method]["mean"]),
                                            "ratio": round(scoped[method]["mean"] / scoped[entries[0][1]]["mean"], 2),
                                            "allocated": format_bytes(scoped[method]["allocated"])}
                                           for library, method in entries] for name, entries in panels}
    with open(os.path.join(target, "summary.json"), "w", encoding="utf-8") as stream:
        json.dump(summary, stream, indent=2, ensure_ascii=False)
    print("wrote charts for", ", ".join(summary["suites"]))


if __name__ == "__main__":
    main()
