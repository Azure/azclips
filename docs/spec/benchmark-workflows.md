# Benchmark Workflows

## Overview

During each PR. The performance benchmarks will be run and compared against the latest historical baseline.
If any performance regression is detected, the PR check will fail and a comment will be posted to the PR with details.
When a push to `main` is made, the new benchmark results will be published to GitHub Pages, updating the historical baseline for future comparisons.
A dashboard is available to view the benchmark history and trends over time.
CLI - https://azure.github.io/cirrus/dev/bench/cli/
PS - https://azure.github.io/cirrus/dev/bench/ps/

### `benchmark.yml` — Performance Benchmarks

Runs the benchmarks of both CLI and PS extensions and uploads results as artifacts to be consumed by `benchmark-comment.yml`.
The pipeline will fail if any benchmark exceeds **115%** of the baseline, preventing merges that degrade performance. For same-repo PRs, it also directly posts a warning comment on the PR.

### `benchmark-comment.yml` — Post Benchmark Comment

This pipeline is triggered to post a comment on the PR with the benchmark results and regression warnings.
It is needed as pipelines initiated from forked PR have read-only permissions and cannot post comments or push to `gh-pages`.

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

