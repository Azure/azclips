#!/usr/bin/env bash
# Smoke test for the network public-ip create / show / list / delete commands through the built CLI.
#
# Creates a zone-redundant Standard public IP with a DNS label and tags, shows it (plain and
# expanded), lists the resource group, deletes it, then shows again to confirm removal (the
# final show is expected to report the address no longer exists).
#
# Prereqs:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login          # az.exe on Windows
#   3. A resource group must already exist - the script creates and deletes only the public IP.
#      If you don't have one:
#        ./artifacts/Debug/Core.Cli/az group create \
#            --subscription-id <id> --name <rg> --location eastus
#
# Usage: SUBSCRIPTION_ID=<id> RESOURCE_GROUP=<rg> ./tests/Scripts/CLI/PublicIpCreate.sh
#
# SUBSCRIPTION_ID and RESOURCE_GROUP are required and deliberately have no default: a smoke test
# must never silently create resources in someone else's subscription. LOCATION, PIP_NAME and
# DNS_NAME are optional overrides.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the public IP}"

LOCATION="${LOCATION:-eastus}"
# Both names get a per-run suffix. Cleanup below deletes PIP_NAME unconditionally and create is a
# PUT, so a fixed default would silently overwrite a colliding address in the caller's resource
# group and then destroy it - most likely another developer's run against a shared group. The DNS
# label additionally has to be unique within the whole region.
PIP_NAME="${PIP_NAME:-azclips-test-pip-$RANDOM}"
DNS_NAME="${DNS_NAME:-$PIP_NAME-$RANDOM}"

# Run unattended: disable the interactive agent so a command error (e.g. the final show after
# the address is deleted) surfaces a classic non-zero exit instead of handing off to the
# agent-mode TUI, which would block on input. No `set -e`: the final show is EXPECTED to fail
# once the address is gone, and it must still run to verify the delete.
"$AZ" config set --key agent.enabled --value false

# Never touch an address this run did not create: cleanup deletes PIP_NAME unconditionally, so an
# overridden name that already exists would be overwritten and then destroyed. This has to run
# before the trap is installed, otherwise the exit below would delete that very address.
if "$AZ" network public-ip show \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$PIP_NAME" >/dev/null 2>&1; then
    echo "public-ip '$PIP_NAME' already exists in '$RESOURCE_GROUP'; refusing to overwrite and delete it. Unset PIP_NAME to get a generated name." >&2
    exit 1
fi

# Always attempt cleanup, even if a step above fails, so a partial run leaves no address behind.
cleanup() {
    "$AZ" network public-ip delete \
        --subscription-id "$SUBSCRIPTION_ID" \
        --resource-group "$RESOURCE_GROUP" \
        --name "$PIP_NAME" >/dev/null 2>&1
}
trap cleanup EXIT

"$AZ" network public-ip create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME" \
    --location "$LOCATION" \
    --sku Standard \
    --allocation-method Static \
    --version IPv4 \
    --dns-name "$DNS_NAME" \
    --idle-timeout 10 \
    --zone 1 2 3 \
    --tags owner=azclips scenario=public-ip-smoke \
    --debug

"$AZ" network public-ip show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME"

"$AZ" network public-ip show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME" \
    --expand ipConfiguration

"$AZ" network public-ip list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP"

"$AZ" network public-ip delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME"

# Expected to fail: the public IP address is gone.
"$AZ" network public-ip show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME"
