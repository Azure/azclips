#!/bin/bash
set -e

# ┌─────────────────────────────────────────────────────────────────────┐
# │ Cirrus Build & Deploy Script                                        │
# │                                                                     │
# │ This script builds the Cirrus solution and publishes binaries       │
# │ to the bin/ directory, making them ready for use.                   │
# │                                                                     │
# │ Usage: ./deploy.sh [v1|v2|v3]                                       │
# │   v1 (default) - Builds legacy Cirrus v1                            │
# │   v2           - Builds clean architecture Cirrus v2                │
# │   v3           - Builds v3 with business logic separation           │
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

# Parse version argument (default to v1)
VERSION="${1:-v1}"

if [[ "$VERSION" != "v1" && "$VERSION" != "v2" && "$VERSION" != "v3" ]]; then
    echo -e "${RED}Error: Invalid version '$VERSION'. Use 'v1', 'v2', or 'v3'${NC}"
    echo "Usage: $0 [v1|v2|v3]"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BIN_DIR="$REPO_ROOT/bin"

echo -e "${GREEN}=== Cirrus Build & Deploy ${BLUE}($VERSION)${GREEN} ===${NC}"
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

if [[ "$VERSION" == "v1" ]]; then
    dotnet build cirrus.slnx -c Release
elif [[ "$VERSION" == "v2" ]]; then
    dotnet build src-v2/cirrus-v2.slnx -c Release
else
    dotnet build src-v3/cirrus-v3.slnx -c Release
fi

echo -e "${GREEN}✓ Build completed${NC}"
echo ""

# Step 3: Publish CLI to bin/
echo -e "${YELLOW}[3/4] Publishing CLI to bin/...${NC}"

# Clean and create bin directory
rm -rf "$BIN_DIR"
mkdir -p "$BIN_DIR"

if [[ "$VERSION" == "v1" ]]; then
    dotnet publish src/Core/Core.Cli/Core.Cli.csproj -c Release -o "$BIN_DIR" --self-contained false
    CLI_NAME="az"
elif [[ "$VERSION" == "v2" ]]; then
    dotnet publish src-v2/Cli/Cli.csproj -c Release -o "$BIN_DIR" --self-contained false
    CLI_NAME="azv2"
else
    dotnet publish src-v3/Cli/Cli.csproj -c Release -o "$BIN_DIR" --self-contained false
    CLI_NAME="azv3"
fi

echo -e "${GREEN}✓ Published CLI to $BIN_DIR${NC}"
echo ""

# Step 4: Copy PowerShell modules
echo -e "${YELLOW}[4/4] Copying PowerShell modules to bin/...${NC}"

if [[ "$VERSION" == "v2" ]]; then
    # Copy Group module (for both CLI commands and PowerShell)
    mkdir -p "$BIN_DIR/Group"
    cp src-v2/Group/bin/Release/net10.0/Group.dll "$BIN_DIR/Group/"
    cp src-v2/Group/bin/Release/net10.0/Core.Abstractions.dll "$BIN_DIR/Group/"
    cp src-v2/Group/bin/Release/net10.0/Group.psd1 "$BIN_DIR/Group/" 2>/dev/null || echo "Warning: Group.psd1 not found"
    
    # Copy Login module (for both CLI commands and PowerShell)
    mkdir -p "$BIN_DIR/Login"
    cp src-v2/Login/bin/Release/net10.0/Login.dll "$BIN_DIR/Login/"
    cp src-v2/Login/bin/Release/net10.0/Core.Abstractions.dll "$BIN_DIR/Login/"
    cp src-v2/Login/bin/Release/net10.0/Login.psd1 "$BIN_DIR/Login/" 2>/dev/null || echo "Warning: Login.psd1 not found"
    
    # Copy Agent.PowerShell module (AI agent for natural language)
    mkdir -p "$BIN_DIR/Agent.PowerShell"
    cp -r src-v2/Agent.PowerShell/bin/Release/net10.0/* "$BIN_DIR/Agent.PowerShell/"
    
    echo -e "${GREEN}✓ PowerShell modules copied to $BIN_DIR${NC}"
elif [[ "$VERSION" == "v3" ]]; then
    # Copy Group module (for both CLI commands and PowerShell)
    mkdir -p "$BIN_DIR/Group"
    cp artifacts/Release/Group/Group.dll "$BIN_DIR/Group/"
    cp artifacts/Release/Group/Core.Abstractions.dll "$BIN_DIR/Group/"
    cp src-v3/Group/Group/Cirrus.Group.psd1 "$BIN_DIR/Group/" 2>/dev/null || echo "Warning: Cirrus.Group.psd1 not found"
    cp src-v3/Group/Group/AgentHelper.ps1 "$BIN_DIR/Group/" 2>/dev/null || echo "Warning: Group AgentHelper.ps1 not found"
    
    # Copy Login module (for both CLI commands and PowerShell)
    mkdir -p "$BIN_DIR/Login"
    cp artifacts/Release/Login/Login.dll "$BIN_DIR/Login/"
    cp artifacts/Release/Login/Core.Abstractions.dll "$BIN_DIR/Login/"
    cp src-v3/Login/Login/Cirrus.Login.psd1 "$BIN_DIR/Login/" 2>/dev/null || echo "Warning: Cirrus.Login.psd1 not found"
    cp src-v3/Login/Login/AgentHelper.ps1 "$BIN_DIR/Login/" 2>/dev/null || echo "Warning: Login AgentHelper.ps1 not found"
    
    echo -e "${GREEN}✓ PowerShell modules copied to $BIN_DIR${NC}"
else
    echo -e "${YELLOW}✓ Skipping PowerShell module copy (v1 uses different structure)${NC}"
fi
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

if [[ "$VERSION" == "v2" || "$VERSION" == "v3" ]]; then
    echo ""
    echo -e "PowerShell Usage:"
    if [[ "$VERSION" == "v2" ]]; then
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Group.psd1; Get-Command -Module AzV2.ResourceGroup\"${NC}"
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Group.psd1; Get-AzV2ResourceGroup\"${NC}"
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Login/Login.psd1; Connect-AzV2Account\"${NC}"
        if [ -d "$BIN_DIR/Agent.PowerShell" ]; then
            echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Agent.PowerShell/Agent.PowerShell.psd1; ask create a resource group\"${NC}"
        fi
    else
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; Get-Command -Module Cirrus.Group\"${NC}"
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; Get-AzV3ResourceGroup\"${NC}"
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Login/Cirrus.Login.psd1; Connect-AzV3Account\"${NC}"
        echo -e "${YELLOW}  pwsh -Command \"Import-Module $BIN_DIR/Group/Cirrus.Group.psd1; ask create a resource group\"${NC}"
    fi
fi

echo ""