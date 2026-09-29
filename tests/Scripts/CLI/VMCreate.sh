#!/usr/bin/env bash
# Smoke test for the vm create / show / delete commands through the built CLI.
#
# Creates a VM (and its NIC, VNet, subnet, and NSG rule), shows it, deletes it,
# then shows again to confirm removal (the final show is expected to report the
# VM no longer exists).
#
# Prereqs: build the solution first with dotnet build, then sign in.
#
# Usage: ./tests/Scripts/CLI/VMCreate.sh
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Resolve the built CLI (az.exe on Windows, az elsewhere).
AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az"
[[ -x "$AZ" ]] || AZ="$REPO_ROOT/artifacts/Debug/Core.Cli/az.exe"

# Run unattended: disable the interactive agent so a command error (e.g. the final
# show after the VM is deleted) surfaces a classic non-zero exit instead of handing
# off to the agent-mode TUI, which would block on input. No `set -e`: the final show
# is EXPECTED to fail once the VM is gone, and it must still run to verify the delete.
"$AZ" config set --key agent.enabled --value false

"$AZ" vm create \
    --subscription-id f758ac53-3e63-4317-a956-0997793808d7 \
    --resource-group yabo-test-group-2 \
    --name yabo-test-vm-2 \
    --location eastus \
    --size Standard_D2s_v3 \
    --admin-username Artemis \
    --admin-password 'ArtemisCriticalX4!' \
    --network-interface-name yabo-test-nic-2 \
    --virtual-network-name yabo-test-vnet-2 \
    --address-prefixes 10.0.0.0/16 \
    --subnet-names yabo-test-subnet-2 \
    --subnet-address-prefixes 10.0.2.0/24 \
    --network-security-group-name yabo-test-nsg-2 \
    --rule-names rdp \
    --rule-protocols tcp \
    --rule-directions Inbound \
    --rule-priorities 1001 \
    --rule-source-address-prefixes "*" \
    --rule-source-port-ranges "*" \
    --rule-destination-port-ranges 3389 \
    --rule-destination-address-prefixes "*" \
    --rule-accesses Allow \
    --debug

"$AZ" vm show \
    --subscription-id f758ac53-3e63-4317-a956-0997793808d7 \
    --resource-group yabo-test-group-2 \
    --name yabo-test-vm-2

"$AZ" vm delete \
    --subscription-id f758ac53-3e63-4317-a956-0997793808d7 \
    --resource-group yabo-test-group-2 \
    --name yabo-test-vm-2

"$AZ" vm show \
    --subscription-id f758ac53-3e63-4317-a956-0997793808d7 \
    --resource-group yabo-test-group-2 \
    --name yabo-test-vm-2
