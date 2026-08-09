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
create Entra applications. Its Terraform state is deliberately separate from
`tests/Live/Terraform/linux-vm` so CI runs never refresh or destroy their own credentials.

Skip this module entirely if you only run the live test locally.

Full documentation, including stages 1 and 2, is in
[`../linux-vm/README.md`](../linux-vm/README.md).
