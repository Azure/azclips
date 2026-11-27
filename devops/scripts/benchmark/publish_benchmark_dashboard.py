#!/usr/bin/env python3
"""Add one main commit's benchmark results to the gh-pages dashboards.

Appends an entry to dev/bench/<dir>/data.js in github-action-benchmark's format and
under the series names it used, so the existing charts continue, and merges trace
sidecars into dev/bench/traces. Publishing a commit again changes nothing, and results
older than what a series already shows are not inserted, so charts stay in order.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from benchmark_results import BenchmarkResult, format_data_js, load_results, parse_data_js, series_for, warn

TOOL = "benchmarkdotnet"
COMMIT = re.compile(r"^[0-9a-f]{40}\Z")
SIDECAR_PATH = re.compile(r"^[A-Z0-9]+/(?:CLI|PS|STARTUP)/[A-Za-z0-9._-]+\.json\Z")
GENERATED = re.compile(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z\Z")


def commit_entry(api: dict) -> dict:
    """The commit object github-action-benchmark stores, from GET /repos/{owner}/{repo}/commits/{sha}."""
    if not isinstance(api.get("sha"), str) or not COMMIT.match(api["sha"]):
        raise ValueError("the commit response has no SHA")
    commit = api.get("commit") or {}

    def person(git_person: dict, account) -> dict:
        return {"email": git_person.get("email"), "name": git_person.get("name"),
                "username": (account or {}).get("login")}

    return {
        "author": person(commit.get("author") or {}, api.get("author")),
        "committer": person(commit.get("committer") or {}, api.get("committer")),
        "distinct": True,
        "id": api["sha"],
        "message": commit.get("message", ""),
        "timestamp": (commit.get("committer") or {}).get("date"),
        "tree_id": (commit.get("tree") or {}).get("sha"),
        "url": api.get("html_url"),
    }


def js_number(value: float) -> str:
    """Format a number as JavaScript's String(number) does for these values."""
    return str(int(value)) if float(value).is_integer() and abs(value) < 1e21 else repr(float(value))


def bench(result: BenchmarkResult) -> dict:
    return {"name": result.full_name, "value": result.mean_ns, "unit": "ns",
            "range": f"± {js_number(result.stddev_ns)}"}


def _time(value) -> datetime | None:
    if not isinstance(value, str):
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


def add_entries(data: dict, series_results: dict, commit: dict, date_ms: int) -> bool:
    """Append this commit to each series of one data.js. Returns whether anything changed."""
    changed = False
    commit_time = _time(commit.get("timestamp"))
    for name, results in series_results.items():
        entries = data["entries"].setdefault(name, [])
        published = [entry.get("commit") or {} for entry in entries if isinstance(entry, dict)]
        if any(existing.get("id") == commit["id"] for existing in published):
            print(f"{name}: {commit['id'][:7]} is already published.")
            continue
        later = [time for time in (_time(existing.get("timestamp")) for existing in published) if time]
        if commit_time and any(time > commit_time for time in later):
            warn(f"{name} already shows a later commit, so {commit['id'][:7]} is not inserted.")
            continue
        entries.append({"commit": commit, "date": date_ms, "tool": TOOL, "benches": [bench(r) for r in results]})
        changed = True
    if changed:
        data["lastUpdate"] = date_ms
    return changed


def _generated(content: bytes) -> str:
    """When a sidecar was generated, as sortable UTC text, or '' when unknown."""
    try:
        value = json.loads(content.decode("utf-8")).get("source", {}).get("generated", "")
    except (ValueError, AttributeError):
        return ""
    return value if isinstance(value, str) and GENERATED.match(value) else ""


def merge_sidecars(source: Path, traces: Path) -> list[str]:
    """Copy trace sidecars into traces, keeping any newer ones. Returns the paths written."""
    written = []
    if not source.is_dir():
        return written
    for path in sorted(item for item in source.rglob("*") if item.is_file()):
        relative = path.relative_to(source).as_posix()
        if not SIDECAR_PATH.match(relative):
            warn(f"Skipping an unexpected sidecar path: {relative!r}")
            continue
        content = path.read_bytes()
        target = traces / relative
        if target.is_file():
            existing = target.read_bytes()
            if existing == content or _generated(existing) > _generated(content):
                continue
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
        written.append(f"dev/bench/traces/{relative}")
    return written


def publish(results, sidecars: Path, gh_pages: Path, commit: dict, date_ms: int, repo_url: str) -> list[str]:
    """Update a gh-pages checkout in place. Returns the paths that changed."""
    by_file: dict[str, dict[str, list[BenchmarkResult]]] = {}
    for result in results:
        series = series_for(result)
        if series is None:
            warn(f"{result.full_name} has no dashboard series, so it is not published.")
            continue
        directory, name = series
        by_file.setdefault(directory, {}).setdefault(name, []).append(result)

    changed = []
    for directory, series_results in sorted(by_file.items()):
        path = gh_pages / directory / "data.js"
        if path.is_file():
            data = parse_data_js(path.read_text(encoding="utf-8"))
        else:
            data = {"lastUpdate": date_ms, "repoUrl": repo_url, "entries": {}}
        if add_entries(data, series_results, commit, date_ms):
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("w", encoding="utf-8", newline="") as handle:
                handle.write(format_data_js(data))
            changed.append(f"{directory}/data.js")
    return changed + merge_sidecars(sidecars, gh_pages / "dev" / "bench" / "traces")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Publish one commit's benchmark results to gh-pages.")
    parser.add_argument("--results", required=True, type=Path, help="extracted Benchmark_Results directory")
    parser.add_argument("--gh-pages", required=True, type=Path, help="gh-pages checkout to update")
    parser.add_argument("--commit-json", required=True, type=Path, help="GitHub commits API response")
    parser.add_argument("--repo-url", required=True, help="repository URL recorded in new data.js files")
    args = parser.parse_args(argv)

    commit = commit_entry(json.loads(args.commit_json.read_text(encoding="utf-8")))
    changed = publish(load_results(args.results / "reports"), args.results / "sidecars", args.gh_pages,
                      commit, int(time.time() * 1000), args.repo_url)
    print(f"Updated {len(changed)} file(s) for {commit['id'][:7]}: {', '.join(changed) or 'none'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
