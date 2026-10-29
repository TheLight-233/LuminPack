"""Report independent process distributions and same-job LuminPack/reference ratios."""

import argparse
import csv
import math
import statistics
from collections import defaultdict
from pathlib import Path


def read_measurements(paths):
    """Keep every actual workload sample, grouped by fixture, job and process."""
    cases = {}
    for path in paths:
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                mode = row["Measurement_IterationMode"]
                if row["Measurement_IterationStage"] != "Actual" or mode not in ("Workload", "Overhead"):
                    continue
                operations = int(row["Measurement_Operations"])
                nanoseconds = float(row["Measurement_Nanoseconds"])
                if operations <= 0 or not math.isfinite(nanoseconds) or nanoseconds < 0:
                    raise ValueError(f"Invalid workload measurement in {path}")
                key = (row["Target_Type"], row["Job_Id"], row["Params"], row["Target_Method"])
                case = cases.setdefault(key, {"launches": defaultdict(list), "overhead": defaultdict(list),
                                              "allocated": row["Allocated_Bytes"], "source": path})
                if case["source"] != path:
                    raise ValueError("Multiple reports for the same job; analyze each host and run separately")
                target = "launches" if mode == "Workload" else "overhead"
                case[target][int(row["Measurement_LaunchIndex"])].append(nanoseconds / operations)
    cases = {key: case for key, case in cases.items() if case["launches"]}
    if not cases:
        raise ValueError("No actual workload measurements were exported")
    for case in cases.values():
        for launch, samples in case["launches"].items():
            overhead = case["overhead"].get(launch, [])
            correction = statistics.median(overhead) if overhead else 0
            corrected = [sample - correction for sample in samples]
            if min(corrected) <= 0:
                raise ValueError("Nonpositive workload time after subtracting process overhead")
            case["launches"][launch] = corrected
    return cases


def summarize(cases, max_spread):
    """Use process means without pairing independently launched library processes."""
    lines = ["# Benchmark process stability", "",
             "All actual samples are retained, subtracting each process's median measured overhead. "
             "CV and spread describe independent process means; "
             "a single process cannot establish process stability. JIT jobs are separate comparisons.", "",
             "| Fixture / job / parameters | Method | Process means (ns/op) | Process CV | Max/min | Allocated B/op | Assessment |",
             "| --- | --- | --- | ---: | ---: | ---: | --- |"]
    groups = defaultdict(dict)
    warnings = []
    for (fixture, job, parameters, method), case in sorted(cases.items()):
        means = [statistics.mean(samples) for _, samples in sorted(case["launches"].items())]
        mean = statistics.mean(means)
        spread = max(means) / min(means)
        cv = statistics.stdev(means) / mean if len(means) > 1 else None
        assessment = "insufficient processes" if cv is None else "unstable" if spread > max_spread else "within threshold"
        label = f"{fixture} / {job} / {parameters or '-'}"
        if assessment == "unstable":
            warnings.append(f"{label}.{method}: process max/min is {spread:.3f}; do not infer a performance win from its aggregate mean.")
        cv_text = "NA" if cv is None else f"{cv:.2%}"
        lines.append(f"| {label} | {method} | {', '.join(f'{value:.3f}' for value in means)} | {cv_text} | {spread:.3f} | {case['allocated']} | {assessment} |")
        groups[(fixture, job, parameters)][method] = (mean, min(means), max(means))

    lines += ["", "## LuminPack / reference", "",
              "Ratios use the same fixture, operation, runtime, options and job. The range is the "
              "observed envelope of independent process means, not a confidence interval or paired samples.", "",
              "| Fixture / job / parameters | Operation | Reference | Ratio | Process envelope |",
              "| --- | --- | --- | ---: | --- |"]
    for (fixture, job, parameters), methods in sorted(groups.items()):
        for operation in ("Serialize", "Deserialize"):
            lumin_name = next((name for name in methods if name.startswith("LuminPack") and operation in name), None)
            if lumin_name is None:
                continue
            lumin = methods[lumin_name]
            for name, reference in sorted(methods.items()):
                if name.startswith("LuminPack") or operation not in name:
                    continue
                lines.append(f"| {fixture} / {job} / {parameters or '-'} | {operation} | {name} | {lumin[0] / reference[0]:.4f} | {lumin[1] / reference[2]:.4f} - {lumin[2] / reference[1]:.4f} |")
    return "\n".join(lines) + "\n", warnings


def main():
    """Write an artifact and optional GitHub job summary without hiding unstable runs."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifacts", type=Path)
    parser.add_argument("--max-spread", type=float, default=1.10)
    parser.add_argument("--summary", type=Path)
    args = parser.parse_args()
    if not math.isfinite(args.max_spread) or args.max_spread <= 1:
        parser.error("--max-spread must be finite and greater than one")
    cases = read_measurements(sorted(args.artifacts.rglob("*-measurements.csv")))
    report, warnings = summarize(cases, args.max_spread)
    (args.artifacts / "process-stability.md").write_text(report, encoding="utf-8")
    if args.summary:
        with args.summary.open("a", encoding="utf-8") as stream:
            stream.write(report)
    print(report)
    for warning in warnings:
        print("::warning::" + warning)


if __name__ == "__main__":
    main()
