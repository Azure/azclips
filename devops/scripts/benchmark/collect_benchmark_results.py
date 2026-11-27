#!/usr/bin/env python3
"""Combine one Azure Pipelines run's benchmark legs into Benchmark_Results.

--legs holds the leg artifacts as DownloadPipelineArtifact@2 lays them out, one
directory per artifact: Benchmark_CLI_<ext>, Benchmark_PS_<ext> and Benchmark_STARTUP,
plus _attempt<N> copies from reruns. Each holds the runner's CI layout,
<EXT>/<PRODUCT>/{results,telemetry}/.

The newest attempt of every leg is written to:
  --merged  <EXT>/<PRODUCT>/telemetry/, the input of aggregate_benchmark_traces.py
  --out     reports/<EXT>/<PRODUCT>/results/*-report-full-compressed.json, and
            metadata.json describing the run for compare_benchmarks.py

Each selected leg (BENCHMARK_EXTENSIONS, BENCHMARK_STARTUP) that contributed no report
gets a warning, so a leg that succeeded but whose reports were not found (for example,
after a layout or artifact-pattern change) is noticed. It never fails the run: a leg that
fails, including the PS leg of an extension without PS benchmarks, already fails the stage.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import sys
import urllib.parse
from pathlib import Path

from benchmark_results import EXTENSION_SEGMENT, PRODUCTS, REPORT_SUFFIX, latest_attempts, warn

LEG_ARTIFACT = re.compile(r"^Benchmark_(?:(?:CLI|PS)_[a-z0-9]+|STARTUP)\Z")
SELECTED_EXTENSION = re.compile(r"^[a-z0-9]+\Z")


def collect(legs: Path, merged: Path, out: Path) -> list[str]:
    """Copy the newest attempt of each leg. Returns the artifact names used."""
    names = [path.name for path in legs.iterdir() if path.is_dir()] if legs.is_dir() else []
    chosen = sorted(name for base, name in latest_attempts(names).items() if LEG_ARTIFACT.match(base))
    for name in chosen:
        for extension_dir in sorted(path for path in (legs / name).iterdir() if path.is_dir()):
            if not EXTENSION_SEGMENT.match(extension_dir.name):
                continue
            for product in PRODUCTS:
                source = extension_dir / product
                relative = Path(extension_dir.name, product)
                if (source / "telemetry").is_dir():
                    shutil.copytree(source / "telemetry", merged / relative / "telemetry", dirs_exist_ok=True)
                for report in sorted((source / "results").glob("*" + REPORT_SUFFIX)):
                    target = out / "reports" / relative / "results"
                    target.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(report, target / report.name)
    return chosen


def warn_about_empty_legs(out: Path, environ) -> None:
    """Warn once for each selected leg that contributed no report to <out>/reports."""
    legs = [(name.upper(), product, f"The {product} leg of {name}")
            for name in environ.get("BENCHMARK_EXTENSIONS", "").split(",") if SELECTED_EXTENSION.match(name)
            for product in ("CLI", "PS")]
    if environ.get("BENCHMARK_STARTUP", "").lower() == "true":
        legs.append(("ALL", "STARTUP", "The startup leg"))
    for extension, product, leg in legs:
        if not any((out / "reports" / extension / product / "results").glob("*" + REPORT_SUFFIX)):
            warn(f"{leg} produced no benchmark reports.")


def build_metadata(environ) -> dict:
    """Describe this run for compare_benchmarks.py. The GitHub workflows never trust it."""
    build_id = environ.get("BUILD_BUILDID", "")
    collection = environ.get("SYSTEM_COLLECTIONURI", "").rstrip("/")
    project = environ.get("SYSTEM_TEAMPROJECT", "")
    pull_request = environ.get("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER", "")
    build_url = ""
    if build_id.isdigit() and collection and project:
        build_url = f"{collection}/{urllib.parse.quote(project)}/_build/results?buildId={build_id}"
    return {
        "schema": 1,
        "buildId": int(build_id) if build_id.isdigit() else None,
        "buildUrl": build_url,
        "reason": environ.get("BUILD_REASON", ""),
        "sourceBranch": environ.get("BUILD_SOURCEBRANCH", ""),
        "commit": environ.get("SYSTEM_PULLREQUEST_SOURCECOMMITID") or environ.get("BUILD_SOURCEVERSION", ""),
        "pullRequest": int(pull_request) if pull_request.isdigit() else None,
        "extensions": [name for name in environ.get("BENCHMARK_EXTENSIONS", "").split(",") if name],
        "startup": environ.get("BENCHMARK_STARTUP", "").lower() == "true",
        "stageResult": None,
        "failedJobs": [],
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Combine benchmark legs into Benchmark_Results.")
    parser.add_argument("--legs", required=True, type=Path, help="downloaded leg artifacts")
    parser.add_argument("--merged", required=True, type=Path, help="merged telemetry output")
    parser.add_argument("--out", required=True, type=Path, help="Benchmark_Results directory")
    args = parser.parse_args(argv)

    args.merged.mkdir(parents=True, exist_ok=True)
    (args.out / "reports").mkdir(parents=True, exist_ok=True)
    chosen = collect(args.legs, args.merged, args.out)
    warn_about_empty_legs(args.out, os.environ)
    metadata = build_metadata(os.environ)
    (args.out / "metadata.json").write_text(json.dumps(metadata, indent=2) + "\n", encoding="utf-8")
    print(f"Collected {len(chosen)} benchmark leg(s): {', '.join(chosen) or 'none'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
