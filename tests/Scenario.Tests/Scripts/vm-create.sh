#!/usr/bin/env bash
set -euo pipefail

az="$AZCLIPS_TEST_AZ"
sub="00000000-0000-0000-0000-000000000000"

rg_json=$("$az" group create --subscription-id "$sub" --name scenario-vm-rg --location westus2 --output json)
rg_name=$(printf '%s' "$rg_json" | sed -n 's/.*"name": *"\([^"]*\)".*/\1/p' | head -n 1)

vm_json=$("$az" vm create --subscription-id "$sub" --resource-group "$rg_name" --name scenario-vm --location westus2 \
  --size Standard_D2s_v3 --admin-username azadmin --admin-password 'PlaceholderP@ss1' \
  --image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest \
  --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet --network-security-group-name scenario-vm-nsg \
  --subnet-names default --subnet-address-prefixes 10.0.0.0/24 --address-prefixes 10.0.0.0/16 --output json)

printf '%s' "$vm_json" | grep -q '"provisioningState": "Succeeded"'
vm_name=$(printf '%s' "$vm_json" | sed -n 's/.*"name": *"\([^"]*\)".*/\1/p' | head -n 1)
echo "created $vm_name in $rg_name: Succeeded"
