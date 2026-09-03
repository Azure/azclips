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
  sudo apt-get update
  sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends "${packages[@]}"
fi

if ! docker version >/dev/null 2>&1; then
  sudo systemctl start docker
  # The hosted agent user runs subsequent pipeline steps outside the docker group.
  sudo chmod 666 /var/run/docker.sock
fi

docker version
docker buildx version
