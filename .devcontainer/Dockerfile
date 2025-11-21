# Dockerfile for Azure PowerShell devcontainer
# Use official .NET 10.0 image (supports multi-arch: amd64, arm64)
FROM mcr.microsoft.com/devcontainers/dotnet:10.0

RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --version 10.0.100 --install-dir /usr/share/dotnet \
    && rm dotnet-install.sh

# Set working directory
WORKDIR /workspaces/cirrus

