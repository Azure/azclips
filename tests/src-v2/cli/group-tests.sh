#!/bin/bash
# AzV2 CLI Group Commands Tests

set -e

BIN_DIR="/workspaces/cirrus/bin"
AZV3="$BIN_DIR/azv2"

echo "=== AzV2 CLI Group Commands Tests ==="
echo ""

# Test 1: List all resource groups
echo "Test 1: List all resource groups"
$AZV3 group list
echo "✓ Test 1 passed"
echo ""

# Test 2: Show specific resource group
echo "Test 2: Show specific resource group"
$AZV3 group show --name rg-demo-001
echo "✓ Test 2 passed"
echo ""

# Test 3: Create resource group
echo "Test 3: Create resource group"
$AZV3 group create --name test-cli-rg --location eastus
echo "✓ Test 3 passed"
echo ""

# Test 4: Delete resource group
echo "Test 4: Delete resource group"
$AZV3 group delete --name test-cli-rg
echo "✓ Test 4 passed"
echo ""

# Test 5: Agent - How to create a resource group (syntax help)
echo "Test 5: Agent - How to create a resource group (syntax help)"
echo "how do I create a resource group?" | $AZV3 2>&1 | grep -q "az group create" && echo "✓ Test 5 passed" || echo "✗ Test 5 failed"
echo ""

# Test 6: Agent - Execute command (natural language)
echo "Test 6: Agent - Execute command (natural language)"
# This test requires interactive input, so we'll just verify the agent responds
echo -e "list my resource groups\nexit" | $AZV3 2>&1 | grep -q "az group list" && echo "✓ Test 6 passed" || echo "✗ Test 6 failed"
echo ""

# Test 7: Agent - Mistyped syntax (fallback to agent)
echo "Test 7: Agent - Mistyped syntax (fallback to agent)"
echo -e "grup list\nexit" | $AZV3 2>&1 | grep -q "Thinking" && echo "✓ Test 7 passed" || echo "✗ Test 7 failed"
echo ""

echo "=== All CLI Group Tests Completed ==="
