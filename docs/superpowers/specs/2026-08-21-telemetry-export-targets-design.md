# Telemetry export targets: Debug ships to both sinks

Date: 2026-08-21
Status: Approved

## Problem

Telemetry has exactly one destination at a time. `TelemetryTargetResolver.Resolve()` returns a
single `TelemetryTarget` — `Local` (OTLP to Jaeger) in Debug, `Remote` (Azure Monitor) in Release —
and `AZCLIPS_TELEMETRY_TARGET` swaps between them.

A developer therefore validates one path or the other, never both. Local runs prove that spans are
produced and shaped correctly, but say nothing about whether tags, schema, and sampling survive the
trip to Application Insights. Confirming that requires deliberately switching to `remote`, at which
point Jaeger's fast feedback is gone. In practice nobody makes that switch, so remote ingestion is
only ever exercised after release.

## Goal

A Debug build exports to Jaeger and Azure Monitor simultaneously, so the remote path is exercised
continuously without giving up local feedback. Spans are tagged with their build configuration so a
developer can find their own runs in Application Insights and production queries can exclude them. A
developer can drop back to local-only when they want it. Release behaviour does not change.

## Non-goals

- **A separate dev or staging Application Insights resource.** Debug spans go to the existing
  production connection string in `WellKnownEndpoints`.
- **A user-facing config key.** The direction switch stays out of `config get/list/set` and
  tab-completion, exactly as it is today.
- **A new way to turn telemetry off.** `telemetry.enabled=false` remains the only opt-out, and the
  set of targets is never empty.

## Design

### Resolution returns a set

`TelemetryTarget` keeps its two members, and `TelemetryExporters.AddTraceExporter(builder, target)`
keeps its single-target signature — it remains the one decision point mapping *a* target to *an*
exporter. What changes is that the resolver returns an `IReadOnlyList<TelemetryTarget>` and callers
loop over it. No flags enum and no bit math, so the existing exporter tests survive untouched.

The environment variable accepts three closed tokens rather than a parsed list, which makes the
never-empty invariant structural instead of something the parser has to defend:

```csharp
internal static IReadOnlyList<TelemetryTarget> Resolve() =>
    Resolve(CompileTimeDefault, EnvironmentReader);

internal static IReadOnlyList<TelemetryTarget> Resolve(
    IReadOnlyList<TelemetryTarget> compileDefault,
    Func<string, string?> environmentReader)
{
    var raw = environmentReader(TargetEnvironmentVariable)?.Trim();
    if (string.IsNullOrEmpty(raw)) return compileDefault;

    return raw.ToLowerInvariant() switch
    {
        "local"  => [TelemetryTarget.Local],
        "remote" => [TelemetryTarget.Remote],
        "both"   => [TelemetryTarget.Local, TelemetryTarget.Remote],
        _        => compileDefault,
    };
}
```

Accepted values are `local`, `remote`, and `both`, case-insensitive and trimmed. Anything else —
including empty and whitespace — falls back to the compile default, preserving today's behaviour.
There is no `none`: disabling remote export in a Debug build is `AZCLIPS_TELEMETRY_TARGET=local`,
and disabling telemetry entirely is `telemetry.enabled=false`.

### Release is hard-wired

```csharp
private static Func<string, string?> EnvironmentReader =>
#if DEBUG
    Environment.GetEnvironmentVariable;
#else
    _ => null;          // Release: the variable is never read
#endif

private static IReadOnlyList<TelemetryTarget> CompileTimeDefault =>
#if DEBUG
    [TelemetryTarget.Local, TelemetryTarget.Remote];
#else
    [TelemetryTarget.Remote];
#endif
```

Today the environment override is live in Release. Once resolution can express more than one target,
that becomes a liability: a shipped binary would honour `AZCLIPS_TELEMETRY_TARGET=local` and silently
redirect a real user's telemetry to `localhost:4317`, on a port that is itself environment-controlled
via `AZCLIPS_OTLP_PORT`. Spans are already scrubbed by the allow-list processors, so the blast radius
is small, but the redirection is avoidable and buys nothing — pointing a customer's telemetry at a
local collector was never a supported story.

Suppressing it at the *reader* rather than in the parser means Release has no parse path to audit.
The variable is inert in a shipped binary by construction, not by remembering to guard a branch.

Both `#if` blocks are confined to private members. The `Resolve(compileDefault, environmentReader)`
overload contains no conditional compilation, so parsing rules and defaults are asserted identically
regardless of which configuration the test assembly is built in — which is how the existing resolver
tests already work.

### The sampler moves out of the Remote branch

`SetSampler(new AlwaysOnSampler())` currently sits inside the `Remote` branch of
`AddTraceExporter`. A sampler belongs to the provider, not to an exporter: there is one per
pipeline. Leaving it where it is means the recording rule depends on the destination — local-only
gets the default sampler, any set containing `Remote` gets keep-everything. A destination should not
change what gets recorded.

It is applied once for whatever set is configured. This is behaviour-preserving in practice: the
default is `ParentBased(AlwaysOn)`, which keeps root spans and has children inherit the parent's
decision, and we create our own roots. Nothing that is exported today stops being exported. The rule
is simply stated once, unconditionally, instead of arriving as a side effect.

### Delivery and retention

`TraceUploader` deletes a trace file only when the sink reports success, and
`TracerProviderExportSink.TryExport` returns a single bool from `ForceFlush`. Two exporters on one
provider would collapse into one verdict, which matters because a stopped Jaeger is the normal state
on a developer machine: at best the flush burns its 10-second timeout on every uploader run, at worst
it reports failure and trace files accumulate indefinitely.

The sink therefore builds one provider per target, so each flush is judged independently and each
sampler stays scoped to its own pipeline. Which verdict governs retention is pure logic, kept
separate from the I/O:

```csharp
// Remote outranks Local. The highest-ranked target present owns retention.
internal static TelemetryTarget SelectAuthoritative(IReadOnlyList<TelemetryTarget> targets) =>
    targets.Contains(TelemetryTarget.Remote) ? TelemetryTarget.Remote : TelemetryTarget.Local;

public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
{
    var authoritative = SelectAuthoritative(_targets);
    var delivered = true;

    foreach (var target in _targets)
    {
        var ok = ExportTo(target, serviceName, entries);
        if (target == authoritative) delivered = ok;
    }

    return delivered;
}
```

`ExportTo` is today's `TryExport` body parameterised by target: build a provider, replay, and
`ForceFlush` with the existing 10-second timeout.

The ranking gives the right answer in both Debug configurations. Under `both`, Remote decides and a
stopped Jaeger is best-effort that never pins files. Under `local`, Local decides — the developer
explicitly asked for Jaeger, so a failed delivery is retried rather than silently discarded. A flat
"Remote decides, otherwise always succeed" rule would delete files after a failed local export,
which is the one outcome a developer running local-only would find baffling.

Replay runs once per target. `TraceReplayer` preserves the original `TraceId` while remapping
SpanIds, so both backends see the same trace, and the duplicated work is negligible at the entry
counts a single trace file holds.

### PowerShell

`DirectTelemetrySender` loops over the set:

```csharp
public void ConfigureExporter(TracerProviderBuilder builder)
{
    foreach (var target in TelemetryTargetResolver.Resolve())
        TelemetryExporters.AddTraceExporter(builder, target);
}
```

There is one `TracerProvider` per client, so both exporters necessarily share it. Retention does not
apply — this path writes no files. The only Debug cost is an inline OTLP attempt against a possibly
dead Jaeger, already bounded by the 500 ms timeout in `TelemetryService.Dispose`.

### Marking Debug traffic

Debug spans land in the production resource alongside real traffic, and nothing on the root span
distinguishes them. That blocks the goal as much as it hurts hygiene: a developer verifying the
remote path needs to find their own spans among production volume. One tag on the root span fixes
both:

```csharp
// TelemetryTags — camelCase, matching the other per-process invariants.
public const string BuildConfiguration = "buildConfiguration";

// TelemetryService.StartRootActivity, alongside the existing invariants.
activity.SetTag(TelemetryTags.BuildConfiguration, BuildConfigurationResolver.Current);   // "Debug" | "Release"
```

`BuildConfigurationResolver.Current` is a single `#if DEBUG` constant. The key must also be added to
`DefaultTelemetryPropertyFilter`'s allow-list: the filter is deny-by-default, so an unlisted key is
silently stripped on `OnEnd` and the tag would never reach either backend.

Production queries then exclude dev traffic with `| where buildConfiguration == "Release"`, and a
developer finds their own runs with the inverse. The tag carries no personal or environmental data,
so it fits the existing privacy model unchanged.

### Unchanged

`FileTelemetrySender` never consulted the target and still does not: CLI and TUI always buffer to
disk, and the fan-out happens in the uploader. `TelemetryOptions`, the config schema, the activity
name filter, and the on-disk trace format are untouched. The property filter changes only by gaining
one allow-listed key.

## Consequences

- Debug builds send spans to the production Application Insights resource. This is intended and was
  chosen deliberately over standing up a dev resource.
- **No existing tag separates Debug traffic from production traffic.** The root span carries
  `InstallationId`, `ProductName`, `ProductVersion`, `SchemaVersion`, `TelemetryVersion`, and
  `ClientType`. `InstallationId` is a per-machine hash, and `ProductVersion` reads an
  `InformationalVersion` that the release pipeline commits into the project file rather than
  stamping at build time — so a local Debug build reports the same version as the shipped build from
  that commit. This is why the `buildConfiguration` tag is in scope.
- A Debug uploader run under `both` with Jaeger down does extra work — one provider build plus a
  failing flush — before delivering to Azure Monitor. It does not block file deletion, because
  Remote owns retention whenever it is present.
- Release carries one target, one exporter, no environment read: identical behaviour and cost to
  today.

## Testing

- **Resolver.** `local`, `remote`, and `both` parse correctly with mixed case and surrounding
  whitespace. Unrecognized, empty, and whitespace-only input fall back to the supplied default. The
  Debug default is `[Local, Remote]` and the Release default is `[Remote]`, both asserted through
  the configuration-independent overload.
- **Retention.** `SelectAuthoritative` returns `Remote` for `[Local, Remote]` and for `[Remote]`,
  and `Local` for `[Local]`.
- **Sink.** Extend the existing construction test to sets. Behaviour stays covered through the fake
  in `TraceUploaderTests`, matching the current comment explaining why `TryExport` is not exercised
  live.
- **Exporters.** Existing tests compile and pass unchanged.
- **Build-configuration tag.** `DefaultTelemetryPropertyFilter.IsAllowed("buildConfiguration")`
  returns true, so the deny-by-default filter does not strip it. The root span emitted by
  `StartRootActivity` carries the tag.
- **Configuration guard.** One `#if DEBUG`-conditional test asserting that the real `Resolve()`
  honours the environment in Debug and ignores it in Release.

## Files touched

| File | Change |
| --- | --- |
| `src/Core/Core.Telemetry/TelemetryTargetResolver.cs` | Returns a set; three-token parsing; Release reader returns null |
| `src/Core/Core.Telemetry/TelemetryExporters.cs` | Sampler hoisted out of the `Remote` branch |
| `src/Core/Core.Telemetry/TracerProviderExportSink.cs` | Holds a set; per-target providers; `SelectAuthoritative` |
| `src/Core/Core.Telemetry/DirectTelemetrySender.cs` | Loops the set |
| `src/Core/Core.Telemetry/TelemetryUpload.cs` | Passes the resolved set to the sink |
| `src/Core/Core.Abstractions/TelemetryTags.cs` | New `BuildConfiguration` key |
| `src/Core/Core.Telemetry/TelemetryService.cs` | Tags the root span with the build configuration |
| `src/Core/Core.Telemetry/DefaultTelemetryPropertyFilter.cs` | Allow-lists the new key |
| `tests/Core.Telemetry.Tests/TelemetryTargetResolverTests.cs` | Set-returning assertions, new parse cases |
| `tests/Core.Telemetry.Tests/TracerProviderExportSinkTests.cs` | Set construction, `SelectAuthoritative` cases |
| `tests/Core.Telemetry.Tests/TelemetryPropertyFilterTests.cs` | Covers the new allow-listed key |
| `wiki/02-deep-dive/telemetry.md` | Provider table, async-export verdict, Debug dual-sink note |
| `eng-docs/spec/telemetry.md` | Destinations table reflects Debug shipping to both |
