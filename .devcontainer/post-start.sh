#!/bin/bash
# Post-start script - runs every time the container starts

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
