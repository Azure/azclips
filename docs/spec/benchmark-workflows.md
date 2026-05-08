# Benchmark Workflows

## Overview

During each PR and push to `main`. The performance benchmarks will be run and compared against the latest historical baseline.
If any performance regression is detected, the PR check will fail and a comment will be posted to the PR with details.

### `benchmark.yml` — Performance Benchmarks

Runs the benchmarks of both CLI and PS extensions and uploads results as artifacts to be consumed by `benchmark-comment.yml`.
The pipeline will fail if any benchmark exceeds **115%** of the baseline, preventing merges that degrade performance. For same-repo PRs, it also directly posts a warning comment on the PR.

### `benchmark-comment.yml` — Post Benchmark Comment

This pipeline is triggered to post a comment on the PR with the benchmark results and regression warnings.
It is needed as pipelines initiated from forked PR have read-only permissions and cannot post comments or push to `gh-pages`.

## Benchmark Dashboard (GitHub Pages)

Benchmark history is published to the `gh-pages` branch and can be viewed at:

> **🔗 Dashboard URL:** `https://azure.github.io/cirrus/dev/bench/cli/` (CLI) and `https://azure.github.io/cirrus/dev/bench/ps/` (PS)
>
> _(Update this link once GitHub Pages is enabled for the repository.)_
