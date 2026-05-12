#!/usr/bin/env bash
# CLI equivalent of tests/Scripts/PS/VMCreate.ps1
# Uses the kebab-case CLI parameter names registered in src/Extensions/Compute/ExtensionIndex.cs

AZV2="${AZV2:-./bin/azv2}"

"$AZV2" vm create \
    --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 \
    --resource-group yabo-test-group \
    --name yabo-test-vm-2 \
    --location eastus \
    --vm-size Standard_D2s_v3 \
    --admin-username Artemis \
    --admin-password 'ArtemisCriticalX4!' \
    --network-interface-id /subscriptions/0c302431-1ad0-4187-87c9-d4cebcf7edc2/resourceGroups/yabo-test-group/providers/Microsoft.Network/networkInterfaces/yabo-test-nic-2

"$AZV2" vm create-combined --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 --resource-group yabo-test-group --name yabo-test-vm-3 --location eastus --vm-size Standard_D2s_v3 --admin-username Artemis --admin-password 'ArtemisCriticalX4!' --network-interface-name yabo-test-nic-3 --virtual-network-name yabo-test-vnet-3 --address-prefixes 10.0.0.0/16 --subnet-names yabo-test-subnet1 --subnet-address-prefixes 10.0.2.0/24 --network-security-group-name yabo-test-nsg-3 --rule-names rdp --rule-protocols tcp --rule-directions Inbound --rule-priorities 1001 --rule-source-address-prefixes "*" --rule-source-port-ranges "*" --rule-destination-port-ranges 3389 --rule-destination-address-prefixes "*" --rule-accesses Allow
