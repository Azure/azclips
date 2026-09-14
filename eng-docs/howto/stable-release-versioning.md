---
title: CLI, PowerShell, and extension versioning
summary: Contributor rules, CI protections, and stable-release version automation.
---

# CLI, PowerShell, and extension versioning

Release automation owns stable package versions and generated release notes. Contributors own
functional code and non-version project metadata.

## Contributor pull requests

| Change | Allowed? | CI behavior |
| --- | --- | --- |
| Product code, tests, and documentation | Yes | Normal build and test checks apply. |
| Non-version `.csproj` properties, references, or build settings | Yes | The release policy permits the change. |
| Direct project version properties | No | The PR policy fails. |
| A nested dependency version, such as `PackageReference/Version` | Yes | It is not treated as the project's release version. |
| Add or delete a `.csproj` | Yes | The release policy permits it in a contributor PR. New packageable extensions must include complete packaging metadata. |
| Rename a `.csproj` | No | The PR policy fails to prevent version-policy bypasses. |
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

The `azclips-trigger-stable-release` pipeline has four phases:

| Phase | Result |
| --- | --- |
| Choose release version | Calculates the next CLI and `Az` version from `patch`, `minor`, `major`, or `custom`. |
| Select extensions | Compares extension C# and PowerShell files with the latest stable tag. |
| Align metadata | Updates project, module, dependency, and `Az` rollup versions together. |
| Prepare release PR | Generates changelogs and opens `release/trigger-stable-release-<version>`. |

Extension versions are selected independently:

| Extension state | Version result |
| --- | --- |
| Changed existing extension | Patch bump. |
| Existing extension that depends on a bumped module | Patch bump so its exact dependency can advance. |
| New extension | Keep its declared initial version. |
| Unchanged unrelated extension | No change. |

### Example: Compute changed

Assume the CLI is `0.1.2`, Compute is `0.1.0`, and the pipeline detects a change under
`src/Extensions/Compute` or `src/PS/Compute` since the latest stable tag. For a patch release:

| File | Generated change |
| --- | --- |
| `src/CLI/Core.Cli.csproj` | CLI version properties: `0.1.2` to `0.1.3`. |
| `src/PS/Az/Az.psd1` | `ModuleVersion`: `0.1.2` to `0.1.3`; Compute dependency: `0.1.0` to `0.1.1`; include Compute notes when present. |
| `src/Extensions/Compute/Extensions.Compute.csproj` | `Version`: `0.1.0` to `0.1.1`. |
| `src/PS/Compute/Compute.psd1` | `ModuleVersion`: `0.1.0` to `0.1.1`; update `ReleaseNotes` when upcoming notes exist. |
| `src/PS/Compute/ChangeLog.md` | If `## Upcoming Release` has notes, move them under `## Version 0.1.1`. |
| `CHANGELOG.md` | Add the generated `0.1.3` release section, including Compute notes when present. |
| `src/Extensions/<Dependent>/Extensions.<Dependent>.csproj`, `src/PS/<Dependent>/<Dependent>.psd1`, and its `Az.psd1` entry | If present, patch-bump the exact dependent and align its Compute dependency and rollup version. |

Compute uses its own patch sequence; it does not become `0.1.3` just because the CLI does.

A packageable extension requires a matching project, module manifest, module changelog, and active
`Az` rollup dependency. Missing or inconsistent metadata fails the pipeline.

## Generated release PR checks

CI permits only generated changelogs, matching module manifests, and version-only project changes.
Any formatting, EOF-only, non-version, added-property, rename, add, or delete change in a generated
`.csproj` fails validation.

## After the release PR merges

The main-branch tag stage checks the merged release commit again. Each changed extension project must
contain exactly one `<Version>` replacement and no other change. If validation succeeds, the pipeline
creates this immutable tag:

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
