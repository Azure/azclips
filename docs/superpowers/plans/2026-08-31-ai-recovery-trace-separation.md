# AI Error-Recovery Trace Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop AI error-recovery from inflating `CLI.Activity.Root` durations, and record it instead as its own trace that references the command trace it came from.

**Architecture:** `az` stops the command root span the moment the command fails, then opens a second root, `CLI.Activity.AiRecovery`, around the agent subprocess spawn and wait. The CLI passes that span's W3C traceparent to `azclips-agent.exe` on the child environment block, and the agent's spans attach to it as real children. Correlation is one-directional, recovery → command, carried as `properties.*` tags.

**Tech Stack:** C# / .NET (SDK pinned in `global.json`), `System.Diagnostics.Activity`, OpenTelemetry 1.15.3, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-31-ai-recovery-trace-separation-design.md`
**Issue:** [Azure/CLIPS#602](https://github.com/Azure/CLIPS/issues/602)

## Global Constraints

- Build with `dotnet build azclips.slnx`. Run targeted tests with `dotnet test <test-project>.csproj`.
- C# with nullable enabled and implicit usings. Follow existing naming, namespace, and folder conventions.
- **The agent's `ActivitySource` name must be exactly `"CLI"`**, matching `CLITelemetryProvider`. `FileExporter.GetResource` derives `service.name` from `ActivitySource.Name`, and `TraceUploader.Run` rebuilds parent/child edges only within a service-name group. Any other name silently reparents every agent span to a random span ID on upload.
- **Every new activity name must be added to `DefaultTelemetryActivityNameFilter`.** `DefaultActivityNameFilterProcessor.OnEnd` clears the `Recorded` flag on any name not on the allowlist, and the span is dropped with no error.
- **Every new tag key must start with `properties.`** — `DefaultTelemetryPropertyFilter.IsAllowed` admits that prefix as a vetted product channel. Keys without it are stripped unless added to `AllowedKeys`.
- Telemetry must never carry prompts, error text, model output, or command parameter values. Names, durations, counts, exit codes, provider, and session style only.
- Do not change when AI recovery triggers, its console output, or its exit-code semantics.

---

### Task 1: Name the recovery root

**Files:**
- Modify: `src/Core/Core.Abstractions/ITelemetryService.cs:28`
- Modify: `src/Core/Core.Telemetry/TelemetryService.cs:48-60`
- Modify: `tests/Common/Stubs/StubTelemetryService.cs:19-20`
- Modify: `tests/Cmdlets.Tests/ImportTelemetryTests.cs:131-132,149`
- Modify: `tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs:252-253,270`
- Test: `tests/Core.Telemetry.Tests/TelemetryServiceStartTimeTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `ITelemetryService.StartRootActivity(DateTimeOffset startTime = default, string? name = null)`. When `name` is null the span keeps its current name, `$"{ClientType}.Activity.Root"`. Task 4 calls it as `StartRootActivity(name: "CLI.Activity.AiRecovery")`.

`StartRootActivity` currently hardcodes its name. Task 4 needs a second root in the same process with a *different* name, because two rows both called `CLI.Activity.Root` are indistinguishable in the App Insights `requests` table. It must stay `StartRootActivity` rather than plain `StartActivity` so the span still gets the invariant tags (`InstallationId`, `productName`, `productVersion`, `schemaVersion`, `telemetryVersion`, `clientType`) that make a root row queryable.

- [ ] **Step 1: Write the failing test**

Append to `tests/Core.Telemetry.Tests/TelemetryServiceStartTimeTests.cs`, inside the existing test class. Open the file first and confirm the fixture's field name for the service before pasting:

```csharp
    [Fact]
    public void StartRootActivity_WithCustomName_UsesItAndKeepsInvariantTags()
    {
        using var activity = _telemetryService.StartRootActivity(name: "CLI.Activity.AiRecovery");

        Assert.NotNull(activity);
        Assert.Equal("CLI.Activity.AiRecovery", activity.OperationName);
        // eventName mirrors the span name; the invariants are what make a root row queryable.
        Assert.Equal("CLI.Activity.AiRecovery", activity.GetTagItem(TelemetryTags.EventName));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.InstallationId));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.ProductName));
        Assert.NotNull(activity.GetTagItem(TelemetryTags.ClientType));
    }

    [Fact]
    public void StartRootActivity_WithoutName_KeepsTheClientTypeDefault()
    {
        using var activity = _telemetryService.StartRootActivity();

        Assert.NotNull(activity);
        Assert.EndsWith(".Activity.Root", activity.OperationName);
    }
```

Add `using Core.Abstractions;` if `TelemetryTags` is not already imported.

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~StartRootActivity_WithCustomName"
```

Expected: compile error — `StartRootActivity` has no parameter named `name`.

- [ ] **Step 3: Add the parameter to the interface**

In `src/Core/Core.Abstractions/ITelemetryService.cs`, replace the `StartRootActivity` declaration:

```csharp
    /// <param name="startTime">See <see cref="StartActivity"/>.</param>
    /// <param name="name">
    /// Overrides the default <c>{ClientType}.Activity.Root</c> span name. A process may open
    /// more than one root — AI error recovery opens a second one after the command root has
    /// closed — and two rows sharing a name are indistinguishable in the telemetry backend.
    /// </param>
    Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null);
```

- [ ] **Step 4: Implement it**

In `src/Core/Core.Telemetry/TelemetryService.cs`, replace the opening of `StartRootActivity`:

```csharp
    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null)
    {
        // No redundant IsTelemetryEnabled() here — StartActivity below is the single gate.
        var activity = StartActivity(
            name ?? $"{_clientType.ToString()}.Activity.Root", ActivityKind.Server, startTime);
        if (activity is null) return null;
```

Leave the rest of the method unchanged.

- [ ] **Step 5: Update the five test stubs**

`tests/Common/Stubs/StubTelemetryService.cs` — replace lines 19-20:

```csharp
    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
        ActivitySource.StartActivity(name ?? "Stub.Activity.Root", ActivityKind.Server, parentContext: default, tags: null, links: null, startTime: startTime);
```

`tests/Cmdlets.Tests/ImportTelemetryTests.cs` — replace lines 131-132:

```csharp
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
            StartActivity(name ?? "Test.Activity.Root", ActivityKind.Server, startTime);
```

and line 149:

```csharp
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) => null;
```

`tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs` — replace lines 252-253:

```csharp
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
            StartActivity(name ?? "Test.Activity.Root", ActivityKind.Server, startTime);
```

and line 270:

```csharp
        public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) => null;
```

- [ ] **Step 6: Run the tests to verify they pass**

```
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
```

Expected: PASS. No behaviour changed for existing callers — `name` defaults to null.

- [ ] **Step 7: Commit**

```bash
git add src/Core/Core.Abstractions/ITelemetryService.cs src/Core/Core.Telemetry/TelemetryService.cs tests/Common/Stubs/StubTelemetryService.cs tests/Cmdlets.Tests/ImportTelemetryTests.cs tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs tests/Core.Telemetry.Tests/TelemetryServiceStartTimeTests.cs
git commit -m "feat(telemetry): allow StartRootActivity to override the span name"
```

---

### Task 2: Telemetry vocabulary — tags and allowed span names

**Files:**
- Modify: `src/Core/Core.Abstractions/TelemetryTags.cs`
- Modify: `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs:13-83`
- Test: `tests/Core.Telemetry.Tests/RecoveryTelemetryVocabularyTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `TelemetryTags.CommandTraceId`, `.CommandSpanId`, `.RecoverySessionStyle`, `.RecoveryProvider`, `.RecoveryExitCode`, `.RecoveryTurns`, `.AgentModel`, `.ProcessName` — all `string` constants prefixed with `properties.`. Allowlist entries for `CLI.Activity.AiRecovery`, `Agent.Session`, `Agent.Create`, `Agent.Turn`, `Agent.Send`, and the `tool.` prefix. Tasks 4, 5, and 6 all depend on these; without them their spans are created and then silently dropped by `DefaultActivityNameFilterProcessor`.

- [ ] **Step 1: Write the failing test**

Create `tests/Core.Telemetry.Tests/RecoveryTelemetryVocabularyTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Telemetry.Tests;

/// <summary>
/// The AI error-recovery spans (Azure/CLIPS#602) are dropped without a trace if their
/// names are missing from the activity-name allowlist, and their tags are stripped if
/// they fall outside the property allowlist. Both failures are silent, so they are
/// pinned here rather than discovered in the portal.
/// </summary>
public class RecoveryTelemetryVocabularyTests
{
    private readonly DefaultTelemetryActivityNameFilter _names = new();
    private readonly DefaultTelemetryPropertyFilter _properties = new();

    [Theory]
    [InlineData("CLI.Activity.AiRecovery")]
    [InlineData("Agent.Session")]
    [InlineData("Agent.Create")]
    [InlineData("Agent.Turn")]
    [InlineData("Agent.Send")]
    [InlineData("tool.get_command")]
    [InlineData("tool.bash")]
    public void IsAllowed_AdmitsRecoverySpanNames(string name) =>
        Assert.True(_names.IsAllowed(name));

    [Theory]
    [InlineData("Agent.NotARealSpan")]
    [InlineData("toolbox.something")]
    public void IsAllowed_StillRejectsUnknownNames(string name) =>
        Assert.False(_names.IsAllowed(name));

    [Theory]
    [InlineData(nameof(TelemetryTags.CommandTraceId))]
    [InlineData(nameof(TelemetryTags.CommandSpanId))]
    [InlineData(nameof(TelemetryTags.RecoverySessionStyle))]
    [InlineData(nameof(TelemetryTags.RecoveryProvider))]
    [InlineData(nameof(TelemetryTags.RecoveryExitCode))]
    [InlineData(nameof(TelemetryTags.RecoveryTurns))]
    [InlineData(nameof(TelemetryTags.AgentModel))]
    [InlineData(nameof(TelemetryTags.ProcessName))]
    public void RecoveryTags_AreNamedForTheProductPropertyChannel(string constantName)
    {
        var value = (string)typeof(TelemetryTags).GetField(constantName)!.GetValue(null)!;

        Assert.StartsWith(TelemetryTags.PropertyTagPrefix, value, StringComparison.Ordinal);
        Assert.True(_properties.IsAllowed(value));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~RecoveryTelemetryVocabulary"
```

Expected: compile error — `TelemetryTags.CommandTraceId` does not exist.

- [ ] **Step 3: Add the tag constants**

In `src/Core/Core.Abstractions/TelemetryTags.cs`, insert before the closing brace:

```csharp
    // AI error-recovery (Azure/CLIPS#602). All live under PropertyTagPrefix, which
    // DefaultTelemetryPropertyFilter admits as the vetted product channel — no
    // allowlist edit required. Names, counts, and outcomes only: never prompts,
    // error text, or model output.
    public const string CommandTraceId = PropertyTagPrefix + "command.traceId";
    public const string CommandSpanId = PropertyTagPrefix + "command.spanId";
    public const string RecoverySessionStyle = PropertyTagPrefix + "recovery.sessionStyle";
    public const string RecoveryProvider = PropertyTagPrefix + "recovery.provider";
    public const string RecoveryExitCode = PropertyTagPrefix + "recovery.exitCode";
    public const string RecoveryTurns = PropertyTagPrefix + "recovery.turns";
    public const string AgentModel = PropertyTagPrefix + "agent.model";
    public const string ProcessName = PropertyTagPrefix + "process";
```

- [ ] **Step 4: Add the span names to the allowlist**

In `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs`, add to `AllowedExactNames` after the `"TUI.Activity.Root"` entry:

```csharp
        // Second root opened by the CLI after the command root closes, wrapping the
        // AI error-recovery subprocess. Its own trace, so the command's duration
        // stays honest (Azure/CLIPS#602).
        "CLI.Activity.AiRecovery",

        // Spans emitted inside azclips-agent.exe, parented to CLI.Activity.AiRecovery
        // through the traceparent handed down on the child environment block.
        "Agent.Session",
        "Agent.Create",
        "Agent.Turn",
        "Agent.Send",
```

and to `AllowedNamePrefixes`, after the `"AzureCLI.Startup."` entry:

```csharp
        // Per-tool spans from ToolInvocationLog.Scope: $"tool.{name}".
        "tool.",
```

- [ ] **Step 5: Run the tests to verify they pass**

```
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Core/Core.Abstractions/TelemetryTags.cs src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs tests/Core.Telemetry.Tests/RecoveryTelemetryVocabularyTests.cs
git commit -m "feat(telemetry): add AI error-recovery tag constants and span-name allowlist entries"
```

---

### Task 3: Stop the command root before AI recovery runs

This is the fix for Azure/CLIPS#602. It is worth shipping on its own.

**Files:**
- Modify: `src/CLI/Program.cs:164-187` (the two catch blocks), and add a helper above `TryAgentFallbackAsync` at `:314`
- Test: `tests/Cli.Tests/AgentFallbackOrderingTests.cs` (create)

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static Task<int> Core.Cli.Program.StopRootThenFallbackAsync(Activity? root, Func<string?, string?, Task<int>> fallback)`. Reads the root's `TraceId` and `SpanId`, disposes the root, then invokes `fallback(traceId, spanId)` and returns its result. Both identifiers are `null` when `root` is `null`. Task 4 changes `TryAgentFallbackAsync` to accept those two identifiers.

Today `TryAgentFallbackAsync` is awaited inside the `catch`, and the root is disposed only in the `finally`. `TryAgentFallbackAsync` blocks on `proc.WaitForExitAsync()` for the whole AI session, so all of it is recorded as command execution time. `Core.Cli` already grants `InternalsVisibleTo Cli.Tests` (`src/CLI/Core.Cli.csproj:22,25`), so an `internal static` helper is directly testable — the same seam `BuildTelemetryUploaderStartInfo` / `TelemetryUploaderSpawnTests` uses.

- [ ] **Step 1: Write the failing test**

Create `tests/Cli.Tests/AgentFallbackOrderingTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Regression coverage for Azure/CLIPS#602. AI error recovery blocks on a subprocess
/// for as long as the model takes. If the command root span is still open while that
/// happens, the wait is billed to the command's duration — a `profile show` that failed
/// instantly was reported at 37.8 seconds. The root must be stopped first.
/// </summary>
public class AgentFallbackOrderingTests
{
    [Fact]
    public async Task StopRootThenFallbackAsync_StopsTheRoot_BeforeRunningTheFallback()
    {
        var order = new List<string>();
        using var source = new ActivitySource("cli-fallback-order-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _ => order.Add("root-stopped"),
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);

        await Core.Cli.Program.StopRootThenFallbackAsync(root, (_, _) =>
        {
            order.Add("fallback-ran");
            return Task.FromResult(1);
        });

        Assert.Equal(new[] { "root-stopped", "fallback-ran" }, order);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_HandsTheFallbackTheRootIdentifiers()
    {
        using var source = new ActivitySource("cli-fallback-ids-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
        Assert.NotNull(root);
        var expectedTrace = root.TraceId.ToString();
        var expectedSpan = root.SpanId.ToString();

        string? seenTrace = null;
        string? seenSpan = null;
        await Core.Cli.Program.StopRootThenFallbackAsync(root, (traceId, spanId) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(1);
        });

        Assert.Equal(expectedTrace, seenTrace);
        Assert.Equal(expectedSpan, seenSpan);
    }

    [Fact]
    public async Task StopRootThenFallbackAsync_PassesNulls_WhenTelemetryIsDisabled()
    {
        // StartRootActivity returns null when telemetry.enabled=false. The fallback
        // must still run, and must still return the agent's exit code.
        string? seenTrace = "unset";
        string? seenSpan = "unset";

        var exitCode = await Core.Cli.Program.StopRootThenFallbackAsync(null, (traceId, spanId) =>
        {
            seenTrace = traceId;
            seenSpan = spanId;
            return Task.FromResult(7);
        });

        Assert.Null(seenTrace);
        Assert.Null(seenSpan);
        Assert.Equal(7, exitCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~AgentFallbackOrdering"
```

Expected: compile error — `StopRootThenFallbackAsync` does not exist.

- [ ] **Step 3: Add the helper**

In `src/CLI/Program.cs`, insert immediately above `private static async Task<int> TryAgentFallbackAsync(`:

```csharp
    /// <summary>
    /// Stops the command root span, then runs <paramref name="fallback"/> with the stopped
    /// root's identifiers.
    /// </summary>
    /// <remarks>
    /// The ordering is load-bearing. AI error recovery spawns azclips-agent.exe and blocks on
    /// it for as long as the model takes. Anything still inside the root span is recorded as
    /// command execution time, which is how a `profile show` that failed instantly reported a
    /// 37.8 second duration (Azure/CLIPS#602). Disposing here is idempotent with the
    /// <c>finally</c> in <see cref="Main"/> — Activity.Dispose only calls Stop when the
    /// activity is not already stopped.
    /// </remarks>
    internal static async Task<int> StopRootThenFallbackAsync(
        Activity? root,
        Func<string?, string?, Task<int>> fallback)
    {
        // Read before disposing: the identifiers stay valid afterwards, but reading first
        // keeps the dependency on Activity's post-stop behaviour out of the picture.
        var traceId = root?.TraceId.ToString();
        var spanId = root?.SpanId.ToString();

        root?.Dispose();

        return await fallback(traceId, spanId);
    }
```

- [ ] **Step 4: Route both catch blocks through it**

In `src/CLI/Program.cs`, replace the `HttpRequestException` catch:

```csharp
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            return await StopRootThenFallbackAsync(
                activity, (_, _) => TryAgentFallbackAsync(positional, ex.Message));
        }
```

and the general `Exception` catch:

```csharp
        catch (Exception ex)
        {
            _logger?.Error(ex.Message);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);

            return await StopRootThenFallbackAsync(
                activity, (_, _) => TryAgentFallbackAsync(positional, ex.Message));
        }
```

The two identifiers are discarded for now; Task 4 wires them into `TryAgentFallbackAsync`. Leave the `finally` block and the `AzclipsErrorException` catch untouched — that path never reaches AI recovery.

- [ ] **Step 5: Run the tests to verify they pass**

```
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~AgentFallbackOrdering"
dotnet build azclips.slnx
```

Expected: PASS, clean build.

- [ ] **Step 6: Commit**

```bash
git add src/CLI/Program.cs tests/Cli.Tests/AgentFallbackOrderingTests.cs
git commit -m "fix(telemetry): stop the command root before AI error recovery blocks

The root activity was only disposed in Main's finally, after TryAgentFallbackAsync
had already blocked on the agent subprocess, so the AI session duration was recorded
as command execution time. Fixes Azure/CLIPS#602."
```

---

### Task 4: Emit `CLI.Activity.AiRecovery` and hand the traceparent to the agent

**Files:**
- Modify: `src/CLI/Program.cs:25-34` (constants), `:314-439` (`TryAgentFallbackAsync`), and the two catch blocks from Task 3
- Test: `tests/Cli.Tests/AgentStartInfoTests.cs` (create)

**Interfaces:**
- Consumes: `Program.StopRootThenFallbackAsync` (Task 3); `TelemetryTags.CommandTraceId` / `.CommandSpanId` / `.RecoverySessionStyle` / `.RecoveryProvider` / `.RecoveryExitCode` (Task 2); `ITelemetryService.StartRootActivity(name:)` (Task 1).
- Produces: `internal static ProcessStartInfo Core.Cli.Program.BuildAgentStartInfo(string agentExe, string[] args, string? errorText, string? recoveryTraceparent)` and `internal const string Core.Cli.Program.RecoveryTraceparentEnvVar = "AZCLIPS_RECOVERY_TRACEPARENT"`. Task 5 reads that variable in the agent process.

- [ ] **Step 1: Write the failing test**

Create `tests/Cli.Tests/AgentStartInfoTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Cli.Tests;

/// <summary>
/// The agent runs in its own process, so the only way its spans can join the
/// CLI.Activity.AiRecovery trace is a W3C traceparent handed down on the child
/// environment block (Azure/CLIPS#602).
/// </summary>
public class AgentStartInfoTests
{
    private const string Traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private static string AgentExe => Path.Combine(AppContext.BaseDirectory, "azclips-agent.exe");

    [Fact]
    public void BuildAgentStartInfo_ForwardsTheRecoveryTraceparent()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", Traceparent);

        Assert.Equal(Traceparent, psi.Environment[Core.Cli.Program.RecoveryTraceparentEnvVar]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildAgentStartInfo_OmitsTheTraceparent_WhenTelemetryProducedNoSpan(string? traceparent)
    {
        // StartRootActivity returns null when telemetry.enabled=false, so Id is null.
        // An empty value would be parsed by the agent and rejected; absent is cleaner.
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", traceparent);

        Assert.False(psi.Environment.ContainsKey(Core.Cli.Program.RecoveryTraceparentEnvVar));
    }

    [Fact]
    public void BuildAgentStartInfo_KeepsTheExistingSpawnShape()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, "boom", Traceparent);

        // Recursion guard: a failed `az` invoked from inside the agent must not spawn another.
        Assert.Equal("1", psi.Environment["AZCLIPS_IN_AGENT_MODE"]);
        // Environment overrides only apply when the process is not shell-executed.
        Assert.False(psi.UseShellExecute);
        // Stdio is inherited so streaming tokens and the y/n prompt reach the user's terminal.
        Assert.False(psi.RedirectStandardOutput);
        Assert.False(psi.RedirectStandardError);
        Assert.False(psi.RedirectStandardInput);
        Assert.Equal(AgentExe, psi.FileName);
        Assert.Equal(
            new[] { "--agent-mode", "--failed-command", "profile show", "--error", "boom" },
            psi.ArgumentList);
    }

    [Fact]
    public void BuildAgentStartInfo_OmitsTheErrorFlag_WhenThereIsNoErrorText()
    {
        var psi = Core.Cli.Program.BuildAgentStartInfo(
            AgentExe, new[] { "profile", "show" }, null, Traceparent);

        Assert.Equal(new[] { "--agent-mode", "--failed-command", "profile show" }, psi.ArgumentList);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~AgentStartInfo"
```

Expected: compile error — `BuildAgentStartInfo` and `RecoveryTraceparentEnvVar` do not exist.

- [ ] **Step 3: Add the constants**

In `src/CLI/Program.cs`, after the `CallerCwdEnvVar` declaration:

```csharp
    /// <summary>
    /// W3C traceparent of the CLI.Activity.AiRecovery span, read by the agent so its spans
    /// join that trace. Namespaced rather than the bare TRACEPARENT so a value set by a
    /// surrounding CI system cannot silently reparent the agent (Azure/CLIPS#602).
    /// Read on the agent side by the same literal name — rename both together.
    /// </summary>
    internal const string RecoveryTraceparentEnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";

    /// <summary>Span name of the second root, opened once the command root has closed.</summary>
    private const string RecoveryActivityName = "CLI.Activity.AiRecovery";
```

- [ ] **Step 4: Extract the start-info builder**

In `src/CLI/Program.cs`, add above `JoinShellQuoted`:

```csharp
    /// <summary>
    /// Builds the spawn shape for azclips-agent.exe. Split out from
    /// <see cref="TryAgentFallbackAsync"/> so the environment block can be asserted without
    /// starting a process — the same seam <c>BuildTelemetryUploaderStartInfo</c> uses.
    /// </summary>
    internal static ProcessStartInfo BuildAgentStartInfo(
        string agentExe,
        string[] args,
        string? errorText,
        string? recoveryTraceparent)
    {
        var psi = new ProcessStartInfo
        {
            FileName = agentExe,
            // Set the working directory to the agent exe's own directory so relative
            // additionalProbingPaths in azclips-agent.runtimeconfig.json resolve correctly.
            // .NET resolves those at CLR startup, before any managed code runs, relative to
            // the working directory rather than the exe location — so it must be set here
            // and cannot be corrected from inside the agent. The user's real cwd is
            // forwarded via AZCLIPS_CALLER_CWD below.
            WorkingDirectory = Path.GetDirectoryName(agentExe)!,
            // Inherit stdio so streaming tokens, the y/n prompt, and device-code links all
            // show live in the user's terminal.
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            RedirectStandardInput = false,
        };

        psi.Environment[AgentModeEnvVar] = "1";
        // Best-effort: GetCurrentDirectory can throw (e.g. the cwd was deleted), and
        // forwarding it is only a hint — skip it rather than fail the launch.
        try { psi.Environment[CallerCwdEnvVar] = Directory.GetCurrentDirectory(); }
        catch { /* leave CallerCwdEnvVar unset; the agent falls back to its own cwd */ }

        // Null when telemetry is disabled and no recovery span was opened. Leave the
        // variable absent in that case rather than setting an empty string.
        if (!string.IsNullOrEmpty(recoveryTraceparent))
            psi.Environment[RecoveryTraceparentEnvVar] = recoveryTraceparent;

        psi.ArgumentList.Add("--agent-mode");
        psi.ArgumentList.Add("--failed-command");
        psi.ArgumentList.Add(JoinShellQuoted(args));
        if (!string.IsNullOrWhiteSpace(errorText))
        {
            psi.ArgumentList.Add("--error");
            psi.ArgumentList.Add(errorText);
        }

        return psi;
    }
```

- [ ] **Step 5: Open the recovery span in `TryAgentFallbackAsync`**

Change the signature to take the command identifiers:

```csharp
    private static async Task<int> TryAgentFallbackAsync(
        string[] args, string? errorText, string? commandTraceId, string? commandSpanId)
```

Leave the four guard blocks exactly as they are — the recovery span must not be opened when no agent runs. Then replace everything from `try` through the closing `catch` (the block that built and started the process) with:

```csharp
        // Opened only now that a spawn is certain. The command root has already been
        // stopped by StopRootThenFallbackAsync, so Activity.Current is null and this
        // becomes a second, independent root with its own TraceId (Azure/CLIPS#602).
        using var recovery = _telemetryService?.StartRootActivity(name: RecoveryActivityName);
        recovery?.SetTag(TelemetryTags.CommandTraceId, commandTraceId);
        recovery?.SetTag(TelemetryTags.CommandSpanId, commandSpanId);
        recovery?.SetTag(TelemetryTags.Command, JoinShellQuoted(args));
        recovery?.SetTag(TelemetryTags.RecoverySessionStyle, agentOptions.SessionStyle);
        recovery?.SetTag(TelemetryTags.RecoveryProvider, agentOptions.Provider);

        try
        {
            var psi = BuildAgentStartInfo(agentExe, args, errorText, recovery?.Id);

            // The child agent takes over Ctrl+C (it cancels the session). The parent must
            // also intercept it — otherwise Ctrl+C kills the parent first, the shell redraws
            // its prompt, and the child's remaining output prints onto that prompt
            // ("terminal hijack"). Intercept-only here; the child owns the actual
            // cancellation and we just wait for it to exit cleanly.
            ConsoleCancelEventHandler cancelHandler = (_, e) => e.Cancel = true;
            Console.CancelKeyPress += cancelHandler;
            try
            {
                using var proc = Process.Start(psi)
                    ?? throw new InvalidOperationException("Process.Start returned null");
                await proc.WaitForExitAsync();

                recovery?.SetTag(TelemetryTags.RecoveryExitCode, proc.ExitCode);
                recovery?.SetStatus(
                    proc.ExitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

                // Exit code semantics:
                //   one-shot — the agent only PROPOSED a fix; it never ran anything, so the
                //     user's original command still failed. Report failure (never let the
                //     agent's own 0 mask it), so `&&`, CI, and $? see the command as failed.
                //   interactive — the agent may have actually run commands and resolved the
                //     task, so its real exit code is meaningful; pass it through unchanged.
                if (agentOptions.IsInteractive)
                {
                    return proc.ExitCode;
                }
                return proc.ExitCode != 0 ? proc.ExitCode : 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"agent mode subprocess failed: {ex.Message}");
            recovery?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return 1;
        }
```

Add `using Core.Abstractions;` at the top of `Program.cs` if `TelemetryTags` is not already resolvable.

- [ ] **Step 6: Pass the identifiers through from the catch blocks**

In both catch blocks touched by Task 3, replace the discards with the real arguments:

```csharp
            return await StopRootThenFallbackAsync(
                activity,
                (traceId, spanId) => TryAgentFallbackAsync(positional, ex.Message, traceId, spanId));
```

- [ ] **Step 7: Run the tests to verify they pass**

```
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet build azclips.slnx
```

Expected: PASS, clean build.

- [ ] **Step 8: Commit**

```bash
git add src/CLI/Program.cs tests/Cli.Tests/AgentStartInfoTests.cs
git commit -m "feat(telemetry): record AI error recovery as its own linked trace

Opens CLI.Activity.AiRecovery as a second root once the command root has closed,
tags it with the command's trace and span ids, and forwards its traceparent to
azclips-agent.exe so the agent can join that trace. Refs Azure/CLIPS#602."
```

---

### Task 5: Turn on telemetry in the agent process

**Files:**
- Create: `Agent/Telemetry/AgentTelemetryProvider.cs`
- Create: `Agent/Telemetry/RecoveryTraceContext.cs`
- Modify: `Agent/Core.Agent.csproj:54-65` (add the `Core.Telemetry` project reference)
- Modify: `Agent/Tools/AgentTracing.cs`
- Modify: `Agent/AgentMode/AgentModeEntry.cs:36` and inside the `try` at `:39-118`
- Test: `Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs` (create)

**Interfaces:**
- Consumes: the env var name `"AZCLIPS_RECOVERY_TRACEPARENT"` set by Task 4; the `Agent.Session` allowlist entry and `TelemetryTags.ProcessName` / `.RecoverySessionStyle` / `.RecoveryProvider` from Task 2.
- Produces: `Core.Agent.Telemetry.AgentTelemetryProvider : DefaultTelemetryProvider` with ctor `(IEnumerable<BaseProcessor<Activity>>, ITelemetrySender, IConfigurationService)`; `Core.Agent.Telemetry.RecoveryTraceContext.FromEnvironment()` and `.Parse(string?)` both returning `ActivityContext`; `AgentTracing.SourceName` changed from `"Azclips.Agent"` to `"CLI"`. Task 6 adds child spans under `Agent.Session` using `AgentTracing.Source`.

Two constraints make this task subtle. First, `AgentTracing.Source` is a plain `ActivitySource` and does not consult `telemetry.enabled` — so switching on a listener would start exporting `tool.*` spans regardless of the user's opt-out. The gate therefore lives in the provider: when telemetry is off it leaves `TracerProvider.Default` in place and nothing records. Second, the source name must be `"CLI"` for the cross-process parent edge to survive upload; see Global Constraints.

- [ ] **Step 1: Write the failing test**

Create `Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Agent.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Core.Agent.Tests;

public class RecoveryTraceContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-traceparent")]
    [InlineData("00-tooshort-00f067aa0ba902b7-01")]
    public void Parse_ReturnsDefault_WhenAbsentOrMalformed(string? value)
    {
        // default(ActivityContext) makes the caller fall back to ambient parenting, so a
        // missing or broken handoff costs the parent edge and nothing else.
        Assert.Equal(default, RecoveryTraceContext.Parse(value));
    }

    [Fact]
    public void Parse_ExtractsTraceAndSpan_WhenWellFormed()
    {
        var ctx = RecoveryTraceContext.Parse(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", ctx.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", ctx.SpanId.ToString());
    }
}

public class AgentTelemetryProviderTests
{
    [Fact]
    public void ActivitySource_IsNamedForTheCliClient()
    {
        // Load-bearing. FileExporter writes service.name from ActivitySource.Name, and
        // TraceUploader rebuilds parent/child edges only within a service-name group. If
        // this drifts from CLITelemetryProvider, every agent span is reparented to a random
        // span id on upload and the recovery trace silently flattens.
        var provider = new AgentTelemetryProvider(
            [], new NoopSender(), new StubConfiguration(telemetryEnabled: true));

        Assert.Equal(ClientType.CLI.ToString(), provider.ActivitySource.Name);
    }

    [Fact]
    public void TracerProvider_DoesNotListen_WhenTelemetryIsDisabled()
    {
        var provider = new AgentTelemetryProvider(
            [], new NoopSender(), new StubConfiguration(telemetryEnabled: false));

        // AgentTracing.Source bypasses TelemetryService's per-call gate, so the opt-out has
        // to be enforced by not building a listener at all.
        using var activity = provider.ActivitySource.StartActivity("Agent.Session");
        Assert.Null(activity);
    }

    private sealed class NoopSender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder) { }
    }

    /// <summary>
    /// IConfigurationService stub whose GetConfiguration&lt;TelemetryOptions&gt;() reports a
    /// fixed Enabled value. Mirrors ToggleableConfigurationService in
    /// tests/Core.Telemetry.Tests/TelemetryServiceEnabledGateTests.cs.
    /// </summary>
    private sealed class StubConfiguration : IConfigurationService
    {
        private readonly bool _telemetryEnabled;

        public StubConfiguration(bool telemetryEnabled) => _telemetryEnabled = telemetryEnabled;

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(TelemetryOptions))
            {
                return (T)(object)new TelemetryOptions { Enabled = _telemetryEnabled };
            }
            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public string GetConfigFilePath() => throw new NotSupportedException();
        public IConfigurationSchema GetSchema() => throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```
dotnet test Agent/tests/Core.Agent.Tests/Core.Agent.Tests.csproj --filter "FullyQualifiedName~RecoveryTraceContext"
```

Expected: compile error — the namespace `Core.Agent.Telemetry` does not exist.

- [ ] **Step 3: Add the project reference**

In `Agent/Core.Agent.csproj`, inside the `ItemGroup` holding the project references, after the `Core.DependencyInjection` entry:

```xml
    <!-- AgentTelemetryProvider derives from DefaultTelemetryProvider and reuses
         FileTelemetrySender. This flows transitively through Core.DependencyInjection,
         but the dependency is direct, so declare it. -->
    <ProjectReference Include="..\src\Core\Core.Telemetry\Core.Telemetry.csproj" />
```

- [ ] **Step 4: Write `RecoveryTraceContext`**

Create `Agent/Telemetry/RecoveryTraceContext.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Agent.Telemetry;

/// <summary>
/// Reads the W3C traceparent the CLI puts on this process's environment block so the
/// agent's spans join the CLI.Activity.AiRecovery trace instead of starting an unrelated
/// one (Azure/CLIPS#602).
/// </summary>
/// <remarks>
/// The variable is namespaced rather than the bare TRACEPARENT: a surrounding CI system
/// commonly sets TRACEPARENT, and inheriting it would silently reparent the agent under
/// a pipeline's trace.
/// </remarks>
internal static class RecoveryTraceContext
{
    /// <summary>Must match <c>Core.Cli.Program.RecoveryTraceparentEnvVar</c>.</summary>
    internal const string EnvVar = "AZCLIPS_RECOVERY_TRACEPARENT";

    internal static ActivityContext FromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary>
    /// Returns <c>default(ActivityContext)</c> when the value is absent or malformed, which
    /// makes the caller fall back to ambient parenting — the agent then starts its own root
    /// and only the link to the recovery trace is lost.
    /// </summary>
    internal static ActivityContext Parse(string? traceparent) =>
        !string.IsNullOrWhiteSpace(traceparent)
        && ActivityContext.TryParse(traceparent, traceState: null, out var context)
            ? context
            : default;
}
```

If the test project cannot see `internal` members, confirm `Agent/Core.Agent.csproj` still
carries `<InternalsVisibleTo Include="Core.Agent.Tests" />` (it does, at lines 80-85).

- [ ] **Step 5: Write `AgentTelemetryProvider`**

Create `Agent/Telemetry/AgentTelemetryProvider.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Agent.Telemetry;

/// <summary>
/// Telemetry pipeline for azclips-agent.exe. Deliberately mirrors
/// <c>Core.Cli.Telemetry.CLITelemetryProvider</c> rather than sharing it: Core.Agent does
/// not reference Core.Cli, and promoting that type into Core.Telemetry would drag the
/// PowerShell and TUI providers' shape along with it.
/// </summary>
public sealed class AgentTelemetryProvider : DefaultTelemetryProvider
{
    public AgentTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    {
        // The name MUST stay in lockstep with CLITelemetryProvider. FileExporter writes
        // service.name from ActivitySource.Name, and TraceUploader rebuilds parent/child
        // edges only within a service-name group — so a different name here would silently
        // reparent every agent span to a random span id on upload, flattening the recovery
        // trace (Azure/CLIPS#602).
        var sourceName = ClientType.CLI.ToString();
        ActivitySource = new ActivitySource(sourceName);

        // Unlike the CLI, where every span goes through ITelemetryService and is gated
        // per call, the agent emits from AgentTracing.Source — a plain ActivitySource that
        // does not consult telemetry.enabled. Gating listener construction is what makes the
        // opt-out cover every agent span, tool.* included. Deciding once per process is fine
        // here: azclips-agent.exe is short-lived and single-shot.
        if (!configuration.GetConfiguration<TelemetryOptions>().Enabled)
        {
            TracerProvider = TracerProvider.Default;
            return;
        }

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .ConfigureResource(r => r.AddService(ProcessUtils.GetRunningProcess()));

        foreach (var processor in filterProcessors)
        {
            builder.AddProcessor(processor);
        }

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build();
    }
}
```

- [ ] **Step 6: Repoint `AgentTracing`**

Replace the body of `Agent/Tools/AgentTracing.cs`:

```csharp
using System.Diagnostics;

namespace Core.Agent;

/// <summary>Shared <see cref="ActivitySource"/> for everything in <c>Core.Agent</c>.</summary>
/// <remarks>
/// The name is "CLI", not an agent-specific name, and must stay that way. FileExporter
/// writes service.name from ActivitySource.Name and TraceUploader rebuilds parent/child
/// edges only within a service-name group, so an agent-specific name would detach every
/// span here from the CLI.Activity.AiRecovery parent on upload (Azure/CLIPS#602). Spans are
/// still distinguishable by their Agent.* / tool.* name prefixes. Matching by name is
/// enough — AddSource("CLI") listens to every ActivitySource with that name, not just the
/// provider's own instance.
/// </remarks>
public static class AgentTracing
{
    public const string SourceName = "CLI";

    public static readonly ActivitySource Source = new(SourceName);
}
```

- [ ] **Step 7: Wire the provider into `AgentModeEntry` and open `Agent.Session`**

In `Agent/AgentMode/AgentModeEntry.cs`, replace the `AzclipsServiceProvider.Initialize(ClientType.CLI);` line and its comment:

```csharp
        // Agent runs as its own process (azclips-agent.exe), spawned by the CLI as its
        // error-handling fallback — so it initializes as ClientType.CLI.
        // Telemetry is registered explicitly: the default registration resolves
        // DefaultTelemetryProvider, whose TracerProvider.Default has no listener, so every
        // span in this process would silently return null (Azure/CLIPS#602).
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            services.AddSingleton<ITelemetryProvider, AgentTelemetryProvider>();
        });
        AzclipsServiceProvider.Initialize(ClientType.CLI);
```

Add `using Core.Agent.Telemetry;` and `using Core.Telemetry;` to the file's usings.

Then add as the first statements inside the `try`, before `var auth = ...`:

```csharp
            // Resolving ITelemetryService is what constructs AgentTelemetryProvider, builds
            // the TracerProvider, and starts the listener; DI singletons are lazy, so without
            // this every StartActivity below returns null. The container disposes it in the
            // finally, which flushes the exporter.
            _ = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();

            // Parented to CLI.Activity.AiRecovery in the CLI process via the traceparent on
            // our environment block. Declared inside the try so it is disposed before the
            // finally tears down the TracerProvider — nothing exports after that.
            using var session = AgentTracing.Source.StartActivity(
                "Agent.Session", ActivityKind.Server, RecoveryTraceContext.FromEnvironment());
            session?.SetTag(TelemetryTags.ProcessName, "azclips-agent");
```

Immediately after `var agentOpts = config.GetConfiguration<AgentOptions>();`:

```csharp
            session?.SetTag(TelemetryTags.RecoverySessionStyle, agentOpts.SessionStyle);
            session?.SetTag(TelemetryTags.RecoveryProvider, agentOpts.Provider);
```

And immediately before the `return 0;` at the end of the `try`:

```csharp
            session?.SetStatus(ActivityStatusCode.Ok);
```

- [ ] **Step 8: Run the tests to verify they pass**

```
dotnet test Agent/tests/Core.Agent.Tests/Core.Agent.Tests.csproj
dotnet build azclips.slnx
```

Expected: PASS, clean build.

- [ ] **Step 9: Commit**

```bash
git add Agent/Telemetry/ Agent/Tools/AgentTracing.cs Agent/AgentMode/AgentModeEntry.cs Agent/Core.Agent.csproj Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs
git commit -m "feat(agent): emit Agent.Session into the CLI's AI-recovery trace

azclips-agent.exe registered no telemetry provider, so every span it created
returned null. Registers one gated on telemetry.enabled and attaches the session
span to the traceparent the CLI hands down. Refs Azure/CLIPS#602."
```

---

### Task 6: Break down where the agent spends its time

**Files:**
- Modify: `Agent/AgentMode/AgentModeEntry.cs:92-117` (`Agent.Create`), `:226-256` (`Agent.Turn`)
- Modify: `Agent/ChatAgent.cs:357-377` (`Agent.Send`)
- Test: `Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs` (extend)

**Interfaces:**
- Consumes: `AgentTracing.Source` and `RecoveryTraceContext` (Task 5); the `Agent.Create` / `Agent.Turn` / `Agent.Send` allowlist entries and `TelemetryTags.RecoveryTurns` / `.AgentModel` (Task 2).
- Produces: no new public API. Adds three spans nested under `Agent.Session`.

`Agent.Session` alone answers "how long did recovery take", which the CLI's recovery span already covers. These three answer "where did it go" — the reason for instrumenting the agent at all. `Agent.Send` sits inside `ChatAgent.SendAsync` rather than around the caller because `SendAndPumpAsync` races the send against the approval pump, and wrapping the caller would fold user think time into model latency.

- [ ] **Step 1: Write the failing test**

Append to `Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs`:

```csharp
public class AgentSpanNestingTests
{
    [Fact]
    public void ChildSpans_NestUnderTheSessionSpan()
    {
        // Proves the ambient Activity.Current chain the agent relies on: spans started
        // inside the session's scope become its children without explicit plumbing, and
        // the whole subtree inherits the CLI's recovery trace id.
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == AgentTracing.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = stopped.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var parent = RecoveryTraceContext.Parse(
            "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        using (var sessionSpan = AgentTracing.Source.StartActivity(
                   "Agent.Session", ActivityKind.Server, parent))
        {
            Assert.NotNull(sessionSpan);
            using (AgentTracing.Source.StartActivity("Agent.Create")) { }
            using (var turnSpan = AgentTracing.Source.StartActivity("Agent.Turn"))
            {
                Assert.NotNull(turnSpan);
                using (AgentTracing.Source.StartActivity("Agent.Send")) { }
            }
        }

        var session = stopped.Single(a => a.OperationName == "Agent.Session");
        var create = stopped.Single(a => a.OperationName == "Agent.Create");
        var turn = stopped.Single(a => a.OperationName == "Agent.Turn");
        var send = stopped.Single(a => a.OperationName == "Agent.Send");

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", session.TraceId.ToString());
        Assert.Equal("00f067aa0ba902b7", session.ParentSpanId.ToString());
        Assert.Equal(session.TraceId, send.TraceId);

        Assert.Equal(session.SpanId, create.ParentSpanId);
        Assert.Equal(session.SpanId, turn.ParentSpanId);
        Assert.Equal(turn.SpanId, send.ParentSpanId);
    }
}
```

- [ ] **Step 2: Run the test**

```
dotnet test Agent/tests/Core.Agent.Tests/Core.Agent.Tests.csproj --filter "FullyQualifiedName~AgentSpanNesting"
```

Expected: PASS. This test documents the nesting contract the next three steps depend on;
it exercises `AgentTracing.Source` directly rather than driving `AgentModeEntry`, which
would require a live Copilot SDK session. If it fails, the source name or the parent
parsing from Task 5 is wrong — fix that before continuing.

- [ ] **Step 3: Wrap `ChatAgent.CreateAsync`**

In `Agent/AgentMode/AgentModeEntry.cs`, replace the `await using var agent = await ChatAgent.CreateAsync(...)` statement with:

```csharp
            ChatAgent agent;
            // Bootstraps the Copilot SDK and its stdio daemon — historically a large share
            // of total recovery latency, so it gets its own span.
            using (AgentTracing.Source.StartActivity("Agent.Create"))
            {
                agent = await ChatAgent.CreateAsync(
                    flavor: flavor,
                    auth: auth,
                    profileService: profileService,
                    azExePath: az,
                    approvalSink: approval,
                    renderer: renderer,
                    options: agentOpts,
                    entrypoint: entrypoint,
                    spinner: spinner,
                    failedCommand: failedCommand,
                    ct: cts.Token);
            }
            await using var agentScope = agent;
```

The existing `agentRef = agent;` line and everything after it stay as they are.

- [ ] **Step 4: Wrap the turn loop**

In `SendWithToolEnforcementAsync`, add as the first statement of the method body:

```csharp
        // One user<->agent exchange, including any tool-enforcement re-prompts below.
        using var turn = AgentTracing.Source.StartActivity("Agent.Turn");
```

and immediately before the closing `return redirect;`:

```csharp
        turn?.SetTag(TelemetryTags.RecoveryTurns, 1);
```

Add `using Core.Abstractions;` to the file's usings if `TelemetryTags` is not resolvable.

- [ ] **Step 5: Wrap the model round trip**

In `Agent/ChatAgent.cs`, in `SendAsync`, add after the `_activeTurnCts` exchange and before the `try`:

```csharp
        // The model round trip itself, tools included. Deliberately not wrapped around the
        // caller in AgentModeEntry: SendAndPumpAsync races this against the approval pump,
        // and wrapping there would fold user think time into model latency.
        using var send = AgentTracing.Source.StartActivity("Agent.Send");
        send?.SetTag(TelemetryTags.AgentModel, _model);
```

Add `using System.Diagnostics;` and `using Core.Abstractions;` to `ChatAgent.cs` if not already present.

- [ ] **Step 6: Run the tests to verify they pass**

```
dotnet test Agent/tests/Core.Agent.Tests/Core.Agent.Tests.csproj
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
dotnet build azclips.slnx
```

Expected: PASS, clean build. `tool.*` spans now record too — `ToolInvocationLog.Scope` already started them on `AgentTracing.Source`; Task 5 gave that source a listener and Task 2 allowlisted the prefix.

- [ ] **Step 7: Commit**

```bash
git add Agent/AgentMode/AgentModeEntry.cs Agent/ChatAgent.cs Agent/tests/Core.Agent.Tests/AgentTelemetryTests.cs
git commit -m "feat(agent): add Create/Turn/Send spans under Agent.Session

Breaks agent latency down into SDK bootstrap, per-turn cost, and model round trip
so the recovery trace shows where the time went. Refs Azure/CLIPS#602."
```

---

### Task 7: Pin the cross-process relink

**Files:**
- Test: `tests/Core.Telemetry.Tests/TraceReplayerTests.cs` (extend)

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing. Pure regression coverage for the invariant Tasks 5 and 6 rest on.

The CLI writes `CLI.Activity.AiRecovery` into its own trace file and the agent writes `Agent.Session` into another. They only end up as parent and child in App Insights because `TraceUploader` flattens both files into one replay batch and `TraceReplayer` remaps the parent span ID through `spanIdMap`. `TraceUploaderTests.Run_FlattensEntriesAcrossFilesInTheSameGroup` already covers the flattening; this covers the remap, which is the half that silently degrades to `ActivitySpanId.CreateRandom()`.

- [ ] **Step 1: Write the test**

Open `tests/Core.Telemetry.Tests/TraceReplayerTests.cs` and check for existing helpers that
build a `TraceEntry` and capture replayed activities. Append the test, adapting it to those
helpers if they exist:

```csharp
    [Fact]
    public void ReplayEntries_LinksChildToParent_WhenTheyCameFromDifferentFiles()
    {
        // The CLI writes CLI.Activity.AiRecovery to traces-{T1}.log and the agent writes
        // Agent.Session to traces-{T2}.log. TraceUploader flattens both into one batch;
        // this is the remap that turns them back into parent and child. Without it the
        // replayer falls through to ActivitySpanId.CreateRandom() and the agent's subtree
        // detaches from the recovery span with no error (Azure/CLIPS#602).
        const string traceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        const string recoverySpanId = "00f067aa0ba902b7";

        var entries = new[]
        {
            // From the CLI's file.
            NewEntry(traceId, spanId: recoverySpanId, parentSpanId: "0000000000000000",
                     name: "CLI.Activity.AiRecovery"),
            // From the agent's file, naming the CLI span as its parent.
            NewEntry(traceId, spanId: "a1b2c3d4e5f60718", parentSpanId: recoverySpanId,
                     name: "Agent.Session"),
        };

        var replayed = Replay(entries);

        var recovery = replayed.Single(a => a.OperationName == "CLI.Activity.AiRecovery");
        var session = replayed.Single(a => a.OperationName == "Agent.Session");

        Assert.Equal(traceId, session.TraceId.ToString());
        Assert.Equal(recovery.SpanId, session.ParentSpanId);
    }
```

If the file has no such helpers, add these to it:

```csharp
    private static TraceEntry NewEntry(
        string traceId, string spanId, string parentSpanId, string name) =>
        new()
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            OperationName = name,
            DisplayName = name,
            StartTime = DateTimeOffset.UtcNow.ToString("O"),
            Duration = 1,
            Status = "Ok",
            Kind = "Server",
        };

    private static List<Activity> Replay(IEnumerable<TraceEntry> entries)
    {
        var replayed = new List<Activity>();
        using var source = new ActivitySource("replay-test-" + Guid.NewGuid().ToString("N"));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = replayed.Add,
        };
        ActivitySource.AddActivityListener(listener);

        TraceReplayer.ReplayEntries(source, entries);
        return replayed;
    }
```

- [ ] **Step 2: Run the test**

```
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~ReplayEntries_LinksChildToParent"
```

Expected: PASS. `TraceReplayer.TopologicallyOrder` already orders parents before children and `spanIdMap` already remaps. This test exists to keep it that way — if it ever fails, Tasks 5 and 6 are producing detached spans in production.

- [ ] **Step 3: Commit**

```bash
git add tests/Core.Telemetry.Tests/TraceReplayerTests.cs
git commit -m "test(telemetry): pin cross-file parent relink used by the AI-recovery trace"
```

---

### Task 8: Correct the documentation

**Files:**
- Modify: `docs/azure-cli/docs-conceptual/ai-based-error-recovery.md:175-188`
- Modify: `eng-docs/spec/telemetry.md:69-85`

**Interfaces:**
- Consumes: the tag names from Task 2.
- Produces: nothing.

The published FAQ states the feature collects no telemetry. After Task 5 that is false, and it is user-facing privacy copy.

- [ ] **Step 1: Rewrite the data-collection FAQ answer**

In `docs/azure-cli/docs-conceptual/ai-based-error-recovery.md`, under `### What data is used and collected?`, replace the sentence *"During the private preview, AI-based error recovery doesn't collect telemetry."* with:

````markdown
AI-based error recovery emits usage telemetry as its own trace, separate from the
telemetry for the command that failed. The trace records:

- How long recovery ran, and how long each phase took
- How many conversation turns and tool calls occurred
- The configured AI provider and interaction style
- The model name and the exit code
- A reference to the failed command's trace, so the two can be correlated

It does **not** record your prompts, the error text, the model's response, or the values
of any command parameters.

To turn all of it off:

```azurecli
az config set --key telemetry.enabled --value false
```
````

Keep the paragraph that follows about `agent.trace.enabled` and `agent.trace.path` — it is
a separate, local-only mechanism — and reword its lead-in to "Separately, if you want a
local record of the model input and output for debugging or auditing, enable tracing:".

- [ ] **Step 2: Document the new tags in the engineering spec**

In `eng-docs/spec/telemetry.md`, add below the `### Custom Tags (CLI/PS)` table:

````markdown
### AI Error-Recovery Tags

AI error recovery runs as a blocking subprocess, so it is recorded as its own trace
rather than inside the command's root span (Azure/CLIPS#602). The command trace ends when
the command fails; `CLI.Activity.AiRecovery` is a second root covering the agent, and the
agent's own spans nest under it via a W3C traceparent passed on the child environment
block.

| Tag                                | Span                        | Description                                     |
|------------------------------------|-----------------------------|-------------------------------------------------|
| `properties.command.traceId`       | `CLI.Activity.AiRecovery`   | Trace id of the command that triggered recovery |
| `properties.command.spanId`        | `CLI.Activity.AiRecovery`   | Root span id of that command                    |
| `properties.recovery.sessionStyle` | recovery / `Agent.Session`  | `oneshot` or `interactive`                      |
| `properties.recovery.provider`     | recovery / `Agent.Session`  | `builtin` or `github`                           |
| `properties.recovery.exitCode`     | `CLI.Activity.AiRecovery`   | Agent process exit code                         |
| `properties.recovery.turns`        | `Agent.Turn`                | User↔agent exchanges                            |
| `properties.agent.model`           | `Agent.Send`                | Model name                                      |
| `properties.process`               | `Agent.Session`             | Emitting process, e.g. `azclips-agent`          |

The link is one-directional. The command trace closes before the recovery trace exists, so
correlation goes recovery → command:

```kusto
requests
| where name == "CLI.Activity.AiRecovery"
| where customDimensions["properties.command.traceId"] == "<command operation_Id>"
```

The agent's `ActivitySource` is named `CLI`, matching the CLI's. `FileExporter` derives
`service.name` from `ActivitySource.Name` and `TraceUploader` rebuilds parent/child edges
only within a service-name group, so any other name would detach the agent's spans from
their parent on upload.
````

- [ ] **Step 3: Verify nothing stale remains**

```
dotnet build azclips.slnx
git --no-pager diff --stat
```

Read both files back and confirm the markdown tables are well-formed and no
"doesn't collect telemetry" sentence survives.

- [ ] **Step 4: Commit**

```bash
git add docs/azure-cli/docs-conceptual/ai-based-error-recovery.md eng-docs/spec/telemetry.md
git commit -m "docs: describe AI error-recovery telemetry and its trace separation"
```

---

## Validation

After all tasks, run the full affected set:

```
dotnet build azclips.slnx
dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
dotnet test Agent/tests/Core.Agent.Tests/Core.Agent.Tests.csproj
```

Manual check against the trace directory (default `%LOCALAPPDATA%\AzureCLI\traces\`):

1. `az config set --key agent.enabled --value true`
2. Run `az profile show` with the required parameters missing.
3. Two trace files appear. The entry named `CLI.Activity.Root` shows a sub-second duration;
   `CLI.Activity.AiRecovery` carries the long one and a `properties.command.traceId` equal
   to the root's trace id.
4. `Agent.Session` appears with the recovery span's trace id, and its `ParentSpanId` equals
   the recovery span's `SpanId`.
5. `az config set --key telemetry.enabled --value false`, repeat step 2, and confirm no new
   trace files are written by either process.

## Open follow-ups (not in scope)

- `FileExporter` pins its filename to the first exported activity's `TraceId`, so the CLI
  writes both trace ids into `traces-{first}.log`. Harmless for replay — each entry carries
  its own `TraceId` — but the filename misleads.
- Nested `az` invocations the agent runs emit their own unrelated roots. Forwarding the
  traceparent to them would fold them into the recovery trace.
- `TraceUploader` grouping by service name is why the agent must borrow the CLI's
  `ActivitySource` name. Grouping by `TraceId` instead would remove that coupling for every
  client, not just this feature.
