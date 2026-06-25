# Local Runbook: Release Extension Lookup Order

@TODO: delete before merge

This runbook validates the runtime lookup policy in a local Release sandbox:

1. Check install-root `Extensions/` first.
2. If not found, check versioned user extensions (`~/.cirrus/extensions` equivalent).
3. If still not found, return not-installed guidance/error.

## Preconditions

- Run from repo root.
- .NET SDK from `global.json` is installed.
- Windows PowerShell (`pwsh`) examples below.

## 1) Create an isolated Release sandbox

```powershell
$repo = "c:/Users/dlanguiller/source/repos/azclips"
$sandbox = Join-Path $env:TEMP "cirrus-release-lookup"
$cliOut = Join-Path $sandbox "cli"
$homeRoot = Join-Path $sandbox "home"
$versionedRoot = Join-Path $homeRoot ".cirrus/extensions"

if (Test-Path $sandbox) { Remove-Item -Recurse -Force $sandbox }
New-Item -ItemType Directory -Path $cliOut | Out-Null
New-Item -ItemType Directory -Path $versionedRoot -Force | Out-Null
```

## 2) Publish Release CLI and built-ins into install-root `Extensions/`

```powershell
dotnet publish "$repo/src/CLI/Core.Cli.csproj" -c Release -o $cliOut

$builtIns = @(
  @{ Proj = "$repo/src/Extensions/Extension/Extensions.Extension.csproj"; Asm = "Microsoft.Azure.Cirrus.Extensions.Extension" },
  @{ Proj = "$repo/src/Extensions/Config/Extensions.Config.csproj"; Asm = "Microsoft.Azure.Cirrus.Extensions.Config" },
  @{ Proj = "$repo/src/Extensions/Login/Extensions.Login.csproj"; Asm = "Microsoft.Azure.Azclips.Extensions.Login" }
)

foreach ($b in $builtIns) {
  $dest = Join-Path $cliOut ("Extensions/" + $b.Asm)
  New-Item -ItemType Directory -Path $dest -Force | Out-Null
  dotnet publish $b.Proj -c Release -o $dest
}
```

## 3) Publish test extension payloads to both locations

Use `Resources` as the validation target.

```powershell
$resourcesProj = "$repo/src/Extensions/Resources/Extensions.Resources.csproj"

$installRootResources = Join-Path $cliOut "Extensions/Microsoft.Azure.Cirrus.Extensions.Resources"
$homeResources = Join-Path $versionedRoot "Microsoft.Azure.Cirrus.Extensions.Resources/0.1.0"

New-Item -ItemType Directory -Path $installRootResources -Force | Out-Null
New-Item -ItemType Directory -Path $homeResources -Force | Out-Null

dotnet publish $resourcesProj -c Release -o $installRootResources
dotnet publish $resourcesProj -c Release -o $homeResources
```

## 4) Set process-local environment for isolated home fallback

This avoids mutating your real user extension directory.

```powershell
$env:AssemblyLoadingOptions__VersionedInstallationDir = $versionedRoot
```

## 5) Validate policy order

### Case A: Install-root wins when both exist

```powershell
& (Join-Path $cliOut "az.exe") group --help
```

Expected:

- Command resolves successfully.
- `Resources` is discovered from install-root `Extensions/` first.

### Case B: Fallback to versioned user extensions

```powershell
Remove-Item -Recurse -Force $installRootResources
& (Join-Path $cliOut "az.exe") group --help
```

Expected:

- Command still resolves successfully.
- Fallback source is `$versionedRoot`.

### Case C: Not found when absent in both

```powershell
Remove-Item -Recurse -Force (Join-Path $versionedRoot "Microsoft.Azure.Cirrus.Extensions.Resources")
& (Join-Path $cliOut "az.exe") group --help
```

Expected:

- Structured not-installed / install-guidance error.
- No silent fallback to unrelated paths.

## 6) Optional visibility checks

```powershell
& (Join-Path $cliOut "az.exe") extension list --output json
& (Join-Path $cliOut "az.exe") --help
```

Use these outputs as evidence for the runbook matrix.

## 7) Cleanup

```powershell
Remove-Item Env:AssemblyLoadingOptions__VersionedInstallationDir -ErrorAction SilentlyContinue
if (Test-Path $sandbox) { Remove-Item -Recurse -Force $sandbox }
```

## Notes

- This runbook is intentionally local/manual and does not modify CI scripts.
- CI packaging scripts already inject built-ins into `Extensions/` for release artifacts; this runbook mirrors that layout for local verification.
