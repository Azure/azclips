# Verify AzCLIPS OIDC (federated credential) login works in GitHub Actions

**Owning Squad:** Identity

Tests both surfaces in one workflow run: CLI on `ubuntu-latest`, PowerShell on
`windows-latest`. All the one-time Azure/GitHub setup is scripted - you only need to
set your `ALIAS`.

## Prerequisite

- A fork of this repo, checked out locally, on any branch that's pushed to your fork
  (e.g. `main`).
- Azure CLI and GitHub CLI installed and signed in:
  - `az login --tenant <yourBAMITenant>` if you have not already login
  - `gh auth login` & `gh auth status` if you have not already login

## One-time setup

1. Set your alias in `bug-bash-identity-alias.sh`
    ```bash
   ALIAS="yourname"
   ```
2. Bootstrap Azure (resource group, managed identity, federated credential) and the
   matching GitHub environment, then trigger and watch the workflow run:
   ```bash
   ./tests/Live.Tests/Identity/bug-bash-identity-bootstrap-and-run.sh
   ```
   This tears down any leftovers from a previous run, bootstraps fresh resources,
   triggers `bug-bash-identity.yml` on your current branch, and watches it to
   completion.

## Pass Criteria

- Both matrix jobs succeed: `oidc-login-test (ubuntu-latest)` and
  `oidc-login-test (windows-latest)`.
- The login steps ("azclips login (OIDC federated token) - Linux" /
  "azps login (OIDC federated token) - Windows") finish without `AADSTS*` errors.
- The assertion steps ("Run az group list and assert - Linux" /
  "Run Get-AzResourceGroup and assert - Windows") each find the bootstrap-created
  resource group.

## Cleanup

```bash
./tests/Live.Tests/Identity/bug-bash-identity-cleanup.sh
```
Deletes the resource group (and everything in it) and the GitHub environment. Leaves
the workflow file itself in place.

