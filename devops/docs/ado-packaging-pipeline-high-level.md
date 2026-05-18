# ADO Packaging Pipeline (High Level)

## Purpose
This pipeline builds release-ready packaging artifacts for Linux and Windows using a shared common build stage.

Primary goals:
- Keep pipeline runs manual (no CI or PR trigger).
- Keep Linux and Windows packaging paths isolated.
- Reuse shared build outputs to avoid duplicate work.
- Validate packaged outputs before final summary.

## Main Pipeline File
- `.azure-pipelines/ado_naga_distribution_release_ready.yml`

## Stage Flow
1. Init
- Prints run metadata and confirms manual run context.

2. BuildCommon
- Builds shared assets once.
- Publishes artifact: `Distribution_Common`.

3. BuildLinux
- Downloads `Distribution_Common`.
- Builds Linux package artifacts.
- Publishes artifact: `Distribution_Linux`.

4. BuildWindows
- Downloads `Distribution_Common`.
- Builds Windows package artifacts.
- Publishes artifact: `Distribution_Windows`.

5. ValidateLinux
- Downloads Linux + Common artifacts.
- Runs Linux packaged-flow validation.
- Publishes Linux simulation artifact when available.

6. ValidateWindows
- Downloads Windows + Common artifacts.
- Runs Windows packaged-flow validation.
- Publishes Windows simulation artifact when available.

7. Finalize
- Always runs.
- Prints stage result summary for all build/validate stages.

## Script Responsibilities (High Level)
- `devops/scripts/packaging/common/build_common.sh`
  - Restores/builds shared components.
  - Produces extension packages and built-in publish outputs.

- `devops/scripts/packaging/linux/build_linux_tarball.sh`
  - Produces Linux self-contained publish output.
  - Injects built-ins from common output.
  - Produces Linux tarball.

- `devops/scripts/packaging/windows/build_win_zip.sh`
  - Produces Windows self-contained publish output.
  - Injects built-ins from common output.
  - Produces Windows zip.

- `devops/scripts/packaging/linux/test_bundled_plus_ext_linux.sh`
  - Validates Linux packaged scenario.
  - Verifies built-in commands and optional extension install flow.

- `devops/scripts/packaging/windows/test_bundled_plus_ext_win.ps1`
  - Validates Windows packaged scenario.
  - On non-Windows hosts, runtime behavior can be skipped.

- `devops/scripts/packaging/local_testing/build_distribution_e2e.sh`
  - Local orchestration entrypoint for end-to-end packaging checks.

## Output Layout
Outputs are runtime-generated under `devops/out` and are not required in source control:
- `devops/out/common`
- `devops/out/linux`
- `devops/out/windows`

Simulation outputs are runtime-generated under `devops/sim`:
- `devops/sim/linux-tarball-bundled-plus-ext`
- `devops/sim/win-zip-bundled-plus-ext`

## Trigger and Security Posture
- Trigger mode is manual only (`trigger: none`, `pr: none`).
- Signing and injected governance/security checks are intentionally disabled for this packaging workflow.

## Local Run (High Level)
1. Clean temp outputs if needed:
- remove `devops/out` and `devops/sim`

2. Run e2e flow:
- `./devops/scripts/packaging/local_testing/build_distribution_e2e.sh --with-pwsh`

3. Inspect outputs:
- `devops/out/common`, `devops/out/linux`, `devops/out/windows`
- `devops/sim/linux-tarball-bundled-plus-ext`, `devops/sim/win-zip-bundled-plus-ext`

## Notes
- Common outputs are built once and reused by both platform builds.
- Linux/Windows packaging outputs are isolated to reduce cross-stage interference.
- Windows runtime validation should be trusted primarily in ADO Windows agents.
