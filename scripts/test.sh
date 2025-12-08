#!/bin/bash
set -e

# ┌─────────────────────────────────────────────────────────────────────┐
# │ Cirrus Test Script                                                   │
# │                                                                      │
# │ This script deploys binaries and runs all tests.                    │
# │                                                                      │
# │ Usage: ./scripts/test.sh                                            │
# └─────────────────────────────────────────────────────────────────────┘

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "╔════════════════════════════════════════════════════════════════════╗"
echo "║                      Cirrus Test Runner                            ║"
echo "╚════════════════════════════════════════════════════════════════════╝"
echo ""

# Step 1: Deploy binaries
echo "📦 Deploying binaries to bin/..."
"$SCRIPT_DIR/deploy.sh" > /dev/null 2>&1
echo "✓ Deployment complete"
echo ""

# Step 2: Run tests
echo "🧪 Running tests..."
cd "$REPO_ROOT"
dotnet test --no-build

echo ""
echo "✓ All done!"
