"""Test helpers that write benchmark files in the shapes the pipeline produces."""

from __future__ import annotations

import io
import json
import os
from contextlib import contextmanager, redirect_stderr
from pathlib import Path
from unittest import mock

from benchmark_results import REPORT_SUFFIX, format_data_js


def write_report(root, extension, product, type_name, benchmarks):
    """Write <root>/<EXT>/<PRODUCT>/results/<Namespace.Type>-report-full-compressed.json.

    benchmarks maps a method name to (mean_ns, stddev_ns), or to None for a benchmark
    that failed and so has no statistics.
    """
    namespace = "Benchmark.Startup" if product == "STARTUP" else f"Benchmark.{product}"
    items = [
        {
            "FullName": f"{namespace}.{type_name}.{method}",
            "Statistics": None if stats is None else {"Mean": stats[0], "StandardDeviation": stats[1]},
        }
        for method, stats in benchmarks.items()
    ]
    directory = Path(root) / extension / product / "results"
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{namespace}.{type_name}{REPORT_SUFFIX}"
    path.write_text(json.dumps({"Benchmarks": items}), encoding="utf-8")
    return path


def data_js_entry(commit_id, date, benches, timestamp="2026-11-01T00:00:00Z"):
    """One data.js entry; benches maps a benchmark FullName to its value in ns."""
    return {
        "commit": {"id": commit_id, "timestamp": timestamp},
        "date": date,
        "tool": "benchmarkdotnet",
        "benches": [{"name": name, "value": value, "unit": "ns", "range": "± 1"} for name, value in benches.items()],
    }


def write_data_js(path, entries):
    """Write a data.js holding entries ({series: [entry, ...]})."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    data = {"lastUpdate": 1, "repoUrl": "https://github.com/Azure/azclips", "entries": entries}
    path.write_text(format_data_js(data), encoding="utf-8")


@contextmanager
def captured_warnings():
    """Capture warn() output as plain text, even when the tests run inside a CI job."""
    stream = io.StringIO()
    with mock.patch.dict(os.environ):
        os.environ.pop("TF_BUILD", None)
        os.environ.pop("GITHUB_ACTIONS", None)
        with redirect_stderr(stream):
            yield stream
