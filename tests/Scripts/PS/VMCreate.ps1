# $vnet=New-VirtualNetwork -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 -Name yabo-test-vnet-2 -ResourceGroupName yabo-test-group -Location eastus -AddressPrefixes "10.0.0.0/16" -SubnetNames yabo-test-subnet1 -SubnetAddressPrefixes "10.0.2.0/24" 
# $nsg=New-NetworkSecurityGroup -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 -ResourceGroupName yabo-test-group -Name yabo-test-nsg-2 -Location eastus -RuleNames rdp -RuleProtocols tcp -RuleDirections Inbound -RulePriorities 1001 -RuleSourceAddressPrefixes * -RuleSourcePortRanges * -RuleDestinationPortRanges 3389 -RuleDestinationAddressPrefixes * -RuleAccesses Allow
# $nic=New-NetworkInterface -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 -ResourceGroupName yabo-test-group -Name yabo-test-nic-2 -Location eastus -SubnetIds /subscriptions/0c302431-1ad0-4187-87c9-d4cebcf7edc2/resourceGroups/yabo-test-group/providers/Microsoft.Network/virtualNetworks/yabo-test-vnet-2/subnets/yabo-test-subnet1 -NetworkSecurityGroupId /subscriptions/0c302431-1ad0-4187-87c9-d4cebcf7edc2/resourceGroups/yabo-test-group/providers/Microsoft.Network/networkSecurityGroups/yabo-test-nsg-2
# $vm2=New-VM -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 -ResourceGroupName yabo-test-group -Name yabo-test-vm-2 -Location eastus -VmSize Standard_D2s_v3 -AdminUsername Artemis -AdminPassword ArtemisCriticalX4! -NetworkInterfaceId /subscriptions/0c302431-1ad0-4187-87c9-d4cebcf7edc2/resourceGroups/yabo-test-group/providers/Microsoft.Network/networkInterfaces/yabo-test-nic-2

New-VM \
    -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1 `
    -Location eastus `
    -VmSize Standard_D2s_v3 `
    -AdminUsername Artemis `
    -AdminPassword ArtemisCriticalX4! `
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
    -RuleAccesses Allow

Get-VM `
    -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1

Remove-VM `
    -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1 `
    -Force

Get-VM `
    -SubscriptionId 0c302431-1ad0-4187-87c9-d4cebcf7edc2 `
    -ResourceGroupName yabo-test-group-1 `
    -Name yabo-test-vm-1