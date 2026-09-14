---
name: azclips-upstream-gap-report
description: "Use when comparing an Azclips command with Azure CLI and Azure PowerShell remote-main customization and tests, then generating a concise Markdown report containing only partially implemented or missing behavior and test coverage."
argument-hint: "Provide the Azclips module and command, for example: Compute vm create / New-AzVM. Optionally provide the output Markdown path."
---

# Azclips Upstream Gap Report

Compare one Azclips command with its Azure CLI and Azure PowerShell equivalents
and write a missing-only Markdown report for the developer who owns the Azclips
customization.

This is a review workflow. Do not modify product code or tests.

## Required Inputs

- Azclips service module, such as `Compute`.
- Azclips CLI command and PowerShell cmdlet, such as `vm create` and `New-AzVM`.
- Optional output path. When omitted, write:
  `src/Extensions/<Module>/<module>-<command-group>-missing-gaps-report.md`.

Derive `<Module>` and `<command-group>` from the command being reviewed. Never
hardcode `Compute`, `vm`, or any other module or command in the output path.
Normalize the filename to lowercase kebab-case and omit the terminal action
verb. For example, `Compute` + `vm create` produces:
`src/Extensions/Compute/compute-vm-missing-gaps-report.md`.

## Evidence Rules

1. Read the current Azclips implementation from the local repository.
2. Read Azure CLI, Azure CLI Extensions, and Azure PowerShell evidence only
   from their remote GitHub `main` branches.
3. Do not use local upstream clones, forks, feature branches, or PR branches.
4. Resolve and record the current remote-main commit SHA for every upstream
   repository used. Cite URLs pinned to those commits in the report.
5. If an upstream repository cannot be reached or the matching behavior cannot
   be found, mark it `insufficient evidence`; do not guess or use a local copy.
6. Use Swagger or TypeSpec only to identify the service operation and API
   contract. Do not mistake generated REST plumbing for customization logic.

## Mandatory Workflow

### 1. Map the command

- Find `src/Extensions/<Module>/ExtensionIndex.cs`.
- Confirm which processor handles the CLI command and PowerShell cmdlet.
- Identify the Swagger/TypeSpec service module, resource, action, HTTP
  operations, and API versions used by the customization.
- Read the active files under `src/Extensions/<Module>/Customization`.
- Read generated processors or models only as supporting evidence.

### 2. Find upstream behavior

On remote `main`, locate:

- Azure CLI custom command implementation and helpers.
- Azure PowerShell cmdlet implementation and helpers.
- Relevant Azure CLI and Azure PowerShell tests.

Trace behavior by intent:

- orchestration and operation order;
- dependency creation, discovery, and reuse;
- request-body construction;
- defaults and conditional branches;
- resource side effects and post-create operations;
- polling and response behavior;
- behavior-relevant tests and assertions.

Do not copy upstream source code into Azclips or into the report.

### 3. Remove non-customization items

Read `../azclips-custom-code-classifier/references/blacklist.md` and exclude
blacklisted concerns before counting behavior.

Also exclude shell-only mechanics unless they cause an Azure behavior
difference:

- CLI option spelling, aliases, help, output tables, and `--no-wait`;
- PowerShell parameter sets, aliases, pipeline binding, `-AsJob`, and output
  formatting;
- duplicate validation already handled by generated clients or the service.

### 4. Build conceptual behavior units

Count behavior units, not source files or helper functions.

One behavior implemented separately by Azure CLI and Azure PowerShell counts as
one shared unit when both have the same intent. Classify every unit exactly
once:

- `Shared`: behavior expected on both surfaces.
- `CLI-only`: Azure operation behavior unique to Azure CLI.
- `PowerShell-only`: Azure operation behavior unique to Azure PowerShell.

Do not assume one upstream customization file equals one behavior unit.

### 5. Compare Azclips

Classify each behavior unit:

- `Fully implemented`: Azclips preserves the meaningful upstream behavior.
- `Partially implemented`: the main path exists, but a condition, default,
  selection rule, request shape, reuse rule, side effect, or branch differs.
- `Missing`: no equivalent Azclips behavior exists.

Look specifically for simplified decision logic:

- hardcoded Windows/Linux or resource-type behavior;
- missing existence checks before create/update;
- omitted default behavior;
- selecting all resources instead of the requested resource;
- explicit switches without native automatic inference;
- guessed publishers, versions, SKUs, or API values;
- missing CLI-only or PowerShell-only post-create operations.

Describe the behavioral consequence, not merely the code difference.

### 6. Compare tests

For every partially implemented or missing behavior, compare upstream tests
with current Azclips scenario tests and live scripts.

Classify test coverage as:

- `Partially covered`: some path exists, but the differing branch or assertion
  is absent.
- `Not covered`: no Azclips test exercises the behavior.

Do not request duplicate tests for fully covered behavior. Recommend the
smallest existing Azclips test location that can prove the missing behavior.

### 7. Write the report

Create or replace only the requested Markdown report. If no path was provided,
place it under the reviewed module's directory:
`src/Extensions/<Module>/<module>-<command-group>-missing-gaps-report.md`.
Keep it simple, developer-facing, and missing-only.

The opening conclusion should follow this pattern:

> Azclips keeps the main CLI/PowerShell workflow, but some conditional and
> selection logic is simplified, hardcoded, or omitted, so behavior differs in
> the cases listed below.

Use this structure:

```markdown
# <Module> <Command> missing gaps report

> **Summary:** <one-sentence conclusion>

## Scope and sources

| Surface | Command | Source |
|---|---|---|
| Azclips CLI | `<command>` | `<local routing/customization paths>` |
| Azclips PowerShell | `<cmdlet>` | `<local routing/customization paths>` |
| Azure CLI | `<command>` | `<commit-pinned remote-main URLs>` |
| Azure PowerShell | `<cmdlet>` | `<commit-pinned remote-main URLs>` |

Swagger/TypeSpec: `<service, operation, API version, source URL>`

## Customization inventory

| Type | Count | Behavior units |
|---|---:|---|
| Shared | `<n>` | `<numbered names>` |
| CLI-only | `<n>` | `<numbered names>` |
| PowerShell-only | `<n>` | `<numbered names>` |
| **Distinct total** | **<n>** | |

## Missing customization

| # | Behavior | Type | Status | What is missing | Resulting difference | Evidence |
|---:|---|---|---|---|---|---|
| | | | Partially implemented / Missing | | | |

## Missing test coverage

| # | Behavior | Coverage | Missing test or assertion | Recommended Azclips location | Upstream evidence |
|---:|---|---|---|---|---|
| | | Partially covered / Not covered | | | |

## Insufficient evidence

- `<Only include unresolved evidence gaps; otherwise write None.>`
```

## Report Rules

- Include the inventory counts for context.
- In the implementation and test tables, include only partial or missing
  items. Never list fully implemented or fully covered items.
- Keep one row per conceptual behavior unit.
- Use short, direct descriptions suitable for creating work items.
- Cite local files with line numbers and upstream files with commit-pinned
  URLs and line ranges where possible.
- Separate observed facts from recommendations.
- Do not include implementation code, migration plans, or large source
  excerpts unless explicitly requested.
- Ensure all counts reconcile:
  `shared + CLI-only + PowerShell-only = distinct total`.
- Ensure each missing-test row maps to a partial or missing behavior, unless the
  test itself exposes a separately documented coverage gap.

## Completion Check

Before finishing:

1. Confirm remote-main commit SHAs and source URLs are recorded.
2. Confirm active Azclips routing was checked.
3. Confirm blacklist items and shell-only mechanics were excluded.
4. Confirm behavior units are conceptual rather than file counts.
5. Confirm the report contains no fully implemented or fully covered rows.
6. Confirm implementation and test counts match their tables.
7. Confirm the Markdown file exists at the requested/default path.
