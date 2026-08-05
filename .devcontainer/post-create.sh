#!/bin/bash
set -e

echo "⚡ Installing Microsoft.PowerShell.PlatyPS..."
pwsh -c "Install-Module -Name Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1 -Force -Scope CurrentUser"

echo "🔐 Installing Az.Accounts for authentication..."
pwsh -c "Install-Module -Name Az.Accounts -Force -Scope CurrentUser"

echo "🔧 Configuring PATH for az command..."
# Add to .bashrc for persistent PATH across all bash sessions
if ! grep -q 'export PATH="/workspaces/azclips/artifacts/Release/Core.Cli:\$PATH"' ~/.bashrc; then
    echo 'export PATH="/workspaces/azclips/artifacts/Release/Core.Cli:$PATH"' >> ~/.bashrc
    echo "✓ Added /workspaces/azclips/artifacts/Release/Core.Cli to ~/.bashrc"
else
    echo "✓ PATH already configured in ~/.bashrc"
fi

# Also export for current session
export PATH="/workspaces/azclips/artifacts/Release/Core.Cli:$PATH"
