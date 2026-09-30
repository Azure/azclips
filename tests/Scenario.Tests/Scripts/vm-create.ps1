$ErrorActionPreference = 'Stop'
$az = $env:AZCLIPS_TEST_AZ
$sub = '00000000-0000-0000-0000-000000000000'

$rg = & $az group create --subscription-id $sub --name scenario-vm-rg --location westus2 --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { exit 1 }

$vm = & $az vm create --subscription-id $sub --resource-group $rg.name --name scenario-vm --location $rg.location `
    --size Standard_D2s_v3 --admin-username azadmin --admin-password 'PlaceholderP@ss1' `
    --network-interface-name scenario-vm-nic --virtual-network-name scenario-vm-vnet `
    --subnet-names default --subnet-address-prefixes 10.0.0.0/24 --address-prefixes 10.0.0.0/16 --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { exit 1 }
if ($vm.properties.provisioningState -ne 'Succeeded') { exit 2 }

"created $($vm.name) in $($rg.name): $($vm.properties.provisioningState)"
