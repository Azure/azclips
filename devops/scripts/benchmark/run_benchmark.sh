#!/usr/bin/env bash
# Runs a benchmark command with a per-attempt timeout and retries, like the
# nick-fields/retry step of the former GitHub workflow. Azure Pipelines'
# retryCountOnTaskFailure does not retry a step that hits its timeout, so the
# timeout and retry live here. GNU timeout stops the whole process group, which
# includes the child processes BenchmarkDotNet starts.
#
# Usage: run_benchmark.sh <command> [arguments...]
# Environment:
#   BENCHMARK_ATTEMPT_TIMEOUT  per-attempt timeout for GNU timeout, e.g. 60m (required)
#   BENCHMARK_MAX_ATTEMPTS     attempts before giving up (default 2)
#   BENCHMARK_RETRY_WAIT       seconds to wait between attempts (default 30)
set -euo pipefail

attempt_timeout="${BENCHMARK_ATTEMPT_TIMEOUT:?BENCHMARK_ATTEMPT_TIMEOUT is required}"
max_attempts="${BENCHMARK_MAX_ATTEMPTS:-2}"
retry_wait="${BENCHMARK_RETRY_WAIT:-30}"

if [[ $# -eq 0 ]]; then
  echo "usage: run_benchmark.sh <command> [arguments...]" >&2
  exit 2
fi

status=1
for ((attempt = 1; attempt <= max_attempts; attempt++)); do
  echo "Benchmark attempt ${attempt} of ${max_attempts} (timeout ${attempt_timeout})"
  status=0
  timeout --kill-after=60 "$attempt_timeout" "$@" || status=$?
  if [[ "$status" -eq 0 ]]; then
    exit 0
  fi
  if [[ "$status" -eq 124 ]]; then
    echo "Attempt ${attempt} timed out after ${attempt_timeout}."
  else
    echo "Attempt ${attempt} failed with exit code ${status}."
  fi
  if ((attempt < max_attempts)); then
    sleep "$retry_wait"
  fi
done
exit "$status"
