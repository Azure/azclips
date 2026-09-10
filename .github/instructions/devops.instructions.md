---
applyTo: [".azure-pipelines/**", "devops/scripts/**"]
description: Use when editing or reviewing Azure DevOps pipeline YAML, CI/CD templates, or build, packaging, signing, and publishing scripts.
---

# DevOps & CI/CD Instructions

These rules attach when creating or modifying Azure DevOps pipeline YAML
(`.azure-pipelines/**`) or build/packaging/signing scripts (`devops/scripts/**`).
They are a thin, always-on layer over the authoritative guides — read those before
non-trivial work:

- Pipelines: [`.azure-pipelines/AGENTS.md`](../../.azure-pipelines/AGENTS.md)
- Scripts: [`devops/scripts/AGENTS.md`](../../devops/scripts/AGENTS.md)

## Non-negotiables
- **Secrets:** never inline credentials. Use variable groups, Azure Key Vault, approved
  Azure service connections (`ame_esrp_connection`, `AzureSubscription`), and built-in
  pipeline auth (`persistCredentials: true`). Do not echo or persist tokens/PATs.
- **Project context:** signing and release-orchestration run in the `release` project;
  PR (`pr-alpha`) and `beta` builds run in `public`; the stable-release pipeline builds
  in `release`. Keep the `validate-release-project.yml` guards intact.
- **Template-driven:** change shared behavior in `.azure-pipelines/templates/dist/**`
  rather than duplicating steps into an entry pipeline.
- **Keep reusable logic in scripts:** put reusable build/packaging logic in
  `devops/scripts/**`; pipeline-specific orchestration, conditions, and short inline
  validation steps may remain in YAML. When changing a build step, prefer editing the
  script over growing inline logic. Update the parity sibling (`stage_agent.*`,
  `inject_builtin_extensions.*`) together when editing one side.
- **Keep this guidance current:** when a change alters CI/CD behavior — a new or renamed
  stage/job/pipeline/script, trigger, project context, variable group, secret source,
  artifact name, RID, or output/staging path — update the matching guide **in the same PR**:
  pipeline changes → `.azure-pipelines/AGENTS.md`, script changes → `devops/scripts/AGENTS.md`,
  and this file when the guardrails themselves change. Treat the docs as part of the change, not a follow-up.

## When editing pipelines
- Preserve existing triggers/`pr:` scopes and project guards; don't add new triggers
  without confirming the intended project and channel.
- Keep RID/output-root and artifact names aligned with
  `.azure-pipelines/templates/dist/variables/packaging.yml`.
- Match existing parameter names (`ubuntu_pool`, `release_channel`, `source_artifact_name`,
  …) and pass pools through parameters.
- Validate YAML wiring before committing: indentation, `parameters`/`variables` templates,
  and `dependsOn`/`stageDependencies` references.

## When editing scripts
- Keep `set -euo pipefail` in bash; don't add or strip a shebang just to normalize
  (most `.ps1` here are invoked via `pwsh -File` and intentionally have none).
- Resolve paths from the script location and preserve env-var override points
  (e.g. `${OUT_ROOT:-…}`); don't assume the caller's working directory.
- Treat `packaging/common/**` parity pairs (`stage_agent.*`, `inject_builtin_extensions.*`)
  as cross-platform; scripts under `linux/`, `macos/`, `windows/` are intentionally OS-specific.
- Packaging build outputs go under `devops/out/`; other scripts use their own defaults or
  pipeline-provided staging/output parameters (e.g. compliance defaults to
  `artifacts/compliance/`, signing receives explicit staging roots). Respect each script's
  existing output/staging parameters rather than forcing everything into `devops/out/`.
- If a script is called by a pipeline job, verify the matching
  `templates/dist/jobs/*.yml` still passes the expected env vars/args after your change.

## Validation
- Bash: `bash -n <script>.sh`
- PowerShell: `pwsh -NoProfile -File <script>.ps1` where safely supported (prefer a
  `-WhatIf`/dry-run path when the script mutates state).
- Python: `python3 -m py_compile <script>.py`
- Keep changes small and include validation notes in the PR.
