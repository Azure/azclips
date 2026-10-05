$ErrorActionPreference = 'Stop'
Import-Module ./src/PS/Accounts/Accounts.psd1
Import-Module ./src/PS/Resources/Resources.psd1
Import-Module ./src/PS/Compute/Compute.psd1

# subscription resolves from the active profile
$rg = New-AzResourceGroup -Name scenario-vm-rg -Location westus2

$vm = New-AzVM -SubscriptionId '00000000-0000-0000-0000-000000000000' `
    -Name scenario-vm -Location $rg.Location -ResourceGroupName $rg.Name `
    -VmSize Standard_D2s_v3 -AdminUsername azadmin -AdminPassword 'PlaceholderP@ss1' `
    -Image MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest `
    -NetworkInterfaceName scenario-vm-nic -VirtualNetworkName scenario-vm-vnet -NetworkSecurityGroupName scenario-vm-nsg `
    -SubnetNames default -SubnetAddressPrefixes @(,@('10.0.0.0/24')) -AddressPrefixes 10.0.0.0/16
if ($vm.Properties.ProvisioningState -ne 'Succeeded') { exit 2 }

"created $($vm.Name) in $($rg.Name): $($vm.Properties.ProvisioningState)"
