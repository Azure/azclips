# Benchmark Trace Baseline Comparison

**Date:** 2026-08-27
**Status:** Approved, not yet implemented
**Supersedes the non-goals of:** [2026-08-26 Benchmark Trace Hierarchy Aggregation](2026-08-26-benchmark-trace-aggregation-design.md)

## Problem

v1 aggregates each benchmark's sampled spans into one averaged tree and renders it. That
answers "where does the time go" but not "what changed", which is the question a developer
actually has when a benchmark slows down. Today they read a v1 tree, then guess whether
`AzureCLI.ProcessCommand` at 10.7 ms is normal or is the regression.

The missing half is a baseline. Nothing persists a tree for `main`, so there is nothing to
compare a pull request against at span granularity. BenchmarkDotNet's method-level mean is
compared against `main` (via `github-action-benchmark` and gh-pages), but that comparison
stops at one number per benchmark.

v1 deliberately deferred both halves of the fix, listing them as non-goals:

> **Baseline comparison.** No history, no delta-versus-main column.
>
> **Machine-readable output.** ASCII only. A JSON sidecar was considered as groundwork for a
> future delta feature and rejected as speculative.

They are no longer speculative. This is v2.

## Goal

Persist each benchmark's aggregated span tree as a JSON sidecar, publish `main`'s sidecars to
gh-pages, and render `main` and the pull request as two trees side by side in the workflow job
summary, with a per-span percentage difference between them.

## Non-goals

- **PR comment changes.** `benchmark-comment.yml` is untouched. The existing trace comment
  keeps posting v1's index table and link. The side-by-side view lives only in the job summary,
  where wide output is acceptable. Rendered against real span names — `ActionStart.<Class>.<Method>`
  and `MsalAuthService.AcquireTokenSilentlyAsync` are among the longest — a comparison block
  runs to roughly 225 columns.
- **Regression gating, thresholds, or alert markers.** The `Δ%` column prints a number and
  nothing else. See [Why no threshold](#why-no-threshold).
- **Scheduled or full-matrix baseline refresh.** Baselines refresh only from the main pushes
  the existing path filter already selects. See [Baseline freshness](#baseline-freshness).
- **Automated tests.** Carried over from v1. See [Testing](#testing) for the risk this carries.
- **Startup benchmarks.** Unchanged from v1: the startup family launches separate processes and
  emits no telemetry, so it has nothing to aggregate and nothing to compare.

## What the output looks like

```
 Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup
 RESOURCES · CLI

                  main @ abc1234 · 13 days ago · 100 traces                   │         │                           PR @ def5678 · 100 traces
         AVG  %ROOT                                                           │    Δ%   │          AVG  %ROOT
   21.962 ms   100%  ####################  CLI.Activity.Root                  │   +9.8% │    24.108 ms   100%  ####################  CLI.Activity.Root
    4.435 ms    20%  ####                  ├─ AzureCLI.Startup                │   -0.7% │     4.402 ms    18%  ###                   ├─ AzureCLI.Startup
    0.050 ms     0%                        │  ├─ AzureCLI.Startup.Container   │   +2.0% │     0.051 ms     0%                        │  ├─ AzureCLI.Startup.Container
    0.487 ms     2%                        │  ├─ AzureCLI.Startup.Services    │     n/a │
                                                                              │     n/a │     0.402 ms     2%                        │  ├─ AzureCLI.Startup.Registry
    3.483 ms    16%  ###                   │  └─ AzureCLI.Startup.Telemetry   │   -0.4% │     3.470 ms    14%  ##                    │  └─ AzureCLI.Startup.Telemetry
    0.009 ms     0%                        ├─ AzureCLI.ParseGlobalArgs        │   +0.0% │     0.009 ms     0%                        ├─ AzureCLI.ParseGlobalArgs
    0.323 ms     1%                        ├─ AzureCLI.HandleGlobalArgs       │   +0.6% │     0.325 ms     1%                        ├─ AzureCLI.HandleGlobalArgs
    2.587 ms    12%  ##                    ├─ AzureCLI.ParseCommandInvocation │   +0.5% │     2.601 ms    11%  ##                    ├─ AzureCLI.ParseCommandInvocation
    1.027 ms     5%  #                     │  └─ SessionCreated               │   +0.4% │     1.031 ms     4%                        │  └─ SessionCreated
   10.725 ms    49%  #########             └─ AzureCLI.ProcessCommand         │  +20.3% │    12.900 ms    54%  ##########            └─ AzureCLI.ProcessCommand
```

Each side is exactly v1's tree, rendered unchanged. The gutter carries `Δ%`. Row `n` refers to
the same node on both sides, which is a guarantee the renderer must provide rather than a
coincidence — see [Union tree construction](#union-tree-construction).

Reading the example: `AzureCLI.ProcessCommand` is `+20.3%` and everything else is under 2%, so
the regression is localised to one span.

### Bars are not comparable across the gutter

**This is a deliberate, known limitation.** Each side's `%ROOT` and bar are computed against
*that side's own root*, exactly as v1 does. When the roots differ, an unchanged span renders a
different bar on each side.

`AzureCLI.Startup` in the example above is the case: 4.435 ms versus 4.402 ms — unchanged, as
`Δ%` of `-0.7%` confirms — but it renders `####` at 20% on the left and `###` at 18% on the
right, purely because the PR's root grew from 21.962 ms to 24.108 ms underneath it. The root
row has the mirror-image problem: both sides read 100% and draw a full bar, so the +9.8% that
actually happened is invisible in the bars.

Scaling both sides by a shared denominator (`max` of the two roots) was considered and
rejected: it would make bars cross-comparable, but each tree would stop reading as "share of
its own total", diverging from v1 and from how a single tree is read everywhere else.

**Consequence for readers: compare using the `AVG` and `Δ%` columns. Treat each side's bar as
a within-tree proportion only.** This is documented in the README rather than being silently
surprising.

### The bar is ASCII, one cell per whole 1/BAR_WIDTH share

`#` characters, one per completed `1/BAR_WIDTH` of the root — at the default `BAR_WIDTH` of 20
that is 5% per cell, **truncated** rather than rounded, so 24% draws four cells and 26% draws
five. A span under 5% of its root draws no bar and is read from its `%ROOT` number instead.

**The cell count is derived from the printed percentage, not from the raw ratio.** Deriving
them independently let them contradict each other: a span measured at 14.83% printed as `15%`
but drew only two cells (`14.83 × 20 ÷ 100` truncates to 2), and — worse — two spans both
printed as `5%` drew one cell and none, because one was really 5.05% and the other 4.81%.
`bar()` now rounds first, exactly as the `%ROOT` column does, so `cells == printed% × BAR_WIDTH
÷ 100` holds for every possible ratio and the bar can always be read off the printed number.

Block elements (`█`, `▏`…`▉`) were used originally and gave eight times the resolution, but
they are Unicode East Asian Width **Ambiguous**: a browser that font-falls-back on them renders
them at a width that is not the monospace cell. A row's block count varies from 0 to
`BAR_WIDTH`, so that per-glyph error is multiplied by the difference between two rows and every
column after the bar drifts — which made the two trees visibly stop lining up in the job
summary. `#` is unambiguous in every font, so alignment becomes a property of the format rather
than of whichever font the reader's browser happens to load.

The tree connectors (`│ ├ └ ─`) are also Ambiguous and also vary per row, so they remain a
smaller residual source of the same drift. They were kept because the ASCII substitutes (`|`,
`+-`, `` `- ``) are markedly harder to read at this density. If drift is still visible in
practice, converting them is the next step.

### One-sided nodes

A node present on only one side renders a completely blank row on the absent side, and `n/a`
in `Δ%`. Nothing is inferred and no placeholder value is printed, because "absent" and "zero"
are different facts and a `0.000 ms` would read as a measurement.

`Δ%` is `n/a` rather than `±100%` because a one-sided node has not regressed — it has been
added, removed, or renamed. A rename appears as an adjacent pair of one-sided rows, which
reads correctly as "moved". Any cost genuinely added or removed still shows up honestly in the
parent's `Δ%`.

Causes of one-sided nodes, in rough order of frequency: truncated exports (v1's known
`BatchActivityExportProcessor` shutdown drop), the real MSAL cache-hit-versus-refresh branch,
and genuine instrumentation changes in the pull request.

### Header and footer

Each side's title line is built from that side's own sidecar, so the two vintages are always
visible and never conflated:

| Element | Source |
| --- | --- |
| `main @ abc1234` | baseline sidecar `source.commit`, abbreviated to 7 characters |
| `· 13 days ago` | baseline sidecar `source.generated`, rendered relative to render time |
| `PR @ def5678` | the run's own sidecar `source.commit`, populated from `--commit` |
| `· 100 traces` | that side's `traces` |

The pull request side carries no age because it is this run. When no baseline exists, v1's
single-tree header is used unchanged.

v1's diagnostic footer notes gain a side prefix, and a shape-change line is appended when the
trees differ:

```
 main: 3 orphan subtree(s) dropped   PR: 1 unreadable line(s) skipped
 1 node only in main, 1 node only in PR
```

The shape-change line states drift explicitly rather than leaving the reader to notice blank
rows, and is omitted entirely when both trees have the same node set.

### Why no threshold

The baseline is measured on a different workflow run, on a different day, on a self-hosted
runner in a different state. Node-level averages are already noisier than BenchmarkDotNet's
method-level means — the reason v1 rejected baseline comparison outright — and cross-run
sourcing adds machine variance on top.

A threshold on that signal would fire on noise. Printing the number and leaving judgement to
the reader is the honest option, and matches the stated intent: find *potential* regressions to
investigate, not gate merges.

## Architecture

```
benchmark.yml : collect job
  ├─ actions/checkout                              (existing, from v1)
  ├─ download-artifact  merge-multiple             (existing)
  ├─ actions/checkout ref: gh-pages                (new — sparse: dev/bench/traces/)
  ├─ aggregate-benchmark-traces.py                 (extended)
  │     --sidecar-out sidecars/                       → this run's trees, as JSON
  │     --baseline    gh-pages/dev/bench/traces/      → main's trees, when present
  │     └─→ $GITHUB_STEP_SUMMARY                      side-by-side trees
  ├─ upload-artifact "benchmark-traces"            (existing — comment body, unchanged)
  └─ upload-artifact "benchmark-trace-sidecars"    (new)

benchmark.yml : publish-traces job                 (new, push-to-main only)
  ├─ download-artifact "benchmark-trace-sidecars"
  ├─ actions/checkout ref: gh-pages
  └─ cp -r → commit → push   dev/bench/traces/

benchmark-comment.yml                              (unchanged)
```

On a `pull_request` event, `actions/checkout@v4` defaults its `repository` input to `github.repository`, which is the base repository rather than the fork. Combined with the `contents: read` scope already granted to `collect`, this makes the base repo's `gh-pages` branch readable with the default token — so fork pull requests work without any added permissions.

### Why `publish-traces` is a separate job

`collect` only `needs` the run jobs, so it can execute concurrently with `publish-extensions`
and `publish-startup`. Those two push to gh-pages and serialise among themselves via
`max-parallel: 1` plus a `needs` edge. If `collect` also pushed, it would race them.

`publish-traces` declares `needs: [collect, publish-extensions, publish-startup]`, joining the
existing serialisation instead of competing with it, and is the only new job that requires
`contents: write`.

## Component: the sidecar

One file per benchmark method, self-describing so an orphaned file is still interpretable.

```
gh-pages: dev/bench/traces/<EXTENSION>/<PRODUCT>/<benchmark-stem>.json
          dev/bench/traces/RESOURCES/CLI/CLI.ResourceGroupBenchmarks.CreateResourceGroup.json
```

`<EXTENSION>` is upper-case because `Program.cs` upper-cases `--extension`, so the CI artifact
path is already `artifacts/Benchmark/ci/RESOURCES/CLI/`. `<benchmark-stem>` is the telemetry
file stem, which `BenchmarkName` has already stripped of its `Benchmark.` prefix. Stems contain
only dots and alphanumerics, so they are safe as filenames unescaped.

```json
{
  "schema": 1,
  "benchmark": "CLI.ResourceGroupBenchmarks.CreateResourceGroup",
  "extension": "RESOURCES",
  "product": "CLI",
  "source": {
    "commit": "abc1234def5678...",
    "ref": "refs/heads/main",
    "run_id": "17384920011",
    "generated": "2026-08-14T02:41:09Z"
  },
  "traces": 100,
  "diagnostics": { "malformed": 0, "orphan_subtrees": 3, "rootless_traces": 0 },
  "nodes": [
    { "path": [["CLI.Activity.Root", 0]],
      "avg_ms": 21.962, "occurrences": 100, "offset_ms": 0.000 },
    { "path": [["CLI.Activity.Root", 0], ["AzureCLI.Startup", 0]],
      "avg_ms": 4.435, "occurrences": 100, "offset_ms": 0.203 }
  ]
}
```

This is a direct serialisation of v1's in-memory `Report`, so it introduces no new aggregation
logic — only a writer and a reader. Field notes:

| Field | Why it is there |
| --- | --- |
| `schema` | An older sidecar must be rejected cleanly, not half-read. Unknown value ⇒ treat as missing |
| `path` | v1's `(OperationName, occurrenceIndex)` key. Already identifies a node across traces; reused to identify it across branches |
| `avg_ms` | The number rendered. Human-readable when the file is opened directly |
| `occurrences` | With `traces`, carries v1's partial-coverage `(n/N)` marker to the baseline side |
| `offset_ms` | **Required.** v1 orders siblings by mean start offset. Without it the baseline tree cannot be ordered, and the two sides would draw children in different orders — breaking row correspondence |
| `source` | Makes staggered baseline vintages visible in the header rather than silent |
| `diagnostics` | v1's footer notes, per side |

`Report.to_dict()` emits `avg_ms = total/count` and `offset_ms = offset_total/count`;
`from_dict()` reverses it (`total = avg_ms × occurrences`). `Report.average()` and
`.mean_offset()` are unchanged, and so is `render_report()` for the no-baseline case.

### Rendering is symmetric

The pull request's sidecar is written first, and **both** sides are then loaded back through
`from_dict()` and rendered from that reconstruction. The renderer never sees an in-memory
`Report` on one side and a deserialised one on the other.

Three reasons, in order of importance:

1. **One render path.** No class of bug that works on the PR side and is subtly wrong on the
   baseline side.
2. **The format is exercised on every run**, not only on main pushes. A field that failed to
   round-trip would otherwise stay latent until a pull request happened to read it.
3. **What is rendered is what is published.** The comparison is derived from the same bytes
   that land on gh-pages, so the tree cannot disagree with the file.

The cost is a round-trip through small JSON files, which is negligible.

## Component: union tree construction

Two independently rendered trees only line up if both have identical shape. The moment one
side gains or loses a node, every row below it shifts and the side-by-side view becomes
actively misleading — it would place `Startup.Services` opposite `Startup.Registry` and imply
one became the other.

The renderer therefore builds a single tree over the **union** of both sides' paths:

1. Node set is the union of `main.nodes` and `pr.nodes`, keyed by path.
2. Siblings are ordered by `offset_ms`, taking the **pull request's** value when the node
   exists there and main's otherwise. Ties break on the path itself.
3. That ordering is computed once and applied to both sides. This is what makes "row `n` is
   the same node on the left and the right" a guarantee.
4. Connectors are drawn over the union, so the spine stays continuous through a blank row.
5. Each side renders its own `AVG`, its own `%ROOT` against its own root, and its own bar — or
   a blank field of exactly the side width.
6. Each side carries v1's partial-coverage marker **independently**, against its own trace
   count: a node seen in 43 of main's 100 traces and 51 of the PR's 100 renders
   `SessionCreated[1]  (43/100)` on the left and `SessionCreated[1]  (51/100)` on the right.
   A marker on one side only is meaningful — it says the node is intermittent there and not
   here — so the two are never reconciled into a single figure.
7. `Δ%` = `(pr − main) / main × 100`, or `n/a`.

## Component: CI wiring

### `benchmark.yml`, `collect` job

| Step | Notes |
| --- | --- |
| `actions/checkout` `ref: gh-pages` | `sparse-checkout: dev/bench/traces`, `continue-on-error`. Same pattern `benchmark-comment.yml` already uses for `data.js` |
| Aggregate trace hierarchies | Gains `--sidecar-out` and `--baseline` |
| `upload-artifact` `benchmark-trace-sidecars` | Retention 1 day; consumed only by `publish-traces` |

Permissions unchanged: `contents: read`.

### `benchmark.yml`, new `publish-traces` job

```yaml
  publish-traces:
    name: Publish trace sidecars to gh-pages
    needs: [collect, publish-extensions, publish-startup]
    if: always() && github.event_name == 'push'
    permissions:
      contents: write
```

Downloads the sidecar artifact, checks out gh-pages, `cp -r sidecars/. dev/bench/traces/`,
commits, pushes. `cp -r` merges rather than replaces, which produces the accumulate-union
behaviour described below.

### Baseline freshness

Sidecars refresh only from the main pushes the existing path filter selects, and
`publish-traces` writes only the files the run produced — it never deletes or rewrites
sidecars for extensions that did not run. gh-pages therefore accumulates the union, each
subtree frozen at its own vintage.

| Main push touches | `detect-benchmark-changes.sh` | gh-pages effect |
| --- | --- | --- |
| `src/Extensions/Resources/**` | `matrix={"extension":["resources"]}` | `traces/RESOURCES/**` rewritten; `COMPUTE/**` untouched |
| `src/Extensions/Compute/**` | `matrix={"extension":["compute"]}` | `traces/COMPUTE/**` rewritten; `RESOURCES/**` untouched |
| `src/Core/**` | `shared: true` ⇒ `RUN=("${UNIVERSE[@]}")` | **every** subtree rewritten in one commit |

Staggered vintages are acceptable for the same reason the path filter is: if an extension's
code has not moved on main, its last measurement *is* current main behaviour for it. Anything
capable of moving every extension's numbers is classified shared and realigns every baseline
at once.

A pull request only reads baselines for extensions it actually ran, so there is never a
dangling comparison:

| PR touches | PR runs | Baseline read |
| --- | --- | --- |
| `src/Extensions/Resources/**` | Resources | `traces/RESOURCES/**` |
| `src/Extensions/Compute/**` | Compute | `traces/COMPUTE/**` |
| `src/Core/**` | both | both |

**The residual risk is machine drift, not commit distance.** A sidecar measured months ago on
a differently-configured runner contributes runner variance to every `Δ%` derived from it, and
for a quiet extension that age is unbounded. This was an explicit call: a scheduled
full-matrix main run would bound the age *and* yield an empirical per-span noise floor by
comparing consecutive main runs, and was declined in favour of adding no CI cost. The
mitigation is disclosure — the header prints `main @ <sha> · <age>` so the reader can weigh
the number — not elimination.

### Behaviour by trigger

| Trigger | Reads baseline | Job summary | Writes gh-pages | PR comment |
| --- | --- | --- | --- | --- |
| Push to `main` | no | v1 single tree | **yes** | no PR exists |
| Same-repo PR | yes | side-by-side | no | unchanged (v1 index table) |
| Fork PR | yes | side-by-side | no | unchanged (v1 index table) |

## Local use

`--baseline` accepts any directory of sidecars, so two local runs can be diffed with no CI:

```bash
git switch main && dotnet run --project tests/Benchmark/Benchmark.csproj -- --local
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<ts1> --sidecar-out /tmp/main-traces

git switch my-branch && dotnet run --project tests/Benchmark/Benchmark.csproj -- --local
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<ts2> --baseline /tmp/main-traces
```

Baseline lookup is by `<EXTENSION>/<PRODUCT>/<stem>.json`. Local runs write to
`artifacts/Benchmark/<timestamp>/telemetry/` and so have no extension segment; when the
extension is unknown, lookup falls back to a recursive search for `<stem>.json`.

## Interface

```
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input       <dir>    # root to search recursively (required)
  --sidecar-out <dir>    # write this run's trees as JSON; omit to skip
  --baseline    <dir>    # compare against sidecars here; omit for v1 single-tree output
  --commit      <sha>    # recorded in sidecar source.commit
  --ref         <ref>    # recorded in sidecar source.ref
  --run-id      <id>     # recorded in sidecar source.run_id
  --summary-out <path>   # append the report here; omit for stdout
  --comment-out <path>   # PR comment body (unchanged from v1)
  --run-url     <url>    # link target embedded in the comment
  --debug-paths          # dump raw path keys with averages instead of rendering
```

Omitting `--baseline` reproduces v1's behaviour exactly, which keeps push-to-main runs and
ad-hoc local invocations working unchanged.

## Error handling

Unchanged posture: telemetry must never fail a benchmark run. `main()` stays wrapped, failures
print `::warning::` and exit `0`.

| Failure | Handling |
| --- | --- |
| gh-pages checkout fails, or `dev/bench/traces/` absent | No baseline ⇒ every benchmark renders v1's single tree |
| Sidecar missing for one benchmark | That benchmark renders v1's single tree; others still compare |
| Sidecar malformed, or unknown `schema` | Warn, treat as missing. Never guess at an unrecognised shape |
| Baseline node `avg_ms == 0` | `Δ%` undefined ⇒ `n/a`. Follows v1's precedent of suppressing `%ROOT` when the root average is zero |
| Sidecar write fails | Warn; the summary still renders and `publish-traces` finds nothing to commit |
| `publish-traces` push rejected | `pull --rebase` and retry up to 3×; then warn and exit `0`. Sidecars are regenerable, so a lost push self-heals on the next main run |
| Job summary exceeds 1 MiB | v1's byte budget already truncates with an explicit note. A comparison block is roughly 2.5× the width of a v1 block (~225 columns against ~90), so the cap binds sooner — the budget must be retained, not removed |

## Testing

**Decision: no automated tests, carried over from v1.** This was an explicit call, made after
stdlib `unittest` was offered as a zero-dependency option and declined.

The risk is larger than it was in v1 and worth stating plainly. v2 adds union construction,
a shared sibling ordering across two data sets, round-trip serialisation, blank-row width
alignment, and `Δ%` arithmetic. Every one of those fails silently rather than loudly: the
output is a plausible-looking tree with wrong numbers, which can send someone chasing a span
that never moved. v1's spec already named this as "the obvious first follow-up"; it remains
outstanding and now covers more surface.

Validation is manual:

1. Run against a real downloaded CI artifact with `--baseline` pointed at a gh-pages checkout;
   confirm node names, nesting, and durations against a known Application Insights view.
2. Run against a local `artifacts/Benchmark/<timestamp>` directory to confirm recursive
   discovery and the extension-less baseline fallback both work.
3. **Self-comparison:** run with `--baseline` pointed at the run's *own* `--sidecar-out`
   directory. Every `Δ%` must read `+0.0%`, both trees must be identical, and no row may be
   blank. This exercises the writer, the reader, the union builder, and the renderer in one
   command and is the cheapest end-to-end check available.
4. Delete one sidecar and re-run; confirm that benchmark falls back to a v1 single tree while
   the others still compare.
5. Confirm `--baseline` omitted reproduces v1 output byte-for-byte.

## Files touched

| File | Change |
| --- | --- |
| `.github/scripts/aggregate-benchmark-traces.py` | `--sidecar-out` / `--baseline` / provenance flags; `Report.to_dict` + `from_dict`; union renderer |
| `.github/workflows/benchmark.yml` | gh-pages sparse checkout and new flags in `collect`; new `publish-traces` job; new sidecar artifact |
| `tests/Benchmark/README.md` | Document the sidecar, the local `--baseline` workflow, and the bar-comparability caveat |

`benchmark-comment.yml` is untouched. Nothing under `src/` or `tests/Benchmark/*.cs` changes —
the telemetry producer already emits everything required.

## Future work

- Bound baseline age with a scheduled full-matrix main run, which would additionally yield an
  empirical per-span noise floor by comparing consecutive main runs.
- `unittest` coverage for the merge, union, and round-trip logic.
- A noise-aware `Δ%` presentation once a noise floor exists, replacing raw percentages with
  something that distinguishes signal from runner variance.
