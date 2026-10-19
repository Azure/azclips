#!/usr/bin/env bash
set -euo pipefail

packages=()
if ! command -v docker >/dev/null 2>&1; then
  packages+=(docker.io)
fi
if ! command -v docker >/dev/null 2>&1 || ! docker buildx version >/dev/null 2>&1; then
  packages+=(docker-buildx)
fi

if (( ${#packages[@]} > 0 )); then
  sudo apt-get -o DPkg::Lock::Timeout=300 update
  sudo env DEBIAN_FRONTEND=noninteractive apt-get -o DPkg::Lock::Timeout=300 install -y --no-install-recommends "${packages[@]}"
fi

if ! docker version >/dev/null 2>&1; then
  sudo systemctl start docker
  # Grant Docker access only to the current ephemeral build-agent identity.
  sudo chown "$(id -u):$(id -g)" /var/run/docker.sock
  sudo chmod 660 /var/run/docker.sock
fi

docker version
docker buildx version
