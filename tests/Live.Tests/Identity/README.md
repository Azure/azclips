# Verify AzCLIPS OIDC (federated credential) login works in GitHub Actions

**Owning Squad:** Identity

Tests both surfaces in one workflow run: CLI on `ubuntu-latest`, PowerShell on
`windows-latest`. All the one-time Azure/GitHub setup is scripted - you only need to
set your `ALIAS`.

## Prerequisite

- A bash shell. Linux/macOS have one built in; on Windows, use Git Bash (installed
  alongside Git for Windows) - run these scripts from a Git Bash terminal, not
  PowerShell or cmd.
- Azure CLI and GitHub CLI installed and signed in:
  - `az login --tenant <yourBAMITenant>` if you have not already login
  - `gh auth login` & `gh auth status` if you have not already login
- Your current branch must have an upstream tracking branch with the **same name**,
  pushed to your own fork (not `Azure/azclips`) - e.g. `git push -u origin
  $(git branch --show-current)`. `bug-bash-identity-env.sh` reads the remote/repo from
  that tracking info and refuses to run if no tracking branch is set, the tracked
  branch name differs from your local branch
- Your branch must be in sync with its remote counterpart -
  `bootstrap-and-run.sh` triggers the workflow via
  `gh workflow run --ref <your-branch>`, which runs whatever is committed on the
  remote branch, not your local uncommitted changes.

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

