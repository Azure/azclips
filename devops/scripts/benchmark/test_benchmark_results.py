#!/usr/bin/env python3

import io
import json
import os
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import benchmark_results as br
from benchmark_fixtures import captured_warnings, write_report


class LoadResultsTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)

    def test_reads_the_ci_layout_in_report_order(self):
        write_report(self.root, "COMPUTE", "CLI", "VirtualMachineBenchmarks",
                     {"ListVirtualMachines": (12.5, 0.5), "CreateVirtualMachine": (20.0, 1.0)})
        write_report(self.root, "COMPUTE", "PS", "VirtualMachineBenchmarks", {"ListVirtualMachines": (8.0, 0.1)})
        write_report(self.root, "ALL", "STARTUP", "CliWarmStartupBenchmarks", {"Config": (2.4e8, 3e6)})
        write_report(self.root, "lowercase", "CLI", "IgnoredBenchmarks", {"Ignored": (1.0, 0.0)})

        results = br.load_results(self.root)

        self.assertEqual(
            [(r.extension, r.product, r.full_name, r.mean_ns, r.stddev_ns) for r in results],
            [
                ("ALL", "STARTUP", "Benchmark.Startup.CliWarmStartupBenchmarks.Config", 2.4e8, 3e6),
                ("COMPUTE", "CLI", "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines", 12.5, 0.5),
                ("COMPUTE", "CLI", "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine", 20.0, 1.0),
                ("COMPUTE", "PS", "Benchmark.PS.VirtualMachineBenchmarks.ListVirtualMachines", 8.0, 0.1),
            ],
        )

    def test_skips_failed_unsafe_and_malformed_benchmarks_with_warnings(self):
        report = write_report(self.root, "KEYVAULT", "CLI", "KeyVaultBenchmarks",
                              {"SetSecret": (5.0, 0.2), "GetSecret": None})
        data = json.loads(report.read_text(encoding="utf-8"))
        data["Benchmarks"] += [
            {"FullName": "Benchmark.CLI.KeyVaultBenchmarks.`|Injected",
             "Statistics": {"Mean": 1, "StandardDeviation": 0}},
            {"FullName": "Benchmark.CLI.KeyVaultBenchmarks.Trailing\n",
             "Statistics": {"Mean": 1, "StandardDeviation": 0}},
            {"FullName": "Benchmark.CLI.KeyVaultBenchmarks.NotANumber",
             "Statistics": {"Mean": float("nan"), "StandardDeviation": 0}},
            {"FullName": "Benchmark.CLI.KeyVaultBenchmarks.TooLarge",
             "Statistics": {"Mean": 10**400, "StandardDeviation": 0}},
            {"FullName": "Benchmark.CLI.KeyVaultBenchmarks.Infinite",
             "Statistics": {"Mean": float("inf"), "StandardDeviation": 0}},
        ]
        report.write_text(json.dumps(data), encoding="utf-8")
        broken = self.root / "KEYVAULT" / "PS" / "results" / ("Broken" + br.REPORT_SUFFIX)
        broken.parent.mkdir(parents=True)
        broken.write_text("{", encoding="utf-8")

        with captured_warnings() as warnings:
            results = br.load_results(self.root)

        self.assertEqual([r.full_name for r in results], ["Benchmark.CLI.KeyVaultBenchmarks.SetSecret"])
        log = warnings.getvalue()
        self.assertIn("GetSecret: it has no usable statistics", log)
        self.assertEqual(log.count("unsupported name"), 2)
        self.assertIn("NotANumber: it has no usable statistics", log)
        self.assertIn("TooLarge: it has no usable statistics", log)
        self.assertIn("Infinite: it has no usable statistics", log)
        self.assertIn("Skipping benchmark report Broken", log)

    def test_a_missing_directory_has_no_results(self):
        self.assertEqual(br.load_results(self.root / "missing"), [])


class NamingTests(unittest.TestCase):
    @staticmethod
    def result(extension, product, name):
        return br.BenchmarkResult(extension, product, name, 1.0, 0.0)

    def test_labels_drop_the_namespace_and_benchmarks_suffix(self):
        self.assertEqual(br.display_label("Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines"),
                         "VirtualMachine.ListVirtualMachines")
        self.assertEqual(br.display_label("Benchmark.Startup.CliColdStartupBenchmarks.Config"),
                         "CliColdStartup.Config")
        self.assertEqual(br.display_label("Lonely"), "Lonely")

    def test_series_match_the_existing_dashboards(self):
        cases = {
            ("MANAGEDSERVICEIDENTITY", "CLI", "Benchmark.CLI.UserAssignedIdentityBenchmarks.Get"):
                ("dev/bench/cli", "Azclips Managedserviceidentity CLI Benchmarks"),
            ("KEYVAULT", "PS", "Benchmark.PS.KeyVaultBenchmarks.Get"):
                ("dev/bench/ps", "Azclips Keyvault PS Benchmarks"),
            ("ALL", "STARTUP", "Benchmark.Startup.CliWarmStartupBenchmarks.Config"):
                ("dev/bench/startup-cli", "Azclips CLI Startup (warm)"),
            ("ALL", "STARTUP", "Benchmark.Startup.PsColdStartupBenchmarks.Login"):
                ("dev/bench/startup-ps", "Azclips PS Startup (cold)"),
            ("ALL", "STARTUP", "Benchmark.Startup.NewStartupBenchmarks.Config"): None,
        }
        for key, expected in cases.items():
            with self.subTest(key=key):
                self.assertEqual(br.series_for(self.result(*key)), expected)

    def test_only_cold_startup_is_informational(self):
        self.assertTrue(br.is_informational(
            self.result("ALL", "STARTUP", "Benchmark.Startup.CliColdStartupBenchmarks.Config")))
        self.assertFalse(br.is_informational(
            self.result("ALL", "STARTUP", "Benchmark.Startup.CliWarmStartupBenchmarks.Config")))
        self.assertFalse(br.is_informational(
            self.result("COMPUTE", "CLI", "Benchmark.CLI.CliColdStartupBenchmarks.Run")))


class AttemptTests(unittest.TestCase):
    def test_the_newest_attempt_wins(self):
        self.assertEqual(
            br.latest_attempts(["Benchmark_CLI_compute", "Benchmark_CLI_compute_attempt3",
                                "Benchmark_CLI_compute_attempt2", "Benchmark_STARTUP"]),
            {"Benchmark_CLI_compute": "Benchmark_CLI_compute_attempt3", "Benchmark_STARTUP": "Benchmark_STARTUP"},
        )


class DataJsTests(unittest.TestCase):
    def test_round_trip_matches_github_action_benchmark(self):
        data = {"lastUpdate": 1, "repoUrl": "https://github.com/Azure/azclips",
                "entries": {"Azclips Compute CLI Benchmarks": [{"benches": [{"range": "± 1.5"}]}]}}

        text = br.format_data_js(data)

        self.assertTrue(text.startswith('window.BENCHMARK_DATA = {\n  "lastUpdate": 1,'))
        self.assertFalse(text.endswith("\n"))
        self.assertIn('"range": "± 1.5"', text)
        self.assertEqual(br.parse_data_js(text), data)

    def test_rejects_other_files(self):
        for text in ("console.log(1)", "window.BENCHMARK_DATA = []", 'window.BENCHMARK_DATA = {"entries": []}'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                br.parse_data_js(text)


class WarnTests(unittest.TestCase):
    def capture(self, environ):
        stdout = io.StringIO()
        with mock.patch.dict(os.environ, environ, clear=True), redirect_stdout(stdout):
            br.warn("first line\n##vso[task.complete result=Failed]second")
        return stdout.getvalue()

    def test_azure_pipelines_format(self):
        self.assertEqual(self.capture({"TF_BUILD": "True"}),
                         "##vso[task.logissue type=warning]first line ##vso_[task.complete result=Failed]second\n")

    def test_github_actions_format(self):
        self.assertEqual(self.capture({"GITHUB_ACTIONS": "true"}),
                         "::warning::first line ##vso_[task.complete result=Failed]second\n")


if __name__ == "__main__":
    unittest.main()
