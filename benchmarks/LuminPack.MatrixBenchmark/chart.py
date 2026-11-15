"""Renders the serialization matrix chart (light and dark SVG) from a matrix artifact directory.

Usage: python benchmarks/LuminPack.MatrixBenchmark/chart.py <matrix artifact directory> <output directory>

Reads the '<format>-<scenario>-<process>.jsonl' files written by the matrix benchmark and writes
matrix-light.svg, matrix-dark.svg and matrix-summary.json. Each pair ratio is the arithmetic mean of
the same-process ratios of the per-process mean times; groups average their pairs with equal weight.
The chart shows every group mean and the distribution of every pair ratio.
"""
import json
import math
import re
import statistics
import sys
from collections import defaultdict
from pathlib import Path
from xml.sax.saxutils import escape

sys.path.insert(0, str(Path(__file__).resolve().parent))
from report import load_process  # noqa: E402

SCENARIOS = ["main", "small", "regular", "large", "changing"]
OPERATIONS = ["write", "fresh", "reuse"]
ROWS = [("binary", "MemoryPack"), ("binary", "Nino"), ("json", "System.Text.Json")]
LABELS = {"write": "Write", "fresh": "Fresh", "reuse": "Reuse", "all": "All"}

THEMES = {
    "light": {"bg": "#FFFFFF", "border": "#D0D7DE", "text": "#1F2328", "muted": "#59636E", "grid": "#EAEEF2",
              "fast": (0x2A, 0x78, 0xD6), "slow": (0xE3, 0x49, 0x48), "mid": (0xF0, 0xEF, 0xEC), "empty": "#F6F8FA"},
    "dark": {"bg": "#0D1117", "border": "#30363D", "text": "#E6EDF3", "muted": "#9198A1", "grid": "#21262D",
             "fast": (0x39, 0x87, 0xE5), "slow": (0xE6, 0x67, 0x67), "mid": (0x38, 0x38, 0x35), "empty": "#161B22"},
}
FONT = "-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif"


def pair_ratios(directory):
    """Returns {(format, scenario, reference, operation): [ratio per pair]} and the run environment."""
    processes = defaultdict(list)
    for path in sorted(directory.glob("*.jsonl")):
        environment, timing, _, _ = load_process(path)
        processes[environment["Format"], environment["Scene"]].append((environment, timing))
    groups = defaultdict(list)
    environment = None
    for (fmt, scene), runs in processes.items():
        environment = environment or runs[0][0]
        if not runs[0][0]["Rounds"]:
            raise SystemExit(f"{fmt}/{scene} contains validation results only; a timed run is required")
        keys = set(runs[0][1])
        for case, operation, library in sorted(keys):
            if library != "LuminPack":
                continue
            for reference in runs[0][0]["References"]:
                if (case, operation, reference) not in keys:
                    continue
                ratios = [statistics.mean(r["Nanoseconds"] for r in timing[case, operation, library]) /
                          statistics.mean(r["Nanoseconds"] for r in timing[case, operation, reference])
                          for _, timing in runs]
                groups[fmt, scene, reference, operation].append(statistics.mean(ratios))
    return groups, environment


def cpu_name(directory):
    """Reads the processor model from the lscpu output stored next to the samples."""
    path = directory / "cpu.txt"
    if path.exists():
        match = re.search(r"^Model name:\s*(.+)$", path.read_text(encoding="utf-8"), re.MULTILINE)
        if match:
            return match.group(1).strip()
    return None


def collect(groups, fmt, reference, scenarios, operations):
    """Concatenates the pair ratios of the selected scenarios and operations."""
    return [value for scene in scenarios for operation in operations
            for value in groups.get((fmt, scene, reference, operation), [])]


def cell_color(ratio, theme):
    """Diverging color on a log2 scale: blue below 1, red above 1, saturated at 4x either way."""
    t = THEMES[theme]
    position = max(-1.0, min(1.0, math.log2(ratio) / 2.0))
    pole = t["slow"] if position > 0 else t["fast"]
    weight = abs(position) ** 0.8
    rgb = tuple(round(m + (p - m) * weight) for m, p in zip(t["mid"], pole))
    return "#%02X%02X%02X" % rgb, rgb


def ink(rgb, theme):
    """Chooses dark or light text for a filled cell."""
    luminance = sum(c * w for c, w in zip(rgb, (0.2126, 0.7152, 0.0722))) / 255
    return "#FFFFFF" if luminance < 0.55 else "#1F2328"


def render(groups, caption, theme):
    """Draws the summary heatmap and the per-pair distributions."""
    t = THEMES[theme]
    columns = SCENARIOS + ["all"]
    row_specs = []
    for fmt, reference in ROWS:
        operations = [op for op in OPERATIONS if collect(groups, fmt, reference, SCENARIOS, [op])] + ["all"]
        row_specs.append((fmt, reference, operations))

    width, left, cell_w, cell_h, gap = 1000, 196, 124, 40, 3
    out = []
    y = 34
    out.append(f'<text x="24" y="{y}" font-size="19" font-weight="600" fill="{t["text"]}">Serialization matrix</text>')
    out.append(f'<text x="{width - 24}" y="{y}" font-size="12" text-anchor="end" fill="{t["muted"]}">'
               'LuminPack time ÷ reference time · lower is better</text>')
    y += 22
    out.append(f'<text x="24" y="{y}" font-size="12" fill="{t["muted"]}">{escape(caption)}</text>')

    # Heatmap header.
    y += 34
    for index, column in enumerate(columns):
        x = left + index * (cell_w + gap) + cell_w / 2
        label = "All scenarios" if column == "all" else column.capitalize()
        out.append(f'<text x="{x:.1f}" y="{y}" font-size="13" font-weight="600" text-anchor="middle" fill="{t["text"]}">{label}</text>')
    y += 12
    for fmt, reference, operations in row_specs:
        y += 10
        out.append(f'<text x="24" y="{y + 4}" font-size="13" font-weight="700" fill="{t["text"]}">{escape(reference)}'
                   f'<tspan font-weight="400" fill="{t["muted"]}">  {fmt}</tspan></text>')
        y += 12
        for operation in operations:
            ops = OPERATIONS if operation == "all" else [operation]
            weight = "700" if operation == "all" else "400"
            out.append(f'<text x="44" y="{y + cell_h * 0.62:.1f}" font-size="13" font-weight="{weight}" fill="{t["text"]}">{LABELS[operation]}</text>')
            for index, column in enumerate(columns):
                x = left + index * (cell_w + gap)
                values = collect(groups, fmt, reference, SCENARIOS if column == "all" else [column], ops)
                if not values:
                    out.append(f'<rect x="{x}" y="{y}" width="{cell_w}" height="{cell_h}" rx="4" fill="{t["empty"]}"/>')
                    out.append(f'<text x="{x + cell_w / 2:.1f}" y="{y + cell_h * 0.62:.1f}" font-size="13" text-anchor="middle" fill="{t["muted"]}">—</text>')
                    continue
                mean = statistics.mean(values)
                fill, rgb = cell_color(mean, theme)
                color = ink(rgb, theme)
                out.append(f'<rect x="{x}" y="{y}" width="{cell_w}" height="{cell_h}" rx="4" fill="{fill}"/>')
                out.append(f'<text x="{x + 12}" y="{y + cell_h * 0.62:.1f}" font-size="14" font-weight="{weight}" fill="{color}">{mean:.3f}</text>')
                out.append(f'<text x="{x + cell_w - 10}" y="{y + cell_h * 0.62:.1f}" font-size="10.5" text-anchor="end" fill="{color}" fill-opacity="0.8">n={len(values):,}</text>')
            y += cell_h + gap

    # Legend for the diverging scale.
    y += 16
    legend_x, legend_w = left, 6 * (cell_w + gap) - gap
    steps = 60
    out.append(f'<text x="24" y="{y + 11}" font-size="12" fill="{t["muted"]}">Color scale</text>')
    for step in range(steps):
        ratio = 2 ** (-2 + 4 * (step + 0.5) / steps)
        fill, _ = cell_color(ratio, theme)
        out.append(f'<rect x="{legend_x + step * legend_w / steps:.1f}" y="{y}" width="{legend_w / steps + 0.4:.1f}" height="12" fill="{fill}"/>')
    for ratio, text in ((0.25, "≤ 0.25 (4× faster)"), (0.5, "0.5"), (1.0, "1.0 (parity)"), (2.0, "2.0"), (4.0, "≥ 4 (4× slower)")):
        x = legend_x + (math.log2(ratio) + 2) / 4 * legend_w
        anchor = "start" if ratio == 0.25 else "end" if ratio == 4.0 else "middle"
        out.append(f'<text x="{x:.1f}" y="{y + 28}" font-size="11" text-anchor="{anchor}" fill="{t["muted"]}">{text}</text>')

    # Distribution of every pair ratio.
    y += 62
    out.append(f'<text x="24" y="{y}" font-size="16" font-weight="600" fill="{t["text"]}">Every paired operation</text>')
    out.append(f'<text x="{width - 24}" y="{y}" font-size="12" text-anchor="end" fill="{t["muted"]}">'
               'histogram of pair ratios, all scenarios · log scale · ◆ arithmetic mean</text>')
    all_values = [v for values in groups.values() for v in values]
    low = 2 ** math.floor(math.log2(min(all_values)))
    high = 2 ** math.ceil(math.log2(max(all_values)))
    axis_x, axis_w = left, width - left - 150
    position = lambda r: axis_x + (math.log2(r) - math.log2(low)) / (math.log2(high) - math.log2(low)) * axis_w
    bins = 64
    edges = [low * (high / low) ** (i / bins) for i in range(bins + 1)]
    strip_h = 30
    y += 18
    top = y
    for fmt, reference, operations in row_specs:
        for operation in operations:
            if operation == "all":
                continue
            values = collect(groups, fmt, reference, SCENARIOS, [operation])
            counts = [0] * bins
            for value in values:
                index = min(bins - 1, max(0, int(math.log(value / low) / math.log(high / low) * bins)))
                counts[index] += 1
            peak = max(counts)
            out.append(f'<text x="24" y="{y + strip_h * 0.66:.1f}" font-size="13" font-weight="600" fill="{t["text"]}">{escape(reference)}'
                       f'<tspan font-weight="400" fill="{t["muted"]}">  {LABELS[operation]}</tspan></text>')
            out.append(f'<rect x="{axis_x}" y="{y}" width="{axis_w}" height="{strip_h}" rx="3" fill="{t["grid"]}" fill-opacity="0.5"/>')
            for index, count in enumerate(counts):
                if not count:
                    continue
                x0, x1 = position(edges[index]), position(edges[index + 1])
                h = max(1.5, (strip_h - 4) * math.sqrt(count / peak))
                center = math.sqrt(edges[index] * edges[index + 1])
                fill = "#%02X%02X%02X" % (t["fast"] if center < 1 else t["slow"])
                out.append(f'<rect x="{x0 + 0.5:.1f}" y="{y + strip_h - h:.1f}" width="{max(1.0, x1 - x0 - 1):.1f}" height="{h:.1f}" rx="1" fill="{fill}"/>')
            mean = statistics.mean(values)
            mx = position(mean)
            out.append(f'<path d="M{mx:.1f} {y - 4} l5 5 l-5 5 l-5 -5 z" fill="{t["text"]}" stroke="{t["bg"]}" stroke-width="1.5"/>')
            faster = sum(v < 1 for v in values)
            out.append(f'<text x="{axis_x + axis_w + 12}" y="{y + strip_h * 0.45:.1f}" font-size="12" fill="{t["text"]}">{faster:,} / {len(values):,} faster</text>')
            out.append(f'<text x="{axis_x + axis_w + 12}" y="{y + strip_h * 0.45 + 14:.1f}" font-size="11" fill="{t["muted"]}">mean {mean:.3f}</text>')
            y += strip_h + 8
    parity = position(1.0)
    out.append(f'<line x1="{parity:.1f}" y1="{top - 6}" x2="{parity:.1f}" y2="{y - 4}" stroke="{t["text"]}" stroke-width="1.2" stroke-dasharray="4 3"/>')
    exponent = math.log2(low)
    while 2 ** exponent <= high:
        ratio = 2 ** exponent
        x = position(ratio)
        out.append(f'<line x1="{x:.1f}" y1="{y - 6}" x2="{x:.1f}" y2="{y - 1}" stroke="{t["muted"]}"/>')
        text = f"{ratio:g}" if ratio >= 0.125 else f"1/{round(1 / ratio)}"
        out.append(f'<text x="{x:.1f}" y="{y + 12}" font-size="11" text-anchor="middle" fill="{t["muted"]}">{text}</text>')
        exponent += 1
    y += 34
    for line in ("Below 1 LuminPack is faster. Every ratio compares two libraries in the same process, fixture and operation; "
                 "groups weight their pairs equally.",
                 "Pairs exclude reference fixtures that fail round-trip validation. System.Text.Json has no root reuse "
                 "operation, so its Reuse row is absent."):
        out.append(f'<text x="24" y="{y}" font-size="11" fill="{t["muted"]}">{escape(line)}</text>')
        y += 16
    height = y + 8
    head = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" font-family="{FONT}">',
            f'<rect x="0.5" y="0.5" width="{width - 1}" height="{height - 1}" rx="10" fill="{t["bg"]}" stroke="{t["border"]}"/>']
    return "\n".join(head + out + ["</svg>"])


def main():
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    source, target = Path(sys.argv[1]), Path(sys.argv[2])
    groups, environment = pair_ratios(source)
    cpu = cpu_name(source) or "unknown CPU"
    caption = (f"{cpu} · {environment['ProcessorCount']} logical CPUs · {environment['OS']} · {environment['Runtime']} "
               f"{environment['Architecture']} · {'Server GC' if environment['ServerGC'] else 'Workstation GC'} · "
               f"3 processes × {environment['Rounds']} rounds")
    target.mkdir(parents=True, exist_ok=True)
    for theme in THEMES:
        (target / f"matrix-{theme}.svg").write_text(render(groups, caption, theme), encoding="utf-8")
    summary = {"environment": environment, "cpu": cpu, "groups": []}
    for fmt, reference in ROWS:
        for column in SCENARIOS + ["all"]:
            for operation in OPERATIONS + ["all"]:
                values = collect(groups, fmt, reference, SCENARIOS if column == "all" else [column],
                                 OPERATIONS if operation == "all" else [operation])
                if values:
                    summary["groups"].append({"format": fmt, "scenario": column, "reference": reference,
                                              "operation": operation, "pairs": len(values),
                                              "arithmetic_mean_ratio": round(statistics.mean(values), 4),
                                              "faster_pairs": sum(v < 1 for v in values)})
    (target / "matrix-summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("wrote matrix chart with", sum(len(v) for v in groups.values()), "pairs")


if __name__ == "__main__":
    main()
