---
applyTo: "tests/**,Agent/tests/**,Agent/eval/**/*.test.js,toolkit/**/test/**,toolkit/**/tests/**,devops/scripts/**/test_*.py,.github/scripts/test_*.py"
description: "Use when writing, modifying, or reviewing tests and test fixtures with delays, sleeps, polling, retry intervals, timeouts, cancellation, or concurrent synchronization."
---

# Test waits and timing validation

- Do not use a fixed sleep to make asynchronous work, a process, a lock, or a
  background task "probably ready". Await an observable condition or explicit
  signal, with a bounded failure timeout. A timeout is a failure ceiling, not a
  synchronization mechanism.
- Prefer existing controllable clocks (`TimeProvider` where supported), completion
  signals (`TaskCompletionSource` with `RunContinuationsAsynchronously`), and
  barriers over elapsed wall-clock time. Do not add production test-only switches
  or change production retry/deadline behavior just to speed up a test.
- In non-timing scenarios, fake services must not impose real retry or polling
  sleeps. Use zero retry intervals or controlled time while retaining assertions
  on poll requests, operation order, completion, and output. Record/playback tests
  must avoid fake-backend delays in the recording phase as well as playback.
- Real delays are justified when the contract under test is elapsed time, timeout,
  cancellation, retry pacing, or lock contention. Keep the production deadline
  and meaningful lower/upper-bound assertions; do not simply shorten, skip, or
  delete the test. Use the shortest delay that still proves that contract.
- Every introduced or retained real sleep must have a brief explanation of the
  contract it proves. Release blocked fakes in `finally`, make waits cancellable
  where possible, and await owned tasks/processes during cleanup. Do not let a
  delayed fake extend teardown beyond the behavior being measured.
- Prefer asynchronous waits over `.Wait()`, `.Result`, or `Thread.Sleep` in async
  tests. If testing a synchronous API requires blocking, keep that boundary
  explicit and bounded, and ensure a failed assertion cannot strand a worker.

## Required validation before completion

1. Inspect changed tests **and their setup, fakes, retry headers, and teardown** for
   `sleep`, `Thread.Sleep`, `Task.Delay`, blocking waits, polling intervals, and
   deadline changes. Classify each wait as synchronization, failure ceiling, or
   part of the timing contract; replace unjustified elapsed-time synchronization.
2. Prove the test still fails when the guarded behavior is broken. For race tests,
   establish the required ordering with signals rather than assuming scheduling;
   for timing tests, verify both the measured deadline and required output shape.
3. Run the smallest relevant suite with per-case timings. Investigate every
   changed case over **one second**, including time spent in setup and teardown,
   and repeat slow cases without unrelated build/test load.
4. Report exact commands, results, before/after timings, and why any remaining
   real wait is necessary. Do not enforce a blanket one-second cap on legitimate
   integration or production-budget tests, or claim speedups from skipped coverage.

Read [the repository guide](../../AGENTS.md) and
[the Agent test guide](../../Agent/AGENTS.md) when applicable.
