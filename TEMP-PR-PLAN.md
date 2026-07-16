# TEMP PR PLAN — Issue #503: `az` (no args) should show top-level help

Tracking file for the work behind
[Azure/CLIPS#503](https://github.com/Azure/CLIPS/issues/503). Delete on merge.

## Problem

Running `az` with no command/flags currently errors:

```
$ az
[ERROR] Arguments cannot be empty. (Parameter 'args')
```

Root cause: [`ExtensionDispatcher.Parse`](src/CLI/Parser/ExtensionDispatcher.cs#L48)
throws `ArgumentException` for `args.Length == 0`. `Program.Main` catches it as a
generic `Exception`, which triggers the AI agent fallback path (or falls out as
exit code 1 when the agent is disabled).

## Goals

1. Bare `az` returns top-level help (same output as `az --help`).
2. Bare `az` prints a small ASCII "welcome" banner above the help, only on the
   empty-args path (not on `az --help`).
3. Discuss how switches (`--help`, `--version`, `--debug`, `--output`,
   `--no-cache`, ...) are handled today and where the fragmentation lives.
4. (Later) Add an easter-egg switch — design pending.

## Non-goals for this PR

- Refactoring the global-flag parsing layer (see "Follow-up" below). Doing that
  in the same PR as a user-visible behavior fix makes the diff harder to review
  and revert.
- Changing help rendering itself (`RuntimeHelpRenderer`, adapter output).

## Implementation

### 1. Route empty args to help

`src/CLI/Parser/ExtensionDispatcher.cs`:

- Replace the `args.Length == 0 → ArgumentException` guard with a
  `DispatchHelpException` throw — the same exception raised by `az --help`.

### 2. Welcome banner (empty-args only)

- Add `src/CLI/Help/WelcomeBanner.cs` — a tiny static helper that writes an
  ASCII banner to a `TextWriter`.
- In `src/CLI/Program.cs`:
    - Capture `bool isBareInvocation = args.Length == 0` at the top of `Main`,
      before any flag stripping.
    - In the `catch (DispatchHelpException)` block, call
      `WelcomeBanner.Write(Console.Out)` when `isBareInvocation` is true, then
      render root help as today.

### 3. Tests

- Update `Parse_ShouldThrowArgumentException_WhenArgsEmpty` → rename to
  `Parse_ShouldThrowDispatchHelpException_WhenArgsEmpty` and assert on
  `DispatchHelpException` (matches the `--help`/`-h`/`help` cases already in
  the parameterized test).

### 4. Docs updates (light-touch)

- Update the wiki notes that enumerate the bare-help shapes (they currently
  list `az`, `az --help`, `az -h`, `az help` — the `az` case is now real, not
  aspirational):
    - `wiki/01-getting-started/cli-usage.md`
    - `wiki/onboarding-guide.md`
    - `wiki/02-deep-dive/core-engine-orchestration.md`

## Progress

- [x] Read issue #503, locate the throw site.
- [x] Route empty args to `DispatchHelpException`.
- [x] Add `WelcomeBanner` helper.
- [x] Wire welcome banner in `Program.Main` for the bare-invocation case.
- [x] Update `ExtensionDispatcherTests`.
- [x] `dotnet build azclips.slnx` clean.
- [x] `dotnet test tests/Cli.Tests/Cli.Tests.csproj` green.
- [x] Docs updates (`cli-usage.md`, `extension-system.md`, `core-engine-orchestration.md`, `onboarding-guide.md`, `zero-to-hero-guide.md`).
- [x] Manual smoke test: `az`, `az --help`, `az --debug`, `az version`.

## Follow-up (separate PR) — switch handling refactor

Today, cold-path global flags are parsed in three places:

| Flag                                  | Parsed in                         | Notes                               |
| ------------------------------------- | --------------------------------- | ----------------------------------- |
| `--upload-telemetry`                  | `Program.Main` (positional check) | Must run before DI init.            |
| `--debug`                             | `Program.ParseDebugFlag`          | Stripped; sets log-level override.  |
| `--output` / `--output=`              | `Program.ParseOutputFlag`         | Stripped; sets JSON error shape.    |
| `--no-cache`                          | `Program.ParseNoCacheFlag`        | Stripped; sets env var side-effect. |
| `--help` / `-h` / `-?` / `?` / `help` | `ExtensionDispatcher.Parse`       | Throws `DispatchHelpException`.     |
| `--version` / `version`               | `ExtensionDispatcher.Parse`       | Throws `DispatchVersionException`.  |

Symptoms of the split:

- Adding a new global flag means editing `Program.Main` and (usually) an ad-hoc
  `ParseXxxFlag` helper that mutates `args`.
- Some flags are stripped early, others are matched deep inside the dispatcher
  — order-of-operations bugs are easy (e.g. `--version foo` semantics).
- `TODO(Yabo): Q&P squad to refactor global parameter handling` already flags
  this on `ParseDebugFlag`.

Proposal (rough sketch, to discuss):

- New `GlobalArgs` record: `(string[] Positional, LogLevel? Debug, bool
OutputJson, bool NoCache, HelpRequest? Help, VersionRequest? Version, bool
UploadTelemetry, EasterEgg? Egg)`.
- New `GlobalArgParser.Parse(string[] raw) → GlobalArgs`. Single pass, single
  place to add a new flag, single place to write tests for.
- `Program.Main` consumes `GlobalArgs` directly — no exceptions for
  help/version, no in-place `args` mutation.
- `ExtensionDispatcher.Parse` receives `GlobalArgs.Positional` only and
  focuses on extension routing.

## Easter egg — TBD

Open questions to answer before implementing:

- Flag name? (candidates: `--roll`, `--fortune`, `--constellations`, `--zen`,
  `--motd`, `--fly`, ...).
- Output? (fixed message, rotating list, ASCII animation, ...).
- Should it be discoverable (listed in `--help`) or truly hidden?
- Should it short-circuit like `--version`, or attach to `az` alone?
- Any telemetry implications? (Probably: no distinct tag; it's a host-side
  cold path.)
