# Scenario Testing (Record / Playback)

## Overview

Scenario tests verify command correctness end to end: through the real dispatcher, argument
binders, engine, and HTTP pipeline, while the traffic to Azure is served from a recorded file
instead of the network. They run on every PR with no credentials, deterministically, in
milliseconds. This is the model the Azure PowerShell test framework uses for 200+ modules,
rebuilt on Azclips' own pipeline seam.

A scenario is authored once and covers both client surfaces: the CLI command and the PowerShell
cmdlet resolve to the same processor and emit byte-identical ARM traffic (verified by permanent
parity tests), so one recording drives both.

- Recording (one time, by the author): run the scenario against a real test subscription with
  `AZURE_TEST_MODE=Record`; every request/response is captured, sanitised, and written to a JSON
  file that is committed.
- Playback (every CI run, the default): the same test replays the file. No Azure, no secrets,
  identical every time.

Delivered through Feature #318 (epic #208) in PRs #562 (recorder, format, mode switch, parity
tests), #622 (sanitisers), #623 (first real recording: `group create`), and #637 (secret-scan
gate). Test code lives in `tests/Scenario.Tests/` and `tests/Common/Recording/`; the authoring
guide is `tests/Scenario.Tests/README.md`.

## How It Works

Extensions never own an `HttpClient`. The engine injects `IRuntimeHttpPipeline`, a composable
`SendAsyncStep` chain, into each processor, and reserves a tracing step for record/playback. The
recorder (`RecordPlaybackInterceptor`) registers as the `ITracingInterceptor` DI singleton and
fills that seam; no engine changes were needed.

```
TerminatingStep      auth          RecordPlaybackInterceptor      RetryError   Retry429   HttpClient
(outermost,     ->  (token)  ->   (record: forward + capture  ->     ...    ->   ...   ->  (wire)
 pre-auth)                         playback: answer, stop here)
```

The seam sits after auth, so the recorder sees requests exactly as they would hit the network
(bearer token attached, which is why sanitisation is mandatory), and playback short-circuits
before the retry handlers and the wire.

In record mode the interceptor forwards to the wire and appends the sanitised request/response
pair to the session record. In playback it matches each outgoing request against the record
(method + URI by default, each entry consumed once, duplicates served in recorded order, which is
what makes LRO polling sequences replayable) and returns the stored response. An unmatched request
fails with the request line and the record's unconsumed entries. The interceptor is thread-safe:
orchestrators fan out concurrent requests (vm create fires vnet + nsg in parallel) and matching
consumes entries atomically.

Matching is pluggable (`IRequestMatcher`) for scenarios that need normalization of
non-deterministic request fields, and the sanitisers already normalise subscription and tenant ids
on both sides, so a recording made under one subscription replays under any other.

Tests execute subcutaneously: the harness (`ScenarioHost`) boots the full DI container per surface
and drives the real `ExtensionDispatcher` and argument binders in-process rather than spawning
`az.exe`. Tradeoff, stated honestly: defects confined to the CLI process entrypoint or the PS
cmdlet binder layer are not caught here; those belong to unit tests.

## Recording Format and Layout

Recordings are versioned JSON with free-form metadata and ordered entries, at
`tests/<Project>/SessionRecords/<TestClass>/<TestName>.json`, resolved from the repository root so
record mode writes a committable source file and CI replays that same file. Round-tripping is
lossless. `RecordingSession` ties the mode, the canonical path, and per-surface interceptors
together, so an authored test is a few lines; see the README for the pattern.

## Keeping Secrets Out of Recordings

Two layers, both delivered:

1. Sanitisers (prevention, #622): a central extensible list scrubs recordings as they are
   written. Subscription and tenant ids become a fixed placeholder, bearer tokens are redacted,
   the `Authorization` and `api-key` headers are dropped, OpenAI keys and endpoints are scrubbed.
   The same rewrites run over outgoing requests during playback so sanitised recordings still
   match. Verified against a real recording: the live subscription and tenant appear nowhere in
   the committed file.
2. Secret-scan gate (detection, #637): the `recording-secret-scan` job scans the recordings a PR
   adds or changes (`devops/scripts/ci/scan_recordings.py`) with rules that mirror the sanitisers,
   and fails on anything that looks like a leak. Findings report file, line, and rule, never the
   value; unreadable recordings fail the check. This keeps Azure CLI's incremental
   only-scan-what-changed approach with our own implementation for our JSON format.

## Limitations and Test Tiers

Playback is a contract test. It pins the client's command-to-REST translation and response
handling against a frozen snapshot, which catches client-side regressions (the bulk of day-to-day
breakage) but not service-side drift. The one true end-to-end moment is record time. The standard
answer is two tiers:

| Tier | When | Mode | Creds | Purpose |
|------|------|------|-------|---------|
| Fast / PR gate | every PR | Playback | none | Catch client regressions; deterministic, fast |
| Slow / scheduled | nightly or weekly | Live record-and-assert | pipeline identity | Catch service drift, refresh recordings |

The fast tier is live today. The scheduled tier is not built yet; the `AZURE_TEST_MODE=Record`
mechanism is ready to be driven by a pipeline, and the tier needs a job, an identity, and an
owner.

Renames and command-surface changes are deliberately out of scope: the command name never travels
the wire, so record/playback cannot see it. That is the job of a separate command-surface
snapshot-and-diff gate (the raw material exists in `CLIContracts` attributes and
`ExtensionHelpManifestBuilder`), tracked outside this effort.

## Delivered Work

| Task | Issue | State |
|------|-------|-------|
| Author-once parity spike: raw HTTP of `group create` diffed across CLI and PS through the real binders; byte-identical | (no issue) | Done; kept as a permanent CI gate |
| Record/replay `SendAsyncStep` at the tracing seam | #470 | Merged (#562) |
| SessionRecords format, layout, pluggable matcher | #471 | Merged (#562) |
| `AZURE_TEST_MODE` record/playback switch | #472 | Merged (#562) |
| Recording sanitisers with playback normalisation | #473 | Merged (#622) |
| POC: `group create` recorded live, committed, replaying on both surfaces | #474 | Merged (#623) |
| Author-once / replay-both harness | #518 | Core satisfied (one scenario + one recording covers both surfaces); close-out pending |
| Recorder library decision | (no issue) | Resolved: custom in-repo recorder at the Azclips seam; `Microsoft.Azure.Test.HttpRecorder` not consumed |
| Secret-scan CI gate on changed recordings | #500 | Merged (#637) |

Findings worth keeping: processors hand-build their `HttpRequestMessage` (no Azure SDK client in
the path), which is why CLI/PS parity holds by construction and only argument binding differs per
surface. Running the recorded real response through the deserializer exposed a product bug:
`group create` never surfaces `provisioningState` because the `ResourceGroup` model declares it
flat while ARM nests it under `properties` (needs its own issue).

## Roadmap

Current work, in order:

1. #564: the Compute `New-AzVM` scenario, the first LRO + orchestration recording. One recording
   shared with the benchmarks, replacing their hand-written fixtures (`StubResponseMappings` and
   `Stubs/Fixtures/*.json`).
2. Secret-scan follow-ups: a negative drill (draft PR with a deliberately bad specimen, confirm
   the check goes red, close it) and adding `recording-secret-scan` to the required status checks
   so red blocks merge.
3. The scheduled live tier (drift detection and recording refresh) with a named owner.
4. #518 close-out and socializing the design with the client tools team.

## Open Questions

1. Enforce a scenario test per command/cmdlet, or keep coverage curated? Related: should scenario
   coverage across the command surface be measured and enforced?
2. How does Azure CLI keep its recordings fresh: automatic re-record in a pipeline, or authors
   re-recording when tests break? What should the live tier copy?
3. Single or multiple recording files across environments (PS + CLI)? One shared file serves both
   surfaces today; open whether any environment- or surface-specific scenario will ever force
   per-surface recordings, and the layout convention if so.
4. Regulation for creating the tests: when a change requires a scenario test, review rules for
   recordings, naming and ownership, and who signs off re-records. The mechanics are in the
   README; the policy is not written.
5. Which test subscription do we record against long term, and who owns recording refreshes?

## References

- Authoring guide: `tests/Scenario.Tests/README.md`
- Recorder: `tests/Common/Recording/`; harness and scenarios: `tests/Scenario.Tests/`
- Secret scan: `devops/scripts/ci/scan_recordings.py` + the `recording-secret-scan` job in
  `.github/workflows/build-and-test.yml`
- Seam: `src/Core/Core.Engine/Engine.cs`, `src/Core/Core.Runtime/HttpPipeline.cs`,
  `Core.Runtime/DIRegisterExtensions.cs`
- Prior art: Azure PowerShell `documentation/testing-docs/using-azure-test-framework.md`; Azure
  CLI incremental recording secret scan
- Tracking: Feature #318, Epic #208, next scenario #564
