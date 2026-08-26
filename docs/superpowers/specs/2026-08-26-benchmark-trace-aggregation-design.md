# Benchmark Trace Hierarchy Aggregation

**Date:** 2026-08-26
**Status:** Approved, not yet implemented

## Problem

Benchmarks already emit sampled OpenTelemetry spans, but nothing reads them. A developer
who sees a benchmark slow down has a single number — BenchmarkDotNet's mean — and no way
to tell which part of the call tree moved.

The spans hold that answer. Each sampled trace is a full parent/child span tree for one CLI
or PowerShell invocation, with per-span durations. Aggregating those trees into one averaged
tree and rendering it turns "this benchmark got 20% slower" into "`AzureCLI.ProcessCommand`
got 20% slower and everything else held steady".

## Goal

Aggregate each benchmark method's sampled traces into a single averaged span tree, render it
as ASCII, publish it to the workflow run's job summary, and link it from a PR comment. Works
on fork PRs.

## Non-goals

These are deliberately excluded from v1.

- **Baseline comparison.** No history, no delta-versus-main column. Node-level averages are
  far noisier than BDN's method-level means, so a naive regression threshold would cry wolf.
  The tree shows where time goes; the developer supplies the "compared to what".
- **Machine-readable output.** ASCII only. A JSON sidecar was considered as groundwork for a
  future delta feature and rejected as speculative.
- **Startup benchmarks.** Only `CLIBenchmarkBase` and `PSBenchmarkBase` call
  `InitializeTelemetryCommon`. The startup family launches separate processes and emits no
  telemetry, so it has nothing to aggregate.
- **Unit tests.** See [Testing](#testing) for the rationale and the risk this carries.

## Existing telemetry pipeline

The producing side already works and needs no changes. Understanding its two hops matters,
because the staging directory is easy to mistake for the artifact location.

1. **Staging.** BenchmarkDotNet runs one child process per benchmark case. Each child prunes
   its measured-iteration traces to at most 100 evenly-spread samples
   (`TraceSampler.TargetSampleCount`) and writes them to
   `artifacts/BenchmarkTelemetry/<benchmark>.jsonl`. A child process cannot know the run's
   `artifactsPath`, which is why this stable staging path exists at all.

2. **Collection.** After every case finishes, `Program.cs` calls `CollectTelemetry`, which
   moves each `.jsonl` into `<artifactsPath>/telemetry/` and deletes the staging root. By the
   end of a run `artifacts/BenchmarkTelemetry` no longer exists.

`artifactsPath` differs by mode, and both layouts must be supported:

| Mode | `artifactsPath` | Telemetry lands in |
| --- | --- | --- |
| CI (`--ci`) | `artifacts/Benchmark/ci/<EXT>/<PRODUCT>` | `artifacts/Benchmark/ci/<EXT>/<PRODUCT>/telemetry/` |
| Local | `artifacts/Benchmark/<timestamp>` | `artifacts/Benchmark/<timestamp>/telemetry/` |

`benchmark.yml` uploads `artifacts/Benchmark/ci`, so telemetry is already inside the
`benchmark-cli-results-*`, `benchmark-ps-results-*`, and `benchmark-startup-results`
artifacts. The `collect` job downloads all of them with `merge-multiple: true`, producing
`downloaded/<EXT>/<PRODUCT>/telemetry/*.jsonl`. No workflow change is needed to *get* the
data — only to consume it.

### Span record shape

One JSON object per line, serialized from `Core.Telemetry.TraceEntry`:

| Field | Notes |
| --- | --- |
| `TraceId` | Groups spans into one invocation |
| `SpanId` / `ParentSpanId` | Builds the tree |
| `OperationName` | Node label |
| `StartTime` | ISO-8601 round-trip (`"O"`), 100ns resolution |
| `Duration` | **Milliseconds**, as a double (`activity.Duration.TotalMilliseconds`) |
| `Status`, `Kind`, `Tags`, `Events`, `Resource` | Unused by v1 |

## Architecture

```
benchmark.yml : collect job
  ├─ actions/checkout                      (new — job currently has none)
  ├─ download-artifact  merge-multiple     (existing)
  ├─ aggregate-benchmark-traces.py         (new)
  │     ├─→ $GITHUB_STEP_SUMMARY           ASCII trees
  │     └─→ trace-comment.md               comment body
  └─ upload-artifact "benchmark-traces"    (new)

benchmark-comment.yml : trace-comment job  (new job, workflow_run trigger)
  ├─ download-artifact "benchmark-traces" by run-id
  └─ post/update PR comment by marker
```

Aggregation happens exactly once, in the job that already holds the data. Posting is split
out because a fork PR's `GITHUB_TOKEN` is read-only — only the `workflow_run` workflow,
which executes in the base repository, can write a comment for a fork.

## Component: the aggregator script

`.github/scripts/aggregate-benchmark-traces.py`. Python 3, standard library only. Chosen to
match the existing CI scripting (`detect-benchmark-changes.sh`, the Python heredoc in
`benchmark-comment.yml`) and to avoid adding a build step to `collect`.

### Interface

```
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input   <dir>    # root to search recursively (required)
  --summary-out <path>   # ASCII report; omit to print to stdout
  --comment-out <path>   # PR comment body; omit to skip
  --run-url <url>        # link target embedded in the comment
```

Running with only `--input` prints the report to stdout. This is the primary validation
mechanism (see [Testing](#testing)) and must work against both a downloaded CI artifact and
a local `artifacts/Benchmark/<timestamp>` directory.

### Discovery

Recursively find every `telemetry/*.jsonl` beneath `--input`, at any depth. A fixed
`*/*/telemetry/*.jsonl` glob would match the CI layout and silently find nothing locally,
breaking the validation workflow.

- **Benchmark name** — the file stem. `BenchmarkName` strips the `Benchmark.` namespace
  prefix, so the stem reads `CLI.ResourceGroupBenchmarks.ListResourceGroups`.
- **Product** — the stem's first segment (`CLI` or `PS`). Derived from the file itself, so it
  is correct in both layouts.
- **Extension** — the path segment two levels above `telemetry/`, when the CI layout is
  present. Omitted from the heading otherwise.

### Aggregation algorithm

Per file:

1. Parse one JSON object per line. Skip blank lines. Count unparseable lines and report the
   total; do not abort.
2. Group spans by `TraceId`. Each group is one invocation.
3. Build each trace's tree from `SpanId` / `ParentSpanId`. A root is a span with no
   `ParentSpanId`, or whose parent is absent from the group.
4. **Multiple roots** mean a truncated export. Keep the earliest-starting root as the tree
   root; count the remaining orphan subtrees and report the total. They are dropped rather
   than reparented, because attaching them under an unrelated node would fabricate a
   hierarchy that never existed.
5. Assign each node a path key: the sequence of `(OperationName, occurrenceIndex)` from the
   root down. `occurrenceIndex` orders same-named siblings by `StartTime` ascending, tie-broken
   on `SpanId` for determinism.
6. Merge across traces on that key, accumulating durations and an occurrence count.
7. Order children by mean start offset from the root, so the tree reads in execution order.

Because the index makes a key unique within a trace, each aggregate node satisfies:

- `avg = sum(durations) / occurrences`
- `coverage = occurrences / total traces`

### Why nodes are keyed by path, and why siblings are indexed

Two spans sharing an `OperationName` are not the same node. `SessionCreated` legitimately
appears under both `AzureCLI.ParseCommandInvocation` and `AzureCLI.ProcessCommand`; keying by
full path keeps them distinct.

The same name can also repeat under the *same* parent. `Engine.Subscriptions.cs` starts
activities from event handlers and pushes them onto a stack — `SessionCreated`,
`AuthBeforeTokenAcquisition`, and `ActionStart` each fire as many times as the run emits the
event. Three such siblings under one parent make the path
`Root > ProcessCommand > SessionCreated` match three nodes per trace, so "the average" is
ambiguous between combining all occurrences, averaging a single occurrence, or separating them.

**Decision: separate them, indexed by start order.** Each occurrence gets its own row, which
shows *which* call slowed down rather than burying it in a combined figure.

The known weakness is that the index is positional, so if occurrence order is not stable
across iterations the rows blend unrelated calls. This is mitigated, not eliminated: ordering
by `StartTime` is deterministic within a trace, and because each `(name, index)` key then
appears at most once per trace, a varying occurrence count surfaces as low coverage on the
higher indices — `SessionCreated[2] (43/97)` — which is visible rather than silent.

### Shape drift

The benchmarks are controlled: HTTP is stubbed, 5000 warmup iterations precede measurement,
and each case runs in its own process. Trees are expected to be near-identical, and the
aggregator does **not** need to reconcile genuinely different shapes.

It does need to survive three real sources of variation:

1. **Truncated traces.** `BatchActivityExportProcessor` drains on shutdown in
   `[IterationCleanup]`; a span still in flight is dropped, leaving that trace missing leaves.
2. **Orphan spans.** Same cause, seen from the child's side.
3. **Real MSAL cache IO.** Cache-hit versus refresh is a genuine branch, unlike stubbed HTTP.

**Decision: merge everything, and mark partial nodes with `n/N`.** A truncated trace
contributes fewer nodes instead of being discarded, and a node present in only 60 of 100
traces becomes visible. Nodes at full coverage show no marker, keeping the common case clean.

### Rendering

Per benchmark, a fenced block:

```
 Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups
 RESOURCEGROUP · CLI · 97 traces

      AVG   %ROOT
 42.310 ms   100%  ████████████████████  CLI.Activity.Root
  8.120 ms    19%  ███▌                  ├─ AzureCLI.Startup
  0.640 ms     2%  ▏                     │  ├─ AzureCLI.Startup.Container
  2.980 ms     7%  █▎                    │  ├─ AzureCLI.Startup.Services
  4.410 ms    10%  ██                    │  └─ AzureCLI.Startup.Telemetry
 28.450 ms    67%  █████████████▍        └─ AzureCLI.ProcessCommand
  3.100 ms     7%  █▌                       ├─ SessionCreated
  1.900 ms     4%  ▉                        └─ SessionCreated[1]  (43/97)
```

- Durations in milliseconds to three decimals — many spans are sub-millisecond, so coarser
  formatting would flatten them to `0.0`.
- Bar is 20 characters scaled against the root's average.
- `%ROOT` is `node avg / root avg`.
- Index suffix appears from `[1]` onward; the first occurrence is unadorned.
- Coverage `(n/N)` appears only below full coverage.

Two degenerate cases need explicit handling. A root average of zero makes `%ROOT` undefined,
so bars and percentages are suppressed for that benchmark and the tree renders with durations
only. A node whose average exceeds the root's — possible with clock skew on a truncated trace —
has its bar clamped to full width, and its percentage is printed as-is rather than capped, so
the anomaly stays visible.

Children will not always sum to their parent. Async gaps and uninstrumented work are real,
and the report does not claim otherwise.

### PR comment body

```markdown
## 🔬 Benchmark Trace Hierarchy

| Extension | Product | Benchmark | Traces | Root avg |
|---|---|---|---|---|
| RESOURCEGROUP | CLI | `ListResourceGroups` | 97 | 42.310 ms |
| RESOURCEGROUP | PS  | `GetAzResourceGroup` | 100 | 51.220 ms |

[📊 View full trace hierarchies](<run-url>)

<!-- benchmark-traces -->
```

The `<!-- benchmark-traces -->` marker is deliberately distinct from the existing
`<!-- benchmark-results -->`, so the two comments coexist and neither overwrites the other.

The link is the workflow run URL — job summaries render on that page — matching the `RUN_URL`
pattern already used by `benchmark-comment.yml`.

## Component: CI wiring

### `benchmark.yml`, `collect` job

Add three steps and no new permissions:

| Step | Notes |
| --- | --- |
| `actions/checkout@v4` | The job currently has none, so the script would not be on disk |
| Aggregate trace hierarchies | Writes `$GITHUB_STEP_SUMMARY` and `$RUNNER_TEMP/trace-comment.md` |
| `upload-artifact` `benchmark-traces` | Uploads the comment body, retention 1 day |

The job keeps `contents: read`. It does **not** gain `pull-requests: write`, because it no
longer posts anything.

`collect` does not write to gh-pages, so it cannot race the `publish-extensions` and
`publish-startup` jobs, which serialise among themselves via `max-parallel: 1`.

### `benchmark-comment.yml`, new `trace-comment` job

```yaml
  trace-comment:
    name: Post trace hierarchy on PR
    if: github.event.workflow_run.event == 'pull_request'
```

No fork restriction — unlike the existing `comment` job, this one runs for every PR. It
downloads `benchmark-traces` by `run-id`, resolves the PR number using the same
`headRefOid`-matching logic the existing job already uses, then finds a comment carrying the
marker and `PATCH`es it, or `POST`s a new one. Body text is read from the artifact and passed
through `gh api -f body=...` in Python, mirroring the call that already works in this file.

The existing `comment` job is left untouched. Its fork-only condition must stay, or non-fork
PRs would get a duplicate of the results comment that `github-action-benchmark`'s
`comment-always` already posts.

### Behaviour by trigger

| Trigger | Job summary | PR comment |
| --- | --- | --- |
| Push to `main` | Yes | No PR exists |
| Same-repo PR | Yes | Yes, via `trace-comment` |
| Fork PR | Yes | Yes, via `trace-comment` |

Both PR paths render the comment body once in `collect` and merely deliver it later, so fork
and same-repo comments are byte-identical and cannot drift.

## Error handling

Telemetry reporting must never fail a benchmark run, matching the posture `CollectTelemetry`
already takes in `Program.cs` ("Collection failed, benchmarks unaffected").

- `main` is wrapped; all failures print a warning to stderr and exit `0`.
- No `.jsonl` files found is a normal outcome, not an error. Print a note and exit.
- Malformed lines are skipped and counted, then reported in the summary footer.
- Traces that yield no usable root are counted and reported.
- Job summaries cap at 1 MiB. With enough extension × method combinations the output could
  exceed it, so the script tracks cumulative bytes and truncates with an explicit
  "output truncated" note rather than silently losing the tail.
- The artifact download in `trace-comment` uses `continue-on-error`, so a run that produced
  no telemetry does not fail the comment workflow.

## Testing

**Decision: no automated tests in v1.** This was an explicit call, made after Python's stdlib
`unittest` was offered as a zero-dependency option and declined.

The risk is concrete and worth stating: the tree-merge, sibling-indexing, and averaging logic
is the part most likely to be subtly wrong, and it is the part now going unverified. A silent
error here produces a plausible-looking tree with wrong numbers, which is worse than a crash.

Validation is therefore manual, and the script's design supports it:

1. Run against a real downloaded CI artifact and read the output; confirm node names, nesting,
   and durations match a known Application Insights end-to-end view for the same operation.
2. Run against a local `artifacts/Benchmark/<timestamp>` directory to confirm recursive
   discovery handles both layouts.
3. Confirm percentages and coverage counts are self-consistent — root at 100%, no coverage
   above `N`, no negative or zero-trace nodes.

Adding `unittest` coverage for the merge logic remains the obvious first follow-up.

## Files touched

| File | Change |
| --- | --- |
| `.github/scripts/aggregate-benchmark-traces.py` | New |
| `.github/workflows/benchmark.yml` | Three steps added to `collect` |
| `.github/workflows/benchmark-comment.yml` | New `trace-comment` job |

Nothing under `src/` or `tests/Benchmark/` changes — the telemetry producer already emits
everything required.

## Future work

- Baseline delta column, requiring per-node averages persisted per commit (gh-pages, beside
  the existing dashboards) plus a noise threshold tuned for node-level variance.
- `unittest` coverage for the merge and indexing logic.
- Publishing to gh-pages for stable per-commit URLs, if job-summary retention proves too short.
