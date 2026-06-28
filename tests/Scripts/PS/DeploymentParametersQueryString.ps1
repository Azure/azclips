# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
    Body-level verification that -QueryString is embedded into the ParametersLink.Uri.

.DESCRIPTION
    ARM's ParametersLink has no dedicated queryString field (unlike TemplateLink), so a SAS/query
    string supplied via -QueryString must be embedded directly in the parameters URI. This script
    loads the built Resources extension assembly and, via reflection, drives the processor's
    protected BuildParameters(out ParametersLink) to assert the resulting ParametersLink.Uri.
    It also confirms the template-link path is UNCHANGED (TemplateLink.QueryString keeps using the
    dedicated ARM field, not embedding).

    This is a local body-inspection (no live Azure / no real SAS blob required). Run after a build.
#>

$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$extDir = Join-Path $root 'artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources'
$coreDir = Join-Path $root 'artifacts/Debug/Core'
$cliDir = Join-Path $root 'artifacts/Debug/Core.Cli'
$extDll = Join-Path $extDir 'Microsoft.Azure.Azclips.Extensions.Resources.dll'

if (-not (Test-Path $extDll)) { throw "Extension DLL not found at $extDll. Build the solution first." }

# Resolve the extension's dependencies (Core.Abstractions, etc.) from build output folders.
$probeDirs = @($extDir, $coreDir, $cliDir)
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

$buildParams   = Get-RequiredMethod -Type $procType -Name 'BuildParameters' -Flags $flags
$buildTemplate = Get-RequiredMethod -Type $procType -Name 'BuildTemplate'   -Flags $flags

function Get-ParametersLinkUri {
    param([string]$ParametersUri, [string]$QueryString)
    $proc = [System.Activator]::CreateInstance($procType)
    $procType.GetProperty('ParametersUri').SetValue($proc, $ParametersUri)
    if ($null -ne $QueryString) { $procType.GetProperty('QueryString').SetValue($proc, $QueryString) }
    $args = [object[]]@($null)
    [void]$buildParams.Invoke($proc, $args)
    $link = $args[0]
    if ($null -eq $link) { return $null }
    return $link.GetType().GetProperty('Uri').GetValue($link)
}

function Get-TemplateLinkQueryString {
    param([string]$TemplateUri, [string]$QueryString)
    $proc = [System.Activator]::CreateInstance($procType)
    $procType.GetProperty('TemplateUri').SetValue($proc, $TemplateUri)
    if ($null -ne $QueryString) { $procType.GetProperty('QueryString').SetValue($proc, $QueryString) }
    $args = [object[]]@($null)
    [void]$buildTemplate.Invoke($proc, $args)
    $link = $args[0]
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

Write-Host "=== ParametersLink.Uri embedding (item 2) ==="
Assert-Equal 'https://x/p.json?sv=2021&sig=ABC' (Get-ParametersLinkUri 'https://x/p.json' 'sv=2021&sig=ABC') 'no existing query, plain qs'
Assert-Equal 'https://x/p.json?foo=1&sv=2021'  (Get-ParametersLinkUri 'https://x/p.json?foo=1' 'sv=2021') 'uri already has query => & join'
Assert-Equal 'https://x/p.json?sv=2021'        (Get-ParametersLinkUri 'https://x/p.json' '?sv=2021') 'leading ? trimmed (single)'
Assert-Equal 'https://x/p.json?sv=2021'        (Get-ParametersLinkUri 'https://x/p.json' '&sv=2021') 'leading & trimmed (single)'
Assert-Equal 'https://x/p.json'                (Get-ParametersLinkUri 'https://x/p.json' '') 'empty qs => unchanged uri'
Assert-Equal 'https://x/p.json'                (Get-ParametersLinkUri 'https://x/p.json' $null) 'no qs => unchanged uri'
Assert-Equal 'https://x/p.json'                (Get-ParametersLinkUri 'https://x/p.json' '?') 'qs of only ? trims empty => no stray separator'
Assert-Equal 'https://x/p.json'                (Get-ParametersLinkUri 'https://x/p.json' '&') 'qs of only & trims empty => no stray separator'

Write-Host "`n=== TemplateLink path UNCHANGED (uses dedicated ARM field, not embedding) ==="
$tl = Get-TemplateLinkQueryString 'https://x/t.json' 'sv=2021&sig=ABC'
Assert-Equal 'https://x/t.json'        $tl.Uri         'template uri NOT modified'
Assert-Equal 'sv=2021&sig=ABC'         $tl.QueryString 'template uses TemplateLink.QueryString field'

Write-Host "`n=== TemplateLink.QueryString separator normalization (items A/B) ==="
$tlLeadingQ = Get-TemplateLinkQueryString 'https://x/t.json' '?sv=abc'
Assert-Equal 'sv=abc' $tlLeadingQ.QueryString 'leading ? trimmed from TemplateLink.QueryString'
$tlLeadingAmp = Get-TemplateLinkQueryString 'https://x/t.json' '&sv=abc'
Assert-Equal 'sv=abc' $tlLeadingAmp.QueryString 'leading & trimmed from TemplateLink.QueryString'
$tlNoSep = Get-TemplateLinkQueryString 'https://x/t.json' 'sv=abc'
Assert-Equal 'sv=abc' $tlNoSep.QueryString 'no leading separator stays unchanged'
$tlOnlyQ = Get-TemplateLinkQueryString 'https://x/t.json' '?'
Assert-Equal $null    $tlOnlyQ.QueryString 'qs of only ? normalizes to null'

if ($failures -gt 0) {
    Write-Host "`n$failures assertion(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll assertions passed."
exit 0
