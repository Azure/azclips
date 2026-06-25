---
summary: Install an extension package from a feed or local file.
status: ga
schema: 1.0.0
online version:
---

Description: Install an extension package into the user extension directory from a NuGet feed or a local .nupkg file. Use `--name` for package-based installs or `--path` for local artifacts. After installation, the package is discoverable by subsequent Cirrus commands without any manual registry edits.

### Install from official feed

```bash
az extension add --name MyExtension --version 1.0.0 --feed-url https://api.nuget.org/v3/index.json
```

### Install from custom feed

```bash
az extension add --name MyExtension --version 1.0.0 --feed-url https://packages.example.com/nuget
```

### Install from local file

```bash
az extension add --path ./MyExtension.1.0.0.nupkg
```
