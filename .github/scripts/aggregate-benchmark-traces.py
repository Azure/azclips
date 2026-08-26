#!/usr/bin/env python3
"""Aggregate sampled benchmark telemetry into averaged span trees.

Reads the .jsonl files written by tests/Benchmark (one span per line, serialized
from Core.Telemetry.TraceEntry), merges every sampled trace for a benchmark into
one averaged span tree, and renders it as ASCII.

Standard library only: this runs on a bare runner with no pip install.
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from collections import defaultdict
from datetime import datetime, timezone

BAR_WIDTH = 20
BLOCKS = "▏▎▍▌▋▊▉█"
PRODUCTS = ("CLI", "PS", "STARTUP")
COMMENT_MARKER = "<!-- benchmark-traces -->"
SUMMARY_BUDGET_BYTES = 900_000  # GitHub caps job summaries at 1 MiB; leave headroom.
FAR_FUTURE = datetime.max.replace(tzinfo=timezone.utc)


# ------------------------------------------------------------------ discovery

def discover(root):
    """Return (path, stem, product, extension) for every telemetry file under root.

    Searches recursively rather than with a fixed depth glob: CI writes
    <EXT>/<PRODUCT>/telemetry/ but a local run writes <timestamp>/telemetry/,
    and the same invocation has to work against both.
    """
    found = []
    for dirpath, _dirnames, filenames in os.walk(root):
        if os.path.basename(dirpath) != "telemetry":
            continue
        for filename in sorted(filenames):
            if not filename.endswith(".jsonl"):
                continue
            stem = filename[: -len(".jsonl")]
            found.append((os.path.join(dirpath, filename), stem,
                          product_of(stem), extension_of(dirpath)))
    return sorted(found, key=lambda item: (item[3] or "", item[2] or "", item[1]))



def product_of(stem):
    """BenchmarkName strips the 'Benchmark.' prefix, so the stem starts with the product."""
    head = stem.split(".", 1)[0].upper()
    return head if head in PRODUCTS else None



def extension_of(telemetry_dir):
    """CI paths are <EXT>/<PRODUCT>/telemetry. Local paths have no extension segment."""
    parts = os.path.normpath(telemetry_dir).split(os.sep)
    if len(parts) >= 3 and parts[-2].upper() in PRODUCTS:
        return parts[-3].upper()
    return None


# -------------------------------------------------------------------- parsing

def parse_time(value):
    """Parse a .NET round-trip ('O') timestamp.

    'O' emits 7 fractional-second digits; datetime.fromisoformat accepts at most
    6 and raises on the seventh, so truncation here is required, not defensive.
    """
    if not value:
        return None
    text = value.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    if "." in text:
        head, _, tail = text.partition(".")
        digits = ""
        for char in tail:
            if not char.isdigit():
                break
            digits += char
        text = head + "." + digits[:6].ljust(6, "0") + tail[len(digits):]
    try:
        parsed = datetime.fromisoformat(text)
    except ValueError:
        return None
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)



def read_spans(path):
    """Return (spans, malformed_count). A span without a SpanId cannot be placed in a tree."""
    spans = []
    malformed = 0
    with open(path, "r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                span = json.loads(line)
            except json.JSONDecodeError:
                malformed += 1
                continue
            if not isinstance(span, dict) or not span.get("SpanId"):
                malformed += 1
                continue
            spans.append(span)
    return spans, malformed


# --------------------------------------------------------------- entry point

def run(args):
    files = discover(args.input)
    if not files:
        print(f"[traces] no telemetry .jsonl found under '{args.input}'")
        return 0

    for path, stem, product, extension in files:
        spans, malformed = read_spans(path)
        traces = {span.get("TraceId") or "" for span in spans}
        print(f"[traces] {extension or '-'}/{product or '-'}/{stem}: "
              f"{len(spans)} span(s), {len(traces)} trace(s), {malformed} malformed")

    return 0



def main():
    parser = argparse.ArgumentParser(description="Aggregate benchmark trace telemetry.")
    parser.add_argument("--input", required=True, help="directory to search recursively")
    parser.add_argument("--summary-out", help="append the ASCII report here; omit for stdout")
    parser.add_argument("--comment-out", help="write the PR comment body here")
    parser.add_argument("--run-url", help="link target embedded in the comment")
    args = parser.parse_args()

    try:
        return run(args)
    except Exception as exc:  # telemetry must never fail the workflow
        print(f"::warning::trace aggregation failed: {exc}", file=sys.stderr)
        return 0


if __name__ == "__main__":
    sys.exit(main())
