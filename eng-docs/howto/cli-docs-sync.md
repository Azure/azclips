# CLI docs sync (to MicrosoftDocs/azure-docs-cli)

Runbook for [build/Sync-AzClipsCliDocs.ps1](../../build/Sync-AzClipsCliDocs.ps1). Covers running locally, the three modes (dry-run / `-Force` / `-Push`), and how to eyeball the output on a real PR against `MicrosoftDocs/azure-docs-cli`.

## What it does

Runs a two-stage transform of AzClips CLI content into the shape `MicrosoftDocs/azure-docs-cli` expects:

1. **Exporter** ([`tools/Help/Azclips.Cli.Export`](../../tools/Help/Azclips.Cli.Export/)) — reads AzClips CLI runtime metadata + authored help markdown under `src/CLI/Help/Docs/**` and emits `snake_case` JSON at `artifacts/cli-export/latest/<verb>.json` + `_root_.json`.
2. **CLI2Yaml** (docs.ms's NuGet-packaged transformer) — reads the JSON + side-car configs (`.docsreference.azclips.json`, `service_name.json`, `index.json`) and emits `YamlMime:AzureCLIGroup` SDP YAML into `docs-ref-autogen/<VersionFolder>/latest/*.yml` in the target docs repo.

Every emitted `editLink:` points at `https://github.com/Azure/azclips/blob/main/…` — via the `--sourceRepo` / `--editBranch` options landed in [ceapex/CLI2Yaml PR 1213094](https://ceapex.visualstudio.com/Engineering/_git/CLI2Yaml/pullrequest/1213094), packaged as `CLI2Yaml 20260827.1.0` and later.

## Three-mode escape hatch

| Mode              | Flag     | Effect                                                                                                                                                 | Auth needed                     |
| ----------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------- |
| Dry-run (default) | —        | Builds + exports + runs CLI2Yaml into a scratch folder at `artifacts/cli-yaml/dry-run/`; prints a shape report with editLink sanity. No target writes. | Read-only on the `apidrop` feed |
| Force             | `-Force` | Same pipeline but writes into a sibling `MicrosoftDocs/azure-docs-cli` clone, creates an `azclips-cli-sync-<timestamp>` branch, commits. **No push.**  | Same                            |
| Push              | `-Push`  | Would push the branch + open a PR. **Currently throws** — docs.ms bot-auth pattern is still pending.                                                   | Deferred                        |

## Prerequisites

- **.NET SDK** matching `global.json`.
- **PowerShell 7** (`pwsh`).
- **Git** — the `-Force` mode branches + commits inside the target clone, and the mock-PR workflow below uses ordinary `git` commands.
- **GitHub CLI** (`gh`) — optional; only needed if you plan to open a mock PR against a personal fork of `MicrosoftDocs/azure-docs-cli` from this machine.
- **Azure Artifacts Credential Provider** installed on a Microsoft-tenant Windows machine — the [`apidrop`](https://apidrop.visualstudio.com/) NuGet feed hosts `CLI2Yaml` behind AAD auth:
    ```powershell
    iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"
    ```
- **CLI2Yaml package cached locally** — first-time setup, one command:
    ```powershell
    $fetch = Join-Path $env:TEMP "azclips-cli2yaml-fetch"
    New-Item -Path $fetch -ItemType Directory -Force | Out-Null
    @'
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RestoreSources>https://apidrop.pkgs.visualstudio.com/_packaging/ReferenceAutomation/nuget/v3/index.json</RestoreSources>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="CLI2Yaml" Version="*" />
      </ItemGroup>
    </Project>
    '@ | Set-Content (Join-Path $fetch "Fetch.csproj")
    dotnet restore (Join-Path $fetch "Fetch.csproj") --interactive
    ```
    Subsequent runs pull from `~/.nuget/packages/cli2yaml/` silently. If the script can't find the DLL there, it throws with this exact snippet.

## Config

[`build/cli-docs-sync.config.json`](../../build/cli-docs-sync.config.json) holds the defaults:

```json
{
    "moniker": "azure-cli-preview",
    "versionFolder": "preview-version",
    "targetRepoPath": "../azure-docs-cli",
    "sourceRepoUrl": "https://github.com/Azure/azclips",
    "sourceBranch": "main",
    "exportProject": "tools/Help/Azclips.Cli.Export/Azclips.Cli.Export.csproj",
    "docsReferenceConfig": "src/CLI/Help/.docsreference.azclips.json",
    "extensionIndex": "src/CLI/Help/index.json",
    "serviceMapping": "src/CLI/Help/service_name.json",
    "cli2yamlPackage": "CLI2Yaml",
    "cli2yamlFeed": "https://apidrop.pkgs.visualstudio.com/_packaging/ReferenceAutomation/nuget/v3/index.json"
}
```

**Precedence** — for each overrideable field: CLI param (`-Moniker`, `-VersionFolder`, `-TargetRepoPath`, `-SourceRepoUrl`, `-SourceBranch`) → env var (`AZCLIPS_CLI_MONIKER`, `AZCLIPS_CLI_VERSION_FOLDER`) → config file.

**CI-oriented parameters** (all optional, no effect on interactive use):

| Param                | Purpose                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `-NoBuild`           | Skip the leading `dotnet build azclips.slnx` (CI usually builds in a preceding step)                                                             |
| `-Configuration`     | `Debug` \| `Release` for the build + exporter run (default `Debug`)                                                                              |
| `-FetchIfMissing`    | Auto-fetch `CLI2Yaml` non-interactively when missing from the NuGet cache. Requires pre-arranged credentials (federated identity or PAT env var) |
| `-CommitAuthor`      | Inline `user.name` for the target commit. Does NOT persist to the target repo's git config                                                       |
| `-CommitEmail`       | Inline `user.email` — same                                                                                                                       |
| `-OutputManifest`    | Write a machine-readable JSON summary to the given path. Feeds CI PR-body generation, artifact upload, downstream steps                          |
| `-SkipServicePages`  | Drop `service-page/**/*.yml` from CLI2Yaml output before applying to the target, and regenerate `latest/TOC.yml` as an alphabetical command-group tree preserving literal command spelling in navigation labels. Groups with subgroups have a `Commands` entry. Root commands from `reference-index.yml` (such as `az login`) are included alongside the groups; command names and UIDs are unchanged. Defaults to `$true` because our current `service_name.json` produces a single broken page with unresolvable xrefs, and CLI2Yaml's own TOC only points at that page. Pass `-SkipServicePages:$false` once CLI2Yaml supports the AzClips layout natively |

**Two folder concepts, decoupled by design:**

- `moniker` = URL slug (`?view=azure-cli-preview` on Learn). Permanent, per docs.ms rules.
- `versionFolder` = filesystem name inside `docs-ref-autogen/`. Convention only, tied to the URL slug via `docfx.json`'s `group`/`src` mapping. Matches how `azure-cli-latest` slug's content lives at `docs-ref-autogen/Latest-version/latest/`.

## Running locally

### Dry-run (safest — start here)

```powershell
pwsh -File build/Sync-AzClipsCliDocs.ps1
```

Output goes to `artifacts/cli-yaml/dry-run/latest/*.yml`. Expected report tail:

```
=== AzClips CLI docs sync — dry-run report ===

Moniker              : azure-cli-preview
Version folder       : preview-version
editLink source repo : https://github.com/Azure/azclips
editLink branch      : main
Files produced       : 20 (20 yml)
Output location      : <repo>/artifacts/cli-yaml/dry-run

Top-level files:
  agent.yml                                        667 bytes
  config.yml                                     7,184 bytes
  ...

editLink sanity (first 3):
  editLink: https://github.com/Azure/azclips/blob/main/src/CLI/Help/Docs/config/index.md
  editLink: https://github.com/Azure/azclips/blob/main/src/CLI/Help/Docs/config/index.md
  editLink: https://github.com/Azure/azclips/blob/main/src/CLI/Help/Docs/config/index.md
```

Any `editLink:` line that doesn't start with `<sourceRepo>/blob/<sourceBranch>/` triggers a yellow warning — the `--sourceRepo`/`--editBranch` override didn't apply (usually because a stale pre-`20260827.1.0` CLI2Yaml is cached).

### `-Force` — write to a local target clone

Requires a local clone of the target docs repo (or a stand-in). Simplest way:

1. Fork `MicrosoftDocs/azure-docs-cli` on GitHub via `gh repo fork MicrosoftDocs/azure-docs-cli --clone=false`.
2. Clone your fork as a sibling of this repo:
    ```powershell
    Push-Location ..
    git clone https://github.com/<your-gh-user>/azure-docs-cli.git
    Pop-Location
    ```
3. Add `upstream` for later hygiene: `git -C ../azure-docs-cli remote add upstream https://github.com/MicrosoftDocs/azure-docs-cli.git`.
4. The moniker registration PR ([MicrosoftDocs/azure-docs-cli#6153](https://github.com/MicrosoftDocs/azure-docs-cli/pull/6153)) creates the `docs-ref-autogen/preview-version/latest/` folder. If it hasn't merged yet, check out the PR branch on your target clone or create the folder yourself to simulate:
    ```powershell
    New-Item -ItemType Directory -Path ../azure-docs-cli/docs-ref-autogen/preview-version/latest -Force | Out-Null
    ```
5. Run:
    ```powershell
    pwsh -File build/Sync-AzClipsCliDocs.ps1 -Force -Timestamp 'YYYYMMDDHHMM'
    ```

`-Timestamp` is optional but locks the branch name (`azclips-cli-sync-<timestamp>`) and commit message. Otherwise the script uses the current UTC time.

Expected tail on a fresh-content run:

```
=== Sync applied ===
Branch      : azclips-cli-sync-202609030524
Commit      : <sha>
Message     : AzClips-CLI docs sync -- azure-cli-preview -- 202609030524
Files       : 20

(Nothing pushed. Inspect with: git -C '<target>' log azclips-cli-sync-<timestamp> --stat)
```

If the target already matches AzClips CLI output (nothing to commit), the script reports it and cleans up the branch:

```
=== No changes to sync ===
Target already matches AzClips CLI output (moniker: azure-cli-preview). Nothing committed.
```

### Reviewing the result on GitHub

For a real preview PR against upstream (not a fork), push the sync branch and let the docs.ms OP build render the pages on `review.learn.microsoft.com`:

```powershell
git -C ../azure-docs-cli push origin azclips-cli-sync-202609030524
gh pr create --repo MicrosoftDocs/azure-docs-cli `
    --base main --head "<your-gh-user>:azclips-cli-sync-202609030524" `
    --draft `
    --title "Preview: AzClips CLI docs sync"
```

Docs.ms's build check on the PR includes a preview URL. Every rendered page's "Edit this page" link should point at `github.com/Azure/azclips/blob/main/…`.

> ⚠️ Same trap as the PS side — after pushing to your fork, don't click GitHub's "Compare & pull request" web-UI prompt. It defaults the base to the fork parent's `main` with the sync shim's auto-commit message as title. Always open via the `gh pr create` command above with an explicit `--head <owner>:<branch>`.

### `-Push` (currently deferred)

Fails cleanly with a message pointing at the auth-pattern decision. Nothing to do here yet.

## Cleanup

After `-Force` (or a mock-PR round trip):

```powershell
# Return the local target clone to whatever branch you were tracking
git -C ../azure-docs-cli checkout main
git -C ../azure-docs-cli branch -D azclips-cli-sync-<timestamp>

# Wipe the scratch dry-run + workspace inside azclips
Remove-Item artifacts/cli-yaml -Recurse -Force
```

The CLI2Yaml cache at `~/.nuget/packages/cli2yaml/` stays around — harmless, and next run is instant.

## Common failure modes

| Error                                                               | Cause                                                                                                                    | Fix                                                                                                                                             |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `CLI2Yaml is not in the local NuGet cache`                          | First run on this machine — package hasn't been fetched                                                                  | Follow the fetch snippet in the error message (or the Prerequisites section above)                                                              |
| `401 Unauthorized` during the fetch snippet                         | Credential Provider not installed or not picked up                                                                       | Rerun `iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) }"`, then retry with `--interactive`                                   |
| `Target version folder 'preview-version' does not exist`            | Registration config PR ([#6153](https://github.com/MicrosoftDocs/azure-docs-cli/pull/6153)) not merged / not checked out | Check out the PR branch on your target clone, or `New-Item -ItemType Directory -Path ../azure-docs-cli/docs-ref-autogen/preview-version -Force` |
| `Target working tree is dirty`                                      | Uncommitted changes in the local target clone                                                                            | Commit, stash, or `git checkout .` in the target                                                                                                |
| `Target '…' is not a git repository`                                | `-TargetRepoPath` points somewhere that isn't a git repo                                                                 | Check the path; typically `../azure-docs-cli`                                                                                                   |
| `Branch 'azclips-cli-sync-…' already exists`                        | Re-running with the same `-Timestamp`                                                                                    | Delete the old branch or pass a fresh `-Timestamp`                                                                                              |
| yellow warning: `editLinks do NOT start with '<sourceRepo>/blob/…'` | A stale pre-`20260827.1.0` `CLI2Yaml` package is cached and doesn't support `--sourceRepo`                               | `Remove-Item -Recurse "$env:USERPROFILE\.nuget\packages\cli2yaml"` and rerun the fetch snippet to pull the current version                      |
| `-Push is not yet implemented`                                      | Correct behavior — auth boundary                                                                                         | Use `-Force` and hand off to your normal PR flow                                                                                                |

## Running the tests

```powershell
Import-Module Pester -MinimumVersion 5.0
Invoke-Pester -Path tests/Cli.DocsSync.Tests/ -Output Detailed
```

Pester tests cover: CLI2Yaml DLL resolution, target-version-folder composition, `latest/` folder clearing, target-repo preflight, workspace assembly, and `-Force`-mode git plumbing (against a temp `git init`'d target — no external target needed).

Exporter + CLI2Yaml integration is intentionally not part of Pester — both require a full build + real NuGet package, too heavy for the test suite. That validation is the dry-run smoke.

## CI orchestration

**PR CI** — the `cli-docs-sync-tests` job in [`.github/workflows/build-and-test.yml`](../../.github/workflows/build-and-test.yml) runs the Pester suite plus a load-only smoke (dot-sources the shim under `AZCLIPS_CLI_SYNC_LOAD_ONLY=1` to catch parse errors and stale function references without needing CLI2Yaml, which lives behind apidrop auth).

**Publish CI** — not wired yet. The shim is intentionally iterative; we're battle-testing it by using it to refresh [MicrosoftDocs/azure-docs-cli#6153](https://github.com/MicrosoftDocs/azure-docs-cli/pull/6153) as we go. A publish workflow (real dry-run + `-Push`) lands once the docs.ms bot-auth pattern is decided (588-D2).

## What's NOT covered (yet)

- **Actual publish.** `-Push` throws by design. Real bot-PR-open + `git push` land when the docs.ms auth pattern is decided.
- **Conceptual docs.** This shim is reference-only. The 11 conceptual articles the content writer plans to migrate live under `docs-ref-conceptual/`, not `docs-ref-autogen/`, and are hand-copied — no automation planned initially.
- **PR body customization / label management.** Deferred.
- **Multi-moniker publishing.** Current script assumes a single moniker per invocation. If we ship multiple concurrent preview channels (e.g. `azure-cli-preview` and a `-lts-preview`), config gains a moniker-list.
- **Automatic CI trigger on AzClips releases.** Same rationale as the PS shim — awaits the bot-auth decision.
