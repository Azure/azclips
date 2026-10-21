# PowerShell docs sync (to MicrosoftDocs/azure-docs-powershell)

Runbook for [build/Sync-AzclipsPowerShellDocs.ps1](../../build/Sync-AzclipsPowerShellDocs.ps1). Covers running locally, the three modes (dry-run / `-Force` / `-Push`), and how to eyeball the output against a personal-fork target.

## What it does

Copies our authored PlatyPS markdown from `src/PS/<Module>/help/*.md` into `<TargetRepo>/<Moniker>/Az.<Module>/` and generates a per-module landing page (`Az.<Module>.md`). The sync rewrites only the frontmatter `Module Name:` to match the packaged module name (for example, `Accounts` becomes `Az.Accounts`); Markdown body content and line endings are preserved. Missing or duplicate module-name fields fail explicitly. The target repo's docfx build (via `PSMD2Yaml`) handles rendering.

Docs sync, module packaging, and release changelogs share the naming rule in [build/Get-AzPackageName.ps1](../../build/Get-AzPackageName.ps1). The `Az` rollup keeps its name; leaf modules use `Az.<Module>`. The old `targetModuleNamePattern` setting is no longer used, so docs names cannot drift from package identities.

## Three-mode escape hatch

| Mode              | Flag     | Effect                                                                                               | Auth needed |
| ----------------- | -------- | ---------------------------------------------------------------------------------------------------- | ----------- |
| Dry-run (default) | —        | Prints the copy plan; no filesystem writes                                                           | No          |
| Force             | `-Force` | Writes to the local target repo + commits on a fresh branch. **No push.**                            | No          |
| Push              | `-Push`  | Would push the branch + open a PR. **Currently throws** — docs.ms bot-auth pattern is still pending. | Deferred    |

## Config

`build/ps-docs-sync.config.json` holds the defaults:

```json
{
    "moniker": "azps-100.0.0",
    "targetRepoPath": "../azure-docs-powershell",
    "sourcePsRoot": "src/PS",
    "sourceRepoUrl": "https://github.com/Azure/azclips",
    "sourceBranch": "main",
    "downloadHelpLinkBase": "https://learn.microsoft.com/powershell/module"
}
```

**Precedence** — for the moniker specifically: CLI param (`-Moniker`) → env var (`$env:AZCLIPS_PS_MONIKER`) → config file. The default `azps-100.0.0` is the registered PowerShell preview moniker.

## Running locally

### Dry-run (safest — start here)

```powershell
pwsh -File build/Sync-AzclipsPowerShellDocs.ps1
```

Prints the discovered modules + a copy plan (one entry per cmdlet md, plus one generated module-index per module). No filesystem writes.

Expected shape:

```
Publishable     : 8 module(s) - Accounts, Compute, Config, KeyVault, ManagedServiceIdentity, MsGraph, Network, Resources
Total files     : 62 (54 cmdlet md + 8 module-index)

Copy plan:

  Az.Compute/
    Get-AzVM.md                                    <-  Get-AzVM.md
    New-AzVM.md                                    <-  New-AzVM.md
    Remove-AzVM.md                                 <-  Remove-AzVM.md
    Az.Compute.md                                  <-  (generated)
  ...
```

### `-Force` — write to local target

Requires a local clone of the target docs repo (or a stand-in). Simplest way:

1. Fork `MicrosoftDocs/azure-docs-powershell` on GitHub via `gh repo fork MicrosoftDocs/azure-docs-powershell --clone=false`.
2. Clone your fork as a sibling of this repo:
    ```powershell
    Push-Location ..
    git clone https://github.com/<your-gh-user>/azure-docs-powershell.git
    Pop-Location
    ```
3. Add `upstream` for later hygiene: `git -C ../azure-docs-powershell remote add upstream https://github.com/MicrosoftDocs/azure-docs-powershell.git`.
4. Simulate onboarding (docs.ms desk-ticket is what actually creates the moniker folder in production):
    ```powershell
    New-Item -ItemType Directory -Path ../azure-docs-powershell/azps-100.0.0 -Force | Out-Null
    ```
5. Run:
    ```powershell
    pwsh -File build/Sync-AzclipsPowerShellDocs.ps1 -Force -Timestamp 'YYYYMMDDHHMM'
    ```

`-Timestamp` is optional but locks the branch name (`azclips-ps-sync-<timestamp>`) and commit message for reproducibility. Otherwise the script uses the current UTC time.

Expected output ends with:

```
=== Sync applied ===
Branch      : azclips-ps-sync-202608201500
Commit      : <sha>
Message     : AzClips-PS docs sync -- azps-100.0.0 -- 202608201500
Files       : 62

(Nothing pushed. Inspect with: git -C '<target>' log azclips-ps-sync-<timestamp> --stat)
```

### Reviewing the result on GitHub (mock PR against your fork)

After `-Force`, push the sync branch to your own fork and open a draft PR **against your fork's own `main`** (not upstream):

```powershell
git -C ../azure-docs-powershell push origin azclips-ps-sync-202608201500
gh pr create --repo <your-gh-user>/azure-docs-powershell `
    --base main --head azclips-ps-sync-202608201500 `
    --draft `
    --title "MOCK: AzClips-PS docs sync preview (do not merge)" `
    --body "Rehearsal; see Azure/azclips#721."
```

You now have a GitHub-rendered view of what a real sync PR looks like. Reviewers can eyeball each module-index landing page as GitHub would render it. Zero risk to the real `MicrosoftDocs/*` upstream.

### `-Push` (currently deferred)

Fails cleanly with a message pointing at the auth-pattern decision. Nothing to do here yet.

## Cleanup

After `-Force` (or the mock-PR round trip):

```powershell
# Close the mock PR + delete the branch on your fork
gh pr close <pr-number> --repo <your-gh-user>/azure-docs-powershell --delete-branch

# Return the local target clone to main
git -C ../azure-docs-powershell checkout main
git -C ../azure-docs-powershell branch -D azclips-ps-sync-<timestamp>

# Remove the simulated onboarding folder (real onboarding creates it upstream, not locally)
Remove-Item ../azure-docs-powershell/azps-100.0.0 -Recurse -Force
```

## Common failure modes

| Error                                                | Cause                                                                                                  | Fix                                                               |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------- |
| `Target moniker folder '…' does not exist under '…'` | You skipped the "simulate onboarding" step (or the docs.ms desk-ticket is still pending in production) | Create the folder to simulate, or wait for onboarding to complete |
| `Target working tree is dirty`                       | Uncommitted changes in the local target clone                                                          | Commit, stash, or `git checkout .` in the target                  |
| `Target '…' is not a git repository`                 | `-TargetRepoPath` points somewhere that isn't a git repo                                               | Check the path; typically `../azure-docs-powershell`              |
| `Branch 'azclips-ps-sync-…' already exists`          | Re-running with the same `-Timestamp`                                                                  | Delete the old branch or pass a fresh `-Timestamp`                |
| `-Push is not yet implemented`                       | Correct behavior — auth boundary                                                                       | Use `-Force` and hand off to your normal PR flow                  |

## Running the tests

```powershell
Import-Module Pester -MinimumVersion 5.0
Invoke-Pester -Path tests/Ps.DocsSync.Tests/ -Output Detailed
```

Pester tests cover: discovery of the current modules, SYNOPSIS extraction, shared package naming, frontmatter-only rewriting (including line-ending and body preservation), plan generation, module-index content, precondition checks, and `-Force`-mode git plumbing (against a temp `git init`'d target — no external target needed). File counts grow as new cmdlets and modules are added.

## CI orchestration

Runs on every PR / push to `main` via the `ps-docs-sync-tests` job in [.github/workflows/build-and-test.yml](../../.github/workflows/build-and-test.yml). Three steps: install Pester → run Pester → run the sync in dry-run against a bogus `-TargetRepoPath` (script warns rather than fails when the target is missing, which is the right CI behavior).

Fork-PR safe — no secrets, no external clone, no `git push`.

## What's NOT covered (yet)

- **Actual publish.** `-Push` throws by design. Real bot-PR-open + `git push` land when the docs.ms auth pattern is decided.
- **Docset config.** Adding an AzClips moniker to `mapping/monikerMapping.json`, creating `docs-conceptual/<moniker>/toc.yml`, and configuring the docset with `PSMD2Yaml` are docs.ms's responsibility (via desk ticket). Our sync assumes those already exist and fails clearly if they don't.
- **PR body customization / label management.** Deferred.
- **Multi-moniker publishing.** Current script assumes a single moniker per invocation. Multi-version support (if we ever version AzClips-PS) means a config extension.
