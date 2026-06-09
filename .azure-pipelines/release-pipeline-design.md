# Cirrus Release Pipeline Design

This document describes the intended production release pipeline shape for Cirrus distribution builds, signing, and publishing.

The main design principle is separation by trust boundary:

- The public Azure DevOps project builds and validates unsigned artifacts.
- The release Azure DevOps project signs, notarizes, and publishes approved artifacts.
- Signing and publishing should consume an explicit source build id instead of rebuilding in the release project.

## Pipeline Roles

| Pipeline | Project | Build | Sign | Publish | Purpose |
| --- | --- | --- | --- | --- | --- |
| `cirrus-pr-build` | public | Yes | No | No | PR validation and unsigned alpha artifacts. |
| `cirrus-rc-build` | public | Yes | No | No | Unsigned RC candidate artifacts from trusted branch context. |
| `cirrus-rc-signing` | release | No | Yes | No | Sign/notarize artifacts produced by a public RC build. |
| `cirrus-stable-release` | release | No | Yes | Yes | Sign/notarize approved artifacts and publish the stable release. |

The release project should not normally rebuild source. It should consume already-built artifacts from the public project using an explicit build run id.

## Existing Worker Pipelines

The current worker pipelines are:

```text
.azure-pipelines/cirrus-build.yml
.azure-pipelines/cirrus-signing.yml
.azure-pipelines/cirrus-publish.yml
```

`cirrus-build.yml` resolves the distribution version, builds packages, and validates unsigned artifacts.

After successful platform builds, the `Finalize` stage also publishes a consolidated `Distribution_Final` artifact. This artifact gathers the final unsigned distribution archives and extension packages into one layout for downstream release consumption:

```text
Distribution_Final/
  archives/
  extensions/
  manifest.txt
```

`cirrus-signing.yml` runs in the `release` project, consumes build artifacts by pipeline name/build id, and produces signed artifacts.

`cirrus-publish.yml` runs in the `release` project, consumes signed artifacts by pipeline name/build id, and publishes release assets.

Future entry pipelines should stay thin. They should hardcode release intent and reuse these worker behaviors instead of duplicating build/sign/publish logic.

## Versioning Policy

`cirrus-build.yml` resolves version values in the `Init(ResolveVersion)` stage.

Normal defaults are:

```text
artifact_version_override=auto
bump_type=patch
release_channel=auto
release_date=auto
pr_number=auto
sequence=auto
```

`auto` means:

| Parameter | Meaning |
| --- | --- |
| `artifact_version_override` | Read the committed CLI base version from `src/CLI/Core.Cli.csproj`. |
| `release_channel` | Infer from runtime context: PR -> `pr-alpha`, main -> `main-rc`, semver tag -> `stable`, otherwise PR-alpha fallback. |
| `release_date` | Use current UTC date as `yyyyMMdd`. |
| `pr_number` | Use Azure DevOps PR variables; fallback to `0` for manual non-PR validation runs. |
| `sequence` | Use `$(Build.BuildId)`. |

Expected version shapes:

| Channel | Example shape | Flow |
| --- | --- | --- |
| `pr-alpha` | `0.1.1-alpha.20260609.pr257.321374` | build |
| `main-rc` | `0.1.1-rc.20260609.321374` | build |
| `stable` | `0.1.1` | sign + publish after an approved source build |

## Public PR Build

Purpose: validate PRs and produce unsigned validation artifacts.

Recommended hardcoded intent:

```text
release_channel=auto
artifact_version_override=auto
release_date=auto
pr_number=auto
sequence=auto
```

In a real PR runtime context, `release_channel=auto` resolves to `pr-alpha`.

This pipeline should not use ESRP, signing variable groups, notarization credentials, or GitHub release publishing credentials.

## Public RC Build

Purpose: produce unsigned RC candidate artifacts that can later be signed in the release project.

Recommended hardcoded intent:

```text
release_channel=main-rc
artifact_version_override=auto
release_date=auto
sequence=auto
```

Output artifacts from this run become the source of truth for RC signing. The build run id must be recorded and passed to the release project signing pipeline.

## Release RC Signing

Purpose: sign and notarize a specific public RC build without publishing a stable release.

Inputs:

```text
source_project=<public project name>
source_pipeline_name=<public RC build pipeline name>
source_build_id=<public RC build run id>
source_artifact_name=Distribution_Final
signed_artifact_name=Distribution_Signed_RC
```

This pipeline must run in the `release` project because ESRP signing and notarization require release-only credentials and variable groups.

It should validate:

- `System.TeamProject` is `release`.
- `source_build_id` is present, numeric, and greater than zero.
- Artifacts are downloaded from the expected source project/pipeline.
- The signed artifact name is explicit and traceable.

## Stable Release

Purpose: sign/notarize approved artifacts and publish the stable release.

The stable release pipeline should not rebuild source in the release project by default.

Recommended flow:

```text
public stable or RC build run
  -> release stable signing
  -> publish signed artifacts to GitHub release
```

Inputs:

```text
source_project=<public project name>
source_pipeline_name=<approved public build pipeline name>
source_build_id=<approved public build run id>
source_artifact_name=Distribution_Final
distribution_version=<stable SemVer, for example 0.1.1>
signed_artifact_name=Distribution_Signed
```

This keeps provenance simple:

```text
commit -> public build run -> unsigned artifacts -> release signing run -> signed artifacts -> publish run
```

## Cross-Project Artifact Handoff

The public project produces unsigned artifacts. The release project consumes them by source project, pipeline name, build id, and artifact name.

The signing worker has release-project guardrails for the signing run itself, but its source artifact inputs are parameterized so it can consume unsigned artifacts produced by the public project.

Signing source inputs:

```text
source_project=<public project name>
source_pipeline_name=<public build pipeline name>
source_build_id=<public build run id>
source_artifact_name=Distribution_Final
```

The release project should still enforce that the signing pipeline itself runs in `release`.

## Guardrails

Required guardrails:

- Public build pipelines must not import ESRP/signing variable groups.
- Public build pipelines must not publish GitHub releases.
- Release signing/publish pipelines must fail outside the `release` project.
- Release signing/publish pipelines must require explicit numeric build ids.
- Release signing must consume artifacts from an approved source project/pipeline.
- Stable publish must use signed artifacts only.
- Stable publish should require explicit stable version confirmation.

## Implementation Plan

1. Keep `cirrus-build.yml` as the reusable build worker and continue centralizing version resolution there.
2. Keep `cirrus-signing.yml` as the release-project signing worker and consume `Distribution_Final` from the approved source build.
3. Keep `cirrus-publish.yml` as the release-project publish worker.
4. Add thin public entry pipeline for PR builds.
5. Add thin public entry pipeline for RC builds.
6. Add thin release entry pipeline for RC signing.
7. Add thin release entry pipeline for stable sign + publish.
8. Register public entry pipelines in the public project and release entry pipelines in the release project.

## Non-Goals

- Do not rebuild source in the release project for the normal stable release path.
- Do not duplicate package build logic across entry pipelines.
- Do not allow public pipelines to access signing or publish secrets.
- Do not compute a different stable version during signing or publishing; the build run artifacts remain the version source of truth.
