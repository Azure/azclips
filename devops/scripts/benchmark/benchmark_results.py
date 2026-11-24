#!/usr/bin/env python3
"""Shared helpers for the benchmark pipeline scripts.

Benchmark results use the layout the runner writes with --ci:
<root>/<EXTENSION>/<PRODUCT>/results/<Namespace.Class>-report-full-compressed.json

Standard library only, so the scripts run on bare CI agents.
"""

from __future__ import annotations

import json
import math
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path

PRODUCTS = ("CLI", "PS", "STARTUP")
REPORT_SUFFIX = "-report-full-compressed.json"
MAX_REPORT_BYTES = 64 * 1024 * 1024
DATA_JS_PREFIX = "window.BENCHMARK_DATA = "

EXTENSION_SEGMENT = re.compile(r"^[A-Z0-9]+\Z")
BENCHMARK_NAME = re.compile(r"^[A-Za-z0-9_.]{1,300}\Z")
ATTEMPT_SUFFIX = re.compile(r"^(?P<base>.+)_attempt(?P<attempt>[0-9]+)\Z")
DATA_JS = re.compile(r"window\.BENCHMARK_DATA\s*=\s*(\{.*\})\s*;?\s*\Z", re.DOTALL)

# Startup class -> (gh-pages data directory, dashboard series, informational only).
# Cold startup is tracked for information and never flagged as a regression.
STARTUP_SERIES = {
    "CliWarmStartupBenchmarks": ("dev/bench/startup-cli", "Azclips CLI Startup (warm)", False),
    "CliColdStartupBenchmarks": ("dev/bench/startup-cli", "Azclips CLI Startup (cold)", True),
    "PsWarmStartupBenchmarks": ("dev/bench/startup-ps", "Azclips PS Startup (warm)", False),
    "PsColdStartupBenchmarks": ("dev/bench/startup-ps", "Azclips PS Startup (cold)", True),
}


@dataclass(frozen=True)
class BenchmarkResult:
    extension: str  # leg extension segment, e.g. COMPUTE; ALL for startup
    product: str  # CLI, PS or STARTUP
    full_name: str  # BenchmarkDotNet FullName
    mean_ns: float
    stddev_ns: float


def warn(message: object) -> None:
    """Report a non-fatal problem in the log format of the CI system running the script."""
    text = " ".join(str(message).split()).replace("##vso[", "##vso_[")
    if os.environ.get("TF_BUILD"):
        print(f"##vso[task.logissue type=warning]{text}", flush=True)
    elif os.environ.get("GITHUB_ACTIONS"):
        print(f"::warning::{text}", flush=True)
    else:
        print(f"warning: {text}", file=sys.stderr, flush=True)


def is_finite_number(value: object) -> bool:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:  # an int too large for a float
        return False


def latest_attempts(names) -> dict[str, str]:
    """Map each artifact base name to the artifact of its newest attempt.

    templates/dist/steps/publish-artifact.yml publishes a job's first attempt as <name>
    and a rerun's attempt N as <name>_attempt<N>.
    """
    chosen: dict[str, tuple[int, str]] = {}
    for name in names:
        match = ATTEMPT_SUFFIX.match(name)
        base, attempt = (match["base"], int(match["attempt"])) if match else (name, 1)
        if base not in chosen or attempt > chosen[base][0]:
            chosen[base] = (attempt, name)
    return {base: name for base, (_, name) in chosen.items()}


def load_results(root) -> list[BenchmarkResult]:
    """Load every benchmark under root, in report-file order.

    A malformed report, or a benchmark without usable statistics (one that failed), is
    skipped with a warning so the remaining results still count.
    """
    root = Path(root)
    results: list[BenchmarkResult] = []
    if not root.is_dir():
        return results
    for extension_dir in sorted(path for path in root.iterdir() if path.is_dir()):
        if not EXTENSION_SEGMENT.match(extension_dir.name):
            continue
        for product in PRODUCTS:
            for report in sorted((extension_dir / product / "results").glob("*" + REPORT_SUFFIX)):
                results.extend(_load_report(report, extension_dir.name, product))
    return results


def _load_report(path: Path, extension: str, product: str) -> list[BenchmarkResult]:
    try:
        if path.stat().st_size > MAX_REPORT_BYTES:
            raise ValueError("the report is too large")
        data = json.loads(path.read_text(encoding="utf-8"))
        benchmarks = data.get("Benchmarks") if isinstance(data, dict) else None
        if not isinstance(benchmarks, list):
            raise ValueError("the report has no Benchmarks array")
    except (OSError, ValueError, RecursionError) as exc:
        warn(f"Skipping benchmark report {path.name}: {exc}")
        return []

    results = []
    for benchmark in benchmarks:
        name = benchmark.get("FullName") if isinstance(benchmark, dict) else None
        if not isinstance(name, str) or not BENCHMARK_NAME.match(name):
            warn(f"Skipping a benchmark with an unsupported name in {path.name}")
            continue
        statistics = benchmark.get("Statistics")
        if not isinstance(statistics, dict):
            statistics = {}
        mean, stddev = statistics.get("Mean"), statistics.get("StandardDeviation")
        if not (is_finite_number(mean) and is_finite_number(stddev) and mean >= 0 and stddev >= 0):
            warn(f"Skipping {name}: it has no usable statistics, so it probably failed")
            continue
        results.append(BenchmarkResult(extension, product, name, float(mean), float(stddev)))
    return results


def benchmark_type(full_name: str) -> str:
    """The class of a FullName, e.g. VirtualMachineBenchmarks."""
    parts = full_name.split(".")
    return parts[-2] if len(parts) >= 2 else ""


def display_label(full_name: str) -> str:
    """'Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines' -> 'VirtualMachine.ListVirtualMachines'."""
    parts = full_name.split(".")
    if len(parts) < 2:
        return full_name
    type_name = parts[-2]
    if type_name.endswith("Benchmarks") and type_name != "Benchmarks":
        type_name = type_name[: -len("Benchmarks")]
    return f"{type_name}.{parts[-1]}"


def series_for(result: BenchmarkResult) -> tuple[str, str] | None:
    """The gh-pages data directory and dashboard series of a result.

    These are the names github-action-benchmark published under, so history continues.
    Returns None for a startup class that has no dashboard.
    """
    if result.product == "STARTUP":
        series = STARTUP_SERIES.get(benchmark_type(result.full_name))
        return (series[0], series[1]) if series else None
    title = result.extension.lower().capitalize()
    return f"dev/bench/{result.product.lower()}", f"Azclips {title} {result.product} Benchmarks"


def is_informational(result: BenchmarkResult) -> bool:
    """Whether a result is reported without regression flagging (cold startup)."""
    series = STARTUP_SERIES.get(benchmark_type(result.full_name)) if result.product == "STARTUP" else None
    return bool(series and series[2])


def parse_data_js(text: str) -> dict:
    """Parse a github-action-benchmark data.js file."""
    match = DATA_JS.search(text)
    if not match:
        raise ValueError("not a benchmark data.js file")
    data = json.loads(match.group(1))
    if not isinstance(data, dict) or not isinstance(data.get("entries"), dict):
        raise ValueError("data.js has no entries object")
    return data


def format_data_js(data: dict) -> str:
    """Serialize data.js byte for byte as github-action-benchmark writes it."""
    return DATA_JS_PREFIX + json.dumps(data, indent=2, ensure_ascii=False)
