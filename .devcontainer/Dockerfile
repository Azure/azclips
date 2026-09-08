# Dockerfile for Azure PowerShell devcontainer
# Use official .NET 10.0 image (supports multi-arch: amd64, arm64)
# Must satisfy the SDK version pinned in global.json.
FROM mcr.microsoft.com/devcontainers/dotnet:10.0

# Registry used by npm inside the container. Defaults to the public registry so a
# plain clone builds anywhere; override via NPM_REGISTRY (see .devcontainer/README.md)
# when the host network blocks registry.npmjs.org and requires a proxy feed.
ARG NPM_REGISTRY=https://registry.npmjs.org/
ENV NPM_CONFIG_REGISTRY=${NPM_REGISTRY}

# Set working directory
WORKDIR /workspaces/azclips
