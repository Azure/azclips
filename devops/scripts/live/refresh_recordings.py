#!/usr/bin/env python3
"""Refresh stale scenario recordings.

Playback every scenario first, re-record only the classes that failed, then
verify the rewritten recordings replay and pass the secret scan. The scheduled
workflow around this script is a thin wrapper; the refresh logic lives here so
it runs the same from a runner or a developer machine.

Assumes the solution is built and, unless --playback-only, a profile with a
subscription is signed in (on the runner: az login --identity with an explicit
subscription id, managed identity login does not resolve one).
"""

import argparse
import os
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

RECORD_MODE_VARIABLE = "AZURE_TEST_MODE"
# Recordings live in a Records folder next to the module that owns the test,
# tests/Scenario.Tests/<Module>/Records/<TestClass>/<TestName>.json.
SCENARIO_TESTS = Path("tests/Scenario.Tests")


def run_tests(repo_root, results_dir, test_filter=None, record=False):
    """Run the scenario suite, return the set of failed test class names."""
    trx = results_dir / ("record.trx" if record else "playback.trx")
    command = [
        "dotnet", "test", "--project", "tests/Scenario.Tests/Scenario.Tests.csproj",
        "--report-xunit-trx", "--report-xunit-trx-filename", trx.name,
        "--results-directory", str(results_dir),
    ]
    if test_filter:
        command += ["--filter-class", f"*{test_filter}*"]

    environment = os.environ.copy()
    environment.pop(RECORD_MODE_VARIABLE, None)
    if record:
        environment[RECORD_MODE_VARIABLE] = "Record"

    trx.unlink(missing_ok=True)
    result = subprocess.run(command, cwd=repo_root, env=environment)
    # MTP uses exit 2 for test failures; other failures must not trigger live recording.
    if result.returncode not in (0, 2):
        raise SystemExit(f"scenario test runner failed with exit code {result.returncode}")
    return parse_failed_classes(trx)


def parse_failed_classes(trx):
    if not trx.exists():
        raise SystemExit(f"test results not written: {trx}")
    namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    failed = set()
    for result in ET.parse(trx).getroot().iter(f"{{{namespace['t']}}}UnitTestResult"):
        if result.get("outcome") == "Failed":
            # testName is Namespace.Class.Method; nested classes do not occur here
            failed.add(result.get("testName").split(".")[-2])
    return failed


def changed_recordings(repo_root):
    output = subprocess.run(
        ["git", "status", "--porcelain", "--", str(SCENARIO_TESTS)],
        cwd=repo_root, capture_output=True, text=True, check=True).stdout
    paths = [line[3:].strip() for line in output.splitlines() if line]
    return [path for path in paths if "/Records/" in path.replace("\\", "/")]


def scan(repo_root, files):
    return subprocess.run(
        [sys.executable, "devops/scripts/ci/scan_recordings.py", *files],
        cwd=repo_root).returncode == 0


def write_summary(repo_root, lines):
    text = "\n".join(lines) + "\n"
    print(text)
    (repo_root / "refresh-summary.md").write_text(text, encoding="utf-8")
    step_summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if step_summary:
        with open(step_summary, "a", encoding="utf-8") as handle:
            handle.write(text)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--filter", default="", help="only consider scenario classes whose name contains this")
    parser.add_argument("--playback-only", action="store_true", help="report stale scenarios without re-recording")
    arguments = parser.parse_args()

    repo_root = Path(subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        capture_output=True, text=True, check=True).stdout.strip())
    recorded_classes = {path.name for path in (repo_root / SCENARIO_TESTS).glob("*/Records/*") if path.is_dir()}
    results_dir = repo_root / "artifacts" / "refresh-results"

    failed = run_tests(repo_root, results_dir, arguments.filter or None)
    stale = sorted(failed & recorded_classes)
    unrefreshable = sorted(failed - recorded_classes)

    if not failed:
        write_summary(repo_root, ["Recording refresh: every scenario replayed, nothing is stale."])
        return 0

    if arguments.playback_only:
        write_summary(repo_root, [
            "Recording refresh, playback only:",
            *[f"- stale: {name}" for name in stale],
            *[f"- failed without a recording: {name}" for name in unrefreshable],
        ])
        return 1

    still_failing = []
    for name in stale:
        if run_tests(repo_root, results_dir, name, record=True) or run_tests(repo_root, results_dir, name):
            still_failing.append(name)

    changed = changed_recordings(repo_root)
    scan_clean = scan(repo_root, changed) if changed else True

    refreshed = [name for name in stale if name not in still_failing]
    write_summary(repo_root, [
        "Recording refresh:",
        *[f"- refreshed: {name}" for name in refreshed],
        *[f"- still failing after re-record: {name}" for name in still_failing],
        *[f"- failed without a recording, needs a human: {name}" for name in unrefreshable],
        f"- rewritten files: {len(changed)}",
        f"- secret scan: {'clean' if scan_clean else 'FINDINGS, do not merge'}",
    ])

    return 1 if still_failing or unrefreshable or not scan_clean else 0


if __name__ == "__main__":
    sys.exit(main())
