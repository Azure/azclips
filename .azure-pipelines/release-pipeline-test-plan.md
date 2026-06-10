# Cirrus Release Pipeline Test Plan

Test the flow in layers so build, handoff, signing, and publishing failures are easy to isolate.

## Static Validation

Run local whitespace validation before registering or updating pipelines:

```bash
git diff --check -- .azure-pipelines
```

Also open the YAML files in VS Code and confirm there are no schema errors for the new public entry pipelines and release entry pipelines. Local schema warnings for `EsrpCodeSigning@5` can be treated as editor/schema limitations if Azure DevOps accepts the task.

## Pipeline Registration

Register or update pipeline definitions in the expected projects:

```text
public project:
  cirrus-alpha-build   -> .azure-pipelines/cirrus-alpha-build.yml
  cirrus-beta-build    -> .azure-pipelines/cirrus-beta-build.yml

release project:
  cirrus-trigger-stable-release -> .azure-pipelines/cirrus-trigger-stable-release.yml
  cirrus-stable-build -> .azure-pipelines/cirrus-stable-build.yml
  cirrus-stable-sign -> .azure-pipelines/cirrus-stable-sign.yml
  cirrus-stable-publish -> .azure-pipelines/cirrus-stable-publish.yml
```

Authorize pipeline resources when Azure DevOps prompts for access to build pipelines across or within projects.

## Alpha Build Smoke Test

Test `cirrus-alpha-build` with a disposable PR targeting `main`:

- Push a branch and open a PR to `main`.
- Push another commit to the same PR while validation is running.
- Confirm the obsolete PR validation is canceled or superseded and the latest PR commit is the one validated.
- Confirm version resolution uses the `pr-alpha` channel and produces a version like `X.Y.Z-alpha.yyyyMMdd.prN.buildId`.
- Confirm the run publishes `Distribution_Final` with `archives/`, `extensions/`, and `manifest.txt`.
- Confirm the artifacts are public Azure DevOps pipeline artifacts only; no signing, notarization, or GitHub Release is produced.

## Beta Build Smoke Test

Test `cirrus-beta-build` with a controlled `main` update:

- Complete a test PR into `main`, or use a temporary protected-branch test setup if production `main` should not be touched.
- Confirm `cirrus-beta-build` starts from the `main` update.
- Push or merge another `main` update while the first run is active to confirm `batch: true` collapses pending work into a build of the latest `main` tip.
- Confirm version resolution uses the `beta` channel and produces a version like `X.Y.Z-beta.yyyyMMdd.buildId`.
- Confirm the run publishes `Distribution_Final`.
- Confirm the artifacts are public Azure DevOps pipeline artifacts only; no signing, notarization, or GitHub Release is produced.

## Stable Trigger Smoke Test

Test `cirrus-trigger-stable-release` in both of its paths: manual PR creation and post-merge tag creation.

Manual PR creation:

- Queue `cirrus-trigger-stable-release` manually again in the `release` project.
- Confirm the preflight logs show `System.TeamProject=release`.
- Select `bump_type=patch`, `minor`, or `major`.
- Confirm `GitHubToken` is configured so the pipeline can create the release PR automatically.
- Confirm it creates a branch like `release/trigger-stable-release-X.Y.Z`.
- Confirm that branch changes only `src/CLI/Core.Cli.csproj` stable version metadata:
  - `Version` or `VersionPrefix` is `X.Y.Z`.
  - `InformationalVersion` is `X.Y.Z`.
  - `AssemblyVersion` is `X.Y.Z.0`.
  - `FileVersion` is `X.Y.Z.0`.
- Confirm the PR title is `Trigger stable release X.Y.Z`.

Manual PR review:

- Review the generated PR.
- Confirm the version bump is the intended patch, minor, or major change.
- Merge the PR to `main` manually.

Post-merge tag creation:

- Confirm the merge to `main` starts a second `cirrus-trigger-stable-release` run in the `release` project because `src/CLI/Core.Cli.csproj` changed.
- Confirm the `TriggerStableRelease` stage is skipped because `Build.Reason` is CI, not `Manual`.
- Confirm the `CreateStableTag` stage runs.
- Confirm the logs say the triggering commit only changed stable version metadata in `src/CLI/Core.Cli.csproj`.
- Confirm the logs show the latest stable tag and allowed next versions.
- Confirm it creates and pushes the matching `vX.Y.Z` tag on the exact `Build.SourceVersion` commit that triggered the tag run.

Stable build handoff:

- Confirm pushing the tag automatically starts `cirrus-stable-build` in the `release` project.
- Confirm `cirrus-stable-build` runs from `refs/tags/vX.Y.Z`.
- Confirm version resolution uses the `stable` channel and produces exactly `X.Y.Z`, with no alpha or beta suffix.
- Confirm `cirrus-stable-build` publishes `Distribution_Final` with version `X.Y.Z` from the tag.

Negative guard test:

- Create a throwaway PR that edits `src/CLI/Core.Cli.csproj` without changing only stable version metadata, such as changing a package reference.
- Merge it to `main` in a test setup.
- Confirm `cirrus-trigger-stable-release` starts because the path filter matches.
- Confirm `CreateStableTag` exits without creating a tag because the project file changed outside stable version metadata.

Use the real production version, such as `v0.1.1`, only when intentionally testing the production release path. The tag must be stable SemVer: `vX.Y.Z`, the tagged commit must be contained in `origin/main`, and the tag version must match the CLI version in the tagged source.

## Stable Signing Test

Test `cirrus-stable-sign` carefully:

- Confirm a successful release-project `cirrus-stable-build` automatically starts `cirrus-stable-sign` in the `release` project.
- Confirm `resources.pipeline.stableBuild.runID` matches the triggering stable build.
- Confirm `Distribution_Signed` is produced.

## Stable Publish Test

Test `cirrus-stable-publish` carefully:

- Confirm a successful release-project `cirrus-stable-sign` automatically starts `cirrus-stable-publish` in the `release` project.
- For a non-production smoke test, point `GitHubTargetRepo` or the GitHub service connection at a test repository before running the publish stage.
- Confirm `resources.pipeline.stableSign.runID` matches the triggering stable signing run.
- Confirm the GitHub release is created for the existing stable tag `vX.Y.Z`, not a build-scoped `dist-*` tag.
- Confirm the GitHub release task publishes assets from `Distribution_Signed` only.

## Expected Evidence

```text
PR update -> cirrus-alpha-build -> Distribution_Final
main update -> cirrus-beta-build -> Distribution_Final
stable tag -> cirrus-stable-build -> Distribution_Final
stableBuild run id -> cirrus-stable-sign -> Distribution_Signed
stableSign run id -> cirrus-stable-publish -> GitHub release
```
