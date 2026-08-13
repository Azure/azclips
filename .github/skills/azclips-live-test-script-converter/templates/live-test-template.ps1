# Azclips PowerShell live test template.
# Generated scripts should be written under tests/Scripts/PS/live-test-scripts/<module-name>/.
# Keep this as a simple command collection like tests/Scripts/PS/VMCreate.ps1.

$SubscriptionId = $env:AZCLIPS_SUBSCRIPTION_ID
$RunId = if ($env:AZCLIPS_LIVE_TEST_RUN_ID) { $env:AZCLIPS_LIVE_TEST_RUN_ID } else { [Guid]::NewGuid().ToString('N').Substring(0, 8) }
$NamePrefix = 'azclips-vm'

$ResourceGroupName = "$NamePrefix-rg-$RunId"
$Location = 'eastus'
$VmName = "$NamePrefix-vm-$RunId"
$NetworkInterfaceName = "$NamePrefix-nic-$RunId"
$VirtualNetworkName = "$NamePrefix-vnet-$RunId"
$SubnetName = "$NamePrefix-subnet-$RunId"
$NetworkSecurityGroupName = "$NamePrefix-nsg-$RunId"

New-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName `
    -Location $Location `
    -VmSize Standard_D2s_v3 `
    -AdminUsername azclipsadmin `
    -AdminPassword $env:AZCLIPS_VM_ADMIN_PASSWORD `
    -NetworkInterfaceName $NetworkInterfaceName `
    -VirtualNetworkName $VirtualNetworkName `
    -AddressPrefixes "10.0.0.0/16" `
    -SubnetNames $SubnetName `
    -SubnetAddressPrefixes "10.0.2.0/24" `
    -NetworkSecurityGroupName $NetworkSecurityGroupName `
    -RuleNames rdp `
    -RuleProtocols tcp `
    -RuleDirections Inbound `
    -RulePriorities 1001 `
    -RuleSourceAddressPrefixes '*' `
    -RuleSourcePortRanges '*' `
    -RuleDestinationPortRanges 3389 `
    -RuleDestinationAddressPrefixes '*' `
    -RuleAccesses Allow

Get-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName

Remove-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName `
    -Force

Get-AzVM `
    -SubscriptionId $SubscriptionId `
    -ResourceGroupName $ResourceGroupName `
    -Name $VmName