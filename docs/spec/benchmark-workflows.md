# Benchmark Workflows

## Overview

During each PR and push to `main`, performance benchmarks are run and compared against the latest historical baseline.

- **On PR** — if any benchmark exceeds **115%** of the baseline, the check fails and a comment is posted to the PR with details.
- **On push to `main`** — new results are published to GitHub Pages, updating the baseline for future comparisons.

**Dashboards** (one page per product; one chart per extension):
- CLI — https://azure.github.io/azclips/dev/bench/cli/ (charts: `Azclips <Extension> CLI Benchmarks`)
- PS — https://azure.github.io/azclips/dev/bench/ps/ (charts: `Azclips <Extension> PS Benchmarks`)

### `benchmark.yml` — Performance Benchmarks

Runs CLI and PS benchmarks and uploads results as artifacts to be consumed by `benchmark-comment.yml`. The pipeline will fail if any benchmark exceeds **115%** of the baseline, preventing merges that degrade performance. For same-repo PRs, it also directly posts a warning comment on the PR.

The pipeline is **fully generic across extensions** — the set of benchmarked extensions
is derived at runtime from the `[BenchmarkExtension("<Name>")]` attribute on the benchmark
classes, so adding a new extension needs no workflow edits. A `detect-changes` job runs
`.github/scripts/detect-benchmark-changes.sh`: `dorny/paths-filter` supplies a `shared`
flag, a `startup` flag, and the changed-file list, and the script greps the attribute to
build the extension universe and map each change to an extension. Changes to shared code
(`src/Core/**`, `src/CLI/**`, the benchmark base classes/stubs, `global.json`,
`azclips.slnx`, the workflow, or the detection script) run **all** extensions; changes
scoped to a single extension (`src/Extensions/<Ext>/**`, `src/PS/<Ext>/**`, or its
benchmark classes) run only that extension; `workflow_dispatch` runs all. The solution is
built once, `run-cli`/`run-ps` execute `--ci --extension <ext> --product CLI|PS` over the
extension matrix, and a generic `publish-extensions` matrix (extension × product) derives
each chart name and publishes its report to `dev/bench/cli` / `dev/bench/ps`. Whole-process
startup benchmarks are cross-cutting and published separately to
`dev/bench/startup-cli` / `dev/bench/startup-ps`.

### `benchmark-comment.yml` — Post Benchmark Comment

Triggered after `benchmark.yml` completes to post benchmark results and regression warnings on fork PRs. This is needed as pipelines initiated from forked PRs have read-only GitHub tokens and cannot post comments or push to `gh-pages` directly.

## Workflow Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TRIGGERS                                      │
│   ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐        │
│   │  Pull Request     │  │  Push to main     │  │ workflow_dispatch │        │
│   └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘        │
└────────────┼─────────────────────┼─────────────────────┼──────────────────┘
             └─────────────────────┼─────────────────────┘
                                   ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     benchmark.yml — Performance Benchmarks                  │
│                                                                             │
│   ┌──────────────────────────────────────────────────────────────────┐     │
│   │  Run CLI & PS benchmarks, compare against gh-pages baseline      │     │
│   │  Gate: fail if any benchmark exceeds 115% of baseline            │     │
│   └──────────────────────────┬───────────────────────────────────────┘     │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │
               ┌───────────────┼────────────────┐
               │               │                │
               ▼               ▼                ▼
     (same-repo PR)     (push to main)     (fork PR)
               │               │                │
               ▼               ▼                ▼
  ┌──────────────────┐  ┌────────────────┐  ┌──────────────────────┐
  │  Post comment     │  │ Auto-push to   │  │ Upload results as    │
  │  on PR directly   │  │ gh-pages branch│  │ workflow artifact     │
  └────────┬─────────┘  └───────┬────────┘  └──────────┬───────────┘
           │                    │                       │
           │                    ▼                       ▼
           │        ┌────────────────────┐  ┌───────────────────────────────┐
           │        │ GitHub Pages        │  │ benchmark-comment.yml          │
           │        │ Dashboard           │  │ (triggered by workflow_run)    │
           │        │ dev/bench/cli/      │  │                               │
           │        │ dev/bench/ps/       │  │  Download artifact, checkout  │
           │        └────────────────────┘  │  gh-pages baseline, find PR,  │
           │                                │  generate & post comment      │
           │                                └───────────────┬───────────────┘
           │                                                │
           ▼                                                ▼
        ┌──────────────────────────────────────────────────────┐
        │                   PR Conversation                     │
        │       Benchmark results table with regression warnings│
        └──────────────────────────────────────────────────────┘
```

