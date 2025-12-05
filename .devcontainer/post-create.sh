#!/bin/bash
set -e

echo "⚡ Installing platyPS..."
pwsh -c "Install-Module -Name platyPS -Force -Scope CurrentUser"

echo "🔐 Installing Az.Accounts for authentication..."
pwsh -c "Install-Module -Name Az.Accounts -Force -Scope CurrentUser"

echo "🔧 Configuring PATH for azv2 command..."
# Add to .bashrc for persistent PATH across all bash sessions
if ! grep -q 'export PATH="/workspaces/cirrus/bin:\$PATH"' ~/.bashrc; then
    echo 'export PATH="/workspaces/cirrus/bin:$PATH"' >> ~/.bashrc
    echo "✓ Added /workspaces/cirrus/bin to ~/.bashrc"
else
    echo "✓ PATH already configured in ~/.bashrc"
fi

# Also export for current session
export PATH="/workspaces/cirrus/bin:$PATH"