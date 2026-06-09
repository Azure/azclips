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
  cirrus-pr-build      -> .azure-pipelines/cirrus-pr-build.yml
  cirrus-beta-build    -> .azure-pipelines/cirrus-beta-build.yml
  cirrus-trigger-stable-release -> .azure-pipelines/cirrus-trigger-stable-release.yml

release project:
  cirrus-stable-build   -> .azure-pipelines/cirrus-stable-build.yml
  cirrus-beta-signing   -> .azure-pipelines/cirrus-beta-signing.yml
  cirrus-stable-release -> .azure-pipelines/cirrus-stable-release.yml
```

Authorize pipeline resources when Azure DevOps prompts for access to build pipelines across or within projects.

## PR Build Smoke Test

Test `cirrus-pr-build` with a disposable PR targeting `main`:

- Push a branch and open a PR to `main`.
- Push another commit to the same PR while validation is running.
- Confirm the obsolete PR validation is canceled or superseded and the latest PR commit is the one validated.
- Confirm the run publishes `Distribution_Final` with `archives/`, `extensions/`, and `manifest.txt`.

## Beta Build Smoke Test

Test `cirrus-beta-build` with a controlled `main` update:

- Complete a test PR into `main`, or use a temporary protected-branch test setup if production `main` should not be touched.
- Confirm `cirrus-beta-build` starts from the `main` update.
- Push or merge another `main` update while the first run is active to confirm `batch: true` collapses pending work into a build of the latest `main` tip.
- Confirm the run publishes `Distribution_Final`.

## Beta Signing Handoff Test

Test `cirrus-beta-signing` handoff:

- Let a successful `cirrus-beta-build` complete.
- Confirm Azure DevOps automatically starts `cirrus-beta-signing` in the `release` project.
- In the `Preflight` logs, confirm `resources.pipeline.betaBuild.runID` matches the source `cirrus-beta-build` run id.
- Confirm signing downloads `Distribution_Final` from the public project and publishes `Distribution_Signed_Beta`.

## Stable Trigger Smoke Test

Test `cirrus-trigger-stable-release` in both of its modes: manual PR creation and post-merge tag creation.

Manual dry run:

- Queue `cirrus-trigger-stable-release` manually.
- Set `dry_run=true`.
- Select `bump_type=patch`, `minor`, or `major`.
- Confirm the run prints the current CLI version, target stable version, intended release branch, and intended tag.
- Confirm it does not push a branch, create a PR, or create a tag.

Manual PR creation:

- Queue `cirrus-trigger-stable-release` manually again.
- Set `dry_run=false`.
- Set `create_pr=true` if `GitHubToken` and `gh` are available, otherwise set `create_pr=false` and use the printed manual PR URL.
- Confirm it creates a branch like `release/trigger-stable-release-X.Y.Z`.
- Confirm that branch changes only `src/CLI/Core.Cli.csproj` and only the stable version line.
- Confirm the PR title is `Trigger stable release X.Y.Z`.

Manual PR review:

- Review the generated PR.
- Confirm the version bump is the intended patch, minor, or major change.
- Merge the PR to `main` manually.

Post-merge tag creation:

- Confirm the merge to `main` starts a second `cirrus-trigger-stable-release` run because `src/CLI/Core.Cli.csproj` changed.
- Confirm the `TriggerStableRelease` stage is skipped because `Build.Reason` is CI, not `Manual`.
- Confirm the `CreateStableTag` stage runs.
- Confirm the logs say the triggering commit only changed the stable version line in `src/CLI/Core.Cli.csproj`.
- Confirm the logs show the latest stable tag and allowed next versions.
- Confirm it creates and pushes the matching `vX.Y.Z` tag on the merged `main` commit.

Stable build handoff:

- Confirm pushing the tag automatically starts `cirrus-stable-build` in the `release` project.
- Confirm `cirrus-stable-build` runs from `refs/tags/vX.Y.Z`.
- Confirm `cirrus-stable-build` publishes `Distribution_Final` with version `X.Y.Z` from the tag.

Negative guard test:

- Create a throwaway PR that edits `src/CLI/Core.Cli.csproj` without changing only the stable version line, such as changing a package reference.
- Merge it to `main` in a test setup.
- Confirm `cirrus-trigger-stable-release` starts because the path filter matches.
- Confirm `CreateStableTag` exits without creating a tag because the project file changed outside the stable version line.

Use the real production version, such as `v0.1.1`, only when intentionally testing the production release path. The tag must be stable SemVer: `vX.Y.Z`, the tagged commit must be contained in `origin/main`, and the tag version must match the CLI version in the tagged source.

## Stable Release Test

Test `cirrus-stable-release` carefully:

- Confirm a successful release-project `cirrus-stable-build` automatically starts `cirrus-stable-release` in the `release` project.
- For a non-production smoke test, point `GitHubTargetRepo` or the GitHub service connection at a test repository before running the publish stage.
- Confirm `resources.pipeline.stableBuild.runID` matches the triggering stable build.
- Confirm the signed artifact is produced before allowing the GitHub release task to publish real assets.

## Expected Evidence

```text
PR update -> cirrus-pr-build -> Distribution_Final
main update -> cirrus-beta-build -> Distribution_Final
cirrus-beta-build run id -> cirrus-beta-signing -> Distribution_Signed_Beta
stable tag -> cirrus-stable-build -> Distribution_Final
stableBuild run id -> cirrus-stable-release -> Distribution_Signed -> GitHub release
```
