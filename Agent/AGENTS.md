# Agent — Agent Guide

Scoped guidance for the AI agent under `Agent/` and its tests. Read this before
changing Agent code or Agent tests. When a rule here changes, update this guide in
the same PR.

## Tests

- Build expected paths from the product's path constants (for example `HomePaths`),
  not from string literals. A literal path can drift from what the product writes, and
  the assertion then passes for the wrong reason.
- Prove that a new assertion fails when the condition it guards is broken.
- Do not write tests for test helpers. Keep fakes, harnesses and fake servers simple
  enough to verify by reading. Add a guard to a helper only when a real test needs it.
  If a helper needs its own tests, simplify it instead.
- Tests must not depend on machine speed. See "Timing in tests".

## Timing in tests

- Do not assert an upper bound on elapsed time.
- Product code with a time budget takes a `TimeProvider` (default `TimeProvider.System`).
  Its tests use `FakeTimeProvider` or `SignalingFakeTimeProvider`, and wait for the timer
  (`WaitForTimerAsync`) before calling `Advance` when the timer may not be armed yet.
  Test infrastructure defaults to a fake clock that never advances.
- A wait for something that must happen on a real thread, process or I/O uses the shared
  60-second hang guard: `.WithAgentHangGuard()`, or `AgentTestTimeouts.HangGuard` where a
  `TimeSpan` is needed (`tests/Common/Utilities/AgentTestTimeouts.cs`).
- A short window of 50-100 ms is allowed only to show that something did not happen.
- A budget the test owns must not end the operation under test. Give the operation its own
  untimed token and assert which token cancelled it.
- A child process the test expects to be killed must outlive every wait in the test.
- Mark any test that is still speed-dependent `[Trait("Category", "LocalOnly")]`. CI skips it.
