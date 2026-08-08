# Managed Identity live test

This environment runs the CLI managed-identity smoke test on a real Azure VM.
Terraform provisions a resource group, the VM, its system-assigned and
user-assigned identities, networking, RBAC, and storage. The test sends the CLI
bundle to the VM with Azure VM Run Command, so SSH is not required.

Every developer gets their own isolated copy of the infrastructure, keyed by a
short **alias** that you configure once. The alias prefixes the resource group
and every resource in it, and selects your own Terraform state blob, so several
developers can share one subscription without overwriting each other.

## Stages

| Stage | Location | Runs | Purpose |
|---|---|---|---|
| 0 | `terraform/bootstrap/setup-github-identity.sh` | Local only, once | Creates the Entra identity GitHub Actions logs in as, and publishes its IDs to GitHub |
| 1 | `terraform/linux-vm/provision-infrastructure.sh` | Local and CI | Creates the resource group, VM, and managed identities |
| 2 | `scripts/mi-live-test.sh` | Local and CI | Builds the bundle and runs the test on the VM |

Stage 0 is only needed if you intend to run the GitHub workflow. For purely
local runs, start at stage 1.

## Azure environment

| Setting | Value |
|---|---|
| Subscription | `Visual Studio Enterprise Subscription` (your own MSDN subscription) |
| Tenant / subscription ID | Taken from your active `az` context |
| Resource group | `<alias>-azclips-live-test-mi-rg` (created for you) |
| VM | `<alias>-azclips-live-test-mi-vm` |
| CI app registration | `<alias>-azclips-live-test-mi` |
| Terraform state RG | `<alias>-azclips-live-test-mi-tfstate-rg` (created for you; kept separate from the workload group so `terraform destroy` cannot delete the state) |
| Terraform state account | `<alias>tf<first 8 of subscription id>` (created for you) |
| Terraform state blob | `<alias>-linux-vm.tfstate`, `<alias>-bootstrap.tfstate` |
| Region | Australia East |

Nothing is pinned to one person's subscription. The scripts check only that the
**name** of the active subscription matches, then derive the tenant ID,
subscription ID and state account from whatever is selected — so each developer
works in their own subscription. Because that name is the MSDN default, the
check guards against picking the wrong subscription, not against a determined
mistake; confirm the ID it prints is yours.

## Prerequisites

Run from Linux or WSL with:

- The .NET SDK selected by the repository `global.json`
- Azure CLI
- Terraform
- GitHub CLI (`gh`) — only for stage 0
- Bash, `curl`, `tar`, and `sha256sum`
- Access to the tenant and subscription above
- Permission to create a resource group and manage VMs, networking, storage,
  managed identities, and role assignments. `Owner` on the subscription is the
  simplest role, and is required for stage 0.

Confirm the local tools:

```bash
dotnet --version
az version
terraform version
```

## Sign in

```bash
az login
az account set --subscription "Visual Studio Enterprise Subscription"
az account show \
  --query '{tenantId:tenantId, subscriptionId:id, subscription:name}' \
  --output table
```

The displayed tenant and subscription IDs must match the table above.

## Choose an alias

Pick a short identifier for yourself — 3 to 14 characters, lowercase, starting
with a letter, containing only `a-z`, `0-9`, and `-`. Your GitHub or corporate
username is a good choice. Use the **same alias in both stages**.

## Stage 0 — create the GitHub identity (once, local only)

Skip this if you only run the test locally.

```bash
cp terraform/terraform.tfvars.example terraform/terraform.tfvars
# edit: set `alias`

terraform/bootstrap/setup-github-identity.sh
```

This creates an Entra app registration, a service principal, a federated
credential trusting your repository's `dev` environment, and a Contributor role
assignment at subscription scope. No client secret is ever created — GitHub
authenticates by exchanging a short-lived OIDC token.

It then publishes four `dev` environment variables to your repository, so
nothing has to be configured by hand:

| Variable | Source |
|---|---|
| `AZURE_CLIENT_ID` | The new app registration |
| `AZURE_TENANT_ID` | Your tenant |
| `AZURE_SUBSCRIPTION_ID` | Your subscription |
| `ALIAS` | Your alias, so CI resolves the same resources and state as you do |

These are variables rather than secrets because they are identifiers, not
credentials; the authentication itself is the OIDC exchange.

This stage must never run in CI: it creates the very identity CI logs in as, and
needs permissions CI deliberately does not have.

## Stage 1 and 2 — run locally

```bash
# Already done if you ran stage 0 — the same file configures both stages.
cp terraform/terraform.tfvars.example terraform/terraform.tfvars
# edit: set `alias`
```

The copied file is gitignored and shared by both stages, so the alias is set once.
`alias` is the only required setting.

Run from the repository root. The first run should include `--provision` so
Terraform creates or updates the Azure resources:

```bash
scripts/mi-live-test.sh --provision
```

This command:

1. Initializes and validates Terraform against your own state blob.
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
MI_LIVE_RESULT:PASS
==> End-to-end MI test PASSED.
```

`MI_LIVE_RESULT:PASS` is the authoritative result because Azure Run Command can
report a successful invocation even when the guest script fails.

To rerun without changing infrastructure:

```bash
scripts/mi-live-test.sh
```

To reuse an already-built bundle:

```bash
scripts/mi-live-test.sh --skip-publish
```

## Run the GitHub Actions pipeline manually

The workflow is `.github/workflows/mi-live.yml`. It authenticates to Azure with
GitHub OIDC and runs the same script with `--provision`, against the same state
and resources as your local runs.

Requirements:

- Stage 0 has been run for this repository.
- The job declares `environment: dev`. The federated credential trusts the OIDC
  subject `repo:<owner>/<repo>:environment:dev`, so removing or renaming the
  environment makes the Azure login fail.
- The selected branch contains the workflow file and is pushed to `origin`.

Because the credential is scoped to the environment rather than to a branch, any
branch can run the workflow — no per-branch credential is needed.

### From the GitHub website

1. Open your repository's **Actions** tab.
2. Select **Managed Identity Live**.
3. Select **Run workflow**.
4. Choose a branch, normally `main`.
5. Select **Run workflow** and wait for the `mi-live` job to finish.

The run is successful only when the job is green and its log contains
`MI_LIVE_RESULT:PASS`.

### From the GitHub CLI

Confirm that `gh` is authenticated:

```bash
gh auth status
```

Trigger the workflow on `main` (your fork — the repository stage 0 detected
from `origin`):

```bash
OWNER="$(gh api user --jq .login)"
REPO="$(basename -s .git "$(git remote get-url origin)")"

gh workflow run mi-live.yml \
  --repo "$OWNER/$REPO" \
  --ref main
```

Find and monitor the run:

```bash
RUN_ID="$(gh run list \
  --repo "$OWNER/$REPO" \
  --workflow mi-live.yml \
  --branch main \
  --limit 1 \
  --json databaseId \
  --jq '.[0].databaseId')"

gh run watch "$RUN_ID" \
  --repo "$OWNER/$REPO" \
  --exit-status
```

To inspect the final MI results:

```bash
gh run view "$RUN_ID" \
  --repo "$OWNER/$REPO" \
  --log |
  grep -E 'MI_LIVE_RESULT|End-to-end MI test|assigned.*OK'
```

Run the workflow from your fork — the same repository stage 0 detected from `origin`.
A different repository's OIDC token will not match the federated credential.

## Optional SSH access

SSH is disabled by default: the NSG has no inbound port 22 rule, and Terraform
creates a throwaway admin key. The `ssh_command` Terraform output does not mean
that SSH is enabled.

To install your public key on the existing VM and allow only your current
public IP (replace `<alias>` with yours):

```bash
test -f ~/.ssh/id_ed25519.pub || ssh-keygen -t ed25519

az vm user update \
  --resource-group "<alias>-azclips-live-test-mi-rg" \
  --name "<alias>-azclips-live-test-mi-vm" \
  --username azureuser \
  --ssh-key-value ~/.ssh/id_ed25519.pub

MY_IP="$(curl -fsS https://api.ipify.org)"
TF_VAR_allowed_ssh_source_cidr="${MY_IP}/32" \
  terraform/linux-vm/provision-infrastructure.sh

VM_IP="$(az network public-ip show \
  --resource-group "<alias>-azclips-live-test-mi-rg" \
  --name "<alias>-azclips-live-test-mi-vm-pip" \
  --query ipAddress --output tsv)"

ssh azureuser@"$VM_IP"
```

Close port 22 again when finished:

```bash
TF_VAR_allowed_ssh_source_cidr="" terraform/linux-vm/provision-infrastructure.sh
```

Never set `allowed_ssh_source_cidr` to `*`.

## Troubleshooting

### Wrong tenant or subscription

Run the sign-in commands again. The provisioning script prints the expected and
active IDs when they do not match.

### `No 'alias' configured`

Copy `terraform.tfvars.example` to `terraform.tfvars` and set `alias`, or export
`TF_VAR_alias`. The alias names your resources and selects your state.

### `Alias changed: 'x' -> 'y'`

Each alias has its own Terraform state, so switching would leave the previous
resources running and untracked. Destroy the old environment first, or delete
the `.alias` marker file in the module directory if it is already gone.

### The workflow fails at Azure login

Usually the OIDC subject does not match. Check that the job still declares
`environment: dev`, and that the workflow is running from the repository set as
the repository stage 0 detected from `origin`. Re-run stage 0 to see the expected subject.

### Terraform state is locked

Another run may still be active — most likely a GitHub Actions run using the
same alias. Wait for it to finish rather than forcing an unlock.

### VM SKU is unavailable

Azure capacity changes over time. Check unrestricted sizes and set a temporary
override:

```bash
az vm list-skus \
  --location australiaeast \
  --resource-type virtualMachines \
  --all \
  --query "[?length(restrictions)==\`0\`].name" \
  --output table

TF_VAR_vm_size=Standard_D2s_v4 scripts/mi-live-test.sh --provision
```

### Managed identity receives an authorization error

RBAC can take several minutes to propagate. The VM-side test retries ARM calls
for up to two minutes. If propagation takes longer, rerun the test.

## Resource lifecycle

Your resource group is yours alone and is disposable — there is no deletion
lock. Tear it down when you are finished, so the VM stops costing money:

```bash
cd terraform/linux-vm
terraform destroy
```

The stage 0 Entra identity is separate and long-lived. Destroy it only if you
are done with the GitHub workflow entirely:

```bash
cd terraform/bootstrap
terraform destroy
```
