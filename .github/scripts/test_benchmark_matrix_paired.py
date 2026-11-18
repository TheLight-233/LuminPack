"""Check same-process, same-round Matrix ratios."""

import csv
import json
import runpy
import tempfile
import unittest
from pathlib import Path


paired = runpy.run_path(str(Path(__file__).with_name("benchmark-matrix-paired.py")))


class MatrixPairTests(unittest.TestCase):
    def test_reference_and_revision_ratios(self):
        """Quiet pairs can have a different median ratio from the ratio of medians."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            output = root / "pairs.csv"
            key = ("binary", "regular", "Queue", "write", "Nino")
            variants = ("stock", "candidate")
            stats = {key: {variant: [] for variant in variants}}
            for variant, scale in zip(variants, (2, 1)):
                for process, clock in enumerate((1, 3)):
                    path = root / f"{variant}-{process}.jsonl"
                    rows = [{}, {"Kind": "Validated", "Library": "LuminPack"}]
                    for index, (lumin, nino) in enumerate(((1, 1), (16, 8), (200, 400), (800, 1))):
                        for library, ns in (("Nino", nino), ("LuminPack", lumin * scale)):
                            rows.append({"Case": "Queue", "Operation": "write", "Library": library,
                                         "Kind": "Sample", "Round": index, "Nanoseconds": ns * clock,
                                         "AllocatedBytes": 16, "JitCompiledMethodsBefore": 0,
                                         "JitCompiledMethodsAfter": int(index == 3 and library == "Nino")})
                    path.write_text("\n".join(map(json.dumps, rows)), encoding="utf-8")
                    stats[key][variant].append(dict(paired["read_pairs"](path))[key[2:]])
            paired["write_summary"](output, stats, variants, processes=2)
            with output.open(encoding="utf-8", newline="") as stream:
                row, = csv.DictReader(stream)
            self.assertEqual(float(row["stock_over_reference"]), 2)
            self.assertEqual(float(row["candidate_over_reference"]), 1)
            self.assertEqual(float(row["candidate_ratio_over_stock"]), 0.5)
            self.assertEqual(float(row["candidate_ratio_spread"]), 1)
            self.assertEqual(float(row["candidate_B_per_op"]), 16)


if __name__ == "__main__":
    unittest.main()
