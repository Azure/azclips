#!/bin/bash
set -e

# ┌─────────────────────────────────────────────────────────────────────┐
# │ Cirrus Build & Deploy Script                                        │
# │                                                                     │
# │ This script builds the entire Cirrus solution and publishes the     │
# │ az CLI to the bin/ directory, making it ready for use.              │
# │                                                                     │
# │ Steps:                                                              │
# │   1. Verifies .NET SDK is installed                                 │
# │   2. Builds the solution in Release mode                            │
# │   3. Publishes az CLI binary to /workspaces/cirrus/bin              │
# └─────────────────────────────────────────────────────────────────────┘

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BIN_DIR="$REPO_ROOT/bin"

echo -e "${GREEN}=== Cirrus Build & Deploy ===${NC}"
echo ""

# Step 1: Verify .NET SDK
echo -e "${YELLOW}[1/3] Verifying .NET SDK...${NC}"
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: dotnet SDK not found${NC}"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✓ .NET SDK version: $DOTNET_VERSION"
echo ""

# Step 2: Build the solution
echo -e "${YELLOW}[2/3] Building solution...${NC}"
cd "$REPO_ROOT"
dotnet build cirrus.slnx -c Release
echo -e "${GREEN}✓ Build completed${NC}"
echo ""

# Step 3: Publish az CLI to bin/
echo -e "${YELLOW}[3/3] Publishing az CLI to bin/...${NC}"
dotnet publish src/Core/Core.Cli/Core.Cli.csproj -c Release -o "$BIN_DIR" --self-contained false
echo -e "${GREEN}✓ Published to $BIN_DIR${NC}"
echo ""

# Make az executable
chmod +x "$BIN_DIR/az"

echo -e "${GREEN}=== Deployment Complete ===${NC}"
echo ""
echo -e "${GREEN}✓ az command is ready!${NC}"
echo ""
echo -e "You can now run:"
echo -e "${YELLOW}  az --help${NC}"
echo -e "${YELLOW}  az group create --name mygroup --location eastus${NC}"
echo -e "${YELLOW}  az create a resource group called mygroup in westus${NC}"
