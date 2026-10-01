[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$toolRoot = $PSScriptRoot
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $toolRoot '../..'))
$genApiProject = Join-Path $toolRoot 'GenApi/GenApi.csproj'
$genApiEntryPoint = Join-Path $toolRoot 'GenApi/src/Tool/Program.cs'
$updateGenApiScript = Join-Path $toolRoot 'Update-GenApi.ps1'
$targetsPath = Join-Path $toolRoot 'GenerateApi.targets'
$coreRoot = Join-Path $repoRoot 'src/Core'
$outputRoot = Join-Path $repoRoot 'artifacts/api-surface/Core'

if (-not (Test-Path -LiteralPath $genApiEntryPoint))
{
    Write-Host 'GenAPI source is missing; downloading the version matching global.json ...'
    & $updateGenApiScript
}

if (Test-Path -LiteralPath $outputRoot)
{
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}
New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null

Write-Host 'Building the repository GenAPI tool ...'
& dotnet build $genApiProject --configuration Release
if ($LASTEXITCODE -ne 0)
{
    throw "GenAPI build failed with exit code $LASTEXITCODE."
}

$projects = Get-ChildItem -LiteralPath $coreRoot -Directory |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File } |
    Sort-Object -Property FullName

foreach ($project in $projects)
{
    Write-Host "Generating $($project.BaseName) ..."
    & dotnet build `
        $project.FullName `
        --configuration Release `
        '/t:GenerateCoreApiSurface' `
        "/p:CustomAfterMicrosoftCommonTargets=$targetsPath"
    if ($LASTEXITCODE -ne 0)
    {
        throw "API surface generation failed for $($project.FullName) with exit code $LASTEXITCODE."
    }
}

Write-Host "Core API surfaces generated in $outputRoot."
