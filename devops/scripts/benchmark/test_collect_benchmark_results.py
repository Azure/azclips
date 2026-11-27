#!/usr/bin/env python3

import io
import json
import os
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import collect_benchmark_results as collect
from benchmark_fixtures import captured_warnings, write_report

PULL_REQUEST_RUN = {
    "BUILD_BUILDID": "378289",
    "BUILD_REASON": "PullRequest",
    "BUILD_SOURCEBRANCH": "refs/pull/1496/merge",
    "BUILD_SOURCEVERSION": "f" * 40,
    "SYSTEM_PULLREQUEST_SOURCECOMMITID": "b" * 40,
    "SYSTEM_PULLREQUEST_PULLREQUESTNUMBER": "1496",
    "SYSTEM_COLLECTIONURI": "https://dev.azure.com/azclitools/",
    "SYSTEM_TEAMPROJECT": "public",
    "BENCHMARK_EXTENSIONS": "compute,keyvault",
    "BENCHMARK_STARTUP": "True",
}


class CollectTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.legs, self.merged, self.out = self.root / "legs", self.root / "merged", self.root / "out"

    def leg(self, artifact, extension, product, type_name, mean):
        write_report(self.legs / artifact, extension, product, type_name, {"Run": (mean, 1.0)})
        telemetry = self.legs / artifact / extension / product / "telemetry"
        telemetry.mkdir(parents=True, exist_ok=True)
        (telemetry / f"{product}.{type_name}.Run.jsonl").write_text(f'{{"attempt": {mean}}}\n', encoding="utf-8")

    def collect_with(self, extensions, startup):
        """Run main() for a run that selected these legs. Returns the warnings, one per line."""
        environ = {"BENCHMARK_EXTENSIONS": extensions, "BENCHMARK_STARTUP": startup}
        with mock.patch.dict(os.environ, environ), captured_warnings() as warnings, redirect_stdout(io.StringIO()):
            self.assertEqual(collect.main(["--legs", str(self.legs), "--merged", str(self.merged),
                                           "--out", str(self.out)]), 0)
        return warnings.getvalue().splitlines()

    def test_keeps_the_newest_attempt_of_each_leg(self):
        self.leg("Benchmark_CLI_compute", "COMPUTE", "CLI", "VirtualMachineBenchmarks", 1.0)
        self.leg("Benchmark_CLI_compute_attempt2", "COMPUTE", "CLI", "VirtualMachineBenchmarks", 2.0)
        self.leg("Benchmark_STARTUP", "ALL", "STARTUP", "CliWarmStartupBenchmarks", 3.0)
        self.leg("Benchmark_Build_Outputs", "COMPUTE", "PS", "IgnoredBenchmarks", 4.0)

        chosen = collect.collect(self.legs, self.merged, self.out)

        self.assertEqual(chosen, ["Benchmark_CLI_compute_attempt2", "Benchmark_STARTUP"])
        report = self.out / "reports/COMPUTE/CLI/results/Benchmark.CLI.VirtualMachineBenchmarks-report-full-compressed.json"
        self.assertEqual(json.loads(report.read_text(encoding="utf-8"))["Benchmarks"][0]["Statistics"]["Mean"], 2.0)
        self.assertTrue((self.out / "reports/ALL/STARTUP/results").is_dir())
        self.assertFalse((self.out / "reports/COMPUTE/PS").exists())
        telemetry = self.merged / "COMPUTE/CLI/telemetry/CLI.VirtualMachineBenchmarks.Run.jsonl"
        self.assertEqual(telemetry.read_text(encoding="utf-8"), '{"attempt": 2.0}\n')

    def test_metadata_describes_a_pull_request_run(self):
        self.assertEqual(collect.build_metadata(PULL_REQUEST_RUN), {
            "schema": 1,
            "buildId": 378289,
            "buildUrl": "https://dev.azure.com/azclitools/public/_build/results?buildId=378289",
            "reason": "PullRequest",
            "sourceBranch": "refs/pull/1496/merge",
            "commit": "b" * 40,
            "pullRequest": 1496,
            "extensions": ["compute", "keyvault"],
            "startup": True,
            "stageResult": None,
            "failedJobs": [],
        })

    def test_metadata_of_a_main_run_uses_the_built_commit(self):
        environ = {"BUILD_BUILDID": "9", "BUILD_SOURCEVERSION": "a" * 40}

        metadata = collect.build_metadata(environ)

        self.assertEqual((metadata["commit"], metadata["pullRequest"], metadata["buildUrl"]), ("a" * 40, None, ""))

    def test_main_writes_reports_and_metadata_without_any_legs(self):
        with mock.patch.dict(os.environ, PULL_REQUEST_RUN), captured_warnings(), redirect_stdout(io.StringIO()):
            self.assertEqual(collect.main(["--legs", str(self.legs), "--merged", str(self.merged),
                                           "--out", str(self.out)]), 0)

        self.assertTrue((self.out / "reports").is_dir())
        self.assertEqual(json.loads((self.out / "metadata.json").read_text(encoding="utf-8"))["buildId"], 378289)

    def test_complete_legs_are_not_warned_about(self):
        self.leg("Benchmark_CLI_compute", "COMPUTE", "CLI", "VirtualMachineBenchmarks", 1.0)
        self.leg("Benchmark_PS_compute", "COMPUTE", "PS", "VirtualMachineBenchmarks", 1.0)
        self.leg("Benchmark_STARTUP", "ALL", "STARTUP", "CliWarmStartupBenchmarks", 1.0)

        self.assertEqual(self.collect_with("compute", "true"), [])

    def test_a_selected_leg_without_reports_is_warned_about(self):
        self.leg("Benchmark_CLI_compute", "COMPUTE", "CLI", "VirtualMachineBenchmarks", 1.0)

        warnings = self.collect_with("compute", "false")

        self.assertEqual(len(warnings), 1)
        self.assertIn("PS leg of compute", warnings[0])

    def test_missing_startup_reports_are_warned_about_only_when_startup_was_selected(self):
        self.assertEqual(self.collect_with("", "false"), [])

        warnings = self.collect_with("", "true")

        self.assertEqual(len(warnings), 1)
        self.assertIn("startup leg", warnings[0])

    def test_extension_names_detection_cannot_produce_are_ignored(self):
        with captured_warnings() as warnings:
            collect.warn_about_empty_legs(self.out, {"BENCHMARK_EXTENSIONS": "../compute,Compute,compute\n"})

        self.assertEqual(warnings.getvalue(), "")


if __name__ == "__main__":
    unittest.main()
