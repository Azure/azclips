Summary: Manage Cirrus extensions and extensions packages.

Description: Install, list, update, and remove extensions that extend Cirrus functionality. Extensions can be installed from a package feed URL or from a local .nupkg file. Installed extensions are registered with Cirrus and loaded at startup.

### Install an extension from a feed

```bash
azv2 extension add --name MyExtension --version 1.0.0 --feed-url https://packages.example.com/nuget
```

### Install an extension from a local file

```bash
azv2 extension add --path ./MyExtension.1.0.0.nupkg
```

### PowerShell: Install from feed

```powershell
# Extension PowerShell support pending
```
