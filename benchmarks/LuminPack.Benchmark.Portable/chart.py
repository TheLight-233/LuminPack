"""Render paired runtime benchmark samples in the LuminPack README chart style."""

import argparse
import csv
import itertools
import math
import runpy
import statistics
from collections import defaultdict
from dataclasses import dataclass
from html import escape
from pathlib import Path
from urllib.parse import urlsplit
from xml.etree import ElementTree as xml


README_CHARTS = runpy.run_path(str(Path(__file__).resolve().parents[1] / "make-charts.py"))
LANGUAGES = {"cpp": "C++", "java": "Java", "go": "Go", "typescript": "TypeScript", "rust": "Rust", "python": "Python"}
MODELS = {"simple": "SimpleClass", "serialize": "CharacterSaveData"}
FORMATS = {"binary": "Binary", "json": "UTF-8 JSON"}
OPERATIONS = {"encode": "Serialize", "decode": "Deserialize"}
REFERENCE = "LuminPack"
COLUMNS = ("language", "model", "format", "library", "operation", "sample", "ns_per_op", "bytes", "alloc_bytes_per_op")
SVG_NS = "http://www.w3.org/2000/svg"
FONT = "-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif"
xml.register_namespace("", SVG_NS)


@dataclass(frozen=True)
class Sample:
    ns: float
    size: int
    allocated: float | None


def read_samples(path):
    """Load one language and ensure each rendered cohort contains paired repeated samples."""
    groups = defaultdict(dict)
    language = None
    with path.open(encoding="utf-8-sig", newline="") as source:
        reader = csv.DictReader(source)
        if reader.fieldnames is None or set(reader.fieldnames) != set(COLUMNS) or len(reader.fieldnames) != len(COLUMNS):
            raise ValueError(f"Unexpected benchmark CSV header in {path}.")
        for row in reader:
            try:
                if None in row or any(value is None for value in row.values()):
                    raise ValueError("The row does not match the CSV header.")
                code = row["language"]
                model, fmt, operation, library = (row[key] for key in ("model", "format", "operation", "library"))
                if code not in LANGUAGES or language not in (None, code):
                    raise ValueError("A CSV must contain exactly one supported language.")
                if model not in MODELS or fmt not in FORMATS or operation not in OPERATIONS or not library.strip():
                    raise ValueError("Unknown model, format, or operation, or empty library name.")
                language = code
                sample = int(row["sample"])
                ns = float(row["ns_per_op"])
                size = int(row["bytes"])
                allocation = float(row["alloc_bytes_per_op"]) if row["alloc_bytes_per_op"] else None
                if sample < 1 or size < 0 or not math.isfinite(ns) or ns <= 0:
                    raise ValueError("Invalid sample number, time, or payload size.")
                if allocation is not None and (not math.isfinite(allocation) or allocation < 0):
                    raise ValueError("Invalid allocated bytes.")
                measurements = groups[model, fmt, operation, library]
                if sample in measurements:
                    raise ValueError("Duplicate sample number in a benchmark group.")
                measurements[sample] = Sample(ns, size, allocation)
            except (TypeError, ValueError) as error:
                raise ValueError(f"{path}:{reader.line_num}: {error}") from error

    if language is None:
        raise ValueError(f"No measured benchmark rows in {path}.")
    for model, fmt, operation in itertools.product(MODELS, FORMATS, OPERATIONS):
        cohort = (model, fmt, operation)
        reference = groups.get((*cohort, REFERENCE), {})
        peers = [samples for key, samples in groups.items() if key[:3] == cohort and key[3] != REFERENCE]
        if not peers or len(reference) < 3:
            raise ValueError(f"{path}: {model}/{fmt}/{operation} needs LuminPack and a peer with at least three samples.")
        if any(samples.keys() != reference.keys() for samples in peers):
            raise ValueError(f"{path}: {model}/{fmt}/{operation} is missing matching sample numbers.")
    return language, groups


def summarize(groups):
    """Summarize each cohort independently and compute LuminPack-to-library ratios by sample."""
    result = {}
    for key, samples in groups.items():
        reference = groups[(*key[:3], REFERENCE)]
        times = [sample.ns for sample in samples.values()]
        ratios = [reference[number].ns / sample.ns for number, sample in samples.items()]
        allocations = [sample.allocated for sample in samples.values() if sample.allocated is not None]
        sizes = {sample.size for sample in samples.values()}
        if len(sizes) != 1:
            raise ValueError(f"{key!r} used different payload sizes across samples.")
        if allocations and len(allocations) != len(samples):
            raise ValueError(f"{key!r} reports allocations for only part of the sample set.")
        result[key] = {
            "median_ns": statistics.median(times),
            "min_ns": min(times),
            "max_ns": max(times),
            "cv_percent": 100 * statistics.stdev(times) / statistics.mean(times),
            "luminpack_ratio": statistics.median(ratios),
            "ratio_min": min(ratios),
            "ratio_max": max(ratios),
            "bytes": sizes.pop(),
            "median_alloc_bytes_per_op": statistics.median(allocations) if allocations else None,
            "samples": len(times),
        }
    return result


def color_map(data):
    palette = README_CHARTS["COLORS"]
    colors = {REFERENCE: palette[REFERENCE]}
    peers = sorted({key[3] for key in data if key[3] != REFERENCE}, key=str.casefold)
    peer_colors = itertools.cycle(color for name, color in palette.items() if name != REFERENCE)
    colors.update(zip(peers, peer_colors))
    return colors


def panel_data(data, model, fmt):
    panels, values = [], {}
    for operation, label in OPERATIONS.items():
        keys = [key for key in data if key[:3] == (model, fmt, operation)]
        keys.sort(key=lambda key: (key[3] != REFERENCE, key[3].casefold()))
        panels.append((label, [(key[3], key) for key in keys]))
        for key in keys:
            row = data[key]
            values[key] = {"mean": row["median_ns"], "low": row["min_ns"], "high": row["max_ns"],
                           "ratio": None if key[3] == REFERENCE else row["luminpack_ratio"]}
    return panels, values


def provenance(source_url, host):
    run = urlsplit(source_url).path.rstrip("/").rsplit("/", maxsplit=1)[-1] if source_url else None
    source = f"GitHub Actions run #{run}" if run else "Local run · unpublished"
    return f"{source} · {host}" if host else source


def svg_node(parent, tag, content, **attributes):
    node = xml.SubElement(parent, f"{{{SVG_NS}}}{tag}", {key.replace("_", "-"): val for key, val in attributes.items()})
    node.text = content
    return node


def write_chart(output, language, data, theme, source_url, host):
    title = f"{LANGUAGES[language]} serialization benchmarks"
    caption = provenance(source_url, host)
    style = README_CHARTS["THEMES"][theme]
    colors = color_map(data)
    cards = []
    for model, fmt in itertools.product(MODELS, FORMATS):
        panels, values = panel_data(data, model, fmt)
        card = README_CHARTS["render"](
            f"{MODELS[model]} · {FORMATS[fmt]}", panels, values, theme, colors=colors,
            subtitle="Median time per operation · lower is better",
            footer="Whiskers show min–max. Ratios are LuminPack time ÷ library time in paired samples.",
        )
        cards.append(xml.fromstring(card))

    width = int(cards[0].attrib["width"]) + 24
    offset = 76
    gap = 12
    height = offset + sum(int(card.attrib["height"]) + gap for card in cards)
    root = xml.Element(f"{{{SVG_NS}}}svg", {"width": str(width), "height": str(height),
                       "viewBox": f"0 0 {width} {height}", "font-family": FONT, "role": "img",
                       "aria-labelledby": "chart-title chart-description"})
    svg_node(root, "title", title, id="chart-title")
    svg_node(root, "desc", f"{caption}. {source_url}".strip(), id="chart-description")
    svg_node(root, "rect", None, x="0.5", y="0.5", width=str(width - 1), height=str(height - 1),
             rx="10", fill=style["bg"], stroke=style["border"])
    svg_node(root, "text", title, x="24", y="32", font_size="19", font_weight="600", fill=style["text"])
    svg_node(root, "text", caption, x="24", y="54", font_size="12", fill=style["muted"])
    for card in cards:
        card.set("x", "12")
        card.set("y", str(offset))
        offset += int(card.attrib["height"]) + gap
        root.append(card)
    xml.indent(root)
    xml.ElementTree(root).write(output / f"{language}-{theme}.svg", encoding="utf-8")


def write_report(output, measured, source_url, host):
    rows = [dict(language=language, model=model, format=fmt, operation=operation, library=library,
                 source_url=source_url, host=host, **metrics)
            for language, groups in measured.items()
            for (model, fmt, operation, library), metrics in groups.items()]
    with (output / "summary.csv").open("w", newline="", encoding="utf-8") as target:
        writer = csv.DictWriter(target, fieldnames=rows[0])
        writer.writeheader()
        writer.writerows(rows)

    sections = []
    for language in measured:
        name = LANGUAGES[language]
        sections.append(f'<section><h2 id="{language}">{name}</h2><picture>'
                        f'<source media="(prefers-color-scheme: dark)" srcset="{language}-dark.svg">'
                        f'<img src="{language}-light.svg" alt="{name} Binary and UTF-8 JSON benchmark results">'
                        '</picture></section>')
    caption = escape(provenance(source_url, host))
    if source_url:
        caption = f'<a href="{escape(source_url, quote=True)}">{caption}</a>'
    (output / "index.html").write_text(
        '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">'
        '<title>LuminPack portable runtime benchmarks</title><style>:root{color-scheme:light dark}'
        'body{max-width:974px;margin:2rem auto;padding:0 1rem;font:16px system-ui}'
        'img{width:100%;display:block;margin:1rem 0}</style><h1>LuminPack portable runtime benchmarks</h1>\n'
        f'<p>{caption} · <a href="summary.csv">Timing, dispersion, payload sizes and allocations</a></p>\n'
        + "\n".join(sections) + "\n</html>\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--input", type=Path, action="append", required=True, help="One benchmark CSV per language.")
    parser.add_argument("--source-url", default="", help="HTTPS URL to the GitHub Actions run supplying all input files.")
    parser.add_argument("--host", default="", help="Short host label for the chart caption.")
    args = parser.parse_args()
    source = urlsplit(args.source_url)
    if args.source_url and (source.scheme != "https" or not source.netloc or "/actions/runs/" not in source.path):
        parser.error("--source-url must be an HTTPS GitHub Actions run URL.")

    measured = {}
    try:
        for path in args.input:
            language, groups = read_samples(path)
            if language in measured:
                raise ValueError(f"Multiple results for {LANGUAGES[language]}. Supply one run per language.")
            measured[language] = summarize(groups)
    except (OSError, ValueError) as error:
        parser.error(str(error))

    args.output.mkdir(parents=True, exist_ok=True)
    for language, groups in measured.items():
        for theme in ("light", "dark"):
            write_chart(args.output, language, groups, theme, args.source_url, args.host)
    write_report(args.output, measured, args.source_url, args.host)


if __name__ == "__main__":
    main()
