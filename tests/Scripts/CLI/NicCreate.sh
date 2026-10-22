#!/usr/bin/env bash
# Smoke test for the network nic create / show / list / delete commands through the built CLI.
#
# Creates a VNet (one subnet) and a Standard public IP as prerequisites, then creates a NIC that
# references both by name, shows it (plain and with the public IP expanded), lists the resource
# group, deletes it, and verifies the delete (a show must now fail with 404 ResourceNotFound).
# The prerequisites are deleted on exit.
#
# NSG association is not exercised: azclips has no command that creates an NSG. It is covered
# offline by tests/Cli.Tests/NetworkInterfaceParameterTests.cs and
# tests/Scenario.Tests/NetworkInterfaceScenarioTests.cs.
#
# Prereqs:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login          # az.exe on Windows
#   3. A resource group must already exist - the script creates and deletes only the NIC, VNet
#      and public IP. If you don't have one:
#        ./artifacts/Debug/Core.Cli/az group create \
#            --subscription-id <id> --name <rg> --location eastus
#
# Usage: SUBSCRIPTION_ID=<id> RESOURCE_GROUP=<rg> ./tests/Scripts/CLI/NicCreate.sh
#
# SUBSCRIPTION_ID and RESOURCE_GROUP are required and deliberately have no default: a smoke test
# must never silently create resources in someone else's subscription. LOCATION is optional.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the NIC, VNet and public IP}"

LOCATION="${LOCATION:-eastus}"
# Per-run names: create is a PUT, so a fixed name would silently overwrite a colliding resource in
# a shared resource group - and cleanup would then destroy it.
SUFFIX=$RANDOM
VNET_NAME="azclips-test-nic-vnet-$SUFFIX"
SUBNET_NAME="azclips-test-subnet"
PIP_NAME="azclips-test-nic-pip-$SUFFIX"
NIC_NAME="azclips-test-nic-$SUFFIX"

# Run unattended: disable the interactive agent so a command error surfaces a classic non-zero
# exit instead of handing off to the agent-mode TUI, which would block on input. No `set -e`: the
# absence checks below rely on show failing.
"$AZ" config set --key agent.enabled --value false

# Succeeds only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed.
verify_absent() {
    local kind=$1 name=$2 out
    if out=$("$AZ" network "$kind" show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$name" 2>&1); then
        echo "$kind '$name' exists in '$RESOURCE_GROUP'." >&2
        return 1
    fi
    [[ "$out" == *"404 Not Found"* && "$out" == *"ResourceNotFound"* ]] && return 0
    echo "Unable to verify that $kind '$name' does not exist:" >&2
    echo "$out" >&2
    return 1
}

# Never touch a resource this run did not create. This has to run before the trap is installed,
# otherwise the exit below would trigger cleanup.
if ! { verify_absent vnet "$VNET_NAME" && verify_absent public-ip "$PIP_NAME" && verify_absent nic "$NIC_NAME"; }; then
    echo "Refusing to run: every name must be verifiably unused before anything is created." >&2
    exit 1
fi

# Each flag is set just before its create: the name was verified free above, so whatever exists
# under it from then on is this run's - including a resource whose PUT was accepted but whose
# polling then failed.
VNET_CREATED=0
PIP_CREATED=0
NIC_CREATED=0

# Delete only what this run created, dependents first: the NIC references the subnet and the
# public IP, so neither can be deleted while it exists. stderr stays visible so a failed delete
# is reported instead of silently leaving resources behind.
cleanup() {
    if (( NIC_CREATED )); then
        "$AZ" network nic delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$NIC_NAME" >/dev/null
    fi
    if (( PIP_CREATED )); then
        "$AZ" network public-ip delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$PIP_NAME" >/dev/null
    fi
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
    --subnets "name=$SUBNET_NAME" address-prefixes=10.0.0.0/24 >/dev/null || exit 1

PIP_CREATED=1
"$AZ" network public-ip create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME" \
    --location "$LOCATION" \
    --sku Standard \
    --allocation-method Static >/dev/null || exit 1

NIC_CREATED=1
"$AZ" network nic create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NIC_NAME" \
    --location "$LOCATION" \
    --vnet-name "$VNET_NAME" \
    --subnet "$SUBNET_NAME" \
    --public-ip-address "$PIP_NAME" \
    --private-ip-address 10.0.0.10 \
    --ip-forwarding true \
    --dns-servers 10.0.0.4 10.0.0.5 \
    --internal-dns-name "$NIC_NAME" \
    --tags owner=azclips scenario=nic-smoke \
    --debug || exit 1

"$AZ" network nic show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NIC_NAME" || exit 1

"$AZ" network nic show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NIC_NAME" \
    --expand ipConfigurations/publicIPAddress || exit 1

"$AZ" network nic list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" || exit 1

"$AZ" network nic delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NIC_NAME" || exit 1

if ! verify_absent nic "$NIC_NAME"; then
    echo "FAIL: NIC '$NIC_NAME' was not confirmed deleted." >&2
    exit 1
fi
NIC_CREATED=0
echo "PASS: NIC '$NIC_NAME' was created, read, listed and deleted."
