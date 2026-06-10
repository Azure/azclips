# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

dotnet build "$PSScriptRoot/../cirrus.slnx" --configuration $Configuration

# Update .psd1 module manifests to reference the correct artifact paths
& "$PSScriptRoot/Set-ModuleConfiguration.ps1" -Configuration $Configuration

# Generate tab-completion metadata (shared JSON for PowerShell and Bash)
$_completionsJson = $null
$_cliProject = "$PSScriptRoot/../src/CLI/Core.Cli.csproj"
if (Test-Path $_cliProject) {
    $_completionsJson = dotnet run --no-build --project $_cliProject -- --completions 2>$null | Where-Object { $_ -match '^\s*\{' }
}
if ($_completionsJson) {
    $_completionsJson -join '' | Set-Content -Path "$PSScriptRoot/../bin/azv2.completions.json" -Encoding utf8NoBOM
    Write-Host "Generated bin/azv2.completions.json" -ForegroundColor Green
}
