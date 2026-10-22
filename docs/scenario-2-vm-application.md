# Scenario 2: Deploy an application on a VM and keep it running

**Customer goal:** "I want to deploy a small web application on an Azure VM, verify that it works, manage it from CLI or PowerShell, and clean up when finished."

**Successful outcome:** The application serves a page, survives a VM stop/start cycle, and all resources created for the scenario are removed afterward.

## Prerequisites

- AzCLIPS CLI and PowerShell installed and authenticated; preview modules imported in a fresh PowerShell 7.6+ session.
- OpenSSH tools (`ssh` and `ssh-keygen`) installed.
- Existing NSG in the VM's region permitting SSH/22 and HTTP/80 only from the tester's IP/CIDR; outbound access for Ubuntu packages.

## Shared inputs

Replace the subscription, region, executable path and NSG ID with approved values. The executable path shown is the Windows preview installation location; adjust it if using another installation layout.

Commands use PowerShell syntax for both surfaces. Stop on errors; check `$LASTEXITCODE` after each AzCLIPS CLI command.

```powershell
$ErrorActionPreference = 'Stop'
$az = "$env:LOCALAPPDATA\Programs\azcli\az.exe"
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$nsgId = '<existing-approved-NSG-resource-ID>'

$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-app-$run"
$vm = "app-$run"
$pip = "$vm-pip"
$admin = 'azadmin'
$image = 'Canonical:0001-com-ubuntu-server-jammy:22_04-lts-gen2:latest'
$key = Join-Path $env:TEMP "$vm-key"

$cliScope = @('--subscription-id', $sub, '--resource-group', $rg)
$psVm = @{
    SubscriptionId    = $sub
    ResourceGroupName = $rg
    Name              = $vm
}
```

## Customer journey

The CLI and PowerShell commands are alternatives for the same action, unless deliberately testing a handoff. Do not execute both creation paths against the same resources.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Prepare the application environment** | Create a dedicated resource group and a static public IP. Confirm the approved NSG is ready. The VM creation step will create its VNet, subnet and NIC. | **AzCLIPS CLI:**<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br><br>`& $az network public-ip create @cliScope --name $pip --location $location --sku Standard --allocation-method Static --version IPv4`<br><br>**AzCLIPS PowerShell:**<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br><br>`New-AzPublicIpAddress -SubscriptionId $sub -ResourceGroupName $rg -Name $pip -Location $location -Sku Standard -AllocationMethod Static -IpAddressVersion IPv4`<br><br>**Pending:** standalone NSG creation/rule configuration and explicit subnet/NIC command variants need Engineering-confirmed syntax and build availability. | Resource group and static public IP exist in the intended subscription and region. Approved NSG rules permit the tester's SSH and HTTP access without opening these ports to everyone. |
| **2. Deploy the VM** | Generate a temporary SSH key pair. Create an Ubuntu VM with the application network, public IP and approved NSG. | **Workstation utility, not AzCLIPS:**<br>`ssh-keygen -t ed25519 -f $key -C "azclips-$run"`<br><br>**AzCLIPS CLI:**<br>`& $az vm create @cliScope --name $vm --location $location --size Standard_D2s_v3 --image $image --admin-username $admin --ssh-key-values "$key.pub" --network-interface-name "$vm-nic" --virtual-network-name "$vm-vnet" --address-prefixes 10.0.0.0/16 --subnet-names app --subnet-address-prefixes 10.0.2.0/24 --nsg $nsgId --public-ip-address $pip`<br><br>**AzCLIPS PowerShell:**<br>**Pending:** equivalent SSH-key-based `New-AzVM` invocation. No guessed parameter or password-based substitution.<br><br>**Inspect using either surface:**<br>`& $az vm show @cliScope --name $vm`<br>`Get-AzVM @psVm` | VM provisioning succeeds. Its NIC is associated with the intended subnet, public IP and approved NSG. The VM is configured for SSH-key authentication. |
| **3. Run the application** | Retrieve the VM's public IP. Connect over SSH, install a small web server, and configure it to start automatically. Request its page from the tester's machine. | **AzCLIPS CLI, retrieve address:**<br>`& $az network public-ip show @cliScope --name $pip`<br><br>**AzCLIPS PowerShell, retrieve address:**<br>`Get-AzPublicIpAddress -SubscriptionId $sub -ResourceGroupName $rg -Name $pip`<br><br>Set `$ip` to the returned public-IP address (`properties.ipAddress` in JSON or `Properties.IpAddress` on the PowerShell object), then run the workload commands below. | SSH succeeds after host identity verification. HTTP returns the recognizable page: **"AzCLIPS scenario 2 is running"**. This is the first application-level success checkpoint. |
| **4. Operate and verify it** | Inspect the same VM from either surface. Stop it, start it, and confirm the application returns. Then deallocate/start it and repeat the application check. | **Inspect:**<br>`& $az vm show @cliScope --name $vm`<br>`Get-AzVM @psVm`<br><br>**Power off, choose one:**<br>`& $az vm stop @cliScope --name $vm`<br>`Stop-AzVM @psVm -StayProvisioned -Force`<br><br>**Start, choose one:**<br>`& $az vm start @cliScope --name $vm`<br>`Start-AzVM @psVm`<br><br>**Deallocate, choose one:**<br>`& $az vm deallocate @cliScope --name $vm`<br>`Stop-AzVM @psVm -Force`<br><br>Start again using either start command, then repeat the HTTP verification below.<br><br>**Pending variants:** resize, detailed instance-status inspection, resource-ID addressing and async/no-wait behavior. | Application is unavailable while the VM is stopped/deallocated and returns after each start without reinstalling or manually starting nginx. CLI and PowerShell operate on the same VM. Allow up to five minutes after each start; capture errors if recovery fails. A failed HTTP request while stopped is expected, but does not by itself establish the VM's power state. |
| **5. Remove the environment** | Delete the VM, then delete the dedicated resource group to remove its remaining disks and networking. Remove the temporary local keys. Run cleanup even if an earlier step fails. | **AzCLIPS CLI:**<br>`& $az vm delete @cliScope --name $vm`<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzVM @psVm`<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg`<br><br>**Workstation cleanup:**<br>`Remove-Item -LiteralPath $key, "$key.pub"` | Wait for deletion to complete. CLI group-exists reports `false`; PowerShell lookup reports the group is absent. Distinguish expected not-found from authentication, authorization or network errors. Scenario-created resources are removed. The pre-existing NSG remains untouched, and temporary local keys are gone. |

## Workload commands for step 3

These are SSH/Linux and HTTP commands, not AzCLIPS commands.

### Connect from the tester's machine

```powershell
$ip = '<public-IP-returned-in-step-3>'

ssh -i $key "$admin@$ip"
```

Verify the SSH host fingerprint through a trusted channel before accepting it. Do not disable host-key checking. If a key passphrase was set during generation, enter it when prompted.

### Install and start the application inside the VM

```bash
sudo apt-get update &&
sudo apt-get install -y nginx &&
printf '%s\n' 'AzCLIPS scenario 2 is running' | sudo tee /var/www/html/index.html &&
sudo systemctl enable --now nginx
```

If installation or startup fails, retain the error and investigate before continuing. The page contains no credentials or sensitive data.

Exit the remote shell:

```bash
exit
```

### Verify from the tester's PowerShell session

```powershell
$response = Invoke-WebRequest -Uri "http://$ip" -TimeoutSec 30
if ($response.StatusCode -ne 200 -or
    $response.Content.Trim() -ne 'AzCLIPS scenario 2 is running') {
    throw 'Application verification failed.'
}
```

Repeat this check after each start in step 4. During boot, retry manually within the five-minute recovery window and retain the final result. If the page has not recovered by then, record a scenario failure and capture the relevant errors before cleanup.
