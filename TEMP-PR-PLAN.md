# TEMP-PR-PLAN: fix `az help` blank summary for intermediate sub-groups (Azure/CLIPS#504)

> Scratch file. **Delete before merging.**

## Status: implementation complete, ready for review

- [x] Runtime resolver switched to nested layout
- [x] `ExtensionIndexHelpAdapter` sources summaries from markdown at every level
- [x] Corpus migrated (9 renames via `git mv` preserving blame) + 3 new intermediate `index.md`
- [x] Tool-side updates (`HelpCommandKey`, `HelpCompletenessValidator`, `Scaffolder`)
- [x] Test coverage added: `MarkdownCommandHelpProviderTests`, `ExtensionDispatcherTests`, `HelpCommandKeyTests`, `HelpCompletenessValidatorTests`
- [x] Full build + help-artifacts smoke test clean (0 warnings, 0 errors, "no issues")
- [x] Live-verified with `dotnet artifacts\Debug\Core.Cli\az.dll deployment group --help`
- [x] Docs (spec + how-to + wiki) updated
- [x] Cli.Tests: 407/407 non-E2E passed; Help.Tool.Tests: 174/174 passed

Pre-existing unrelated failure: `ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail` needs a published `az` binary (native exe from `dotnet publish`), not affected by this change.

---

## Goal

Fix [Azure/CLIPS#504](https://github.com/Azure/CLIPS/issues/504) — `az deployment group --help` renders a blank summary because intermediate sub-groups (nodes that are neither the verb root nor a leaf command) have no summary source.

Structural fix, not a bandage: **migrate CLI help markdown from flat kebab (`deployment/group-create.md`) to nested directories (`deployment/group/create.md` + `deployment/group/index.md`).** Intermediate-group summary then falls out of the same `index.md` rule that already covers verb roots — no new spec section, no orphan-vs-required policy debate.

## Convention (new)

| Path role                                    | Old (kebab)                       | New (nested)                                                      |
| -------------------------------------------- | --------------------------------- | ----------------------------------------------------------------- |
| Verb root                                    | `Docs/deployment/index.md`        | `Docs/deployment/index.md` (unchanged)                            |
| Leaf command under verb                      | `Docs/deployment/create.md`       | `Docs/deployment/create.md` (unchanged for single-segment leaves) |
| Leaf command under sub-group                 | `Docs/deployment/group-create.md` | `Docs/deployment/group/create.md`                                 |
| Intermediate sub-group                       | _(no home — the bug)_             | `Docs/deployment/group/index.md`                                  |
| Verb that is also runnable (e.g. `az login`) | `Docs/login/index.md`             | `Docs/login/index.md` (unchanged)                                 |

Rule: filesystem mirrors command tree 1:1. `az a b c --help` → `Docs/a/b/c.md` (leaf) or `Docs/a/b/c/index.md` (group). Resolver looks for the leaf file first, then falls back to `<key>/index.md`, so we never need to know up-front whether a path is a group or a command.

## Existing corpus (45 files)

Files that need renaming (all others stay put):

- `Docs/deployment/group-create.md` → `Docs/deployment/group/create.md`
- `Docs/deployment/group-what-if.md` → `Docs/deployment/group/what-if.md`
- `Docs/agent/tool-env.md` → `Docs/agent/tool/env.md`
- `Docs/agent/tool-help.md` → `Docs/agent/tool/help.md`
- `Docs/agent/tool-resource-find.md` → `Docs/agent/tool/resource/find.md`
- `Docs/agent/tool-resource-refresh.md` → `Docs/agent/tool/resource/refresh.md`
- `Docs/agent/tool-resource-show.md` → `Docs/agent/tool/resource/show.md`
- `Docs/agent/tool-resource-sku.md` → `Docs/agent/tool/resource/sku.md`
- `Docs/agent/tool-resource-tree.md` → `Docs/agent/tool/resource/tree.md`

New intermediate-group `index.md` files to author (this is what fixes the bug):

- `Docs/deployment/group/index.md` — summary for `az deployment group`
- `Docs/agent/tool/index.md` — summary for `az agent tool`
- `Docs/agent/tool/resource/index.md` — summary for `az agent tool resource`

All other verbs (`vm`, `disk`, `config`, `extension`, `group`, `login`, `logout`, `profile`, `rest`, `version`, `login`, `profile`) only have single-segment leaves, so their trees don't move.

## Consumers to update

### Runtime (`src/CLI/Help/`)

- [ ] `MarkdownCommandHelpProvider.ResolveHelpFilePath` — split `commandKey` on space, walk segments; try `<segments>.md` (leaf) then `<segments>/index.md` (group). Keep the empty-key `index.md` shortcut for the verb root.
- [ ] `ExtensionIndexHelpAdapter.GetGroupHelp` — drop the `groupPath.Count == 1` guard; look up summary for every group path (root or intermediate) via `MarkdownCommandHelpProvider.Get(Verb, relativeKey)`.
- [ ] `ExtensionIndexHelpAdapter.BuildGroup` — populate each `RuntimeHelpGroupSummary.Summary` from `MarkdownCommandHelpProvider.Get(Verb, subgroupAbsoluteKey).Summary`.
- [ ] `RuntimeHelpRenderer` — no change; already handles non-empty summaries. Verify header line renders `az deployment group : <summary>` after the fix.

### Packaging (`src/CLI/Core.Cli.csproj`)

- [ ] No change needed. Content glob is already `Help\Docs\**\*.md` and `<Link>help\%(RecursiveDir)%(Filename)%(Extension)</Link>` preserves nested subdirectories under `help/`.

### Build-time tool (`tools/Help/Azclips.Help.Tool/`)

- [ ] `HelpCommandKey.ToHelpFileName` — return a relative path with `/` separators: `""` → `"index.md"`, `"config get"` → `"config/get.md"`. Break the kebab rule.
- [ ] `HelpCommandKey.FromHelpFileName` — accept path-with-separators input; strip trailing `/index.md` for group entries.
- [ ] `HelpCompletenessValidator` —
    - `expectedFiles` set: add every proper prefix of each routed command key as `<prefix>/index.md`. So `deployment group create` contributes `deployment/group/create.md` **and** `deployment/group/index.md`. Verb-root `index.md` continues to be required.
    - Directory enumeration and orphan detection already use recursive `SearchOption.AllDirectories` — normalize the enumerated relative paths to `/` separators for comparison.
- [ ] `Scaffolder` — emit `mkdir -p` for nested paths; write `index.md` stubs for every intermediate prefix.
- [ ] `MarkdownStructureValidator` — group-page relaxation: `index.md` files that describe an intermediate group only require frontmatter `summary`; the `Description:` line and ≥1 example become optional. (Verb-root `index.md` files today already carry both; we don't need to loosen those.) Simplest heuristic: if the file's parent directory contains other `.md` files at the same level, treat it as a group page. Alternative: expose the "kind = group vs leaf" decision from the same key-vs-file walk used by completeness.
- [ ] `ExampleSyntaxValidator`, `PowerShellExampleSyntaxValidator` — no logic change; they walk `SearchOption.AllDirectories` already. Sanity-check the error-message path formatting after the rename.

### Manifest emission (`src/CLI/Help/ExtensionHelpManifestBuilder.cs`)

- [ ] Uses `MarkdownCommandHelpProvider.Get(verb, cliEntry.Key)` today, so it picks up the new resolver for free. Optional add: emit a `Subgroups: IReadOnlyList<(string Path, string Summary)>` on `HelpManifest` for the agent's help lookup tools. Additive schema change — call out separately if we do it.

### Docs

- [ ] `docs/spec/help-system.md` §4 — rewrite the authoring model section: nested tree, `index.md` at every group level, leaf `<name>.md`. Update the sample tree in §3.
- [ ] `docs/howto/help.md` — update file-path column, scaffolding example, and the `New-PSHelpMarkdown.ps1` (CLI) equivalents.
- [ ] `docs/spec/ARCHITECTURE.md` — one-line path fix.
- [ ] `wiki/01-getting-started/cli-usage.md`, `wiki/02-deep-dive/build-cicd.md`, `wiki/02-deep-dive/extension-system.md`, `wiki/onboarding-guide.md`, `wiki/zero-to-hero-guide.md`, `wiki/catalogue.json` — path references, mostly one-liners.

### Tests

- [ ] `tests/Cli.Tests/Help/` — extend `MarkdownCommandHelpProviderTests` (or add if missing) with:
    - Group leaf: `Get("deployment", "group create")` returns markdown from `Docs/deployment/group/create.md`.
    - Intermediate group: `Get("deployment", "group")` returns markdown from `Docs/deployment/group/index.md`.
    - Verb root still resolves from `Docs/<verb>/index.md`.
- [ ] `tests/Cli.Tests/Help/` — `ExtensionIndexHelpAdapterTests`: `GetGroupHelp(["deployment","group"])` → non-empty `Summary`; sub-group rows in `GetGroupHelp(["deployment"])` carry summaries.
- [ ] `tests/Cli.Tests/Help/HelpPathsCasingTests.cs` — verify the new nested `deployment/group/` folder passes the casing rule (Linux-CI-safe).
- [ ] `tools/Help/Azclips.Help.Tool.Tests/HelpCommandKeyTests.cs` — update expected values: `"config get"` → `"config/get.md"`; add cases for nested (`"deployment group create"` → `"deployment/group/create.md"`).
- [ ] `tools/Help/Azclips.Help.Tool.Tests/HelpCompletenessValidatorTests.cs` — add cases:
    - Intermediate `index.md` present → no orphan warning.
    - Intermediate `index.md` missing → `Completeness_MissingHelpFile` error.
    - Nested leaf under intermediate group present/missing.

### Manual verification

- [ ] `./build/build.ps1 -GenerateHelpArtifacts -Configuration Debug` — canonical end-to-end smoke test (surfaced 52 false positives on prior help refactor per repo memory; must be re-run against real corpus).
- [ ] `az deployment group --help` — header line shows non-empty summary.
- [ ] `az deployment --help` — `group` sub-group row shows non-empty summary.
- [ ] `az agent tool resource --help` — same coverage.

## Sequencing (single PR)

1. Rename markdown files (git mv to preserve blame) + author the three new `index.md` files.
2. Update `MarkdownCommandHelpProvider.ResolveHelpFilePath` (runtime resolver).
3. Update `ExtensionIndexHelpAdapter` (drop empty-summary guards).
4. Update `HelpCommandKey` (`ToHelpFileName` / `FromHelpFileName`) — cascading test updates.
5. Update `HelpCompletenessValidator` (expected-file set + normalized path comparison).
6. Update `MarkdownStructureValidator` (group-page relaxation).
7. Update `Scaffolder` (nested output).
8. Update docs + wiki path references.
9. Full build + `-GenerateHelpArtifacts` sweep, fix fallout.

## Open questions

- **Group-page examples**: keep optional (`index.md` for a group has no runnable command) or require ≥1 example that dispatches to a child? Current lean: **optional**, matches PlatyPS convention for group pages. Revisit if it degrades docs quality.
- **Manifest `Subgroups` field**: additive schema change for agent tools. Ship in this PR or a follow-up? Lean: **follow-up**, keeps this PR scope tight and doesn't force agent-tool consumers to redeploy.
- **`ExampleSyntaxValidator` file-path formatting**: after rename the reported `FilePath` shows nested segments (`Docs/deployment/group/create.md`). Confirm this reads well in CI output — probably fine.

## Repo-memory hooks to update after merge

- `/memories/repo/help-paths-casing.md` — path grid changes; nested folders now exist.
- `/memories/repo/help-tool-pr253-followup.md` — the "file names use sub-key only" note becomes obsolete; replace with "nested `index.md` at every group level."
