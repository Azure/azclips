#!/usr/bin/env bash
set -euo pipefail

if command -v docker >/dev/null 2>&1 && docker version >/dev/null 2>&1; then
  docker buildx version
  exit 0
fi

sudo apt-get update
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends docker.io docker-buildx
sudo systemctl start docker
# The hosted agent user runs subsequent pipeline steps outside the docker group.
sudo chmod 666 /var/run/docker.sock
docker version
docker buildx version
