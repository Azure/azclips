#!/bin/bash
set -e

# ┌─────────────────────────────────────────────────────────────────────┐
# │ Cirrus Build & Deploy Script                                        │
# │                                                                     │
# │ This script builds the Cirrus solution and publishes binaries       │
# │ to the bin/ directory, making them ready for use.                   │
# │                                                                     │
# │ Usage: ./deploy.sh                                                  │
# │                                                                     │
# │ Steps:                                                              │
# │   1. Verifies .NET SDK is installed                                 │
# │   2. Builds the solution in Release mode                            │
# │   3. Publishes az CLI binary to /workspaces/cirrus/bin              │
# │   4. Publishes PowerShell module (v2 only)                          │
# └─────────────────────────────────────────────────────────────────────┘

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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
echo -e "${YELLOW}[2/4] Building solution...${NC}"
cd "$REPO_ROOT"

dotnet build cirrus.slnx -c Release

echo -e "${GREEN}✓ Build completed${NC}"
echo ""

# Step 3: Publish CLI to bin/
echo -e "${YELLOW}[3/4] Publishing CLI to bin/...${NC}"

# Clean and create bin directory
rm -rf "$BIN_DIR"
mkdir -p "$BIN_DIR"

dotnet publish src/Core/Core.Cli/Core.Cli.csproj -c Release -o "$BIN_DIR" --self-contained false
CLI_NAME="azv2"

echo -e "${GREEN}✓ Published CLI to $BIN_DIR${NC}"
echo ""

# Step 4: Copy PowerShell modules
echo -e "${YELLOW}[4/4] Copying PowerShell modules to bin/...${NC}"

# Copy Group module (for both CLI commands and PowerShell)
mkdir -p "$BIN_DIR/Group"
cp artifacts/Release/Group/Group.dll "$BIN_DIR/Group/"
cp artifacts/Release/Group/Core.Abstractions.dll "$BIN_DIR/Group/"
cp src/Group/Group/Cirrus.Group.psd1 "$BIN_DIR/Group/" 2>/dev/null || echo "Warning: Cirrus.Group.psd1 not found"
cp src/Group/Group/AgentHelper.ps1 "$BIN_DIR/Group/" 2>/dev/null || echo "Warning: Group AgentHelper.ps1 not found"

# Copy Login module (for both CLI commands and PowerShell)
mkdir -p "$BIN_DIR/Login"
cp artifacts/Release/Login/Login.dll "$BIN_DIR/Login/"
cp artifacts/Release/Login/Core.Abstractions.dll "$BIN_DIR/Login/"
cp src/Login/Login/Cirrus.Login.psd1 "$BIN_DIR/Login/" 2>/dev/null || echo "Warning: Cirrus.Login.psd1 not found"
cp src/Login/Login/AgentHelper.ps1 "$BIN_DIR/Login/" 2>/dev/null || echo "Warning: Login AgentHelper.ps1 not found"

echo -e "${GREEN}✓ PowerShell modules copied to $BIN_DIR${NC}"
echo ""

# Make CLI executable
chmod +x "$BIN_DIR/$CLI_NAME"

echo -e "${GREEN}=== Deployment Complete ===${NC}"
echo ""
echo -e "${GREEN}✓ $CLI_NAME CLI is ready!${NC}"
echo -e "${GREEN}✓ PowerShell modules are ready!${NC}"
echo ""
echo -e "CLI Usage:"
echo -e "${YELLOW}  $BIN_DIR/$CLI_NAME --help${NC}"
echo -e "${YELLOW}  $BIN_DIR/$CLI_NAME group list${NC}"
echo -e "${YELLOW}  $BIN_DIR/$CLI_NAME create a resource group${NC}"

echo ""
echo -e "PowerShell Usage:"
echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; Get-Command -Module Cirrus.Group\"${NC}"
echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; Get-AzV2ResourceGroup\"${NC}"
echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Login/Cirrus.Login.psd1; Connect-AzV2Account\"${NC}"
echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; ask create a resource group\"${NC}"

echo ""