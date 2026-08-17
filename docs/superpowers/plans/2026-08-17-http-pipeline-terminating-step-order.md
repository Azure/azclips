# HTTP Pipeline Terminating-Step Reorder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the HTTP pipeline's terminating step so it runs after user-agent, auth, and tracing but before the retry handlers, so a mocked terminator no longer skips the steps production always executes.

**Architecture:** `HttpPipeline` composes steps by folding a list — `Prepend` appends to the list, `Append` inserts at index 0, and the fold makes index 0 the innermost step. The terminating step is currently `Prepend`ed, which makes it outermost, so a mock that does not call `next` short-circuits the entire chain. Moving it to an `Append` placed after the tracing `Append` puts it at index 0, immediately inside tracing and immediately outside the retry handlers. Only `Engine.cs` changes; `HttpPipeline` itself is untouched.

**Tech Stack:** .NET 10 (`net10.0`), xUnit 2.9.3, BenchmarkDotNet 0.13.12.

## Global Constraints

- Use the .NET SDK pinned in `global.json`. Build with `dotnet build azclips.slnx`.
- C# with nullable enabled and implicit usings; follow existing naming, namespace, and folder conventions.
- Preserve existing public behavior for real users. `DefaultHttpTerminateHandler` is a pure passthrough, so its position must not alter production behavior — verify this reasoning holds rather than assuming it.
- Do not modify `src/Core/Core.Runtime/HttpPipeline.cs`. The retry handlers are composed there between the transport and the step list, and this change deliberately does not touch that.
- Do not modify the startup benchmarks (`tests/Benchmark/Startup/`).
- `[WarmupCount(5000)]` and `[IterationCount(5000)]` must be 5000 in committed code.
- Do not commit secrets.
- Scope is the reorder only. Do not add regression tests, and do not change `VerifyAuthFixture` — both were explicitly descoped by the user.

## Pre-Existing Working-Tree State

Before starting, the working tree has uncommitted changes that are NOT part of this plan and must not be committed by it:

- `tests/Benchmark/CLI/CLIBenchmarkBase.cs` and `tests/Benchmark/PS/PSBenchmarkBase.cs` — iteration/warmup counts lowered from 5000 to 5 by an interrupted `validate-benchmarks` run.
- `tests/Benchmark/CLI/ResourceGroupBenchmarks.cs` — two benchmark methods commented out during manual experimentation.

Task 1 resolves these so the branch is clean before the real change lands. Confirm with the user before discarding anything you did not expect.

## Background: why this change is needed

`Engine.CreateProcessorContext` builds the pipeline like this today:

```csharp
HttpPipeline: httpPipeline.Prepend(authHandler.SendAsync),          // steps = [auth]
httpPipeline.Prepend(httpPipelineService.HttpAddUserAgentStep);      // steps = [auth, userAgent]
httpPipeline.Prepend(httpPipelineService.HttpPipelineTerminatingStep); // steps = [auth, userAgent, terminating]
httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep); // steps = [tracing, auth, userAgent, terminating]
```

`HttpPipeline.Pipeline` then folds:

```csharp
var next = terminal.Create();
next = new SendAsyncFactory(Retry429).Create(next) ?? next;
next = new SendAsyncFactory(RetryError).Create(next) ?? next;
foreach (var factory in steps)
{
    next = factory.Create(next) ?? next;
}
```

Each factory wraps the previous value, so the LAST list entry becomes the OUTERMOST step and executes first. Current execution order is therefore:

```
terminating → userAgent → auth → tracing → RetryError → Retry429 → transport
```

`StubHttpTerminateHandler` (registered only by `tests/Benchmark/BenchmarkBase.cs`) returns a canned response **without calling `next`**. Because it is outermost, user-agent, auth, and tracing never execute during benchmark iterations. This was confirmed empirically: a temporary probe in `MsalAuthService.AcquireTokenSilentlyAsync` fired exactly once per run — from `VerifyAuthFixture` in `[GlobalSetup]`, which calls the auth service directly — and zero times across measured invocations that did execute.

Target execution order:

```
userAgent → auth → tracing → terminating → RetryError → Retry429 → transport
```

Production is unaffected because `DefaultHttpTerminateHandler` is `async (request, callback, next) => await next.SendAsync(request, callback)` — a step that only delegates has no observable effect wherever it sits, so the relative order of every other step is unchanged.

---

## File Structure

**Modify:**
- `src/Core/Core.Engine/Engine.cs` — move the terminating-step registration from `Prepend` to `Append`, positioned after the tracing `Append`.

No other file changes. No new files.

---

## Task 1: Clean the working tree

**Files:**
- Restore: `tests/Benchmark/CLI/CLIBenchmarkBase.cs`
- Restore: `tests/Benchmark/PS/PSBenchmarkBase.cs`
- Restore: `tests/Benchmark/CLI/ResourceGroupBenchmarks.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: a clean working tree, so Task 2's diff contains only the reorder.

- [ ] **Step 1: Inspect what is uncommitted**

```powershell
git status --porcelain
git --no-pager diff --stat
```

Expected: three modified files, matching the "Pre-Existing Working-Tree State" section above. If you see anything else, STOP and ask the user before proceeding — do not discard changes this plan did not predict.

- [ ] **Step 2: Restore the three files**

```powershell
git checkout -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs tests/Benchmark/CLI/ResourceGroupBenchmarks.cs
```

- [ ] **Step 3: Verify the tree is clean and the counts are back to 5000**

```powershell
git status --porcelain
(Select-String -Path tests/Benchmark/CLI/CLIBenchmarkBase.cs,tests/Benchmark/PS/PSBenchmarkBase.cs -Pattern 'Count\(5000\)').Count
(Select-String -Path tests/Benchmark/CLI/ResourceGroupBenchmarks.cs -Pattern '^\s*//\s*\[Benchmark').Count
```

Expected: `git status --porcelain` prints nothing; the `Count(5000)` count is `4`; the commented-out-benchmark count is `0`.

No commit for this task — restoring tracked files to HEAD leaves nothing to commit.

---

## Task 2: Reorder the terminating step

**Files:**
- Modify: `src/Core/Core.Engine/Engine.cs:132-137`

**Interfaces:**
- Consumes: a clean working tree from Task 1.
- Produces: pipeline execution order `userAgent → auth → tracing → terminating → RetryError → Retry429 → transport`. Nothing else consumes this; it is the final task.

- [ ] **Step 1: Read the current code in context**

Open `src/Core/Core.Engine/Engine.cs` and read lines 116-144. Confirm the block matches what the plan expects:

```csharp
            ProcessorContext context = new ProcessorContext
            (
                HttpPipeline: httpPipeline.Prepend(authHandler.SendAsync),
                Publisher: session.EventBus,
                AuthHandler: authHandler,
                OutputHandler: outputHandler,
                Profile: profile
            );
            // stamp User-Agent header on every outgoing request
            httpPipeline.Prepend(httpPipelineService.HttpAddUserAgentStep);
            // passthrough by default, terminate during client mocking
            httpPipeline.Prepend(httpPipelineService.HttpPipelineTerminatingStep);
            // trace every http request message, record/playback request during test
            httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep);
```

If it does not match, STOP and report — the line numbers or content have drifted.

- [ ] **Step 2: Apply the reorder**

Replace exactly this:

```csharp
            // stamp User-Agent header on every outgoing request
            httpPipeline.Prepend(httpPipelineService.HttpAddUserAgentStep);
            // passthrough by default, terminate during client mocking
            httpPipeline.Prepend(httpPipelineService.HttpPipelineTerminatingStep);
            // trace every http request message, record/playback request during test
            httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep);
```

with exactly this:

```csharp
            // stamp User-Agent header on every outgoing request
            httpPipeline.Prepend(httpPipelineService.HttpAddUserAgentStep);
            // trace every http request message, record/playback request during test
            httpPipeline.Append(httpPipelineService.HttpRequestMessageTracingStep);
            // Passthrough by default; terminates the chain during client mocking. Appended last so it
            // sits innermost among the steps — after User-Agent, auth, and tracing, and immediately
            // before the retry handlers and the transport — so a mock short-circuits only the network
            // send. Prepending it here would make it outermost, silently skipping the auth,
            // User-Agent, and tracing work that production always performs.
            httpPipeline.Append(httpPipelineService.HttpPipelineTerminatingStep);
```

Note the ORDER of the two `Append` calls is load-bearing: `Append` is `steps.Insert(0, …)`, so whichever is appended LAST ends up innermost. Tracing must be appended before the terminating step.

- [ ] **Step 3: Verify the resulting order by reasoning through the fold**

No code to run for this step — confirm on paper and record it in your report:

steps list after the change is `[terminating, tracing, auth, userAgent]`, because:
- `Prepend(auth)` → `steps.Add` → `[auth]`
- `Prepend(userAgent)` → `steps.Add` → `[auth, userAgent]`
- `Append(tracing)` → `steps.Insert(0, …)` → `[tracing, auth, userAgent]`
- `Append(terminating)` → `steps.Insert(0, …)` → `[terminating, tracing, auth, userAgent]`

The fold wraps in list order, so the last entry is outermost. Execution is the reverse of the list, then the fixed core:

```
userAgent → auth → tracing → terminating → RetryError → Retry429 → transport
```

Confirm that matches the goal. If your reasoning produces a different order, STOP and report rather than "fixing" it by guessing.

- [ ] **Step 4: Build the solution**

```powershell
dotnet build azclips.slnx -c Release
```

Expected: build succeeds, 0 errors. This is slow (several minutes) — use a generous timeout and wait rather than assuming a hang. If the build fails to restore the GitHub Copilot npm package in `Core.Agent` with a TLS/SSL error, use the `building-the-solution` skill, which documents that known issue.

- [ ] **Step 5: Run the test suites**

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj
```

Expected: `Scenario.Tests` 42/42 passed. `Cli.Tests` 587 passed, 1 failed, 7 skipped — the single failure is `ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`, a KNOWN PRE-EXISTING failure caused by a missing `artifacts\Debug\Core.Cli\az` binary, and the 7 skips are static `[Fact(Skip=…)]` attributes.

If any ADDITIONAL test fails, that is a real regression from this change — report it rather than working around it. `Scenario.Tests` is the suite most likely to notice, since it drives the engine end to end.

- [ ] **Step 6: Prove auth now runs inside the measured benchmark path**

This is the whole point of the change, so verify it rather than inferring it from a green build.

Add a temporary probe to `src/Core/Core.Authentication/MsalAuthService.cs`. Find this line inside `AcquireTokenSilentlyAsync`:

```csharp
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");
```

and add immediately after it:

```csharp
        Console.WriteLine("PROBE_ACQUIRE_CALLED");
```

Then rebuild and run a single benchmark class:

```powershell
dotnet build azclips.slnx -c Release
dotnet run -c Release --project tests/Benchmark/Benchmark.csproj -- --local --product CLI --extension Resources
```

Count the probe lines in the output. Before this change the count was exactly 1 (the `[GlobalSetup]` verification only). After this change it must be substantially greater than 1, because every measured CLI invocation now runs the auth step.

Interpretation:
- Count `> 1` → the reorder worked; auth is executing inside the benchmark path.
- Count `== 1` → the reorder did NOT take effect. Do not proceed; report it.

- [ ] **Step 7: Remove the temporary probe**

```powershell
git checkout -- src/Core/Core.Authentication/MsalAuthService.cs
```

Verify it is gone — this must return `0`:

```powershell
(Select-String -Path src/Core/Core.Authentication/MsalAuthService.cs -Pattern 'PROBE_ACQUIRE_CALLED').Count
```

The probe must NOT be committed.

- [ ] **Step 8: Confirm the diff contains only the reorder**

```powershell
git status --porcelain
git --no-pager diff
```

Expected: exactly one modified file, `src/Core/Core.Engine/Engine.cs`, showing only the moved registration and its new comment.

- [ ] **Step 9: Commit**

```powershell
git add src/Core/Core.Engine/Engine.cs
git commit -m "fix(engine): run the HTTP terminating step after auth, User-Agent, and tracing"
```

---

## Expected Consequence

Benchmark timings will rise. Before this change, a mocked terminator short-circuited the chain immediately, so user-agent, auth, and tracing never ran during measured iterations — the MSAL token-cache file IO that the benchmarks were supposed to measure was never executed. After it, all three run.

The dominant new cost is the MSAL token-cache disk read; user-agent is a bare delegate and tracing publishes one event into `BenchmarkEventPublisher`, whose `Publish` returns `Task.CompletedTask`. Both are negligible.

The 115% regression gate will flag the introducing pull request. That is expected and requires a one-time rebaseline on merge to `main`.

## Out of Scope

Descoped by the user; record as follow-ups rather than implementing:

- A regression test pinning the pipeline execution order. Nothing currently prevents this from silently reverting.
- Strengthening `BenchmarkBase.VerifyAuthFixture`, which calls `AcquireTokenSilentlyAsync` directly and therefore passes whether or not the benchmark path ever reaches auth. It cannot detect this class of bug.
