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


# -------------------------------------------------------------- tree building

def group_by_trace(spans):
    """One group per TraceId, i.e. per CLI or cmdlet invocation."""
    traces = defaultdict(list)
    for span in spans:
        traces[span.get("TraceId") or ""].append(span)
    return list(traces.values())


def child_sort_key(span):
    """Order siblings by start time; SpanId breaks ties so ordering is deterministic."""
    return (parse_time(span.get("StartTime")) or FAR_FUTURE, span.get("SpanId") or "")


def link(spans):
    """Return (children_by_parent_id, roots).

    A span is a root when it has no parent, or when its parent is absent from this
    trace. The latter is a truncated export: BatchActivityExportProcessor drains on
    shutdown in [IterationCleanup] and a span still in flight is dropped.
    """
    known = {span["SpanId"] for span in spans}
    children = defaultdict(list)
    roots = []
    for span in spans:
        parent = span.get("ParentSpanId")
        if parent and parent in known and parent != span["SpanId"]:
            children[parent].append(span)
        else:
            roots.append(span)
    return children, roots


def flatten(root, children):
    """Walk one trace, returning [(path, duration_ms, start_offset_ms)].

    Same-named siblings are indexed by start order, so a path is unique within a
    trace. That matters because SessionCreated, AuthBeforeTokenAcquisition, and
    ActionStart are started from event handlers in Engine.Subscriptions.cs and can
    each fire more than once under the same parent.
    """
    root_start = parse_time(root.get("StartTime"))
    collected = []
    visited = set()

    def visit(span, path):
        span_id = span["SpanId"]
        if span_id in visited:
            return
        visited.add(span_id)

        start = parse_time(span.get("StartTime"))
        offset = (start - root_start).total_seconds() * 1000.0 if start and root_start else 0.0
        collected.append((path, float(span.get("Duration") or 0.0), offset))

        counts = defaultdict(int)
        for child in sorted(children.get(span_id, []), key=child_sort_key):
            name = child.get("OperationName") or "<unnamed>"
            index = counts[name]
            counts[name] += 1
            visit(child, path + ((name, index),))

    visit(root, ((root.get("OperationName") or "<unnamed>", 0),))
    return collected


# ---------------------------------------------------------------- aggregation

class Report:
    """Cross-trace totals for one benchmark method, keyed by node path."""

    def __init__(self, stem, product, extension):
        self.stem = stem
        self.product = product
        self.extension = extension
        self.trace_count = 0
        self.malformed = 0
        self.orphan_subtrees = 0
        self.rootless_traces = 0
        self.nodes = {}

    def add(self, path, duration, offset):
        node = self.nodes.get(path)
        if node is None:
            node = {"total": 0.0, "count": 0, "offset_total": 0.0}
            self.nodes[path] = node
        node["total"] += duration
        node["count"] += 1
        node["offset_total"] += offset

    def average(self, path):
        node = self.nodes[path]
        return node["total"] / node["count"] if node["count"] else 0.0

    def mean_offset(self, path):
        node = self.nodes[path]
        return node["offset_total"] / node["count"] if node["count"] else 0.0

    def occurrences(self, path):
        """How many traces contained this node. Compare against trace_count for coverage."""
        return self.nodes[path]["count"]

    @property
    def root_path(self):
        """The dominant single-element path. More than one means the root's name varied."""
        roots = [path for path in self.nodes if len(path) == 1]
        if not roots:
            return None
        return max(roots, key=lambda path: self.nodes[path]["count"])


def aggregate_file(path, stem, product, extension):
    spans, malformed = read_spans(path)
    report = Report(stem, product, extension)
    report.malformed = malformed

    for trace_spans in group_by_trace(spans):
        children, roots = link(trace_spans)
        if not roots:
            # Only reachable if every span's parent is present, i.e. a reference
            # cycle. Counted rather than dropped silently.
            report.rootless_traces += 1
            continue
        roots.sort(key=child_sort_key)
        # Keep the earliest root. Other roots are detached subtrees from a truncated
        # export; reparenting them would fabricate a hierarchy that never existed.
        report.orphan_subtrees += len(roots) - 1
        report.trace_count += 1
        for node_path, duration, offset in flatten(roots[0], children):
            report.add(node_path, duration, offset)

    return report


# ----------------------------------------------------------------- rendering

def bar(ratio):
    """A proportional bar of BAR_WIDTH cells, resolved to eighths."""
    ratio = max(0.0, min(1.0, ratio))
    eighths = int(round(ratio * BAR_WIDTH * 8))
    full, remainder = divmod(eighths, 8)
    text = "█" * full
    if remainder:
        text += BLOCKS[remainder - 1]
    return text


def label(path, report):
    """Node name, an index suffix from the second occurrence on, and partial coverage."""
    name, index = path[-1]
    text = name if index == 0 else f"{name}[{index}]"
    count = report.occurrences(path)
    if count < report.trace_count:
        text += f"  ({count}/{report.trace_count})"
    return text


def render_report(report):
    """Render one benchmark's averaged tree, or None when there is nothing to show."""
    root = report.root_path
    if root is None or report.trace_count == 0:
        return None

    children_of = defaultdict(list)
    for path in report.nodes:
        if len(path) > 1:
            children_of[path[:-1]].append(path)
    for paths in children_of.values():
        paths.sort(key=report.mean_offset)

    root_average = report.average(root)
    show_bars = root_average > 0

    heading = " · ".join(part for part in (
        report.extension, report.product, f"{report.trace_count} traces") if part)
    lines = [f" Benchmark.{report.stem}", f" {heading}", ""]
    lines.append(f"{'AVG':>12}  {'%ROOT':>5}" if show_bars else f"{'AVG':>12}")

    def emit(path, prefix, is_last, is_root):
        average = report.average(path)
        row = f"{average:>9.3f} ms"
        if show_bars:
            ratio = average / root_average
            row += f"  {ratio * 100:>4.0f}%  {bar(ratio):<{BAR_WIDTH}}"
        connector = "" if is_root else prefix + ("└─ " if is_last else "├─ ")
        lines.append(f"{row}  {connector}{label(path, report)}")

        kids = children_of.get(path, [])
        child_prefix = prefix if is_root else prefix + ("   " if is_last else "│  ")
        for position, kid in enumerate(kids):
            emit(kid, child_prefix, position == len(kids) - 1, False)

    emit(root, "", True, True)

    notes = []
    if report.orphan_subtrees:
        notes.append(f"{report.orphan_subtrees} orphan subtree(s) dropped")
    if report.rootless_traces:
        notes.append(f"{report.rootless_traces} trace(s) had no resolvable root")
    if report.malformed:
        notes.append(f"{report.malformed} unreadable line(s) skipped")
    if notes:
        lines.extend(["", " " + "; ".join(notes)])

    return "\n".join(lines)


# ------------------------------------------------------------------- outputs

def render_summary(reports):
    """Markdown for $GITHUB_STEP_SUMMARY, truncated to stay under the 1 MiB cap."""
    blocks = ["## 🔬 Benchmark Trace Hierarchy", ""]
    # "\n".join inserts one separator byte per element beyond the first, so charge
    # each block for its own separator or the budget silently undercounts.
    used = sum(len(block.encode("utf-8")) for block in blocks) + len(blocks) - 1
    truncated = 0

    for report in reports:
        body = render_report(report)
        if body is None:
            continue
        block = "```\n" + body + "\n```\n"
        size = len(block.encode("utf-8")) + 1
        if used + size > SUMMARY_BUDGET_BYTES:
            truncated += 1
            continue
        blocks.append(block)
        used += size

    if truncated:
        blocks.append(
            f"\n_Output truncated: {truncated} benchmark(s) omitted to stay under "
            "GitHub's 1 MiB job summary limit._\n")

    return "\n".join(blocks)


def render_comment(reports, run_url):
    """An index table linking to the job summary, or None when nothing was aggregated."""
    rows = []
    for report in reports:
        root = report.root_path
        if root is None or report.trace_count == 0:
            continue
        rows.append(f"| {report.extension or '—'} | {report.product or '—'} "
                    f"| `{report.stem.rsplit('.', 1)[-1]}` | {report.trace_count} "
                    f"| {report.average(root):.3f} ms |")

    if not rows:
        return None

    lines = ["## 🔬 Benchmark Trace Hierarchy", "",
             "| Extension | Product | Benchmark | Traces | Root avg |",
             "|---|---|---|---|---|", *rows, ""]
    if run_url:
        lines.extend([f"[📊 View full trace hierarchies]({run_url})", ""])
    lines.append(COMMENT_MARKER)
    return "\n".join(lines)


# --------------------------------------------------------------- entry point

def run(args):
    files = discover(args.input)
    if not files:
        print(f"[traces] no telemetry .jsonl found under '{args.input}'")
        return 0

    reports = [aggregate_file(*item) for item in files]
    usable = [report for report in reports if report.trace_count > 0]
    print(f"[traces] aggregated {len(usable)} benchmark(s) from {len(files)} file(s)")

    if args.debug_paths:
        for report in usable:
            print(f"\n== {report.stem} ({report.trace_count} traces, "
                  f"{report.orphan_subtrees} orphans, {report.malformed} malformed)")
            for path in sorted(report.nodes, key=lambda p: (len(p), report.mean_offset(p))):
                trail = " > ".join(f"{name}[{index}]" for name, index in path)
                print(f"   {report.average(path):9.3f} ms  "
                      f"{report.occurrences(path):3d}/{report.trace_count}  {trail}")
        return 0

    summary = render_summary(usable)
    if args.summary_out:
        # Append: the runner may already have written to $GITHUB_STEP_SUMMARY.
        with open(args.summary_out, "a", encoding="utf-8") as handle:
            handle.write(summary + "\n")
        print(f"[traces] wrote summary to {args.summary_out}")
    else:
        print(summary)

    if args.comment_out:
        body = render_comment(usable, args.run_url)
        if body:
            with open(args.comment_out, "w", encoding="utf-8") as handle:
                handle.write(body + "\n")
            print(f"[traces] wrote comment body to {args.comment_out}")
    return 0



def main():
    parser = argparse.ArgumentParser(description="Aggregate benchmark trace telemetry.")
    parser.add_argument("--input", required=True, help="directory to search recursively")
    parser.add_argument("--summary-out", help="append the ASCII report here; omit for stdout")
    parser.add_argument("--comment-out", help="write the PR comment body here")
    parser.add_argument("--run-url", help="link target embedded in the comment")
    parser.add_argument("--debug-paths", action="store_true",
                        help="dump raw path keys with averages instead of rendering")
    args = parser.parse_args()

    try:
        return run(args)
    except Exception as exc:  # telemetry must never fail the workflow
        print(f"::warning::trace aggregation failed: {exc}", file=sys.stderr)
        return 0


if __name__ == "__main__":
    sys.exit(main())
