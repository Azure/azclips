#!/usr/bin/env python3
"""Select the benchmark legs an Azure Pipelines run executes.

Runs in the Benchmark stage's DetectBenchmarks job and sets these output variables on
its step (name: detect):
  hasExtensionWork  'true' when at least one extension is benchmarked
  runStartup        'true' when the startup family runs
  extensionMatrix   matrix for the CLI and PS legs: {"<ext>": {"Extension": "<ext>"}}
  extensions        comma-separated extensions, for logs and metadata
  changeBase        what the changed files were compared with

The path rules are the former GitHub workflow's dorny/paths-filter rules, plus the
build-wide MSBuild and NuGet inputs that filter missed. The extension universe comes from
the [BenchmarkExtension("<Name>")] attribute, so adding a benchmarked extension needs no
pipeline edits.
"""

from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path

from benchmark_results import warn

REPO_ROOT = Path(__file__).resolve().parents[3]
ATTRIBUTE = re.compile(r'^\s*\[BenchmarkExtension\("([^"]*)"\)', re.MULTILINE)
EXTENSION_NAME = re.compile(r"^[A-Za-z][A-Za-z0-9]*\Z")
SOURCE_DIRECTORY = re.compile(r"^src/(?:Extensions|PS)/([^/]+)/")
COMMIT = re.compile(r"^[0-9a-f]{40}\Z")
PLACEHOLDER_MATRIX = {"none": {"Extension": "none"}}

# A change under any of these runs every extension and the startup family.
SHARED_PREFIXES = (
    "src/Core/",
    "src/Extensions/ExtensionRuntime/",
    "src/Extensions/Targets/",
    "src/PS/Common/",
    "tests/Benchmark/Auth/",
    "tests/Benchmark/Fixtures/",
    "tests/Benchmark/Telemetry/",
    "tests/Common/",
    "devops/scripts/benchmark/",
)
SHARED_FILES = frozenset({
    "tests/Benchmark/CLI/CLIBenchmarkBase.cs",
    "tests/Benchmark/CLI/BenchmarkAssemblyConfigurationService.cs",
    "tests/Benchmark/PS/PSBenchmarkBase.cs",
    "tests/Benchmark/PS/PSManifestStager.cs",
    "global.json",
    "azclips.slnx",
    "Shared.props",
    "Directory.Build.targets",
    "src/Directory.Build.props",
    "src/Extensions/Directory.Build.targets",
    "tests/Directory.Build.props",
    "NuGet.config",
    ".azure-pipelines/templates/dist/jobs/benchmark-suite.yml",
    ".azure-pipelines/templates/dist/steps/run-benchmark-leg.yml",
})
STARTUP_PREFIX = "tests/Benchmark/Startup/"


@dataclass(frozen=True)
class Selection:
    extensions: tuple[str, ...]
    startup: bool


NOTHING = Selection((), False)


def is_shared(path: str) -> bool:
    """Whether a changed path affects every benchmark."""
    if path in SHARED_FILES or path.startswith(SHARED_PREFIXES):
        return True
    parts = path.split("/")
    if parts[:2] == ["src", "CLI"] and len(parts) >= 3:
        # src/CLI/* and src/CLI/!(Help)/**, then src/CLI/Help/* and src/CLI/Help/!(Docs)/**.
        if len(parts) == 3 or parts[2] != "Help":
            return True
        return len(parts) == 4 or parts[3] != "Docs"
    if parts[:2] == ["tests", "Benchmark"] and len(parts) == 3:
        return parts[2].endswith((".cs", ".csproj"))
    return False


def discover_extensions(repo_root: Path) -> dict[str, str]:
    """Map each benchmark class file (repository-relative, '/'-separated) to its extension, lowercased."""
    files = {}
    for path in sorted((repo_root / "tests" / "Benchmark").rglob("*.cs")):
        match = ATTRIBUTE.search(path.read_text(encoding="utf-8", errors="replace"))
        if not match:
            continue
        name = match.group(1)
        if not EXTENSION_NAME.match(name):
            raise ValueError(f'{path.name}: [BenchmarkExtension("{name}")] must contain only letters and digits')
        files[path.relative_to(repo_root).as_posix()] = name.lower()
    return files


def select_all(files: dict[str, str]) -> Selection:
    return Selection(tuple(sorted(set(files.values()))), True)


def select(changed, files: dict[str, str]) -> Selection:
    """Apply the path rules to the changed files."""
    changed = [path for path in changed if path]
    if any(is_shared(path) for path in changed):
        return select_all(files)
    universe = set(files.values())
    hits = set()
    for path in changed:
        if path in files:
            hits.add(files[path])
        match = SOURCE_DIRECTORY.match(path)
        if match and match.group(1).lower() in universe:
            hits.add(match.group(1).lower())
    return Selection(tuple(sorted(hits)), any(path.startswith(STARTUP_PREFIX) for path in changed))


def plan_mode(reason: str, source_branch: str, target_branch: str) -> str:
    """'pull-request', 'main', 'all' or 'none' for an Azure Pipelines build."""
    reason = reason.lower()
    if reason == "pullrequest":
        return "pull-request" if target_branch.removeprefix("refs/heads/") == "main" else "none"
    if reason in ("individualci", "batchedci"):
        return "main" if source_branch == "refs/heads/main" else "none"
    if reason == "manual":
        # A manual run of main compares like a CI build; other branches have no base to compare with.
        return "main" if source_branch == "refs/heads/main" else "all"
    return "none"


def git(repo_root: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", "-C", str(repo_root), *args], capture_output=True, text=True)


def changed_files(repo_root: Path, base: str) -> list[str]:
    result = git(repo_root, "diff", "--name-only", "--no-renames", "-z", base, "HEAD")
    if result.returncode != 0:
        raise RuntimeError(f"git diff failed: {result.stderr.strip()}")
    return [path for path in result.stdout.split("\0") if path]


def last_succeeded_commit(environ) -> str | None:
    """The commit of the newest successful run of this pipeline on main, from the build API."""
    query = urllib.parse.urlencode({
        "definitions": environ["SYSTEM_DEFINITIONID"],
        "branchName": "refs/heads/main",
        "resultFilter": "succeeded",
        "statusFilter": "completed",
        "queryOrder": "finishTimeDescending",
        "$top": "1",
        "api-version": "7.1",
    })
    collection = environ["SYSTEM_COLLECTIONURI"].rstrip("/")
    project = urllib.parse.quote(environ["SYSTEM_TEAMPROJECT"])
    request = urllib.request.Request(f"{collection}/{project}/_apis/build/builds?{query}",
                                     headers={"Accept": "application/json"})
    if environ.get("SYSTEM_ACCESSTOKEN"):
        request.add_header("Authorization", f"Bearer {environ['SYSTEM_ACCESSTOKEN']}")
    with urllib.request.urlopen(request, timeout=30) as response:
        builds = json.load(response).get("value") or []
    commit = builds[0].get("sourceVersion") if builds else None
    return commit if isinstance(commit, str) and COMMIT.match(commit) else None


def decide(environ, repo_root: Path, find_base=last_succeeded_commit) -> tuple[Selection, str]:
    """The selection for this build, and a description of what it was compared with."""
    files = discover_extensions(repo_root)
    mode = plan_mode(environ.get("BUILD_REASON", ""), environ.get("BUILD_SOURCEBRANCH", ""),
                     environ.get("SYSTEM_PULLREQUEST_TARGETBRANCH", ""))
    if mode == "none":
        return NOTHING, "nothing, because this trigger and branch are not benchmarked"
    if mode == "all":
        return select_all(files), "nothing, because manual runs outside main benchmark everything"
    if mode == "pull-request":
        if git(repo_root, "rev-parse", "--verify", "--quiet", "HEAD^2").returncode != 0:
            warn("The pull request checkout is not a merge commit, so every benchmark runs.")
            return select_all(files), "an unknown base, so everything runs"
        return select(changed_files(repo_root, "HEAD^1"), files), "the pull request target"
    # Any failure to look up the base must not fail the stage, so every exception falls back.
    try:
        base = find_base(environ)
    except Exception as exc:
        warn(f"Could not find the last successful main run ({exc}), so every benchmark runs.")
        return select_all(files), "an unknown base, so everything runs"
    if not base or git(repo_root, "merge-base", "--is-ancestor", base, "HEAD").returncode != 0:
        warn("The last successful main run is missing or not an ancestor of this commit, so every benchmark runs.")
        return select_all(files), "an unknown base, so everything runs"
    return select(changed_files(repo_root, base), files), f"the last successful main run ({base[:7]})"


def output_lines(selection: Selection, change_base: str) -> list[str]:
    """Azure Pipelines logging commands that set the job's output variables."""
    matrix = {name: {"Extension": name} for name in selection.extensions} or PLACEHOLDER_MATRIX
    outputs = {
        "hasExtensionWork": "true" if selection.extensions else "false",
        "runStartup": "true" if selection.startup else "false",
        "extensionMatrix": json.dumps(matrix, separators=(",", ":")),
        "extensions": ",".join(selection.extensions),
        "changeBase": change_base,
    }
    return [f"##vso[task.setvariable variable={name};isOutput=true]{value}" for name, value in outputs.items()]


def main() -> int:
    selection, change_base = decide(os.environ, REPO_ROOT)
    for line in output_lines(selection, change_base):
        print(line)
    print(f"Benchmarks: extensions={','.join(selection.extensions) or 'none'}, "
          f"startup={'yes' if selection.startup else 'no'}; compared with {change_base}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
