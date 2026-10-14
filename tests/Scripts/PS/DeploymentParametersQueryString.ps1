# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Body-level verification of the -QueryString rules.

.DESCRIPTION
    ARM carries a query string only on TemplateLink, which exists only when the template comes
    from a URI. So -QueryString requires -TemplateUri, it lands on TemplateLink.QueryString, and
    it is never embedded into the template URI or the parameters URI. This script loads the built
    Resources extension assembly and, via reflection, drives the processor's protected
    ValidateCommonInputs / BuildTemplate / BuildParameters to assert exactly that.

    This is a local body-inspection (no live Azure / no real SAS blob required). Run after a build.
#>

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$extDir = Join-Path $root 'artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources'
$cliDir = Join-Path $root 'artifacts/Debug/Core.Cli'
$extDll = Join-Path $extDir 'Microsoft.Azure.Azclips.Extensions.Resources.dll'

if (-not (Test-Path $extDll)) { throw "Extension DLL not found at $extDll. Build the solution first." }

# Resolve the extension's dependencies (Core.Abstractions, etc.) from the CLI output folder.
$probeDirs = @($extDir, $cliDir)
$resolver = [System.ResolveEventHandler]{
    param($s, $e)
    $name = ([System.Reflection.AssemblyName]::new($e.Name)).Name
    foreach ($d in $probeDirs) {
        $candidate = Join-Path $d "$name.dll"
        if (Test-Path $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$asm = [System.Reflection.Assembly]::LoadFrom($extDll)
$procType = $asm.GetType('Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator', $true)

$flags = [System.Reflection.BindingFlags]'Instance,Public,NonPublic'

function Get-RequiredMethod {
    param(
        [Parameter(Mandatory)][Type]$Type,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][System.Reflection.BindingFlags]$Flags
    )
    $t = $Type
    while ($null -ne $t) {
        $m = $t.GetMethod($Name, $Flags)
        if ($null -ne $m) { return $m }
        $t = $t.BaseType
    }
    throw "Reflection lookup failed: could not find method '$Name' on '$($Type.FullName)' or any of its base types (flags: $Flags). The processor's method may have been renamed, removed, or had its visibility changed."
}

$validateInputs = Get-RequiredMethod -Type $procType -Name 'ValidateCommonInputs' -Flags $flags
$buildParams    = Get-RequiredMethod -Type $procType -Name 'BuildParameters'      -Flags $flags
$buildTemplate  = Get-RequiredMethod -Type $procType -Name 'BuildTemplate'        -Flags $flags

function New-Processor {
    param([hashtable]$Properties)
    $proc = [System.Activator]::CreateInstance($procType)
    foreach ($name in $Properties.Keys) {
        $procType.GetProperty($name).SetValue($proc, $Properties[$name])
    }
    return $proc
}

# Runs ValidateCommonInputs and returns the innermost error message, or $null when the input is accepted.
function Get-ValidationError {
    param([hashtable]$Properties)
    try {
        [void]$validateInputs.Invoke((New-Processor $Properties), @())
        return $null
    } catch {
        $ex = $_.Exception
        while ($null -ne $ex.InnerException) { $ex = $ex.InnerException }
        return $ex.Message
    }
}

function Get-ParametersLinkUri {
    param([string]$ParametersUri, [string]$QueryString, [string]$TemplateUri)
    $proc = New-Processor @{ ParametersUri = $ParametersUri; QueryString = $QueryString; TemplateUri = $TemplateUri }
    $outArgs = [object[]]@($null)
    [void]$buildParams.Invoke($proc, $outArgs)
    $link = $outArgs[0]
    if ($null -eq $link) { return $null }
    return $link.GetType().GetProperty('Uri').GetValue($link)
}

function Get-TemplateLink {
    param([string]$TemplateUri, [string]$QueryString)
    $proc = New-Processor @{ TemplateUri = $TemplateUri; QueryString = $QueryString }
    $outArgs = [object[]]@($null)
    [void]$buildTemplate.Invoke($proc, $outArgs)
    $link = $outArgs[0]
    return @{
        Uri = $link.GetType().GetProperty('Uri').GetValue($link)
        QueryString = $link.GetType().GetProperty('QueryString').GetValue($link)
    }
}

$failures = 0
function Assert-Equal {
    param($Expected, $Actual, [string]$Case)
    if ($Expected -eq $Actual) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected '$Expected' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}

function Assert-Match {
    param([string]$Pattern, $Actual, [string]$Case)
    if ($Actual -match $Pattern) {
        Write-Host "PASS  $Case  => '$Actual'"
    } else {
        Write-Host "FAIL  $Case  expected a message matching '$Pattern' but got '$Actual'" -ForegroundColor Red
        $script:failures++
    }
}

Write-Host "=== -QueryString requires -TemplateUri ==="
Assert-Match 'TemplateUri' (Get-ValidationError @{
    ResourceGroupName = 'rg'; TemplateFile = 'C:\t.json'; ParametersFile = 'C:\p.json'; QueryString = 'sv=2021&sig=ABC'
}) 'template file + query string is rejected'
Assert-Match 'TemplateUri' (Get-ValidationError @{
    ResourceGroupName = 'rg'; TemplateSpecId = '/subscriptions/x/templateSpecs/s/versions/1'; QueryString = 'sv=2021'
}) 'template spec + query string is rejected'
Assert-Equal $null (Get-ValidationError @{
    ResourceGroupName = 'rg'; TemplateUri = 'https://x/t.json'; ParametersUri = 'https://x/p.json'; QueryString = 'sv=2021'
}) 'template uri + query string is accepted'

Write-Host "`n=== TemplateLink carries the query string in its dedicated ARM field ==="
$tl = Get-TemplateLink 'https://x/t.json' 'sv=2021&sig=ABC'
Assert-Equal 'https://x/t.json' $tl.Uri         'template uri NOT modified'
Assert-Equal 'sv=2021&sig=ABC'  $tl.QueryString 'template uses TemplateLink.QueryString field'

Write-Host "`n=== TemplateLink.QueryString separator normalization ==="
Assert-Equal 'sv=abc' (Get-TemplateLink 'https://x/t.json' '?sv=abc').QueryString 'leading ? trimmed'
Assert-Equal 'sv=abc' (Get-TemplateLink 'https://x/t.json' '&sv=abc').QueryString 'leading & trimmed'
Assert-Equal 'sv=abc' (Get-TemplateLink 'https://x/t.json' 'sv=abc').QueryString  'no leading separator stays unchanged'
Assert-Equal $null    (Get-TemplateLink 'https://x/t.json' '?').QueryString       'qs of only ? normalizes to null'

Write-Host "`n=== ParametersLink.Uri is never rewritten (a SAS token is not copied into it) ==="
Assert-Equal 'https://x/p.json'       (Get-ParametersLinkUri 'https://x/p.json' 'sv=2021&sig=ABC' 'https://x/t.json') 'query string not appended'
Assert-Equal 'https://x/p.json?foo=1' (Get-ParametersLinkUri 'https://x/p.json?foo=1' 'sv=2021' 'https://x/t.json')   'existing query preserved as-is'
Assert-Equal 'https://x/p.json'       (Get-ParametersLinkUri 'https://x/p.json' $null $null)                          'no query string => unchanged uri'

if ($failures -gt 0) {
    Write-Host "`n$failures assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
