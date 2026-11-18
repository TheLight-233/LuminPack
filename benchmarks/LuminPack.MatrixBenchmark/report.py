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
    warmups = {}
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
            elif row["Kind"] == "Warmup":
                key = row["Case"], row["Operation"], row["Library"]
                if key in warmups:
                    raise ValueError(f"{path}: duplicate warmup for {key}")
                warmups[key] = row
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
    if environment.get("WarmupStrategy"):
        if warmups.keys() != timing.keys():
            raise ValueError(f"{path}: incomplete operation warmup")
        for key, warmup in warmups.items():
            if (warmup["Calls"] < environment["WarmupMinCalls"]
                    or warmup["Milliseconds"] < environment["WarmupMinMilliseconds"]):
                raise ValueError(f"{path}: incomplete warmup for {key}")
    return environment, timing, validated, issues


def quiet_rounds(samples):
    return {sample["Round"]: sample for sample in samples
            if sample.get("JitCompiledMethodsBefore") == sample.get("JitCompiledMethodsAfter")}


def main(directory):
    processes = defaultdict(list)
    for path in sorted(directory.glob("*.jsonl")):
        environment, timing, validated, issues = load_process(path)
        processes[environment["Format"], environment["Scene"]].append((path.stem, environment, timing, validated, issues))
    expected = {(fmt, scene) for fmt in ("binary", "json")
                for scene in ("main", "small", "regular", "large", "changing")}
    if processes.keys() != expected:
        raise ValueError("Expected every format and scenario")

    configs = {(env.get("TieredCompilation"), env.get("TieredPGO"), env.get("CallCountingDelayMilliseconds"),
                env.get("WarmupMinCalls"), env.get("WarmupMinMilliseconds")) for runs in processes.values()
               for _, env, _, _, _ in runs}
    if len(configs) != 1:
        raise ValueError("The matrix contains mixed JIT or warmup configurations")
    tiered, pgo, delay, warmup_calls, warmup_ms = next(iter(configs))
    report = ["# Serialization benchmark matrix", "",
              f"Tiered compilation: {tiered}; dynamic PGO: {pgo}; call-counting delay (ms): {delay}.",
              f"Minimum warmup per library and operation: {warmup_ms} ms and {warmup_calls} calls.", "",
              "Ratios are LuminPack time divided by reference time. Lower is faster.",
              "Counts include only operations with valid results from both libraries in every process.", "",
              "| Format | Scenario | Reference | Operation | Paired operations | Geometric mean | Arithmetic mean |",
              "|---|---|---|---|---:|---:|---:|"]
    coverage = []
    operations = []
    pairs = []
    unsupported = []
    drift = []
    interrupted_samples = 0
    missing_stable_pairs = 0
    for (fmt, scene), runs in sorted(processes.items()):
        _, environment, first, validated, issues = runs[0]
        if len(runs) != (3 if environment["Rounds"] else 1):
            raise ValueError(f"{fmt}/{scene}: incomplete process set")
        if any((env, set(timing), set(checks)) != (environment, set(first), set(validated))
               for _, env, timing, checks, _ in runs):
            raise ValueError(f"{fmt}/{scene}: inconsistent process results")
        references = environment["References"]
        primary_count = sum(lib == "LuminPack" for _, lib in validated)
        for reference in references:
            coverage.append((fmt, scene, reference, primary_count,
                             sum(lib == reference for _, lib in validated)))
        for issue in issues:
            unsupported.append((fmt, scene, issue["Case"], issue["Library"],
                                issue.get("Operation", "all"), issue["Reason"]))

        for process, _, timing, _, _ in runs:
            for (case, operation, library), samples in timing.items():
                if len(samples) < 6:
                    continue
                times = [sample["Nanoseconds"] for sample in sorted(samples, key=lambda sample: sample["Round"])]
                early, late = statistics.median(times[:3]), statistics.median(times[-3:])
                change = (late / early - 1) * 100
                if abs(change) >= 20:
                    drift.append((fmt, scene, process, case, operation, library, early, late, change))

        stable = [{key: quiet_rounds(samples) for key, samples in timing.items()}
                  for _, _, timing, _, _ in runs]
        interrupted_samples += sum(len(timing[key]) - len(quiet[key])
                                   for (_, _, timing, _, _), quiet in zip(runs, stable) for key in first)
        for key in sorted(first):
            case, operation, library = key
            samples_by_process = [quiet[key].values() for quiet in stable]
            if not all(samples_by_process):
                continue
            times = [statistics.median(row["Nanoseconds"] for row in samples) for samples in samples_by_process]
            allocations = [statistics.median(row["AllocatedBytes"] for row in samples) for samples in samples_by_process]
            operations.append((fmt, scene, case, operation, library, statistics.median(times),
                               min(times), max(times), statistics.median(allocations), validated[case, library]))

        for reference in references:
            for operation in ("write", "fresh", "reuse", "all"):
                values = []
                for case, action, lib in sorted(first):
                    if lib != "LuminPack" or operation not in (action, "all") or (case, action, reference) not in first:
                        continue
                    ratios = []
                    for quiet in stable:
                        lumin = quiet[case, action, lib]
                        other = quiet[case, action, reference]
                        common = lumin.keys() & other.keys()
                        if not common:
                            break
                        ratios.append(statistics.median(lumin[round_]["Nanoseconds"] for round_ in common) /
                                      statistics.median(other[round_]["Nanoseconds"] for round_ in common))
                    if len(ratios) != len(stable):
                        if operation != "all":
                            missing_stable_pairs += 1
                        continue
                    values.append(statistics.median(ratios))
                    if operation != "all":
                        pairs.append((fmt, scene, case, action, reference, values[-1], min(ratios), max(ratios)))
                if first:
                    means = f"{statistics.geometric_mean(values):.3f} | {statistics.mean(values):.3f}" if values else "— | —"
                    report.append(f"| {fmt} | {scene} | {reference} | {operation} | {len(values)} | {means} |")

    if not operations and all(not env["Rounds"] for runs in processes.values() for _, env, _, _, _ in runs):
        report = ["# Serialization benchmark matrix validation", "",
                  "This run validated fixtures without collecting timing samples."]
    elif drift:
        report += ["", f"**Stability warning:** {len(drift)} measurements shifted by at least 20% between "
                         "the first and last three rounds of a process. See stability.csv for the raw-round drift check.",
                   "The drift detector flags observations for investigation and does not identify their cause."]
    if interrupted_samples:
        report += ["", f"Global JIT activity excluded {interrupted_samples} timing samples. "
                         f"{missing_stable_pairs} comparisons had no common quiet round in at least one process."]
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
    write_csv("stability.csv", ["Format", "Scenario", "Process", "Case", "Operation", "Library",
                                "First_three_median_ns", "Last_three_median_ns", "Change_pct"], drift)
    (directory / "REPORT.md").write_text("\n".join(report) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
