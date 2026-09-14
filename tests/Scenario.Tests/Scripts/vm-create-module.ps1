$ErrorActionPreference = 'Stop'
# The build stages every module as a self-contained folder under artifacts/<Configuration>/PS;
# the runner points at the configuration it was built with, Debug is the dev default.
$modules = if ($env:AZCLIPS_PS_MODULES) { $env:AZCLIPS_PS_MODULES } else { './artifacts/Debug/PS' }
Import-Module (Join-Path $modules 'Accounts/Accounts.psd1')
Import-Module (Join-Path $modules 'Resources/Resources.psd1')
Import-Module (Join-Path $modules 'Compute/Compute.psd1')

# subscription resolves from the active profile
$rg = New-AzResourceGroup -Name scenario-vm-rg -Location westus2

$vm = New-AzVM -SubscriptionId '00000000-0000-0000-0000-000000000000' `
    -Name scenario-vm -Location $rg.Location -ResourceGroupName $rg.Name `
    -VmSize Standard_D2s_v3 -AdminUsername azadmin -AdminPassword 'PlaceholderP@ss1' `
    -NetworkInterfaceName scenario-vm-nic -VirtualNetworkName scenario-vm-vnet -NetworkSecurityGroupName scenario-vm-nsg `
    -SubnetNames default -SubnetAddressPrefixes @(,@('10.0.0.0/24')) -AddressPrefixes 10.0.0.0/16
if ($vm.Properties.ProvisioningState -ne 'Succeeded') { exit 2 }

"created $($vm.Name) in $($rg.Name): $($vm.Properties.ProvisioningState)"
