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

$PlaintextEnvVar = 'AZUREPS_OUTPUT_PLAINTEXT_AZACCESSTOKEN'

function Section($title) {
    Write-Host ""
    Write-Host "=== $title ===" -ForegroundColor Cyan
}

Section 'Get-AzAccessToken (default target - ARM)'
$armToken = Get-AzAccessToken
$armToken | Select-Object TenantId, ExpiresOn, TokenType | Format-List | Out-Host

Section 'Secure by default: token must NOT be exposed as plain text'
# With the plaintext env var unset, the token field must be redacted / non-plaintext.
Remove-Item "Env:\$PlaintextEnvVar" -ErrorAction SilentlyContinue
$secure = Get-AzAccessToken
if ($secure.Token -is [System.Security.SecureString] -or "$($secure.Token)" -notmatch '^ey') {
    Write-Host "OK: token is protected by default (not emitted as a raw JWT)." -ForegroundColor Green
} else {
    Write-Host "UNEXPECTED: raw token appears to be exposed without opt-in." -ForegroundColor Red
}

Section "Plaintext opt-in via `$env:$PlaintextEnvVar"
Set-Item "Env:\$PlaintextEnvVar" 'true'
try {
    $plain = Get-AzAccessToken
    if ("$($plain.Token)" -match '^ey') {
        Write-Host "OK: raw token revealed when the env var is set." -ForegroundColor Green
    } else {
        Write-Host "UNEXPECTED: env var set but token still redacted." -ForegroundColor Red
    }
}
finally {
    Remove-Item "Env:\$PlaintextEnvVar" -ErrorAction SilentlyContinue
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
try {
    Get-AzAccessToken -ResourceUrl 'https://vault.azure.net' -Scope 'https://graph.microsoft.com/.default' -ErrorAction Stop | Out-Null
    Write-Host "UNEXPECTED: command succeeded with conflicting target inputs." -ForegroundColor Red
} catch {
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}

Section 'Negative: unknown -ResourceTypeName should be rejected'
try {
    Get-AzAccessToken -ResourceTypeName 'not-a-real-type' -ErrorAction Stop | Out-Null
    Write-Host "UNEXPECTED: command succeeded with an unknown resource type." -ForegroundColor Red
} catch {
    Write-Host "OK: rejected as expected -" $_.Exception.Message
}

Write-Host ""
Write-Host "All Get-AzAccessToken smoke checks completed." -ForegroundColor Green
