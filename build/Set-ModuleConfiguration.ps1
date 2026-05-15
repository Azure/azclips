# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$psd1Files = Get-ChildItem -Path "$PSScriptRoot/../src/PS" -Filter '*.psd1' -Recurse

foreach ($file in $psd1Files) {
    $content = Get-Content -Path $file.FullName -Raw

    # Replace both Debug and Release artifact paths with the target configuration
    $updated = $content -replace '/artifacts/(Debug|Release)/', "/artifacts/$Configuration/"

    if ($content -ne $updated) {
        Set-Content -Path $file.FullName -Value $updated -NoNewline
        Write-Host "Updated $($file.Name) to use '$Configuration' configuration"
    }
    else {
        Write-Host "$($file.Name) already using '$Configuration' configuration"
    }
}
