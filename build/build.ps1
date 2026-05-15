# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$GenerateHelpArtifacts
)

dotnet build "$PSScriptRoot/../cirrus.slnx" --configuration $Configuration

# Update .psd1 module manifests to reference the correct artifact paths
& "$PSScriptRoot/Set-ModuleConfiguration.ps1" -Configuration $Configuration

if ($GenerateHelpArtifacts)
{
    $repoRoot = Resolve-Path "$PSScriptRoot/.."
    $artifactsRoot = Join-Path $repoRoot "artifacts/$Configuration"
    $outputRoot = Join-Path $repoRoot "artifacts/help"

    dotnet run --project "$repoRoot/tools/Help/Cirrus.Help.Tool/Cirrus.Help.Tool.csproj" -- --artifacts-root "$artifactsRoot" --output-root "$outputRoot"

    # @TODO DANIEL: Remove this copy step once artifacts module layout is refined and Get-Help can consume packaged output directly.
    # For now, copy generated MAML XML into src/PS/<Module>/en-US so local Get-Help resolves examples and descriptions.
    Get-ChildItem "$repoRoot/src/PS" -Filter *.psd1 -Recurse | ForEach-Object {
        $psd1 = $_
        $manifest = Import-PowerShellDataFile $psd1.FullName
        $moduleRefs = @()
        if ($manifest.RootModule) { $moduleRefs += $manifest.RootModule }
        if ($manifest.NestedModules) { $moduleRefs += @($manifest.NestedModules) }
        if (-not $moduleRefs) { return }

        $dest = Join-Path $psd1.DirectoryName "en-US"
        foreach ($moduleRef in $moduleRefs)
        {
            $dllName = [System.IO.Path]::GetFileNameWithoutExtension($moduleRef)
            $xmlName = "$dllName.dll-Help.xml"
            $src = Join-Path $outputRoot "powershell/$xmlName"
            if (Test-Path $src)
            {
                New-Item -ItemType Directory -Force -Path $dest | Out-Null
                Copy-Item $src $dest -Force
            }
        }
    }
}
