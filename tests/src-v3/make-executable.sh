#!/bin/bash
# Make test scripts executable
chmod +x /workspaces/cirrus/tests/src-v3/cli/*.sh
chmod +x /workspaces/cirrus/tests/src-v3/powershell/*.ps1 2>/dev/null || true
echo "Test scripts are now executable"
