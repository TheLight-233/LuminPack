"""Summarize matrix processes using only same-process library pairs."""

import csv
import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path


def load_process(path):
    timing = defaultdict(list)
    validated = {}
    issues = []
    with path.open(encoding="utf-8") as stream:
        environment = json.loads(next(stream))
        if environment["Kind"] != "Environment":
            raise ValueError(f"Missing environment in {path}")
        for line in stream:
            row = json.loads(line)
            if row["Kind"] == "Validated":
                validated[row["Case"], row["Library"]] = row["Bytes"]
            elif row["Kind"] == "Sample":
                timing[row["Case"], row["Operation"], row["Library"]].append(row)
            else:
                issues.append(row)

    expected_fixtures = 154 if environment["Scene"] == "main" else 153
    primary = {case for case, library in validated if library == "LuminPack"}
    if len(primary) != expected_fixtures:
        raise ValueError(f"{path}: LuminPack validated {len(primary)} of {expected_fixtures} fixtures")
    if environment["Format"] == "binary":
        nino = {case for case, library in validated if library == "Nino"}
        nino_failures = {row["Case"] for row in issues
                         if row["Library"] == "Nino" and row["Kind"] == "ValidationFailure"}
        if not nino or nino & nino_failures or (nino | nino_failures) != primary:
            raise ValueError(f"{path}: incomplete Nino validation results")
    rounds = environment["Rounds"]
    if rounds:
        for case in primary:
            for operation in ("write", "fresh", "reuse"):
                if (case, operation, "LuminPack") not in timing:
                    raise ValueError(f"{path}: missing {case}/{operation}")
        for case, library in validated:
            for operation in ("write", "fresh"):
                if (case, operation, library) not in timing:
                    raise ValueError(f"{path}: missing {case}/{library}/{operation}")
    for key, samples in timing.items():
        if len(samples) != rounds or {r["Round"] for r in samples} != set(range(rounds)):
            raise ValueError(f"{path}: incomplete samples for {key}")
    return environment, timing, validated, issues


def main(directory):
    processes = defaultdict(list)
    for path in sorted(directory.glob("*.jsonl")):
        environment, timing, validated, issues = load_process(path)
        processes[environment["Format"], environment["Scene"]].append((environment, timing, validated, issues))
    expected = {(fmt, scene) for fmt in ("binary", "json")
                for scene in ("main", "small", "regular", "large", "changing")}
    if processes.keys() != expected:
        raise ValueError("Expected every format and scenario")

    report = ["# Serialization benchmark matrix", "",
              "Ratios are LuminPack time divided by reference time. Lower is faster.",
              "Counts include only operations with valid results from both libraries in every process.", "",
              "| Format | Scenario | Reference | Operation | Paired operations | Geometric mean | Arithmetic mean |",
              "|---|---|---|---|---:|---:|---:|"]
    coverage = []
    operations = []
    pairs = []
    unsupported = []
    for (fmt, scene), runs in sorted(processes.items()):
        environment, first, validated, issues = runs[0]
        if len(runs) != (3 if environment["Rounds"] else 1):
            raise ValueError(f"{fmt}/{scene}: incomplete process set")
        if any((env, set(timing), set(checks)) != (environment, set(first), set(validated))
               for env, timing, checks, _ in runs):
            raise ValueError(f"{fmt}/{scene}: inconsistent process results")
        references = environment["References"]
        primary_count = sum(lib == "LuminPack" for _, lib in validated)
        for reference in references:
            coverage.append((fmt, scene, reference, primary_count,
                             sum(lib == reference for _, lib in validated)))
        for issue in issues:
            unsupported.append((fmt, scene, issue["Case"], issue["Library"],
                                issue.get("Operation", "all"), issue["Reason"]))

        def medians(key, field):
            return [statistics.median(row[field] for row in timing[key]) for _, timing, _, _ in runs]

        for key in sorted(first):
            case, operation, library = key
            times = medians(key, "Nanoseconds")
            allocations = medians(key, "AllocatedBytes")
            operations.append((fmt, scene, case, operation, library, statistics.median(times),
                               min(times), max(times), statistics.median(allocations), validated[case, library]))

        for reference in references:
            for operation in ("write", "fresh", "reuse", "all"):
                values = []
                for case, action, lib in sorted(first):
                    if lib != "LuminPack" or operation not in (action, "all") or (case, action, reference) not in first:
                        continue
                    ratios = [a / b for a, b in zip(medians((case, action, lib), "Nanoseconds"),
                                                   medians((case, action, reference), "Nanoseconds"))]
                    values.append(statistics.median(ratios))
                    if operation != "all":
                        pairs.append((fmt, scene, case, action, reference, values[-1], min(ratios), max(ratios)))
                if first:
                    means = f"{statistics.geometric_mean(values):.3f} | {statistics.mean(values):.3f}" if values else "— | —"
                    report.append(f"| {fmt} | {scene} | {reference} | {operation} | {len(values)} | {means} |")

    if not operations:
        report = ["# Serialization benchmark matrix validation", "",
                  "This run validated fixtures without collecting timing samples."]
    report += ["", "| Format | Scenario | Reference | Validated LuminPack fixtures | Validated reference fixtures |",
               "|---|---|---|---:|---:|"]
    report += [f"| {fmt} | {scene} | {library} | {primary} | {reference} |"
               for fmt, scene, library, primary, reference in coverage]
    report += ["", "See the uploaded JSONL for raw samples and environment metadata.",
               "The CSV files contain operation medians, process ranges, allocations, and unavailable operations."]

    def write_csv(name, header, rows):
        with (directory / name).open("w", encoding="utf-8", newline="") as stream:
            writer = csv.writer(stream)
            writer.writerow(header)
            writer.writerows(rows)

    write_csv("operations.csv", ["Format", "Scenario", "Case", "Operation", "Library", "Median_ns",
                                "Min_process_ns", "Max_process_ns", "B_per_op", "Payload_bytes"], operations)
    write_csv("pairs.csv", ["Format", "Scenario", "Case", "Operation", "Reference", "Median_ratio",
                           "Min_process_ratio", "Max_process_ratio"], pairs)
    write_csv("unavailable.csv", ["Format", "Scenario", "Case", "Library", "Operation", "Reason"], unsupported)
    (directory / "REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
