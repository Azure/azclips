# Azclips CLI live test template.
# Generated scripts should be written under tests/Scripts/CLI/live-test-scripts/<module-name>/.
# Keep this as a simple command collection like tests/Scripts/CLI/VMCreate.sh.

SUBSCRIPTION_ID="${AZCLIPS_SUBSCRIPTION_ID}"
RUN_ID="${AZCLIPS_LIVE_TEST_RUN_ID:-$(uuidgen | tr '[:upper:]' '[:lower:]' | cut -c1-8)}"
NAME_PREFIX="azclips-vm"

RESOURCE_GROUP="${NAME_PREFIX}-rg-${RUN_ID}"
LOCATION="eastus"
VM_NAME="${NAME_PREFIX}-vm-${RUN_ID}"
NETWORK_INTERFACE_NAME="${NAME_PREFIX}-nic-${RUN_ID}"
VIRTUAL_NETWORK_NAME="${NAME_PREFIX}-vnet-${RUN_ID}"
SUBNET_NAME="${NAME_PREFIX}-subnet-${RUN_ID}"
NETWORK_SECURITY_GROUP_NAME="${NAME_PREFIX}-nsg-${RUN_ID}"

vm create `
    --subscription-id "$SUBSCRIPTION_ID" `
    --resource-group "$RESOURCE_GROUP" `
    --name "$VM_NAME" `
    --location "$LOCATION" `
    --vm-size Standard_D2s_v3 `
    --admin-username azclipsadmin `
    --admin-password "$AZCLIPS_VM_ADMIN_PASSWORD" `
    --network-interface-name "$NETWORK_INTERFACE_NAME" `
    --virtual-network-name "$VIRTUAL_NETWORK_NAME" `
    --address-prefixes 10.0.0.0/16 `
    --subnet-names "$SUBNET_NAME" `
    --subnet-address-prefixes 10.0.2.0/24 `
    --network-security-group-name "$NETWORK_SECURITY_GROUP_NAME" `
    --rule-names rdp `
    --rule-protocols tcp `
    --rule-directions Inbound `
    --rule-priorities 1001 `
    --rule-source-address-prefixes "*" `
    --rule-source-port-ranges "*" `
    --rule-destination-port-ranges 3389 `
    --rule-destination-address-prefixes "*" `
    --rule-accesses Allow `
    --debug

vm show `
    --subscription-id "$SUBSCRIPTION_ID" `
    --resource-group "$RESOURCE_GROUP" `
    --name "$VM_NAME"

vm delete `
    --subscription-id "$SUBSCRIPTION_ID" `
    --resource-group "$RESOURCE_GROUP" `
    --name "$VM_NAME" `

vm show `
    --subscription-id "$SUBSCRIPTION_ID" `
    --resource-group "$RESOURCE_GROUP" `
    --name "$VM_NAME"