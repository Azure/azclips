[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$toolRoot = $PSScriptRoot
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $toolRoot '../..'))
$genApiRoot = Join-Path $toolRoot 'GenApi'
$vendorRoot = Join-Path $genApiRoot 'src'
$globalJsonPath = Join-Path $repoRoot 'global.json'

$globalJson = Get-Content -Path $globalJsonPath -Raw | ConvertFrom-Json
$sdkVersion = $globalJson.sdk.version
if (-not $sdkVersion)
{
    throw "No SDK version was found in $globalJsonPath."
}

$tag = "v$sdkVersion"
$repository = 'https://github.com/dotnet/sdk'
$archiveUrl = "$repository/archive/refs/tags/$tag.tar.gz"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-genapi-$([System.Guid]::NewGuid().ToString('N'))"
$archivePath = Join-Path $tempRoot 'sdk.tar.gz'
$extractPath = Join-Path $tempRoot 'sdk'

function Copy-SourceFiles
{
    param(
        [Parameter(Mandatory)]
        [string]$Source,

        [Parameter(Mandatory)]
        [string]$Destination,

        [string[]]$Exclude = @()
    )

    if (-not (Test-Path -LiteralPath $Source))
    {
        throw "Expected upstream source path was not found: $Source"
    }

    Get-ChildItem -LiteralPath $Source -Filter '*.cs' -File -Recurse |
        Where-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($Source, $_.FullName)
            $Exclude -notcontains $relativePath
        } |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($Source, $_.FullName)
            $destinationPath = Join-Path $Destination $relativePath
            $destinationDirectory = Split-Path -Parent $destinationPath
            New-Item -Path $destinationDirectory -ItemType Directory -Force | Out-Null
            Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
        }
}

function Write-ResourceClass
{
    param(
        [Parameter(Mandatory)]
        [string]$ResourceFile,

        [Parameter(Mandatory)]
        [string]$Namespace,

        [Parameter(Mandatory)]
        [string]$Destination
    )

    [xml]$resourceDocument = Get-Content -LiteralPath $ResourceFile -Raw
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("namespace $Namespace;")
    $lines.Add('')
    $lines.Add('internal static class Resources')
    $lines.Add('{')

    foreach ($dataNode in $resourceDocument.root.data)
    {
        $name = $dataNode.name
        $value = [string]$dataNode.value
        $literal = [System.Text.Json.JsonSerializer]::Serialize(
            $value,
            [string],
            [System.Text.Json.JsonSerializerOptions]$null)
        $lines.Add("    internal const string $name = $literal;")
    }

    $lines.Add('}')
    Set-Content -LiteralPath $Destination -Value $lines -Encoding utf8NoBOM
}

try
{
    New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null
    New-Item -Path $extractPath -ItemType Directory -Force | Out-Null

    Write-Host "Resolving dotnet/sdk $tag ..."
    $remoteRef = & git ls-remote $repository "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0 -or -not $remoteRef)
    {
        throw "Could not resolve dotnet/sdk tag $tag."
    }
    $commit = ($remoteRef -split '\s+')[0]

    Write-Host "Downloading $archiveUrl ..."
    Invoke-WebRequest -Uri $archiveUrl -OutFile $archivePath
    & tar -xzf $archivePath -C $extractPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not extract $archiveUrl."
    }

    $sourceRoot = Get-ChildItem -LiteralPath $extractPath -Directory | Select-Object -First 1
    if (-not $sourceRoot)
    {
        throw "The dotnet/sdk archive did not contain a source directory."
    }

    $genApiSource = Join-Path $sourceRoot.FullName 'src/Compatibility/GenAPI/Microsoft.DotNet.GenAPI'
    $toolSource = Join-Path $sourceRoot.FullName 'src/Compatibility/GenAPI/Microsoft.DotNet.GenAPI.Tool'
    $symbolSource = Join-Path $sourceRoot.FullName 'src/Compatibility/Microsoft.DotNet.ApiSymbolExtensions'
    $stagedVendorRoot = Join-Path $tempRoot 'vendor'

    $vendoredGenApi = Join-Path $stagedVendorRoot 'GenAPI'
    $vendoredTool = Join-Path $stagedVendorRoot 'Tool'
    $vendoredSymbols = Join-Path $stagedVendorRoot 'ApiSymbolExtensions'

    Copy-SourceFiles -Source $genApiSource -Destination $vendoredGenApi
    Copy-SourceFiles -Source $toolSource -Destination $vendoredTool
    Copy-SourceFiles -Source $symbolSource -Destination $vendoredSymbols -Exclude @('Logging/MSBuildLog.cs')

    Write-ResourceClass `
        -ResourceFile (Join-Path $genApiSource 'Resources.resx') `
        -Namespace 'Microsoft.DotNet.GenAPI' `
        -Destination (Join-Path $vendoredGenApi 'Resources.cs')
    Write-ResourceClass `
        -ResourceFile (Join-Path $symbolSource 'Resources.resx') `
        -Namespace 'Microsoft.DotNet.ApiSymbolExtensions' `
        -Destination (Join-Path $vendoredSymbols 'Resources.cs')

    if (Test-Path -LiteralPath $vendorRoot)
    {
        Remove-Item -LiteralPath $vendorRoot -Recurse -Force
    }
    Move-Item -LiteralPath $stagedVendorRoot -Destination $vendorRoot

    Copy-Item `
        -LiteralPath (Join-Path $sourceRoot.FullName 'LICENSE.txt') `
        -Destination (Join-Path $genApiRoot 'LICENSE.dotnet-sdk.txt') `
        -Force

    $manifest = [ordered]@{
        repository = $repository
        tag = $tag
        commit = $commit
        copiedPaths = @(
            'src/Compatibility/GenAPI/Microsoft.DotNet.GenAPI/**/*.cs'
            'src/Compatibility/GenAPI/Microsoft.DotNet.GenAPI.Tool/Program.cs'
            'src/Compatibility/Microsoft.DotNet.ApiSymbolExtensions/**/*.cs'
        )
        localAdaptations = @(
            'Exclude ApiSymbolExtensions/Logging/MSBuildLog.cs because it depends on MSBuild task assemblies.'
            'Generate Resources.cs from each upstream Resources.resx file.'
            'Build the sources with the tool-local GenApi.csproj.'
            'Suppress upstream CA2241 in the tool-local project without patching vendored source.'
        )
    }

    $manifest |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $genApiRoot 'upstream.json') -Encoding utf8NoBOM

    Write-Host "Updated GenAPI from dotnet/sdk $tag ($commit)."
}
finally
{
    if (Test-Path -LiteralPath $tempRoot)
    {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
