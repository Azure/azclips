# AzV2 Tests - Run All Tests

Write-Host "=== AzV2 Comprehensive Test Suite ===" -ForegroundColor Cyan
Write-Host ""

$TestDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Running PowerShell Tests..." -ForegroundColor Yellow
Write-Host ""

# Group tests
Write-Host "1. Running Group cmdlet tests..." -ForegroundColor White
& "$TestDir/group-tests.ps1"
Write-Host ""

# Login tests
Write-Host "2. Running Login cmdlet tests..." -ForegroundColor White
& "$TestDir/login-tests.ps1"
Write-Host ""

# Agent tests (documentation)
Write-Host "3. Agent natural language tests (manual)..." -ForegroundColor White
& "$TestDir/agent-natural-language-tests.ps1"
Write-Host ""

Write-Host "=== All Automated Tests Completed ===" -ForegroundColor Green
Write-Host "Review agent tests above for manual testing instructions" -ForegroundColor Yellow
