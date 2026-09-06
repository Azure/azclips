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
PRODUCTS = ("CLI", "PS", "STARTUP")
SUMMARY_BUDGET_BYTES = 900_000  # GitHub caps job summaries at 1 MiB; leave headroom.
FAR_FUTURE = datetime.max.replace(tzinfo=timezone.utc)
SIDECAR_SCHEMA = 1


# ------------------------------------------------------------------ discovery

def discover(root):
    """Return (path, stem, product, extension) for every telemetry file under root.

    Walks recursively for directories named 'telemetry', so both the CI layout
    (<EXT>/<PRODUCT>/telemetry/) and a local run's (<timestamp>/telemetry/) match.
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
    """The stem's first dot-separated segment, when it names a known product."""
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
    """Parse a .NET round-trip ('O') timestamp, or None when unparseable.

    'O' emits 7 fractional-second digits; fromisoformat accepts at most 6, so the
    fraction is truncated before parsing.
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
    trace — the latter indicates a truncated export.
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

    A path is a tuple of (operation_name, occurrence_index) pairs from the root down.
    Same-named siblings are indexed by start order, keeping a path unique within a
    trace when one operation fires several times under the same parent.
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
        self.source = {}

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

    def to_dict(self, commit=None, ref=None, run_id=None, generated=None):
        """Serialize to the sidecar shape, storing averages rather than running totals.

        offset_ms is included because sibling ordering derives from it.
        """
        stamp = generated or datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        return {
            "schema": SIDECAR_SCHEMA,
            "benchmark": self.stem,
            "extension": self.extension,
            "product": self.product,
            "source": {
                "commit": commit or "",
                "ref": ref or "",
                "run_id": run_id or "",
                "generated": stamp,
            },
            "traces": self.trace_count,
            "diagnostics": {
                "malformed": self.malformed,
                "orphan_subtrees": self.orphan_subtrees,
                "rootless_traces": self.rootless_traces,
            },
            "nodes": [
                {
                    "path": [[name, index] for name, index in path],
                    "avg_ms": self.average(path),
                    "occurrences": self.occurrences(path),
                    "offset_ms": self.mean_offset(path),
                }
                for path in sorted(self.nodes, key=lambda p: (len(p), self.mean_offset(p)))
            ],
        }

    @classmethod
    def from_dict(cls, data):
        """Rebuild a Report from a sidecar. Raises ValueError on an unusable document.

        Totals are reconstructed as avg * count so average() and mean_offset() work.
        """
        if not isinstance(data, dict):
            raise ValueError(f"sidecar is {type(data).__name__}, expected object")
        if data.get("schema") != SIDECAR_SCHEMA:
            raise ValueError(f"unsupported sidecar schema {data.get('schema')!r}")

        report = cls(data.get("benchmark") or "", data.get("product"), data.get("extension"))
        report.trace_count = int(data.get("traces") or 0)
        diagnostics = data.get("diagnostics")
        if diagnostics is not None and not isinstance(diagnostics, dict):
            raise ValueError(f"'diagnostics' is {type(diagnostics).__name__}, expected object")
        diagnostics = diagnostics or {}
        report.malformed = int(diagnostics.get("malformed") or 0)
        report.orphan_subtrees = int(diagnostics.get("orphan_subtrees") or 0)
        report.rootless_traces = int(diagnostics.get("rootless_traces") or 0)
        source = data.get("source")
        if source is not None and not isinstance(source, dict):
            raise ValueError(f"'source' is {type(source).__name__}, expected object")
        source = source or {}
        report.source = source

        for node_idx, node in enumerate(data.get("nodes") or []):
            if not isinstance(node, dict):
                raise ValueError(f"node {node_idx} is {type(node).__name__}, expected object")
            node_path = node.get("path")
            if node_path is None:
                raise ValueError(f"node {node_idx} has no 'path'")
            try:
                path = tuple((str(name), int(index)) for name, index in node_path)
            except (TypeError, ValueError) as exc:
                raise ValueError(f"node {node_idx} has invalid 'path': {exc}") from exc
            count = int(node.get("occurrences") or 0)
            average = float(node.get("avg_ms") or 0.0)
            offset = float(node.get("offset_ms") or 0.0)
            report.nodes[path] = {
                "total": average * count,
                "count": count,
                "offset_total": offset * count,
            }
        return report


def aggregate_file(path, stem, product, extension):
    spans, malformed = read_spans(path)
    report = Report(stem, product, extension)
    report.malformed = malformed

    for trace_spans in group_by_trace(spans):
        children, roots = link(trace_spans)
        if not roots:
            # Every span's parent is present, i.e. a reference cycle.
            report.rootless_traces += 1
            continue
        roots.sort(key=child_sort_key)
        # Keep the earliest root; the rest are detached subtrees from a truncated
        # export, and reparenting them would invent a hierarchy.
        report.orphan_subtrees += len(roots) - 1
        report.trace_count += 1
        for node_path, duration, offset in flatten(roots[0], children):
            report.add(node_path, duration, offset)

    return report


# ----------------------------------------------------------------- outputs

def sidecar_path(root, report):
    """<root>/<EXTENSION>/<PRODUCT>/<stem>.json, with 'LOCAL' standing in when a run
    has no extension segment. Stems are dotted alphanumerics and need no escaping.
    """
    return os.path.join(root, report.extension or "LOCAL",
                        report.product or "UNKNOWN", report.stem + ".json")


def write_sidecars(root, reports, commit, ref, run_id):
    """Write one sidecar per report, sharing one timestamp. Returns how many were
    written. A partial file from a failed write is removed.
    """
    generated = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    written = 0
    for report in reports:
        path = sidecar_path(root, report)
        try:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8") as handle:
                json.dump(report.to_dict(commit, ref, run_id, generated), handle, indent=2)
            written += 1
        except Exception as exc:
            # A sidecar is regenerable, so a write failure must not cost the summary.
            try:
                os.unlink(path)
            except OSError:
                pass
            print(f"::warning::could not write sidecar {path}: {exc}", file=sys.stderr)
    return written


def load_sidecar(path, source="baseline"):
    """Read one sidecar. Returns None for anything unusable, having warned.

    source names the caller in the warning, distinguishing a reload from a baseline.
    """
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return Report.from_dict(json.load(handle))
    except Exception as exc:
        # A missing baseline degrades to a single-tree render rather than failing.
        print(f"::warning::ignoring {source} sidecar {path}: {exc}", file=sys.stderr)
        return None


def find_baseline(root, report):
    """Locate this benchmark's baseline under root, or None.

    Tries the exact <EXT>/<PRODUCT>/<stem>.json, then a recursive search by filename,
    which is the only match for a local run's extension-less report. Ties are broken
    by sorted order.
    """
    filename = report.stem + ".json"
    exact = sidecar_path(root, report)
    if os.path.isfile(exact):
        return load_sidecar(exact, source="baseline")
    matches = []
    for dirpath, _dirnames, filenames in os.walk(root):
        if filename in filenames:
            matches.append(os.path.join(dirpath, filename))
    if matches:
        return load_sidecar(sorted(matches)[0], source="baseline")
    return None


# ----------------------------------------------------------------- rendering

def bar(ratio):
    """A proportional bar of BAR_WIDTH cells, drawn from the displayed percentage.

    Uses '#' because block elements (U+2588..U+258F) are East Asian Width "Ambiguous"
    and misalign every column after the bar under font fallback. The cell count comes
    from round(ratio * 100), the same value the caller prints, and truncates: at
    BAR_WIDTH 20 each cell is 5%, so 24% draws four and 26% draws five.
    """
    ratio = max(0.0, min(1.0, ratio))
    percent = round(ratio * 100)          # matches the caller's f"{ratio * 100:>4.0f}%"
    return "#" * (percent * BAR_WIDTH // 100)


def label(path, report):
    """Node name, an index suffix from the second occurrence on, and partial coverage."""
    name, index = path[-1]
    text = name if index == 0 else f"{name}[{index}]"
    count = report.occurrences(path)
    if count < report.trace_count:
        text += f"  ({count}/{report.trace_count})"
    return text


def node_text(path):
    """Node name, plus [n] from the second occurrence on. No coverage marker."""
    name, index = path[-1]
    return name if index == 0 else f"{name}[{index}]"


def delta_percent(baseline_average, current_average):
    """None when the comparison is undefined: a one-sided node, or a zero baseline.

    A one-sided node was added, removed, or renamed rather than slowed, so a delta
    would misreport a shape change as a timing change.
    """
    if baseline_average is None or current_average is None or baseline_average == 0:
        return None
    return (current_average - baseline_average) / baseline_average * 100.0


def compare_rows(baseline, current):
    """Returns (rows, orphaned_count) over the union of both node sets, where rows is
    [(path, connector)] in one shared order, so row n is the same node on both sides.

    orphaned_count counts union nodes unreachable from any root; they are not
    rendered. Ordering uses the current run's mean start offset where the node exists
    there and the baseline's otherwise, tie-broken on the path.
    """
    roots = []
    for report in (current, baseline):
        root = report.root_path
        if root is not None and root not in roots:
            roots.append(root)
    if not roots:
        return [], 0

    root_set = set(roots)
    paths = {path for path in set(current.nodes) | set(baseline.nodes)
             if path[:1] in root_set}

    def offset(path):
        if path in current.nodes:
            return current.mean_offset(path)
        return baseline.mean_offset(path)

    children_of = defaultdict(list)
    placed = set(roots)  # roots are always placed
    for path in paths:
        # flatten() emits every ancestor of the nodes it collects, so a parent is
        # always in the union; depth-1 paths are the roots themselves.
        if len(path) > 1 and path[:-1] in paths:
            children_of[path[:-1]].append(path)
            placed.add(path)
    for kids in children_of.values():
        kids.sort(key=lambda p: (offset(p), p))
    roots.sort(key=lambda p: (offset(p), p))

    rows = []

    def emit(path, prefix, is_last, is_root):
        connector = "" if is_root else prefix + ("└─ " if is_last else "├─ ")
        rows.append((path, connector))
        kids = children_of.get(path, [])
        child_prefix = prefix if is_root else prefix + ("   " if is_last else "│  ")
        for position, kid in enumerate(kids):
            emit(kid, child_prefix, position == len(kids) - 1, False)

    for position, root in enumerate(roots):
        emit(root, "", position == len(roots) - 1, True)

    orphaned_count = len(paths) - len(placed)
    return rows, orphaned_count


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


def relative_age(generated):
    """Coarse age of a baseline, or None when the timestamp is unusable.

    Resolution is whole days, enough to flag a stale baseline.
    """
    stamp = parse_time(generated)
    if stamp is None:
        return None
    days = (datetime.now(timezone.utc) - stamp).days
    if days <= 0:
        return "today"
    if days == 1:
        return "1 day ago"
    return f"{days} days ago"


def render_comparison(baseline, current):
    """Render main and this run as two trees sharing one row order.

    Each side scales %ROOT and its bar to its own root, so bars are not comparable
    across the gutter; the AVG and Δ% columns carry the comparison.
    """
    rows, orphaned_count = compare_rows(baseline, current)
    if not rows:
        return None

    baseline_root = baseline.root_path
    current_root = current.root_path
    baseline_scale = baseline.average(baseline_root) if baseline_root else 0.0
    current_scale = current.average(current_root) if current_root else 0.0

    def side_label(report, path, connector):
        """Node label with coverage counted against this side's own trace count, so a
        marker on one side only means the node is intermittent there.
        """
        text = connector + node_text(path)
        count = report.occurrences(path)
        if count < report.trace_count:
            text += f"  ({count}/{report.trace_count})"
        return text

    label_width = 0
    for path, connector in rows:
        for report in (baseline, current):
            if path in report.nodes:
                label_width = max(label_width, len(side_label(report, path, connector)))
    # 12 for "9.3f ms", 2 gutter, 5 for %ROOT, 2 gutter, BAR_WIDTH, 2 gutter, label;
    # must agree with the format strings in cell().
    side_width = 12 + 2 + 5 + 2 + BAR_WIDTH + 2 + label_width

    def cell(report, path, connector, scale):
        if path not in report.nodes:
            return " " * side_width          # absent, not zero
        average = report.average(path)
        text = side_label(report, path, connector)
        if scale > 0:
            ratio = average / scale
            body = (f"{average:>9.3f} ms  {ratio * 100:>4.0f}%  "
                    f"{bar(ratio):<{BAR_WIDTH}}  {text}")
        else:
            # Blank the percentage and bar but keep their widths, so a zero-root row
            # still aligns with normal rows.
            body = f"{average:>9.3f} ms  {'':>5}  {'':<{BAR_WIDTH}}  {text}"
        return body.ljust(side_width)

    def heading(report, tag, with_age):
        parts = []
        commit = (report.source or {}).get("commit") or ""
        parts.append(f"{tag} @ {commit[:7]}" if commit else tag)
        if with_age:
            age = relative_age((report.source or {}).get("generated"))
            if age:
                parts.append(age)
        parts.append(f"{report.trace_count} traces")
        return " · ".join(parts)

    lines = [f" Benchmark.{current.stem}"]
    subtitle = " · ".join(part for part in (current.extension, current.product) if part)
    if subtitle:
        lines.append(f" {subtitle}")
    lines.append("")
    lines.append((f"{heading(baseline, 'main', True):^{side_width}}"
                  + " │         │ "
                  + f"{heading(current, 'PR', False):^{side_width}}").rstrip())
    lines.append(f"{'AVG':>12}  {'%ROOT':>5}".ljust(side_width)
                 + " │    Δ%   │ " + f"{'AVG':>12}  {'%ROOT':>5}")

    for path, connector in rows:
        left = baseline.average(path) if path in baseline.nodes else None
        right = current.average(path) if path in current.nodes else None
        delta = delta_percent(left, right)
        delta_text = "n/a" if delta is None else f"{delta:+.1f}%"
        lines.append((cell(baseline, path, connector, baseline_scale)
                      + " │ " + f"{delta_text:>7}" + " │ "
                      + cell(current, path, connector, current_scale)).rstrip())

    notes = []
    for tag, report in (("main", baseline), ("PR", current)):
        detail = []
        if report.orphan_subtrees:
            detail.append(f"{report.orphan_subtrees} orphan subtree(s) dropped")
        if report.rootless_traces:
            detail.append(f"{report.rootless_traces} trace(s) had no resolvable root")
        if report.malformed:
            detail.append(f"{report.malformed} unreadable line(s) skipped")
        if detail:
            notes.append(f"{tag}: " + "; ".join(detail))

    only_baseline = sum(1 for path, _ in rows if path not in current.nodes)
    only_current = sum(1 for path, _ in rows if path not in baseline.nodes)
    shape = []
    if only_baseline:
        shape.append(f"{only_baseline} node(s) only in main")
    if only_current:
        shape.append(f"{only_current} node(s) only in PR")
    if shape:
        notes.append(", ".join(shape) + " — tree shape changed")

    if orphaned_count:
        notes.append(f"{orphaned_count} union node(s) could not be placed (orphaned)")

    if notes:
        lines.extend(["", " " + "   ".join(notes)])

    return "\n".join(lines)


# ------------------------------------------------------------------- outputs

def render_summary(reports, baselines=None):
    """Markdown for $GITHUB_STEP_SUMMARY, truncated to stay under the 1 MiB cap.

    A benchmark with a baseline renders side by side; one without falls back to a
    single tree.
    """
    baselines = baselines or {}
    blocks = ["## 🔬 Benchmark Trace Hierarchy", ""]
    # "\n".join adds one separator byte per element beyond the first, so each block
    # is charged for its separator to keep the budget accurate.
    used = sum(len(block.encode("utf-8")) for block in blocks) + len(blocks) - 1
    truncated = 0

    for report in reports:
        baseline = baselines.get(report.stem)
        body = render_comparison(baseline, report) if baseline else render_report(report)
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


# --------------------------------------------------------------- entry point

def run(args):
    files = discover(args.input)
    if not files:
        print(f"[traces] no telemetry .jsonl found under '{args.input}'")
        return 0

    reports = [aggregate_file(*item) for item in files]
    usable = [report for report in reports if report.trace_count > 0]
    print(f"[traces] aggregated {len(usable)} benchmark(s) from {len(files)} file(s)")

    if args.sidecar_out:
        written = write_sidecars(args.sidecar_out, usable,
                                 args.commit, args.ref, args.run_id)
        print(f"[traces] wrote {written} sidecar(s) to {args.sidecar_out}")
        # Render from what was just written so both sides of a comparison travel the
        # same to_dict/from_dict path, and this run's tree gets its provenance header.
        usable = [load_sidecar(sidecar_path(args.sidecar_out, report), source="reload") or report
                  for report in usable]

    baselines = {}
    if args.baseline and os.path.isdir(args.baseline):
        for report in usable:
            baseline = find_baseline(args.baseline, report)
            if baseline is not None:
                baselines[report.stem] = baseline
        print(f"[traces] matched {len(baselines)}/{len(usable)} baseline(s) "
              f"from {args.baseline}")
    elif args.baseline:
        print(f"[traces] baseline directory '{args.baseline}' not found; "
              "rendering without comparison")

    if args.debug_paths:
        for report in usable:
            print(f"\n== {report.stem} ({report.trace_count} traces, "
                  f"{report.orphan_subtrees} orphans, {report.malformed} malformed)")
            for path in sorted(report.nodes, key=lambda p: (len(p), report.mean_offset(p))):
                trail = " > ".join(f"{name}[{index}]" for name, index in path)
                print(f"   {report.average(path):9.3f} ms  "
                      f"{report.occurrences(path):3d}/{report.trace_count}  {trail}")
        return 0

    summary = render_summary(usable, baselines)
    if args.summary_out:
        # Append: the runner may already have written to $GITHUB_STEP_SUMMARY.
        with open(args.summary_out, "a", encoding="utf-8") as handle:
            handle.write(summary + "\n")
        print(f"[traces] wrote summary to {args.summary_out}")
    else:
        print(summary)

    return 0



def main():
    parser = argparse.ArgumentParser(description="Aggregate benchmark trace telemetry.")
    parser.add_argument("--input", required=True, help="directory to search recursively")
    parser.add_argument("--sidecar-out", help="write this run's trees as JSON here")
    parser.add_argument("--commit", help="commit sha recorded in the sidecar")
    parser.add_argument("--ref", help="git ref recorded in the sidecar")
    parser.add_argument("--run-id", help="workflow run id recorded in the sidecar")
    parser.add_argument("--baseline", help="directory of sidecars to compare against")
    parser.add_argument("--summary-out", help="append the ASCII report here; omit for stdout")
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
