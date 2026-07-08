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

A `detect-changes` job uses `dorny/paths-filter` to determine which extensions changed
and emits a matrix. Changes to shared code (`src/Core/**`, the benchmark runner/stubs,
`global.json`, `azclips.slnx`) run **all** extensions; changes scoped to a single
extension (`src/Extensions/<Ext>/**`, `src/PS/<Ext>/**`, or its benchmark classes) run
only that extension. `workflow_dispatch` runs all. The matrix `benchmark` job builds,
runs `--ci --extension <ext> --product CLI|PS`, and publishes each report to
`dev/bench/cli` / `dev/bench/ps` under its own chart name.

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

