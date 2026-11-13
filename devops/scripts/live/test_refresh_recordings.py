import os
import subprocess
import tempfile
import unittest
from contextlib import ExitStack
from pathlib import Path
from unittest.mock import patch

import refresh_recordings


class RunTestsTests(unittest.TestCase):
    def test_mtp_command_reports_failed_classes_and_preserves_record_opt_in(self):
        report = """<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Scenario.Tests.GroupCreateScenarioTests.CreatesGroup" outcome="Failed" />
            <UnitTestResult testName="Scenario.Tests.GroupReadScenarioTests.ReadsGroup" outcome="Passed" />
          </Results>
        </TestRun>"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for record in (False, True):
                with self.subTest(record=record):
                    filename = "record.trx" if record else "playback.trx"

                    def run(command, *, cwd, env):
                        self.assertEqual(root, cwd)
                        self.assertEqual([
                            "dotnet", "test", "--project", "tests/Scenario.Tests/Scenario.Tests.csproj",
                            "--report-xunit-trx", "--report-xunit-trx-filename", filename,
                            "--results-directory", str(root),
                            "--filter-class", "*GroupCreate*",
                        ], command)
                        self.assertEqual("Record" if record else None, env.get("AZURE_TEST_MODE"))
                        (root / filename).write_text(report, encoding="utf-8")
                        return subprocess.CompletedProcess(command, 2)

                    with patch.dict(os.environ, {"AZURE_TEST_MODE": "Record"}), \
                            patch.object(refresh_recordings.subprocess, "run", side_effect=run):
                        self.assertEqual(
                            {"GroupCreateScenarioTests"},
                            refresh_recordings.run_tests(root, root, "GroupCreate", record=record),
                        )

    def test_runner_error_cannot_reuse_a_stale_report(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            stale = root / "playback.trx"
            stale.write_text("<TestRun />", encoding="utf-8")
            with patch.object(refresh_recordings.subprocess, "run",
                              return_value=subprocess.CompletedProcess([], 5)):
                with self.assertRaisesRegex(SystemExit, "runner failed with exit code 5"):
                    refresh_recordings.run_tests(root, root)
            self.assertFalse(stale.exists())

    def test_success_without_a_report_fails_explicitly(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch.object(refresh_recordings.subprocess, "run",
                              return_value=subprocess.CompletedProcess([], 0)):
                with self.assertRaisesRegex(SystemExit, "test results not written"):
                    refresh_recordings.run_tests(root, root)


class MainTests(unittest.TestCase):
    def setUp(self):
        context = ExitStack()
        self.addCleanup(context.close)
        self.root = Path(context.enter_context(tempfile.TemporaryDirectory()))
        self.output = self.root / "github-output"
        self.classes = {"AlphaScenarioTests", "BetaScenarioTests"}
        for name in self.classes:
            (self.root / refresh_recordings.SCENARIO_TESTS / "Resources" / "Records" / name).mkdir(parents=True)
        context.enter_context(patch.dict(os.environ, {
            "GITHUB_OUTPUT": str(self.output),
            "GITHUB_STEP_SUMMARY": str(self.root / "step-summary"),
        }))
        context.enter_context(patch.object(refresh_recordings.sys, "argv", ["refresh_recordings.py"]))
        context.enter_context(patch.object(
            refresh_recordings.subprocess, "run",
            return_value=subprocess.CompletedProcess([], 0, stdout=str(self.root)),
        ))
        self.run_tests = context.enter_context(patch.object(
            refresh_recordings, "run_tests",
            side_effect=[self.classes, set(), set(), SystemExit("runner failed with exit code 5")],
        ))
        self.changed = ["tests/Scenario.Tests/Resources/Records/AlphaScenarioTests/test.json"]
        self.changed_recordings = context.enter_context(patch.object(
            refresh_recordings, "changed_recordings", return_value=self.changed,
        ))
        self.scan = context.enter_context(patch.object(refresh_recordings, "scan", return_value=True))

    def test_interrupted_refresh_scans_partial_changes_without_suppressing_runner_error(self):
        with self.assertRaisesRegex(SystemExit, "runner failed with exit code 5"):
            refresh_recordings.main()

        self.scan.assert_called_once_with(self.root, self.changed)
        summary = (self.root / "refresh-summary.md").read_text(encoding="utf-8")
        self.assertIn("- refreshed: AlphaScenarioTests", summary)
        self.assertNotIn("- refreshed: BetaScenarioTests", summary)
        self.assertIn("refresh interrupted", summary)
        self.assertIn("- secret scan: clean", summary)
        self.assertEqual("scan_clean=true\n", self.output.read_text(encoding="utf-8"))

    def test_scan_findings_block_publishing_after_interruption(self):
        self.scan.return_value = False
        with self.assertRaisesRegex(SystemExit, "runner failed with exit code 5"):
            refresh_recordings.main()

        self.assertIn("FINDINGS, do not merge", (self.root / "refresh-summary.md").read_text(encoding="utf-8"))
        self.assertEqual("scan_clean=false\n", self.output.read_text(encoding="utf-8"))

    def test_scanner_error_never_authorizes_publishing(self):
        self.scan.side_effect = OSError("scanner unavailable")
        with self.assertRaisesRegex(OSError, "scanner unavailable"):
            refresh_recordings.main()

        self.assertFalse(self.output.exists())

    def test_initial_playback_error_never_authorizes_publishing(self):
        self.run_tests.side_effect = SystemExit("runner failed with exit code 5")
        with self.assertRaisesRegex(SystemExit, "runner failed with exit code 5"):
            refresh_recordings.main()

        self.changed_recordings.assert_not_called()
        self.scan.assert_not_called()
        self.assertFalse(self.output.exists())

    def test_completed_refresh_publishes_only_after_scan_and_summary(self):
        self.run_tests.side_effect = [self.classes, set(), set(), set(), set()]

        def scan_before_publication(*_):
            self.assertFalse(self.output.exists())
            return True

        self.scan.side_effect = scan_before_publication
        self.assertEqual(0, refresh_recordings.main())

        self.scan.assert_called_once_with(self.root, self.changed)
        summary = (self.root / "refresh-summary.md").read_text(encoding="utf-8")
        self.assertIn("- refreshed: AlphaScenarioTests", summary)
        self.assertIn("- refreshed: BetaScenarioTests", summary)
        self.assertNotIn("refresh interrupted", summary)
        self.assertEqual("scan_clean=true\n", self.output.read_text(encoding="utf-8"))

    def test_summary_error_never_authorizes_publishing(self):
        self.run_tests.side_effect = [self.classes, set(), set(), set(), set()]
        with patch.object(refresh_recordings, "write_summary", side_effect=OSError("cannot write summary")):
            with self.assertRaisesRegex(OSError, "cannot write summary"):
                refresh_recordings.main()

        self.scan.assert_called_once_with(self.root, self.changed)
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
