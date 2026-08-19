# Inject ITelemetrySender into the Telemetry Providers — Design

**Date:** 2026-08-19
**Status:** Approved
**Scope:** `CLITelemetryProvider`, `TUITelemetryProvider`, `PSTelemetryProvider`, and the three client composition roots

## Problem

All three telemetry providers construct their own sender:

```csharp
// CLITelemetryProvider, TUITelemetryProvider
FileTelemetrySender sender = new();

// PSTelemetryProvider
var sender = new DirectTelemetrySender();
```

Two consequences.

**The providers cannot be constructed with a different sender.** Each one hard-codes its
export strategy, so there is no way to build a provider in a test without also building the
real exporter pipeline that sender configures.

**The export decision is invisible at the composition root.** Whether a client buffers to
file or exports inline is a per-client architectural choice, but it is expressed inside a
constructor body rather than beside the other per-client registrations. Reading
`Program.cs` tells you which `ITelemetryProvider` a client uses but not which sender.

The seam already exists. `ITelemetrySender` was reduced to a single method in the
`FileTelemetrySender` refactor (see
`docs/superpowers/specs/2026-08-19-file-telemetry-sender-refactor-design.md`), and every
provider already receives `IEnumerable<BaseProcessor<Activity>>` by injection. The sender
is the one collaborator still built by hand.

## Goals

- Providers receive their sender rather than constructing it.
- Each client's sender choice is visible beside its other registrations.
- No behavior change.

## Non-goals

- Changing which sender any client uses. CLI and TUI keep `FileTelemetrySender`; PS keeps
  `DirectTelemetrySender`.
- Changing `ITelemetrySender`, `FileTelemetrySender`, or `DirectTelemetrySender`.
- Adding telemetry tests. This is wiring; the three client builds and the existing suite
  are the proof.

## Design

Each provider takes `ITelemetrySender` as a constructor parameter and drops the `new`:

```csharp
public CLITelemetryProvider(
    IEnumerable<BaseProcessor<Activity>> filterProcessors,
    ITelemetrySender sender)
{
    ActivitySource = new ActivitySource(ClientType.CLI.ToString());
    // ... builder configuration unchanged ...
    sender.ConfigureExporter(builder);
    TracerProvider = builder.Build();
}
```

`TUITelemetryProvider` and `PSTelemetryProvider` change identically — same parameter, same
removal, everything else untouched.

Each client registers its own sender in the `OverrideRegistration` block where it already
selects `ITelemetryProvider`:

| Client | Registration site | Sender |
| --- | --- | --- |
| CLI | `src/CLI/Program.cs` | `FileTelemetrySender` |
| TUI | `TUI/Program.cs` | `FileTelemetrySender` |
| PS | `src/PS/Login/ModuleInitializer.cs` | `DirectTelemetrySender` |

```csharp
services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
```

Singleton lifetime matches the surrounding registrations. Both senders are stateless, so
lifetime is not load-bearing — consistency is the only reason.

## Decisions

| Decision | Rationale |
| --- | --- |
| Register per client rather than a default in `RegisterTelemetryServices` | That extension is shared by all three clients, and they do not agree: PS needs `DirectTelemetrySender` while CLI and TUI need `FileTelemetrySender`. A shared default would be wrong for at least one client. |
| No fallback default at all | Every client now registers explicitly. A default would be dead registration that silently masks a client forgetting to register — better to fail loudly at resolution. |
| Inject the interface, not the concrete type | `ITelemetrySender` exists for exactly this. Injecting the concrete type would leave the hard-coding in place and just move it. |
| Change all three providers | Their own comments state they are kept uniform on purpose. Injecting two of three would break that parity for no gain. |

## Testing

No new tests. Nothing gains behavior; this changes how existing collaborators are supplied.
Verification is:

- `dotnet build src/CLI/Core.Cli.csproj`
- `dotnet build TUI/Core.Tui.csproj`
- `dotnet build src/PS/Login/Cmdlets.Login.csproj`
- `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

A missed registration surfaces at container resolution, not at compile time, so the builds
alone are not sufficient proof. Two concrete runtime checks close that gap:

- Run a normal CLI command (for example `az --version`), which builds the container and
  resolves `ITelemetryProvider`. A missing `ITelemetrySender` registration throws here.
- Import the PowerShell module manifest from `src/PS/Login`, which runs
  `ModuleInitializer` and resolves `PSTelemetryProvider` the same way.

The TUI shares the CLI's registration shape, so its build plus the CLI resolution check
together give adequate coverage without launching an interactive session.
