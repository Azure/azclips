#!/usr/bin/env python3
"""Render benchmark results compared with main's latest results on gh-pages.

--format summary  Markdown for the Azure DevOps build summary.
--format comment  The pull request comment posted by benchmark-comment.yml.

Results can come from a fork's pull request build, so only validated benchmark names
and numbers are rendered; no free text from a build reaches the output.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path

from benchmark_results import display_label, is_finite_number, is_informational, load_results, parse_data_js, warn

COMMENT_MARKER = "<!-- benchmark-results -->"
MAX_COMMENT_CHARS = 60_000
DASHBOARD_URL = "https://azure.github.io/azclips/dev/bench"
DATA_DIRS = ("cli", "ps", "startup-cli", "startup-ps")
SECTIONS = (("CLI", "CLI"), ("PS", "PowerShell"), ("STARTUP", "Startup"))
PASSING_RESULTS = ("succeeded", "succeededWithIssues")
COMMIT = re.compile(r"^[0-9a-f]{40}\Z")
BUILD_URL = re.compile(r"^https://dev\.azure\.com/[A-Za-z0-9._~%/-]+/_build/results\?buildId=[0-9]+\Z")
UNSAFE_JOB_CHARACTERS = re.compile(r"[^A-Za-z0-9 _().-]")
UNITS = ((1e9, "s", 2), (1e6, "ms", 1), (1e3, "µs", 1), (1.0, "ns", 1))


@dataclass(frozen=True)
class Row:
    product: str
    extension: str
    label: str
    current: str
    baseline: str
    change: str
    regressed: bool


def _unit(nanoseconds: float) -> tuple[float, str, int]:
    for unit in UNITS:
        if nanoseconds >= unit[0]:
            return unit
    return UNITS[-1]


def format_duration(nanoseconds: float) -> str:
    scale, unit, digits = _unit(nanoseconds)
    return f"{nanoseconds / scale:,.{digits}f} {unit}"


def format_measurement(mean: float, stddev: float) -> str:
    """Mean ± standard deviation, both in the mean's unit."""
    scale, unit, digits = _unit(mean)
    return f"{mean / scale:,.{digits}f} {unit} ± {stddev / scale:,.{digits}f} {unit}"


def load_baseline(bench_dir: Path) -> dict[str, float]:
    """The latest main value of every benchmark across the dashboards' data.js files."""
    latest: dict[str, tuple[float, float]] = {}
    for directory in DATA_DIRS:
        path = Path(bench_dir) / directory / "data.js"
        if not path.is_file():
            continue
        try:
            data = parse_data_js(path.read_text(encoding="utf-8"))
        except ValueError as exc:
            warn(f"Ignoring the {directory} baseline: {exc}")
            continue
        for entries in data["entries"].values():
            for entry in entries if isinstance(entries, list) else []:
                date = entry.get("date") if isinstance(entry, dict) else None
                if not is_finite_number(date):
                    continue
                for bench in entry.get("benches") or []:
                    name = bench.get("name") if isinstance(bench, dict) else None
                    value = bench.get("value") if isinstance(bench, dict) else None
                    if isinstance(name, str) and is_finite_number(value) and value > 0:
                        if name not in latest or date >= latest[name][0]:
                            latest[name] = (date, float(value))
    return {name: value for name, (_, value) in latest.items()}


def build_rows(results, baseline: dict[str, float], threshold: float) -> list[Row]:
    """One row per result, flagging changes above threshold percent (rounded as shown)."""
    rows = []
    for result in results:
        informational = is_informational(result)
        previous = baseline.get(result.full_name)
        regressed = False
        if previous:
            change = f"{(result.mean_ns - previous) / previous * 100:+.1f}%"
            regressed = not informational and float(change[:-1]) > threshold
            if regressed:
                change += " ⚠️"
            elif informational:
                change += " (informational)"
            previous_text = format_duration(previous)
        else:
            change, previous_text = "n/a", "new"
        rows.append(Row(result.product, result.extension, display_label(result.full_name),
                        format_measurement(result.mean_ns, result.stddev_ns), previous_text, change, regressed))
    return sorted(rows, key=lambda row: (row.product, row.extension, row.label))


def safe_job_name(name: object) -> str:
    """A job name reduced to characters that cannot change the Markdown around it."""
    return UNSAFE_JOB_CHARACTERS.sub("", name)[:100].strip() if isinstance(name, str) else ""


def _build_url(info: dict) -> str:
    url = info.get("buildUrl")
    return url if isinstance(url, str) and BUILD_URL.match(url) else ""


def _context_line(info: dict) -> str:
    parts = []
    commit = info.get("commit")
    if isinstance(commit, str) and COMMIT.match(commit):
        parts.append(f"Commit `{commit[:7]}`")
    build_id, url = info.get("buildId"), _build_url(info)
    if isinstance(build_id, int) and not isinstance(build_id, bool) and url:
        parts.append(f"[ADO build {build_id}]({url})")
    parts.append(f"compared with the latest `main` results on the [CLI]({DASHBOARD_URL}/cli/) "
                 f"and [PS]({DASHBOARD_URL}/ps/) dashboards")
    line = " · ".join(parts)
    return line[0].upper() + line[1:]


def _passed(info: dict) -> bool:
    result = info.get("stageResult")
    if result is None:
        return not info.get("failedJobs")
    return result in PASSING_RESULTS


def render(rows: list[Row], info: dict, *, fmt: str, threshold: float, max_chars: int = MAX_COMMENT_CHARS) -> str:
    comment = fmt == "comment"
    title = "Benchmark results"
    if comment:
        title = ("🟢 " if _passed(info) else "🔴 ") + title
    head = [f"## {title}", "", _context_line(info), ""]
    regressions = sum(row.regressed for row in rows)
    if not rows:
        head += ["No benchmark results were produced.", ""]
    elif regressions:
        noun = "benchmark is" if regressions == 1 else "benchmarks are"
        head += [f"> ⚠️ **{regressions} {noun} more than {threshold:g}% slower than `main`.**", ""]
    else:
        head += [f"> ✅ No benchmark is more than {threshold:g}% slower than `main`.", ""]

    tail = []
    if comment:
        jobs = [job for job in (safe_job_name(name) for name in info.get("failedJobs") or []) if job]
        if jobs:
            tail += ["Failed benchmark jobs: " + ", ".join(f"`{job}`" for job in jobs), ""]
        url = _build_url(info)
        if url:
            tail.append(f"Per-span telemetry comparison: [build summary]({url}&view=ms.vss-build-web.run-extensions-tab)")
        tail.append(COMMENT_MARKER)

    column = "This PR" if comment else "This run"
    budget = max_chars - len("\n".join(head + tail)) - 200
    body, used, omitted = [], 0, 0
    for product, heading in SECTIONS:
        section = [row for row in rows if row.product == product]
        if not section:
            continue
        header = [f"### {heading}", "", f"| Benchmark | {column} | main | Change |", "| --- | --- | --- | --- |"]
        cost = sum(len(line) + 1 for line in header) + 1
        lines = []
        for row in section:
            line = f"| `{row.label}` | {row.current} | {row.baseline} | {row.change} |"
            if used + cost + len(line) + 1 > budget:
                omitted += 1
                continue
            lines.append(line)
            cost += len(line) + 1
        if lines:
            body += header + lines + [""]
            used += cost
    if omitted:
        body += [f"_{omitted} more benchmarks are not shown here; see the build summary._", ""]
    return "\n".join(head + body + tail) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Compare benchmark results with main.")
    parser.add_argument("--results", required=True, type=Path,
                        help="reports laid out as <EXT>/<PRODUCT>/results/*-report-full-compressed.json")
    parser.add_argument("--baseline", required=True, type=Path, help="gh-pages dev/bench directory")
    parser.add_argument("--build-info", required=True, type=Path,
                        help="JSON with commit, buildId, buildUrl, stageResult and failedJobs")
    parser.add_argument("--format", required=True, choices=("summary", "comment"))
    parser.add_argument("--threshold", type=float, default=15.0, help="regression threshold in percent")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)

    info = json.loads(args.build_info.read_text(encoding="utf-8"))
    rows = build_rows(load_results(args.results), load_baseline(args.baseline), args.threshold)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(render(rows, info, fmt=args.format, threshold=args.threshold), encoding="utf-8")
    print(f"Wrote the {args.format} for {len(rows)} benchmark(s) to {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
