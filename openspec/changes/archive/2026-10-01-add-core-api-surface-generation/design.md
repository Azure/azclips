## Context

The current Core API review files were produced by building GenAPI from `dotnet/sdk` source in session storage and invoking it directly over Release assemblies. The useful pattern from `azure-sdk-for-net` is to invoke GenAPI through MSBuild after project references have been resolved, avoiding hard-coded framework and package reference paths.

This repository only needs a handy review tool at this stage. Generated files remain ignored artifacts, and the tool does not need tests, validation infrastructure, CI integration, or checked-in API baselines.

## Goals / Non-Goals

**Goals:**

- Keep the complete implementation under `tools/ApiSurface`.
- Provide one command to regenerate API surfaces for all projects directly beneath `src/Core`.
- Provide one command to update the vendored GenAPI implementation.
- Use each project's resolved MSBuild references.
- Write generated files under `artifacts/api-surface/Core`.

**Non-Goals:**

- Add tests or output-validation infrastructure.
- Enforce API compatibility or breaking-change policy.
- Check generated API listings into source control.
- Add the tool to the main solution or normal product build.
- Prescribe package-feed configuration.
- Generate listings outside `src/Core`.

## Decisions

### Use one self-contained tool directory

Everything required to maintain and run the tool will live under:

```text
tools/ApiSurface/
├── Generate-CoreApi.ps1
├── Update-GenApi.ps1
├── GenerateApi.targets
├── README.md
└── GenApi/
    ├── .gitignore
    ├── GenApi.csproj
    ├── upstream.json
    └── src/                 # ignored, downloaded source
```

No implementation file, target import, documentation file, or project registration is required elsewhere in the repository. The only output outside this directory is generated content under `artifacts/api-surface/Core`.

### Expose exactly two commands

Generate all Core API surfaces:

```powershell
pwsh tools/ApiSurface/Generate-CoreApi.ps1
```

Refresh the vendored GenAPI implementation:

```powershell
pwsh tools/ApiSurface/Update-GenApi.ps1
```

The generation script downloads the source through the update command when `GenApi/src` is absent, builds the local tool, discovers `src/Core/*/*.csproj`, and invokes each project with `GenerateApi.targets` as a temporary custom MSBuild import. The update script reads the SDK version from `global.json`, selects the matching `dotnet/sdk` tag, replaces the ignored source files, and updates `upstream.json`.

### Resolve references through MSBuild

`GenerateApi.targets` will run after the project build and pass these values to the local GenAPI executable:

- `$(TargetPath)` as the input assembly;
- `@(ReferencePath)` as assembly references; and
- `artifacts/api-surface/Core/<AssemblyName>.<TargetFramework>.cs` as output.

The generation script supplies the target file through an MSBuild custom-import property, so Core projects and repository-wide build props do not need modification.

### Keep generated output temporary

The generation command replaces `artifacts/api-surface/Core` on each run and writes one file per assembly and target framework. The directory is already ignored by Git and is intended only for local API review.

### Keep upstream refresh mechanical

`upstream.json` remains tracked and records the upstream repository, tag, commit, copied source paths, and local adaptation files. `GenApi/src` and the downloaded upstream license are ignored. `Update-GenApi.ps1` performs the same source-selection and adaptation steps on every update so maintainers do not manually reconstruct the tool.

The update command changes only files under `tools/ApiSurface/GenApi`. A clean checkout requires network access on its first generation; subsequent generation uses the local ignored source until it is explicitly refreshed or removed.

## Risks / Trade-offs

- **An upstream source-layout change can break the update command** → Fail with the missing upstream path and update the small source mapping in `Update-GenApi.ps1`.
- **The local source cache can become old** → Run the explicit update command when new GenAPI behavior is desired; routine API generation remains independent of upstream availability after bootstrap.
- **A clean checkout needs GitHub access** → Have the generation command automatically invoke the updater only when the ignored source is missing.
- **The upstream GenAPI projects rely on broader SDK build infrastructure** → Keep the local project and adaptation files stable while refreshing only the required implementation source.
- **Reference arguments can contain spaces** → Have the MSBuild target use quoting-safe argument or response-file handling.

## Migration Plan

1. Add the self-contained `tools/ApiSurface` directory.
2. Add the generation command and ignored artifact output.
3. Add the upstream update command and provenance manifest.
4. Document the two commands in the tool-local README.

Rollback consists of removing `tools/ApiSurface` and `artifacts/api-surface/Core`. Product builds and binaries are unaffected.

## Open Questions

None.
