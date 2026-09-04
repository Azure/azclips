# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for the Get-AzMsGraphGroup and Get-AzMsGraphGroupMember cmdlets. Exercises every
# parameter set and their aliases, plus the resolver error paths.
#
# Prereqs:
#   1. Build the solution:   pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Import the Accounts + MsGraph modules (below).
#   3. Sign in:              Connect-AzAccount -TenantId <tenant>
#
# Override the test-tenant fixtures via env vars if running against a different tenant:
#   AZCLIPS_MSGRAPH_GROUP_ID           - a group's object ID
#   AZCLIPS_MSGRAPH_GROUP_NAME         - the same group's exact display name
#   AZCLIPS_MSGRAPH_GROUP_PREFIX       - a name prefix that matches exactly one group
#   AZCLIPS_MSGRAPH_GROUP_PREFIX_MANY  - a name prefix that matches >1 groups
#
# Usage: pwsh -NoProfile -File .\tests\Scripts\PS\MsGraphGroup.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\MsGraph\MsGraph.psd1') -Force

# Defaults match the shared test tenant (azclitools20260430.onmicrosoft.com).
$groupId          = $env:AZCLIPS_MSGRAPH_GROUP_ID          ?? 'e311fa4a-a740-4a3b-af0d-e538e2b01926'
$groupName        = $env:AZCLIPS_MSGRAPH_GROUP_NAME        ?? 'GroupA'
$groupPrefix      = $env:AZCLIPS_MSGRAPH_GROUP_PREFIX      ?? 'GroupA'
$groupPrefixMany  = $env:AZCLIPS_MSGRAPH_GROUP_PREFIX_MANY ?? 'Group'

function Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

Section 'Get-AzMsGraphGroup -ObjectId (Get parameter set)'
Get-AzMsGraphGroup -ObjectId $groupId | Select-Object displayName, id | Out-Host

Section 'Get-AzMsGraphGroup -Id (Id alias on -ObjectId)'
Get-AzMsGraphGroup -Id $groupId | Select-Object displayName, id | Out-Host

Section 'Get-AzMsGraphGroup -DisplayName (exact eq)'
Get-AzMsGraphGroup -DisplayName $groupName | Select-Object displayName, id | Out-Host

Section 'Get-AzMsGraphGroup -DisplayNameStartsWith (prefix)'
Get-AzMsGraphGroup -DisplayNameStartsWith $groupPrefixMany | Select-Object displayName -First 3 | Out-Host

Section 'Get-AzMsGraphGroup -SearchString (SearchString alias on -DisplayNameStartsWith)'
Get-AzMsGraphGroup -SearchString $groupPrefixMany | Select-Object displayName -First 3 | Out-Host

Section 'Get-AzMsGraphGroup (default, first page)'
Get-AzMsGraphGroup | Select-Object displayName -First 5 | Out-Host

Section 'Get-AzMsGraphGroup -Filter (custom OData)'
Get-AzMsGraphGroup -Filter "securityEnabled eq true" | Select-Object displayName -First 3 | Out-Host

Section 'Get-AzMsGraphGroup -Select + -AppendSelected'
Get-AzMsGraphGroup -ObjectId $groupId -Select 'visibility','createdDateTime' -AppendSelected | Format-List | Out-Host

Section 'Get-AzMsGraphGroupMember -GroupObjectId (ObjectId parameter set)'
Get-AzMsGraphGroupMember -GroupObjectId $groupId | Out-Host

Section 'Get-AzMsGraphGroupMember -Id (Id alias)'
Get-AzMsGraphGroupMember -Id $groupId | Out-Host

Section 'Get-AzMsGraphGroupMember -ObjectId (ObjectId alias)'
Get-AzMsGraphGroupMember -ObjectId $groupId | Out-Host

Section 'Get-AzMsGraphGroupMember -GroupDisplayName (DisplayName parameter set)'
Get-AzMsGraphGroupMember -GroupDisplayName $groupName | Out-Host

Section 'Negative: ambiguous prefix on -ObjectId should throw with "more than one group matched"'
try {
    Get-AzMsGraphGroup -ObjectId $groupPrefixMany -ErrorAction Stop | Out-Null
    Write-Host "UNEXPECTED: command succeeded on ambiguous prefix" -ForegroundColor Red
} catch {
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}

Section 'Negative: name with no matches should throw with "is not found in Graph"'
try {
    Get-AzMsGraphGroupMember -GroupDisplayName "no-such-group-$([Guid]::NewGuid())" -ErrorAction Stop | Out-Null
    Write-Host "UNEXPECTED: command succeeded on non-existent name" -ForegroundColor Red
} catch {
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}

Write-Host ""
Write-Host "All Get-AzMsGraphGroup / Get-AzMsGraphGroupMember smoke checks completed." -ForegroundColor Green
