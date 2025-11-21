#!/bin/bash
set -e

echo "⚡ Upgrading PowerShell to 7.6.0-preview.5..."

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)
        ARCH="x64"
        ;;
    aarch64|arm64)
        ARCH="arm64"
        ;;
    *)
        echo "❌ Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

# Set download URL for Linux
PWSH_VERSION="7.6.0-preview.5"
DOWNLOAD_URL="https://github.com/PowerShell/PowerShell/releases/download/v${PWSH_VERSION}/powershell-${PWSH_VERSION}-linux-${ARCH}.tar.gz"

echo "📦 Downloading PowerShell ${PWSH_VERSION} for ${ARCH}..."
wget -q --show-progress "$DOWNLOAD_URL" -O /tmp/powershell.tar.gz

# Create installation directory
INSTALL_DIR="/opt/microsoft/powershell/7-preview"
echo "🔧 Installing to ${INSTALL_DIR}..."
sudo mkdir -p "$INSTALL_DIR"

# Extract and install
sudo tar -xzf /tmp/powershell.tar.gz -C "$INSTALL_DIR"

# Set execute permissions
sudo chmod +x "${INSTALL_DIR}/pwsh"

# Create symbolic link
sudo ln -sf "${INSTALL_DIR}/pwsh" /usr/local/bin/pwsh

# Clear bash hash table
hash -r 2>/dev/null || true

# Clean up
rm /tmp/powershell.tar.gz

echo "✅ PowerShell upgraded successfully!"
pwsh --version
