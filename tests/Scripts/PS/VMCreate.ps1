    -SubscriptionId f758ac53-3e63-4317-a956-0997793808d7 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1 `
    -Location eastus `
    -VmSize Standard_D2s_v3 `
    -AdminUsername Artemis `
    -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
    -NetworkInterfaceName yabo-test-nic-1 `
    -VirtualNetworkName yabo-test-vnet-1 `
    -AddressPrefixes "10.0.0.0/16" `
    -SubnetNames yabo-test-subnet-1 `
    -SubnetAddressPrefixes "10.0.2.0/24" `
    -NetworkSecurityGroupName yabo-test-nsg-1 `
    -RuleNames rdp `
    -RuleProtocols tcp `
    -RuleDirections Inbound `
    -RulePriorities 1001 `
    -RuleSourceAddressPrefixes * `
    -RuleSourcePortRanges * `
    -RuleDestinationPortRanges 3389 `
    -RuleDestinationAddressPrefixes * `
    -RuleAccesses Allow `
    -debug

Get-AzVM `
    -SubscriptionId f758ac53-3e63-4317-a956-0997793808d7 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1

Remove-AzVM `
    -SubscriptionId f758ac53-3e63-4317-a956-0997793808d7 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1 `
    -Force

Get-AzVM `
    -SubscriptionId f758ac53-3e63-4317-a956-0997793808d7 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1