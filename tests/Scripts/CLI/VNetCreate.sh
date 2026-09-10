#!/usr/bin/env bash
# Smoke test for the network vnet create / show / list / delete commands through the built CLI.
#
# Creates a VNet with two subnets, shows it (plain and expanded), lists the resource group,
# deletes it, then shows again to confirm removal (the final show is expected to report the
# VNet no longer exists).
#
# Prereqs:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login          # az.exe on Windows
#   3. A resource group must already exist - the script creates and deletes only the VNet.
#      If you don't have one:
#        ./artifacts/Debug/Core.Cli/az group create \
#            --subscription-id <id> --name <rg> --location eastus
#
# Usage: SUBSCRIPTION_ID=<id> RESOURCE_GROUP=<rg> ./tests/Scripts/CLI/VNetCreate.sh
#
# SUBSCRIPTION_ID and RESOURCE_GROUP fall back to the author's test values below, so pass your
# own. LOCATION, VNET_NAME and SUBNET_NAME are overridable the same way.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

SUBSCRIPTION_ID="${SUBSCRIPTION_ID:-f758ac53-3e63-4317-a956-0997793808d7}"
RESOURCE_GROUP="${RESOURCE_GROUP:-yabo-test-group-2}"
LOCATION="${LOCATION:-eastus}"
VNET_NAME="${VNET_NAME:-azclips-test-vnet-1}"
SUBNET_NAME="${SUBNET_NAME:-azclips-test-subnet-1}"

# Run unattended: disable the interactive agent so a command error (e.g. the final show after
# the VNet is deleted) surfaces a classic non-zero exit instead of handing off to the agent-mode
# TUI, which would block on input. No `set -e`: the final show is EXPECTED to fail once the VNet
# is gone, and it must still run to verify the delete.
"$AZ" config set --key agent.enabled --value false

# Always attempt cleanup, even if a step above fails, so a partial run leaves no VNet behind.
cleanup() {
    "$AZ" network vnet delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$VNET_NAME" >/dev/null 2>&1
}
trap cleanup EXIT

"$AZ" network vnet create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME" \
    --location "$LOCATION" \
    --address-prefixes 10.0.0.0/16 \
    --dns-servers 10.0.0.4 10.0.0.5 \
    --tags owner=azclips scenario=vnet-smoke \
    --flowtimeout 10 \
    --subnets "name=$SUBNET_NAME" address-prefixes=10.0.2.0/24 default-outbound-access=false \
    --subnets name=data-subnet address-prefixes=10.0.3.0/24 service-endpoints=Microsoft.Storage \
    --debug

"$AZ" network vnet show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME"

"$AZ" network vnet show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME" \
    --expand subnets/ipConfigurations

"$AZ" network vnet list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

"$AZ" network vnet delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME"

# Expected to fail: the VNet is gone.
"$AZ" network vnet show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME"
