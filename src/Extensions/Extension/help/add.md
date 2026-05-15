---
summary: Install a new extension from a package feed or local file.
status: ga
schema: 1.0.0
online version:
---

Description: Add a new extension to Cirrus. Extensions can be installed from a NuGet package feed URL or from a local .nupkg file. Specify the extension name and version (for feed installs) or the path to the .nupkg file (for local installs). Once installed, extensions are automatically loaded on next CLI startup.

### Install from official feed (CLI)

```bash
azv2 extension add --name MyExtension --version 1.0.0 --feed-url https://api.nuget.org/v3/index.json
```

### Install from custom feed (CLI)

```bash
azv2 extension add --name MyExtension --version 1.0.0 --feed-url https://packages.example.com/nuget
```

### Install from local file (CLI)

```bash
azv2 extension add --path ./MyExtension.1.0.0.nupkg
```

### Install extension (PowerShell)

```powershell
# Extension installation via PowerShell pending
```
