# Benchmark Workflows

## Overview

During each PR and push to `main`, performance benchmarks are run and compared against the latest historical baseline.

- **On PR** — if any benchmark exceeds **115%** of the baseline, the check fails and a comment is posted to the PR with details.
- **On push to `main`** — new results are published to GitHub Pages, updating the baseline for future comparisons.

**Dashboard:**
- CLI — https://azure.github.io/azclips/dev/bench/cli/
- PS — https://azure.github.io/azclips/dev/bench/ps/

### `benchmark.yml` — Performance Benchmarks

Runs CLI and PS benchmarks and uploads results as artifacts to be consumed by `benchmark-comment.yml`. The pipeline will fail if any benchmark exceeds **115%** of the baseline, preventing merges that degrade performance. For same-repo PRs, it also directly posts a warning comment on the PR.

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

