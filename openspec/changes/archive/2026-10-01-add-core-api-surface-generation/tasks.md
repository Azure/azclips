## 1. Self-Contained Tool

- [x] 1.1 Create `tools/ApiSurface/GenApi/GenApi.csproj` and add the minimal vendored GenAPI and API-symbol-loading source required by the executable.
- [x] 1.2 Add `tools/ApiSurface/GenApi/upstream.json` with the upstream repository, tag, commit, copied source paths, and local adaptations.
- [x] 1.3 Add `tools/ApiSurface/GenerateApi.targets` to invoke the local GenAPI executable with `$(TargetPath)` and `@(ReferencePath)` and write output under `artifacts/api-surface/Core`.

## 2. Generate Command

- [x] 2.1 Add `tools/ApiSurface/Generate-CoreApi.ps1` to build the local tool, replace the Core API artifact directory, discover `src/Core/*/*.csproj`, and invoke each project in Release configuration with the tool-local MSBuild target.
- [x] 2.2 Generate API surface artifacts for all current Core projects with `pwsh tools/ApiSurface/Generate-CoreApi.ps1`.

## 3. Update Command

- [x] 3.1 Add `tools/ApiSurface/Update-GenApi.ps1` to read `global.json`, fetch the matching `dotnet/sdk` revision, refresh the selected upstream source, apply the local adaptations, and update `upstream.json`.
- [x] 3.2 Run `pwsh tools/ApiSurface/Update-GenApi.ps1` once to confirm the checked-in source can be reproduced by the update command.

## 4. Tool Documentation

- [x] 4.1 Add `tools/ApiSurface/README.md` documenting the generate command, update command, artifact location, upstream provenance, and source-update workflow.

## 5. Ignored Upstream Source

- [x] 5.1 Add a tool-local ignore file for the fetched GenAPI source and upstream license.
- [x] 5.2 Make `Generate-CoreApi.ps1` invoke `Update-GenApi.ps1` automatically when the ignored source is absent, and update the tool documentation.
