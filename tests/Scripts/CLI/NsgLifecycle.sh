#!/usr/bin/env bash
# Smoke test for the network nsg create / show / list / delete commands through the built CLI.
#
# Creates a network security group (location taken from the resource group, plus a bare-key tag),
# shows it (plain and with its network interfaces expanded), lists the resource group, deletes
# it, and verifies the delete (a show must now fail with 404 ResourceNotFound).
#
# Prereqs:
#   1. Build:    dotnet build azclips.slnx
#   2. Sign in:  ./artifacts/Debug/Core.Cli/az login          # az.exe on Windows
#   3. A resource group must already exist - the script creates and deletes only the NSG. If you
#      don't have one:
#        ./artifacts/Debug/Core.Cli/az group create \
#            --subscription-id <id> --name <rg> --location eastus
#
# Usage: SUBSCRIPTION_ID=<id> RESOURCE_GROUP=<rg> ./tests/Scripts/CLI/NsgLifecycle.sh
#
# SUBSCRIPTION_ID and RESOURCE_GROUP are required and deliberately have no default: a smoke test
# must never silently create resources in someone else's subscription.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the NSG}"

# Per-run name: create is a PUT, so a fixed name would silently overwrite a colliding NSG in a
# shared resource group - and cleanup would then destroy it.
NSG_NAME="azclips-test-nsg-$RANDOM"

# Run unattended: disable the interactive agent so a command error surfaces a classic non-zero
# exit instead of handing off to the agent-mode TUI, which would block on input. No `set -e`: the
# absence checks below rely on show failing.
"$AZ" config set --key agent.enabled --value false

# Succeeds only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed.
verify_absent() {
    local name=$1 out
    if out=$("$AZ" network nsg show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$name" 2>&1); then
        echo "NSG '$name' exists in '$RESOURCE_GROUP'." >&2
        return 1
    fi
    [[ "$out" == *"404 Not Found"* && "$out" == *"ResourceNotFound"* ]] && return 0
    echo "Unable to verify that NSG '$name' does not exist:" >&2
    echo "$out" >&2
    return 1
}

# Never touch a resource this run did not create. This has to run before the trap is installed,
# otherwise the exit below would trigger cleanup.
if ! verify_absent "$NSG_NAME"; then
    echo "Refusing to run: the NSG name must be verifiably unused before anything is created." >&2
    exit 1
fi

# Set just before the create: the name was verified free above, so whatever exists under it from
# then on is this run's - including an NSG whose PUT was accepted but whose polling then failed.
NSG_CREATED=0

# stderr stays visible so a failed delete is reported instead of silently leaving the NSG behind.
cleanup() {
    if (( NSG_CREATED )); then
        "$AZ" network nsg delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$NSG_NAME" >/dev/null
    fi
}
trap cleanup EXIT

NSG_CREATED=1
"$AZ" network nsg create \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NSG_NAME" \
    --tags owner=azclips scenario=nsg-smoke smoke \
    --debug || exit 1

"$AZ" network nsg show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NSG_NAME" || exit 1

"$AZ" network nsg show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NSG_NAME" \
    --expand networkInterfaces || exit 1

"$AZ" network nsg list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" || exit 1

"$AZ" network nsg delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NSG_NAME" || exit 1

if ! verify_absent "$NSG_NAME"; then
    echo "FAIL: NSG '$NSG_NAME' was not confirmed deleted." >&2
    exit 1
fi
NSG_CREATED=0
echo "PASS: NSG '$NSG_NAME' was created, read, listed and deleted."
