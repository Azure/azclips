#!/usr/bin/env python3

import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

import compare_benchmarks as compare
from benchmark_fixtures import captured_warnings, data_js_entry, write_data_js, write_report
from benchmark_results import BenchmarkResult

CLI = "Benchmark.CLI.VirtualMachineBenchmarks."
COLD = "Benchmark.Startup.CliColdStartupBenchmarks.Config"
BUILD_URL = "https://dev.azure.com/azclitools/public/_build/results?buildId=378289"
INFO = {"buildId": 378289, "buildUrl": BUILD_URL, "commit": "b3b44286e925f09f587c20858539aa137521f568",
        "stageResult": "succeeded", "failedJobs": []}


def result(name, mean, product="CLI", extension="COMPUTE"):
    return BenchmarkResult(extension, product, name, mean, mean / 100)


class RowTests(unittest.TestCase):
    def test_changes_are_flagged_only_above_the_threshold(self):
        baseline = {CLI + "Exact": 100.0, CLI + "Rounded": 100.0, CLI + "Over": 100.0, CLI + "Faster": 100.0,
                    COLD: 100.0}
        results = [result(CLI + "Exact", 115.0), result(CLI + "Rounded", 115.04), result(CLI + "Over", 115.06),
                   result(CLI + "Faster", 90.0), result(CLI + "New", 50.0), result(COLD, 200.0, "STARTUP", "ALL")]

        rows = {row.label: row for row in compare.build_rows(results, baseline, 15.0)}

        self.assertEqual((rows["VirtualMachine.Exact"].change, rows["VirtualMachine.Exact"].regressed), ("+15.0%", False))
        # The displayed one-decimal change is compared: +15.04% shows as +15.0%, which a raw comparison would flag.
        self.assertEqual((rows["VirtualMachine.Rounded"].change, rows["VirtualMachine.Rounded"].regressed),
                         ("+15.0%", False))
        self.assertEqual((rows["VirtualMachine.Over"].change, rows["VirtualMachine.Over"].regressed), ("+15.1% ⚠️", True))
        self.assertEqual(rows["VirtualMachine.Faster"].change, "-10.0%")
        self.assertEqual((rows["VirtualMachine.New"].baseline, rows["VirtualMachine.New"].change), ("new", "n/a"))
        cold = rows["CliColdStartup.Config"]
        self.assertEqual((cold.change, cold.regressed), ("+100.0% (informational)", False))

    def test_durations_scale_their_units(self):
        self.assertEqual(compare.format_duration(850.0), "850.0 ns")
        self.assertEqual(compare.format_duration(12_345.0), "12.3 µs")
        self.assertEqual(compare.format_duration(16_800_000.0), "16.8 ms")
        self.assertEqual(compare.format_duration(1_234_000_000.0), "1.23 s")
        self.assertEqual(compare.format_measurement(20_100_000.0, 200_000.0), "20.1 ms ± 0.2 ms")


class BaselineTests(unittest.TestCase):
    def test_uses_the_latest_value_across_series(self):
        with tempfile.TemporaryDirectory() as temp:
            bench = Path(temp)
            write_data_js(bench / "ps" / "data.js", {
                "Azclips PS Benchmarks": [data_js_entry("old", 1, {"Benchmark.PS.X.Get": 50.0})],
                "Azclips Compute PS Benchmarks": [data_js_entry("a", 2, {"Benchmark.PS.X.Get": 40.0}),
                                                  data_js_entry("b", 3, {"Benchmark.PS.X.Get": 30.0})],
            })
            (bench / "cli").mkdir()
            (bench / "cli" / "data.js").write_text("not data", encoding="utf-8")

            with captured_warnings() as warnings:
                baseline = compare.load_baseline(bench)

        self.assertEqual(baseline, {"Benchmark.PS.X.Get": 30.0})
        self.assertIn("Ignoring the cli baseline", warnings.getvalue())


class RenderTests(unittest.TestCase):
    def rows(self):
        return compare.build_rows([result(CLI + "Create", 120.0), result(CLI + "List", 100.0),
                                   result("Benchmark.PS.VirtualMachineBenchmarks.Create", 80.0, "PS")],
                                  {CLI + "Create": 100.0, CLI + "List": 100.0}, 15.0)

    def test_comment(self):
        info = {**INFO, "stageResult": "failed", "failedJobs": ["CLI Benchmarks compute", "PS | `x` ![i](u)"]}

        text = compare.render(self.rows(), info, fmt="comment", threshold=15.0)

        self.assertTrue(text.startswith("## 🔴 Benchmark results\n\nCommit `b3b4428` · "
                                        f"[ADO build 378289]({BUILD_URL}) · compared with"))
        self.assertIn("> ⚠️ **1 benchmark is more than 15% slower than `main`.**", text)
        self.assertIn("### CLI\n\n| Benchmark | This PR | main | Change |\n| --- | --- | --- | --- |\n"
                      "| `VirtualMachine.Create` | 120.0 ns ± 1.2 ns | 100.0 ns | +20.0% ⚠️ |\n", text)
        self.assertIn("### PowerShell\n", text)
        self.assertIn("Failed benchmark jobs: `CLI Benchmarks compute`, `PS  x i(u)`", text)
        self.assertIn(f"[build summary]({BUILD_URL}&view=ms.vss-build-web.run-extensions-tab)", text)
        self.assertTrue(text.endswith(compare.COMMENT_MARKER + "\n"))

    def test_summary(self):
        text = compare.render(self.rows(), {**INFO, "stageResult": None}, fmt="summary", threshold=15.0)

        self.assertTrue(text.startswith("## Benchmark results\n"))
        self.assertIn("| Benchmark | This run | main | Change |", text)
        self.assertNotIn(compare.COMMENT_MARKER, text)

    def test_a_passing_run_without_results(self):
        text = compare.render([], {"stageResult": "succeeded"}, fmt="comment", threshold=15.0)

        self.assertTrue(text.startswith("## 🟢 Benchmark results\n\nCompared with the latest `main` results"))
        self.assertIn("No benchmark results were produced.", text)

    def test_build_info_with_a_trailing_newline_is_left_out(self):
        info = {**INFO, "commit": INFO["commit"] + "\n", "buildUrl": BUILD_URL + "\n"}

        text = compare.render([], info, fmt="comment", threshold=15.0)

        self.assertTrue(text.startswith("## 🟢 Benchmark results\n\nCompared with the latest `main` results"))
        self.assertNotIn("_build/results", text)

    def test_long_comments_are_truncated(self):
        rows = compare.build_rows([result(f"{CLI}Method{index:03d}", 100.0) for index in range(300)], {}, 15.0)

        text = compare.render(rows, INFO, fmt="comment", threshold=15.0, max_chars=4_000)

        self.assertLessEqual(len(text), 4_000)
        self.assertIn("more benchmarks are not shown here; see the build summary.", text)
        self.assertTrue(text.endswith(compare.COMMENT_MARKER + "\n"))

    def test_main_writes_the_rendered_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_report(root / "reports", "COMPUTE", "CLI", "VirtualMachineBenchmarks", {"List": (110.0, 1.0)})
            write_data_js(root / "bench" / "cli" / "data.js", {
                "Azclips Compute CLI Benchmarks": [data_js_entry("a", 1, {CLI + "List": 100.0})]})
            (root / "build.json").write_text(json.dumps(INFO), encoding="utf-8")

            with redirect_stdout(io.StringIO()):
                compare.main(["--results", str(root / "reports"), "--baseline", str(root / "bench"),
                              "--build-info", str(root / "build.json"), "--format", "comment",
                              "--output", str(root / "comment.md")])

            text = (root / "comment.md").read_text(encoding="utf-8")

        self.assertIn("| `VirtualMachine.List` | 110.0 ns ± 1.0 ns | 100.0 ns | +10.0% |", text)


if __name__ == "__main__":
    unittest.main()
