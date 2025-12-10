#!/bin/bash
set -e

echo "⚡ Installing platyPS..."
pwsh -c "Install-Module -Name platyPS -Force -Scope CurrentUser"

echo "🔐 Installing Az.Accounts for authentication..."
pwsh -c "Install-Module -Name Az.Accounts -Force -Scope CurrentUser"

echo "🔧 Configuring PATH for az command..."
# Add to .bashrc for persistent PATH across all bash sessions
if ! grep -q 'export PATH="/workspaces/cirrus/artifacts/Release/Core.Cli:\$PATH"' ~/.bashrc; then
    echo 'export PATH="/workspaces/cirrus/artifacts/Release/Core.Cli:$PATH"' >> ~/.bashrc
    echo "✓ Added /workspaces/cirrus/artifacts/Release/Core.Cli to ~/.bashrc"
else
    echo "✓ PATH already configured in ~/.bashrc"
fi

# Set extension directory
if ! grep -q 'export AZURE_CLI_EXTENSION_DIRECTORY=' ~/.bashrc; then
    echo 'export AZURE_CLI_EXTENSION_DIRECTORY="/workspaces/cirrus/artifacts/Release/Core.Cli"' >> ~/.bashrc
    echo "✓ Added AZURE_CLI_EXTENSION_DIRECTORY to ~/.bashrc"
else
    echo "✓ AZURE_CLI_EXTENSION_DIRECTORY already configured in ~/.bashrc"
fi

# Also export for current session
export PATH="/workspaces/cirrus/artifacts/Release/Core.Cli:$PATH"
export AZURE_CLI_EXTENSION_DIRECTORY="/workspaces/cirrus/artifacts/Release/Core.Cli"