# JAYSON: A custom build script to unblock performance benchmarking GitHub action where PS modules looking for specific Debug directories
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$GenerateHelpArtifacts
)

$ErrorActionPreference = 'Stop'

function Invoke-AzclipsBuild
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Debug', 'Release')][string]$Configuration
    )

    dotnet build "$PSScriptRoot/../azclips.slnx" --configuration $Configuration
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

    dotnet run --project "$RepoRoot/tools/Help/Azclips.Help.Tool/Azclips.Help.Tool.csproj" -- --artifacts-root "$ArtifactsRoot" --output-root "$OutputRoot" | Out-Host
    return $LASTEXITCODE
}

function Write-Utf8NoBomFile
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Content,
        [switch]$NormalizeLf
    )

    if ($NormalizeLf)
    {
        # zsh completion scripts must be LF-only to avoid `^M` command parsing errors.
        $Content = $Content -replace "`r`n?", "`n"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

# Repairs PlatyPS 1.0.1 quirk: Export-MamlCommandHelp leaves fenced example bodies
# inside <maml:para> instead of populating <dev:code>, so `Get-Help -Examples`
# prints raw fence markers. Move code into <dev:code> and replace its U+0080
# separator paragraphs with real newlines, preserving description-before-code order.
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
        $hasCode = $codeNode -and -not [string]::IsNullOrWhiteSpace($codeNode.InnerText)

        $intro = $ex.SelectSingleNode('maml:introduction', $nsmgr)
        if (-not $intro) { continue }

        $codeFragments = New-Object System.Collections.Generic.List[string]
        $parasToRemove = New-Object System.Collections.Generic.List[System.Xml.XmlNode]
        foreach ($para in $intro.SelectNodes('maml:para', $nsmgr))
        {
            $text = $para.InnerText
            if ([string]::IsNullOrWhiteSpace($text)) { continue }

            # Match a paragraph whose ENTIRE content is one fenced block (tagged or bare).
            if (-not $hasCode -and $text -match '(?ms)^\s*```[a-zA-Z0-9_-]*\s*\r?\n(?<body>.*?)\r?\n```\s*$')
            {
                $codeFragments.Add($Matches['body'])
                $parasToRemove.Add($para)
            }
        }

        if ($codeFragments.Count -gt 0)
        {
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

        $previousPara = $null
        foreach ($para in @($intro.SelectNodes('maml:para', $nsmgr)))
        {
            if ($para.InnerText.Trim() -eq [string][char]0x80)
            {
                if ($previousPara) { $previousPara.InnerText += "`n" }
                $intro.RemoveChild($para) | Out-Null
                $changed = $true
            }
            elseif (-not [string]::IsNullOrWhiteSpace($para.InnerText))
            {
                $previousPara = $para
            }
        }

        # PowerShell drops whitespace-only paragraphs and leading code newlines.
        # Two trailing newlines on the description preserve the blank line before code.
        if ($previousPara -and $codeNode -and
            -not [string]::IsNullOrWhiteSpace($codeNode.InnerText) -and
            -not $previousPara.InnerText.EndsWith("`n`n"))
        {
            $previousPara.InnerText = $previousPara.InnerText.TrimEnd([char[]]"`r`n") + "`n`n"
            $changed = $true
        }
    }

    if ($changed)
    {
        $doc.Save($Path)
    }
}

# Generates MAML XML for one PowerShell module via PlatyPS 1.x.
# Runs in an isolated child pwsh: parent process loads its own YamlDotNet via dotnet build,
# and PlatyPS bundles its own copy — same-name assembly conflict if loaded together.
function ConvertTo-AzclipsModuleMaml
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
function Publish-AzclipsHelpArtifacts
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

        $tempOut = Join-Path ([System.IO.Path]::GetTempPath()) ("AzclipsMaml-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Force -Path $tempOut | Out-Null
        try
        {
            $produced = ConvertTo-AzclipsModuleMaml -HelpDir $helpDir -OutputFolder $tempOut -ModuleName $moduleDir.Name

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

Invoke-AzclipsBuild -Configuration $Configuration

# Copy shell completion scripts to artifacts/<Configuration>/Core.Cli/ for packaging
$_completionsDir = "$PSScriptRoot/../src/CLI/Completions"
$_cliArtifactDir = "$PSScriptRoot/../artifacts/$Configuration/Core.Cli"
if (Test-Path $_cliArtifactDir) {
    foreach ($_script in @('az.completions.ps1', 'az.completions.bash', 'az.completions.zsh')) {
        $_src = Join-Path $_completionsDir $_script
        if (Test-Path $_src) {
            Write-Utf8NoBomFile -Path "$_cliArtifactDir/$_script" -Content (Get-Content -Path $_src -Raw) -NormalizeLf
        }
    }
    Write-Host "Copied completion artifacts to artifacts/$Configuration/Core.Cli/" -ForegroundColor Green
}

if ($GenerateHelpArtifacts)
{
    $repoRoot = Resolve-Path "$PSScriptRoot/.."
    $artifactsRoot = Join-Path $repoRoot "artifacts/$Configuration"
    $outputRoot = Join-Path $repoRoot "artifacts/help"

    # Ensure PlatyPS (and any other build-time PS modules) are installed and pinned.
    & "$PSScriptRoot/Install-BuildDependencies.ps1"

    $helpToolExitCode = Invoke-HelpValidationTool -RepoRoot $repoRoot -ArtifactsRoot $artifactsRoot -OutputRoot $outputRoot

    Publish-AzclipsHelpArtifacts -RepoRoot $repoRoot -ArtifactsRoot $artifactsRoot

    # Propagate the help tool's exit code so CI fails when help validation fails.
    if ($helpToolExitCode -ne 0)
    {
        exit $helpToolExitCode
    }
}
