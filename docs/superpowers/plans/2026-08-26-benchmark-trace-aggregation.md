# Benchmark Trace Hierarchy Aggregation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggregate each benchmark method's sampled OpenTelemetry spans into one averaged span tree, render it as ASCII in the workflow run's job summary, and link it from a PR comment that works on forks.

**Architecture:** A single stdlib-only Python script reads the `.jsonl` telemetry the benchmarks already emit, merges every sampled trace per benchmark into one tree keyed by node path, and renders ASCII. The existing `collect` job in `benchmark.yml` runs it and uploads the rendered comment body as an artifact; a new job in `benchmark-comment.yml` posts that body, because only a `workflow_run` workflow has a writable token for fork PRs.

**Tech Stack:** Python 3 (standard library only — no pip, no venv), GitHub Actions, `gh` CLI.

**Spec:** `docs/superpowers/specs/2026-08-26-benchmark-trace-aggregation-design.md`

## Global Constraints

- **The script code in this plan has been executed end-to-end and verified** against the Task 1 fixture on Python 3.13. Every expected-output block below is captured actual output, not a sketch. If your run differs, suspect your transcription before suspecting the plan.
- **Standard library only.** No pip installs, no `requirements.txt`, no venv. The script must run on a bare `ubuntu-latest` runner with `python3` and nothing else.
- **No automated tests.** This was an explicit decision, recorded in the spec's Testing section. Every task is verified by running the script against a fixture and reading the output. Do not add `unittest`, `pytest`, or a test project.
- **Never fail the workflow.** Telemetry reporting must not change a benchmark run's outcome. The script exits `0` on every internal error, matching the posture `CollectTelemetry` already takes in `tests/Benchmark/Program.cs`.
- **Durations are milliseconds.** `TraceEntry.Duration` is written as `activity.Duration.TotalMilliseconds` (`src/Core/Core.Telemetry/FileExporter.cs:46`). Never re-scale it.
- **Timestamps are .NET round-trip format.** `StartTime` uses `"O"`, which emits **7** fractional-second digits. `datetime.fromisoformat` accepts at most **6** and will raise on the raw value. Truncation is mandatory, not defensive.
- **Nothing under `src/` or `tests/` changes.** The telemetry producer already emits everything required.
- **Windows dev note.** The renderer emits box-drawing and block characters. When running locally in PowerShell, prefix commands with `$env:PYTHONIOENCODING='utf-8';` or the console encoding will raise `UnicodeEncodeError`.

---

## File Structure

| File | Responsibility |
| --- | --- |
| `.github/scripts/aggregate-benchmark-traces.py` | **Create.** The entire aggregator: discovery, parsing, tree building, aggregation, rendering, output. |
| `.github/workflows/benchmark.yml` | **Modify.** Three steps added to the `collect` job. |
| `.github/workflows/benchmark-comment.yml` | **Modify.** One new `trace-comment` job. |
| `artifacts/trace-fixture/**` | **Dev only, never committed.** `artifacts/` is gitignored (`.gitignore:78`). Synthetic telemetry for eyeball verification. |

**Why one script rather than a package:** the CI invocation is `checkout` then `python3 <path>`. A package would need either `PYTHONPATH` juggling or an install step, for a file that stays well under 400 lines. This also matches the existing `.github/scripts/` convention (`detect-benchmark-changes.sh` is a single file). The script is organised into commented sections that map 1:1 onto the tasks below.

---

## Background: the data you are reading

Read this before Task 1. Getting the layout wrong is the most likely way to waste an hour.

**Two hops produce the files.** BenchmarkDotNet runs one child process per benchmark case. Each child writes its sampled traces to `artifacts/BenchmarkTelemetry/<name>.jsonl` (a stable staging path, because a child cannot know the run's `artifactsPath`). After all cases finish, `Program.cs:97` moves every file into `<artifactsPath>/telemetry/` and deletes the staging root. **`artifacts/BenchmarkTelemetry` does not exist by the time CI uploads anything — do not glob for it.**

**Two layouts must both work:**

| Mode | Telemetry directory |
| --- | --- |
| CI (`--ci`) | `artifacts/Benchmark/ci/<EXT>/<PRODUCT>/telemetry/` |
| Local | `artifacts/Benchmark/<timestamp>/telemetry/` |

In CI the `collect` job downloads with `merge-multiple: true`, producing `downloaded/<EXT>/<PRODUCT>/telemetry/*.jsonl`.

**One span per line**, serialized from `Core.Telemetry.TraceEntry`:

```json
{"TraceId":"4bf9...","SpanId":"00f0...","ParentSpanId":"a1b2...","OperationName":"AzureCLI.Startup","DisplayName":"AzureCLI.Startup","StartTime":"2026-08-26T04:12:33.1234567Z","Duration":8.12,"Status":"Unset","Kind":"Internal","Tags":{},"Events":[],"Resource":{"service.name":"CLI"}}
```

Only `TraceId`, `SpanId`, `ParentSpanId`, `OperationName`, `StartTime`, and `Duration` are used.

**The file stem is the benchmark name** with the `Benchmark.` namespace prefix already stripped by `BenchmarkName.StripNamespace`, e.g. `CLI.ResourceGroupBenchmarks.ListResourceGroups`. Its first segment is the product.

---

### Task 1: Script skeleton — discovery, parsing, CLI

**Files:**
- Create: `.github/scripts/aggregate-benchmark-traces.py`
- Dev fixture (not committed): `artifacts/trace-fixture/`

**Interfaces:**
- Consumes: nothing.
- Produces: `discover(root) -> list[(path, stem, product, extension)]`; `read_spans(path) -> (list[dict], int)`; `parse_time(value) -> datetime | None`; `run(args) -> int`; `main() -> int`. Constant `PRODUCTS = ("CLI", "PS", "STARTUP")`.

- [ ] **Step 1: Create the dev fixture generator and run it**

This is throwaway scaffolding under gitignored `artifacts/`. It builds both directory layouts and includes the awkward cases deliberately: a repeated sibling, a truncated trace missing a leaf, an orphan span, and a 7-fractional-digit timestamp.

Save as `artifacts/make-trace-fixture.py`:

```python
"""Dev-only: synthesise benchmark telemetry in both CI and local layouts."""
import json, os, shutil
from datetime import datetime, timedelta, timezone

ROOT = os.path.join("artifacts", "trace-fixture")
shutil.rmtree(ROOT, ignore_errors=True)

BASE = datetime(2026, 8, 26, 4, 12, 33, tzinfo=timezone.utc)


def stamp(offset_ms):
    moment = BASE + timedelta(milliseconds=offset_ms)
    # Mimic .NET "O": 7 fractional digits.
    return moment.strftime("%Y-%m-%dT%H:%M:%S.") + f"{moment.microsecond:06d}0" + "Z"


def span(trace, span_id, parent, name, offset_ms, duration_ms):
    return {
        "TraceId": trace, "SpanId": span_id, "ParentSpanId": parent,
        "OperationName": name, "DisplayName": name,
        "StartTime": stamp(offset_ms), "Duration": duration_ms,
        "Status": "Unset", "Kind": "Internal",
        "Tags": {}, "Events": [], "Resource": {"service.name": "CLI"},
    }


def trace(index, sessions, truncated, orphan):
    tid = f"trace{index:03d}"
    jitter = index * 0.1
    rows = [
        span(tid, f"{tid}-r", None, "CLI.Activity.Root", 0, 42.0 + jitter),
        span(tid, f"{tid}-s", f"{tid}-r", "AzureCLI.Startup", 1, 8.0 + jitter),
        span(tid, f"{tid}-sc", f"{tid}-s", "AzureCLI.Startup.Container", 2, 0.6),
        span(tid, f"{tid}-ss", f"{tid}-s", "AzureCLI.Startup.Services", 3, 3.0),
        span(tid, f"{tid}-p", f"{tid}-r", "AzureCLI.ProcessCommand", 10, 28.0 + jitter),
    ]
    for n in range(sessions):
        rows.append(span(tid, f"{tid}-sess{n}", f"{tid}-p", "SessionCreated", 11 + n, 3.0 - n))
    if not truncated:
        rows.append(span(tid, f"{tid}-st", f"{tid}-s", "AzureCLI.Startup.Telemetry", 4, 4.4))
    if orphan:
        rows.append(span(tid, f"{tid}-orph", "missing-parent", "Detached.Span", 20, 1.5))
    return rows


def write(directory, filename, rows):
    os.makedirs(directory, exist_ok=True)
    with open(os.path.join(directory, filename), "w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row) + "\n")


records = []
for i in range(10):
    records += trace(i, sessions=2 if i < 4 else 1, truncated=(i == 7), orphan=(i == 8))
records_ps = []
for i in range(5):
    records_ps += trace(i, sessions=1, truncated=False, orphan=False)

# CI layout: <EXT>/<PRODUCT>/telemetry
write(os.path.join(ROOT, "ci", "RESOURCEGROUP", "CLI", "telemetry"),
      "CLI.ResourceGroupBenchmarks.ListResourceGroups.jsonl", records)
write(os.path.join(ROOT, "ci", "RESOURCEGROUP", "PS", "telemetry"),
      "PS.ResourceGroupBenchmarks.GetAzResourceGroup.jsonl", records_ps)
# Local layout: <timestamp>/telemetry
write(os.path.join(ROOT, "local", "2026-08-26_04-12-33", "telemetry"),
      "CLI.ResourceGroupBenchmarks.ListResourceGroups.jsonl", records)

print("fixture written to", ROOT)
```

Run:

```powershell
python artifacts\make-trace-fixture.py
```

Expected: `fixture written to artifacts\trace-fixture`

- [ ] **Step 2: Write the script's header, constants, and discovery**

Create `.github/scripts/aggregate-benchmark-traces.py`:

```python
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
```

- [ ] **Step 3: Add timestamp and span parsing**

Append to the same file:

```python
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
```

- [ ] **Step 4: Add the CLI entry point with counts-only output**

Append:

```python
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
```

- [ ] **Step 5: Verify discovery on both layouts**

```powershell
$env:PYTHONIOENCODING='utf-8'
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\trace-fixture\ci
```

Expected — extension and product resolved from the CI path:

```
[traces] RESOURCEGROUP/CLI/CLI.ResourceGroupBenchmarks.ListResourceGroups: 74 span(s), 10 trace(s), 0 malformed
[traces] RESOURCEGROUP/PS/PS.ResourceGroupBenchmarks.GetAzResourceGroup: 35 span(s), 5 trace(s), 0 malformed
```

```powershell
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\trace-fixture\local
```

Expected — extension is `-` because the local layout has no extension segment, but the product still resolves from the file stem:

```
[traces] -/CLI/CLI.ResourceGroupBenchmarks.ListResourceGroups: 74 span(s), 10 trace(s), 0 malformed
```

```powershell
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\does-not-exist
```

Expected: `[traces] no telemetry .jsonl found under 'artifacts\does-not-exist'` and exit code 0.

- [ ] **Step 6: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): add trace telemetry discovery and parsing"
```

---

### Task 2: Tree building and cross-trace aggregation

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py`

**Interfaces:**
- Consumes: `read_spans`, `parse_time`, `FAR_FUTURE` from Task 1.
- Produces: `class Report` with attributes `stem`, `product`, `extension`, `trace_count`, `malformed`, `orphan_subtrees`, `rootless_traces`, `nodes`; methods `add(path, duration, offset)`, `average(path)`, `mean_offset(path)`, `occurrences(path)`, property `root_path`. Functions `group_by_trace(spans)`, `child_sort_key(span)`, `link(spans) -> (children, roots)`, `flatten(root, children) -> list[(path, duration, offset)]`, `aggregate_file(path, stem, product, extension) -> Report`. A **path** is a tuple of `(operation_name, occurrence_index)` pairs from the root down. Note `occurrences(path)` returns a **count**, not a ratio — compare it against `trace_count` to get coverage.

- [ ] **Step 1: Add tree building**

Insert after the parsing section:

```python
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
```

- [ ] **Step 2: Add the Report accumulator and per-file aggregation**

Append after tree building:

```python
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
```

- [ ] **Step 3: Add a `--debug-paths` flag and wire aggregation into `run`**

This flag is a permanent debugging affordance, not scaffolding — it is the only way to inspect raw path keys when a rendered tree looks wrong.

Add the argument in `main`, directly after the `--run-url` line:

```python
    parser.add_argument("--debug-paths", action="store_true",
                        help="dump raw path keys with averages instead of rendering")
```

Replace the whole body of `run` with:

```python
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
```

- [ ] **Step 4: Verify aggregation against the fixture**

```powershell
$env:PYTHONIOENCODING='utf-8'
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\trace-fixture\ci --debug-paths
```

Check these four things in the output for `CLI.ResourceGroupBenchmarks.ListResourceGroups`:

1. **Root coverage is full** — `CLI.Activity.Root[0]` shows `10/10`.
2. **The repeated sibling is indexed and partially covered** — the fixture gives 2 sessions to traces 0–3 and 1 to the rest, so `SessionCreated[0]` shows `10/10` and `SessionCreated[1]` shows `4/10`.
3. **The truncated trace lowers one leaf** — trace 7 omits `AzureCLI.Startup.Telemetry`, so it shows `9/10`.
4. **The orphan is counted, not rendered** — the header reports `1 orphans`, and no path contains `Detached.Span`.

- [ ] **Step 5: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): aggregate sampled traces into averaged span trees"
```

---

### Task 3: ASCII rendering

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py`

**Interfaces:**
- Consumes: `Report`, `BAR_WIDTH`, `BLOCKS` from Tasks 1–2.
- Produces: `bar(ratio) -> str`, `label(path, report) -> str`, `render_report(report) -> str | None`.

- [ ] **Step 1: Add the renderer**

Insert a new section after aggregation:

```python
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
        paths.sort(key=report.mean_offset)  # execution order, like an App Insights view

    root_average = report.average(root)
    # A zero root makes %ROOT undefined; fall back to durations only.
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
            # Percentages are printed uncapped so a >100% anomaly stays visible,
            # even though bar() clamps the bar itself to full width.
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
```

- [ ] **Step 2: Print rendered trees from `run`**

In `run`, replace the `if args.debug_paths:` block's trailing `return 0` region so rendering happens when `--debug-paths` is not set. The final lines of `run` become:

```python
    if args.debug_paths:
        for report in usable:
            print(f"\n== {report.stem} ({report.trace_count} traces, "
                  f"{report.orphan_subtrees} orphans, {report.malformed} malformed)")
            for path in sorted(report.nodes, key=lambda p: (len(p), report.mean_offset(p))):
                trail = " > ".join(f"{name}[{index}]" for name, index in path)
                print(f"   {report.average(path):9.3f} ms  "
                      f"{report.occurrences(path):3d}/{report.trace_count}  {trail}")
        return 0

    for report in usable:
        body = render_report(report)
        if body:
            print("\n" + body)

    return 0
```

- [ ] **Step 3: Verify the rendered tree**

```powershell
$env:PYTHONIOENCODING='utf-8'
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\trace-fixture\ci
```

Expected shape for the CLI benchmark (this is verified actual output, not an approximation — bar glyphs included):

```
 Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups
 RESOURCEGROUP · CLI · 10 traces

         AVG  %ROOT
   42.450 ms   100%  ████████████████████  CLI.Activity.Root
    8.450 ms    20%  ████                  ├─ AzureCLI.Startup
    0.600 ms     1%  ▎                     │  ├─ AzureCLI.Startup.Container
    3.000 ms     7%  █▍                    │  ├─ AzureCLI.Startup.Services
    4.400 ms    10%  ██▏                   │  └─ AzureCLI.Startup.Telemetry  (9/10)
   28.450 ms    67%  █████████████▍        └─ AzureCLI.ProcessCommand
    3.000 ms     7%  █▍                       ├─ SessionCreated
    2.000 ms     5%  █                        └─ SessionCreated[1]  (4/10)

 1 orphan subtree(s) dropped
```

Verify by eye:

1. **Columns line up** — the `%ROOT` header sits directly above the percentage values.
2. **Execution order** — `AzureCLI.Startup` precedes `AzureCLI.ProcessCommand`, and `Container` precedes `Services` precedes `Telemetry`.
3. **Connectors are correct** — the last child of each parent uses `└─`, others `├─`, and grandchildren under a non-last parent are prefixed `│  `.
4. **Markers appear only where earned** — no `(10/10)`, no `[0]` suffix.
5. **The root bar is full** at 20 cells and 100%.

- [ ] **Step 4: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): render aggregated span trees as ASCII"
```

---

### Task 4: Job summary and PR comment output

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py`

**Interfaces:**
- Consumes: `render_report`, `Report`, `COMMENT_MARKER`, `SUMMARY_BUDGET_BYTES`.
- Produces: `render_summary(reports) -> str`, `render_comment(reports, run_url) -> str | None`. `--summary-out` **appends** (the runner may already have written to `$GITHUB_STEP_SUMMARY`); `--comment-out` overwrites.

- [ ] **Step 1: Add the output renderers**

Insert a new section after rendering:

```python
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
```

- [ ] **Step 2: Route output through the new renderers**

Replace the final rendering loop in `run` (the `for report in usable: body = render_report(...)` block and its `return 0`) with:

```python
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
```

- [ ] **Step 3: Verify both outputs and the never-fail contract**

```powershell
$env:PYTHONIOENCODING='utf-8'
python .github\scripts\aggregate-benchmark-traces.py `
  --input artifacts\trace-fixture\ci `
  --summary-out artifacts\summary.md `
  --comment-out artifacts\comment.md `
  --run-url https://github.com/JaysonTaiMicrosoft/cirrus/actions/runs/123
Get-Content artifacts\comment.md
```

Expected `comment.md` — two rows, one per product, ending with the marker:

```markdown
## 🔬 Benchmark Trace Hierarchy

| Extension | Product | Benchmark | Traces | Root avg |
|---|---|---|---|---|
| RESOURCEGROUP | CLI | `ListResourceGroups` | 10 | 42.450 ms |
| RESOURCEGROUP | PS | `GetAzResourceGroup` | 5 | 42.200 ms |

[📊 View full trace hierarchies](https://github.com/JaysonTaiMicrosoft/cirrus/actions/runs/123)

<!-- benchmark-traces -->
```

Confirm `artifacts\summary.md` opens with `## 🔬 Benchmark Trace Hierarchy` and wraps each tree in a fenced block.

Then prove the never-fail contract with a deliberately corrupt file:

```powershell
"not json at all" | Out-File -Encoding utf8 artifacts\trace-fixture\ci\RESOURCEGROUP\CLI\telemetry\broken.jsonl
python .github\scripts\aggregate-benchmark-traces.py --input artifacts\trace-fixture\ci
echo "exit=$LASTEXITCODE"
Remove-Item artifacts\trace-fixture\ci\RESOURCEGROUP\CLI\telemetry\broken.jsonl
```

Expected: `exit=0`. The corrupt file yields no traces and is skipped; the two real benchmarks still render.

- [ ] **Step 4: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): emit trace report to job summary and PR comment body"
```

---

### Task 5: Wire the aggregator into the benchmark workflow

**Files:**
- Modify: `.github/workflows/benchmark.yml` — the `collect` job, currently at lines 404–439.

**Interfaces:**
- Consumes: `.github/scripts/aggregate-benchmark-traces.py` from Tasks 1–4.
- Produces: a workflow artifact named `benchmark-traces` containing `trace-comment.md`, consumed by Task 6.

**Critical ordering constraint:** `actions/checkout` defaults to `clean: true` and wipes the workspace. It **must** run before `Download all benchmark results`, or the downloaded artifacts are deleted.

- [ ] **Step 1: Add checkout ahead of the existing download step**

In the `collect` job, insert immediately after the `Harden runner` step and **before** `Download all benchmark results`:

```yaml
      - uses: actions/checkout@v4
```

- [ ] **Step 2: Add the aggregation and upload steps**

Append to the end of the `collect` job's `steps:` list, after `Upload BDN logs (always)`:

```yaml
      - name: Aggregate trace hierarchies
        if: always()
        env:
          RUN_URL: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
        run: |
          python3 .github/scripts/aggregate-benchmark-traces.py \
            --input downloaded \
            --summary-out "$GITHUB_STEP_SUMMARY" \
            --comment-out "$RUNNER_TEMP/trace-comment.md" \
            --run-url "$RUN_URL"

      - name: Upload trace hierarchy comment
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-traces
          path: ${{ runner.temp }}/trace-comment.md
          retention-days: 1
          if-no-files-found: ignore
```

Leave the job's `permissions` alone. It keeps the workflow-level `contents: read` and deliberately does **not** gain `pull-requests: write`, because it no longer posts anything — Task 6 owns posting.

- [ ] **Step 3: Verify the workflow parses and the step order is right**

```powershell
python -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/benchmark.yml',encoding='utf-8')); s=[x.get('name') or x.get('uses') for x in d['jobs']['collect']['steps']]; print('\n'.join(str(i) for i in s))"
```

Expected order — `actions/checkout@v4` appears before `Download all benchmark results`:

```
step-security/harden-runner@v2
actions/checkout@v4
Download all benchmark results
Upload benchmark results for fork PR
Upload BDN logs (always)
Aggregate trace hierarchies
Upload trace hierarchy comment
```

If `yaml` is not installed locally, skip the command and verify the order by reading the file — the ordering claim is what matters, not the tool.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/benchmark.yml
git commit -m "ci(bench): aggregate trace hierarchies in the collect job"
```

---

### Task 6: Post the trace comment, including on forks

**Files:**
- Modify: `.github/workflows/benchmark-comment.yml` — add a job alongside the existing `comment` job.

**Interfaces:**
- Consumes: the `benchmark-traces` artifact from Task 5, containing `trace-comment.md` whose last line is `<!-- benchmark-traces -->`.
- Produces: nothing consumed downstream.

**Why a separate workflow at all:** a fork PR's `GITHUB_TOKEN` is read-only, so `collect` can never post from there. This workflow triggers on `workflow_run`, which executes in the base repository with a writable token and can pull artifacts from the triggering run by `run-id`.

**Do not touch the existing `comment` job.** Its fork-only `if` must stay: for non-fork PRs, `github-action-benchmark`'s `comment-always` already posts the results comment, and relaxing that condition would produce a duplicate.

- [ ] **Step 1: Append the new job**

Add at the end of `.github/workflows/benchmark-comment.yml`, at the same indentation as the existing `comment:` job:

```yaml
  # Posts the trace hierarchy index for every PR, fork or not. The body is
  # rendered once in the benchmark workflow's collect job and only delivered
  # here, so fork and same-repo comments are byte-identical.
  trace-comment:
    name: Post trace hierarchy on PR
    runs-on: ubuntu-latest
    if: github.event.workflow_run.event == 'pull_request'
    steps:
      - name: Harden runner
        uses: step-security/harden-runner@v2
        with:
          egress-policy: audit

      - name: Download trace comment artifact
        id: download
        uses: actions/download-artifact@v4
        with:
          name: benchmark-traces
          path: ${{ runner.temp }}/benchmark-traces
          run-id: ${{ github.event.workflow_run.id }}
          github-token: ${{ secrets.GITHUB_TOKEN }}
        continue-on-error: true

      - name: Find PR number
        if: steps.download.outcome == 'success'
        id: find-pr
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          HEAD_SHA: ${{ github.event.workflow_run.head_sha }}
        run: |
          PR_NUMBER=$(gh pr list \
            --repo "${{ github.repository }}" \
            --state open \
            --json number,headRefOid \
            --jq ".[] | select(.headRefOid == \"$HEAD_SHA\") | .number" 2>/dev/null || true)

          if [ -z "$PR_NUMBER" ] || [ "$PR_NUMBER" = "null" ]; then
            echo "::warning::Could not resolve a PR for $HEAD_SHA"
            exit 1
          fi
          echo "pr_number=$PR_NUMBER" >> "$GITHUB_OUTPUT"

      - name: Post trace hierarchy comment
        if: steps.download.outcome == 'success' && steps.find-pr.outcome == 'success'
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          PR_NUMBER: ${{ steps.find-pr.outputs.pr_number }}
          BODY_FILE: ${{ runner.temp }}/benchmark-traces/trace-comment.md
        run: |
          python3 << 'PYEOF'
          import os, subprocess, sys

          body_file = os.environ["BODY_FILE"]
          if not os.path.exists(body_file):
              print("No trace comment body was produced; nothing to post.")
              sys.exit(0)

          with open(body_file, encoding="utf-8") as handle:
              body = handle.read()

          marker = "<!-- benchmark-traces -->"
          pr = os.environ["PR_NUMBER"]
          repo = os.environ["GITHUB_REPOSITORY"]

          found = subprocess.run(
              ["gh", "api", f"repos/{repo}/issues/{pr}/comments",
               "--jq", f'[.[] | select(.body | contains("{marker}")) | .id][0]'],
              capture_output=True, text=True,
          )
          existing_id = found.stdout.strip()

          if existing_id and existing_id != "null":
              subprocess.run(
                  ["gh", "api", "--method", "PATCH",
                   f"repos/{repo}/issues/comments/{existing_id}",
                   "-f", f"body={body}"],
                  check=True,
              )
              print(f"Updated trace comment {existing_id} on PR #{pr}")
          else:
              subprocess.run(
                  ["gh", "api", "--method", "POST",
                   f"repos/{repo}/issues/{pr}/comments",
                   "-f", f"body={body}"],
                  check=True,
              )
              print(f"Posted trace comment on PR #{pr}")
          PYEOF
```

- [ ] **Step 2: Verify the workflow parses and the existing job is untouched**

```powershell
python -c "import yaml; d=yaml.safe_load(open('.github/workflows/benchmark-comment.yml',encoding='utf-8')); print(list(d['jobs'].keys())); print(d['jobs']['comment']['if'])"
```

Expected — both jobs present, and the original fork-only condition unchanged:

```
['comment', 'trace-comment']
github.event.workflow_run.event == 'pull_request' && github.event.workflow_run.head_repository.full_name != github.event.workflow_run.repository.full_name
```

Also confirm by reading the file that `trace-comment`'s `if` has **no** fork clause — that is the whole point of the task.

- [ ] **Step 3: Clean up the dev fixture**

The fixture lives under gitignored `artifacts/`, so it cannot be committed by accident, but leave the tree tidy:

```powershell
Remove-Item -Recurse -Force artifacts\trace-fixture, artifacts\make-trace-fixture.py, artifacts\summary.md, artifacts\comment.md -ErrorAction SilentlyContinue
git status --short
```

Expected: no untracked files under `artifacts/`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/benchmark-comment.yml
git commit -m "ci(bench): post trace hierarchy comment on all PRs including forks"
```

---

## Post-merge verification

The only true test is a real run — the fixture proves the logic, not the wiring.

1. Open a PR touching `src/**` or `tests/Benchmark/**` so the `benchmark.yml` path filters fire.
2. On the completed run, open the **Collect benchmark artifacts** job and confirm the trace hierarchy renders in its summary, with real operation names (`CLI.Activity.Root`, `AzureCLI.Startup`, `AzureCLI.ProcessCommand`).
3. Confirm the PR gained a comment carrying `<!-- benchmark-traces -->`, distinct from the existing `<!-- benchmark-results -->` comment, and that its link opens the run.
4. Push a second commit and confirm the comment is **updated in place** rather than duplicated.
5. Sanity-check the numbers against a known Application Insights end-to-end view for the same operation: the nesting and the relative proportions should agree, even though absolute durations will not, since the benchmark stubs HTTP.

## Known gaps carried forward

- No automated tests. A merge bug produces a plausible-looking tree with wrong numbers rather than a crash. `unittest` coverage of `flatten` and `Report` is the obvious first follow-up.
- Sibling indexing is positional. If occurrence order ever stops being stable, indexed rows blend unrelated calls; low coverage on higher indices is the signal to watch.
- No baseline comparison. The tree shows where time goes, not what changed.
