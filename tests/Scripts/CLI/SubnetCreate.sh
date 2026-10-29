#!/usr/bin/env bash
# Smoke test for the network vnet subnet create / show / list / delete commands through the built CLI.
#
# Creates an NSG and a VNet (no subnets) as prerequisites, then creates a subnet that references
# the NSG by name, with a service endpoint, a delegation in the dotted legacy form and default
# outbound access off. It shows the subnet (plain and with the NSG expanded), lists the VNet's
# subnets, deletes the subnet (which must print nothing), and verifies the delete (a show must
# now fail with 404). The prerequisites are deleted on exit.
#
# Prereqs:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login          # az.exe on Windows
#   3. A resource group must already exist - the script creates and deletes only the NSG, VNet
#      and subnet. If you don't have one:
#        ./artifacts/Debug/Core.Cli/az group create \
#            --subscription-id <id> --name <rg> --location eastus
#
# Usage: SUBSCRIPTION_ID=<id> RESOURCE_GROUP=<rg> ./tests/Scripts/CLI/SubnetCreate.sh
#
# SUBSCRIPTION_ID and RESOURCE_GROUP are required and deliberately have no default: a smoke test
# must never silently create resources in someone else's subscription. LOCATION is optional.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the NSG, VNet and subnet}"

LOCATION="${LOCATION:-eastus}"
# Per-run names: create is a PUT, so a fixed name would silently overwrite a colliding resource in
# a shared resource group - and cleanup would then destroy it. The subnet lives in this run's
# VNet, so its name cannot collide.
SUFFIX=$RANDOM
NSG_NAME="azclips-test-subnet-nsg-$SUFFIX"
VNET_NAME="azclips-test-subnet-vnet-$SUFFIX"
SUBNET_NAME="azclips-test-subnet"
SCOPE=(--subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP")
SUBNET=("${SCOPE[@]}" --vnet-name "$VNET_NAME" --name "$SUBNET_NAME")

# Run unattended: disable the interactive agent so a command error surfaces a classic non-zero
# exit instead of handing off to the agent-mode TUI, which would block on input. No `set -e`: the
# absence checks below rely on show failing.
"$AZ" config set --key agent.enabled --value false

# Succeeds only on a verified 404 with the given error code. Any other failure - auth, throttling,
# transport, or a missing resource group (ResourceGroupNotFound) - is not proof of absence, so it
# fails closed. Top-level resources answer ResourceNotFound; the network RP answers NotFound for a
# missing child subnet of an existing VNet.
verify_absent() {
    local code=$1 out
    shift
    if out=$("$AZ" network "$@" 2>&1); then
        echo "'$*' found an existing resource." >&2
        return 1
    fi
    [[ "$out" == *"404"* && "$out" == *"\"$code\""* ]] && return 0
    echo "Unable to verify absence with '$*':" >&2
    echo "$out" >&2
    return 1
}

# Never touch a resource this run did not create. This has to run before the trap is installed,
# otherwise the exit below would trigger cleanup.
if ! { verify_absent ResourceNotFound nsg show "${SCOPE[@]}" --name "$NSG_NAME" \
        && verify_absent ResourceNotFound vnet show "${SCOPE[@]}" --name "$VNET_NAME"; }; then
    echo "Refusing to run: every name must be verifiably unused before anything is created." >&2
    exit 1
fi

# Each flag is set just before its create: the name was verified free above, so whatever exists
# under it from then on is this run's - including a resource whose PUT was accepted but whose
# polling then failed.
NSG_CREATED=0
VNET_CREATED=0

# Delete only what this run created. Deleting the VNet takes the subnet with it and releases the
# NSG association, so the NSG goes last. stderr stays visible so a failed delete is reported
# instead of silently leaving resources behind.
cleanup() {
    if (( VNET_CREATED )); then
        "$AZ" network vnet delete "${SCOPE[@]}" --name "$VNET_NAME" >/dev/null
    fi
    if (( NSG_CREATED )); then
        "$AZ" network nsg delete "${SCOPE[@]}" --name "$NSG_NAME" >/dev/null
    fi
}
trap cleanup EXIT

NSG_CREATED=1
"$AZ" network nsg create "${SCOPE[@]}" --name "$NSG_NAME" --location "$LOCATION" >/dev/null || exit 1

VNET_CREATED=1
"$AZ" network vnet create "${SCOPE[@]}" \
    --name "$VNET_NAME" \
    --location "$LOCATION" \
    --address-prefixes 10.20.0.0/16 >/dev/null || exit 1

"$AZ" network vnet subnet create "${SUBNET[@]}" \
    --address-prefixes 10.20.1.0/24 \
    --nsg "$NSG_NAME" \
    --service-endpoints Microsoft.Storage \
    --delegations Microsoft.Web.serverFarms \
    --default-outbound-access false \
    --debug || exit 1

"$AZ" network vnet subnet show "${SUBNET[@]}" || exit 1
"$AZ" network vnet subnet show "${SUBNET[@]}" --expand networkSecurityGroup || exit 1
"$AZ" network vnet subnet list "${SCOPE[@]}" --vnet-name "$VNET_NAME" || exit 1

if ! out=$("$AZ" network vnet subnet delete "${SUBNET[@]}"); then
    exit 1
fi
if [[ -n "$out" ]]; then
    echo "FAIL: subnet delete printed output: $out" >&2
    exit 1
fi

if ! verify_absent NotFound vnet subnet show "${SUBNET[@]}"; then
    echo "FAIL: subnet '$SUBNET_NAME' was not confirmed deleted." >&2
    exit 1
fi
echo "PASS: subnet '$SUBNET_NAME' was created, read, listed and deleted."