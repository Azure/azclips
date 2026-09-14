# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
#
# Smoke test for the Get-AzAccessToken cmdlet. Exercises every parameter set (resource type,
# resource URL, and explicit scopes), the secure-by-default vs plaintext output behavior, and the
# resolver/validation error paths.
#
# Prereqs:
#   1. Build the solution:   pwsh -File .\build\build.ps1 -Configuration Debug -GenerateHelpArtifacts
#   2. Import the Accounts module (below).
#   3. Sign in:              Connect-AzAccount -TenantId <tenant>
#
# Usage: pwsh -NoProfile -File .\tests\Scripts\PS\AccessToken.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
Import-Module (Join-Path $repoRoot 'artifacts\Debug\PS\Accounts\Accounts.psd1') -Force

function Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

Section 'Get-AzAccessToken (default target - ARM)'
$armToken = Get-AzAccessToken
$armToken | Select-Object TenantId, ExpiresOn, ExpiresOnUnixTime, Type | Format-List | Out-Host

Section 'Secure by default: token is a SecureString, not plain text'
$secure = Get-AzAccessToken
if ($secure.Token -is [System.Security.SecureString]) {
    Write-Host "OK: token is a SecureString by default (not a plain JWT)." -ForegroundColor Green
} else {
    throw "Secure-by-default contract broken: token is not a SecureString: $($secure.Token.GetType().FullName)"
}

Section 'Plaintext opt-in via -AsPlainText'
$plain = Get-AzAccessToken -AsPlainText
# OAuth access tokens are opaque: a valid token need not be a JWT or start with "ey"
# (e.g. tokens for personal Microsoft accounts). Only assert a non-empty string was returned.
if ($plain.Token -is [string] -and -not [string]::IsNullOrEmpty($plain.Token)) {
    Write-Host "OK: raw token returned as a non-empty plain string when -AsPlainText is passed." -ForegroundColor Green
} else {
    throw "-AsPlainText did not return a non-empty plain string."
}

Section 'Get-AzAccessToken -ResourceTypeName MSGraph (well-known type shortcut)'
Get-AzAccessToken -ResourceTypeName MSGraph | Select-Object TenantId, ExpiresOn | Format-List | Out-Host

Section 'Get-AzAccessToken -ResourceTypeName KeyVault (PowerShell name resolves via the unified map)'
Get-AzAccessToken -ResourceTypeName KeyVault | Select-Object TenantId, ExpiresOn | Format-List | Out-Host

Section 'Get-AzAccessToken -ResourceUrl (v1.0 resource)'
Get-AzAccessToken -ResourceUrl 'https://vault.azure.net' | Select-Object TenantId, ExpiresOn | Format-List | Out-Host

Section 'Get-AzAccessToken -Scope (v2.0 multi-scope list)'
Get-AzAccessToken -Scope 'https://graph.microsoft.com/.default' | Select-Object TenantId, ExpiresOn | Format-List | Out-Host

Section 'Negative: -ResourceUrl and -Scope together should be rejected (mutually exclusive)'
$rejected = $false
try {
    Get-AzAccessToken -ResourceUrl 'https://vault.azure.net' -Scope 'https://graph.microsoft.com/.default' -ErrorAction Stop | Out-Null
} catch {
    $rejected = $true
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}
if (-not $rejected) { throw "Command succeeded with conflicting target inputs (-ResourceUrl and -Scope should be mutually exclusive)." }

Section 'Negative: unknown -ResourceTypeName should be rejected'
$rejected = $false
try {
    Get-AzAccessToken -ResourceTypeName 'not-a-real-type' -ErrorAction Stop | Out-Null
} catch {
    $rejected = $true
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}
if (-not $rejected) { throw "Command succeeded with an unknown resource type." }

Write-Host ""
Write-Host "All Get-AzAccessToken smoke checks completed." -ForegroundColor Green
