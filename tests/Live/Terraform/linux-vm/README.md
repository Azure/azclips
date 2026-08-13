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
| 0 | `dotnet run --project tests/Live/Tool -- --bootstrap` | Local only, once per tenant | Creates the Entra identity GitHub Actions logs in as, and publishes its IDs to GitHub |
| 1 | `tests/Live/Terraform/linux-vm/provision-infrastructure.sh` | Local and CI | Creates the resource group, VM, and managed identities |
| 2 | `dotnet run --project tests/Live/Tool` | Local and CI | Builds the bundle and runs the test on the VM |

Stage 0 is only needed if you intend to run the GitHub workflow. For purely
local runs, start at stage 1.

## Azure environment

| Setting | Value |
|---|---|
| Subscription | `Azure CLI/PWSH` |
| Tenant / subscription ID | Taken from your active `az` context |
| Resource group | `<alias>-azclips-live-test-mi-rg` (created for you) |
| VM | `<alias>-azclips-live-test-mi-vm` |
| CI app registration | `<alias>-azclips-live-test-mi` |
| Terraform state RG | `<alias>-azclips-live-test-mi-tfstate-rg` (created for you; kept separate from the workload group so `terraform destroy` cannot delete the state out from under itself — `--destroy` deletes it afterwards, as a separate step) |
| Terraform state account | `<alias>tf<first 8 of subscription id>` (created for you; also holds the CLI bundle blobs, in a separate `milive-bundles` container) |
| Terraform state blob | `<alias>-linux-vm.tfstate` (stage 0 has no state — see below) |
| Region | Australia East |

The bundle blobs deliberately live in the state account rather than the workload
resource group. That keeps the workload group holding *only* Terraform-managed
resources, so `--destroy` can delete it — the azurerm provider refuses to delete
a resource group that still contains anything Terraform did not create.

Nothing is pinned to one person's subscription. The scripts check only that the
**name** of the active subscription matches, then derive the tenant ID,
subscription ID and state account from whatever is selected — so each developer
works in their own subscription.

Matching on name rather than ID is deliberate. These tenants are **rotated
periodically**: the old one is disabled and replaced by a new tenant containing a
subscription with the same name but a new ID. Because everything downstream keys
off the *resolved* ID, signing in to the new tenant is the only step required —
the state account name changes with the subscription ID, the new backend is
created on demand, and `terraform init -reconfigure` discards the stale cached
config. Pinning an ID would need a code change every rotation.

Two consequences worth knowing:

- During the overlap window both subscriptions are visible, so
  `az account set --subscription "Azure CLI/PWSH"` fails with *"has more than one
  match"*. The scripts detect this and print the exact per-ID commands to use.
- The new tenant starts empty. Your resources are not migrated; re-run
  `--provision` to recreate them, and re-run stage 0 to recreate the CI identity.

Because names are not unique across tenants, the check guards against picking the
wrong subscription, not against a determined mistake; confirm the ID it prints is
yours.

## Prerequisites

Run from Linux or WSL with:

- The .NET SDK selected by the repository `global.json`
- Azure CLI
- Terraform **1.9.x** — see below
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

### Terraform version

`required_version` in `tests/Live/Terraform/*/versions.tf` pins the **1.9 series**, and CI
pins the same version in `.github/workflows/mi-live.yml`. Anything outside it is
rejected at `terraform init`:

```
Error: Unsupported Terraform Core version
```

That is deliberate. Terraform stamps the version that last wrote a state file
into the state, and then refuses to read state written by anything newer. CI and
every developer on the same alias share one state blob, so a single run on a
newer binary would lock out everyone still on the old one — and there is no way
back short of everybody upgrading. Failing at `init` is the recoverable outcome.

Upgrading is therefore a coordinated change, not an individual one: bump
`required_version` in both modules and `terraform_version` in the workflow in the
same commit, and have everyone sharing the alias upgrade together.

## Sign in

```bash
az login
az account set --subscription "Azure CLI/PWSH"
az account show \
  --query '{tenantId:tenantId, subscriptionId:id, subscription:name}' \
  --output table
```

If `az account set` reports *"has more than one match"*, a tenant rotation has
left the old and new subscriptions both visible. List them and select the newer
tenant by ID:

```bash
az account list --all --query "[?name=='Azure CLI/PWSH' && state=='Enabled'].[id,tenantId]" -o tsv
az account set --subscription <id-in-the-new-tenant>
```

After a rotation you may need `az login --tenant <new-tenant-id>` first, so the
new tenant appears at all.

The displayed subscription name must match the table above; the tenant and
subscription IDs are whatever your current tenant uses.

## Choose an alias

Pick a short identifier for yourself — 3 to 14 characters, lowercase, starting
with a letter, containing only `a-z`, `0-9`, and `-`. Your GitHub or corporate
username is a good choice. Use the **same alias in both stages**.

## Stage 0 — create the GitHub identity (local only)

Skip this entirely if you only run the test locally. It exists solely to give
GitHub Actions something to sign in as.

```bash
cp tests/Live/Terraform/terraform.tfvars.example tests/Live/Terraform/terraform.tfvars
# edit: set `alias`

dotnet run --project tests/Live/Tool -- --bootstrap
```

This creates an Entra app registration, a service principal, a federated
credential trusting your repository's `dev` environment, and two role
assignments at subscription scope: `Contributor` to create the resources, plus
`Role Based Access Control Administrator` because the module grants the VM's two
identities a role, and Contributor cannot write role assignments. Two narrow
roles rather than Owner — RBAC Administrator adds only `roleAssignments`
write/delete, and the roles being granted are non-privileged (`Reader`). No
client secret is ever created — GitHub authenticates by exchanging a short-lived
OIDC token.

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
needs permissions CI deliberately does not have. `--bootstrap` refuses to run
when it detects GitHub Actions.

### Why this one is not Terraform

Every other stage is. This one reads what exists and creates what is missing,
holding no state at all, for two reasons.

Nothing it creates lives in a resource group — directory objects and a
subscription-scoped role assignment — so there is nowhere natural to keep remote
state, and the local state file the Terraform version used was the one thing that
could not survive a tenant rotation: it described objects in a tenant that no
longer answered, and refreshing them failed with a 401.

The second reason is ownership. `az ad app create` records the caller as an
**owner** of the application, and Entra lets an owner manage it afterwards. The
`azuread` provider creates the application without one. In a tenant where you are
a guest — which is normal for these rotating test tenants — that difference is
the whole game: you can create the app and then not create its service principal,
not add a federated credential, and not delete it. The service-principal failure
even arrives disguised as `the backing application of the service principal being
created must in the local tenant`, which sends you off inspecting
`signInAudience` instead.

Being stateless also makes re-running free, which is what makes rotation a
non-event.

### After a tenant rotation

Entra objects are tenant-scoped, so unlike stage 1 nothing here survives a
rotation. Sign in to the new tenant and run it again:

```bash
az login
dotnet run --project tests/Live/Tool -- --bootstrap
```

It creates a fresh identity and repoints the `dev` variables at it. Re-running
when nothing has changed is a safe no-op — it reports what already exists and
mutates nothing.

Do this **before** dispatching the workflow. Until it runs, CI still holds the
previous tenant's client and tenant ids, and the OIDC exchange fails.

### If a same-named application already exists

The application is chosen by **ownership**, not by name — Entra display names are
not unique, and ownership is what decides whether you can manage it at all. So
bootstrap prefers one you own, tries to take ownership of one you do not, and
only creates a separate one if that is refused. An administrator adopts and never
duplicates.

If it reports creating a separate one, the existing application is residue from
something that did not record an owner (the retired Terraform bootstrap did this)
and only a tenant administrator can delete it. Nothing is blocked in the meantime:
CI is pointed at a client id, not a name.

Leaving that residue in place is harmless, and not worth chasing an administrator
for. Residue of this kind never got a service principal — failing to create one is
what stranded it — and an application without a service principal is a blueprint
with no identity attached: nothing can sign in as it, and it can hold no role
assignment. With no credentials, no federated credential and no API permissions
either, there is nothing to expire, rotate or misuse. The cost is a confusing
duplicate name, not exposure. It disappears with the next tenant rotation.

Confusingly, that residue was still *created* by you. Entra tracks creation and
ownership separately, and only ownership grants any rights over the object:

```bash
OBJ=$(az ad app list --display-name "$ALIAS-azclips-live-test-mi" --query "[0].id" -o tsv | tr -d '\r')
az rest --method GET --url "https://graph.microsoft.com/beta/applications/$OBJ/createdOnBehalfOf" --query displayName -o tsv
az rest --method GET --url "https://graph.microsoft.com/v1.0/applications/$OBJ/owners" --query "value[].userPrincipalName" -o tsv
```

An application created by `az ad app create` lists you under both. One created by
the azuread provider lists you as the creator and has no owners at all, because
the provider treats owners as a declarative attribute that nothing asked for. So
you can be looking at an application you made and still be unable to delete it.
Ask an administrator to add you as an owner — that is quicker than asking them to
delete it — or simply wait for the next tenant rotation to take it.

## Stage 1 and 2 — run locally

```bash
# Already done if you ran stage 0 — the same file configures both stages.
cp tests/Live/Terraform/terraform.tfvars.example tests/Live/Terraform/terraform.tfvars
# edit: set `alias`
```

The copied file is gitignored and shared by both stages, so the alias is set once.
`alias` is the only required setting.

Run from the repository root. The first run should include `--provision` so
Terraform creates or updates the Azure resources:

```bash
dotnet run --project tests/Live/Tool -- --provision
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
dotnet run --project tests/Live/Tool
```

To reuse an already-built bundle:

```bash
dotnet run --project tests/Live/Tool -- --skip-publish
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
  tests/Live/Terraform/linux-vm/provision-infrastructure.sh

VM_IP="$(az network public-ip show \
  --resource-group "<alias>-azclips-live-test-mi-rg" \
  --name "<alias>-azclips-live-test-mi-vm-pip" \
  --query ipAddress --output tsv)"

ssh azureuser@"$VM_IP"
```

Close port 22 again when finished:

```bash
TF_VAR_allowed_ssh_source_cidr="" tests/Live/Terraform/linux-vm/provision-infrastructure.sh
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
resources running and untracked. Destroy the old environment first (`--destroy`
clears the marker for you, since nothing is left to orphan), or delete the
`.alias` marker file in the module directory if it is already gone.

### The workflow fails at Azure login

Usually the OIDC subject does not match. Check that the job still declares
`environment: dev`, and that the workflow is running from the repository set as
the repository stage 0 detected from `origin`. Re-run `--bootstrap` to see the expected subject.

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

TF_VAR_vm_size=Standard_D2s_v4 dotnet run --project tests/Live/Tool -- --provision
```

### Managed identity receives an authorization error

RBAC can take several minutes to propagate. The VM-side test retries ARM calls
for up to two minutes. If propagation takes longer, rerun the test.

## Resource lifecycle

Your resource group is yours alone and is disposable — there is no deletion
lock. Tear it down when you are finished, so the VM stops costing money:

```bash
dotnet run --project tests/Live/Tool -- --destroy
```

This shows the destroy plan, asks you to confirm, and then removes the VM, both
managed identities, their role assignments, networking, and the resource group.
It then deletes the Terraform state group — including the state blob and the
bundle container — so a destroy leaves nothing behind except the stage 0
bootstrap identity. Pass `--yes` to skip the prompt in a script. Re-create
everything later with `dotnet run --project tests/Live/Tool -- --provision`,
which re-bootstraps the state backend.

Use this rather than deleting the resource group in the portal. Deleting it by
hand removes the resources in Azure but leaves the Terraform state believing
they exist, so the next `--provision` fails on resources it cannot find.

`--yes` skips the prompt for unattended use. What it destroys is bounded by the
Terraform state plus the one group named after your alias
(`<alias>-azclips-live-test-mi-tfstate-rg`) — but it does remove the one step
where a human reads the target resource group. In CI there is no `.alias` marker
file, so the alias-drift guard cannot fire either, and the `ALIAS` variable alone
decides whose environment is destroyed. Check it before automating this.

If you expect to run the test again within a day or two, deallocating is cheaper
than a full rebuild — it stops all compute billing and leaves only the disk:

```bash
az vm deallocate -g <alias>-azclips-live-test-mi-rg -n <alias>-azclips-live-test-mi-vm
az vm start      -g <alias>-azclips-live-test-mi-rg -n <alias>-azclips-live-test-mi-vm
```

Start the VM again before the next run: the test drives it with
`az vm run-command`, which fails against a deallocated VM.

The stage 0 Entra identity is separate and long-lived. Nothing above touches it:
it is an Entra object, and its role assignment is scoped to the subscription,
above every resource group. Remove it only if you are done with the GitHub
workflow entirely:

```bash
# `az -o tsv` ends lines with CRLF, and the stray CR makes every id below fail
# to resolve — strip it.
SUB=$(az account show --query id -o tsv | tr -d '\r')
APP=$(az ad app list --display-name "$ALIAS-azclips-live-test-mi" --query "[0].appId" -o tsv | tr -d '\r')
SP=$(az ad sp list --filter "appId eq '$APP'" --query "[0].id" -o tsv | tr -d '\r')

# Order matters. The role assignment is an ARM object, not a directory one, so
# it does NOT cascade: deleting the application first leaves an assignment
# pointing at a principal that no longer exists, which is invisible to
# `--assignee` afterwards and has to be hunted down by principal id.
az role assignment list --assignee "$SP" --scope "/subscriptions/$SUB" --query "[].id" -o tsv \
  | tr -d '\r' | xargs -r az role assignment delete --ids
az ad app delete --id "$APP"
```

Deleting the application also deletes its service principal — that one does
cascade. Both list you as an owner, because Graph records the caller as an owner
of each, so neither needs an administrator.

If an assignment was orphaned by deleting in the wrong order, find and remove it
by principal id:

```bash
az role assignment list --all --query "[?principalId=='$SP'].id" -o tsv | tr -d '\r' | xargs -r az role assignment delete --ids
```

If the principal id is no longer to hand, orphans are the assignments that have
lost their principal name — worth checking after any manual cleanup, because a
stranded assignment at subscription scope is invisible in the portal beyond an
"Identity not found" row:

```bash
az role assignment list --all --query "[?principalName==null].{role:roleDefinitionName,principalId:principalId,id:id}" -o table
```
