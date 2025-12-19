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

# Also export for current session
export PATH="/workspaces/cirrus/artifacts/Release/Core.Cli:$PATH"

echo "🌐 Setting up browser symlinks for authentication..."
# Check if BROWSER environment variable is set
if [ -n "$BROWSER" ]; then
    sudo ln -sf "$BROWSER" /usr/local/bin/xdg-open
    sudo ln -sf "$BROWSER" /usr/local/bin/gnome-open
    sudo ln -sf "$BROWSER" /usr/local/bin/kfmclient
    echo "✓ Browser symlinks created successfully"
else
    echo "⚠ BROWSER environment variable not set, skipping browser setup"
fi