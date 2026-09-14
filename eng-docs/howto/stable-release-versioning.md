---
title: Stable release versioning
summary: Contributor rules, CI protections, and stable-release version automation.
---

# Stable release versioning

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
| Module `src/PS/<Module>/ChangeLog.md` upcoming notes | Yes | Add customer-facing module notes under `## Upcoming Release`. |

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

## Stable release trigger

The `azclips-trigger-stable-release` pipeline performs these versioning steps:

1. Calculates the next CLI version from `patch`, `minor`, `major`, or an explicit stable SemVer
   `custom` value. The result must be greater than the current CLI version.
2. Creates `release/trigger-stable-release-<version>`. The run fails if that branch already exists.
3. Updates the CLI version properties and the top-level `Az` module version.
4. Detects extension C# or PowerShell changes since the latest timestamped stable tag.
5. Patch-bumps each changed existing packageable extension independently.
6. Patch-bumps existing transitive dependents when their exact PowerShell dependency version must
   advance.
7. Preserves the declared initial version of a new extension and leaves unrelated extensions
   unchanged.
8. Aligns each affected extension project version, child module `ModuleVersion`, child dependency
   versions, and the `Az` rollup dependency version.
9. Moves module `## Upcoming Release` notes into a versioned section and generates the root
   `CHANGELOG.md` entry.
10. Opens a generated stable-release PR.

A packageable extension must have all of the following:

- `src/Extensions/<Module>/Extensions.<Module>.csproj`
- `src/PS/<Module>/<Module>.psd1`
- `src/PS/<Module>/ChangeLog.md`
- One active matching dependency in `src/PS/Az/Az.psd1`

Missing or inconsistent metadata fails release preparation.

## Generated release PR checks

Generated stable-release PRs are intentionally stricter than contributor PRs. CI permits only:

- `CHANGELOG.md`
- CLI project version properties
- Matching extension project files with version-only replacements
- Module changelogs
- Matching module manifests

For an existing `.csproj`, property structure and version-property cardinality must remain unchanged.
Formatting, EOF-only changes, non-version properties, added version properties, project renames, and
project additions or deletions fail validation.

## After the release PR merges

The main-branch tag stage validates the triggering commit again. It requires allowed release files,
a CLI version change, and exactly one removed and one added `<Version>` line in each changed extension
project, with no other extension project changes.

If validation succeeds, it creates an immutable tag in this form:

```text
v<major>.<minor>.<patch>.<UTC yyyyMMddTHHmmssZ>
```

That tag starts the stable build, signing, and publishing flow. If the commit contains unexpected
changes, tag creation is skipped.

## Retrying release preparation

Before rerunning the same target version after an abandoned generated PR:

1. Merge any required release-automation fix into `main`.
2. Close the old generated release PR.
3. Delete `release/trigger-stable-release-<version>`.
4. Trigger stable-release preparation again.

Closing the PR does not delete its branch; the pipeline will fail while the deterministic release
branch still exists.
