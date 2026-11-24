#!/usr/bin/env python3

import io
import json
import os
import stat
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import fetch_ado_benchmark_results as fetch
from benchmark_fixtures import captured_warnings

HEAD = "b3b44286e925f09f587c20858539aa137521f568"
MAIN = "246c7500366b0a574b3ee2185ba64cb1537ac029"
API = "https://dev.azure.com/azclitools/public/_apis/build/builds/378289"
DOWNLOAD = "https://artprodwus21.artifacts.visualstudio.com/A1/_apis/artifact/x/content?format=zip"
REPORT = "reports/COMPUTE/CLI/results/Benchmark.CLI.VirtualMachineBenchmarks-report-full-compressed.json"


def pull_request_build(**changes):
    build = {"id": 378289, "status": "completed", "reason": "pullRequest",
             "project": {"name": "public"}, "definition": {"name": "azclips-alpha-build"},
             "sourceBranch": "refs/pull/1496/merge", "sourceVersion": "f" * 40,
             "triggerInfo": {"pr.number": "1496", "pr.sourceSha": HEAD}}
    build.update(changes)
    return build


def main_build(**changes):
    build = {"id": 378438, "status": "completed", "reason": "batchedCI", "project": {"name": "public"},
             "definition": {"name": "azclips-beta-build"}, "sourceBranch": "refs/heads/main", "sourceVersion": MAIN}
    build.update(changes)
    return build


def zip_bytes(entries):
    """A zip holding entries: {name: bytes}, or {name: (bytes, unix_mode)}."""
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        for name, content in entries.items():
            data, mode = content if isinstance(content, tuple) else (content, None)
            info = zipfile.ZipInfo(name)
            if mode is not None:
                info.create_system = 3
                info.external_attr = mode << 16
            archive.writestr(info, data)
    return buffer.getvalue()


class VerifyTests(unittest.TestCase):
    def verify(self, build, expect="pull-request", head_sha=HEAD, definition="azclips-alpha-build"):
        return fetch.verify_build(build, project="public", definition=definition, expect=expect, head_sha=head_sha)

    def test_accepts_the_pull_request_run_of_the_suite(self):
        self.assertEqual(self.verify(pull_request_build()), fetch.BuildFacts(378289, HEAD, 1496))

    def test_accepts_a_main_run(self):
        facts = self.verify(main_build(), expect="main", head_sha=MAIN, definition="azclips-beta-build")

        self.assertEqual(facts, fetch.BuildFacts(378438, MAIN, None))

    def test_rejects_other_runs(self):
        cases = {
            "other project": (pull_request_build(project={"name": "release"}), "pull-request"),
            "other pipeline": (pull_request_build(definition={"name": "azclips-beta-build"}), "pull-request"),
            "still running": (pull_request_build(status="inProgress"), "pull-request"),
            "not a pull request": (pull_request_build(reason="manual"), "pull-request"),
            "other commit": (pull_request_build(triggerInfo={"pr.number": "1", "pr.sourceSha": "a" * 40}),
                             "pull-request"),
            "no number": (pull_request_build(triggerInfo={"pr.sourceSha": HEAD}), "pull-request"),
            "not main": (pull_request_build(), "main"),
        }
        for label, (build, expect) in cases.items():
            with self.subTest(label), self.assertRaises(fetch.VerificationError):
                self.verify(build, expect=expect)

    def test_rejects_a_commit_ending_in_a_newline(self):
        # No head_sha, so only the commit pattern can reject these.
        cases = {
            "pull-request": (pull_request_build(triggerInfo={"pr.number": "1496", "pr.sourceSha": HEAD + "\n"}),
                             "azclips-alpha-build"),
            "main": (main_build(sourceVersion=MAIN + "\n"), "azclips-beta-build"),
        }
        for expect, (build, definition) in cases.items():
            with self.subTest(expect), self.assertRaises(fetch.VerificationError):
                self.verify(build, expect=expect, head_sha=None, definition=definition)


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)

    def extract(self, entries, root="Benchmark_Results"):
        archive = self.root / "artifact.zip"
        archive.write_bytes(zip_bytes(entries))
        return fetch.safe_extract(archive, self.root / "out", root)

    def test_picks_the_newest_attempt(self):
        artifacts = [{"name": "Benchmark_Results"}, {"name": "Benchmark_Results_attempt2"}, {"name": "Distribution"}]

        self.assertEqual(fetch.pick_artifact(artifacts), {"name": "Benchmark_Results_attempt2"})
        self.assertIsNone(fetch.pick_artifact([{"name": "Distribution"}]))

    def test_download_urls_must_point_at_azure_devops(self):
        valid = {"resource": {"type": "PipelineArtifact", "downloadUrl": DOWNLOAD}}
        self.assertEqual(fetch.download_url(valid), DOWNLOAD)
        for url in ("http://artprodwus21.artifacts.visualstudio.com/x", "https://example.com/x", None):
            with self.subTest(url=url), self.assertRaises(fetch.VerificationError):
                fetch.download_url({"resource": {"type": "PipelineArtifact", "downloadUrl": url}})

    def test_extracts_only_expected_files(self):
        with captured_warnings() as warnings:
            files = self.extract({"Benchmark_Results/metadata.json": b"{}",
                                  f"Benchmark_Results/{REPORT}": b'{"Benchmarks": []}',
                                  "Benchmark_Results/sidecars/COMPUTE/CLI/CLI.X.List.json": b"{}",
                                  "Benchmark_Results/run.sh": b"echo hi"})

        self.assertEqual(files, ["metadata.json", REPORT, "sidecars/COMPUTE/CLI/CLI.X.List.json"])
        self.assertTrue((self.root / "out" / REPORT).is_file())
        self.assertFalse((self.root / "out" / "run.sh").exists())
        self.assertIn("Ignoring an unexpected file in Benchmark_Results: 'run.sh'", warnings.getvalue())

    def test_ignores_names_ending_in_a_newline(self):
        for name in ("metadata.json", "summary.md", REPORT, "sidecars/COMPUTE/CLI/CLI.X.List.json"):
            entry = name + "\n"
            with self.subTest(name=name):
                with captured_warnings() as warnings:
                    files = self.extract({f"Benchmark_Results/{entry}": b"{}"})

                self.assertEqual(files, [])
                self.assertFalse((self.root / "out" / entry).exists())
                self.assertIn(f"Ignoring an unexpected file in Benchmark_Results: {entry!r}", warnings.getvalue())

    def test_rejects_unsafe_archives(self):
        cases = {
            "parent directory": {"Benchmark_Results/../evil.json": b"x"},
            "absolute path": {"/Benchmark_Results/metadata.json": b"x"},
            "backslash": {"Benchmark_Results\\..\\evil.json": b"x"},
            "other root": {"Other/metadata.json": b"x"},
            "symbolic link": {"Benchmark_Results/metadata.json": (b"/etc/passwd", stat.S_IFLNK | 0o777)},
        }
        for label, entries in cases.items():
            with self.subTest(label), self.assertRaises(fetch.UnsafeArchiveError):
                self.extract(entries)

    def test_rejects_oversized_content(self):
        with mock.patch.object(fetch, "MAX_FILE_BYTES", 10), self.assertRaises(fetch.UnsafeArchiveError):
            self.extract({"Benchmark_Results/metadata.json": b"x" * 11})
        with mock.patch.object(fetch, "MAX_DOWNLOAD_BYTES", 10), self.assertRaises(fetch.UnsafeArchiveError):
            fetch.download(DOWNLOAD, self.root / "big.zip", lambda request, timeout: io.BytesIO(b"x" * 11))


class TimelineTests(unittest.TestCase):
    def test_reports_the_stage_result_and_its_failed_jobs(self):
        timeline = {"records": [
            {"id": "s1", "type": "Stage", "identifier": "Benchmark", "result": "failed"},
            {"id": "p1", "parentId": "s1", "type": "Phase"},
            {"id": "j1", "parentId": "p1", "type": "Job", "name": "CLI Benchmarks compute", "result": "failed"},
            {"id": "j2", "parentId": "p1", "type": "Job", "name": "Startup Benchmarks", "result": "canceled"},
            {"id": "j3", "parentId": "p1", "type": "Job", "name": "PS Benchmarks compute", "result": "succeeded"},
            {"id": "s2", "type": "Stage", "identifier": "Test", "result": "failed"},
            {"id": "p2", "parentId": "s2", "type": "Phase"},
            {"id": "j4", "parentId": "p2", "type": "Job", "name": "Unit Tests (Linux)", "result": "failed"},
        ]}

        self.assertEqual(fetch.benchmark_stage_status(timeline),
                         ("failed", ["CLI Benchmarks compute", "Startup Benchmarks"]))
        self.assertEqual(fetch.benchmark_stage_status({"records": []}), (None, []))


class MainTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.out = Path(temp.name) / "benchmark"
        self.github_output = Path(temp.name) / "github_output"
        self.github_output.write_text("", encoding="utf-8")

    def run_main(self, responses, archive=b""):
        def opener(request, timeout):
            self.assertEqual(request.full_url, DOWNLOAD)
            return io.BytesIO(archive)

        arguments = ["--organization-url", "https://dev.azure.com/azclitools/", "--project", "public",
                     "--definition", "azclips-alpha-build", "--expect", "pull-request", "--build-id", "378289",
                     "--head-sha", HEAD, "--out", str(self.out)]
        with mock.patch.dict(os.environ, {"GITHUB_OUTPUT": str(self.github_output)}), \
                redirect_stdout(io.StringIO()) as stdout:
            code = fetch.main(arguments, fetch=lambda url: responses[url], opener=opener)
        return code, stdout.getvalue()

    def responses(self, artifacts):
        return {
            f"{API}?api-version=7.1": pull_request_build(),
            f"{API}/artifacts?api-version=7.1": {"value": artifacts},
            f"{API}/timeline?api-version=7.1": {"records": [
                {"id": "s1", "type": "Stage", "identifier": "Benchmark", "result": "succeeded"}]},
        }

    def test_writes_build_info_results_and_step_outputs(self):
        artifacts = [{"name": "Benchmark_Results", "resource": {"type": "PipelineArtifact", "downloadUrl": DOWNLOAD}}]

        code, _ = self.run_main(self.responses(artifacts), zip_bytes({"Benchmark_Results/metadata.json": b"{}"}))

        self.assertEqual(code, 0)
        self.assertEqual(json.loads((self.out / "build.json").read_text(encoding="utf-8")), {
            "buildId": 378289,
            "buildUrl": "https://dev.azure.com/azclitools/public/_build/results?buildId=378289",
            "commit": HEAD,
            "pullRequest": 1496,
            "stageResult": "succeeded",
            "failedJobs": [],
            "available": True,
        })
        self.assertTrue((self.out / "results" / "metadata.json").is_file())
        self.assertFalse((self.out / "artifact.zip").exists())
        self.assertEqual(self.github_output.read_text(encoding="utf-8"),
                         f"available=true\ncommit={HEAD}\npr_number=1496\n")

    def test_a_run_without_results_is_reported_unavailable(self):
        code, stdout = self.run_main(self.responses([]))

        self.assertEqual(code, 0)
        self.assertIn("nothing was benchmarked", stdout)
        self.assertIn("available=false", self.github_output.read_text(encoding="utf-8"))

    def test_a_rejected_run_fails_without_downloading(self):
        responses = self.responses([])
        responses[f"{API}?api-version=7.1"] = pull_request_build(reason="manual")

        code, stdout = self.run_main(responses)

        self.assertEqual(code, 1)
        self.assertIn("::error::Azure DevOps run 378289 was rejected", stdout)
        self.assertFalse(self.out.exists())


if __name__ == "__main__":
    unittest.main()
