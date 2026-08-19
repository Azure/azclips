# Inject ITelemetrySender Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the three telemetry providers receive their `ITelemetrySender` by constructor injection instead of constructing it, and register each client's sender at its composition root.

**Architecture:** Each provider gains an `ITelemetrySender` constructor parameter and drops its `new`. Each client registers its own concrete sender in the `OverrideRegistration` block where it already selects `ITelemetryProvider` — `FileTelemetrySender` for CLI and TUI, `DirectTelemetrySender` for PowerShell. No shared default is registered, so a client that forgets fails loudly at container resolution.

**Tech Stack:** C# / .NET 10, Microsoft.Extensions.DependencyInjection, OpenTelemetry 1.15.3.

**Design spec:** `docs/superpowers/specs/2026-08-19-inject-telemetry-sender-design.md`

## Global Constraints

- Target framework is `net10.0`; nullable and implicit usings are enabled. Do not add `using System;`, `using System.IO;`, `using System.Collections.Generic;`, or `using System.Linq;` — they are implicit.
- This is a **wiring change with no behavior change**. Do not alter `ITelemetrySender`, `FileTelemetrySender`, `DirectTelemetrySender`, or any provider's builder configuration. The only edits are the constructor signature, the removed `new`, the registration line, and any `using` a file now needs.
- `ITelemetryProvider` lives in `Core.Abstractions`. `ITelemetrySender`, `FileTelemetrySender`, and `DirectTelemetrySender` live in `Core.Telemetry`. A registration site therefore needs `using Core.Telemetry;` — check whether the file already has it before adding one.
- Register with `AddSingleton`, matching every neighbouring registration.
- `src/Directory.Build.props` sets `TreatWarningsAsErrors=true`, so any compiler warning in product code fails the build. That includes a `using` left unused.
- Do not add a fallback registration in `Core.Telemetry.DIRegisterExtensions.RegisterTelemetryServices`. Every client registers explicitly; a shared default would be wrong for at least one of them and would mask a missing registration.
- Commit messages use the repo convention `{Telemetry} Description` — see `git log --oneline`.
- **A missed registration is invisible to the compiler.** All three projects will build clean and fail at runtime. Each task's verification therefore includes a resolution check, not just a build.
- Do NOT run `dotnet build azclips.slnx` without `-p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/` — `Agent/Core.Agent` downloads an npm package at build time and a bare solution build fails on TLS. Targeted project builds do not need the property.

---

### Task 1: Inject the sender into the CLI provider

The CLI is done first and on its own because it is the only client whose resolution path can be exercised non-interactively, which makes it the task that proves the registration is load-bearing.

**Files:**
- Modify: `src/CLI/Telemetry/CLITelemetryProvider.cs:23-25`
- Modify: `src/CLI/Program.cs:66-72`

**Interfaces:**
- Consumes: `ITelemetrySender` with the single member `void ConfigureExporter(TracerProviderBuilder builder)`, and `FileTelemetrySender : ITelemetrySender`, both in namespace `Core.Telemetry`.
- Produces: nothing later tasks depend on. Tasks 2 and 3 repeat this shape for their own client.

- [ ] **Step 1: Change the provider constructor**

In `src/CLI/Telemetry/CLITelemetryProvider.cs`, change the constructor signature and delete the line that constructs the sender.

From:

```csharp
    public CLITelemetryProvider(IEnumerable<BaseProcessor<Activity>> filterProcessors)
    {
        FileTelemetrySender sender = new();
        ActivitySource = new ActivitySource(ClientType.CLI.ToString());
```

To:

```csharp
    public CLITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender)
    {
        ActivitySource = new ActivitySource(ClientType.CLI.ToString());
```

Leave the rest of the constructor exactly as it is, including the `sender.ConfigureExporter(builder);` call near the end and the class doc comment above the constructor.

- [ ] **Step 2: Run the CLI to verify resolution now fails**

Run: `dotnet run --project src/CLI/Core.Cli.csproj -- --version`

Expected: FAIL. The container has no `ITelemetrySender` registration, so resolving `ITelemetryProvider` throws. The message names the unresolvable service, along the lines of:

```
Unable to resolve service for type 'Core.Telemetry.ITelemetrySender' while attempting to activate 'Core.Cli.Telemetry.CLITelemetryProvider'.
```

This is the point of the step: it proves the registration you add next is genuinely load-bearing rather than redundant. Capture the actual message — it is your RED evidence.

- [ ] **Step 3: Register the sender**

In `src/CLI/Program.cs`, add the sender registration to the existing `OverrideRegistration` block, above the provider that consumes it.

From:

```csharp
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, CLIUserAgentHandler>();
            });
```

To:

```csharp
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider, CLIAssemblyLoadingProvider>();
                services.AddSingleton<IArgumentParsingProvider, CLIArgumentParsingProvider>();
                services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
                services.AddSingleton<ITelemetryProvider, CLITelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, CLIUserAgentHandler>();
            });
```

`src/CLI/Program.cs` already has `using Core.Telemetry;` (line 17), so no new `using` is needed here.

- [ ] **Step 4: Run the CLI again to verify resolution succeeds**

Run: `dotnet run --project src/CLI/Core.Cli.csproj -- --version`

Expected: PASS. The command prints version information and exits 0. This is your GREEN evidence.

- [ ] **Step 5: Run the telemetry test suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS, 198 tests. Nothing in this task should move that number — it is a regression check, not new coverage.

- [ ] **Step 6: Commit**

```bash
git add src/CLI/Telemetry/CLITelemetryProvider.cs src/CLI/Program.cs
git commit -m "{Telemetry} Inject ITelemetrySender into the CLI provider"
```

---

### Task 2: Inject the sender into the TUI provider

**Files:**
- Modify: `TUI/Telemetry/TUITelemetryProvider.cs:18-20`
- Modify: `TUI/Program.cs:31-38`

**Interfaces:**
- Consumes: `ITelemetrySender` with the single member `void ConfigureExporter(TracerProviderBuilder builder)`, and `FileTelemetrySender : ITelemetrySender`, both in namespace `Core.Telemetry`.
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Change the provider constructor**

In `TUI/Telemetry/TUITelemetryProvider.cs`, change the constructor signature and delete the line that constructs the sender.

From:

```csharp
    public TUITelemetryProvider(IEnumerable<BaseProcessor<Activity>> filterProcessors)
    {
        FileTelemetrySender sender = new();
        ActivitySource = new ActivitySource(ClientType.TUI.ToString());
```

To:

```csharp
    public TUITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender)
    {
        ActivitySource = new ActivitySource(ClientType.TUI.ToString());
```

Leave the rest of the constructor exactly as it is, including the `sender.ConfigureExporter(builder);` call and the class doc comment.

- [ ] **Step 2: Register the sender**

In `TUI/Program.cs`, add the sender registration to the existing `OverrideRegistration` block, above the provider that consumes it.

From:

```csharp
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            // Replace the default ScopedLoggingService (which falls back to
            // ConsoleLoggingService and writes VERBOSE:/DEBUG: directly to the
            // console) with a silent implementation that won't corrupt the TUI.
            services.AddSingleton<ILoggingService, TuiLoggingService>();
            services.AddSingleton<ITelemetryProvider, TUITelemetryProvider>();
        });
```

To:

```csharp
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            // Replace the default ScopedLoggingService (which falls back to
            // ConsoleLoggingService and writes VERBOSE:/DEBUG: directly to the
            // console) with a silent implementation that won't corrupt the TUI.
            services.AddSingleton<ILoggingService, TuiLoggingService>();
            services.AddSingleton<ITelemetrySender, FileTelemetrySender>();
            services.AddSingleton<ITelemetryProvider, TUITelemetryProvider>();
        });
```

**`TUI/Program.cs` does NOT currently have `using Core.Telemetry;`.** Its existing `ITelemetryProvider` reference resolves through `using Core.Abstractions;`, but `ITelemetrySender` and `FileTelemetrySender` do not. Add `using Core.Telemetry;` to the using block, keeping it in the file's existing alphabetical order — between `using Core.DependencyInjection;` and `using Core.Tui.Azure;`:

```csharp
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Core.Tui.Azure;
```

- [ ] **Step 3: Build the TUI project**

Run: `dotnet build TUI/Core.Tui.csproj`

Expected: PASS, 0 errors and 0 warnings. A missing `using Core.Telemetry;` shows up here as `CS0246: The type or namespace name 'ITelemetrySender' could not be found`.

The TUI is an interactive application, so there is no non-interactive resolution check equivalent to Task 1's `--version`. The registration shape is identical to the CLI's, which Task 1 already proved at runtime; the build plus a read of the diff is adequate here. Do not launch the TUI.

- [ ] **Step 4: Run the telemetry test suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS, 198 tests.

- [ ] **Step 5: Commit**

```bash
git add TUI/Telemetry/TUITelemetryProvider.cs TUI/Program.cs
git commit -m "{Telemetry} Inject ITelemetrySender into the TUI provider"
```

---

### Task 3: Inject the sender into the PowerShell provider

This client differs from the other two: it registers `DirectTelemetrySender`, which exports inline rather than buffering to file.

**Files:**
- Modify: `src/PS/Login/Telemetry/PSTelemetryProvider.cs:21-23`
- Modify: `src/PS/Login/ModuleInitializer.cs:23-28`

**Interfaces:**
- Consumes: `ITelemetrySender` with the single member `void ConfigureExporter(TracerProviderBuilder builder)`, and `DirectTelemetrySender : ITelemetrySender`, both in namespace `Core.Telemetry`.
- Produces: nothing.

- [ ] **Step 1: Change the provider constructor**

In `src/PS/Login/Telemetry/PSTelemetryProvider.cs`, change the constructor signature and delete the line that constructs the sender.

From:

```csharp
    public PSTelemetryProvider(IEnumerable<BaseProcessor<Activity>> filterProcessors)
    {
        var sender = new DirectTelemetrySender();
        ActivitySource = new ActivitySource(ClientType.PowerShell.ToString());
```

To:

```csharp
    public PSTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender)
    {
        ActivitySource = new ActivitySource(ClientType.PowerShell.ToString());
```

Leave the rest of the constructor exactly as it is, including the `sender.ConfigureExporter(builder);` call and the class doc comment.

- [ ] **Step 2: Register the sender**

In `src/PS/Login/ModuleInitializer.cs`, add the sender registration to the existing `OverrideRegistration` block, above the provider that consumes it. Note this client registers `DirectTelemetrySender`, not `FileTelemetrySender`.

From:

```csharp
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
```

To:

```csharp
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetrySender, DirectTelemetrySender>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
```

`src/PS/Login/ModuleInitializer.cs` already has `using Core.Telemetry;` (line 8), so no new `using` is needed here.

- [ ] **Step 3: Build the PowerShell Login project**

Run: `dotnet build src/PS/Login/Cmdlets.Login.csproj`

Expected: PASS, 0 errors and 0 warnings.

- [ ] **Step 4: Verify the module imports and resolves**

Importing the module runs `ModuleInitializer.OnImport`, which builds the container and resolves `ITelemetryProvider` — the same resolution path a missing registration would break.

```powershell
pwsh -NoProfile -Command "Import-Module 'src\PS\Login\Login.psd1' -ErrorAction Stop; Write-Output 'IMPORT OK'"
```

Expected: `IMPORT OK`, exit 0. A missing `ITelemetrySender` registration surfaces here as a resolution exception naming `PSTelemetryProvider`.

If the import fails for a reason unrelated to DI — a missing build output, or the manifest resolving to an artifacts path that has not been staged — do NOT patch around it. Report it: per `AGENTS.md` the solution must be built before importing binary-backed modules, and `build/Set-ModuleConfiguration.ps1` stages modules under `artifacts/<Configuration>/PS`. Say what you saw and stop.

- [ ] **Step 5: Run the telemetry test suite**

Run: `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj`

Expected: PASS, 198 tests.

- [ ] **Step 6: Commit**

```bash
git add src/PS/Login/Telemetry/PSTelemetryProvider.cs src/PS/Login/ModuleInitializer.cs
git commit -m "{Telemetry} Inject ITelemetrySender into the PowerShell provider"
```

---

### Task 4: Whole-solution verification

**Files:** none.

**Interfaces:**
- Consumes: everything from Tasks 1–3.
- Produces: nothing.

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/`

Expected: build succeeds, 0 errors. Three pre-existing `GHCP001` warnings in `Agent/ChatAgent.cs` are expected and unrelated to this change; any warning naming a file this plan touched is a finding.

The `CopilotNpmRegistryUrl` property is required — `Agent/Core.Agent` downloads a GitHub Copilot npm package at build time, and without it the build reaches `registry.npmjs.org` and fails with a TLS handshake error.

- [ ] **Step 2: Confirm no provider constructs a sender any more**

Run: `git --no-pager grep -n "new FileTelemetrySender\|FileTelemetrySender sender = new\|new DirectTelemetrySender" -- "*.cs"`

Expected: no output. Every construction site should now be a DI registration.

- [ ] **Step 3: Confirm every client registers a sender**

Run: `git --no-pager grep -n "AddSingleton<ITelemetrySender" -- "*.cs"`

Expected: exactly three matches — `src/CLI/Program.cs`, `TUI/Program.cs`, and `src/PS/Login/ModuleInitializer.cs`. Two matches means a client was missed and will throw at runtime.

- [ ] **Step 4: Re-run the CLI resolution check**

Run: `dotnet run --project src/CLI/Core.Cli.csproj -- --version`

Expected: version output, exit 0.

This task produces no commits.
