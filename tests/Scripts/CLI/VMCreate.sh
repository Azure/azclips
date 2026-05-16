vm create `
    --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    --resource-group yabo-test-group-2 `
    --name yabo-test-vm-2 `
    --location eastus `
    --vm-size Standard_D2s_v3 `
    --admin-username Artemis `
    --admin-password 'ArtemisCriticalX4!' `
    --network-interface-name yabo-test-nic-2 `
    --virtual-network-name yabo-test-vnet-2 `
    --address-prefixes 10.0.0.0/16 `
    --subnet-names yabo-test-subnet-2 `
    --subnet-address-prefixes 10.0.2.0/24 `
    --network-security-group-name yabo-test-nsg-2 `
    --rule-names rdp `
    --rule-protocols tcp `
    --rule-directions Inbound `
    --rule-priorities 1001 `
    --rule-source-address-prefixes "*" `
    --rule-source-port-ranges "*" `
    --rule-destination-port-ranges 3389 `
    --rule-destination-address-prefixes "*" `
    --rule-accesses Allow

vm show `
    --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    --resource-group yabo-test-group-2 `
    --name yabo-test-vm-2

vm delete `
    --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    --resource-group yabo-test-group-2 `
    --name yabo-test-vm-2 `

vm show `
    --subscription-id 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    --resource-group yabo-test-group-2 `
    --name yabo-test-vm-2