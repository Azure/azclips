# Azure DevOps Pipelines — Agent Guide

Scoped guidance for the Azure DevOps (ADO) CI/CD pipelines under `.azure-pipelines/`.
Read this before editing any pipeline YAML. Packaging/build logic invoked by the
pipelines lives in `devops/scripts/`; keep the two in sync. When you add or change a
stage, job, pipeline, trigger, variable/group, secret source, or artifact, update this
guide in the same PR so it stays accurate.

## Essentials
- Pipeline YAML is authored for **Azure DevOps**, not GitHub Actions (`.github/workflows/` is separate).
- Entry pipelines live directly in `.azure-pipelines/`; reusable templates live in `.azure-pipelines/templates/`.
- Never inline secrets. Credentials come from **variable groups**, **Azure Key Vault**, **approved Azure service connections** (e.g. `ame_esrp_connection`, `AzureSubscription`), and **built-in pipeline authentication** (e.g. `persistCredentials: true` on checkout).
- Signing and release-orchestration work runs in the **`release`** ADO project; PR (`pr-alpha`) and `beta` builds run in **`public`**. The stable-release pipeline runs its full build in **`release`**.
- Keep changes template-driven: prefer editing a shared template over duplicating steps in an entry pipeline.

## Entry Pipelines
| File | Trigger | Project | Purpose |
| --- | --- | --- | --- |
| `azclips-scenario-6-cli-install.yml` | Manual only (`trigger: none`, `pr: none`) | release | Scenario 6 full CLI installation and Ubuntu OIDC/deployment test; `include_windows_macos: true` also enables Windows 2022 and macOS 15 installation checks. Reuses `get-github-pat-token` and the existing Key Vault template for downloads. On Ubuntu, `naga-azclips-scenario6` supplies OIDC; AzCLIPS signs in and `tests/Scripts/CLI/Scenario6Pipeline.sh` deploys a unique storage account twice in pre-created `rg-azclips-scenario6`, verifies identity/count and expected ARM not-found failure, then always deletes only that run's storage account/deployment record with fresh federation and bounded deletion polling. An ownership marker gates cleanup; isolated `AZCLIPS_HOME` and separate always-run profile cleanup preserve unrelated state. `fail_after_deployment: true` deliberately fails after deployment to exercise cleanup; the run should remain failed. The resource group and service connection are retained. |
| `azclips-alpha-build.yml` | PR to any branch (`pr: '*'`, autoCancel) + daily scheduled `main` run (`cron '0 0 * * *'`, `always: true`, for Component Governance) | public | PR validation + unsigned build (`release_channel: pr-alpha`). |
| `azclips-beta-build.yml` | CI on `main` (batched) | public | Unsigned beta build; optional `sign_beta` param adds a `sign-beta` build tag. |
| `azclips-beta-sign.yml` | Daily scheduled `main` run (`cron '0 0 * * *'`, `always: true`) + pipeline resource `betaBuild` tagged `sign-beta` | release | Scheduled/manual runs default to the latest successful `main` beta build without requiring a tag; ESRP-signs the selected build, verifies signatures/content, and runs the full native lifecycle matrix against the signed archives. |
| `azclips-trigger-stable-release.yml` | Manual (guarded) opens the PR; path-trigger on `src/CLI/Core.Cli.csproj` | release | Manual run bumps CLI and changed extension/PowerShell module versions, then opens the release PR via `gh` (Key Vault PAT). After the PR merges, a CI run of the same pipeline (`CreateStableTag`) creates the stable tag via `git tag`/`git push`. |
| `azclips-stable-release.yml` | Stable tag `v*.*.*.*T*Z` | release | Full build (in `release`) → ESRP sign → verify → signed lifecycle validation → publish GitHub release. In parallel after verify, signed PowerShell images are built/validated and CLI + PowerShell images are published to the ACR repositories configured by the `Docker*Registry*`/`Docker*Repository` variables in the entry pipeline, then verified from ACR and from MCR after a 10-minute delay. |

## Template Layout (`templates/`)
- `templates/variables.yml` — top-level shared variables (pools, project names, repo, service connections, pipeline names).
- `templates/dist/variables/` — `common.yml`, `packaging.yml` (script/out roots, RIDs), `release.yml` (ESRP keycodes).
- `templates/dist/stages/` — stage-level composition (`build-unsigned-distribution.yml`, `sign-with-esrp.yml`, `validate-pull-request.yml`, `validate-signed-distribution.yml`, `component-governance.yml`, `init-resolve-version.yml`, `publish-docker-to-acr.yml`, `publish-to-github-release.yml`, `verify-signed-distribution.yml`, `build-ps-modules.yml`). The PR validation stage checks title/history conventions, blocks manual root changelog or project-version edits, and restricts release-bot stable PRs to generated metadata paths and version-only project changes.
- `templates/dist/jobs/` — per-platform build/validate/sign jobs (linux, windows, macos, ps-modules, nuget), signed PowerShell installer validation, plus `test-suite.yml`, `benchmark-suite.yml`, `finalize-build-summary.yml`.
- `templates/dist/steps/` — small reusable steps (`get-github-pat-token.yml`, `validate-release-project.yml`).

## Build Graph (unsigned distribution)
`build-unsigned-distribution.yml` fans out from an `Init` stage that resolves the version:

- `ComponentGovernance` and `BuildPSModules` depend on `Init`.
- `BuildCommon` depends on `Init`.
- `BuildLinux`, `BuildWindows`, `BuildMacOS`, `Test`, and `Benchmark` depend on `Init` + `BuildCommon` and can run in parallel.
- `BuildLinux` produces Lean and Full archives for glibc (`linux-x64`, `linux-arm64`) and musl (`linux-musl-x64`, `linux-musl-arm64`). Each archive receives one full installer, layout, and runtime validation in a compatible environment: glibc on Ubuntu and musl in Alpine. Arm64 runtime checks require a matching Arm64 agent.
- Each platform validation stage depends on `Init` + its corresponding platform build.
- The unit-test job restores and builds the complete solution before running tests with `--no-build`, ensuring exporter fixture tests see a complete, current set of extension artifacts.
- `build-unsigned-distribution.yml` retains the established combined Docker behavior behind `build_docker_images` (default `true`) and independently selects unsigned CLI and PowerShell images through `build_cli_docker_images` and `build_powershell_docker_images` (both default `true`). Alpha and beta therefore continue producing both image families from unsigned artifacts.
- Docker build, publish, and verification stages depend directly on `Init` and consume `SetCoreVersion.ArtifactVersion`; they do not independently derive a Docker version.
- ACR publish and the AMD64 published-image pull job follow classic release-agent convention and expect `az` on the AMD64 agent. The AMD64 job pulls every published digest and exports per-image pipeline artifacts; native verification jobs load those artifacts and run smoke tests, so ARM64 agents require Docker but not Azure CLI or ACR credentials.
- `publish-docker-to-acr.yml` builds PowerShell images from signed artifacts, then `ValidateSignedPowerShellDockerImages` loads each downloaded AMD64/ARM64 archive on a native agent and smoke-tests it (expected Az version from `powershell-version.txt`), mirroring `ValidateDockerAzureLinux3` for CLI images. `PublishDockerImagesToAcr` depends on it. Docker build jobs (signed and unsigned) only build and publish the image artifact; smoke testing happens once per image in the dedicated validation stages, not inside the build jobs.
- Docker publication follows classic retry behavior in both test and production: existing immutable version tags are retained and verified downstream rather than rebuilt under candidate tags.
- Both `azclips-stable-release.yml` and `azclips-stable-release-test.yml` set the target ACR repositories through their `Docker*RegistryName`, `Docker*RegistryServer`, and `Docker*Repository` variables, which are passed to `publish-docker-to-acr.yml`; nothing in the template or scripts hardcodes them. Repositories must be onboarded with MAR before publishing, and must start with `public/` or `unlisted/` so MCR serves them anonymously (the MCR path drops that prefix). Unlisted images allow anonymous pulls and public tag listing but are excluded from the public catalog and discovery portal. After ACR publish, a server-side 10-minute delay (`WaitForMcrSync`, matching the classic `Wait for MCR Sync` phase) runs in parallel with ACR verification; then `VerifyMcrDockerImages` checks, with one retry after a 2-minute gap, that each versioned manifest tag exists on `mcr.microsoft.com/<repo without the public/ or unlisted/ prefix>`, pulls it anonymously on native AMD64/ARM64 agents, and runs the smoke tests, which check the version (no digest comparison, as in the classic release). These stages always run in the stable and stable-test pipelines; alpha and beta builds never call this template, so they skip signed PowerShell images, ACR publish and MCR verification. Only immutable version tags are published; aliases are not promoted.
- CLI tags follow the classic `azurelinux3.0` convention; full architecture tags omit a `full` qualifier, and `VERSION-azurelinux3.0` combines them as the default full multi-architecture image. PowerShell tags follow classic `azurelinux-3.0` spelling.
- `Finalize` depends on every preceding stage and uses `condition: always()` to aggregate their results into the build summary.

`ArtifactVersion` is produced by `Init` (`SetCoreVersion.ArtifactVersion`) and consumed by downstream stages via `stageDependencies`.

## Projects & Context Guards
- **`public`** — PR (`pr-alpha`) and `beta` builds. No ESRP access.
- **`release`** — beta signing, stable release trigger, and stable release. Has ESRP + GitHub PAT access.
- `templates/dist/steps/validate-release-project.yml` enforces that signing/release pipelines only run in `release`. Preserve these guards when editing.

## Variables, Groups & Secrets
- Shared pool, project, repository, service-connection, bundle, and pipeline-name values are defined in `templates/variables.yml`.
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
- **Beta (on demand, unchanged):** Manually queue `azclips-beta-build` (public) with `sign_beta: true` → add `sign-beta` tag → `azclips-beta-sign` (release) consumes that exact completed tagged run via a pipeline resource → ESRP sign → verify → run all 16 Linux, Windows, and macOS lifecycle rows plus the signed PowerShell installer row against the exact signed archives.
- **Beta (daily):** `azclips-beta-sign` (release) also runs daily at 00:00 UTC on `main`, even without source changes, and resolves its `betaBuild` pipeline resource to the latest successfully completed `azclips-beta-build` (public) run on `refs/heads/main`, without requiring a `sign-beta` tag. Manual signing runs use the same default unless a resource version is explicitly selected. All paths pin downloads to the resolved resource run ID and use the same signing, verification, and lifecycle validation stages.
- **Stable:** `azclips-trigger-stable-release` (release, manual) bumps the CLI and top-level Az versions, detects extension code changes since the latest stable tag, independently patch-bumps each changed existing extension, preserves new extensions' declared initial versions, requires their PowerShell child changelogs, aligns their child manifests and Az dependencies, and opens a release PR via `gh`. After that PR merges to `main`, a CI run of the same pipeline (`CreateStableTag`, condition `IndividualCI`/`BatchedCI` on `refs/heads/main`) creates the tag `vX.Y.Z.yyyyMMddTHHmmssZ` via `git tag`/`git push` → the tag triggers `azclips-stable-release` → build → `sign-with-esrp` → component governance → verify → signed lifecycle validation → `publish-to-github-release` (and, in parallel, `publish-docker-to-acr`; the same stages run in `azclips-stable-release-test` with its own repository variables)..
- `validate-signed-distribution.yml` downloads the flat final signed artifact in each native matrix job, requires exactly one matching archive per RID/profile, stages its signed extension packages for the existing lifecycle harnesses, and passes the archive path explicitly. A separate Ubuntu job executes the bundled installer from the exact signed `azps-cmdlets` archive, imports a child module at its packaged version, and verifies numeric version inference. Validation never rebuilds, modifies, repacks, or resigns a publication candidate.
- macOS notarization uses `macos_bundle_id: com.microsoft.azclips.cli`. Outermost `.app`, `.framework`, `.xpc`, `.appex`, `.plugin`, and `.bundle` directories with Apple bundle metadata are sent to ESRP as complete signing units rather than signing their nested Mach-O files separately; unrecognized Mach-O-bearing code containers fail closed. Signing ZIPs are created and extracted on macOS with symlink preservation and remain opaque on Windows. Reconstructed payloads must pass strict `codesign`, Microsoft Team ID, hardened-runtime, timestamp, and required .NET entitlement checks before notarization ZIPs are created.

## Editing Rules for Agents
- Change shared behavior in `templates/dist/**`, not by copy-pasting into entry pipelines.
- Keep project guards, branch/tag validation, and secret-source patterns intact.
- Match existing parameter naming (`ubuntu_pool`, `release_channel`, `source_artifact_name`, etc.) and pass pools through parameters.
- Keep checkout tasks on the Azure Pipelines default of no automatic task retries.
- Validate YAML changes: check indentation, template `parameters`/`variables` wiring, and `dependsOn`/`stageDependencies` references before committing.
- Do not add new triggers or `pr:` scopes without confirming the intended project and channel.
