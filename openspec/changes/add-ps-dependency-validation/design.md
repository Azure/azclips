## Context

See proposal.md for motivation. The existing matrix bootstraps a framework-dependent PowerShell tool and tests globally installed modules. Its smoke workers intentionally preload dependencies, so they cannot establish automatic dependency resolution.

## Goals / Non-Goals

**Goals:** Exercise actual local NuGet packages and production import guards in isolated processes; retain diagnostics for unexpected failures; run before normal installation.

**Non-Goals:** Public-gallery publishing, PSResourceGet delivery, upgrades, live Azure calls, or changing the normal installation lifecycle.

## Decisions

- Resolve real packages with PowerShellGet Save-Module into fresh scenario directories. Unlike CurrentUser installation this does not mutate native Windows user module directories, while still exercising the package resolver. Existing Install-Module coverage remains in the lifecycle.
- Validate source-derived dependency identities and versions against NuGet metadata independently, then request each consumer alone and import without preloading.
- Negative repositories are private copies with a missing dependency or incompatible version. Restrict resolution to the named repository and imports to scenario and built-in module paths.
- Run fresh processes with deadlines and capture per-case results, inventory and diagnostics. Expected failures must identify the intended dependency, not merely return nonzero.
- Add Dependencies after Bootstrap and before Install, using the existing distribution container/toolchain. Scenarios own their scratch directories and cannot alter the normal lifecycle state.
- Test supported runtime discovery, missing framework diagnostics using a private runtime copy, and rejection under an older PowerShell installed only into temporary toolchain locations. The missing-framework probe must run from the orchestration process.
- Keep fixture tests offline and distinguish synthetic fixtures from production package integration.

## Risks / Trade-offs

- PowerShellGet uses shared repository registration: use unique names, remove only registrations created by a scenario, and never modify existing ones.
- Save-Module is not Install-Module lifecycle validation: document this distinction and retain the existing real install/uninstall phases.
- Runtime negative probes need an older tool/runtime download in production: bound commands and report provisioning failures rather than accepting them as expected rejection.
- Runtime/native loader messages vary by OS: assert stable error categories and required dependency identifiers rather than entire third-party output.
- Package version constraints and exact import guards can disagree: fail the metadata/import contract instead of preloading a convenient version.
