# Scenario 2: Deploy an application on a VM and keep it running

**User goal:** "I want to deploy a small application on an Azure VM, verify that it works, manage it from CLI or PowerShell, and clean up when finished."

**Successful outcome:** The application serves a page, survives a VM stop/start cycle, and all resources created for the scenario are removed afterward.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/pp-scenarios/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- OpenSSH tools (`ssh` and `ssh-keygen`) and, for the Bash path, `curl`.
- An existing network security group (NSG) in the VM's region that allows SSH (port 22) and HTTP (port 80) only from your IP address, and outbound access for Ubuntu packages.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps. Use one path for all creation steps rather than creating the same resources twice.

- **1. Prepare the environment:** Create a dedicated resource group and a static public IP address.
- **2. Deploy the VM:** Create an Ubuntu VM with the public IP and your NSG. The Bash and PowerShell CLI paths generate a temporary SSH key pair for sign-in. The AzCLIPS PowerShell path signs in with an admin password that you choose.
- **3. Run the application:** Connect over SSH, install a small test application and request its page from your machine.
- **4. Operate the VM:** Stop it, start it and confirm the application returns. Optionally deallocate it, start it again and repeat the check.
- **5. Clean up:** Delete the resource group and remove any temporary local keys.

Verify the SSH host fingerprint when prompted. Do not disable host-key checking.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
nsg_id="<existing-NSG-resource-ID>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-app-$run"
vm="app-$run"
pip="$vm-pip"
admin="azadmin"
image="Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest"
key="/tmp/$vm-key"
```

**1. Prepare the environment**

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
az network public-ip create --subscription-id "$sub" --resource-group "$rg" --name "$pip" --location "$location" --sku Standard --allocation-method Static --version IPv4
```

**2. Deploy the VM**

```bash
ssh-keygen -t ed25519 -f "$key" -C "azclips-$run"
az vm create --subscription-id "$sub" --resource-group "$rg" --name "$vm" --location "$location" --size Standard_D2s_v5 --image "$image" --admin-username "$admin" --ssh-key-values "$key.pub" --network-interface-name "$vm-nic" --virtual-network-name "$vm-vnet" --address-prefixes 10.0.0.0/16 --subnet-names app --subnet-address-prefixes 10.0.2.0/24 --nsg "$nsg_id" --public-ip-address "$pip"
az vm show --subscription-id "$sub" --resource-group "$rg" --name "$vm"
```

**3. Run the application**

```bash
ip=$(az network public-ip show --subscription-id "$sub" --resource-group "$rg" --name "$pip" --query properties.ipAddress --output tsv)
ssh -i "$key" "$admin@$ip" "sudo apt-get update && sudo apt-get install -y nginx && printf '%s\n' 'AzCLIPS scenario 2 is running' | sudo tee /var/www/html/index.html && sudo systemctl enable --now nginx"
test "$(curl -fsS --max-time 30 "http://$ip")" = "AzCLIPS scenario 2 is running"
```

**4. Operate the VM**

```bash
az vm stop --subscription-id "$sub" --resource-group "$rg" --name "$vm"
az vm start --subscription-id "$sub" --resource-group "$rg" --name "$vm"
test "$(curl -fsS --max-time 30 "http://$ip")" = "AzCLIPS scenario 2 is running"
```

Allow up to five minutes after each start for the page to return. The application is unavailable while the VM is stopped.

Optional: deallocate and start the VM, then repeat the check.

```bash
az vm deallocate --subscription-id "$sub" --resource-group "$rg" --name "$vm"
az vm start --subscription-id "$sub" --resource-group "$rg" --name "$vm"
test "$(curl -fsS --max-time 30 "http://$ip")" = "AzCLIPS scenario 2 is running"
```

**5. Clean up**

```bash
az vm delete --subscription-id "$sub" --resource-group "$rg" --name "$vm"
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
rm -f "$key" "$key.pub"
```

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$nsgId = '<existing-NSG-resource-ID>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-app-$run"
$vm = "app-$run"
$pip = "$vm-pip"
$admin = 'azadmin'
$image = 'Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest'
$key = Join-Path ([IO.Path]::GetTempPath()) "$vm-key"
$scope = @('--subscription-id', $sub, '--resource-group', $rg)
$page = 'AzCLIPS scenario 2 is running'
```

**1. Prepare the environment**

```powershell
az group create --subscription-id $sub --name $rg --location $location
az network public-ip create @scope --name $pip --location $location --sku Standard --allocation-method Static --version IPv4
```

**2. Deploy the VM**

```powershell
ssh-keygen -t ed25519 -f $key -C "azclips-$run"
az vm create @scope --name $vm --location $location --size Standard_D2s_v5 --image $image --admin-username $admin --ssh-key-values "$key.pub" --network-interface-name "$vm-nic" --virtual-network-name "$vm-vnet" --address-prefixes 10.0.0.0/16 --subnet-names app --subnet-address-prefixes 10.0.2.0/24 --nsg $nsgId --public-ip-address $pip
az vm show @scope --name $vm
```

**3. Run the application**

```powershell
$ip = az network public-ip show @scope --name $pip --query properties.ipAddress --output tsv
ssh -i $key "$admin@$ip" "sudo apt-get update && sudo apt-get install -y nginx && printf '%s\n' '$page' | sudo tee /var/www/html/index.html && sudo systemctl enable --now nginx"
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

**4. Operate the VM**

```powershell
az vm stop @scope --name $vm
az vm start @scope --name $vm
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

Allow up to five minutes after each start for the page to return. The application is unavailable while the VM is stopped.

Optional: deallocate and start the VM, then repeat the check.

```powershell
az vm deallocate @scope --name $vm
az vm start @scope --name $vm
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

**5. Clean up**

```powershell
az vm delete @scope --name $vm
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
Remove-Item -LiteralPath $key, "$key.pub"
```

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-app-$run"
$vm = "app-$run"
$pip = "$vm-pip"
$admin = 'azadmin'
$image = 'Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest'
$nsgId = '<existing-NSG-resource-ID>'
$psVm = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $vm }
$page = 'AzCLIPS scenario 2 is running'
```

**1. Prepare the environment**

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
New-AzPublicIpAddress -SubscriptionId $sub -ResourceGroupName $rg -Name $pip -Location $location -Sku Standard -AllocationMethod Static -IpAddressVersion IPv4
```

**2. Deploy the VM**

`New-AzVM` signs in to the VM with an admin password. Choose a throwaway password of at least 12 characters with upper-case, lower-case and a digit. You enter it again when SSH asks for it in step 3.

```powershell
$secure = Read-Host 'Choose an admin password' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $secure).Password
New-AzVM @psVm -Location $location -VmSize 'Standard_D2s_v5' -Image $image -AdminUsername $admin -AdminPassword $password -NetworkInterfaceName "$vm-nic" -VirtualNetworkName "$vm-vnet" -AddressPrefixes '10.0.0.0/16' -SubnetNames 'app' -SubnetAddressPrefixes '10.0.2.0/24' -SecurityGroupName $nsgId -PublicIpAddressName $pip
Get-AzVM @psVm
```

**3. Run the application**

```powershell
$ip = (Get-AzPublicIpAddress -SubscriptionId $sub -ResourceGroupName $rg -Name $pip).Properties.IpAddress
ssh "$admin@$ip" "sudo apt-get update && sudo apt-get install -y nginx && printf '%s\n' '$page' | sudo tee /var/www/html/index.html && sudo systemctl enable --now nginx"
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

**4. Operate the VM**

```powershell
Stop-AzVM @psVm -StayProvisioned -Force
Start-AzVM @psVm
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

Allow up to five minutes after each start for the page to return. The application is unavailable while the VM is stopped.

Optional: deallocate and start the VM, then repeat the check.

```powershell
Stop-AzVM @psVm -Force
Start-AzVM @psVm
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.Content.Trim() -ne $page) { throw 'Application verification failed.' }
```

**5. Clean up**

```powershell
Remove-AzVM @psVm
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
Remove-Variable password, secure -ErrorAction SilentlyContinue
```
