"""Run and summarize alternating same-host Matrix builds with the same harness."""

import argparse
import csv
import json
import os
import statistics
import subprocess
from collections import defaultdict
from pathlib import Path


def make_row(key, reference, pairs, variants):
    """Summarize process medians and their dispersion for one operation."""
    baseline = statistics.median(value[0] for value in pairs[variants[0]])
    reference_medians = [statistics.median(value[1] for value in pairs[variant]) for variant in variants]
    drift = max(reference_medians) / min(reference_medians)
    row = [*key, reference, drift]
    for variant in variants:
        samples = pairs[variant]
        times = [value[0] for value in samples]
        median = statistics.median(times)
        row += [median, median / baseline, statistics.median(value[0] / value[1] for value in samples),
                max(times) / min(times), statistics.median(value[2] for value in samples), len(samples)]
    return row


def read_pairs(path):
    """Pair each LuminPack sample with the reference sample from the same quiet round."""
    samples = defaultdict(dict)
    with path.open(encoding="utf-8") as stream:
        next(stream)
        validated = False
        for line in stream:
            row = json.loads(line)
            validated |= row["Kind"] == "Validated" and row["Library"] == "LuminPack"
            if row["Kind"] == "Sample" and row["JitCompiledMethodsBefore"] == row["JitCompiledMethodsAfter"]:
                samples[row["Case"], row["Operation"], row["Library"]][row["Round"]] = row
    if not validated:
        raise ValueError(f"{path}: no matching LuminPack fixtures validated")
    for case, operation, reference in samples:
        if reference == "LuminPack":
            continue
        lumin = samples.get((case, operation, "LuminPack"), {})
        other = samples[case, operation, reference]
        common = lumin.keys() & other.keys()
        if common:
            yield (case, operation, reference), (
                statistics.median(lumin[index]["Nanoseconds"] for index in common),
                statistics.median(other[index]["Nanoseconds"] for index in common),
                statistics.median(lumin[index]["AllocatedBytes"] for index in common))


def run(args):
    """Validate and sample each requested matrix using rotating process order."""
    variants = ["stock", "candidate"]
    dlls = [args.stock, args.candidate]
    if args.comparison:
        variants.append("comparison")
        dlls.append(args.comparison)
    rounds = args.rounds or (15 if args.mode == "pgo" else 9)
    mode = "1" if args.mode == "pgo" else "0"
    env = dict(os.environ, DOTNET_TieredCompilation=mode, DOTNET_TieredPGO=mode,
               DOTNET_TC_CallCountingDelayMs="0", LUMINPACK_MATRIX_CASES=args.cases)
    stats = defaultdict(lambda: defaultdict(list))
    args.output.mkdir(parents=True, exist_ok=True)
    for fmt, scene in dict.fromkeys(args.group):
        for process in range(args.processes):
            for offset in range(len(variants)):
                index = (process + offset) % len(variants)
                variant = variants[index]
                path = args.output / f"{fmt}-{scene}-{variant}-{process + 1}.jsonl"
                print(f"{args.mode} {fmt}/{scene} {variant} process {process + 1}", flush=True)
                subprocess.run(["dotnet", str(dlls[index]), fmt, scene, str(rounds), str(path)], env=env, check=True)
                for (case, operation, reference), sample in read_pairs(path):
                    stats[fmt, scene, case, operation, reference][variant].append(sample)
    with (args.output / "pairs.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        fields = ["ns", "over_stock", "over_reference", "spread", "B_per_op", "processes"]
        writer.writerow(["Format", "Scene", "Case", "Operation", "Reference", "Reference_drift"] +
                        [f"{variant}_{field}" for variant in variants for field in fields])
        for (fmt, scene, case, operation, reference), pairs in sorted(stats.items()):
            if any(len(pairs[variant]) != args.processes for variant in variants):
                continue
            row = make_row((fmt, scene, case, operation), reference, pairs, variants)
            writer.writerow(row)


def parse_group(value):
    """Accept a Matrix format and scene separated by a slash."""
    fmt, _, scene = value.partition("/")
    if fmt not in ("binary", "json") or scene not in ("main", "small", "regular", "large", "changing"):
        raise argparse.ArgumentTypeError("Expected binary|json/main|small|regular|large|changing")
    return fmt, scene


def main():
    """Parse CLI options and run the selected pair or trio of revisions."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("fullopts", "pgo"))
    parser.add_argument("stock", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--comparison", type=Path)
    parser.add_argument("--group", type=parse_group, action="append", required=True)
    parser.add_argument("--cases", required=True, help="Comma-separated exact fixture names")
    parser.add_argument("--processes", type=int, default=3)
    parser.add_argument("--rounds", type=int)
    args = parser.parse_args()
    if args.processes < 1 or (args.rounds is not None and args.rounds < 1) or not args.cases.strip():
        parser.error("Specify positive process and round counts and at least one fixture")
    run(args)


if __name__ == "__main__":
    main()
