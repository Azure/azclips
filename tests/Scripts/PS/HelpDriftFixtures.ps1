# Smoke fixtures for build/Test-PSHelpMarkdown.ps1.
#
# Verifies PSL13/PSL14 fire on the expected inputs and stay silent on clean input.
# Each scenario runs in a fresh temp module + child pwsh so DLL locks and PlatyPS
# state don't leak across scenarios.
#
# Prereqs: pwsh 7+, Microsoft.PowerShell.PlatyPS 1.0.1 available on the runner.
# Usage:   pwsh -NoProfile -File ./tests/Scripts/PS/HelpDriftFixtures.ps1

$ErrorActionPreference = 'Stop'

$repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$gateScript = Join-Path $repoRoot 'build/Test-PSHelpMarkdown.ps1'
if (-not (Test-Path $gateScript)) { throw "Gate script not found at $gateScript" }

# --- Cmdlet source templates --------------------------------------------------
# Each template compiles to HelpDrift.dll containing one exported Get-TestWidget cmdlet.
# Scenarios pair a "scaffold" template (used to generate the md) with a "validate" template
# (compiled over the same DLL right before the gate runs) to induce specific drift shapes.

$oneParamMandatoryCode = @'
using System.Management.Automation;
namespace HelpDrift {
    [Cmdlet(VerbsCommon.Get, "TestWidget")]
    public class GetTestWidgetCmdlet : PSCmdlet {
        [Parameter(Mandatory = true)] public string Name { get; set; }
        protected override void ProcessRecord() { }
    }
}
'@

$oneParamOptionalCode = @'
using System.Management.Automation;
namespace HelpDrift {
    [Cmdlet(VerbsCommon.Get, "TestWidget")]
    public class GetTestWidgetCmdlet : PSCmdlet {
        [Parameter] public string Name { get; set; }
        protected override void ProcessRecord() { }
    }
}
'@

$twoParamCode = @'
using System.Management.Automation;
namespace HelpDrift {
    [Cmdlet(VerbsCommon.Get, "TestWidget")]
    public class GetTestWidgetCmdlet : PSCmdlet {
        [Parameter(Mandatory = true)] public string Name { get; set; }
        [Parameter] public string Color { get; set; }
        protected override void ProcessRecord() { }
    }
}
'@

$twoParamReversedCode = @'
using System.Management.Automation;
namespace HelpDrift {
    [Cmdlet(VerbsCommon.Get, "TestWidget")]
    public class GetTestWidgetCmdlet : PSCmdlet {
        [Parameter] public string Color { get; set; }
        [Parameter(Mandatory = true)] public string Name { get; set; }
        protected override void ProcessRecord() { }
    }
}
'@

$namedSetsCode = @'
using System.Management.Automation;
namespace HelpDrift {
    [Cmdlet(VerbsCommon.Get, "TestWidget", DefaultParameterSetName = "ByName")]
    public class GetTestWidgetCmdlet : PSCmdlet {
        [Parameter(Mandatory = true, ParameterSetName = "ByName")] public string Name { get; set; }
        [Parameter(Mandatory = true, ParameterSetName = "ById")] public string Id { get; set; }
        protected override void ProcessRecord() { }
    }
}
'@

# --- Fixture helpers ----------------------------------------------------------

# Absolute path to System.Management.Automation — pass explicitly rather than by friendly
# name so Roslyn resolves against the running pwsh's SMA rather than a framework fallback.
$smaAssembly = [System.Management.Automation.Cmdlet].Assembly.Location

function New-ScratchModule
{
    param(
        [Parameter(Mandatory)][string]$TempRoot,
        [Parameter(Mandatory)][string]$CmdletCode,
        [Parameter(Mandatory)][string]$SmaAssembly
    )
    # Isolated child pwsh so PlatyPS + Import-Module's DLL locks release when it exits.
    pwsh -NoProfile -Command {
        param($tmp, $code, $sma)
        $ErrorActionPreference = 'Stop'
        $moduleDir = Join-Path $tmp 'HelpDrift'
        New-Item -ItemType Directory -Path $moduleDir -Force | Out-Null
        $dll = Join-Path $moduleDir 'HelpDrift.dll'
        Add-Type -OutputAssembly $dll -TypeDefinition $code -ReferencedAssemblies $sma | Out-Null
        New-ModuleManifest `
            -Path (Join-Path $moduleDir 'HelpDrift.psd1') `
            -RootModule 'HelpDrift.dll' `
            -CmdletsToExport 'Get-TestWidget' `
            -ModuleVersion '1.0.0'

        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
        Import-Module (Join-Path $moduleDir 'HelpDrift.psd1') -Force
        $helpDir = Join-Path $moduleDir 'help'
        New-Item -ItemType Directory -Path $helpDir -Force | Out-Null
        # New-MarkdownCommandHelp emits under <OutputFolder>/<ModuleName>/. Flatten one level so
        # the layout matches src/PS/<Module>/help/<Cmdlet>.md that the gate expects.
        New-MarkdownCommandHelp -ModuleInfo (Get-Module HelpDrift) -OutputFolder $helpDir -Force | Out-Null
        Get-ChildItem -Path (Join-Path $helpDir 'HelpDrift') -Filter '*.md' -File |
            Move-Item -Destination $helpDir -Force
        Remove-Item -Path (Join-Path $helpDir 'HelpDrift') -Recurse -Force -ErrorAction SilentlyContinue
    } -Args $TempRoot, $CmdletCode, $SmaAssembly
    if ($LASTEXITCODE -ne 0) { throw "New-ScratchModule failed (exit $LASTEXITCODE)" }
}

function Set-ScratchDll
{
    param(
        [Parameter(Mandatory)][string]$ModuleDir,
        [Parameter(Mandatory)][string]$CmdletCode,
        [Parameter(Mandatory)][string]$SmaAssembly
    )
    # Fresh child pwsh so the new DLL isn't loaded/locked in the parent.
    pwsh -NoProfile -Command {
        param($dir, $code, $sma)
        $ErrorActionPreference = 'Stop'
        $dll = Join-Path $dir 'HelpDrift.dll'
        Remove-Item -Path $dll -Force -ErrorAction SilentlyContinue
        Add-Type -OutputAssembly $dll -TypeDefinition $code -ReferencedAssemblies $sma | Out-Null
    } -Args $ModuleDir, $CmdletCode, $SmaAssembly
    if ($LASTEXITCODE -ne 0) { throw "Set-ScratchDll failed (exit $LASTEXITCODE)" }
}

# --- Scenario runner ----------------------------------------------------------

function Invoke-Scenario
{
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ScaffoldCode,
        [string]$ValidateCode,
        [scriptblock]$MutateMd,
        [string]$ExpectPsl
    )

    Write-Host ("=" * 60)
    Write-Host "Scenario: $Name"
    Write-Host ("=" * 60)

    $shortId = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $tmp = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) "helpdrift-$Name-$shortId")

    try
    {
        New-ScratchModule -TempRoot $tmp -CmdletCode $ScaffoldCode -SmaAssembly $smaAssembly

        $moduleDir = Join-Path $tmp 'HelpDrift'
        if ($ValidateCode)
        {
            Set-ScratchDll -ModuleDir $moduleDir -CmdletCode $ValidateCode -SmaAssembly $smaAssembly
        }

        if ($MutateMd)
        {
            & $MutateMd (Join-Path $moduleDir 'help/Get-TestWidget.md')
        }

        Write-Host "Invoking Test-PSHelpMarkdown.ps1 -ModuleRoot $tmp ..."
        $out  = pwsh -NoProfile -File $gateScript -ModuleRoot $tmp 2>&1 | ForEach-Object { $_.ToString() }
        $exit = $LASTEXITCODE

        Write-Host "--- output ---"
        $out | ForEach-Object { Write-Host "  $_" }
        Write-Host "--- exit: $exit ---"

        $joined = ($out -join "`n")

        if ([string]::IsNullOrEmpty($ExpectPsl))
        {
            if ($exit -ne 0) { throw "[$Name] expected exit 0 (clean); got $exit`n$joined" }
            if ($joined -match 'PSL\d+') { throw "[$Name] expected no PSL records; got:`n$joined" }
        }
        else
        {
            if ($exit -eq 0) { throw "[$Name] expected non-zero exit; got 0`n$joined" }
            if ($joined -notmatch $ExpectPsl) { throw "[$Name] expected /$ExpectPsl/ in output; got:`n$joined" }
        }

        Write-Host "[$Name] PASS`n"
    }
    finally
    {
        Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- Scenarios ----------------------------------------------------------------

# (a) Clean pass — md matches the cmdlet exactly.
Invoke-Scenario -Name 'clean-pass' `
    -ScaffoldCode $twoParamCode `
    -ExpectPsl $null

# (b) Declaration order differs, but parameter semantics are identical — must remain clean.
Invoke-Scenario -Name 'declaration-order-only' `
    -ScaffoldCode $twoParamCode `
    -ValidateCode $twoParamReversedCode `
    -ExpectPsl $null

Invoke-Scenario -Name 'normalized-unnamed-set' `
    -ScaffoldCode $twoParamCode `
    -MutateMd {
        param($md)
        . (Join-Path $repoRoot 'build/Convert-PSHelpSyntax.ps1')
        $content = Convert-PSHelpSyntax -Content (Get-Content -LiteralPath $md -Raw)
        if ($content -match '(?m)^### __AllParameterSets') { throw 'Internal heading not removed' }
        Set-Content -LiteralPath $md -Value $content -NoNewline
    } `
    -ExpectPsl $null

Invoke-Scenario -Name 'named-sets-clean' `
    -ScaffoldCode $namedSetsCode `
    -ExpectPsl $null

Invoke-Scenario -Name 'named-set-renamed' `
    -ScaffoldCode $namedSetsCode `
    -ValidateCode ($namedSetsCode.Replace('"ById"', '"ByResourceId"')) `
    -ExpectPsl 'PSL14_ParameterDrift'

Invoke-Scenario -Name 'named-default-changed' `
    -ScaffoldCode $namedSetsCode `
    -ValidateCode ($namedSetsCode.Replace('DefaultParameterSetName = "ByName"', 'DefaultParameterSetName = "ById"')) `
    -ExpectPsl 'PSL14_ParameterDrift'

# (c) Missing param — scaffold md against one-param cmdlet, then swap in two-param DLL.
Invoke-Scenario -Name 'missing-param' `
    -ScaffoldCode $oneParamMandatoryCode `
    -ValidateCode $twoParamCode `
    -ExpectPsl 'PSL14_ParameterDrift'

# (d) Orphan param — scaffold md against two-param cmdlet, then swap in one-param DLL.
Invoke-Scenario -Name 'orphan-param' `
    -ScaffoldCode $twoParamCode `
    -ValidateCode $oneParamMandatoryCode `
    -ExpectPsl 'PSL14_ParameterDrift'

# (e) Required flag flipped — scaffold with Mandatory=true, swap in Mandatory=false DLL.
Invoke-Scenario -Name 'required-flipped' `
    -ScaffoldCode $oneParamMandatoryCode `
    -ValidateCode $oneParamOptionalCode `
    -ExpectPsl 'PSL14_ParameterDrift'

# (f) Schema-corrupt md — clean cmdlet, but stomp the md's frontmatter to unparseable.
#     Either PSL13 (Test-MarkdownCommandHelp fires) or PSL14 (Import-MarkdownCommandHelp
#     throws in the drift-check try/catch) is acceptable — both signal "author must fix".
Invoke-Scenario -Name 'schema-corrupt' `
    -ScaffoldCode $oneParamMandatoryCode `
    -MutateMd {
        param($md)
        Set-Content -Path $md -Value 'not a valid PlatyPS md — no frontmatter, no anything.' -Encoding utf8 -NoNewline
    } `
    -ExpectPsl 'PSL1[34]_'

foreach ($fixture in @(
    @{ Name = 'unnamed'; Code = $twoParamCode }
    @{ Name = 'named'; Code = $namedSetsCode }
))
{
    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("help-generation-" + [guid]::NewGuid().ToString('N'))
    $psRoot = Join-Path $tmp 'src/PS'
    $buildRoot = Join-Path $tmp 'build'
    New-Item -ItemType Directory -Path $psRoot, $buildRoot -Force | Out-Null
    try
    {
        New-ScratchModule -TempRoot $psRoot -CmdletCode $fixture.Code -SmaAssembly $smaAssembly
        Copy-Item (Join-Path $repoRoot 'build/New-PSHelpMarkdown.ps1') $buildRoot
        Copy-Item (Join-Path $repoRoot 'build/Convert-PSHelpSyntax.ps1') $buildRoot
        $script = Join-Path $buildRoot 'New-PSHelpMarkdown.ps1'
        $md = Join-Path $psRoot 'HelpDrift/help/Get-TestWidget.md'
        Remove-Item -LiteralPath $md
        foreach ($refresh in @($false, $true, $true))
        {
            $arguments = @('-NoProfile', '-File', $script, '-Module', 'HelpDrift')
            if ($refresh) { $arguments += '-Refresh' }
            & pwsh @arguments
            if ($LASTEXITCODE -ne 0) { throw "Generation fixture $($fixture.Name) failed" }
            $content = Get-Content -LiteralPath $md -Raw
            if ($content -match '(?m)^### __AllParameterSets')
            {
                throw "Generation fixture $($fixture.Name) exposed the internal heading"
            }
            if ($fixture.Name -eq 'named' -and
                ($content -notmatch '(?m)^### ByName \(Default\)' -or $content -notmatch '(?m)^### ById'))
            {
                throw 'Generation lost named parameter sets'
            }
            if (@([regex]::Matches($content, '(?m)^## DESCRIPTION')).Count -ne 1)
            {
                throw 'Generation duplicated the description'
            }
            $out = pwsh -NoProfile -File $gateScript -ModuleRoot $psRoot 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Generated help has drift: $($out -join "`n")" }
        }
        Write-Host "[generation-$($fixture.Name)] PASS"
    }
    finally
    {
        Remove-Item -LiteralPath $tmp -Recurse -Force
    }
}

Write-Host "All HelpDrift scenarios passed."
# Explicit exit 0: the last Invoke-Scenario invokes the gate which exits 1 on the schema-corrupt
# scenario (asserted-away as expected). Without this, $LASTEXITCODE bleeds into the script's own
# exit code and GitHub Actions marks the step failed despite the visible PASS lines.
exit 0
