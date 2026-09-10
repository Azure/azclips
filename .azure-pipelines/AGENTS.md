# Azure DevOps Pipelines — Agent Guide

Scoped guidance for the Azure DevOps (ADO) CI/CD pipelines under `.azure-pipelines/`.
Read this before editing any pipeline YAML. Packaging/build logic invoked by the
pipelines lives in `devops/scripts/`; keep the two in sync.

## Essentials
- Pipeline YAML is authored for **Azure DevOps**, not GitHub Actions (`.github/workflows/` is separate).
- Entry pipelines live directly in `.azure-pipelines/`; reusable templates live in `.azure-pipelines/templates/`.
- Never inline secrets. Credentials come from **variable groups**, **Azure Key Vault**, **approved Azure service connections** (e.g. `ame_esrp_connection`, `AzureSubscription`), and **built-in pipeline authentication** (e.g. `persistCredentials: true` on checkout).
- Signing and release-orchestration work runs in the **`release`** ADO project; PR (`pr-alpha`) and `beta` builds run in **`public`**. The stable-release pipeline runs its full build in **`release`**.
- Keep changes template-driven: prefer editing a shared template over duplicating steps in an entry pipeline.

## Entry Pipelines
| File | Trigger | Project | Purpose |
| --- | --- | --- | --- |
| `azclips-alpha-build.yml` | PR to any branch (`pr: '*'`, autoCancel) + daily scheduled `main` run (`cron '0 0 * * *'`, `always: true`, for Component Governance) | public | PR validation + unsigned build (`release_channel: pr-alpha`). |
| `azclips-beta-build.yml` | CI on `main` (batched) | public | Unsigned beta build; optional `sign_beta` param adds a `sign-beta` build tag. |
| `azclips-beta-sign.yml` | Pipeline resource `betaBuild` tagged `sign-beta` | release | ESRP-signs a completed beta build, verifies, runs component governance. |
| `azclips-trigger-stable-release.yml` | Manual (guarded) opens the PR; path-trigger on `src/CLI/Core.Cli.csproj` | release | Manual run bumps CLI version and opens the release PR via `gh` (Key Vault PAT). After the PR merges, a CI run of the same pipeline (`CreateStableTag`) creates the stable tag via `git tag`/`git push`. |
| `azclips-stable-release.yml` | Stable tag `v*.*.*.*T*Z` | release | Full build (in `release`) → ESRP sign → verify → publish GitHub release. |

## Template Layout (`templates/`)
- `templates/variables.yml` — top-level shared variables (pools, project names, repo, service connections, pipeline names).
- `templates/dist/variables/` — `common.yml`, `packaging.yml` (script/out roots, RIDs), `release.yml` (ESRP keycodes).
- `templates/dist/stages/` — stage-level composition (`build-unsigned-distribution.yml`, `sign-with-esrp.yml`, `validate-pull-request.yml`, `component-governance.yml`, `init-resolve-version.yml`, `publish-to-github-release.yml`, `verify-signed-distribution.yml`, `build-ps-modules.yml`).
- `templates/dist/jobs/` — per-platform build/validate/sign jobs (linux, windows, macos, ps-modules, nuget), plus `test-suite.yml`, `benchmark-suite.yml`, `finalize-build-summary.yml`.
- `templates/dist/steps/` — small reusable steps (`get-github-pat-token.yml`, `validate-release-project.yml`).

## Build Graph (unsigned distribution)
`build-unsigned-distribution.yml` fans out from an `Init` stage that resolves the version:

- `ComponentGovernance` and `BuildPSModules` depend on `Init`.
- `BuildCommon` depends on `Init`.
- `BuildLinux`, `BuildWindows`, `BuildMacOS`, `Test`, and `Benchmark` depend on `Init` + `BuildCommon` and can run in parallel.
- Each platform validation stage depends on `Init` + its corresponding platform build.
- `Finalize` depends on every preceding stage and uses `condition: always()` to aggregate their results into the build summary.

`ArtifactVersion` is produced by `Init` (`SetCoreVersion.ArtifactVersion`) and consumed by downstream stages via `stageDependencies`.

## Projects & Context Guards
- **`public`** — PR (`pr-alpha`) and `beta` builds. No ESRP access.
- **`release`** — beta signing, stable release trigger, and stable release. Has ESRP + GitHub PAT access.
- `templates/dist/steps/validate-release-project.yml` enforces that signing/release pipelines only run in `release`. Preserve these guards when editing.

## Variables, Groups & Secrets
- Shared values in `templates/variables.yml`: pools (`ubuntu-latest`, `windows-latest`, `macOS-15-arm64`, `macOS-15`), `public_project`/`release_project`, `github_target_repo: Azure/azclips`, `esrp_service_connection: ame_esrp_connection`, `macos_bundle_id`, and pipeline names.
- Variable groups (attached only when `System.TeamProject == 'release'`): `AME ESRP Variable Group`, `esrp-signing-codes`, `get-github-pat-token`.
- ESRP keycodes (`ESRPNuGetKeyCode`, `ESRPAuthenticodeKeyCode`, `ESRPThirdPartyKeyCode`, `ESRPStrongNameKeyCode`, `ESRPAppleKeyCode`) come from groups and are mapped in `release.yml`. Never hardcode them.
- The GitHub PAT is fetched at runtime from Key Vault via `steps/get-github-pat-token.yml`; do not persist it.
- Some jobs also rely on **service connections** (`ame_esrp_connection` for ESRP, `AzureSubscription` for Key Vault) and **persisted checkout credentials** (`persistCredentials: true`, for checkout/fetch). Git tag/push and `gh` operations authenticate with the Key Vault `GH_TOKEN`, not the persisted checkout credentials — use these built-in mechanisms rather than embedding tokens.

## Packaging Roots (`packaging.yml`)
- `PackagingScriptsRoot = devops/scripts/packaging`
- `PackagingOutRoot = devops/out` (with `common/`, `linux/`, `windows/`, `macos/` subfolders)
- Default RIDs: `linux-x64`, `win-x64`, `osx-arm64`.
- Pipeline jobs shell out to `devops/scripts/` (`ci/`, `compliance/`, `packaging/`, `publishing/`). When changing a build step, update the script there rather than embedding logic in YAML.

## Artifacts
- `Final_Build_Distribution` — unsigned build output.
- `Final_Signed_Distribution` — stable ESRP-signed output.
- `Final_Signed_Beta_Distribution` — beta ESRP-signed output.
- `PSModules` — packaged PowerShell modules.

## Signing & Release Flow
- **Beta:** `azclips-beta-build` (public) → add `sign-beta` tag → `azclips-beta-sign` (release) consumes the tagged run via a pipeline resource → ESRP sign → verify.
- **Stable:** `azclips-trigger-stable-release` (release, manual) bumps the CLI version and opens a release PR via `gh`. After that PR merges to `main`, a CI run of the same pipeline (`CreateStableTag`, condition `IndividualCI`/`BatchedCI` on `refs/heads/main`) creates the tag `vX.Y.Z.yyyyMMddTHHmmssZ` via `git tag`/`git push` → the tag triggers `azclips-stable-release` → build → `sign-with-esrp` → component governance → verify → `publish-to-github-release`.
- macOS notarization uses `macos_bundle_id: com.microsoft.azclips.cli`.

## Editing Rules for Agents
- Change shared behavior in `templates/dist/**`, not by copy-pasting into entry pipelines.
- Keep project guards, branch/tag validation, and secret-source patterns intact.
- Match existing parameter naming (`ubuntu_pool`, `release_channel`, `source_artifact_name`, etc.) and pass pools through parameters.
- Validate YAML changes: check indentation, template `parameters`/`variables` wiring, and `dependsOn`/`stageDependencies` references before committing.
- Do not add new triggers or `pr:` scopes without confirming the intended project and channel.
