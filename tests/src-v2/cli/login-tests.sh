#!/bin/bash
# AzV2 CLI Login Commands Tests

set -e

BIN_DIR="/workspaces/cirrus/bin"
AZV3="$BIN_DIR/azv2"

echo "=== AzV2 CLI Login Commands Tests ==="
echo ""

# Test 1: Login (using default credentials)
echo "Test 1: Login with default credentials"
$AZV3 login
echo "✓ Test 1 passed"
echo ""

# Test 2: Show current context
echo "Test 2: Show current context"
$AZV3 login --action show
echo "✓ Test 2 passed"
echo ""

# Test 3: Login with specific tenant
echo "Test 3: Login with specific tenant"
$AZV3 login --tenant 11111111-1111-1111-1111-111111111111
echo "✓ Test 3 passed"
echo ""

# Test 4: Logout
echo "Test 4: Logout"
$AZV3 login --action logout
echo "✓ Test 4 passed"
echo ""

# Test 5: Agent - How to login (syntax help)
echo "Test 5: Agent - How to login (syntax help)"
echo "how do I login to Azure?" | $AZV3 2>&1 | grep -q "az login" && echo "✓ Test 5 passed" || echo "✗ Test 5 failed"
echo ""

# Test 6: Agent - Execute login command (natural language)
echo "Test 6: Agent - Execute login command (natural language)"
echo -e "login to azure\nexit" | $AZV3 2>&1 | grep -q "az login" && echo "✓ Test 6 passed" || echo "✗ Test 6 failed"
echo ""

# Test 7: Agent - Mistyped syntax (fallback to agent)
echo "Test 7: Agent - Mistyped syntax (fallback to agent)"
echo -e "loginn\nexit" | $AZV3 2>&1 | grep -q "Thinking" && echo "✓ Test 7 passed" || echo "✗ Test 7 failed"
echo ""

echo "=== All CLI Login Tests Completed ==="
