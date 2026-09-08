# Design Spec: Extension Management

> **Compat posture.** Source-compat with az-cli. Verbs, primary flags, and `--output json` shapes carry over; secondary flags, error structure, and internals are free to improve. A migration tool is acknowledged as necessary.
>
> **Related.** [extension-framework.md](./extension-framework.md) covers extension loading, ALC isolation, and the processor contract. This doc covers the layer _above_ that: how extensions are registered, discovered, routed to, and managed over their lifecycle.

---

## 1. Goals & non-goals

### Goals

- Make installed extensions first-class: usable without code changes to the host.
- Persistent registry populated at install/remove time, replacing any hardcoded command index.
- Full CRUD lifecycle: `add`, `list`, `remove`, `update`, `show`, `pin`, `unpin`.
- Multi-level command trees so extensions can ship `az vm boot-diagnostics enable` and similar.
- ALC caching across invocations for long-running hosts.
- Multiple extensions contributing under the same top-level verb, with per-leaf ownership.
- Host↔extension compatibility metadata so version mismatches surface as actionable errors.

### Non-goals

- **Help system.** `--help` rendering, group descriptions, MAML/online help — separate help branch.
- **PowerShell module routing.** Cross-client compat metadata (§6) is shared; PS module load/isolation has its own model. See [extension-framework.md](./extension-framework.md) §4.4.
- **NativeAOT.** Conflicts with dynamic loading regardless of routing topology.
- **Marketplace / catalog UX.** Discovery beyond the embedded hint catalog is vNext.
- **Trust / signing / provenance.** Anything in `~/.azclips/extensions/` loads.
- **Feed-fetch path for the hint catalog.** v1 is embed-only; the feed half is vNext.

---

## 2. Model

> **A top-level verb is a shared namespace. Any number of extensions can contribute leaves under it. Each leaf — the full `(verb, path)` tuple — is owned by exactly one extension.**

Consequences:

- Routing is `{(verb, path) → packageId}`. Lookup is O(1).
- There is no "namespace owner" role. Every contributor is equal. Namespaces exist by virtue of being contributed to.
- A single extension may contribute to multiple namespaces — e.g. one `Compute` package contributing under `vm`, `disk`, `disk-access`. This is the common case for Azure-style resource families.
- `az vm --help` (when help lands) aggregates leaves across all extensions contributing to `vm`. The host can answer "who contributes to this namespace" without loading anything.

### Bundled base extensions

Every packaging profile ships three extensions in the Bundled tier so a fresh install has a usable command surface out of the box:

- **`extension`** — manages other extensions (the CRUD surface in §3).
- **`login`** — authentication and tenant/subscription context.
- **`config`** — user/host configuration.

These are ordinary extensions with no runtime privileges — dispatch, discovery, and conflict resolution treat them identically to any other extension. What makes them non-removable at runtime is the **write boundary** ([extension-framework.md §2.3](./extension-framework.md#23-extension-tiers)): the extension API only touches the Installed root, and these three ship in the Bundled root. `az extension remove login` reports `ExtensionNotFound` for the same reason it does for any other Bundled-only extension.

Everything else (including `group` / Resources and `vm` / Compute) is an installable extension that can also appear in the Bundled tier depending on the packaging profile (full ships them, lean does not). The host has no built-in Azure-resource verbs.

---

## 3. User-facing surface

### 3.1 Extension CRUD commands

All under `az extension`. Kebab-case CLI convention.

| Command                                        | Behavior                                                                                                                                                                                                                                                                                              |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `az extension add <pkg>[@<version>] [--force]` | Install. Overwrites any prior version of the same package. `--pin` flag locks the version against `update`. `--force` is required to overwrite an existing pinned installation (the pin is then carried forward).                                                                                     |
| `az extension list`                            | Show installed extensions: name, `selectedVersion`, namespaces, install source, install date, and every discovered candidate per package (`candidates: [{ version, tier, location }]`, sorted by SemVer descending across Bundled + Installed tiers). `--by-verb` regroups output verb-headed (§3.5). |
| `az extension remove <name>`                   | Uninstall. Remove from registry, delete `~/.azclips/extensions/<pkg>/`. Reports `ExtensionNotFound` for Bundled-only extensions (they're part of the install artifact — see the write boundary in [extension-framework.md §2.3](./extension-framework.md#23-extension-tiers)).                        |
| `az extension update <name>[@<version>]`       | Install newer version, replacing the old one in place. Fails on pinned extensions unless `--force`.                                                                                                                                                                                                   |
| `az extension pin <name>[@<version>]`          | Lock active version. With no version, pins the currently installed version.                                                                                                                                                                                                                           |
| `az extension unpin <name>`                    | Remove the pin.                                                                                                                                                                                                                                                                                       |
| `az extension show <name>`                     | Detail view: full path, version, namespaces, contributions, source feed, install date, compat range.                                                                                                                                                                                                  |

`extension add --name <token>` resolves two ways: **dotted token** → passthrough (full package id, including third-party); **bare token** that is _not_ a known verb → legacy `Microsoft.Azure.Azclips.Extensions.{name}` prefix-strip. A bare token that **is** a known verb in the embedded `ExtensionHintTable` is rejected with a usage error pointing the user at the owning package id (e.g. `add --name disk` errors with "use `--name Compute`"). The unit of install is a package, not a verb — accepting a verb here would silently pull in unrelated sibling verbs (Compute ships both `vm` and `disk`).

The same rule applies to `extension remove/show/update/pin/unpin --name <token>`: only the package id (full or short suffix) is a valid selector. A miss is enriched with a "did you mean _PackageId_?" hint when the token is a verb or namespace contributed by exactly one installed package — diagnostic only, never auto-resolves the destructive action.

When `extension add --name <token>` misses every configured feed, the resulting `ExtensionNotFound` (exit 4) probes the local repo via `LocalArtifactProbe.Find` (walks up from cwd looking for `azclips.slnx`, then globs `artifacts/Debug/<packageId>/<packageId>.*.nupkg`) and surfaces the most recently written match as an `--path` suggestion in the error message. Suggestion-only — never auto-installs from local builds.

### 3.2 Conflict at install time

`extension add` validates the candidate's contributions against:

1. **Compat metadata** (§6). Hard fail if the candidate's declared range excludes the current `core` version.
2. **Per-leaf collision.** Every `(verb, path)` tuple the candidate contributes is checked against a discovery-backed registry (Bundled + Installed leaves overlaid with file-registry preferences). All collisions across the candidate's full leaf set are accumulated and surfaced as a single `LeafConflict`:

    ```
    > az extension add Contoso.Foo
    ERROR: [LeafConflict] Extension 'Contoso.Foo' has 2 leaf conflicts with already-installed extensions.
    Remove the conflicting extension(s) first or rename the offending leaves.
    ```

    Conflict order is deterministic (candidate `s_cliTable` insertion order). Undeclared-verb checks remain first-error-wins — those are usage errors in the candidate's own code; enumeration adds noise without value.

There is no tier bias in this check: a leaf collision against a Bundled extension fails the install exactly like a collision against another Installed extension. There is no `--force` override on collision. To swap owners of a leaf: `remove` then `add` (only possible when the incumbent is in the Installed tier — Bundled contributions can only change via a new install of azcli). Multiple extensions advertising `[ExtensionVerb("vm")]` is fine and expected — only per-leaf collisions fail.

### 3.3 Unknown-verb / unknown-leaf UX

When the dispatcher sees an unknown token, it consults the embedded hint catalog (§7) and falls through three cases:

1. **Exact `(verb, path)` match** in the catalog:

    ```
    > az vm list
    Error: 'vm list' is provided by extension 'Microsoft.Azure.Azclips.Extensions.Compute',
    which is not installed. Install it with:
      az extension add --name Compute
    ```

2. **Verb match, no path match**: "package X provides verb '<verb>' but no '<path>' leaf — try `az <verb> --help` after installing."

3. **No verb match**: fall through to the generic "try `extension search <verb>`" path. The AI fallback in `OrchestratorEngine` continues to handle free-form input.

### 3.4 Error surface

**Exit codes** (apply to the `extension` verb's commands):

| Code | Meaning                                              |
| ---- | ---------------------------------------------------- |
| 0    | success                                              |
| 1    | generic error                                        |
| 2    | usage error (bad args, unknown flag)                 |
| 3    | leaf conflict on install                             |
| 4    | extension not found (in feed or registry)            |
| 5    | feed / network error                                 |
| 6    | pin violation (`update` on pinned without `--force`) |
| 7    | compatibility violation (see §6)                     |
| 8    | registry concurrency (concurrent write detected)     |
| 9    | registry invalid (on-disk invariant violation)       |

**Structured output.** `extension` commands respect the host's existing `--output json` flag. On failure:

```jsonc
{
    "error": {
        "code": "LeafConflict",
        "message": "Extension 'Contoso.Foo' has 1 leaf conflict with already-installed extensions.",
        "details": {
            "packageId": "Contoso.Foo",
            "conflictCount": 1,
            "conflicts": [
                {
                    "leafKey": "vm contoso-magic",
                    "verb": "vm",
                    "path": "contoso-magic",
                    "existingPackageId": "Microsoft.Azure.Azclips.Extensions.Compute",
                },
            ],
        },
    },
}
```

- `code` is the **contract** for programmatic callers (`LeafConflict`, `ExtensionNotFound`, `CommandNotFound`, `PinViolation`, `FeedUnavailable`, `UsageError`, `CompatibilityViolation`, `RegistryConcurrency`, `RegistryInvalid`). Tests and downstream tooling MUST assert on `code`, never on `message` — the latter is free to evolve.
- `ExtensionNotFound` (exit 4) fires only when the owning package is genuinely absent from discovery. `CommandNotFound` (exit 4) fires when the top-level verb IS owned by a discovered extension but the specific `(verb, path)` leaf doesn't exist — e.g. `az vm --list` or `az group foobar` when Compute/Resources are installed. Splitting these prevents the misleading "install the extension you already have" hint.
- `message` is human-readable and not contractual.
- `details` carries operation-specific context. For `LeafConflict`, the array enumerates every `(verb, path)` a single `add` collided on. Field names within `details` are not contractual.

### 3.5 `extension list` output

Default output groups by extension. Each row carries every existing field plus `selectedVersion` (the version dispatch will route to) and `candidates` (every discovered `(version, tier, location)` triple, sorted by SemVer descending across both Bundled and Installed tiers):

```jsonc
[
    {
        "packageId": "Microsoft.Azure.Azclips.Extensions.Compute",
        "verb": "vm",
        "namespaces": ["vm", "disk"],
        "version": "0.1.2",
        "selectedVersion": "0.1.2",
        "pinned": false,
        "source": null,
        "installedAt": "2026-08-13T...",
        "location": "<install>/Extensions/.../Compute.dll",
        "candidates": [
            {
                "version": "0.1.2",
                "tier": "Bundled",
                "location": "<install>/Extensions/.../Compute.dll",
            },
            {
                "version": "0.1.1",
                "tier": "Installed",
                "location": "~/.azclips/extensions/.../0.1.1/Compute.dll",
            },
        ],
    },
]
```

With no pin, `selectedVersion` is the highest SemVer across tiers (here Bundled `0.1.2`). If the user runs `az extension pin --name Compute` while a lower version is currently selected, or pins the older version explicitly, `selectedVersion` follows the pin — the `candidates` array continues to surface every alternative so the user can see what they could unpin to.

When both tiers report the same `(PackageId, ResolvedVersion, command surface)`, `ExtensionCandidateSelector` collapses them to one candidate before this projection runs — there is no meaningful choice to expose. The multi-tier array populates when versions actually differ across tiers.

`--by-verb` regroups verb-headed:

```
vm
  Microsoft.Azure.Azclips.Extensions.Compute       0.1.2
  Contoso.VmPreviewExt                            0.3.0-preview
disk
  Microsoft.Azure.Azclips.Extensions.Compute       0.1.2
group
  Microsoft.Azure.Azclips.Extensions.Resources     1.0.0
```

Each contributor row under a verb carries the same `candidates` array as the default projection.

**Field-level stability contract:**

- Default JSON: `[{ packageId, verb, namespaces, version, selectedVersion, pinned, source, installedAt, location, candidates }]`. `version` remains for backward-compat and equals `selectedVersion`; consumers should migrate to `selectedVersion` in future changes.
- `--by-verb` JSON: `{ verb: [{ packageId, version, selectedVersion, pinned, source, installedAt, location, candidates }] }`.
- `candidates[i]` shape: `{ version, tier, location }`. `tier` is `"Bundled"` or `"Installed"` (path-inferred from the configured `assemblyLoading.installedExtensionsRoot`).

---

## 4. Internals

### 4.1 Registry schema (v2)

A single JSON file at `~/.azclips/extensions/registry.json`:

```jsonc
{
    "schemaVersion": 2,
    "extensions": {
        "Microsoft.Azure.Azclips.Extensions.Compute": {
            "version": "1.1.0",
            "pinned": false,
            "source": "https://api.nuget.org/v3/index.json",
            "installedAt": "2026-04-12T10:23:00Z",
            "namespaces": ["vm", "disk", "disk-access"],
            "contributions": [
                { "verb": "vm", "path": "create" },
                { "verb": "vm", "path": "show" },
                { "verb": "vm", "path": "list" },
                { "verb": "vm", "path": "delete" },
                { "verb": "disk", "path": "create" },
                { "verb": "disk", "path": "list" },
                { "verb": "disk-access", "path": "show" },
            ],
        },
    },
}
```

- `contributions` is the authoritative per-leaf list. The dispatcher builds an in-memory `{(verb, path) → packageId}` index from this at startup.
- `namespaces` is derived (distinct `verb` values across contributions). Persisted as a convenience for fast `extension list --by-verb` rendering. Kept in sync on write.
    - **Ordering contract.** `namespaces` preserves the order of _first appearance_ in `contributions` (case-insensitive distinct, first-seen casing wins). Hand-editing `contributions` may temporarily desynchronize the order until the next `Save()`, which re-derives the field. Tools that compare two registries should treat `namespaces` as a derived field and compare on `contributions` instead.
- `version` / `pinned` / `source` / `installedAt` are per-extension, not per-leaf.
- The reader is tolerant of v1 entries: a v1 entry with `verb: "<verb>"` and no `contributions` array reads as a single namespace-wide contribution `[{verb, path: ""}]`. No on-disk rewrite is forced; the next `add`/`update`/`remove` write triggers a transparent schema-2 upgrade for the touched entry.
- Built-in extensions (`extension`, `login`, `config`) are not in this file — they live in the in-memory seed (§4.6).

**Future-proofing commitments.** These are cheap to honor now and keep the vNext explicit-override path additive rather than a schema break:

1. **Per-leaf ownership is a stack of contributors, top-wins.** Each `(verb, path)` resolves to an ordered list; dispatch routes to the top. v1 enforces depth ≤ 1 via install-time rejection (§3.2), so callers always observe stacks of length 1. vNext `--override` pushes; `extension remove` pops, which naturally restores the contributor underneath. Built-ins are seeded as the bottom entry of their leaves' stacks at process start (§4.6); the seed is not persisted to `registry.json`. Collision-detection and dispatch code never branch on builtin-ness, only on "is the stack non-empty."
2. **Error responses enumerate per-leaf conflicts as an array.** `LeafConflict` `details` carries every colliding `(verb, path)` from a single `add`. The `code` field is the contract (§3.4); `message` and field names inside `details` are free to evolve. Tests MUST assert on `code`, never on `message`.
3. **PowerShell parity is cmdlet-shaped, not leaf-shaped.** The Azure PS modules expose flat cmdlet mappings (e.g. `Get-AzResourceGroup`) rather than `(verb, path)` leaves. The CLI ↔ PS bridge runs at the cmdlet-to-command-metadata layer, so PS does not consume `Contributions` directly and is unaffected by stack-vs-pointer changes to the registry shape.

### 4.2 Where contributions are declared

**Source of truth: the `s_cliTable` keys themselves, in full-path form.**

Each leaf's `s_cliTable` key encodes its full path including the namespace token:

```csharp
private static readonly Dictionary<string, CommandMetadata> s_cliTable = new()
{
    ["vm create"]   = s_newVMOrchestrator_CLI,
    ["vm show"]     = s_getVM_CLI,
    ["vm list"]     = s_listVM_CLI,
    ["vm delete"]   = s_deleteVM_CLI,
    ["disk create"] = s_newDisk_CLI,        // same extension, second namespace
    ["disk list"]   = s_listDisk_CLI,
};
```

`[ExtensionVerb]` is **repeatable** and advisory: it lists the namespaces an extension wants advertised in the build-time hint catalog. It has no runtime role.

```csharp
[ExtensionVerb("vm")]
[ExtensionVerb("disk")]
public class ExtensionIndex : IExtensionIndex { ... }
```

**Build-time validation** (`ExtensionHints.targets`): every `s_cliTable` key's first token must match one of the extension's `[ExtensionVerb]` declarations. Mismatches fail the build with a clear error pointing at the offending key. Keeps `[ExtensionVerb]` declarations and table contents in lockstep so the hint catalog is always complete.

### 4.3 Registration on install

`extension add` is multi-step and transactional:

1. Resolve package from feed (or accept `--path` for a local nupkg).
2. Extract DLLs to `~/.azclips/extensions/<pkg>/<version>/`.
3. Read `[ExtensionVerb]` declarations and `s_cliTable` keys from the candidate DLLs in a throwaway ALC. No extension code executes.
4. Validate: compat metadata against current `core` (§6); per-leaf `(verb, path)` collisions enumerated against the discovery-backed registry (Bundled + Installed).
5. Update `registry.json` transactionally (write-with-timestamp-guard, §4.7).
6. On failure at any step: roll back partial state (delete extraction dir, restore registry).

`extension remove`: validate name → delete dir(s) → remove registry entry → done.

### 4.4 Dispatch flow

```
az vm create --name foo
       │
       ▼
  args[0..n] = longest-prefix match against LeafIndex (verb, path) keys
       │  ↓ ("vm", "create")  →  "Microsoft.Azure.Azclips.Extensions.Compute" v1.1.0
       ▼
  CompatibilityGuard.Assert(asm)         ← warn-and-skip on failure (§6)
       │
       ▼
  AssemblyLoader.LoadOrCache(pkg, version)  ← ALC cache from §4.5
       │
       ▼
  IExtensionIndex.GetCommandMetadata("vm create", CLI)
       │
       ▼
  IProcessor.Init(ctx) → ProcessAsync()
```

- **Longest-prefix matching.** Residual tokens (past the matched prefix) bind to positional arguments.
- **Lazy dispatch, eager help.** Dispatch loads only the extension owning the matched leaf. Help (when implemented) loads every extension whose `namespaces` array contains the requested verb so it can aggregate leaves across contributors. Both paths reuse the ALC cache.

#### 4.4.1 Positional argument binding via `ParameterAliases`

Residual (unflagged) tokens are collected under an empty-string key by `ExtensionParser` regardless of whether a command wants them. A leaf only binds them if its `CommandMetadata.ParameterAliases` declares indexed slots: `[""]` for position 0, `["1"]` for position 1, `["2"]` for position 2, and so on. This is opt-in per command and CLI-only (PowerShell cmdlets already get native positional binding from the host).

```csharp
// `config get <key>` / `config delete <key>` — one positional slot.
ParameterAliases: new Dictionary<string, string> { [""] = "key" }

// `config set <key> <value>` — two positional slots, bound in order.
ParameterAliases: new Dictionary<string, string> { [""] = "key", ["1"] = "value" }
```

With the above, `az config set core.output.color always` binds `Key = "core.output.color"`, `Value = "always"`, and `az config set --key core.output.color --value always` still works unchanged.

`CLIArgumentParsingProvider` (`src/CLI/ArgumentParsing/CLIArgumentParsingProvider.cs`) enforces two invariants for any command that opts in:

- **Unrecognized positionals are rejected.** A token with no declared slot for its position throws instead of being silently dropped.
- **Positional/flag conflicts are rejected.** Supplying the same parameter both positionally and via its flag (e.g. `config set core.output.color --value always`, then also `--key foo`) throws instead of silently picking one based on dictionary iteration order.

`profile show/set/remove` (Accounts) and `extension show/remove/update/pin/unpin` (Extension) also declare `[""]` and get the same two invariants for free.

### 4.5 ALC caching

`ConcurrentDictionary<(string pkg, string version), Lazy<ExtensionLoadContext>>` in `AssemblyLoadingService`:

- Cache lifetime = process lifetime. No eviction. No attempt to unload old ALCs.
- One-shot CLI invocations are unaffected (process dies between invocations).
- Long-running hosts (TUI, test harness) get full benefit.
- After `update` or `remove`, the long-running host should restart. The cache key includes version, so any new version installed simply gets a new ALC on its first use.
- `--no-cache` test hook for benchmarking.

### 4.6 Bundled tier at dispatch

Bundled extensions are **not** seeded into the `LeafIndex` from a hardcoded map. They participate in dispatch through the same discovery pass as everything else:

- `BundledExtensionLoader` enumerates the Bundled root (`<install>/Extensions/` or the local build artifacts dir) alongside `InstalledExtensionLoader` enumerating `~/.azclips/extensions/`.
- Every discovered extension — regardless of tier — contributes its `s_cliTable` leaves to the `LeafIndex`.
- Duplicates are a hard error at index-build time (`LeafIndex.Build` throws `RegistryInvalid`) — there is no tier tiebreak. The install-time `LeafConflict` guard (§3.2 / §4.3 step 4) is what prevents this at write time.
- The write boundary ([extension-framework.md §2.3](./extension-framework.md#23-extension-tiers)) — not any runtime privilege — is what makes Bundled extensions non-removable.

### 4.7 Registry hardening

- **Discovery-first installed state.** Runtime routing and extension listing are driven by discovery of on-disk payloads; `registry.json` is treated as a preferences overlay only. Missing payloads therefore disappear naturally from live routing without a separate reconciliation pass.
- **Concurrency guard.** `FileExtensionRegistryStore` captures the file's last-write timestamp at `Load()` and checks it at `Save()`. A mismatch throws `AzclipsErrorException.RegistryConcurrency` so two interleaved CLI invocations can't silently overwrite each other.
- **Invariant check.** `FileExtensionRegistryStore.Load()` enforces the leaf-uniqueness invariant by scanning every entry's `Contributions`; duplicates across packages throw `AzclipsErrorException.RegistryInvalid` with both offending package ids. Catches hand-edited corruption before it reaches the `LeafIndex`.

### 4.8 Telemetry

`Add` / `Update` / `Remove` activities are tagged with five OTEL-shaped attributes: `extension.package_id`, `extension.version`, `extension.source` (feed URL or `"path"` sentinel), `extension.namespace_count`, `extension.contribution_count`. Tags are set as soon as each value becomes known, so failed installs (e.g. mid-iteration `LeafConflict`) still carry whatever was determined before the throw — including the _attempted_ contribution count read from `s_cliTable`. Sideloaded (`--path`) installs are distinguishable from feed installs in the data. **PII note:** package id + version are PII-safe for first-party feeds; revisit before enabling third-party feeds.

---

## 5. Conflict & validation

Three checkpoints, increasing in cost from compile-time to dispatch-time.

### 5.1 Build time (monorepo)

`ExtensionHints.targets` scans `src/Extensions/**` for `[ExtensionVerb]` declarations and `s_cliTable` literal entries. Two failure modes:

1. **Cross-project leaf collision.** Two projects emit the same `(verb, path)`. Build fails: `Verb 'vm', path 'create' is registered by both 'Compute' and 'OtherPkg' (locations: …)`.
2. **Undeclared verb in `s_cliTable`.** A leaf key's first token isn't covered by any `[ExtensionVerb]` declaration in the same project. Build fails with the offending key and the project's declared namespaces.

Catches conflicts at PR time, before any artifact ships.

### 5.2 Install time (third party)

`extension add` step 4 (§4.3):

1. **Compat metadata check** (§6). Hard fail if the candidate's `[AzclipsCompatibility]` range excludes the current `core`.
2. **Per-leaf collisions against the discovery-backed registry.** All collisions across the candidate's full leaf set are accumulated and surfaced as a single `LeafConflict` (§3.2). Bundled and Installed contributions are checked on the same terms — no tier bias. No `--force` override.

Namespace-level conflicts no longer exist — multiple extensions declaring the same `[ExtensionVerb]` is fine and expected.

### 5.3 Runtime (load)

`CompatibilityGuard.Assert(asm)` runs after the ALC loads the extension assembly, before any `IProcessor` resolves. On failure (compat range excludes current `core`):

- Log a warning identifying the extension and the range mismatch.
- Drop that extension's contributions from the live `LeafIndex` for this invocation.
- Continue dispatch. Other extensions — including other contributors to the same verb — keep working.

Per-extension, not per-verb. Mirrors az-cli's load loop verbatim.

---

## 6. Compatibility metadata

Extensions declare a `core` version range; the host enforces it at install (hard fail) and load (warn-and-skip). Models az-cli's `ext_compat_with_cli` / `check_version_compatibility`.

### 6.1 Declaration

Assembly-level attribute defined in `Core.Abstractions`:

```csharp
[assembly: AzclipsCompatibility(MinCoreVersion = "1.0.0", MaxCoreVersion = "2.0.0")]
```

Both fields optional. Missing attribute = assumed compatible (matches az-cli and Az PS — strict mode would block every undecorated dev/internal extension for negligible safety gain). SemVer with pre-release ordering via NuGet's `SemanticVersion`.

### 6.2 Guard

`Core.Runtime.CompatibilityGuard`:

- A pure `Check(min, max, current)` matrix and a `CheckAssembly(extensionDir, current)` reader that uses `MetadataLoadContext` (no execution, mirrors `ExtensionVerbReader`).
- Tolerant defaults: missing attribute → compatible, unparseable bound → ignored with advisory, unparseable current → compatible (treated as host bug).

Called from:

- **CLI install.** `Add.cs` invokes the guard before extracting; failure throws `CompatibilityViolation` (exit 7) and rollback handles disk cleanup.
- **CLI dispatch / PS cmdlet entry.** Wired in vNext (see §8); dormant in v1.

### 6.3 Failure modes

- **Install** (`extension add`): hard error, abort install, no disk write. Exit code 7, `CompatibilityViolation` symbolic code.
- **Runtime** (load): per-extension warn-and-skip. Contributions dropped from live `LeafIndex`. Other extensions keep working (§5.3). Wired in vNext.

---

## 7. Discovery / hint catalog

v1 ships an embedded `extension-hints.json`. Schema is feed-ready so the fetch half can be added in vNext without changing the file format or the resolution path.

### 7.1 Schema

```jsonc
{
    "schemaVersion": 1,
    "generatedAt": "2026-05-28T10:00:00Z",
    "sourceHostVersion": "1.2.0",
    "leaves": {
        "vm": { "packageId": "Microsoft.Azure.Azclips.Extensions.Compute" },
        "vm create": {
            "packageId": "Microsoft.Azure.Azclips.Extensions.Compute",
        },
        "vm list": {
            "packageId": "Microsoft.Azure.Azclips.Extensions.Compute",
        },
        "disk": { "packageId": "Microsoft.Azure.Azclips.Extensions.Compute" },
        "disk list": {
            "packageId": "Microsoft.Azure.Azclips.Extensions.Compute",
        },
        "group": {
            "packageId": "Microsoft.Azure.Azclips.Extensions.Resources",
        },
        "group create": {
            "packageId": "Microsoft.Azure.Azclips.Extensions.Resources",
        },
    },
}
```

Per-leaf entries are objects (not bare strings) so optional fields can be added later without bumping `schemaVersion` (additive only — the reader ignores unknown properties):

```jsonc
"vm contoso-magic": {
    "packageId": "Contoso.VmPreviewExt",
    "minCoreVersion": "1.5.0",
    "maxCoreVersion": null
}
```

v1 first-party embed populates only `packageId`. Compat fields are reserved for the feed half.

### 7.2 Generation

`ExtensionHints.targets` scans every project in `src/Extensions/**`:

- Bare verbs from `[ExtensionVerb]` declarations.
- Full-path leaves from `s_cliTable` literal keys.

Cross-project collisions fail the build (§5.1). Output is embedded as a resource in `Core.Cli` and consumed by `ExtensionHintTable`.

### 7.3 Resolution

On dispatch miss, `ExtensionHintTable` walks the longest-prefix path against `extension-hints.json`:

1. Exact `(verb, path)` match → "install package X" (§3.3 case 1).
2. Verb match, no path match → "package X provides verb '<verb>' but no '<path>' leaf" (§3.3 case 2).
3. No verb match → fall through to generic "try `extension search`" (§3.3 case 3).

---

## 8. vNext / deferred

Tracked, not v1.

- **Feed-fetch path for `extension-hints.json`.** Additive — no host change beyond fetch + cache read. Same schema, fetched on a TTL (lean toward 24h), written to `~/.azclips/cache/extension-hints.json`. Resolution becomes cache-first if fresh, embedded fallback if cache is missing or stale. Stale fallback is silent — no "hints may be outdated" warning. Compat-aware filtering: once feed entries populate `minCoreVersion`/`maxCoreVersion`, the host can pre-filter suggestions whose package would fail the install-time compat check.
- **Feed endpoint location.** Per-`core`-release GitHub asset, Azclips-owned blob, or NuGet feed metadata. Pick when wiring up the fetch.
- **`extension list --available`.** Comes for free when the feed exists; out of v1 scope until then.
- **Host core-version source-of-truth.** Today `Add.cs` and any future runtime-side caller pass the literal `"1.0.0"` to `CompatibilityGuard`. Establish a single host version (az-cli's `__version__` is the model) and thread it through. Until then `[AzclipsCompatibility]` ranges are checked against the literal — fine for v1 since no breaking host change has shipped, but the literal must move when one does.
- **Runtime warn-and-skip in the dispatch path** (§5.3). Install-time hard fail catches the common case (operator-driven `extension add`); a runtime check protects against side-loaded packages or post-install host upgrades. Cleanest hook is a registry-side filter applied at `LeafIndex.Build` time so incompatible packages disappear from live routing with a stderr warning. Deferred only because it inherits the host-version source-of-truth question above.
- **`HostExtensionRequirements` (inverse direction).** `Dictionary<string, VersionRange>` in `Core.Runtime` paralleling az-cli's `EXTENSION_VERSION_REQUIREMENTS`. Checked inside `CompatibilityGuard` after the extension's own range passes. Populated by the host team in the same PR that introduces a breaking change against a specific extension.
- **`AZCLIPS_DISABLE_COMPAT_CHECK` env var.** Neither az-cli nor Az PS has one. Runtime warn-and-skip is already forgiving. Additive in ~3 lines when needed.
- **`AzclipsCmdletBase : PSCmdlet`** so the PS surface gets `CompatibilityGuard.CheckAssembly` for free in `BeginProcessing`, instead of duplicating the guard call across the ~12 cmdlet entry points across four modules. Skipped in v1 because the base type doesn't exist yet and rebasing every cmdlet is its own refactor.
- **Bounded LRU on the ALC cache** if memory matters (§4.5). Worst case ~100MB for 20 extensions today, acceptable for the TUI. Revisit only if profiling demands it.
- **Multi-version retention.** `versions: [...]` + `activeVersion`, additive schema change. Strict single-version is the v1 stance; matches az-cli.
- **Latency / cache guardrails.** (a) If needed, add caching around discovery metadata collection on network home dirs (SMB/NFS/OneDrive); local-SSD impact today is negligible. (b) BenchmarkDotNet target in `tests/Benchmark/` asserting a leaf-miss hot path stays under a p99 budget (~300ms), plus an assembly-load-counter contract test asserting the dispatcher never spins up an ALC on miss. Insurance against drift toward az-cli's load-everything-on-CNF behavior.
- **Explicit `--override` for `extension add`** (informed by az-cli's `overwrite_order_in_cli.md`). v1 rejects all leaf collisions at install time, no exceptions. vNext relaxes this with an opt-in `--override "verb path[, ...]"` flag that lets a new extension take ownership of specific leaves while preserving the **stack** model committed in §4.1: the displaced contributor is pushed down (not removed), `extension list --by-verb` annotates the override, and `extension remove` of the top-of-stack contributor restores the next one underneath. Built-in displacement uses the same mechanism. This is a strict improvement over az-cli's silent alphabetical-name-wins overwrite (no audit trail, no restore-on-remove, see [`doc/overwrite_order_in_cli.md`](https://github.com/Azure/azure-cli/blob/main/doc/overwrite_order_in_cli.md)). Schema cost: additive only — a `displaced: [...]` field on the contribution object plus an `overriddenAt` timestamp.
- **Aggregated namespace help.** Whether `az vm --help` auto-merges leaves across contributors or each contributor advertises separately. The per-leaf ownership model guarantees the data shape; help branch decides the rendering.
- **Trust / signing / provenance.** Anything in `~/.azclips/extensions/` loads today. Shared namespaces mean unsigned third-party extensions can contribute leaves under first-party verbs (namespace squatting). Revisit before the first third-party extension ships.
- **Registry hygiene gaps.** Schema-versioning commitment ("how many versions back do we read indefinitely?"); roaming home directories; system-wide installs. Current posture: per-user, unsupported on roaming.
- **PowerShell parity definition.** "Same surface" vs. "same shape when both exist." Decide post-v1.
- **PowerShell CNF integration with the embedded hint catalog.** The hint catalog already works for the CLI dispatcher's verb-miss → extension-name suggestion path. Whether the PS surface should consume the same catalog from a CNF (Command-Not-Found) handler is open: PS modules are loaded by name (`Import-Module Az.Compute`) rather than dispatched by verb, so the unknown-verb shape doesn't map cleanly. Revisit when the PS team has a concrete scenario.
- **Empty-namespace declarations.** How does an extension declare a namespace it owns but ships zero leaves under (e.g. a future "Networking" extension wanting `network` for help-text purposes only)? Low priority; revisit if a use case appears.

---

## 9. PR review notes (delete before merge)

Fresh-eyes review pass against `origin/main`. Findings ordered by severity. Each entry is a discussion point for the PR thread, not a commitment to fix in this branch.

> **Status update (PR #253):** items marked **[fixed]** landed in this PR; items marked **[deferred]** are tracked for a follow-up. The full A1-A8 / B1-B11 matrix is summarised in the PR conversation.

### Critical

1. **Optimistic concurrency uses filesystem mtime.** `FileExtensionRegistryStore` captures `File.GetLastWriteTimeUtc()` at `Load()` and re-checks at `Save()`. Millisecond resolution + clock skew + same-tick writes leave a race window where two interleaved saves both think they were unopposed. Fix: replace mtime with a content hash or an integer `revision` field bumped on every write, or wrap load+save in an OS file lock with timeout. Spec §4.7 promises the guard; the implementation is best-effort. **[deferred — follow-up: add `revision` field to schema + CAS-on-revision; mtime stays as belt-and-suspenders.]**

### High

2. **`ExtensionHintTable.LoadFromEmbeddedResource` disk fallback is too broad.** The CI fix walks every `*.dll` in `AppContext.BaseDirectory` and `Assembly.LoadFrom`s each one until the embedded resource is found. That's an unbounded code-execution surface and slow on populated bin dirs. Tighten to `Path.Combine(BaseDirectory, "az.dll")` (the assembly the build target actually embeds into), with the existing in-memory scan as the fast path. Today's broad probe was a thirty-second fix for the CI flake; replace with the targeted version before merge. **[fixed]**

3. **Reflection paths swallow malformed input.** `LeafReader` parses an enum without a guard against unknown values; `CompatibilityGuard` treats unparseable `MinCoreVersion` / `MaxCoreVersion` as "no bound" with only an inline advisory. A typo in a manifest (`"main"`, `"1.0.0.0"`, `"latest"`) currently looks indistinguishable from "compatible." Surface unparseable bounds as a warning at minimum, ideally fail closed unless an explicit "I really mean unbounded" sentinel is used. **[deferred]**

### Medium

4. **TOCTOU between compatibility check and registry persistence in `Add.cs`.** `CompatibilityGuard.CheckAssembly(installPath, ...)` runs ~40 lines before the registry write. Between those points, another process can mutate `installPath`. Fix: move the compat check next to the write, or hold the registry concurrency guard across both. **[deferred]**

5. **`NormalizeEntry` skips contributions validation when a v1 entry already has a `Contributions` array.** Hand-edited or partially upgraded registry files can carry duplicates / null verbs / malformed paths past `Load()`. Run `ValidateNoDuplicateContributions` per-entry during normalization, not only at the cross-entry level. **[fixed — `ValidateNoDuplicateContributions` now flags intra-entry duplicates as well as cross-entry.]**

6. **Whitespace-only paths bypass key collapse.** `string.IsNullOrEmpty(c.Path) ? c.Verb : $"{c.Verb} {c.Path}"` treats `" "` as a non-empty path, producing `"vm "` keys that collide with neither `"vm"` nor `"vm create"`. Trim on load; reject or coerce to `string.Empty`. **[fixed — `NormalizeEntry` now trims `Verb`/`Path` and coerces whitespace-only verbs to `null` and whitespace-only paths to empty.]**

7. **`ExtensionHints.targets` regex is fragile.** `s_cliTable\s*=\s*new[^{}]*\{(?<body>[^{}]*)\}` breaks on nested braces (collection initializers with object initializers, lambdas, string interpolation, `//` comments containing `{`). Today's first-party tables happen to be flat, but a contributor adding a single nested `new()` silently produces an incomplete hint catalog with no error. Either codify the constraint as a build-time assertion ("no nested braces inside `s_cliTable`") or replace the regex with a Roslyn syntax walk. **[deferred]**

8. **No concurrency / corruption tests.** Test suite covers single-process happy paths and cross-entry duplicate detection. Missing: simultaneous writes from two processes, mtime going backward (clock skew, NTP, restored backup), temp-file cleanup when the rename step fails, and recovery from a half-written `registry.json`. These are the scenarios the §4.7 hardening was specifically designed for. **[deferred]**

### Low

9. **`ExtensionResolver.TryResolve` is O(n) per call** with no caching. Negligible at today's installed-extension counts; flag for revisit if anyone actually ships dozens of extensions in a single user profile.

10. **`SplitLeafKey` accepts garbage.** `""`, `"   "`, leading/trailing whitespace produce `("", "")` or surprising splits. Add input guards or trim+revalidate. **[fixed — already throws via `ArgumentException.ThrowIfNullOrWhiteSpace` and trims; verified no further hardening required.]**

11. **Namespace ordering contract is implicit.** §4.1 says "kept in sync on write" but doesn't pin order semantics. Implementation preserves first-seen casing from `Contributions`. Either document the contract in the spec ("Namespaces order = first appearance in Contributions; reordering Contributions may diverge until next save") or sort canonically (case-insensitive ordinal) for determinism across writers. **[fixed — see §4.1 contract note.]**

### Nits

- `Remove.cs` doesn't re-validate the registry between match and save; idempotency under racing removes is not documented.
- `Add.cs` doesn't re-read the registry after the compat check, so a vanishingly rare race could install two extensions both "valid" against snapshots taken seconds apart.
- The MSBuild task silently skips unreadable `.csproj` files. Should log a warning so misconfigured projects don't quietly drop from the hint catalog.
