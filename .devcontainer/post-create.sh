#!/bin/bash
set -e

echo "� Updating npm..."

echo "⚡ Installing platyPS..."
pwsh -c "Install-Module -Name platyPS -Force -Scope CurrentUser"

echo "🔐 Installing Az.Accounts for authentication..."
pwsh -c "Install-Module -Name Az.Accounts -Force -Scope CurrentUser"
