#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

echo -e "${GREEN}=== Cirrus Wiki Deployment Setup ===${NC}\n"

# Get GitHub repository from git config
GIT_DIR="$SCRIPT_DIR/../.git"
if [ ! -d "$GIT_DIR" ]; then
    echo -e "${RED}Error: Git repository not found at $GIT_DIR${NC}"
    exit 1
fi

REPO_URL=$(git -C "$SCRIPT_DIR/.." config --get remote.origin.url)
if [[ $REPO_URL =~ github.com[:/](.+/.+)(\.git)?$ ]]; then
    GITHUB_REPO="${BASH_REMATCH[1]%.git}"
    echo -e "${GREEN}GitHub Repository:${NC} $GITHUB_REPO"
else
    echo -e "${RED}Error: Could not determine GitHub repository from: $REPO_URL${NC}"
    exit 1
fi

# Check if gh CLI is available
if ! command -v gh &> /dev/null; then
    echo -e "${RED}Error: GitHub CLI (gh) is not installed${NC}"
    echo "Install it from: https://cli.github.com/"
    exit 1
fi

# Authenticate with GitHub if needed
if ! gh auth status &> /dev/null; then
    echo -e "${YELLOW}GitHub authentication required...${NC}"
    gh auth login
fi

echo ""

# Function to read .env file
read_env_value() {
    local key=$1
    if [ -f "$ENV_FILE" ]; then
        grep "^${key}=" "$ENV_FILE" | cut -d '=' -f2- | tr -d '"' | tr -d "'"
    fi
}

# Check for .env file
if [ ! -f "$ENV_FILE" ]; then
    echo -e "${RED}Error: .env file not found at $ENV_FILE${NC}"
    echo -e "${YELLOW}Copy .env.example to .env and fill in your values${NC}"
    exit 1
fi

# Read Azure credentials from .env
echo -e "${YELLOW}Reading Azure Service Principal credentials from .env...${NC}"

AZURE_TENANT_ID=$(read_env_value "AZURE_TENANT_ID")
AZURE_CLIENT_ID=$(read_env_value "AZURE_CLIENT_ID")
AZURE_CLIENT_SECRET=$(read_env_value "AZURE_CLIENT_SECRET")
AZURE_SUBSCRIPTION_ID=$(read_env_value "AZURE_SUBSCRIPTION_ID")

# Validate all required fields are present
if [ -z "$AZURE_TENANT_ID" ] || [ -z "$AZURE_CLIENT_ID" ] || [ -z "$AZURE_CLIENT_SECRET" ] || [ -z "$AZURE_SUBSCRIPTION_ID" ]; then
    echo -e "${RED}Error: Missing required credentials in .env${NC}"
    echo -e "${YELLOW}Required variables:${NC}"
    echo "  AZURE_TENANT_ID"
    echo "  AZURE_CLIENT_ID"
    echo "  AZURE_CLIENT_SECRET"
    echo "  AZURE_SUBSCRIPTION_ID"
    echo ""
    echo -e "${YELLOW}Please add them to: $ENV_FILE${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Loaded Azure credentials from .env${NC}"
echo -e "  Tenant ID: ${AZURE_TENANT_ID:0:8}...${AZURE_TENANT_ID: -4}"
echo -e "  Client ID: ${AZURE_CLIENT_ID:0:8}...${AZURE_CLIENT_ID: -4}"
echo -e "  Subscription ID: ${AZURE_SUBSCRIPTION_ID:0:8}...${AZURE_SUBSCRIPTION_ID: -4}"
echo ""

# Read storage account name and resource group
CIRRUS_WEB_STORAGE=$(read_env_value "CIRRUS_WEB_STORAGE")
RESOURCE_GROUP=$(read_env_value "RESOURCE_GROUP")

if [ -z "$CIRRUS_WEB_STORAGE" ]; then
    echo -e "${RED}Error: CIRRUS_WEB_STORAGE not found in .env${NC}"
    echo -e "${YELLOW}Please add it to: $ENV_FILE${NC}"
    exit 1
fi

if [ -z "$RESOURCE_GROUP" ]; then
    echo -e "${RED}Error: RESOURCE_GROUP not found in .env${NC}"
    echo -e "${YELLOW}Please add it to: $ENV_FILE${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Storage account name:${NC} $CIRRUS_WEB_STORAGE"
echo -e "${GREEN}✓ Resource group:${NC} $RESOURCE_GROUP"
echo ""

# Create STORAGE_CREDENTIALS JSON
STORAGE_CREDENTIALS=$(cat <<EOF
{
  "clientId": "$AZURE_CLIENT_ID",
  "clientSecret": "$AZURE_CLIENT_SECRET",
  "subscriptionId": "$AZURE_SUBSCRIPTION_ID",
  "tenantId": "$AZURE_TENANT_ID"
}
EOF
)

# Upload secrets to GitHub
echo -e "${YELLOW}Uploading secrets to GitHub repository: $GITHUB_REPO${NC}\n"

# Upload STORAGE_CREDENTIALS as GitHub secret
echo -e "  ${BLUE}Uploading STORAGE_CREDENTIALS...${NC}"
echo "$STORAGE_CREDENTIALS" | gh secret set STORAGE_CREDENTIALS --repo "$GITHUB_REPO"
echo -e "  ${GREEN}✓ STORAGE_CREDENTIALS uploaded${NC}\n"

# Upload CIRRUS_WEB_STORAGE and RESOURCE_GROUP as GitHub variables (not secrets)
echo -e "  ${BLUE}Uploading CIRRUS_WEB_STORAGE (as variable)...${NC}"
gh variable set CIRRUS_WEB_STORAGE --body "$CIRRUS_WEB_STORAGE" --repo "$GITHUB_REPO"
echo -e "  ${GREEN}✓ CIRRUS_WEB_STORAGE uploaded${NC}\n"

echo -e "  ${BLUE}Uploading RESOURCE_GROUP (as variable)...${NC}"
gh variable set RESOURCE_GROUP --body "$RESOURCE_GROUP" --repo "$GITHUB_REPO"
echo -e "  ${GREEN}✓ RESOURCE_GROUP uploaded${NC}\n"

# Summary
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}Setup Complete!${NC}\n"
echo -e "${GREEN}✓ GitHub secrets uploaded to $GITHUB_REPO:${NC}"
echo -e "  • STORAGE_CREDENTIALS (secret)"
echo -e ""
echo -e "${GREEN}✓ GitHub variables uploaded to $GITHUB_REPO:${NC}"
echo -e "  • CIRRUS_WEB_STORAGE (variable)"
echo -e "  • RESOURCE_GROUP (variable)"
echo -e "\n${BLUE}Configuration read from:${NC} $ENV_FILE"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

echo -e "${GREEN}You can now trigger the deploy-wiki workflow!${NC}"
echo -e "${YELLOW}The workflow will automatically deploy when you push to main branch.${NC}"
