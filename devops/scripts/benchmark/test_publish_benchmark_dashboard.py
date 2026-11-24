#!/usr/bin/env python3

import io
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

import publish_benchmark_dashboard as publish
from benchmark_fixtures import captured_warnings, data_js_entry, write_data_js, write_report
from benchmark_results import load_results, parse_data_js

SHA = "70da7c373c15b213999db4215c0bfc37f0528d40"
COMMIT_API = {
    "sha": SHA,
    "html_url": f"https://github.com/Azure/azclips/commit/{SHA}",
    "author": {"login": "teresaritorto"},
    "committer": {"login": "web-flow"},
    "commit": {
        "author": {"name": "Teresa Ritorto", "email": "tritorto@gmail.com", "date": "2026-11-11T22:49:25Z"},
        "committer": {"name": "GitHub", "email": "noreply@github.com", "date": "2026-11-11T22:49:25Z"},
        "message": "{Telemetry} Ensure client request correlation header",
        "tree": {"sha": "09b99c3c43f34894600c213e01e51b63022ca6cb"},
    },
}


def sidecar(generated):
    return json.dumps({"schema": 1, "source": {"generated": generated}}).encode("utf-8")


class CommitEntryTests(unittest.TestCase):
    def test_matches_the_shape_github_action_benchmark_stored(self):
        self.assertEqual(publish.commit_entry(COMMIT_API), {
            "author": {"email": "tritorto@gmail.com", "name": "Teresa Ritorto", "username": "teresaritorto"},
            "committer": {"email": "noreply@github.com", "name": "GitHub", "username": "web-flow"},
            "distinct": True,
            "id": SHA,
            "message": "{Telemetry} Ensure client request correlation header",
            "timestamp": "2026-11-11T22:49:25Z",
            "tree_id": "09b99c3c43f34894600c213e01e51b63022ca6cb",
            "url": f"https://github.com/Azure/azclips/commit/{SHA}",
        })

    def test_rejects_a_sha_with_a_trailing_newline(self):
        with self.assertRaisesRegex(ValueError, "no SHA"):
            publish.commit_entry({**COMMIT_API, "sha": SHA + "\n"})

    def test_formats_numbers_like_javascript(self):
        self.assertEqual(publish.js_number(1714668.4293500076), "1714668.4293500076")
        self.assertEqual(publish.js_number(5.0), "5")


class PublishTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        self.reports, self.sidecars, self.pages = root / "reports", root / "sidecars", root / "gh-pages"
        self.commit = publish.commit_entry(COMMIT_API)
        write_report(self.reports, "MANAGEDSERVICEIDENTITY", "CLI", "FederatedIdentityCredentialBenchmarks",
                     {"Get": (10.0, 1.5)})
        write_report(self.reports, "MANAGEDSERVICEIDENTITY", "CLI", "UserAssignedIdentityBenchmarks", {"Get": (20.0, 2.0)})
        write_report(self.reports, "ALL", "STARTUP", "CliWarmStartupBenchmarks", {"Config": (2.5e8, 3e6)})
        write_report(self.reports, "ALL", "STARTUP", "CliColdStartupBenchmarks", {"Config": (3.2e8, 4e6)})
        write_report(self.reports, "ALL", "STARTUP", "NewStartupBenchmarks", {"Config": (1.0, 0.0)})
        write_data_js(self.pages / "dev/bench/cli/data.js", {
            "Azclips Managedserviceidentity CLI Benchmarks": [data_js_entry("a" * 40, 5, {}, "2026-11-10T00:00:00Z")]})

    def run_publish(self, date_ms=1000):
        with captured_warnings() as warnings, redirect_stdout(io.StringIO()):
            changed = publish.publish(load_results(self.reports), self.sidecars, self.pages, self.commit, date_ms,
                                      "https://github.com/Azure/azclips")
        return changed, warnings.getvalue()

    def data(self, directory):
        return parse_data_js((self.pages / directory / "data.js").read_text(encoding="utf-8"))

    def test_appends_one_entry_per_series_with_every_report(self):
        changed, warnings = self.run_publish()

        self.assertEqual(changed, ["dev/bench/cli/data.js", "dev/bench/startup-cli/data.js"])
        cli = self.data("dev/bench/cli")
        self.assertEqual(cli["lastUpdate"], 1000)
        series = cli["entries"]["Azclips Managedserviceidentity CLI Benchmarks"]
        self.assertEqual([entry["commit"]["id"] for entry in series], ["a" * 40, SHA])
        entry = series[-1]
        self.assertEqual((entry["commit"]["id"], entry["date"], entry["tool"]), (SHA, 1000, "benchmarkdotnet"))
        self.assertEqual(entry["benches"], [
            {"name": "Benchmark.CLI.FederatedIdentityCredentialBenchmarks.Get", "value": 10.0, "unit": "ns",
             "range": "± 1.5"},
            {"name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.Get", "value": 20.0, "unit": "ns", "range": "± 2"},
        ])
        startup = self.data("dev/bench/startup-cli")
        self.assertEqual(sorted(startup["entries"]), ["Azclips CLI Startup (cold)", "Azclips CLI Startup (warm)"])
        self.assertEqual(startup["repoUrl"], "https://github.com/Azure/azclips")
        self.assertIn("NewStartupBenchmarks.Config has no dashboard series", warnings)

    def test_publishing_again_changes_nothing(self):
        self.run_publish()
        before = (self.pages / "dev/bench/cli/data.js").read_bytes()

        changed, _ = self.run_publish(date_ms=2000)

        self.assertEqual(changed, [])
        self.assertEqual((self.pages / "dev/bench/cli/data.js").read_bytes(), before)

    def test_older_results_are_not_inserted(self):
        write_data_js(self.pages / "dev/bench/cli/data.js", {
            "Azclips Managedserviceidentity CLI Benchmarks": [data_js_entry("c" * 40, 5, {}, "2026-11-12T00:00:00Z")]})

        changed, warnings = self.run_publish()

        self.assertNotIn("dev/bench/cli/data.js", changed)
        self.assertIn("already shows a later commit", warnings)

    def test_sidecars_never_replace_newer_ones(self):
        traces = self.pages / "dev/bench/traces/COMPUTE/CLI"
        traces.mkdir(parents=True)
        (traces / "CLI.Newer.json").write_bytes(sidecar("2026-11-20T00:00:00Z"))
        (traces / "CLI.Older.json").write_bytes(sidecar("2026-11-01T00:00:00Z"))
        incoming = self.sidecars / "COMPUTE/CLI"
        incoming.mkdir(parents=True)
        for name in ("CLI.Newer.json", "CLI.Older.json", "CLI.New.json"):
            (incoming / name).write_bytes(sidecar("2026-11-11T00:00:00Z"))
        (self.sidecars / "COMPUTE/notes.txt").write_text("x", encoding="utf-8")

        changed, warnings = self.run_publish()

        self.assertEqual([path for path in changed if "traces" in path],
                         ["dev/bench/traces/COMPUTE/CLI/CLI.New.json", "dev/bench/traces/COMPUTE/CLI/CLI.Older.json"])
        self.assertEqual((traces / "CLI.Newer.json").read_bytes(), sidecar("2026-11-20T00:00:00Z"))
        self.assertIn("unexpected sidecar path: 'COMPUTE/notes.txt'", warnings)

    def test_a_generated_time_with_a_trailing_newline_is_unknown(self):
        traces = self.pages / "dev/bench/traces/COMPUTE/CLI"
        traces.mkdir(parents=True)
        incoming = self.sidecars / "COMPUTE/CLI"
        incoming.mkdir(parents=True)
        (traces / "CLI.Replaced.json").write_bytes(sidecar("2026-11-20T00:00:00Z\n"))
        (incoming / "CLI.Replaced.json").write_bytes(sidecar("2026-11-11T00:00:00Z"))
        (traces / "CLI.Kept.json").write_bytes(sidecar("2026-11-01T00:00:00Z"))
        (incoming / "CLI.Kept.json").write_bytes(sidecar("2026-11-20T00:00:00Z\n"))

        changed, _ = self.run_publish()

        self.assertEqual([path for path in changed if "traces" in path],
                         ["dev/bench/traces/COMPUTE/CLI/CLI.Replaced.json"])
        self.assertEqual((traces / "CLI.Kept.json").read_bytes(), sidecar("2026-11-01T00:00:00Z"))

    def test_sidecar_paths_cannot_end_in_a_newline(self):
        # Windows can't create a file name holding a newline, so check the pattern itself.
        self.assertIsNotNone(publish.SIDECAR_PATH.match("COMPUTE/CLI/x.json"))
        self.assertIsNone(publish.SIDECAR_PATH.match("COMPUTE/CLI/x.json\n"))


if __name__ == "__main__":
    unittest.main()
