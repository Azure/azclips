# AzV2 PowerShell Group Commands Tests

$ErrorActionPreference = "Stop"
$BIN_DIR = "/workspaces/cirrus/bin"

Write-Host "=== AzV2 PowerShell Group Commands Tests ===" -ForegroundColor Cyan
Write-Host ""

# Import the module
Import-Module "$BIN_DIR/Group/Cirrus.Group.psd1"

# Test 1: List all resource groups
Write-Host "Test 1: List all resource groups" -ForegroundColor Yellow
$groups = Get-AzV2ResourceGroup
if ($groups.Count -gt 0) {
    Write-Host "✓ Test 1 passed - Found $($groups.Count) resource groups" -ForegroundColor Green
} else {
    Write-Host "✗ Test 1 failed - No resource groups found" -ForegroundColor Red
}
Write-Host ""

# Test 2: Get specific resource group
Write-Host "Test 2: Get specific resource group" -ForegroundColor Yellow
$group = Get-AzV2ResourceGroup -Name "rg-demo-001"
if ($group -and $group.Name -eq "rg-demo-001") {
    Write-Host "✓ Test 2 passed - Found resource group rg-demo-001" -ForegroundColor Green
} else {
    Write-Host "✗ Test 2 failed - Resource group not found" -ForegroundColor Red
}
Write-Host ""

# Test 3: Create resource group
Write-Host "Test 3: Create resource group" -ForegroundColor Yellow
$newGroup = New-AzV2ResourceGroup -Name "test-ps-rg" -Location "eastus"
if ($newGroup -and $newGroup.Name -eq "test-ps-rg") {
    Write-Host "✓ Test 3 passed - Created resource group test-ps-rg" -ForegroundColor Green
} else {
    Write-Host "✗ Test 3 failed - Failed to create resource group" -ForegroundColor Red
}
Write-Host ""

# Test 4: Update resource group (show)
Write-Host "Test 4: Update/Show resource group" -ForegroundColor Yellow
$updatedGroup = Set-AzV2ResourceGroup -Name "test-ps-rg"
if ($updatedGroup -and $updatedGroup.Name -eq "test-ps-rg") {
    Write-Host "✓ Test 4 passed - Updated/showed resource group" -ForegroundColor Green
} else {
    Write-Host "✗ Test 4 failed - Failed to update resource group" -ForegroundColor Red
}
Write-Host ""

# Test 5: Delete resource group
Write-Host "Test 5: Delete resource group" -ForegroundColor Yellow
$result = Remove-AzV2ResourceGroup -Name "test-ps-rg" -Force
if ($result) {
    Write-Host "✓ Test 5 passed - Deleted resource group test-ps-rg" -ForegroundColor Green
} else {
    Write-Host "✗ Test 5 failed - Failed to delete resource group" -ForegroundColor Red
}
Write-Host ""

# Test 6: Agent - How to create a resource group (syntax help)
Write-Host "Test 6: Agent - How to create a resource group (syntax help)" -ForegroundColor Yellow
Write-Host "Note: This test requires manual verification of syntax output" -ForegroundColor Gray
try {
    # This should return syntax without executing
    $output = Invoke-AzV2 "how do I create a resource group" 2>&1 | Out-String
    if ($output -match "New-AzV2ResourceGroup") {
        Write-Host "✓ Test 6 passed - Agent provided syntax help" -ForegroundColor Green
    } else {
        Write-Host "⚠ Test 6 partial - Agent responded but verify output manually" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ Test 6 failed - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test 7: Agent - Execute command (natural language)
Write-Host "Test 7: Agent - Execute command (natural language)" -ForegroundColor Yellow
Write-Host "Note: This test is interactive and requires user confirmation" -ForegroundColor Gray
Write-Host "Skipping interactive test - Use 'ask create a resource group' manually" -ForegroundColor Yellow
Write-Host "⚠ Test 7 skipped - Requires interactive input" -ForegroundColor Yellow
Write-Host ""

# Test 8: Agent - Mistyped cmdlet (should still work via agent)
Write-Host "Test 8: Agent aliases (ask, please, pls)" -ForegroundColor Yellow
# Verify aliases exist
$aliases = Get-Alias -Name ask, please, pls -ErrorAction SilentlyContinue
if ($aliases.Count -eq 3) {
    Write-Host "✓ Test 8 passed - All three aliases (ask, please, pls) are available" -ForegroundColor Green
} else {
    Write-Host "✗ Test 8 failed - Expected 3 aliases, found $($aliases.Count)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== All PowerShell Group Tests Completed ===" -ForegroundColor Cyan
