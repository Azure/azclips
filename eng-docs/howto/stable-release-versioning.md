---
title: CLI, PowerShell, and extension versioning
description: Contributor rules, CI protections, and stable-release version automation.
---

# CLI, PowerShell, and extension versioning

Release automation calculates stable package versions and generates release notes. Contributors own
functional code and non-version project metadata.

## Contributor pull requests

| Change | Allowed? | CI behavior |
| --- | --- | --- |
| Product code, tests, and documentation | Yes | Normal build and test checks apply. Changes to extension `.cs` files or non-`Az` PowerShell module files must add or update a nonempty `## Upcoming Release` entry in the corresponding packageable module's `ChangeLog.md`. |
| Non-version `.csproj` properties, references, or build settings | Yes | The release policy permits the change. |
| Direct version properties in an existing `.csproj` | No | The PR policy fails. A new project may declare its initial versions. |
| A nested dependency version, such as `PackageReference/Version` | Yes | It is not treated as the project's release version. |
| PowerShell manifest versions (`ModuleVersion` or `RequiredModules`) | Yes | The PR policy does not inspect `.psd1` content. Stable-release automation later recalculates and aligns packageable module versions. |
| Add or delete a `.csproj` | Yes, when not combined with the opposite operation | New packageable extensions must include complete packaging metadata. When a PR contains both operations, every project addition and deletion must match one-to-one as a Git rename. |
| Rename a `.csproj` | Yes, if direct version properties are unchanged | The PR policy uses a low similarity threshold to identify renames and compares release-owned versions between the old and new paths. |
| Root `CHANGELOG.md` | No | The PR policy fails because stable-release automation owns it. |
| Module-level changelog (`src/PS/<Module>/ChangeLog.md`) | Yes | CI permits the file; contributors should add customer-facing notes under `## Upcoming Release`. Release automation creates the versioned section. |

The protected direct MSBuild properties are:

- `Version`
- `VersionPrefix`
- `VersionSuffix`
- `PackageVersion`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`

The validation job runs the policy checker from the PR base commit. A contributor cannot weaken the
policy in the same PR that changes protected files.

## Stable release pipeline

The `azclips-trigger-stable-release` pipeline has three stages:

| Stage | Result |
| --- | --- |
| Preflight | Verifies that the pipeline is running in the release project. |
| Trigger stable release | On a manual run, calculates the CLI and `Az` version, selects changed packageable extensions, aligns metadata, generates changelogs, and opens `release/trigger-stable-release-<version>`. |
| Create stable tag | After the generated PR merges, validates its merge commit and creates the stable tag. |

Extension selection compares `*.cs` files under `src/Extensions/<Module>` and all files under
non-`Az` PowerShell module directories with the latest stable tag. `src/PS/Az` does not select an
extension; its rollup metadata is aligned separately.

Version selection writes a shared release plan containing each selected module's base version,
target version, direct/new/dependency reason, and dependency version transitions. Changelog
generation consumes that plan. Direct and new modules require authored upcoming notes;
dependency-only modules receive generated dependency-update notes. Every selected module gets a
matching versioned changelog section and manifest release notes, and Az release notes are refreshed
for every stable release.

Packageable extension versions are selected independently:

| Extension state | Version result |
| --- | --- |
| Changed existing packageable extension | Patch bump. |
| Existing packageable extension that depends on a bumped module | Patch bump so its exact dependency can advance. |
| New packageable extension | Keep its declared initial version. |
| Unchanged unrelated packageable extension | No change. |

When no stable tag exists, the first stable release treats every packageable extension as new. Each
keeps its declared initial version while module and rollup dependency metadata is aligned.

### Example: Compute changed

Assume the CLI is `0.1.2`, Compute is `0.1.0`, and the pipeline detects a `*.cs` change under
`src/Extensions/Compute` or any change under `src/PS/Compute` since the latest stable tag. For a
patch release:

| File | Generated change |
| --- | --- |
| `src/CLI/Core.Cli.csproj` | `Version` and `InformationalVersion`: `0.1.2` to `0.1.3`; `AssemblyVersion` and `FileVersion`: `0.1.2.0` to `0.1.3.0`. |
| `src/PS/Az/Az.psd1` | `ModuleVersion`: `0.1.2` to `0.1.3`; Compute dependency: `0.1.0` to `0.1.1`; include Compute notes when present. |
| `src/Extensions/Compute/Extensions.Compute.csproj` | `Version`: `0.1.0` to `0.1.1`. |
| `src/PS/Compute/Compute.psd1` | `ModuleVersion`: `0.1.0` to `0.1.1`; update `ReleaseNotes` when upcoming notes exist. |
| `src/PS/Compute/ChangeLog.md` | If `## Upcoming Release` has notes, move them under `## Version 0.1.1`. |
| `CHANGELOG.md` | Add `## v0.1.3 - <release timestamp>`, including merged PR notes and Compute notes when present. |
| `src/Extensions/<Dependent>/Extensions.<Dependent>.csproj`, `src/PS/<Dependent>/<Dependent>.psd1`, and its `Az.psd1` entry | If present, patch-bump the exact dependent and align its Compute dependency and rollup version. |

Compute uses its own patch sequence; it does not become `0.1.3` just because the CLI does.

A matching extension project and module manifest identify a packageable extension. A missing module
changelog, `Az` rollup dependency, or project version fails release preparation. Before updates,
unchanged packageable extensions must already have aligned project, manifest, and rollup versions;
after updates, all packageable extensions must be aligned.

## Generated release PR checks

The generated-release rules apply only to a same-repository PR authored by
`x-engineering-agent[bot]`, titled `{DevOps} Trigger stable release <version>`, and using the matching
`release/trigger-stable-release-<version>` branch.

CI allowlists `CHANGELOG.md`, `src/CLI/Core.Cli.csproj`, matching extension projects, and each
module's `ChangeLog.md` and matching `.psd1`, including `src/PS/Az/Az.psd1`. It verifies extension,
child-module, and Az dependency versions agree; each bumped module has a matching versioned
changelog section and manifest release notes; the root changelog starts with the target release; and
Az release notes begin with the target Az version.

An existing `.csproj` may change only direct version-property lines while preserving property names,
property counts, and all non-version structure and content. Project renames, additions, deletions,
and formatting or EOF-only changes outside direct version-property lines fail validation.

## After the release PR merges

The main-branch tag stage checks the merged release commit again. The CLI project must change at
least one supported stable-version line (`Version` or `VersionPrefix`, `InformationalVersion`,
`AssemblyVersion`, or `FileVersion`) and no other content. Each changed extension project must
contain exactly one `<Version>` replacement and no other change. If validation succeeds, the
pipeline creates this immutable tag:

```text
v<major>.<minor>.<patch>.<UTC yyyyMMddTHHmmssZ>
```

The tag starts the stable build, signing, and publishing flow. Unexpected changes skip tag creation.

## Retrying release preparation

Before rerunning the same target version after an abandoned generated PR:

1. Merge any required release-automation fix into `main`.
2. Close the old generated release PR.
3. Delete `release/trigger-stable-release-<version>`.
4. Trigger stable-release preparation again.

Closing the PR does not delete its branch; the pipeline will fail while the deterministic release
branch still exists.
