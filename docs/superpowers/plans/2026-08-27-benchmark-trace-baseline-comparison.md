# Benchmark Trace Baseline Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist each benchmark's aggregated span tree as a JSON sidecar, publish `main`'s sidecars to gh-pages, and render `main` versus the pull request as two trees side by side in the workflow job summary.

**Architecture:** Extend the existing `.github/scripts/aggregate-benchmark-traces.py` with serialisation (`Report.to_dict` / `from_dict`), a baseline loader, a union-tree row builder, and a side-by-side renderer. The `collect` job sparse-checks-out gh-pages to read baselines and uploads this run's sidecars; a new `publish-traces` job commits them to gh-pages on pushes to `main`. No C# changes — the telemetry producer already emits everything required.

**Tech Stack:** Python 3 standard library only (no pip, matching the existing script's constraint), GitHub Actions, gh-pages.

**Spec:** [`docs/superpowers/specs/2026-08-27-benchmark-trace-baseline-comparison-design.md`](../specs/2026-08-27-benchmark-trace-baseline-comparison-design.md)

## Global Constraints

- **Standard library only.** The script runs on a bare runner with no `pip install`. No third-party imports, ever.
- **Telemetry must never fail a benchmark run.** `main()` stays wrapped; every failure prints `::warning::` to stderr and returns exit code `0`.
- **Omitting `--baseline` must reproduce v1 output byte-for-byte.** Push-to-main runs and ad-hoc local invocations depend on this.
- **No automated tests.** This was an explicit user decision. Every task therefore ends with a concrete manual verification command and its expected output. Do not add a test framework.
- **`benchmark-comment.yml` is not modified.** The existing PR comment keeps posting v1's index table.
- **Nothing under `src/` or `tests/Benchmark/*.cs` changes.**
- **Sidecar schema version is `1`.** An unrecognised `schema` value is treated as a missing baseline, never partially read.
- **Δ% is never flagged, thresholded, or marked.** Print the number or `n/a`.
- **Existing v1 behaviour must not regress:** `render_report`, `render_comment`, `discover`, `aggregate_file`, and the 1 MiB summary byte budget all stay working.

## File Structure

| File | Responsibility | Change |
| --- | --- | --- |
| `.github/scripts/aggregate-benchmark-traces.py` | Discovery, aggregation, serialisation, rendering, CLI | Modify — all script work lands here, matching v1's single-file design |
| `.github/workflows/benchmark.yml` | Benchmark orchestration | Modify — `collect` gains gh-pages checkout + new flags + sidecar artifact; new `publish-traces` job |
| `tests/Benchmark/README.md` | Benchmark suite documentation | Modify — sidecar format, local `--baseline` workflow, bar-comparability caveat |

The script stays one file. It is ~440 lines today and will reach ~700; v1 deliberately chose a single stdlib script so the `collect` job needs no build step, and splitting it would require either a package directory or `sys.path` manipulation for no benefit.

Section ordering inside the script is preserved — discovery, parsing, tree building, aggregation, rendering, outputs, entry point — with new code added to the matching section.

---

### Task 1: Sidecar serialisation

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (constants block at lines 20–25; `Report` class at lines 185–225)

**Interfaces:**
- Consumes: nothing (first task)
- Produces:
  - `SIDECAR_SCHEMA = 1` — module constant
  - `Report.source` — instance attribute, `dict`, defaults to `{}`
  - `Report.to_dict(self, commit=None, ref=None, run_id=None, generated=None) -> dict`
  - `Report.from_dict(cls, data) -> Report` — classmethod, raises `ValueError` on unknown schema

- [ ] **Step 1: Add the schema constant**

In the constants block near the top of the file (after `FAR_FUTURE` on line 25), add:

```python
SIDECAR_SCHEMA = 1
```

- [ ] **Step 2: Give `Report` a `source` attribute**

In `Report.__init__`, after `self.rootless_traces = 0`, add:

```python
        self.source = {}
```

This keeps a freshly-aggregated report and a deserialised one structurally identical, so `render_comparison` can read `report.source` on either without a `hasattr` check.

- [ ] **Step 3: Add `to_dict`**

Add these methods to `Report`, after the `root_path` property (which ends on line 225):

```python
    def to_dict(self, commit=None, ref=None, run_id=None, generated=None):
        """Serialize to the sidecar shape. Averages are stored, not the running totals.

        offset_ms is not cosmetic: sibling ordering is derived from it, so a sidecar
        without it could not be rendered in execution order and the two sides of a
        comparison would draw children in different sequences.
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

        Totals are reconstructed as avg * count so average() and mean_offset() keep
        working unchanged against a deserialised report.
        """
        if not isinstance(data, dict):
            raise ValueError(f"sidecar is {type(data).__name__}, expected object")
        if data.get("schema") != SIDECAR_SCHEMA:
            raise ValueError(f"unsupported sidecar schema {data.get('schema')!r}")

        report = cls(data.get("benchmark") or "", data.get("product"), data.get("extension"))
        report.trace_count = int(data.get("traces") or 0)
        diagnostics = data.get("diagnostics") or {}
        report.malformed = int(diagnostics.get("malformed") or 0)
        report.orphan_subtrees = int(diagnostics.get("orphan_subtrees") or 0)
        report.rootless_traces = int(diagnostics.get("rootless_traces") or 0)
        report.source = data.get("source") or {}

        for node in data.get("nodes") or []:
            path = tuple((str(name), int(index)) for name, index in node["path"])
            count = int(node.get("occurrences") or 0)
            average = float(node.get("avg_ms") or 0.0)
            offset = float(node.get("offset_ms") or 0.0)
            report.nodes[path] = {
                "total": average * count,
                "count": count,
                "offset_total": offset * count,
            }
        return report
```

- [ ] **Step 4: Create the fixture generator**

There is no committed telemetry to test against, and a real benchmark run takes hours (5,000 warmup + 5,000 measurement iterations). Create this generator **outside the repository** so it is never committed — use `$RUNNER_TEMP`, `/tmp`, or `$env:TEMP`.

Save as `<tmp>/make-fixture.py`:

```python
"""Generate synthetic benchmark telemetry for manual validation.

Writes the CI directory layout (<EXT>/<PRODUCT>/telemetry/<stem>.jsonl) so that
discovery, product_of, and extension_of are all exercised.
"""
import json, os, sys
from datetime import datetime, timedelta, timezone

# (OperationName, parent OperationName or None, duration_ms)
TREE = [
    ("CLI.Activity.Root", None, 21.962),
    ("AzureCLI.Startup", "CLI.Activity.Root", 4.435),
    ("AzureCLI.Startup.Container", "AzureCLI.Startup", 0.050),
    ("AzureCLI.Startup.Services", "AzureCLI.Startup", 0.487),
    ("AzureCLI.Startup.Telemetry", "AzureCLI.Startup", 3.483),
    ("AzureCLI.ParseCommandInvocation", "CLI.Activity.Root", 2.587),
    ("SessionCreated", "AzureCLI.ParseCommandInvocation", 1.027),
    ("AzureCLI.ProcessCommand", "CLI.Activity.Root", 10.725),
    ("ProcessorProcessAsyncStart", "AzureCLI.ProcessCommand", 9.640),
]


def build(path, traces, scale, drop=None):
    """drop: an OperationName omitted from every trace, to simulate shape drift."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    base = datetime(2026, 8, 20, tzinfo=timezone.utc)
    with open(path, "w", encoding="utf-8") as handle:
        for trace in range(traces):
            ids = {}
            start = base + timedelta(seconds=trace)
            for index, (name, parent, duration) in enumerate(TREE):
                if name == drop:
                    continue
                ids[name] = f"{trace:04d}{index:04d}"
                # 7 fractional digits, matching .NET's round-trip "O" format.
                stamp = (start + timedelta(milliseconds=index * 0.1)).strftime(
                    "%Y-%m-%dT%H:%M:%S.%f") + "0Z"
                handle.write(json.dumps({
                    "TraceId": f"trace{trace:04d}",
                    "SpanId": ids[name],
                    "ParentSpanId": ids.get(parent) if parent else None,
                    "OperationName": name,
                    "StartTime": stamp,
                    "Duration": round(duration * scale, 4),
                }) + "\n")


root = sys.argv[1]
stem = "CLI.ResourceGroupBenchmarks.CreateResourceGroup"
build(os.path.join(root, "main", "RESOURCES", "CLI", "telemetry", f"{stem}.jsonl"),
      100, 1.0)
# The PR run is 20% slower and has lost AzureCLI.Startup.Services entirely.
build(os.path.join(root, "pr", "RESOURCES", "CLI", "telemetry", f"{stem}.jsonl"),
      100, 1.2, drop="AzureCLI.Startup.Services")
print(f"fixtures written under {root}")
```

Generate them:

```bash
python3 <tmp>/make-fixture.py <tmp>/fixtures
```

Expected: `fixtures written under <tmp>/fixtures`, with `<tmp>/fixtures/main/RESOURCES/CLI/telemetry/` and `<tmp>/fixtures/pr/...` each holding one `.jsonl` of 900 lines (100 traces × 9 spans; the PR side has 800).

- [ ] **Step 5: Verify the round-trip preserves every value**

```bash
python3 -c "
import importlib.util, sys
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)

stem = 'CLI.ResourceGroupBenchmarks.CreateResourceGroup'
path = '<tmp>/fixtures/main/RESOURCES/CLI/telemetry/' + stem + '.jsonl'
original = agg.aggregate_file(path, stem, 'CLI', 'RESOURCES')
restored = agg.Report.from_dict(original.to_dict(commit='abc1234', ref='refs/heads/main', run_id='42'))

assert set(original.nodes) == set(restored.nodes), 'node sets differ'
assert original.trace_count == restored.trace_count, 'trace counts differ'
for p in original.nodes:
    assert abs(original.average(p) - restored.average(p)) < 1e-9, ('avg', p)
    assert abs(original.mean_offset(p) - restored.mean_offset(p)) < 1e-9, ('offset', p)
    assert original.occurrences(p) == restored.occurrences(p), ('occ', p)
assert restored.source['commit'] == 'abc1234'
assert original.root_path == restored.root_path
print('round-trip OK:', len(original.nodes), 'nodes,', original.trace_count, 'traces')
"
```

Expected: `round-trip OK: 9 nodes, 100 traces`

- [ ] **Step 6: Verify an unknown schema is rejected**

```bash
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)
for bad in [{'schema': 99}, {'schema': None}, {}, []]:
    try:
        agg.Report.from_dict(bad); print('FAIL: accepted', bad)
    except ValueError as exc:
        print('rejected:', exc)
"
```

Expected: four `rejected:` lines, no `FAIL`.

- [ ] **Step 7: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): serialize aggregated span trees to a sidecar shape"
```

---

### Task 2: Write sidecars from a run

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (new `outputs` section helpers; `run` at line 379; `main` at line 418)

**Interfaces:**
- Consumes: `Report.to_dict(commit, ref, run_id, generated)`, `SIDECAR_SCHEMA` (Task 1)
- Produces:
  - `sidecar_path(root, report) -> str`
  - `write_sidecars(root, reports, commit, ref, run_id) -> int` (count written)
  - CLI flags `--sidecar-out`, `--commit`, `--ref`, `--run-id`
  - The `if args.sidecar_out:` block in `run()`, which Task 3 Step 3 extends

- [ ] **Step 1: Add the writer**

In the `outputs` section, before `render_summary` (line 326), add:

```python
def sidecar_path(root, report):
    """<root>/<EXTENSION>/<PRODUCT>/<stem>.json.

    Local runs have no extension segment in their telemetry path, so extension is
    None; 'LOCAL' keeps those files addressable instead of collapsing them into the
    root directory. Stems are dotted alphanumerics, so they need no escaping.
    """
    return os.path.join(root, report.extension or "LOCAL",
                        report.product or "UNKNOWN", report.stem + ".json")


def write_sidecars(root, reports, commit, ref, run_id):
    """Write one sidecar per report. Returns how many were written.

    One timestamp is shared across the whole run so every sidecar from a single
    workflow run reports the same vintage.
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
        except OSError as exc:
            # A sidecar is regenerable, so a write failure must not cost us the summary.
            print(f"::warning::could not write sidecar {path}: {exc}", file=sys.stderr)
    return written
```

- [ ] **Step 2: Register the CLI flags**

In `main()`, after the `--input` argument (line 420), add:

```python
    parser.add_argument("--sidecar-out", help="write this run's trees as JSON here")
    parser.add_argument("--commit", help="commit sha recorded in the sidecar")
    parser.add_argument("--ref", help="git ref recorded in the sidecar")
    parser.add_argument("--run-id", help="workflow run id recorded in the sidecar")
```

- [ ] **Step 3: Call the writer from `run`**

In `run()`, immediately after the `usable = [...]` / `print(...)` pair (lines 386–387) and before the `if args.debug_paths:` block, add:

```python
    if args.sidecar_out:
        written = write_sidecars(args.sidecar_out, usable,
                                 args.commit, args.ref, args.run_id)
        print(f"[traces] wrote {written} sidecar(s) to {args.sidecar_out}")
```

Task 3 Step 3 extends this block to render from the files just written. Leave it as-is for now so this task stands on its own.

Only `usable` reports are written: a report with zero traces has no root and no nodes, so it would serialise to an empty tree that a later run would treat as a real baseline of nothing.

- [ ] **Step 4: Generate a sidecar and inspect it**

```bash
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/main \
  --sidecar-out <tmp>/sidecars-main \
  --commit abc1234def5678 --ref refs/heads/main --run-id 17384920011
```

Expected output includes:
```
[traces] aggregated 1 benchmark(s) from 1 file(s)
[traces] wrote 1 sidecar(s) to <tmp>/sidecars-main
```

Then confirm the path and the shape:

```bash
cat <tmp>/sidecars-main/RESOURCES/CLI/CLI.ResourceGroupBenchmarks.CreateResourceGroup.json | head -30
```

Expected: `"schema": 1`, `"extension": "RESOURCES"`, `"product": "CLI"`, `"traces": 100`, `source.commit` of `abc1234def5678`, and a `nodes` array whose first entry is `CLI.Activity.Root` with `avg_ms` near `21.962`.

- [ ] **Step 5: Confirm v1 output is unchanged**

The summary is still printed, and `--sidecar-out` must not have altered it:

```bash
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/fixtures/main > <tmp>/v1-before.txt
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/fixtures/main \
  --sidecar-out <tmp>/throwaway > <tmp>/v1-after.txt
diff <(grep -v '^\[traces\] wrote' <tmp>/v1-after.txt) <tmp>/v1-before.txt && echo "IDENTICAL"
```

Expected: `IDENTICAL`

- [ ] **Step 6: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): write trace sidecars with run provenance"
```

---

### Task 3: Load a baseline

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (new helpers after `write_sidecars`; `main` argument parser)

**Interfaces:**
- Consumes: `Report.from_dict` (Task 1), `sidecar_path`, `write_sidecars` (Task 2)
- Produces:
  - `load_sidecar(path) -> Report | None`
  - `find_baseline(root, report) -> Report | None`
  - `baselines` — local in `run()`, a `dict[stem, Report]` consumed by Task 6
  - CLI flag `--baseline`

- [ ] **Step 1: Add the loader and the lookup**

After `write_sidecars`, add:

```python
def load_sidecar(path):
    """Read one sidecar. Returns None for anything unusable, having warned."""
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return Report.from_dict(json.load(handle))
    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError) as exc:
        # A bad baseline degrades to no baseline, which renders the v1 single tree.
        print(f"::warning::ignoring baseline sidecar {path}: {exc}", file=sys.stderr)
        return None


def find_baseline(root, report):
    """Locate this benchmark's baseline under root, or None.

    The exact <EXT>/<PRODUCT>/<stem>.json is tried first. A local run's telemetry
    path carries no extension segment, so its reports have extension None and only
    the recursive fallback can match; the stem alone identifies the benchmark.
    """
    filename = report.stem + ".json"
    exact = sidecar_path(root, report)
    if os.path.isfile(exact):
        return load_sidecar(exact)
    for dirpath, _dirnames, filenames in os.walk(root):
        if filename in filenames:
            return load_sidecar(os.path.join(dirpath, filename))
    return None
```

- [ ] **Step 2: Register the flag**

In `main()`, after `--run-id`, add:

```python
    parser.add_argument("--baseline", help="directory of sidecars to compare against")
```

- [ ] **Step 3: Render from the sidecars just written, not from memory**

Extend the `--sidecar-out` block added in Task 2 Step 3 so it ends with a reload:

```python
    if args.sidecar_out:
        written = write_sidecars(args.sidecar_out, usable,
                                 args.commit, args.ref, args.run_id)
        print(f"[traces] wrote {written} sidecar(s) to {args.sidecar_out}")
        # Render from what was just written. Both sides of a comparison then travel
        # the same to_dict/from_dict path, so a field that fails to round-trip
        # surfaces here rather than silently only on the baseline side. It is also
        # what gives this run's tree its provenance header: an in-memory report has
        # an empty source, so the heading would read "PR · 100 traces" with no commit.
        usable = [load_sidecar(sidecar_path(args.sidecar_out, report)) or report
                  for report in usable]
```

This is the symmetric-rendering decision from the spec, not an optimisation. Do not drop it: without the reload the two sides of every comparison are built by different code paths, and this run's tree loses its commit in the header.

- [ ] **Step 4: Resolve baselines in `run`**

Directly after the block above, add:

```python
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
```

A missing directory is a normal outcome — gh-pages may not have `dev/bench/traces/` yet — so it prints a note rather than warning.

- [ ] **Step 5: Verify the reload preserves the reports and adds provenance**

```bash
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/main --sidecar-out <tmp>/sidecars-main \
  --commit abc1234def5678 --ref refs/heads/main --run-id 17384920011 \
  > <tmp>/reload-check.txt
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)
stem = 'CLI.ResourceGroupBenchmarks.CreateResourceGroup'
report = agg.load_sidecar('<tmp>/sidecars-main/RESOURCES/CLI/' + stem + '.json')
assert report is not None, 'sidecar did not load'
assert report.source['commit'] == 'abc1234def5678', report.source
assert report.trace_count == 100 and len(report.nodes) == 9
print('reload OK:', report.stem, report.source['commit'][:7])
"
grep -c 'Benchmark.CLI.ResourceGroupBenchmarks' <tmp>/reload-check.txt
```

Expected: `reload OK: CLI.ResourceGroupBenchmarks.CreateResourceGroup abc1234` and a count of `1` — the summary still renders one tree after the swap.

- [ ] **Step 6: Verify exact-path lookup**

```bash
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/pr --baseline <tmp>/sidecars-main
```

Expected: `[traces] matched 1/1 baseline(s) from <tmp>/sidecars-main`

- [ ] **Step 7: Verify the fallback, the missing directory, and a corrupt file**

```bash
# Fallback: flatten the layout so the exact path cannot match.
mkdir -p <tmp>/flat/somewhere/else
cp <tmp>/sidecars-main/RESOURCES/CLI/*.json <tmp>/flat/somewhere/else/
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/pr --baseline <tmp>/flat | grep matched

# Missing directory.
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/pr --baseline <tmp>/does-not-exist | grep baseline

# Corrupt file.
mkdir -p <tmp>/corrupt/RESOURCES/CLI
echo 'not json{' > <tmp>/corrupt/RESOURCES/CLI/CLI.ResourceGroupBenchmarks.CreateResourceGroup.json
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/pr --baseline <tmp>/corrupt; echo "exit=$?"
```

Expected, in order: `matched 1/1`; `baseline directory '<tmp>/does-not-exist' not found; rendering without comparison`; then

```
::warning::ignoring baseline sidecar <tmp>/corrupt/.../CreateResourceGroup.json: Expecting value: line 1 column 1 (char 0)
[traces] matched 0/1 baseline(s) from <tmp>/corrupt
```

followed by a normal v1 summary and `exit=0`.

- [ ] **Step 8: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): load baseline trace sidecars for comparison"
```

---

### Task 4: Build the union row model

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (rendering section, after `label` at line 264)

**Interfaces:**
- Consumes: `Report.nodes`, `Report.mean_offset`, `Report.root_path`
- Produces:
  - `node_text(path) -> str`
  - `compare_rows(baseline, current) -> list[tuple[path, connector]]`
  - `delta_percent(baseline_avg, current_avg) -> float | None`

- [ ] **Step 1: Add the row builder**

After the existing `label` function (which ends on line 271), add:

```python
def node_text(path):
    """v1's node label without the coverage marker: name, plus [n] from the second
    occurrence on."""
    name, index = path[-1]
    return name if index == 0 else f"{name}[{index}]"


def delta_percent(baseline_average, current_average):
    """None when the comparison is undefined: a one-sided node, or a zero baseline.

    A one-sided node has not regressed — it was added, removed, or renamed — so
    reporting +/-100% would misattribute a shape change as a timing change.
    """
    if baseline_average is None or current_average is None or baseline_average == 0:
        return None
    return (current_average - baseline_average) / baseline_average * 100.0


def compare_rows(baseline, current):
    """Rows over the union of both node sets, in one shared order.

    Returns [(path, connector)]. Both sides render from this single list, which is
    what makes "row n is the same node on the left and the right" a guarantee rather
    than a coincidence: two independently ordered trees would drift apart the moment
    one side gained or lost a node.

    Ordering uses the current run's mean start offset where the node exists there and
    the baseline's otherwise, tie-broken on the path for determinism.
    """
    roots = []
    for report in (current, baseline):
        root = report.root_path
        if root is not None and root not in roots:
            roots.append(root)
    if not roots:
        return []

    root_set = set(roots)
    paths = {path for path in set(current.nodes) | set(baseline.nodes)
             if path[:1] in root_set}

    def offset(path):
        if path in current.nodes:
            return current.mean_offset(path)
        return baseline.mean_offset(path)

    children_of = defaultdict(list)
    for path in paths:
        # flatten() emits every ancestor of every node it collects, so a node's
        # parent is always present on the side that produced it, and therefore in
        # the union. Depth-1 paths are the roots themselves.
        if len(path) > 1 and path[:-1] in paths:
            children_of[path[:-1]].append(path)
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
    return rows
```

- [ ] **Step 2: Verify the union covers both sides and stays ordered**

```bash
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)

stem = 'CLI.ResourceGroupBenchmarks.CreateResourceGroup'
main = agg.aggregate_file('<tmp>/fixtures/main/RESOURCES/CLI/telemetry/' + stem + '.jsonl', stem, 'CLI', 'RESOURCES')
pr   = agg.aggregate_file('<tmp>/fixtures/pr/RESOURCES/CLI/telemetry/' + stem + '.jsonl', stem, 'CLI', 'RESOURCES')

rows = agg.compare_rows(main, pr)
print('union rows:', len(rows), '| main nodes:', len(main.nodes), '| pr nodes:', len(pr.nodes))
for path, connector in rows:
    side = 'both'
    if path not in pr.nodes: side = 'MAIN-ONLY'
    elif path not in main.nodes: side = 'PR-ONLY'
    print(f'  {connector}{agg.node_text(path):<40} {side}')
assert len(rows) == len(set(main.nodes) | set(pr.nodes)), 'union incomplete'
print('OK')
"
```

Expected: `union rows: 9 | main nodes: 9 | pr nodes: 8`, an indented tree in execution order (`AzureCLI.Startup` before `AzureCLI.ProcessCommand`, `Startup.Container` before `Startup.Services`), `AzureCLI.Startup.Services` tagged `MAIN-ONLY`, and a final `OK`.

- [ ] **Step 3: Verify `delta_percent` edge cases**

```bash
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)
cases = [(10.0, 12.0, 20.0), (10.0, 9.0, -10.0), (10.0, 10.0, 0.0),
         (None, 5.0, None), (5.0, None, None), (0.0, 5.0, None)]
for base, cur, want in cases:
    got = agg.delta_percent(base, cur)
    ok = (got is None and want is None) or (got is not None and abs(got - want) < 1e-9)
    print(('PASS' if ok else 'FAIL'), base, cur, '->', got)
"
```

Expected: six `PASS` lines.

- [ ] **Step 4: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): build a union row model across two span trees"
```

---

### Task 5: Render the side-by-side comparison

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (rendering section, after `render_report` at line 321)

**Interfaces:**
- Consumes: `compare_rows`, `node_text`, `delta_percent` (Task 4); `bar` (line 253); `parse_time` (line 68)
- Produces:
  - `relative_age(generated) -> str | None`
  - `render_comparison(baseline, current) -> str | None`

- [ ] **Step 1: Add the age helper**

After `render_report` (ends line 321), add:

```python
def relative_age(generated):
    """Coarse age of a baseline, or None when the timestamp is unusable.

    Days are enough: the point is to let a reader discount a stale baseline, not to
    time it precisely.
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
```

- [ ] **Step 2: Add the comparison renderer**

Immediately after `relative_age`, add:

```python
def render_comparison(baseline, current):
    """Render main and this run as two trees sharing one row order.

    Each side keeps v1's semantics exactly: AVG, %ROOT against its own root, and a
    bar scaled to its own root. Bars are therefore NOT comparable across the gutter
    — an unchanged node draws differently on each side whenever the roots differ.
    That is a deliberate trade for keeping each tree readable on its own; the AVG
    and Δ% columns are the comparison.
    """
    rows = compare_rows(baseline, current)
    if not rows:
        return None

    baseline_root = baseline.root_path
    current_root = current.root_path
    baseline_scale = baseline.average(baseline_root) if baseline_root else 0.0
    current_scale = current.average(current_root) if current_root else 0.0

    def side_label(report, path, connector):
        """v1's label, with coverage counted against this side's own trace count.

        The two sides are never reconciled into one figure: a marker on one side
        only is meaningful — it says the node is intermittent there and not here.
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
        # State drift explicitly rather than leaving the reader to spot blank rows.
        notes.append(", ".join(shape) + " — tree shape changed")

    if notes:
        lines.extend(["", " " + "   ".join(notes)])

    return "\n".join(lines)
```

- [ ] **Step 3: Render the fixture comparison**

```bash
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)

stem = 'CLI.ResourceGroupBenchmarks.CreateResourceGroup'
main = agg.aggregate_file('<tmp>/fixtures/main/RESOURCES/CLI/telemetry/' + stem + '.jsonl', stem, 'CLI', 'RESOURCES')
main.source = {'commit': 'abc1234def', 'generated': '2026-08-14T02:41:09Z'}
pr = agg.aggregate_file('<tmp>/fixtures/pr/RESOURCES/CLI/telemetry/' + stem + '.jsonl', stem, 'CLI', 'RESOURCES')
pr.source = {'commit': 'def5678abc'}
print(agg.render_comparison(main, pr))
" | tee <tmp>/comparison.txt
```

Expected, checked against the spec's sample:
- Header reads `main @ abc1234 · <n> days ago · 100 traces` on the left and `PR @ def5678 · 100 traces` on the right.
- Both roots show `100%` and a full bar.
- Every present node's `Δ%` reads `+20.0%` (the fixture scales durations by 1.2).
- The `AzureCLI.Startup.Services` row has content on the left and **nothing** on the right, with `n/a` in the gutter.
- Footer reads `1 node(s) only in main — tree shape changed`.

The right-hand heading reads `PR` with no commit here, because this step constructs the
reports in memory and only sets `source` by hand. Through the real CLI the reports are
reloaded from the sidecars just written, so the commit is present — Task 6 Step 3 verifies
that end to end.

- [ ] **Step 4: Verify alignment mechanically**

Blank-side padding is the easiest thing to get subtly wrong, and a one-character error is invisible by eye.

Note the regex: tree connectors also use `│` (U+2502), so a naive `line.index('│')` finds the connector inside a label rather than the gutter and reports a false misalignment. Match the delta cell — a pipe, a right-aligned percentage or `n/a`, a pipe — instead:

```bash
python3 -c "
import re
lines = [l.rstrip('\n') for l in open('<tmp>/comparison.txt', encoding='utf-8')]
pattern = re.compile(r'\u2502\s*(?:[-+][\d.]+%|n/a|\u0394%|\s+)\s*\u2502')
columns = {}
for line in lines:
    match = pattern.search(line)
    if match:
        columns.setdefault(match.start(), 0)
        columns[match.start()] += 1
print('distinct gutter columns:', columns)
assert len(columns) == 1, 'MISALIGNED'
print('max width:', max(len(l) for l in lines))
print('ALIGNED')
"
```

Expected: exactly one distinct column, covering every data row plus the two header rows; a max width around 167 for this fixture (real span names push it to roughly 225); and `ALIGNED`.

- [ ] **Step 5: Verify self-comparison is a no-op**

Comparing a report against itself must produce zero deltas and no blank rows. This exercises the union builder, the ordering, and both cells in one shot:

```bash
python3 -c "
import importlib.util, re
spec = importlib.util.spec_from_file_location('agg', '.github/scripts/aggregate-benchmark-traces.py')
agg = importlib.util.module_from_spec(spec); spec.loader.exec_module(agg)
stem = 'CLI.ResourceGroupBenchmarks.CreateResourceGroup'
r = agg.aggregate_file('<tmp>/fixtures/main/RESOURCES/CLI/telemetry/' + stem + '.jsonl', stem, 'CLI', 'RESOURCES')
out = agg.render_comparison(r, r)
deltas = re.findall(r'│\s*([-+]?[\d.]+%|n/a)\s*│', out)
print('deltas:', sorted(set(deltas)))
assert set(deltas) == {'+0.0%'}, 'self-comparison is not zero'
assert 'tree shape changed' not in out
print('SELF-COMPARE OK')
"
```

Expected: `deltas: ['+0.0%']` and `SELF-COMPARE OK`.

- [ ] **Step 6: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): render main and PR span trees side by side"
```

---

### Task 6: Emit comparisons into the job summary

**Files:**
- Modify: `.github/scripts/aggregate-benchmark-traces.py` (`render_summary` at line 326; `run` at line 379)

**Interfaces:**
- Consumes: `render_comparison` (Task 5), `render_report` (line 274), the `baselines` dict (Task 3)
- Produces: `render_summary(reports, baselines)` — **signature change**, second parameter added

- [ ] **Step 1: Change `render_summary` to take baselines**

Replace the signature and the body's per-report block. The current function (lines 326–351) becomes:

```python
def render_summary(reports, baselines=None):
    """Markdown for $GITHUB_STEP_SUMMARY, truncated to stay under the 1 MiB cap.

    A benchmark with a baseline renders side by side; one without falls back to the
    v1 single tree, so a new benchmark or a first-ever run still reports something.
    """
    baselines = baselines or {}
    blocks = ["## 🔬 Benchmark Trace Hierarchy", ""]
    # "\n".join inserts one separator byte per element beyond the first, so charge
    # each block for its own separator or the budget silently undercounts.
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
```

The byte budget is retained deliberately: a comparison block is roughly 2.5× the width of a v1 block, so the 1 MiB cap binds sooner than it did in v1, not later.

- [ ] **Step 2: Pass baselines from `run`**

In `run()`, change the existing call (line 399):

```python
    summary = render_summary(usable)
```

to:

```python
    summary = render_summary(usable, baselines)
```

- [ ] **Step 3: Verify the full pipeline end to end**

```bash
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/fixtures/pr \
  --baseline <tmp>/sidecars-main \
  --sidecar-out <tmp>/sidecars-pr \
  --commit def5678abc --ref refs/pull/42/merge --run-id 999
```

Expected — this exact output was produced by a working prototype of Tasks 1–6, so treat any deviation as a defect:

````
[traces] aggregated 1 benchmark(s) from 1 file(s)
[traces] wrote 1 sidecar(s) to <tmp>/sidecars-pr
[traces] matched 1/1 baseline(s) from <tmp>/sidecars-main
## 🔬 Benchmark Trace Hierarchy

```
 Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup
 RESOURCES · CLI

                     main @ abc1234 · today · 100 traces                      │         │                           PR @ def5678 · 100 traces
         AVG  %ROOT                                                           │    Δ%   │          AVG  %ROOT
   21.962 ms   100%  ████████████████████  CLI.Activity.Root                  │  +20.0% │    26.354 ms   100%  ████████████████████  CLI.Activity.Root
    4.435 ms    20%  ████                  ├─ AzureCLI.Startup                │  +20.0% │     5.322 ms    20%  ████                  ├─ AzureCLI.Startup
    0.050 ms     0%                        │  ├─ AzureCLI.Startup.Container   │  +20.0% │     0.060 ms     0%                        │  ├─ AzureCLI.Startup.Container
    0.487 ms     2%  ▌                     │  ├─ AzureCLI.Startup.Services    │     n/a │
    3.483 ms    16%  ███▏                  │  └─ AzureCLI.Startup.Telemetry   │  +20.0% │     4.180 ms    16%  ███▏                  │  └─ AzureCLI.Startup.Telemetry
    2.587 ms    12%  ██▍                   ├─ AzureCLI.ParseCommandInvocation │  +20.0% │     3.104 ms    12%  ██▍                   ├─ AzureCLI.ParseCommandInvocation
    1.027 ms     5%  ▉                     │  └─ SessionCreated               │  +20.0% │     1.232 ms     5%  ▉                     │  └─ SessionCreated
   10.725 ms    49%  █████████▊            └─ AzureCLI.ProcessCommand         │  +20.0% │    12.870 ms    49%  █████████▊            └─ AzureCLI.ProcessCommand
    9.640 ms    44%  ████████▊                └─ ProcessorProcessAsyncStart   │  +20.0% │    11.568 ms    44%  ████████▊                └─ ProcessorProcessAsyncStart

 1 node(s) only in main — tree shape changed
```
````

Three things to check specifically:
- The right heading reads **`PR @ def5678`**. A bare `PR` means the reload added in Task 2 Step 3 is missing, so the summary is being rendered from memory rather than from the sidecars — the asymmetric path this design explicitly rejects.
- `main @ abc1234 · today` shows `today` only because the baseline sidecar was written moments ago. In CI it reads `13 days ago` or similar.
- Every `%ROOT` matches on both sides here **only because** the fixture scales every duration uniformly. Real data will differ per side; that is expected, not a bug.

- [ ] **Step 4: Verify the no-baseline fallback still produces v1 output**

```bash
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/fixtures/pr > <tmp>/v1-final.txt
python3 -c "
import re
text = open('<tmp>/v1-final.txt', encoding='utf-8').read()
# Count delta cells, not bare pipes: v1 trees already contain U+2502 as a tree
# connector, so grep -c '│' would report a non-zero count on correct v1 output.
cells = re.findall(r'\u2502\s*(?:[-+][\d.]+%|n/a)\s*\u2502', text)
print('delta cells without --baseline:', len(cells))
assert not cells, 'comparison rendered without a baseline'
print('V1 FALLBACK OK')
"
```

Expected: `delta cells without --baseline: 0` and `V1 FALLBACK OK` — without `--baseline` no comparison is rendered, exactly as v1 behaved.

- [ ] **Step 5: Verify the PR comment body is untouched**

`render_comment` reads only `report.average(root)` and `report.trace_count`, both of which survive the round-trip, so the comment must be byte-identical to v1:

```bash
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/fixtures/pr \
  --comment-out <tmp>/comment-v1.md > /dev/null
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/fixtures/pr \
  --baseline <tmp>/sidecars-main --comment-out <tmp>/comment-v2.md > /dev/null
diff <tmp>/comment-v1.md <tmp>/comment-v2.md && echo "COMMENT UNCHANGED"
```

Expected: `COMMENT UNCHANGED`

- [ ] **Step 6: Commit**

```bash
git add .github/scripts/aggregate-benchmark-traces.py
git commit -m "feat(bench): emit side-by-side trace comparisons to the job summary"
```

---

### Task 7: Read baselines and publish sidecars in CI

**Files:**
- Modify: `.github/workflows/benchmark.yml` (`collect` job, lines 404–461)

**Interfaces:**
- Consumes: `--baseline`, `--sidecar-out`, `--commit`, `--ref`, `--run-id` (Tasks 2–3)
- Produces: artifact `benchmark-trace-sidecars` containing `<EXT>/<PRODUCT>/<stem>.json`, consumed by Task 8

- [ ] **Step 1: Add the gh-pages sparse checkout**

In the `collect` job, after the "Download all benchmark results" step (ends line 422), insert:

```yaml
      # Baselines are a public gh-pages read, so this works on fork PRs with the
      # default token and needs no added permissions. continue-on-error covers the
      # first ever run, before dev/bench/traces/ exists.
      - name: Checkout gh-pages for trace baselines
        id: baselines
        continue-on-error: true
        uses: actions/checkout@v4
        with:
          ref: gh-pages
          path: gh-pages
          sparse-checkout: |
            dev/bench/traces
```

- [ ] **Step 2: Pass the new flags to the aggregator**

Replace the "Aggregate trace hierarchies" step (lines 443–452) with:

```yaml
      - name: Aggregate trace hierarchies
        if: always()
        env:
          RUN_URL: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
        run: |
          python3 .github/scripts/aggregate-benchmark-traces.py \
            --input downloaded \
            --baseline gh-pages/dev/bench/traces \
            --sidecar-out sidecars \
            --commit "${{ github.sha }}" \
            --ref "${{ github.ref }}" \
            --run-id "${{ github.run_id }}" \
            --summary-out "$GITHUB_STEP_SUMMARY" \
            --comment-out "$RUNNER_TEMP/trace-comment.md" \
            --run-url "$RUN_URL"
```

`--baseline` is passed unconditionally. On a push to `main` the directory usually exists and a comparison against the previous main run is rendered in the summary, which is harmless and occasionally useful; the script handles a missing directory by falling back to v1 output.

- [ ] **Step 3: Upload the sidecars**

After the existing "Upload trace hierarchy comment" step (ends line 461), append:

```yaml
      - name: Upload trace sidecars
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: benchmark-trace-sidecars
          path: sidecars
          retention-days: 1
          if-no-files-found: ignore
```

Retention is 1 day: the artifact only has to survive until `publish-traces` reads it in the same run.

- [ ] **Step 4: Validate the workflow parses**

```bash
python3 -c "
import json, subprocess, sys
try:
    import yaml
except ImportError:
    sys.exit('PyYAML unavailable — validate with: gh workflow view \"Performance Benchmarks\"')
doc = yaml.safe_load(open('.github/workflows/benchmark.yml', encoding='utf-8'))
collect = doc['jobs']['collect']['steps']
names = [s.get('name', s.get('uses')) for s in collect]
print('\n'.join(f'  {n}' for n in names))
assert 'Checkout gh-pages for trace baselines' in names
assert 'Upload trace sidecars' in names
agg = next(s for s in collect if s.get('name') == 'Aggregate trace hierarchies')
for flag in ('--baseline', '--sidecar-out', '--commit', '--ref', '--run-id'):
    assert flag in agg['run'], flag
print('COLLECT JOB OK')
"
```

Expected: the step list, then `COLLECT JOB OK`. If PyYAML is unavailable, fall back to `gh workflow view "Performance Benchmarks"` after pushing, or `python3 -c "import yaml"`-free inspection by eye against the snippets above.

- [ ] **Step 5: Confirm permissions were not widened**

```bash
grep -n -A3 '^  collect:' .github/workflows/benchmark.yml
grep -n 'permissions' .github/workflows/benchmark.yml
```

Expected: the `collect` job still declares no `permissions:` block of its own, inheriting the workflow-level `contents: read`. Reading gh-pages is a public clone and writing happens only in Task 8's job.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/benchmark.yml
git commit -m "ci(bench): read trace baselines and upload sidecars in collect"
```

---

### Task 8: Publish sidecars to gh-pages

**Files:**
- Modify: `.github/workflows/benchmark.yml` (append a new job after `collect`)

**Interfaces:**
- Consumes: artifact `benchmark-trace-sidecars` (Task 7)
- Produces: `dev/bench/traces/<EXT>/<PRODUCT>/<stem>.json` on the `gh-pages` branch

- [ ] **Step 1: Add the job**

Append to the end of `.github/workflows/benchmark.yml`:

```yaml
  # ─────────────── Publish trace sidecars to gh-pages (main only) ───────────────
  # Separate from `collect` on purpose: `collect` only needs the run jobs, so it can
  # execute alongside publish-extensions / publish-startup, which push to gh-pages and
  # serialise among themselves. Pushing from `collect` would race them. Chaining here
  # joins that existing serialisation instead of competing with it.
  publish-traces:
    name: Publish trace sidecars to gh-pages
    needs: [collect, publish-extensions, publish-startup]
    if: always() && github.event_name == 'push'
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Harden runner
        uses: step-security/harden-runner@v2
        with:
          egress-policy: audit

      - name: Download trace sidecars
        id: download
        continue-on-error: true
        uses: actions/download-artifact@v4
        with:
          name: benchmark-trace-sidecars
          path: sidecars

      - name: Checkout gh-pages
        if: steps.download.outcome == 'success'
        uses: actions/checkout@v4
        with:
          ref: gh-pages
          path: gh-pages

      - name: Commit sidecars
        if: steps.download.outcome == 'success'
        env:
          SHA: ${{ github.sha }}
        run: |
          if [ -z "$(ls -A sidecars 2>/dev/null)" ]; then
            echo "No sidecars were produced; nothing to publish."
            exit 0
          fi

          mkdir -p gh-pages/dev/bench/traces
          # cp -r merges rather than replaces: extensions the path filter skipped keep
          # their existing baseline, so dev/bench/traces accumulates the union with
          # each subtree frozen at its own vintage.
          cp -r sidecars/. gh-pages/dev/bench/traces/

          cd gh-pages
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
          git add dev/bench/traces

          if git diff --cached --quiet; then
            echo "Sidecars unchanged; nothing to commit."
            exit 0
          fi

          git commit -m "bench: update trace sidecars for ${SHA}"

          # publish-extensions and publish-startup also push to gh-pages. They are
          # upstream in `needs`, but a retry keeps this green if one lands late.
          for attempt in 1 2 3; do
            if git push; then
              echo "Published trace sidecars on attempt ${attempt}."
              exit 0
            fi
            echo "Push rejected (attempt ${attempt}); rebasing."
            git pull --rebase --autostash || true
            sleep 5
          done

          # Sidecars are regenerable, so a lost push self-heals on the next main run.
          echo "::warning::Could not push trace sidecars after 3 attempts."
          exit 0
```

- [ ] **Step 2: Validate the job definition**

```bash
python3 -c "
import sys
try:
    import yaml
except ImportError:
    sys.exit('PyYAML unavailable — inspect the job by eye against the plan')
doc = yaml.safe_load(open('.github/workflows/benchmark.yml', encoding='utf-8'))
job = doc['jobs']['publish-traces']
print('needs:', job['needs'])
print('if:', job['if'])
print('permissions:', job['permissions'])
assert set(job['needs']) == {'collect', 'publish-extensions', 'publish-startup'}
assert job['permissions'] == {'contents': 'write'}
assert \"github.event_name == 'push'\" in job['if']
print('PUBLISH JOB OK')
"
```

Expected: the three fields printed, then `PUBLISH JOB OK`.

- [ ] **Step 3: Dry-run the merge semantics locally**

The `cp -r` merge is the mechanic that produces staggered vintages, so verify it rather than assuming:

```bash
rm -rf <tmp>/ghp && mkdir -p <tmp>/ghp/dev/bench/traces/COMPUTE/CLI
echo '{"schema":1,"benchmark":"old"}' > <tmp>/ghp/dev/bench/traces/COMPUTE/CLI/CLI.VirtualMachineBenchmarks.CreateVirtualMachine.json
cp -r <tmp>/sidecars-pr/. <tmp>/ghp/dev/bench/traces/
find <tmp>/ghp/dev/bench/traces -name '*.json' | sort
```

Expected: **both** files listed — the pre-existing `COMPUTE/CLI/...` untouched and the new `RESOURCES/CLI/...` added. A `COMPUTE` file that disappeared would mean `cp` replaced instead of merging, which would wipe baselines for every extension the path filter skipped.

- [ ] **Step 4: Verify the shell script is syntactically valid**

```bash
python3 -c "
import sys
try:
    import yaml
except ImportError:
    sys.exit(0)
doc = yaml.safe_load(open('.github/workflows/benchmark.yml', encoding='utf-8'))
step = next(s for s in doc['jobs']['publish-traces']['steps'] if s.get('name') == 'Commit sidecars')
open('<tmp>/commit-sidecars.sh', 'w').write(step['run'])
print('extracted')
"
bash -n <tmp>/commit-sidecars.sh && echo "SHELL SYNTAX OK"
```

Expected: `SHELL SYNTAX OK`

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/benchmark.yml
git commit -m "ci(bench): publish trace sidecars to gh-pages on main"
```

---

### Task 9: Document the sidecar and the local workflow

**Files:**
- Modify: `tests/Benchmark/README.md` (Output section, lines 54–62; Telemetry & Feedback Loop section, lines 163–176)

**Interfaces:**
- Consumes: everything from Tasks 1–8
- Produces: nothing consumed by later tasks

- [ ] **Step 1: Extend the Output list**

In the Output section, after the `telemetry/...` bullet (line 61), add:

```markdown
- `sidecars/<EXT>/<PRODUCT>/<Benchmark>.json` - aggregated span tree per benchmark, written only when `--sidecar-out` is passed (CI does this; a plain local run does not)
```

- [ ] **Step 2: Add a comparison section**

After the Telemetry & Feedback Loop section (ends line 176), insert:

````markdown
### Comparing traces against main

CI aggregates each benchmark's sampled spans into one averaged span tree and publishes
`main`'s trees to gh-pages under `dev/bench/traces/<EXT>/<PRODUCT>/<Benchmark>.json`. A
pull-request run reads them back and renders `main` and the PR as two trees side by side
in the workflow **job summary**, with a per-span `Δ%` between them. The PR comment is
unchanged — the comparison is summary-only, because a block runs to roughly 225 columns.

**Bars are not comparable across the gutter.** Each side's `%ROOT` and bar are scaled to
that side's own root, exactly as a standalone tree is. When the roots differ, an
unchanged span draws a different bar on each side. Compare using the `AVG` and `Δ%`
columns; read each bar as a within-tree proportion only.

A span present on only one side renders a blank row on the absent side and `n/a` in
`Δ%` — it was added, removed, or renamed rather than made slower, and any real cost
change still shows in its parent's `Δ%`.

Baselines refresh only from the main pushes the path filter selects, so each extension's
baseline sits at its own commit. The header states which: `main @ abc1234 · 13 days ago`.
A shared change (`src/Core`, `tests/Common`, the benchmark base classes) re-runs every
extension and realigns them all.

To compare two local runs without CI:

```bash
git switch main
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --extension Resources
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<timestamp> --sidecar-out /tmp/main-traces

git switch my-branch
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --extension Resources
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<timestamp> --baseline /tmp/main-traces
```

Local runs write to `artifacts/Benchmark/<timestamp>/telemetry/` and so carry no extension
segment; baseline lookup falls back to a recursive search for `<Benchmark>.json`, so the
flat `/tmp/main-traces` layout matches.

Omitting `--baseline` renders the single-tree view.
````

- [ ] **Step 3: Verify the documented commands are real**

Every flag named in the README must exist:

```bash
python3 .github/scripts/aggregate-benchmark-traces.py --help
```

Expected: `--sidecar-out`, `--baseline`, `--commit`, `--ref`, `--run-id` all present alongside v1's `--input`, `--summary-out`, `--comment-out`, `--run-url`, `--debug-paths`.

- [ ] **Step 4: Verify the gh-pages path in the docs matches the code**

```bash
grep -n 'dev/bench/traces' tests/Benchmark/README.md .github/workflows/benchmark.yml
```

Expected: the same `dev/bench/traces` path in the README, the `collect` job's `--baseline`, and `publish-traces`. A mismatch here silently produces zero baseline matches forever.

- [ ] **Step 5: Commit**

```bash
git add tests/Benchmark/README.md
git commit -m "docs(bench): document trace sidecars and baseline comparison"
```

---

## Final verification

Run after all tasks, from a clean tree.

- [ ] **Step 1: Full pipeline against fixtures**

```bash
rm -rf <tmp>/final && mkdir -p <tmp>/final
python3 <tmp>/make-fixture.py <tmp>/final/fixtures

python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/final/fixtures/main --sidecar-out <tmp>/final/baseline \
  --commit aaaa111 --ref refs/heads/main --run-id 1 > /dev/null

python3 .github/scripts/aggregate-benchmark-traces.py \
  --input <tmp>/final/fixtures/pr --baseline <tmp>/final/baseline \
  --sidecar-out <tmp>/final/pr --commit bbbb222 --ref refs/pull/1/merge --run-id 2
```

Expected: `matched 1/1 baseline(s)`, a comparison block with `+20.0%` on every shared node, a blank right side on `AzureCLI.Startup.Services`, and the `1 node(s) only in main — tree shape changed` footer.

- [ ] **Step 2: Self-comparison is exactly zero**

```bash
python3 -c "
import re, subprocess, sys
out = subprocess.run([sys.executable, '.github/scripts/aggregate-benchmark-traces.py',
                      '--input', '<tmp>/final/fixtures/pr', '--baseline', '<tmp>/final/pr'],
                     capture_output=True, text=True, encoding='utf-8').stdout
deltas = re.findall(r'\u2502\s*([-+][\d.]+%|n/a)\s*\u2502', out)
print('deltas:', sorted(set(deltas)))
assert set(deltas) == {'+0.0%'}, 'self-comparison is not zero'
assert 'tree shape changed' not in out
print('SELF-COMPARE OK')
"
```

Expected: `deltas: ['+0.0%']` and `SELF-COMPARE OK`. Any other value means the writer, the reader, the union builder, or the renderer disagrees with itself.

- [ ] **Step 3: v1 behaviour is intact**

```bash
python3 -c "
import re, subprocess, sys
out = subprocess.run([sys.executable, '.github/scripts/aggregate-benchmark-traces.py',
                      '--input', '<tmp>/final/fixtures/pr'],
                     capture_output=True, text=True, encoding='utf-8').stdout
cells = re.findall(r'\u2502\s*(?:[-+][\d.]+%|n/a)\s*\u2502', out)
assert not cells, 'comparison rendered without a baseline'
assert 'Benchmark.CLI.ResourceGroupBenchmarks' in out, 'v1 tree missing'
print('V1 OK')
"
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/final/fixtures/pr --debug-paths | head -5
python3 .github/scripts/aggregate-benchmark-traces.py --input <tmp>/does-not-exist; echo "exit=$?"
```

Expected: `V1 OK`; `--debug-paths` still dumps path keys; the missing-input run prints `[traces] no telemetry .jsonl found under ...` and `exit=0`.

- [ ] **Step 4: Nothing outside the three intended files changed**

```bash
git diff --stat origin/main...HEAD -- . ':!docs/superpowers'
```

Expected: exactly `.github/scripts/aggregate-benchmark-traces.py`, `.github/workflows/benchmark.yml`, and `tests/Benchmark/README.md`. Anything under `src/` or `tests/Benchmark/*.cs` is a mistake — the telemetry producer needs no changes.

- [ ] **Step 5: Remove the fixture scratch space**

```bash
rm -rf <tmp>/fixtures <tmp>/final <tmp>/sidecars-main <tmp>/sidecars-pr \
       <tmp>/flat <tmp>/corrupt <tmp>/ghp <tmp>/throwaway \
       <tmp>/make-fixture.py <tmp>/commit-sidecars.sh \
       <tmp>/comparison.txt <tmp>/v1-*.txt <tmp>/comment-v*.md
git status --porcelain
```

Expected: clean, with no fixture or scratch files anywhere in the repository.

## What manual validation cannot catch

The spec declines automated tests, so this is the residual risk, recorded rather than hidden.

The fixture is synthetic and uniform: every trace has the same shape, and durations scale by
a constant. Real telemetry does not. These stay unverified until the first live CI run:

- **Truncated exports.** `BatchActivityExportProcessor` drains on shutdown in
  `[IterationCleanup]`, so real traces lose in-flight spans and produce partial coverage
  `(n/N)` markers and orphan subtrees. The fixture produces neither.
- **Repeated same-named siblings.** `SessionCreated`, `AuthBeforeTokenAcquisition`, and
  `ActionStart` fire from event handlers and can occur more than once under one parent,
  exercising the `[1]`, `[2]` index suffixes and their interaction with the union.
- **Genuinely divergent trees**, where the PR reorders spans rather than only adding or
  removing one.
- **The 1 MiB truncation path**, which needs far more benchmarks than a fixture provides.

After merging, inspect the first real PR run's job summary against these four before
trusting the numbers.
