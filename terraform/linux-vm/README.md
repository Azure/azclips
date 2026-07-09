# Managed Identity live E2E test

This environment runs the CLI managed-identity smoke test on a real Azure VM.
Terraform provisions the VM, its system-assigned and user-assigned identities,
networking, RBAC, storage, and a resource-group deletion lock. The test sends the
CLI bundle to the VM with Azure VM Run Command, so SSH is not required.

## Azure environment

| Setting | Value |
|---|---|
| Tenant | Azure Client Tools 2026-07-20 |
| Tenant ID | `4f00b3b6-2940-4f2c-b037-94637c180d30` |
| Subscription | Azure CLI/PWSH |
| Subscription ID | `f758ac53-3e63-4317-a956-0997793808d7` |
| Resource group | `ManagedIdentityLiveTest` |
| Region | East US |

The scripts reject any other active tenant or subscription.

## Prerequisites

Run from Linux or WSL with:

- The .NET SDK selected by the repository `global.json`
- Azure CLI
- Terraform
- Bash, `curl`, `tar`, and `sha256sum`
- Access to the tenant and subscription above
- Sufficient resource-group permissions to manage VMs, networking, storage,
  managed identities, locks, and role assignments. `Owner` on
  `ManagedIdentityLiveTest` is the simplest role for initial setup.

Confirm the local tools:

```bash
dotnet --version
az version
terraform version
```

## Sign in

```bash
az login --tenant 4f00b3b6-2940-4f2c-b037-94637c180d30
az account set --subscription f758ac53-3e63-4317-a956-0997793808d7
az account show \
  --query '{tenantId:tenantId, subscriptionId:id, subscription:name}' \
  --output table
```

The displayed tenant and subscription IDs must match the table above.

## Configure Terraform

From the repository root:

```bash
cp terraform/linux-vm/terraform.tfvars.example \
  terraform/linux-vm/terraform.tfvars
```

The example already targets `ManagedIdentityLiveTest`. The copied file is
gitignored and normally needs no changes.

The provisioning script creates or reuses the remote state account and
container in the resource group. Do not run this environment concurrently with
another developer; Terraform protects the shared state with a lock, but both
runs target the same VM and bundle storage.

## Run the full test

```bash
scripts/mi-e2e-test.sh --provision
```

This command:

1. Initializes and validates Terraform.
2. Plans and applies any required infrastructure changes.
3. Builds a self-contained Linux CLI bundle.
4. Uploads the bundle to a short-lived private blob.
5. Runs the bundle on the VM using `az vm run-command`.
6. Tests system-assigned identity login and user-assigned identity login by
   client ID, resource ID, and object ID.

A successful run ends with:

```text
[system-assigned] OK
[user-assigned/client-id] OK
[user-assigned/resource-id] OK
[user-assigned/object-id] OK
MI_E2E_RESULT:PASS
==> End-to-end MI test PASSED.
```

`MI_E2E_RESULT:PASS` is the authoritative result because Azure Run Command can
report a successful invocation even when the guest script fails.

To rerun without changing infrastructure:

```bash
scripts/mi-e2e-test.sh
```

To reuse an already-built bundle:

```bash
scripts/mi-e2e-test.sh --skip-publish
```

## Optional SSH access

SSH is disabled by default: the NSG has no inbound port 22 rule, and Terraform
creates a throwaway admin key. The `ssh_command` Terraform output does not mean
that SSH is enabled.

To install your public key on the existing VM and allow only your current
public IP:

```bash
test -f ~/.ssh/id_ed25519.pub || ssh-keygen -t ed25519

az vm user update \
  --resource-group ManagedIdentityLiveTest \
  --name cirrus-mi-e2e-vm \
  --username azureuser \
  --ssh-key-value ~/.ssh/id_ed25519.pub

MY_IP="$(curl -fsS https://api.ipify.org)"
TF_VAR_allowed_ssh_source_cidr="${MY_IP}/32" \
  terraform/linux-vm/change.sh

VM_IP="$(az network public-ip show \
  --resource-group ManagedIdentityLiveTest \
  --name cirrus-mi-e2e-vm-pip \
  --query ipAddress --output tsv)"

ssh azureuser@"$VM_IP"
```

Close port 22 again when finished:

```bash
TF_VAR_allowed_ssh_source_cidr="" terraform/linux-vm/change.sh
```

Never set `allowed_ssh_source_cidr` to `*`.

## Troubleshooting

### Wrong tenant or subscription

Run the sign-in commands again. `terraform/linux-vm/change.sh` prints the
expected and active IDs when they do not match.

### Terraform state is locked

Another run may still be active. Wait for it to finish rather than forcing an
unlock. Force-unlocking shared state can corrupt concurrent deployments.

### VM SKU is unavailable

Azure capacity changes over time. Check unrestricted sizes in East US and set a
temporary override:

```bash
az vm list-skus \
  --location eastus \
  --resource-type virtualMachines \
  --all \
  --query "[?length(restrictions)==\`0\`].name" \
  --output table

TF_VAR_vm_size=Standard_D2s_v4 scripts/mi-e2e-test.sh --provision
```

### Managed identity receives an authorization error

RBAC can take several minutes to propagate. The VM-side test retries ARM calls
for up to two minutes. If propagation takes longer, rerun the test.

## Resource lifecycle

The environment is shared and protected by a `CanNotDelete` resource-group
lock. Do not run `terraform destroy` or remove the lock unless the environment
owner has approved deleting the live-test resources.
