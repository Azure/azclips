# AzV2 PowerShell Login Commands Tests

$ErrorActionPreference = "Stop"
$BIN_DIR = "/workspaces/cirrus/bin"

Write-Host "=== AzV2 PowerShell Login Commands Tests ===" -ForegroundColor Cyan
Write-Host ""

# Import the module
Import-Module "$BIN_DIR/Login/Cirrus.Login.psd1"

# Test 1: Connect to Azure (login)
Write-Host "Test 1: Connect to Azure (login)" -ForegroundColor Yellow
$context = Connect-AzV2Account
if ($context -and $context.IsAuthenticated) {
    Write-Host "✓ Test 1 passed - Successfully authenticated" -ForegroundColor Green
    Write-Host "  SubscriptionId: $($context.SubscriptionId)" -ForegroundColor Gray
    Write-Host "  TenantId: $($context.TenantId)" -ForegroundColor Gray
} else {
    Write-Host "✗ Test 1 failed - Authentication failed" -ForegroundColor Red
}
Write-Host ""

# Test 2: Get current context
Write-Host "Test 2: Get current context" -ForegroundColor Yellow
$currentContext = Get-AzV2Context
if ($currentContext -and $currentContext.IsAuthenticated) {
    Write-Host "✓ Test 2 passed - Retrieved current context" -ForegroundColor Green
    Write-Host "  SubscriptionId: $($currentContext.SubscriptionId)" -ForegroundColor Gray
} else {
    Write-Host "✗ Test 2 failed - No authenticated context found" -ForegroundColor Red
}
Write-Host ""

# Test 3: Connect with specific tenant
Write-Host "Test 3: Connect with specific tenant" -ForegroundColor Yellow
$contextWithTenant = Connect-AzV2Account -TenantId "11111111-1111-1111-1111-111111111111"
if ($contextWithTenant -and $contextWithTenant.TenantId -eq "11111111-1111-1111-1111-111111111111") {
    Write-Host "✓ Test 3 passed - Connected with specific tenant" -ForegroundColor Green
} else {
    Write-Host "✗ Test 3 failed - Tenant ID mismatch" -ForegroundColor Red
}
Write-Host ""

# Test 4: Connect with subscription and tenant
Write-Host "Test 4: Connect with subscription and tenant" -ForegroundColor Yellow
$contextFull = Connect-AzV2Account -TenantId "22222222-2222-2222-2222-222222222222" -SubscriptionId "33333333-3333-3333-3333-333333333333"
if ($contextFull -and $contextFull.SubscriptionId -eq "33333333-3333-3333-3333-333333333333") {
    Write-Host "✓ Test 4 passed - Connected with subscription and tenant" -ForegroundColor Green
} else {
    Write-Host "✗ Test 4 failed - Subscription ID mismatch" -ForegroundColor Red
}
Write-Host ""

# Test 5: Disconnect (logout)
Write-Host "Test 5: Disconnect (logout)" -ForegroundColor Yellow
Disconnect-AzV2Account
$contextAfterLogout = Get-AzV2Context
if (-not $contextAfterLogout) {
    Write-Host "✓ Test 5 passed - Successfully logged out" -ForegroundColor Green
} else {
    Write-Host "✗ Test 5 failed - Context still exists after logout" -ForegroundColor Red
}
Write-Host ""

# Re-authenticate for agent tests
Connect-AzV2Account | Out-Null

# Test 6: Agent - How to login (syntax help)
Write-Host "Test 6: Agent - How to login (syntax help)" -ForegroundColor Yellow
Write-Host "Note: This test requires manual verification of syntax output" -ForegroundColor Gray
try {
    $output = Invoke-AzV2 "how do I login to Azure" 2>&1 | Out-String
    if ($output -match "Connect-AzV2Account") {
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
Write-Host "Skipping interactive test - Use 'ask login to azure' manually" -ForegroundColor Yellow
Write-Host "⚠ Test 7 skipped - Requires interactive input" -ForegroundColor Yellow
Write-Host ""

# Test 8: Agent aliases
Write-Host "Test 8: Agent aliases (ask, please, pls)" -ForegroundColor Yellow
$aliases = Get-Alias -Name ask, please, pls -ErrorAction SilentlyContinue
if ($aliases.Count -eq 3) {
    Write-Host "✓ Test 8 passed - All three aliases (ask, please, pls) are available" -ForegroundColor Green
} else {
    Write-Host "✗ Test 8 failed - Expected 3 aliases, found $($aliases.Count)" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== All PowerShell Login Tests Completed ===" -ForegroundColor Cyan
