FROM mcr.microsoft.com/devcontainers/rust:latest

# RUN apt-get update && apt-get install -y software-properties-common 
# RUN add-apt-repository ppa:dotnet/backports
ENV HOME=/home/vscode

RUN apt-get update && apt-get install -y protobuf-compiler

# Install .NET SDK (for building and running Cirrus Csharp projects)
RUN curl -sSL https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh | bash
RUN curl -sSL https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh | bash /dev/stdin --channel 6.0 --runtime dotnet
ENV DOTNET_ROOT="$HOME/.dotnet"
ENV PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"

RUN chown -R vscode:vscode $HOME/.dotnet

RUN wget https://github.com/PowerShell/PowerShell/releases/download/v7.5.1/powershell_7.5.1-1.deb_amd64.deb
RUN dpkg -i powershell_7.5.1-1.deb_amd64.deb
RUN apt-get install -f
RUN rm powershell_7.5.1-1.deb_amd64.deb