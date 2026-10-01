# Core API Surface Tool

This self-contained wrapper generates C# API listings for every project directly under `src/Core`. The upstream GenAPI source is downloaded locally and ignored by Git.

## Generate Core API surfaces

From the repository root:

```powershell
pwsh tools/ApiSurface/Generate-CoreApi.ps1
```

The command downloads the GenAPI source matching `global.json` when it is missing, builds the local tool, builds each Core project in Release configuration, and writes one listing per assembly and target framework to:

```text
artifacts/api-surface/Core/
```

The output directory is ignored by Git and is intended for local review.

## Update GenAPI

To refresh the vendored GenAPI implementation:

```powershell
pwsh tools/ApiSurface/Update-GenApi.ps1
```

The command reads the SDK version from `global.json`, downloads the matching `dotnet/sdk` tag, refreshes the selected sources under the ignored `GenApi/src` directory, regenerates resource accessors, copies the ignored upstream license, and updates the tracked `GenApi/upstream.json`.

The update command requires Git, `tar`, and network access to GitHub. API surface generation also requires that access on a clean checkout, then uses the local ignored source on subsequent runs.

Review changes under `tools/ApiSurface/GenApi` after updating before accepting the new capabilities.
