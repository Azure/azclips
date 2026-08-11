# Stage 0 — GitHub Actions identity

Creates the Microsoft Entra identity that `.github/workflows/mi-live.yml` uses to
authenticate to Azure, and publishes its IDs to GitHub so nothing needs to be
configured by hand.

```bash
cp terraform.tfvars.example terraform.tfvars
# edit: set `alias`
./setup-github-identity.sh
```

Creates:

- an app registration and service principal
- a federated credential trusting `repo:<owner>/<repo>:environment:dev`, so the
  workflow authenticates with a short-lived OIDC token and no client secret
- a Contributor role assignment at subscription scope

Then sets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and
`ALIAS` as `dev` environment variables on the repository.

**Run this only from a developer machine, never in CI.** It creates the very
identity CI logs in as, and needs Owner on the subscription plus the right to
create Entra applications. Its Terraform state is **local** — `terraform.tfstate`
in this directory, gitignored — rather than in the shared state account used by
`tests/Live/Terraform/linux-vm`, so CI runs never refresh or destroy their own
credentials.

Local state is also what makes this module safe to re-run in a tenant that
deletes resource groups on a schedule. Nothing here lives in a resource group
(Entra objects, plus a subscription-scoped role assignment), so the identity
outlives the sweep — but a remote state blob would not, and re-running against
empty state would create a duplicate app registration rather than a no-op.

Keep `terraform.tfstate`: without it the identity still works, but Terraform can
no longer manage or destroy it.

Skip this module entirely if you only run the live test locally.

Full documentation, including stages 1 and 2, is in
[`../linux-vm/README.md`](../linux-vm/README.md).
