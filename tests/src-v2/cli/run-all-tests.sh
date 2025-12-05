#!/bin/bash
# AzV2 Tests - Run All CLI Tests

echo "=== AzV2 CLI Comprehensive Test Suite ==="
echo ""

TEST_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running CLI Tests..."
echo ""

# Group tests
echo "1. Running Group command tests..."
bash "$TEST_DIR/group-tests.sh"
echo ""

# Login tests
echo "2. Running Login command tests..."
bash "$TEST_DIR/login-tests.sh"
echo ""

echo "=== All CLI Tests Completed ==="
echo "Note: Some agent tests require interactive input and were skipped"
echo "To test agent interactively, run: azv2 <your natural language query>"
