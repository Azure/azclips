# AI error-recovery trace separation

Issue: [Azure/CLIPS#602](https://github.com/Azure/CLIPS/issues/602)

## Problem

Cirrus telemetry reports a 37.8 second root activity duration for a `profile show`
invocation that failed on missing required parameters. The command itself failed in
milliseconds.

The cause is in `src/CLI/Program.cs`. `Main` opens the root activity, and the two
catch blocks that trigger AI error recovery call `TryAgentFallbackAsync` *inside* the
catch:

```csharp
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddException(ex);
    return await TryAgentFallbackAsync(positional, ex.Message);   // blocks
}
finally
{
    activity?.Dispose();                                          // root ends here
}
```

`TryAgentFallbackAsync` spawns `azclips-agent.exe` and awaits
`proc.WaitForExitAsync()`. The root activity is not stopped until the `finally`, so
the entire AI session — model latency, tool calls, user think time in interactive
mode — is recorded as command execution time. Every command-duration metric,
percentile, and regression alert derived from `CLI.Activity.Root` is corrupted by it.

A second, related gap: `azclips-agent.exe` emits no telemetry at all. `AgentModeEntry`
calls `AzclipsServiceProvider.Initialize(ClientType.CLI)` without registering an
`ITelemetryProvider` or `ITelemetrySender`, so it resolves `DefaultTelemetryProvider`
(`ActivitySource("Default")` + `TracerProvider.Default`). No listener is attached, so
every `StartActivity` returns `null`. There is no way to see where the 37.8 seconds
went.

## Goals

1. `CLI.Activity.Root` records only the command's own execution time.
2. AI error recovery is recorded as its own trace, with an accurate duration.
3. The recovery trace carries a reference back to the command trace that triggered it.
4. The agent process is instrumented so the recovery trace shows where its time went.

## Non-goals

- Changing when AI recovery triggers, its console output, or its exit-code semantics.
- Making the CLI stop waiting for the agent. The parent must keep waiting: it owns
  terminal handoff and the exit code. This design does not change that.
- A back-reference from the command trace to the recovery trace. The link is
  one-directional, recovery → command. See [Decisions](#decisions).
- Linking nested `az` invocations that the agent itself runs. Those emit their own
  unrelated roots.
- Capturing prompts, error text, or model output in telemetry.

## Architecture

Two traces. `CLI.Activity.Root` (T1) closes the instant the command fails.
`CLI.Activity.AiRecovery` (T2) is a second root opened in the same `az` process,
wrapping the subprocess spawn and wait. The agent process joins T2 as a real child via
a W3C traceparent passed on its environment block.

```
T1  CLI.Activity.Root            [az]               ~200ms   Error
    ├─ AzureCLI.Startup
    ├─ AzureCLI.ParseCommandInvocation
    └─ AzureCLI.ProcessCommand
       └─ ProfileShowProcessor.ExecuteAsync          throws

T2  CLI.Activity.AiRecovery      [az]                37.8s
    │  properties.command.traceId = T1
    └─ Agent.Session             [azclips-agent]
       ├─ Agent.Create
       └─ Agent.Turn
          ├─ Agent.Send
          └─ tool.*
```

T2 becomes a new root rather than a child of T1 because of ordering:
`Activity.Dispose()` calls `Stop()`, which restores `Activity.Current` to the
activity's parent. T1's parent is `null`, so after T1 is disposed `Activity.Current`
is `null` and the next `StartActivity` has no ambient parent — it allocates a fresh
`TraceId`. Same process, separate trace, separate `operation_Id` in Application
Insights.

### Why the CLI owns the recovery root

The recovery root is emitted by `az`, not by the agent, for three reasons:

- **It always closes.** The parent controls the span, so a hung, crashed, or killed
  agent still produces a recovery row with a duration. An agent-owned root is lost in
  exactly the failure mode most worth measuring.
- **It measures what the user waited.** Agent-internal timing excludes child-process
  CLR startup, DI construction, and assembly probing.
- **It gives the agent's spans a parent that exists.** The alternative — pre-generating
  a trace ID for the agent — produces a dangling `operation_ParentId` with no span
  behind it.

### Correlation

One-directional, recovery → command, carried as tags on T2:

| Tag | Value |
| --- | --- |
| `properties.command.traceId` | T1's `TraceId` |
| `properties.command.spanId` | T1's root `SpanId` |
| `command` | the failed command |
| `properties.recovery.sessionStyle` | `oneshot` \| `interactive` |
| `properties.recovery.provider` | `builtin` \| `github` |
| `properties.recovery.exitCode` | agent process exit code |

Query direction is command → recovery:

```kusto
requests
| where name == "CLI.Activity.AiRecovery"
| where customDimensions["properties.command.traceId"] == "<command operation_Id>"
```

T1 carries no reference to T2. T2's `TraceId` does not exist until after T1 has closed,
so a back-reference would require pre-generating it — the dangling-parent problem
above. The KQL join above serves both directions.

The `properties.` prefix is significant: `DefaultTelemetryPropertyFilter.IsAllowed`
admits any key under `TelemetryTags.PropertyTagPrefix` as a vetted product channel, so
these tags need no allowlist edit.

### Cross-process span linking

Traces are not exported directly. `FileExporter` writes them to
`traces-{traceId}.log`, and a detached uploader replays them through
`TraceReplayer`. Replay assigns new span IDs and rebuilds parent edges through a
`spanIdMap`, which is populated per **export group**. `TraceUploader.Run` groups by
service name, and `FileExporter.GetResource` derives `service.name` from
`activity.Source.Name`.

A child whose parent is in a different group falls into this branch:

```csharp
else
{
    parentSpanId = ActivitySpanId.CreateRandom();
}
```

The `TraceId` survives, but the parent edge points at a span that does not exist.

Therefore **the agent's `ActivitySource` must be named `"CLI"`**, matching the CLI's,
so both processes' spans land in the same group and `TraceUploader` — which already
flattens entries across files within a group precisely so a parent and child in
different files can be linked — relinks them. Agent spans stay distinguishable by
their `Agent.*` / `tool.*` name prefixes and a `properties.process = azclips-agent`
tag.

## Changes

### `src/CLI/Program.cs`

Both catch blocks that reach AI recovery (`HttpRequestException` and the general
`Exception`) capture the command's identifiers, stop the root, and pass the
identifiers down:

```csharp
catch (Exception ex)
{
    _logger?.Error(ex.Message);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddException(ex);

    var commandTraceId = activity?.TraceId.ToString();
    var commandSpanId  = activity?.SpanId.ToString();
    activity?.Dispose();

    return await TryAgentFallbackAsync(positional, ex.Message, commandTraceId, commandSpanId);
}
```

The `finally`'s `activity?.Dispose()` stays. `Activity.Dispose` calls `Stop()` only
when the activity is not already stopped, so the second call is a no-op.

In `TryAgentFallbackAsync`, the recovery root opens **after** the four early-return
guards (`agent.enabled` false, interactive-mode non-TTY, `AZCLIPS_IN_AGENT_MODE`
recursion, agent exe missing), so no recovery span is emitted when no agent runs:

```csharp
using var recovery = _telemetryService?.StartRootActivity(name: "CLI.Activity.AiRecovery");
recovery?.SetTag(TelemetryTags.CommandTraceId, commandTraceId);
recovery?.SetTag(TelemetryTags.CommandSpanId, commandSpanId);
recovery?.SetTag(TelemetryTags.Command, JoinShellQuoted(args));
recovery?.SetTag(TelemetryTags.RecoverySessionStyle, agentOptions.SessionStyle);
recovery?.SetTag(TelemetryTags.RecoveryProvider, agentOptions.Provider);
// ... spawn + WaitForExitAsync inside this span ...
recovery?.SetTag(TelemetryTags.RecoveryExitCode, proc.ExitCode);
recovery?.SetStatus(proc.ExitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
```

`StartRootActivity` rather than `StartActivity`: a bare `StartActivity` produces a
request row without `InstallationId`, `productName`, `productVersion`,
`schemaVersion`, `telemetryVersion`, or `clientType`, which would make T2 unqueryable
alongside every other root.

The `ProcessStartInfo` construction moves into an `internal static` seam so tests can
assert the environment block without spawning a process, mirroring the existing
`BuildTelemetryUploaderStartInfo` / `TelemetryUploaderSpawnTests` pattern:

```csharp
internal static ProcessStartInfo BuildAgentStartInfo(
    string agentExe, string[] args, string? errorText, string? recoveryTraceparent);
```

It adds one entry to the environment block, alongside the two already there:

```csharp
psi.Environment["AZCLIPS_RECOVERY_TRACEPARENT"] = recoveryTraceparent;
```

Namespaced rather than the bare `TRACEPARENT` so an ambient CI-set traceparent cannot
hijack the agent's parent. `Activity.Id` is already in W3C traceparent format
(`00-{trace}-{span}-{flags}`) in .NET, so no formatting is required.

### `src/Core/Core.Abstractions/ITelemetryService.cs` and `TelemetryService.cs`

One addition:

```csharp
Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null);
```

`name` defaults to the existing `$"{ClientType}.Activity.Root"`, so all fifteen
existing call sites — fourteen PowerShell cmdlets plus `ImportTelemetry` — are
unaffected.

Five `ITelemetryService` stubs in tests need the updated signature:
`tests/Common/Stubs/StubTelemetryService.cs` (one),
`tests/Cmdlets.Tests/ImportTelemetryTests.cs` (two), and
`tests/Core.Telemetry.Tests/PerformanceTimelineTests.cs` (two).

No `parentContext` overload is added. `ITelemetryService.StartActivity` hardcodes
`parentContext: default`, so it cannot attach to a trace from another process — but
the agent does not need it to. `AgentTracing.Source` is a plain `ActivitySource` and
its `StartActivity(name, kind, parentContext)` overload takes the context directly.
That is already the agent's established pattern (`ToolInvocationLog.Scope`), and it
keeps the shared interface unchanged.

### `src/Core/Core.Abstractions/TelemetryTags.cs`

New constants, all under the `properties.` prefix except `Command`, which already
exists:

```csharp
public const string CommandTraceId        = "properties.command.traceId";
public const string CommandSpanId         = "properties.command.spanId";
public const string RecoverySessionStyle  = "properties.recovery.sessionStyle";
public const string RecoveryProvider      = "properties.recovery.provider";
public const string RecoveryExitCode      = "properties.recovery.exitCode";
public const string AgentModel            = "properties.agent.model";
public const string AgentTurns            = "properties.agent.turns";
public const string ProcessName           = "properties.process";
```

No `DefaultTelemetryPropertyFilter` change: the prefix is already admitted.

### `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs`

Names not on the allowlist have their `Recorded` flag cleared by
`DefaultActivityNameFilterProcessor` and are silently dropped. Add to
`AllowedExactNames`:

```
CLI.Activity.AiRecovery
Agent.Session
Agent.Create
Agent.Turn
Agent.Send
```

and to `AllowedNamePrefixes`:

```
tool.
```

### `Agent/` — telemetry provider

A provider local to `Core.Agent`, deliberately not shared with
`Core.Cli.Telemetry.CLITelemetryProvider` (`Core.Agent` does not reference
`Core.Cli`, and promoting it into `Core.Telemetry` would pull in the PowerShell and
TUI providers' shape):

```csharp
// Agent/Telemetry/AgentTelemetryProvider.cs
//
// The ActivitySource name MUST stay "CLI", matching CLITelemetryProvider.
// FileExporter derives service.name from ActivitySource.Name, and TraceUploader
// rebuilds parent/child edges only within a service-name group. A different name
// here silently reparents every agent span to a random span id on upload.
public sealed class AgentTelemetryProvider : DefaultTelemetryProvider
{
    public AgentTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    { /* mirrors CLITelemetryProvider, plus the gate below */ }
}
```

The duplication is intentional and is called out in the comment above.

The provider carries one behaviour the CLI's does not: it consults
`TelemetryOptions.Enabled` and leaves `TracerProvider = TracerProvider.Default` when
telemetry is off. The CLI can rely on `TelemetryService`'s per-call gate because every
CLI span goes through `ITelemetryService`. Agent spans do not — `AgentTracing.Source`
is a plain `ActivitySource` — so without a listener-level gate, enabling the provider
would start exporting `tool.*` spans regardless of the user's opt-out. Gating listener
construction suppresses every agent span at once. A per-process decision is sound here
because the agent is short-lived and single-shot.

### `Agent/AgentMode/AgentModeEntry.cs`

Register the provider before initializing the container, then open the session span
against the traceparent handed down by the CLI:

```csharp
AzclipsServiceProvider.OverrideRegistration(s =>
{
    s.AddSingleton<ITelemetrySender, FileTelemetrySender>();
    s.AddSingleton<ITelemetryProvider, AgentTelemetryProvider>();
});
AzclipsServiceProvider.Initialize(ClientType.CLI);

// Resolving ITelemetryService constructs AgentTelemetryProvider, which builds the
// TracerProvider and starts listening. Without it the ActivitySource has no listener
// and every StartActivity below returns null. The container disposes it on exit,
// which flushes the exporter.
_ = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();

using var session = AgentTracing.Source.StartActivity(
    "Agent.Session", ActivityKind.Server, RecoveryTraceContext.FromEnvironment());
```

When the variable is absent or malformed, `RecoveryTraceContext.FromEnvironment`
returns `default(ActivityContext)` and `Agent.Session` becomes its own root. The agent
still functions; only the parent edge is lost.

The agent must **not** call `TelemetryUpload.RunPending()` or spawn an uploader. It
does not today, and that must hold: the CLI's `finally` spawns the uploader after the
agent has exited, so both trace files are pending and get claimed in a single run.

### `Agent/` — span seams

| Span | Wraps | Answers |
| --- | --- | --- |
| `Agent.Session` | all of `AgentModeEntry.RunAsync` | total agent cost |
| `Agent.Create` | `ChatAgent.CreateAsync` | SDK / daemon bootstrap |
| `Agent.Turn` | `SendWithToolEnforcementAsync` | per-turn cost, including tool-enforcement retries |
| `Agent.Send` | `ChatAgent.SendAsync` | model round-trip latency |
| `tool.*` | `ToolInvocationLog.Scope` — already exists | per-tool cost |

`Agent/Tools/AgentTracing.cs` currently declares `ActivitySource("Azclips.Agent")`,
which no provider listens to. It is repointed at the same `"CLI"` source so existing
`tool.*` spans start recording and land in the correct upload group.

Tags on `Agent.Session`: `properties.process`, `properties.agent.model`,
`properties.agent.turns`, plus the invariants. No prompts, error text, or model output.

### `docs/azure-cli/docs-conceptual/ai-based-error-recovery.md`

The FAQ currently states:

> During the private preview, AI-based error recovery doesn't collect telemetry.

That becomes untrue. Replace it with a description of what is collected — span names,
durations, turn and tool counts, provider, session style, exit code — and what is not:
prompts, error text, model output, command parameter values. Keep the existing
`agent.trace.enabled` local-JSONL paragraph, which is a separate mechanism, and note
that `telemetry.enabled=false` suppresses all of it.

## Error handling

| Scenario | Behaviour |
| --- | --- |
| Agent hangs, is Ctrl+C'd, or crashes | T2 still closes — the CLI owns it via `using`. Agent spans may be lost; T2 retains the wall-clock duration and exit code. |
| `telemetry.enabled=false` | No spans anywhere. In the CLI, `TelemetryService` re-reads config per span. In the agent, `AgentTelemetryProvider` skips listener construction entirely, which also suppresses `AgentTracing.Source` spans that bypass `ITelemetryService`. The duration fix still applies — `activity?.Dispose()` is null-safe. |
| `agent.enabled=false`, non-TTY interactive, recursion guard, agent exe missing | No T2. The guards return before it opens. |
| `AZCLIPS_RECOVERY_TRACEPARENT` absent or malformed | `Agent.Session` becomes its own root. Agent works normally; only the parent edge is lost. |
| Trace files claimed in different uploader runs | Agent spans keep `TraceId` T2 but get a random parent. The tree flattens; no span is lost. |
| Nested `az` run by the agent | Emits its own unrelated root. Not linked — non-goal. |

`FileExporter` pins `_filePath` to the first exported activity's `TraceId` and never
re-evaluates it, so T1 and T2 both land in `traces-{T1}.log` from the CLI process. This
is harmless — each entry carries its own `TraceId` and `TraceReplayer` replays them as
two traces — but the filename is misleading. Left as-is with a comment; changing it is
out of scope.

## Testing

| Project | Test |
| --- | --- |
| `Cli.Tests` | **Regression for #602**: the command root is stopped before the agent launcher is invoked. |
| `Cli.Tests` | `BuildAgentStartInfo` sets `AZCLIPS_RECOVERY_TRACEPARENT` to a well-formed traceparent carrying T2's trace and span IDs. |
| `Cli.Tests` | `BuildAgentStartInfo` omits the variable when telemetry is disabled and the recovery activity is `null`. |
| `Cli.Tests` | No recovery span is emitted when a guard short-circuits the fallback. |
| `Core.Telemetry.Tests` | `StartRootActivity(name:)` with `Activity.Current == null` yields a new `TraceId`, the supplied name, and the full invariant tag set. |
| `Core.Telemetry.Tests` | The name filter admits the five new exact names and the `tool.` prefix. |
| `Core.Telemetry.Tests` | `properties.*` tags survive `DefaultTelemetryPropertyFilter`. |
| `Core.Telemetry.Tests` | `TraceReplayer` relinks a parent and child sourced from two different files within one service group. |
| `Core.Agent.Tests` | `RecoveryTraceContext.Parse` extracts trace and span IDs from a well-formed traceparent, and returns `default` for absent or malformed input. |
| `Core.Agent.Tests` | `AgentTelemetryProvider.ActivitySource.Name` equals `ClientType.CLI.ToString()` — the invariant the cross-process link depends on. |
| `Core.Agent.Tests` | `AgentTelemetryProvider` leaves `TracerProvider.Default` in place when `telemetry.enabled=false`, so no agent span records. |

The regression test is the one that pins #602. To make it possible without spawning a
process, `TryAgentFallbackAsync` takes an injectable launcher delegate defaulting to
the real `Process.Start` path; the test substitutes one that records whether the
command activity is already stopped at invocation time.

## Decisions

| Decision | Rationale |
| --- | --- |
| CLI emits the recovery root, not the agent | Always closes, measures true wall-clock, gives agent spans a real parent. |
| One-directional link, recovery → command | T2's `TraceId` does not exist until T1 has closed. A back-reference would need a pre-generated ID with no span behind it. KQL joins both ways regardless. |
| Correlate with tags, not OTel `ActivityLink` | `FileExporter`, `TraceEntry`, and `TraceReplayer` do not serialize links. Tags survive the existing pipeline untouched. |
| Agent `ActivitySource` named `"CLI"` | `TraceUploader` relinks parents only within a service-name group, and `service.name` comes from the source name. |
| Duplicate the provider in `Core.Agent` | `Core.Agent` does not reference `Core.Cli`. Promoting it to `Core.Telemetry` would drag the PowerShell and TUI providers into scope. |
| Namespaced `AZCLIPS_RECOVERY_TRACEPARENT` | A bare `TRACEPARENT` could be set by the surrounding CI system and silently hijack the parent. |
| CLI still waits for the agent | It owns terminal handoff and the exit code. Detaching would change user-visible behaviour. |
| Agent spans use `AgentTracing.Source`, not `ITelemetryService` | Already the agent's pattern (`ToolInvocationLog`), takes a parent context directly, and leaves the shared interface unchanged. |
| The `telemetry.enabled` gate moves into `AgentTelemetryProvider` | `AgentTracing.Source` bypasses `TelemetryService`'s per-call gate. Gating listener construction suppresses every agent span, including `tool.*`. |
