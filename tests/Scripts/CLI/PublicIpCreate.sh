#!/usr/bin/env bash
# Smoke test for the network public-ip create / show / list / delete commands through the built CLI.
#
# Creates a zone-redundant Standard public IP with a DNS label and tags, shows it (plain and with
# the IP configuration expanded), lists the resource group, deletes it, and verifies the delete (a
# show must now fail with 404 ResourceNotFound).
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
# must never silently create resources in someone else's subscription. LOCATION is optional.
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

: "${SUBSCRIPTION_ID:?is required - set it to the subscription this smoke test may create resources in}"
: "${RESOURCE_GROUP:?is required - set it to an existing resource group; the script creates and deletes only the public IP}"

LOCATION="${LOCATION:-eastus}"
# Per-run names: create is a PUT, so a fixed name would silently overwrite a colliding address in
# a shared resource group - and cleanup would then destroy it. The DNS label additionally has to be
# unique within the whole region.
SUFFIX=$RANDOM
PIP_NAME="azclips-test-pip-$SUFFIX"
DNS_NAME="$PIP_NAME-$RANDOM"

# Run unattended: disable the interactive agent so a command error surfaces a classic non-zero
# exit instead of handing off to the agent-mode TUI, which would block on input. No `set -e`: the
# absence checks below rely on show failing.
"$AZ" config set --key agent.enabled --value false

# Succeeds only on a verified "404 Not Found ... ResourceNotFound". Any other failure - auth,
# throttling, transport, or a missing resource group (ResourceGroupNotFound) - is not proof of
# absence, so it fails closed.
verify_absent() {
    local out
    if out=$("$AZ" network public-ip show \
            --subscription-id "$SUBSCRIPTION_ID" \
            --resource-group "$RESOURCE_GROUP" \
            --name "$PIP_NAME" 2>&1); then
        echo "public-ip '$PIP_NAME' exists in '$RESOURCE_GROUP'." >&2
        return 1
    fi
    [[ "$out" == *"404 Not Found"* && "$out" == *"ResourceNotFound"* ]] && return 0
    echo "Unable to verify that public-ip '$PIP_NAME' does not exist:" >&2
    echo "$out" >&2
    return 1
}

# Never touch an address this run did not create. This has to run before the trap is installed,
# otherwise the exit below would trigger cleanup.
if ! verify_absent; then
    echo "Refusing to run: the name must be verifiably unused before anything is created." >&2
    exit 1
fi

# Set just before the create: the name was verified free above, so whatever exists under it from
# then on is this run's - including an address whose PUT was accepted but whose polling then failed.
PIP_CREATED=0

# Delete only what this run created. stderr stays visible so a failed delete is reported instead
# of silently leaving the address behind.
cleanup() {
    if (( PIP_CREATED )); then
        "$AZ" network public-ip delete --subscription-id "$SUBSCRIPTION_ID" --resource-group "$RESOURCE_GROUP" --name "$PIP_NAME" >/dev/null
    fi
}
trap cleanup EXIT

PIP_CREATED=1
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
    --tags owner=azclips scenario=public-ip-smoke temporary \
    --debug || exit 1

"$AZ" network public-ip show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME" || exit 1

"$AZ" network public-ip show \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME" \
    --expand ipConfiguration || exit 1

"$AZ" network public-ip list \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" || exit 1

# A finished delete prints nothing.
DELETE_OUTPUT=$("$AZ" network public-ip delete \
    --subscription-id "$SUBSCRIPTION_ID" \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PIP_NAME") || exit 1
if [[ -n "$DELETE_OUTPUT" ]]; then
    echo "FAIL: delete wrote output: $DELETE_OUTPUT" >&2
    exit 1
fi

if ! verify_absent; then
    echo "FAIL: public-ip '$PIP_NAME' was not confirmed deleted." >&2
    exit 1
fi
PIP_CREATED=0
echo "PASS: public-ip '$PIP_NAME' was created, read, listed and deleted."
