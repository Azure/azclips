# TEMP PR Plan

@NOTE: Delete before merge

## Goal

Prepare a PR similar in shape to PR #319 for the owned surfaces in Config, Extension, and the help system, with explicit tracking for every place where command names, help content, or agent help artifacts may need to change.

## Scope To Scan

- Config PowerShell surface
- Config CLI surface
- Extension CLI surface
- Help system build-time artifacts and validation flow
- Agent help snapshot files consumed by the Copilot agent

## Current Findings

- `config` has a real PowerShell surface and will likely need the same style of rename/update sweep as `login`.
- `extension` appears CLI-only from the runtime metadata surface, so it should be reviewed for help/documentation and agent-corpus coverage rather than PowerShell cmdlet renames.
- The agent currently consumes checked-in static help files under `Agent/Tools/Help/`.
- `Agent/Tools/Help/HelpIndex.json` appears in PR diffs today because it is a tracked snapshot, not because the agent build regenerates it in place.

## Expected Touchpoints

### Config

- `src/PS/Config/Config.psd1`
- `src/PS/Config/Cmdlets/`
- `src/PS/Config/help/`
- `src/PS/Config/en-US/`
- `src/Extensions/Config/ExtensionIndex.cs`
- `src/CLI/Help/Docs/config/` if CLI-facing descriptions/examples need to stay aligned
- `Agent/Tools/Help/HelpIndex.json`
- `Agent/Tools/Help/HelpManifest.json`

### Extension

- `src/Extensions/Extension/ExtensionIndex.cs`
- `src/CLI/Help/Docs/extension/`
- `Agent/Tools/Help/HelpIndex.json`
- `Agent/Tools/Help/HelpManifest.json`

### Help System

- `tools/Help/Cirrus.Help.Tool/`
- `build/build.ps1`
- `docs/howto/help.md`
- `docs/spec/help-system.md`

## Work Items

- Confirm the exact rename or API-shape changes needed for Config.
- Scan Extension commands for naming/help consistency and agent-corpus coverage gaps.
- Identify whether `Agent/Tools/Help/HelpIndex.json` and `Agent/Tools/Help/HelpManifest.json` need direct edits for the PR.
- Run help validation/generation tooling and compare generated `artifacts/help/HelpIndex.json` against the agent snapshot files.
- Decide whether the PR should only update names/content, or also fix the help snapshot workflow.

## Investigation Required

We need to investigate and fix the generation/update story for `Agent/Tools/Help/HelpIndex.json`.

Current understanding:

- The help tool generates `artifacts/help/HelpIndex.json`.
- The agent consumes `Agent/Tools/Help/HelpIndex.json` as a checked-in static file.
- There does not appear to be an automated step that syncs the generated artifact back into `Agent/Tools/Help/HelpIndex.json`.

Follow-up requirement:

- Remove or replace the manual snapshot maintenance model for `Agent/Tools/Help/HelpIndex.json` if feasible.
- At minimum, add an explicit, reproducible sync path so this file is never effectively hand-maintained.

## Open Questions

- Should this PR include automation for syncing agent help artifacts, or should that land as a separate follow-up?
- Is `Agent/Tools/Help/HelpManifest.json` subject to the same manual-maintenance problem and therefore part of the same fix?
- Does the agent snapshot intentionally lag the full CLI help corpus, or is that just current drift?

## Validation Ideas

- `dotnet build cirrus.slnx`
- `dotnet test cirrus.slnx --configuration Debug`
- `dotnet run --project tools/Help/Cirrus.Help.Tool/Cirrus.Help.Tool.csproj -- --artifacts-root artifacts/Debug --output-root artifacts/help`
- Compare generated help artifacts with `Agent/Tools/Help/` before finalizing the PR
