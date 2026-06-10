# Cirrus Release Pipeline Design

This document describes the intended production release pipeline shape for Cirrus distribution builds, signing, and publishing.

The main design principle is separation by trust boundary:

- The public Azure DevOps project builds and validates PR alpha and main beta unsigned artifacts.
- The release Azure DevOps project owns stable release orchestration, stable tag builds, signing, notarization, and publishing.
- Signing and publishing should consume an explicit source build id instead of rebuilding inside the signing/publish stages.

## Pipeline Roles

| Pipeline | Project | Build | Sign | Publish | Purpose |
| --- | --- | --- | --- | --- | --- |
| `cirrus-alpha-build` | public | Yes | No | No | PR validation and unsigned alpha artifacts downloadable from Azure DevOps. |
| `cirrus-beta-build` | public | Yes | No | No | Unsigned beta artifacts from trusted branch context. |
| `cirrus-trigger-stable-release` | release | No | No | No | Creates the stable release PR manually, then tags the merged version on `main`. |
| `cirrus-stable-build` | release | Yes | No | No | Unsigned stable candidate artifacts from an approved stable tag. |
| `cirrus-stable-sign` | release | No | Yes | No | Sign/notarize approved stable artifacts. |
| `cirrus-stable-publish` | release | No | No | Yes | Publish signed stable artifacts to GitHub Releases. |

Alpha and beta artifacts are public Azure DevOps pipeline artifacts only. They are not signed, notarized, or published to GitHub Releases. Stable releases are stricter: the approved tag starts `cirrus-stable-build` in the release project, `cirrus-stable-sign` signs that exact build resource, and `cirrus-stable-publish` publishes the signed artifacts.

## Archived Manual Utility Pipelines

The archived manual utility pipelines are kept for reference and emergency experiments, but they are not part of the production registration path:

```text
.azure-pipelines/_archive/cirrus-adhoc-build.yml
.azure-pipelines/_archive/cirrus-adhoc-sign.yml
.azure-pipelines/_archive/cirrus-adhoc-publish.yml
```

`cirrus-adhoc-build.yml` resolves the distribution version, builds packages, and validates unsigned artifacts for manual experiments or recovery scenarios.

After successful platform builds, the `Finalize` stage also publishes a consolidated `Distribution_Final` artifact. This artifact gathers the final unsigned distribution archives and extension packages into one layout for downstream release consumption:

```text
Distribution_Final/
  archives/
  extensions/
  manifest.txt
```

`cirrus-adhoc-sign.yml` runs in the `release` project, consumes build artifacts by pipeline name/build id, and produces signed artifacts.

`cirrus-adhoc-publish.yml` runs in the `release` project, consumes signed artifacts by pipeline name/build id, and publishes release assets.

Future entry pipelines should stay thin. They should hardcode release intent and reuse shared templates instead of duplicating build/sign/publish logic.

The build entry pipelines reuse `.azure-pipelines/templates/dist/stages/build-unsigned-distribution.yml` so PR alpha, main beta, and stable builds all produce the same `Distribution_Final` contract.

The release entry pipelines use Azure DevOps pipeline resources to avoid manually copying source build ids:

```text
cirrus-stable-sign
  resources.pipeline.stableBuild.runID
  -> source_build_id

cirrus-stable-publish
  resources.pipeline.stableSign.runID
  -> signed_build_id
```

`cirrus-stable-sign` is configured to trigger automatically from the release-project `cirrus-stable-build` pipeline after a stable tag build completes. `cirrus-stable-publish` is configured to trigger automatically from `cirrus-stable-sign` after signing completes.

## Trigger Semantics

`cirrus-alpha-build` should run for every update to an active PR that targets `main`. In Azure Repos, configure this as a branch policy build validation on `main`; in repository providers that honor YAML PR triggers, the `pr` block provides the same intent. PR validation should use latest-update behavior: YAML PR triggers use `autoCancel: true`, while Azure Repos branch policies should be configured to avoid piling up obsolete validation runs for superseded PR commits.

`cirrus-beta-build` should run for every new commit that lands on `main`. In a protected branch flow, that normally means every completed PR merge into `main`. If someone pushes directly to `main`, that commit should also trigger a beta build because it changed the trusted branch tip. Its CI trigger uses `batch: true` so multiple pending `main` updates collapse into one build of the latest branch tip when the pipeline is already running.

`cirrus-trigger-stable-release` must run in the `release` project because it can create release PRs and push stable tags. It has two paths. When queued manually, it always creates a PR that bumps `src/CLI/Core.Cli.csproj` to the intended stable version. After that PR merges to `main`, the same release-project pipeline runs from its CI trigger. The post-merge stage only creates a tag if the triggering commit changes exactly the stable version line in `src/CLI/Core.Cli.csproj`; other project-file edits are skipped.

`cirrus-stable-build` should run for approved stable tags, such as `v0.1.1`. `cirrus-stable-sign` should run after a successful stable build through its pipeline resource trigger, and `cirrus-stable-publish` should run after a successful stable signing run.

## Stable Release Source Of Truth

Use release tags, not release branches, as the normal stable release trigger.

The recommended production flow is:

```text
main receives PR merges
  -> cirrus-beta-build produces beta artifacts for each trusted main tip
  -> validate/promote a chosen commit
  -> queue cirrus-trigger-stable-release for patch, minor, or major
  -> review and merge the generated PR that updates src/CLI/Core.Cli.csproj to X.Y.Z
  -> cirrus-trigger-stable-release runs again from the main version change
  -> same pipeline creates stable tag vX.Y.Z on the main commit whose CLI version is X.Y.Z
  -> cirrus-stable-build produces unsigned stable artifacts with version X.Y.Z
  -> cirrus-stable-sign signs and notarizes from that stable build resource
  -> cirrus-stable-publish publishes signed artifacts to GitHub Releases
```

The stable tag is the immutable release decision. For example, tag `v0.1.1` must produce version `0.1.1`; the stable build should not compute a different stable version during signing or publishing.

The version comes back to `main` through the `cirrus-trigger-stable-release` pipeline. A manual run opens a normal PR that updates `src/CLI/Core.Cli.csproj` from the previous released version to the intended stable version. The PR updates `Version` or `VersionPrefix` to `X.Y.Z`, updates `InformationalVersion` to `X.Y.Z`, and updates both `AssemblyVersion` and `FileVersion` to `X.Y.Z.0`. After the PR is reviewed and merged, a CI run of the same pipeline tags the merge commit. This keeps the repository, tag, artifacts, and release notes aligned.

The post-merge tag stage validates that the triggering commit only changed stable version metadata in `src/CLI/Core.Cli.csproj`, then validates the CLI version on `main` against the latest stable tag. If the latest stable tag is `v0.1.0`, the only allowed next versions are:

```text
v0.1.1  patch
v0.2.0  minor
v1.0.0  major
```

The stable build validates that the tag version matches the CLI version in the tagged source. The tagged commit must also be contained in `origin/main`. Invalid tag names, tags on unmerged commits, tag/source version mismatches, and skipped versions fail before packaging.

Use release branches only when maintaining an older release line after `main` has moved on. For example, create `release/0.1` from the last `0.1.x` tag, cherry-pick the hotfix, validate it, and then create a new stable tag such as `v0.1.2` from the hotfix commit. The tag still triggers the stable build.

Release-project `cirrus-trigger-stable-release` handles both manual PR creation and post-merge tag creation, `cirrus-stable-build` is the tag-triggered release-project build, `cirrus-stable-sign` signs/notarizes the build output, and `cirrus-stable-publish` publishes the signed output to GitHub Releases.

## Versioning Policy

`cirrus-adhoc-build.yml` resolves version values in the `Init(ResolveVersion)` stage.

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
| `release_channel` | Infer from runtime context: PR -> `pr-alpha`, main -> `beta`, stable SemVer tag -> `stable`, otherwise PR-alpha fallback. |
| `release_date` | Use current UTC date as `yyyyMMdd`. |
| `pr_number` | Use Azure DevOps PR variables; fallback to `0` for manual non-PR validation runs. |
| `sequence` | Use `$(Build.BuildId)`. |

Expected version shapes:

| Channel | Example shape | Flow |
| --- | --- | --- |
| `pr-alpha` | `0.1.1-alpha.20260609.pr257.321374` | build |
| `beta` | `0.1.1-beta.20260609.321374` | build |
| `stable` | `0.1.1` | sign + publish after an approved source build |

For stable tag builds, the tag version wins only after validation. `refs/tags/v0.1.1` resolves to `0.1.1` only if the tagged source also has CLI version `0.1.1`.

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

## Public Beta Build

Purpose: produce unsigned beta artifacts for every trusted `main` update that can later be signed in the release project.

Recommended hardcoded intent:

```text
release_channel=beta
artifact_version_override=auto
release_date=auto
sequence=auto
```

Output artifacts from this run are public downloadable Azure DevOps pipeline artifacts. They are not published to GitHub Releases.

## Release Stable Build

Purpose: produce unsigned stable artifacts from an approved stable tag inside the release project.

Recommended hardcoded intent:

```text
release_channel=stable
artifact_version_override=auto
release_date=auto
sequence=auto
```

The stable build validates that the tag is a stable SemVer tag, that the tag version matches `src/CLI/Core.Cli.csproj`, and that the tagged commit is contained in `origin/main`. It publishes `Distribution_Final`, which becomes the only unsigned artifact source for stable signing.

## Stable Signing

Purpose: sign/notarize approved stable artifacts.

The stable signing pipeline should not rebuild source. It should consume the completed release-project stable build resource.

The stable signing entry pipeline declares this resource:

```yaml
resources:
  pipelines:
  - pipeline: stableBuild
    project: release
    source: cirrus-stable-build
    trigger: true
```

When the release-project stable build completes, Azure DevOps starts the stable signing pipeline and sets:

```text
source_build_id=$(resources.pipeline.stableBuild.runID)
```

Recommended flow:

```text
release stable build run
  -> stable signing
  -> signed stable artifacts
```

Inputs:

```text
source_project=<release project name>
source_pipeline_name=cirrus-stable-build
source_build_id=$(resources.pipeline.stableBuild.runID)
source_artifact_name=Distribution_Final
distribution_version=<stable SemVer, for example 0.1.1>
signed_artifact_name=Distribution_Signed
```

## Stable Publish

Purpose: publish signed stable artifacts to GitHub Releases.

The stable publish pipeline should not rebuild or sign. It should consume the completed release-project stable signing resource.
It publishes against the existing stable source tag `vX.Y.Z` derived from the signed artifact version and does not delete an existing GitHub release before creating the stable release.

The stable publish entry pipeline declares this resource:

```yaml
resources:
  pipelines:
  - pipeline: stableSign
    project: release
    source: cirrus-stable-sign
    trigger: true
```

When stable signing completes, Azure DevOps starts the stable publish pipeline and sets:

```text
signed_build_id=$(resources.pipeline.stableSign.runID)
```

Inputs:

```text
source_project=<release project name>
signed_pipeline_name=cirrus-stable-sign
signed_build_id=$(resources.pipeline.stableSign.runID)
signed_artifact_name=Distribution_Signed
```

This keeps provenance simple:

```text
commit -> cirrus-trigger-stable-release tag stage -> stable tag -> stable build run -> unsigned artifacts -> stable sign run -> signed artifacts -> stable publish run -> GitHub release
```

## Artifact Handoff

Build pipelines produce unsigned artifacts. Signing pipelines consume them by source project, pipeline name, build id, and artifact name.

The signing worker has release-project guardrails for the signing run itself. In the normal production release flow, stable signing consumes unsigned artifacts produced by the release-project stable build.

Signing source inputs:

```text
source_project=<public or release project name>
source_pipeline_name=<approved build pipeline name>
source_build_id=$(resources.pipeline.<alias>.runID)
source_artifact_name=Distribution_Final
```

The release project should still enforce that the signing pipeline itself runs in `release`.

The release pipeline test plan lives in [release-pipeline-test-plan.md](release-pipeline-test-plan.md).

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

1. Keep archived adhoc build/sign/publish YAMLs only as manual utility references.
2. Keep production entrypoints focused on `cirrus-alpha-build`, `cirrus-beta-build`, `cirrus-trigger-stable-release`, `cirrus-stable-build`, `cirrus-stable-sign`, and `cirrus-stable-publish`.
3. Keep build, signing, and publish behavior centralized in shared templates.
4. Add thin public entry pipeline for alpha PR builds.
5. Add thin public entry pipeline for beta builds.
6. Add thin release entry pipeline for triggering stable release PR creation and post-merge tag creation.
7. Add thin release entry pipeline for stable tag builds.
8. Add thin release entry pipeline for stable sign + publish.
9. Register public entry pipelines in the public project and release entry pipelines in the release project.
10. Authorize pipeline resources to read build pipelines/artifacts where Azure DevOps prompts.

## Non-Goals

- Do not rebuild source in the release project for the normal stable release path.
- Do not duplicate package build logic across entry pipelines.
- Do not allow public pipelines to access signing or publish secrets.
- Do not compute a different stable version during signing or publishing; the build run artifacts remain the version source of truth.
