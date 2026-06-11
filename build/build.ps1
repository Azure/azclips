# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$GenerateHelpArtifacts
)

function Invoke-CirrusBuild
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Debug', 'Release')][string]$Configuration
    )

    dotnet build "$PSScriptRoot/../cirrus.slnx" --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }

    # Update .psd1 module manifests to reference the correct artifact paths.
    & "$PSScriptRoot/Set-ModuleConfiguration.ps1" -Configuration $Configuration
}

function Invoke-HelpValidationTool
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$ArtifactsRoot,
        [Parameter(Mandatory)][string]$OutputRoot
    )

    dotnet run --project "$RepoRoot/tools/Help/Cirrus.Help.Tool/Cirrus.Help.Tool.csproj" -- --artifacts-root "$ArtifactsRoot" --output-root "$OutputRoot"
    return $LASTEXITCODE
}

# Repairs PlatyPS 1.0.1 quirk: Export-MamlCommandHelp leaves fenced example bodies
# inside <maml:para> instead of populating <dev:code>, so `Get-Help -Examples`
# prints raw fence markers. We move each fenced paragraph's body into <dev:code>.
function Repair-MamlExampleCodeBlocks
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path
    )

    [xml]$doc = Get-Content -Path $Path -Raw

    $nsmgr = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $nsmgr.AddNamespace('maml', 'http://schemas.microsoft.com/maml/2004/10')
    $nsmgr.AddNamespace('command', 'http://schemas.microsoft.com/maml/dev/command/2004/10')
    $nsmgr.AddNamespace('dev', 'http://schemas.microsoft.com/maml/dev/2004/10')

    $changed = $false
    $examples = $doc.SelectNodes('//command:example', $nsmgr)
    foreach ($ex in $examples)
    {
        $codeNode = $ex.SelectSingleNode('dev:code', $nsmgr)
        if ($codeNode -and -not [string]::IsNullOrWhiteSpace($codeNode.InnerText))
        {
            continue
        }

        $intro = $ex.SelectSingleNode('maml:introduction', $nsmgr)
        if (-not $intro) { continue }

        $codeFragments = New-Object System.Collections.Generic.List[string]
        $parasToRemove = New-Object System.Collections.Generic.List[System.Xml.XmlNode]
        foreach ($para in $intro.SelectNodes('maml:para', $nsmgr))
        {
            $text = $para.InnerText
            if ([string]::IsNullOrWhiteSpace($text)) { continue }

            # Match a paragraph whose ENTIRE content is one fenced block (tagged or bare).
            if ($text -match '(?ms)^\s*```[a-zA-Z0-9_-]*\s*\r?\n(?<body>.*?)\r?\n```\s*$')
            {
                $codeFragments.Add($Matches['body'])
                $parasToRemove.Add($para)
            }
        }

        if ($codeFragments.Count -eq 0) { continue }

        if (-not $codeNode)
        {
            # <dev:code> belongs after <maml:introduction> for round-trip via Import-MamlCommandHelp.
            $codeNode = $doc.CreateElement('dev', 'code', 'http://schemas.microsoft.com/maml/dev/2004/10')
            $ex.InsertAfter($codeNode, $intro) | Out-Null
        }
        $codeNode.InnerText = ($codeFragments -join "`n`n")

        foreach ($p in $parasToRemove)
        {
            $intro.RemoveChild($p) | Out-Null
        }

        $changed = $true
    }

    if ($changed)
    {
        $doc.Save($Path)
    }
}

# Generates MAML XML for one PowerShell module via PlatyPS 1.x.
# Runs in an isolated child pwsh: parent process loads its own YamlDotNet via dotnet build,
# and PlatyPS bundles its own copy — same-name assembly conflict if loaded together.
function ConvertTo-CirrusModuleMaml
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$HelpDir,
        [Parameter(Mandatory)][string]$OutputFolder,
        [Parameter(Mandatory)][string]$ModuleName
    )

    pwsh -NoProfile -Command {
        param($helpDirArg, $outDirArg)
        $ErrorActionPreference = 'Stop'
        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
        Get-ChildItem -Path $helpDirArg -Filter *.md -File |
            ForEach-Object { Import-MarkdownCommandHelp -Path $_.FullName } |
            Export-MamlCommandHelp -OutputFolder $outDirArg -Force | Out-Null
    } -Args $HelpDir, $OutputFolder

    if ($LASTEXITCODE -ne 0)
    {
        throw "Microsoft.PowerShell.PlatyPS pipeline failed for module '$ModuleName' (exit $LASTEXITCODE)."
    }

    $produced = Get-ChildItem -Path $OutputFolder -Recurse -Filter '*.xml' -File
    if (-not $produced)
    {
        throw "PlatyPS produced no MAML for module '$ModuleName'."
    }

    return $produced
}

# Emits repaired MAML for every src/PS/<Module>/help/ corpus to both
# src/PS/<Module>/en-US (gitignored) and <ArtifactsRoot>/PS/<Module>/en-US.
function Publish-CirrusHelpArtifacts
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$ArtifactsRoot
    )

    $stagedPsRoot = Join-Path $ArtifactsRoot "PS"
    $moduleCount = 0

    Get-ChildItem "$RepoRoot/src/PS" -Directory | ForEach-Object {
        $moduleDir = $_
        $helpDir = Join-Path $moduleDir.FullName "help"
        if (-not (Test-Path $helpDir)) { return }
        if (-not (Get-ChildItem -Path $helpDir -Filter *.md -ErrorAction SilentlyContinue)) { return }

        $enUs = Join-Path $moduleDir.FullName "en-US"
        New-Item -ItemType Directory -Force -Path $enUs | Out-Null

        $tempOut = Join-Path ([System.IO.Path]::GetTempPath()) ("CirrusMaml-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tempOut | Out-Null
        try
        {
            $produced = ConvertTo-CirrusModuleMaml -HelpDir $helpDir -OutputFolder $tempOut -ModuleName $moduleDir.Name

            foreach ($xml in $produced)
            {
                Repair-MamlExampleCodeBlocks -Path $xml.FullName

                Copy-Item -Path $xml.FullName -Destination (Join-Path $enUs $xml.Name) -Force

                $stagedModuleDir = Join-Path $stagedPsRoot $moduleDir.Name
                if (Test-Path $stagedModuleDir)
                {
                    $stagedEnUs = Join-Path $stagedModuleDir "en-US"
                    New-Item -ItemType Directory -Force -Path $stagedEnUs | Out-Null
                    Copy-Item -Path $xml.FullName -Destination $stagedEnUs -Force
                }
            }

            $moduleCount++
        }
        finally
        {
            Remove-Item -Path $tempOut -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host "Generated MAML help for $moduleCount PowerShell module(s)."
}

Invoke-CirrusBuild -Configuration $Configuration

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

# Copy shell completion scripts from source to bin/
$_completionsDir = "$PSScriptRoot/../src/CLI/Completions"
foreach ($_script in @('azv2.completions.ps1', 'azv2.completions.bash')) {
    $_src = Join-Path $_completionsDir $_script
    if (Test-Path $_src) {
        Copy-Item $_src -Destination "$PSScriptRoot/../bin/$_script" -Force
    }
}
Write-Host "Copied completion scripts to bin/" -ForegroundColor Green

if ($GenerateHelpArtifacts)
{
    $repoRoot = Resolve-Path "$PSScriptRoot/.."
    $artifactsRoot = Join-Path $repoRoot "artifacts/$Configuration"
    $outputRoot = Join-Path $repoRoot "artifacts/help"

    # Ensure PlatyPS (and any other build-time PS modules) are installed and pinned.
    & "$PSScriptRoot/Install-BuildDependencies.ps1"

    $helpToolExitCode = Invoke-HelpValidationTool -RepoRoot $repoRoot -ArtifactsRoot $artifactsRoot -OutputRoot $outputRoot

    Publish-CirrusHelpArtifacts -RepoRoot $repoRoot -ArtifactsRoot $artifactsRoot

    # Propagate the help tool's exit code so CI fails when help validation fails.
    if ($helpToolExitCode -ne 0)
    {
        exit $helpToolExitCode
    }
}
