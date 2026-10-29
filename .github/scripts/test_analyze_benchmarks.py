"""Check process aggregation and ratio direction using independent JIT jobs."""

import csv
import tempfile
import unittest
from pathlib import Path

from analyze_benchmarks import read_measurements, summarize


class BenchmarkAnalysisTests(unittest.TestCase):
    """Exercise noisy distributions and incomplete timing artifacts."""

    def test_jobs_and_processes_stay_separate(self):
        """A bimodal process distribution must survive aggregation and raise a warning."""
        cases = {
            ("Fixture", "TieredPGO", "", "LuminPackSerialize"): {"launches": {1: [2, 2], 2: [4, 4]}, "allocated": "0"},
            ("Fixture", "TieredPGO", "", "NinoSerialize"): {"launches": {1: [4], 2: [4]}, "allocated": "0"},
            ("Fixture", "FullOpts", "", "LuminPackSerialize"): {"launches": {1: [8], 2: [8]}, "allocated": "0"},
            ("Fixture", "FullOpts", "", "NinoSerialize"): {"launches": {1: [4], 2: [4]}, "allocated": "0"},
        }
        report, warnings = summarize(cases, 1.1)
        self.assertEqual(len(warnings), 1)
        self.assertIn("2.000, 4.000", report)
        self.assertIn("| 0.7500 | 0.5000 - 1.0000 |", report)
        self.assertIn("| 2.0000 | 2.0000 - 2.0000 |", report)

    def test_single_process_is_inconclusive(self):
        """Repeated samples in one process do not establish cross-process stability."""
        cases = {("Fixture", "Job", "", "LuminPackDeserialize"): {"launches": {1: [1, 1, 1]}, "allocated": "32"}}
        report, warnings = summarize(cases, 1.1)
        self.assertIn("insufficient processes", report)
        self.assertEqual(warnings, [])

    def test_csv_keeps_actual_samples_and_rejects_empty_results(self):
        """Remove measured overhead while retaining every actual workload sample."""
        columns = ["Target_Type", "Job_Id", "Params", "Target_Method", "Allocated_Bytes", "Measurement_LaunchIndex",
                   "Measurement_IterationMode", "Measurement_IterationStage", "Measurement_Operations", "Measurement_Nanoseconds"]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "measurements.csv"
            with path.open("w", newline="", encoding="utf-8") as stream:
                writer = csv.writer(stream)
                writer.writerow(columns)
                for mode, stage, ns in (("Workload", "Warmup", 999), ("Overhead", "Actual", 10),
                                        ("Workload", "Result", 999), ("Workload", "Actual", 20)):
                    writer.writerow(["Fixture", "Job", "", "LuminPackSerialize", "0", 1, mode, stage, 10, ns])
            cases = read_measurements([path])
            self.assertEqual(cases[("Fixture", "Job", "", "LuminPackSerialize")]["launches"], {1: [1]})
            other_host = Path(directory) / "other-host.csv"
            other_host.write_bytes(path.read_bytes())
            with self.assertRaisesRegex(ValueError, "each host and run separately"):
                read_measurements([path, other_host])
            with self.assertRaises(ValueError):
                read_measurements([])


if __name__ == "__main__":
    unittest.main()
