import os
import subprocess
import tempfile
import unittest
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


if __name__ == "__main__":
    unittest.main()
