#!/usr/bin/env bash
# Smoke test for the network vnet create / show / list / delete commands through the built CLI.
#
# Creates a VNet with two subnets, shows it (plain and with subnet IP configurations expanded),
# lists the resource group, deletes it (the delete must print nothing), and verifies the delete
# (a show must now fail with 404 ResourceNotFound).
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
# SUBSCRIPTION_ID and RESOURCE_GROUP are required and deliberately have no default: a smoke test
# must never silently create resources in someone else's subscription. LOCATION is optional.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the VNet}"

LOCATION="${LOCATION:-eastus}"
# Per-run name: create is a PUT, so a fixed name would silently overwrite a colliding VNet in a
# shared resource group - and cleanup would then destroy it.
VNET_NAME="azclips-test-vnet-$RANDOM"

# Run unattended: disable the interactive agent so a command error surfaces a classic non-zero
# exit instead of handing off to the agent-mode TUI, which would block on input. No `set -e`: the
# absence checks below rely on show failing.
"$AZ" config set --key agent.enabled --value false

# Succeeds only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed.
verify_absent() {
    local out
    if out=$("$AZ" network vnet show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$VNET_NAME" 2>&1); then
        echo "VNet '$VNET_NAME' exists in '$RESOURCE_GROUP'." >&2
        return 1
    fi
    [[ "$out" == *"404 Not Found"* && "$out" == *"ResourceNotFound"* ]] && return 0
    echo "Unable to verify that VNet '$VNET_NAME' does not exist:" >&2
    echo "$out" >&2
    return 1
}

# Never touch a resource this run did not create. This has to run before the trap is installed,
# otherwise the exit below would trigger cleanup.
if ! verify_absent; then
    echo "Refusing to run: the VNet name must be verifiably unused before anything is created." >&2
    exit 1
fi

# Set just before the create: the name was verified free above, so whatever exists under it from
# then on is this run's - including a VNet whose PUT was accepted but whose polling then failed.
VNET_CREATED=0

# Delete only what this run created. stderr stays visible so a failed delete is reported instead
# of silently leaving the VNet behind.
cleanup() {
    if (( VNET_CREATED )); then
        "$AZ" network vnet delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$VNET_NAME" >/dev/null
    fi
}
trap cleanup EXIT

VNET_CREATED=1
"$AZ" network vnet create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME" \
    --location "$LOCATION" \
    --address-prefixes 10.0.0.0/16 \
    --dns-servers 10.0.0.4 10.0.0.5 \
    --tags owner=azclips scenario=vnet-smoke \
    --flowtimeout 10 \
    --subnets name=web address-prefixes=10.0.2.0/24 default-outbound-access=false \
    --subnets name=data address-prefixes=10.0.3.0/24 service-endpoints=Microsoft.Storage \
    --debug || exit 1

"$AZ" network vnet show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME" || exit 1

"$AZ" network vnet show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME" \
    --expand subnets/ipConfigurations || exit 1

"$AZ" network vnet list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" || exit 1

DELETE_OUTPUT=$("$AZ" network vnet delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$VNET_NAME") || exit 1
if [[ -n "$DELETE_OUTPUT" ]]; then
    echo "FAIL: delete printed output; it must be silent:" >&2
    echo "$DELETE_OUTPUT" >&2
    exit 1
fi

if ! verify_absent; then
    echo "FAIL: VNet '$VNET_NAME' was not confirmed deleted." >&2
    exit 1
fi
VNET_CREATED=0
echo "PASS: VNet '$VNET_NAME' was created, read, listed and deleted."