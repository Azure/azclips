# Design Spec: Extension Management

> **Status (2026-05-28).** Branch `feature-extension-management-shape-b`. Shape B (shared verb namespaces, per-leaf ownership) is the v1 model — confirmed by the 2026-05-28 PM meeting. Phases 1–9 complete; phase 10 (polish + `[CirrusCompatibility]` + hint-schema bump) in progress.
>
> **Compat posture.** Source-compat with az-cli is the baseline. Verbs, primary flags, and `--output json` shapes carry over; secondary flags and internals are free to improve. A migration tool is acknowledged as necessary.
>
> **Related.** [extension-framework.md](./extension-framework.md) covers extension loading, ALC isolation, and the processor contract. This doc covers the layer _above_ that: how extensions are registered, discovered, routed to, and managed over their lifecycle.

---

## 1. Decisions & rationale (Q&A)

Every design question raised on this branch, with its current resolution. Questions are grouped by area. Skip to §2 if you want the spec body; come back here to look up "why did we decide X."

### 1.1 Foundational posture

- **Q0 (resolved, 2026-05-28 PM meeting): compat posture vs. az-cli.** Source-compat, not strict drop-in, not greenfield. Verbs, primary flags, and `--output json` shapes carry over; secondary flags, error structure, and internals are free to improve. A migration tool is acknowledged as necessary. Bias toward preserving the API of heavily-used modules (`vm`, `network`, etc.). Reversibility is low — user scripts will form around whatever ships — so the posture is explicit rather than emergent.

### 1.2 Verb declaration & dispatch model

- **Q1 (resolved): how does an extension declare its verb?** `[ExtensionVerb("…")]` attribute on the `IExtensionIndex` implementation. Colocated with the index, no separate manifest, readable by the installer in a throwaway ALC without instantiating anything. Under Shape B the attribute is **repeatable** (`AllowMultiple = true`) and purely advisory — leaves declare their own verb in their `s_cliTable` key.
- **Q4 (resolved): tree vs. flat command keys?** Flat `Dictionary<string, CommandMetadata>` with hierarchy encoded by spaces in the key (e.g. `"vm create"`). Zero migration cost, parser already handles multi-token names, tree representation can come later when the help branch needs it.

### 1.3 Namespace ownership (Shape A → Shape B)

- **Q-shape (resolved, 2026-05-28 PM meeting): Shape B (many-to-many).** Multiple extensions can contribute under the same top-level verb. Ownership is per-leaf `(verb, path)`, not per-verb. Matches az-cli's monorepo. Rationale captured in the room: partner teams work independently under shared verbs; users install only what they need; preview commands can ship as separate extensions; resource families span multiple extensions.
- **Q13 (resolved, see §1.5): per-extension, not per-verb.** Compat failure for one contributor under a shared verb does not poison the verb. Other contributors keep loading.
- **Q14 (resolved): `extension list --by-verb` is verb-headed.** Contributors grouped under each verb so "who contributes to this verb" is one line of scanning. JSON output is `{verb: [{packageId, version, ...}]}`. Tracked as Phase 10 item 31.
- **Q15 (deferred to help branch): aggregated namespace help.** Whether `azv2 vm --help` auto-merges leaves across contributors or each contributor advertises separately. Shape B guarantees the data shape; the help branch decides the rendering.

### 1.4 Registry & storage

- **Q2 (resolved): registry location.** `~/.cirrus/extensions/registry.json`, user-scoped, colocated with extension install dirs. Schema in §5.1.
- **Q3 (resolved): true core of Cirrus.** `extension`, `login`, `config` only. Everything else (including `vm` and `group`) is installable. Seeding mechanism: hardcoded seed dict merged with on-disk registry at startup (option A; options B and C — disk-shipped registry, install-on-first-boot — rejected).
- **Q5 (resolved): pinning UX.** Both: primary `extension pin/unpin` commands and a `--pin` flag on `extension add`. Pinning blocks `update` unless `--force` is passed. Pinning also blocks `extension add` of a different version of the same package unless `--force`; on `--force` the pin is carried forward onto the new entry (Phase 10 item 41).
- **Q6 (resolved): old-version retention.** Strict single-version. `update` overwrites in place; only one version on disk at any time. Matches az-cli. Multi-version is an additive schema change later if needed.
- **Q7 (resolved): conflict & scoping edge cases.** User-scoped only (no machine-wide install path). No `--force` for verb conflicts on install — `remove` then `add` to swap owners. `remove <name>` purges the single install. `update` on pinned fails with a clear message pointing at `unpin` or `--force`. Built-ins cannot be removed.
- **Q8 (resolved): ALC cache strategy.** Process-lifetime `ConcurrentDictionary<(string pkg, string version), Lazy<ExtensionLoadContext>>` in `AssemblyLoadingService`. No eviction. Long-running hosts (TUI, test harness) benefit; one-shot CLI invocations are unaffected. `--no-cache` test hook for benchmarking. Bounded LRU is a vNext follow-up if memory becomes a concern.

### 1.5 Compatibility metadata

- **Q-compat (resolved, 2026-05-28 PM meeting): ship min/max in v1.** Extensions declare a `core` version range. Enforcement details Q16–Q22 resolved by mirroring az-cli's `ext_compat_with_cli` / `check_version_compatibility`.
- **Q16 (resolved): source of truth = assembly attribute.** `[CirrusCompatibility(MinCoreVersion, MaxCoreVersion)]` on the extension DLL. One artifact, both CLI and PS read it identically via reflection. No manifest file (drift risk for no benefit). Mirrors az-cli's wheel `METADATA` keys.
- **Q17 (resolved): tolerant on missing declaration.** Missing attribute = assumed compatible. Matches az-cli and Az PS. Strict mode would block every undecorated dev/internal extension for negligible safety gain.
- **Q18 (resolved): both install-time and runtime, different failure modes.**
    - **Install** (`extension add`): hard error, abort install, no disk write. Mirrors az-cli `check_version_compatibility` at the top of `add_extension`.
    - **Runtime** (load): per-extension warn-and-skip. The dispatcher logs a warning, drops that extension's contributions from the live `LeafIndex` for this invocation, continues. Other contributors to the same verb keep working. Mirrors the az-cli load loop verbatim.
- **Q19 (resolved): inverse direction shipped in v1, dictionary starts empty.** A `Dictionary<string, VersionRange> HostExtensionRequirements` in `Core.Runtime` parallels az-cli's `EXTENSION_VERSION_REQUIREMENTS`. Checked inside `CompatibilityGuard.Assert` after the extension's own range passes. Host team adds an entry in the same PR that introduces a breaking change for a specific extension.
- **Q20 (resolved): SemVer with pre-release ordering.** Use NuGet's `NuGetVersion` (already a dependency). `1.5.0-rc1` orders correctly relative to `1.5.0`. az-cli uses PEP 440; same intent.
- **Q21 (deferred to vNext): bypass env var `CIRRUS_DISABLE_COMPAT_CHECK=1`.** Neither az-cli nor Az PS has one. Runtime warn-and-skip is already forgiving. Additive in ~3 lines when needed.
- **Q22 (resolved): per-extension, not per-verb on compat failure under Shape B.** Same answer as Q13. A compat failure removes one extension's contributions from the `LeafIndex`; other contributors stay routable.

### 1.6 Discovery / hint catalog

- **Q-hints (resolved, 2026-05-28 PM meeting): blended embed + feed.** v1 cut: embed only, but schema and resolution path are feed-ready so the fetch half is purely additive.
- **Q9 (resolved as Q-hints, v1 = embed): feed-served verb index.** Originally tracked as non-blocking; now resolved as part of the v1 embed-only / vNext fetch plan.
- **Q10 (resolved): build error when an `s_cliTable` key's first token isn't covered by an `[ExtensionVerb]` declaration.** The MSBuild target fails the build with a clear message. Cost: developers must keep `[ExtensionVerb]` and table contents in sync. Benefit: the build-time hint table is guaranteed complete.
- **Q11 (open, low priority): how does an extension declare a namespace it owns but has no leaves under?** E.g. a future "Networking" extension that wants `network` for help-text purposes only. No real use case today; `[ExtensionVerb]` declarations without matching leaves currently have no effect.
- **Q12 (deferred to help branch): aggregated namespace help.** Same as Q15.
- **Q23 (deferred): feed endpoint location.** Don't pick now. Candidates: per-`core`-release GitHub asset, Cirrus-owned blob, NuGet feed metadata. Schema being feed-ready is enough.
- **Q24 (deferred, lean 24h): TTL.** az-cli uses 10 days because their release cadence is ~2 weeks. Cirrus ships faster. Not worth deciding before the fetch exists.
- **Q25 (resolved): silent embedded fallback when feed unreachable AND embedded stale.** No "your hints might be outdated" warning — it's noise the user can't act on. If the suggested install fails because the hint was wrong, the install error speaks for itself. Mirrors az-cli.
- **Q26 (resolved): feed carries compat metadata.** Each leaf entry's schema becomes `{packageId, minCoreVersion?, maxCoreVersion?}`. v1 embed populates only `packageId`; compat fields are reserved in the schema for the feed half.
- **Q27 (deferred, out of v1 scope): `extension list --available`.** Same source-of-truth as the hint catalog, so it comes "for free" when the feed exists. Until then, listing the embedded snapshot as "available" is misleading.

### 1.7 Deferred sections (not yet discussed)

- **Registry hygiene** (stale entries, schema versioning commitment, hand-edits, roaming profiles, system-wide installs) — current postures stand. Phase 8.5 covers most of stale-entry handling; hand-edits surface as `RegistryInvalid`. Schema-versioning commitment ("how many versions back do we commit to reading?") still open.
- **Trust and provenance** — explicitly deferred. The interaction with Shape B matters: shared namespaces mean unsigned third-party extensions can contribute leaves under first-party verbs (namespace squatting). Revisit before the first third-party extension ships, not before v1.
- **CLI/PS surface polish** (verb-accepting `add --name`, local nupkg hint, output format default, PS parity definition) — Phase 10 items 34 and 35 cover two of these; the rest decide post-v1.

---

## 2. Goals & non-goals

### Goals

- Make installed extensions first-class: usable without code changes to the host.
- Replace the hardcoded `CommandGroupIndex` with a persistent registry populated at install/remove time.
- Add the missing CRUD lifecycle operations: `list`, `remove`, `update`, `show` (alongside `add`).
- Lift the parser's depth-1 limitation so extensions can ship multi-level command trees (`azv2 vm boot-diagnostics enable`).
- Cache ALCs across invocations so extension load isn't paid on every command.
- Allow multiple extensions to contribute under the same top-level verb (Shape B), with per-leaf ownership.
- Ship host↔extension compatibility metadata in v1 so version mismatches surface as actionable errors rather than mysterious failures.

### Non-goals (this branch)

- **Help system.** `--help` rendering, group descriptions, MAML/online help — tracked separately on the help branch.
- **PowerShell side, deep.** Cross-client compat metadata (§7) is shared; PS module routing has its own load/isolation model. See [extension-framework.md](./extension-framework.md) §4.4.
- **NativeAOT.** Conflicts with dynamic loading regardless of routing topology.
- **Marketplace / catalog UX.** Discovery beyond the embedded hint catalog is out of scope (vNext).
- **Trust / signing / provenance.** Anything in `~/.cirrus/extensions/` loads. Deferred (§1.7).
- **Feed-fetch path for hint catalog.** v1 is embed-only; the feed half is vNext (§8).

---

## 3. Model

> **A top-level verb is a shared namespace. Any number of extensions can contribute leaves under it. Each leaf — the full `(verb, path)` tuple — is owned by exactly one extension.**

Consequences:

- Routing is `{(verb, path) → packageId}`. Lookup is O(1); the key is wider than Shape A's `{verb → packageId}` but the structure is identical.
- There is no "namespace owner" role. Every contributor is equal. Namespaces exist by virtue of being contributed to.
- A single extension may contribute to multiple namespaces — e.g. one `Compute` package contributing under `vm`, `disk`, `disk-access`. This is the common case for Azure-style resource families.
- `azv2 vm --help` (when help lands) aggregates leaves across all extensions contributing to `vm`. The host can answer "who contributes to this namespace" without loading anything.

### Built-in trio

The host ships with exactly three built-in extensions, present in every install:

- **`extension`** — manages other extensions (the CRUD surface in §4).
- **`login`** — authentication and tenant/subscription context.
- **`config`** — user/host configuration.

Built-ins are namespaces in the same sense as installed extensions; the host is the sole contributor for each. Their verbs are **reserved** — `extension add` rejects any third-party contribution whose leaf has a built-in verb as its first token. Built-ins cannot be removed.

Everything else (including today's `group` / Resources and `vm` / Compute) is an installable extension. The host has no built-in Azure-resource verbs.

---

## 4. User-facing surface

### 4.1 Extension CRUD commands

All under `azv2 extension`. Kebab-case CLI convention.

| Command                                          | Behavior                                                                                                                                                                                                          |
| ------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `azv2 extension add <pkg>[@<version>] [--force]` | Install. Overwrites any prior version of the same package. `--pin` flag locks the version against `update`. `--force` is required to overwrite an existing pinned installation (the pin is then carried forward). |
| `azv2 extension list`                            | Show installed extensions: name, version, namespaces, install source, install date. `--by-verb` regroups output verb-headed (§4.5, Phase 10).                                                                     |
| `azv2 extension remove <name>`                   | Uninstall. Remove from registry, delete `~/.cirrus/extensions/<pkg>/`. Errors on built-in extensions.                                                                                                             |
| `azv2 extension update <name>[@<version>]`       | Install newer version, replacing the old one in place. Fails on pinned extensions unless `--force`.                                                                                                               |
| `azv2 extension pin <name>[@<version>]`          | Lock active version. With no version, pins the currently installed version.                                                                                                                                       |
| `azv2 extension unpin <name>`                    | Remove the pin.                                                                                                                                                                                                   |
| `azv2 extension show <name>`                     | Detail view: full path, version, namespaces, contributions, source feed, install date, compat range.                                                                                                              |

### 4.2 Conflict at install time

`extension add` validates the candidate's contributions against:

1. **Built-in verbs.** Any leaf whose first token is a built-in verb is rejected immediately.
2. **Compat metadata** (§7). Hard fail if the candidate's declared range excludes the current `core` version.
3. **Per-leaf collision.** For every `(verb, path)` tuple the candidate contributes, the registry is scanned for an existing owner. First conflict aborts the install:

    ```
    > azv2 extension add Contoso.Foo
    Error: Extension 'Contoso.Foo' wants to contribute leaf 'vm contoso-magic',
    but it is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'.

    Options:
      - Remove the existing extension first: azv2 extension remove Compute
      - Contact the extension author to choose a different leaf
    ```

There is no `--force` override on collision. To swap owners of a leaf: `remove` then `add`. The per-verb ownership gate that briefly existed in early Shape B code (rejecting any extension whose declared verb was already claimed) was removed 2026-05-28 — multiple extensions advertising `[ExtensionVerb("vm")]` is fine and expected.

### 4.3 Unknown-verb / unknown-leaf UX

When the dispatcher sees an unknown token, it consults the embedded hint catalog (§8) and falls through three cases:

1. **Exact `(verb, path)` match** in the catalog:

    ```
    > azv2 vm list
    Error: 'vm list' is provided by extension 'Microsoft.Azure.Cirrus.Extensions.Compute',
    which is not installed. Install it with:
      azv2 extension add --name Compute
    ```

2. **Verb match, no path match**: "package X provides verb '<verb>' but no '<path>' leaf — try `azv2 <verb> --help` after installing."

3. **No verb match**: fall through to the generic "try `extension search <verb>`" path. The AI fallback in `OrchestratorEngine` continues to handle free-form input as today.

### 4.4 Error surface

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
| 7    | compatibility violation (see §7)                     |
| 8    | registry concurrency (concurrent write detected)     |
| 9    | registry invalid (on-disk invariant violation)       |

**Structured output.** `extension` commands respect the host's existing `--output json` flag. On failure:

```jsonc
{
    "error": {
        "code": "LeafConflict",
        "message": "Extension 'Contoso.Foo' wants to contribute leaf 'vm contoso-magic', but it is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'.",
        "details": {
            "requestedPackage": "Contoso.Foo",
            "collisions": [
                {
                    "verb": "vm",
                    "path": "contoso-magic",
                    "existingPackage": "Microsoft.Azure.Cirrus.Extensions.Compute",
                },
            ],
        },
    },
}
```

- `code` is the **contract** for programmatic callers (`LeafConflict`, `ExtensionNotFound`, `PinViolation`, `FeedUnavailable`, `UsageError`, `CompatibilityViolation`, `RegistryConcurrency`, `RegistryInvalid`). Tests and downstream tooling MUST assert on `code`, never on `message` — the latter is free to evolve (e.g. to suggest `--override` once that vNext flag exists).
- `message` is human-readable and not contractual.
- `details` carries operation-specific context. For `LeafConflict`, the array enumerates every `(verb, path)` a single `add` collided on — even if v1 stops at the first one when generating the `message`, the array shape is the future-proof contract so scripted callers don't have to be retrained when vNext's `--override` flow lands. Field names within `details` (including the array's name) are not contractual.

### 4.5 `extension list` output

Default output groups by extension:

```
PackageId                                       Version   Namespaces             Pinned  InstalledAt
Microsoft.Azure.Cirrus.Extensions.Compute       1.1.0     vm, disk               no      2026-04-12
Microsoft.Azure.Cirrus.Extensions.Resources     1.0.0     group                  no      2026-04-12
```

`--by-verb` regroups verb-headed (Q14, Phase 10):

```
vm
  Microsoft.Azure.Cirrus.Extensions.Compute       1.1.0
  Contoso.VmPreviewExt                            0.3.0-preview
disk
  Microsoft.Azure.Cirrus.Extensions.Compute       1.1.0
group
  Microsoft.Azure.Cirrus.Extensions.Resources     1.0.0
```

JSON shapes are stable: default is `[{packageId, version, namespaces, ...}]`; `--by-verb` is `{verb: [{packageId, version, ...}]}`.

`extension list` also surfaces stale entries (Phase 8.5): the response carries separate `Live` and `Stale` arrays. Stale entries include the missing path and a reason so the user can decide whether to `remove` them.

---

## 5. Internals

### 5.1 Registry schema (v2)

A single JSON file at `~/.cirrus/extensions/registry.json`:

```jsonc
{
    "schemaVersion": 2,
    "extensions": {
        "Microsoft.Azure.Cirrus.Extensions.Compute": {
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

**Design points:**

- `contributions` is the authoritative per-leaf list. The dispatcher builds an in-memory `{(verb, path) → packageId}` index from this at startup.
- `namespaces` is derived (distinct `verb` values across contributions). Persisted as a convenience for fast `extension list --by-verb` rendering. Kept in sync on write.
- `version` / `pinned` / `source` / `installedAt` are per-extension, not per-leaf.
- `schemaVersion: 2` reader is tolerant of v1 entries: a v1 entry with `verb: "<verb>"` and no `contributions` array reads as a single namespace-wide contribution `[{verb, path: ""}]`. No on-disk rewrite forced; the next `add`/`update`/`remove` write triggers a transparent schema-2 upgrade for the touched entry.
- Built-in extensions (`extension`, `login`, `config`) are not in this file — they live in the in-memory seed (§5.6).

**Future-proofing commitments (locked-in for v1).** These are cheap to honor now and keep the vNext explicit-override path (§10, deferred) additive rather than a schema break:

1. **Per-leaf ownership is a stack of contributors, top-wins.** Each `(verb, path)` resolves to an ordered list; dispatch routes to the top. v1 enforces depth ≤ 1 via install-time rejection (§4.2), so callers always observe stacks of length 1. vNext `--override` pushes; `extension remove` pops, which naturally restores the contributor underneath — the property az-cli's overwrite model fundamentally lacks. Built-ins are seeded as the bottom entry of their leaves' stacks at process start (§5.6); the seed is not persisted to `registry.json`, which makes "built-in vs extension" a question of _where the entry came from_, not a flag on the entry. Collision-detection and dispatch code therefore never branch on builtin-ness, only on "is the stack non-empty."
2. **Error responses enumerate per-leaf conflicts as an array.** `LeafConflict` `details` carries every colliding `(verb, path)` from a single `add`, not just the first. The `code` field is the contract (§4.4); `message` and field names inside `details` are free to evolve (e.g. to suggest `--override` once that flag exists). Tests MUST assert on `code`, never on `message`.
3. **PowerShell parity is cmdlet-shaped, not leaf-shaped.** The Cirrus PS modules expose flat cmdlet mappings (e.g. `Get-CirrusResourceGroup`) rather than `(verb, path)` leaves. The CLI ↔ PS bridge runs at the cmdlet-to-command-metadata layer, so PS does not consume `Contributions` directly and is unaffected by stack-vs-pointer changes to the registry shape.

### 5.2 Where contributions are declared

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

**Build-time validation** (`ExtensionHints.targets`, Phase 9): every `s_cliTable` key's first token must match one of the extension's `[ExtensionVerb]` declarations. Mismatches fail the build with a clear error pointing at the offending key. Keeps `[ExtensionVerb]` declarations and table contents in lockstep so the hint catalog is always complete.

### 5.3 Registration on install

`extension add` is multi-step and transactional:

1. Resolve package from feed (or accept `--path` for a local nupkg).
2. Extract DLLs to `~/.cirrus/extensions/<pkg>/<version>/`.
3. Read `[ExtensionVerb]` declarations and `s_cliTable` keys from the candidate DLLs in a throwaway ALC. No extension code executes.
4. Validate: built-in verbs rejected; compat metadata against current `core` (§7); per-leaf `(verb, path)` collision against the registry.
5. Update `registry.json` transactionally (write-with-timestamp-guard, §5.7).
6. On failure at any step: roll back partial state (delete extraction dir, restore registry).

`extension remove`: validate name → delete dir(s) → remove registry entry → done.

### 5.4 Dispatch flow

```
azv2 vm create --name foo
       │
       ▼
  args[0..n] = longest-prefix match against LeafIndex (verb, path) keys
       │  ↓ ("vm", "create")  →  "Microsoft.Azure.Cirrus.Extensions.Compute" v1.1.0
       ▼
  CompatibilityGuard.Assert(asm)         ← warn-and-skip on failure (§7)
       │
       ▼
  AssemblyLoader.LoadOrCache(pkg, version)  ← ALC cache from §5.5
       │
       ▼
  IExtensionIndex.GetCommandMetadata("vm create", CLI)
       │
       ▼
  IProcessor.Init(ctx) → ProcessAsync()
```

- **Longest-prefix matching.** Residual tokens (past the matched prefix) bind to positional arguments. Closes Shape A's deferred item #17.
- **Lazy dispatch, eager help.** Dispatch loads only the extension owning the matched leaf. Help (when implemented) loads every extension whose `namespaces` array contains the requested verb so it can aggregate leaves across contributors. Both paths reuse the ALC cache.

### 5.5 ALC caching

`ConcurrentDictionary<(string pkg, string version), Lazy<ExtensionLoadContext>>` in `AssemblyLoadingService`:

- Cache lifetime = process lifetime. No eviction. No attempt to unload old ALCs.
- One-shot CLI invocations are unaffected (process dies between invocations).
- Long-running hosts (TUI, test harness) get full benefit.
- After `update` or `remove`, the long-running host should restart. The cache key includes version, so any new version installed simply gets a new ALC on its first use.
- `--no-cache` test hook for benchmarking.
- Bounded LRU is a vNext follow-up if memory becomes a concern (~100MB worst case for 20 extensions, acceptable for TUI).

### 5.6 Built-in seeding

A hardcoded seed dict in the host (replacing the old `CommandGroupIndex`), merged with the on-disk registry at startup:

- Built-in entries are never written to `registry.json`. They live only in memory.
- On startup, the dispatcher's `LeafIndex` is `built-in seed ∪ on-disk registry`.
- On any collision between a built-in and an installed extension, **built-in wins.** Defensive — the collision should be impossible because `extension add` rejects built-in-rooted contributions in step 4 of §5.3.

Alternatives rejected: shipping a `registry.json` with built-ins baked in (footgun — users might edit it), or treating built-ins as installed extensions that install on first boot (adds first-boot failure modes).

### 5.7 Registry hardening (Phase 8.5)

- **`RegistryReconciler`** compares `registry.json` against the install root on every dispatch. Entries whose `~/.cirrus/extensions/{packageId}/{version}/` folder is missing are dropped from the live view so the dispatcher never routes to a stale entry. Non-destructive — the file on disk is unchanged.
- **Concurrency guard.** `FileExtensionRegistryStore` captures the file's last-write timestamp at `Load()` and checks it at `Save()`. A mismatch throws `CirrusErrorException.RegistryConcurrency` so two interleaved CLI invocations can't silently overwrite each other.
- **Invariant check.** `FileExtensionRegistryStore.Load()` enforces the §6.2 leaf-uniqueness invariant by scanning every entry's `Contributions`; duplicates across packages throw `CirrusErrorException.RegistryInvalid` with both offending package ids. Catches hand-edited corruption before it reaches the `LeafIndex`.
- **Stale entries in `extension list`.** Separate `Live` and `Stale` arrays in the response (§4.5).

---

## 6. Conflict & validation

Three checkpoints, increasing in cost from compile-time to dispatch-time.

### 6.1 Build time (monorepo)

`ExtensionHints.targets` scans `src/Extensions/**` for `[ExtensionVerb]` declarations and `s_cliTable` literal entries. Two failure modes:

1. **Cross-project leaf collision.** Two projects emit the same `(verb, path)`. Build fails: `Verb 'vm', path 'create' is registered by both 'Compute' and 'OtherPkg' (locations: …)`.
2. **Undeclared verb in `s_cliTable`.** A leaf key's first token isn't covered by any `[ExtensionVerb]` declaration in the same project. Build fails with the offending key and the project's declared namespaces.

Catches conflicts at PR time, before any artifact ships.

### 6.2 Install time (third party)

`extension add` step 4 (§5.3):

1. **Built-in verb rejection.** Any leaf whose first token is a built-in verb (`extension`, `login`, `config`) is rejected immediately, even if no installed extension currently claims that root.
2. **Compat metadata check** (§7). Hard fail if the candidate's `[CirrusCompatibility]` range excludes the current `core`.
3. **Per-leaf collision against registry.** First conflict aborts the install. No `--force` override.

Namespace-level conflicts no longer exist — multiple extensions declaring the same `[ExtensionVerb]` is fine and expected.

### 6.3 Runtime (load)

`CompatibilityGuard.Assert(asm)` runs after the ALC loads the extension assembly, before any `IProcessor` resolves. On failure (compat range excludes current `core`):

- Log a warning identifying the extension and the range mismatch.
- Drop that extension's contributions from the live `LeafIndex` for this invocation.
- Continue dispatch. Other extensions — including other contributors to the same verb — keep working.

Per-extension, not per-verb. Mirrors az-cli's load loop verbatim.

---

## 7. Compatibility metadata

Extensions declare a `core` version range; the host enforces it at install (hard fail) and load (warn-and-skip). Models az-cli's `ext_compat_with_cli` / `check_version_compatibility` (see [azure-cli/src/azure-cli-core/azure/cli/core/extension/**init**.py](C:/Users/dlanguiller/source/repos/azure-cli/src/azure-cli-core/azure/cli/core/extension/__init__.py) and [azure-cli/src/azure-cli-core/azure/cli/core/**init**.py](C:/Users/dlanguiller/source/repos/azure-cli/src/azure-cli-core/azure/cli/core/__init__.py)).

### 7.1 Declaration

Assembly-level attribute defined in `Core.Abstractions`:

```csharp
[assembly: CirrusCompatibility(MinCoreVersion = "1.0.0", MaxCoreVersion = "2.0.0")]
```

Both fields optional. Missing attribute = assumed compatible (tolerant, Q17). SemVer with pre-release ordering via NuGet's `NuGetVersion` (Q20).

### 7.2 Guard

`Core.Runtime.CompatibilityGuard.Assert(Assembly asm)`:

1. Read `[CirrusCompatibility]` via reflection.
2. Compare current `core` version against the declared range. Fail if outside.
3. Look up `HostExtensionRequirements[packageId]` — the inverse direction dict, az-cli's `EXTENSION_VERSION_REQUIREMENTS` equivalent (Q19). Fail if installed extension version is below the host-required minimum.

Called from:

- **CLI.** `ExtensionDispatcher` after `AssemblyLoader.LoadOrCache`, before the first `IProcessor` resolves.
- **PS.** `CirrusCmdletBase.BeginProcessing()` at cmdlet start.

### 7.3 Failure modes

- **Install** (`extension add`): hard error, abort install, no disk write. Exit code 7, `CompatibilityViolation` symbolic code.
- **Runtime** (load): per-extension warn-and-skip. Contributions dropped from live `LeafIndex`. Other extensions keep working (§6.3).

### 7.4 Inverse direction (`HostExtensionRequirements`)

```csharp
internal static readonly Dictionary<string, VersionRange> HostExtensionRequirements = new()
{
    // Populated when the host team ships a breaking change.
    // Empty in v1.
    // Example:
    // ["Microsoft.Azure.Cirrus.Extensions.Compute"] = VersionRange.Parse("[2.0.0,)"),
};
```

Ships empty. The host team adds an entry in the same PR that introduces a breaking change for a specific extension. Cost is ~10 lines; worth shipping with v1 so the escape hatch exists when the first breaking change happens.

### 7.5 Bypass (deferred)

`CIRRUS_DISABLE_COMPAT_CHECK=1` is **not** in v1 (Q21). Runtime warn-and-skip is already forgiving. Additive in ~3 lines when needed.

---

## 8. Discovery / hint catalog

v1 ships an embedded `extension-hints.json`. Schema is feed-ready so the fetch half can be added in vNext without changing the file format or the resolution path.

### 8.1 Schema (v1, feed-ready)

Nothing under `extension-hints.json` has shipped to main, so v1 carries the feed-ready shape directly — no v1→v2 fork in the reader, no transitional back-compat. Future additive fields can be introduced without bumping `schemaVersion` (the reader already tolerates unknown properties).

```jsonc
{
    "schemaVersion": 1,
    "generatedAt": "2026-05-28T10:00:00Z",
    "sourceHostVersion": "1.2.0",
    "leaves": {
        "vm": { "packageId": "Microsoft.Azure.Cirrus.Extensions.Compute" },
        "vm create": {
            "packageId": "Microsoft.Azure.Cirrus.Extensions.Compute",
        },
        "vm list": { "packageId": "Microsoft.Azure.Cirrus.Extensions.Compute" },
        "disk": { "packageId": "Microsoft.Azure.Cirrus.Extensions.Compute" },
        "disk list": {
            "packageId": "Microsoft.Azure.Cirrus.Extensions.Compute",
        },
        "group": { "packageId": "Microsoft.Azure.Cirrus.Extensions.Resources" },
        "group create": {
            "packageId": "Microsoft.Azure.Cirrus.Extensions.Resources",
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

v1 first-party embed populates only `packageId`. Compat fields (Q26) are reserved for the feed half.

### 8.2 Generation

`ExtensionHints.targets` (Phase 8) scans every project in `src/Extensions/**`:

- Bare verbs from `[ExtensionVerb]` declarations.
- Full-path leaves from `s_cliTable` literal keys.

Cross-project collisions fail the build (§6.1). Output is embedded as a resource in `Core.Cli` and consumed by `ExtensionHintTable`.

### 8.3 Resolution (v1)

On dispatch miss, `ExtensionHintTable` walks the longest-prefix path against `extension-hints.json`:

1. Exact `(verb, path)` match → "install package X" (§4.3 case 1).
2. Verb match, no path match → "package X provides verb '<verb>' but no '<path>' leaf" (§4.3 case 2).
3. No verb match → fall through to generic "try `extension search`" (§4.3 case 3).

### 8.4 vNext: feed refresh

Additive — no host change beyond fetch + cache read. Same schema, fetched on a TTL (lean 24h, Q24), written to `~/.cirrus/cache/extension-hints.json`. Resolution becomes cache-first if fresh, embedded fallback if cache is missing or stale. Stale fallback is silent — no "hints may be outdated" warning (Q25).

Compat-aware filtering (Q26): once feed entries populate `minCoreVersion`/`maxCoreVersion`, the host can pre-filter suggestions whose package would fail the install-time compat check. For v1 (embed only, compat fields empty), the code path is dormant.

### 8.5 Deferred / open

- **Feed endpoint location** (Q23) — per-`core`-release GitHub asset, Cirrus-owned blob, or NuGet feed metadata. Pick when we wire up the fetch.
- **`extension list --available`** (Q27) — comes for free when the feed exists; out of v1 scope until then.
- **PowerShell CNF integration** — Cirrus PS reads the same embedded `extension-hints.json` for CommandNotFound scenarios. Conversation with the PS team is pending. Out of scope for this branch.

---

## 9. Implementation status

Phases 1–9 complete; Phase 10 in progress.

### Phase 1 — Foundation ✅

1. ✅ `[ExtensionVerb("…")]` attribute in `Core.Abstractions`.
2. ✅ Annotate existing extensions: `Compute → "vm"`, `Resources → "group"`, `Login → "login"`, `Extension → "extension"`, `Config → "config"`.
3. ✅ Registry JSON read/write: schema class (§5.1), atomic write, unit tests.
4. ✅ Built-in seed dictionary in host.

### Phase 2 — Routing through registry ✅

5. ✅ Dispatcher reads `(built-in seed ∪ on-disk registry)` instead of `CommandGroupIndex`.
6. ✅ Delete `CommandGroupIndex`.

### Phase 3 — Mutating CRUD ✅

7. ✅ `extension add`: load attribute in throwaway ALC, conflict-check, extract DLLs, transactional registry write, rollback on failure.
8. ✅ `extension list` and `extension show`.
9. ✅ `extension remove` (errors on built-in verbs).
10. ✅ `extension update` (overwrite in place, pin-respecting, `--force` to break a pin).
11. ✅ `extension pin` / `unpin` commands + `--pin` flag on `add`.

### Phase 4 — `vm` and `group` become real extensions ✅

12. ✅ MSBuild target emits `extension-hints.json` as an embedded resource.
13. ✅ Dispatcher consults the hint table on unknown verbs.
14. ✅ Extract Compute and Resources into standalone NuGet packages. Drop from built-in seed.
15. ✅ Built-in seed is now exactly `extension` / `login` / `config`.

### Phase 5 — Shape A polish ✅

16. ✅ Error surface: exit codes 0–6, symbolic codes in JSON, `--output json` wired.
17. ✅ ALC caching: `ConcurrentDictionary<(pkg, version), Lazy<ALC>>`, no eviction, `--no-cache` test hook.

### Phase 6 — Schema and reader (Shape B foundation) ✅

19. ✅ Registry `schemaVersion: 2`. `contributions: [{verb, path}]` and `namespaces: [string]` fields. Tolerant reader for v1 entries.
20. ✅ In-memory `{(verb, path) → packageId}` index from merged seed + on-disk registry.
21. ✅ Unit tests: v1 tolerance, v2 round-trip, mixed-schema input.

### Phase 7 — Dispatcher rewrite ✅

22. ✅ Longest-prefix match against `(verb, path)` index. Positional binding for residual tokens.
23. ✅ Closes Shape A deferred item #17.
24. ✅ Compute (and other in-tree extensions) migrate `s_cliTable` keys to full-path form.

### Phase 8 — Conflict checks ✅

25. ✅ `extension add` per-leaf `(verb, path)` conflict check against registry.
26. ✅ `ExtensionHints.targets` collects bare verbs + full-path leaves, deduplicates across projects, emits single embedded `extension-hints.json`.
27. ✅ Dispatcher walks longest-prefix path against `extension-hints.json` for unknown-leaf messages.

### Phase 8.5 — Registry hardening ✅

27a. ✅ `RegistryReconciler` drops missing-payload entries from the live view (§5.7).
27b. ✅ `FileExtensionRegistryStore` write-timestamp concurrency guard.
27c. ✅ Load-time leaf-uniqueness invariant check.
27d. ✅ `extension list` returns separate `Live` / `Stale` arrays.

### Phase 9 — Multi-namespace validation ✅

28. ✅ Compute declares `[ExtensionVerb("vm")]` + `[ExtensionVerb("disk")]` and contributes a stub `disk list` leaf.
29. ✅ `[ExtensionVerb]` is repeatable. `Add.cs` / `Update.cs` validate each leaf's first token against the full declared set. Build-time check enforces the same rule.
30. ✅ Tests: multi-namespace routing, resolver, persistence roundtrip, remove-purges-both-namespaces, `extension list` exposes `Namespaces`.

### Phase 10 — Polish (in progress)

31. ✅ `extension list --by-verb` rendering (uses the `namespaces` field; output shape per §4.5). Multi-namespace extensions appear under each verb they contribute to.
32. ✅ **Telemetry dimensions on extension activities.** `Add` / `Update` / `Remove` activities are tagged with five OTEL-shaped attributes: `extension.package_id`, `extension.version`, `extension.source` (feed URL or `"path"` sentinel), `extension.namespace_count`, `extension.contribution_count`. Tags are set as soon as each value becomes known, so failed installs (e.g. mid-iteration `LeafConflict`) still carry whatever was determined before the throw — including the _attempted_ contribution count read from `s_cliTable`. Sideloaded (`--path`) installs are distinguishable from feed installs in the data. **PII note:** package id + version are PII-safe for first-party feeds; revisit before enabling third-party feeds.
33. ✅ [extension-framework.md](./extension-framework.md) §3.4 now points here for the dispatch model (resolution rules in §4, Shape B in §5, registry/hint-catalog/`s_cliTable` interaction in §11).
34. ✅ **Symmetric `extension add --name`.** `Add.ResolvePackageId(name, hints)` resolves in three branches: (a) dotted token → passthrough (full package id, including third-party); (b) bare token matching a verb in the embedded `ExtensionHintTable` → resolved package id (so `add --name vm` installs `Microsoft.Azure.Cirrus.Extensions.Compute`, matching the dispatcher's unknown-verb suggestion); (c) bare token miss → legacy `Microsoft.Azure.Cirrus.Extensions.{name}` prefix-strip. Hint wins over prefix-strip when both could apply. Covered by `ExtensionAddNameResolutionTests` (7 cases). `--path` unchanged.
35. ✅ **Smarter `ExtensionNotFound` hint.** When `extension add --name <token>` misses every configured feed, `Add.cs` now catches the feed-miss `InvalidOperationException`, normalizes it to a typed `ExtensionNotFound` (exit 4), and probes the local repo via `LocalArtifactProbe.Find`. The probe walks up from cwd looking for `cirrus.slnx`, then globs `artifacts/Debug/<packageId>/<packageId>.*.nupkg` and surfaces the most recently written match as an `--path` suggestion in the error message. Suggestion-only — never auto-installs from local, to avoid silently consuming stale builds. Debug-only by design (a Release-only developer edits the suggested command). Closes the dev-loop gap where `--name` silently failed when no NuGet feed was configured. Covered by `LocalArtifactProbeTests` (6 cases).
36. ✅ **`[CirrusCompatibility]` + `CompatibilityGuard`** per §7 (partial; remainder in vNext). Shipped: assembly-level `[CirrusCompatibility(MinCoreVersion, MaxCoreVersion)]` in `Core.Abstractions` with init-only properties; `Core.Runtime.CompatibilityGuard` exposing a pure `Check(min, max, current)` and a `CheckAssembly(extensionDir, current)` reader that uses `MetadataLoadContext` (no execution, mirrors `ExtensionVerbReader`). NuGet `SemanticVersion` parsing with pre-release ordering and tolerant defaults: missing attribute → compatible, unparseable bound → ignored with advisory, unparseable current → compatible (treated as host bug). Install-time hard fail wired in `Add.cs` (new `CompatibilityViolation` factory on `CirrusErrorException`, exit code 7); existing rollback handles disk cleanup. First-party extensions stamped via `Compatibility.cs` files (note: `AssemblyInfo.cs` is excluded by SDK convention with `GenerateAssemblyInfo=false`). Covered by `CompatibilityGuardTests` (15 cases: pure `Check` matrix + MLC reader against the stamped Login artifact). Renumbered exit codes: `RegistryConcurrency` 7→8, `RegistryInvalid` 8→9. **Deferred to vNext** (see below): host core-version source-of-truth (today: literal `"1.0.0"` at the call site), runtime warn-and-skip filtering in the dispatch path, `HostExtensionRequirements` inverse-direction dictionary, and `CirrusCmdletBase` so PS cmdlets get the check via `BeginProcessing` without per-cmdlet duplication.
37. ✅ **`extension-hints.json` feed-ready shape** per §8.1. Generator emits `generatedAt` + `sourceHostVersion` provenance and per-leaf object entries (`{ "packageId": ... }`) so optional `minCoreVersion`/`maxCoreVersion` can be added later without a schema bump; reader surfaces all four fields and tolerates unknown properties. v1 first-party output leaves compat fields unset (Q26 reserves them for the feed half). `schemaVersion` stays at `1` — nothing had shipped to main, so no v1→v2 fork is needed in the reader.
38. ✅ **Shape B contract test.** Two extensions sharing a verb with non-colliding leaves both install successfully; same verb with a colliding leaf fails the second install. Implemented as `ShapeBContractTests` (4 cases) in `Cli.Tests`, which drives the conflict-detection branches via the new `ExtensionAddPlanner` (a pure static extracted from `AddExtensionProcessor.Process()`). Two minimal fixture class libraries under `tests/Fixtures/` (`Cirrus.Tests.Fixtures.SharedAlpha` and `…SharedBeta`, both `[ExtensionVerb("sharedverb")]`) compile to real DLLs that tests stage into temp dirs and read with `LeafReader` — covering co-existence (Alpha `sharedverb alpha` + Beta `sharedverb beta`), collision (Alpha staged under two package ids), self-upsert, and the undeclared-verb guard. Skips `ExtensionInstaller` / NuGet extraction by design ("B1" fidelity per planning) — keeps the test self-contained, CI-safe, and never touches `~/.cirrus`. Fixture projects ship under `tests/Fixtures/Directory.Build.props` which shadows the test-SDK injection so they build as plain class libs.
39. ⏩ PS team conversation re: CNF integration with the embedded hint catalog. **Moved to vNext** — hypothetical future scenario, no concrete v1 ask.
40. ✅ **`extension add` respects pins.** `add` of a different version of a pinned package now fails with `[PinViolation]` (exit 6) unless `--force` is passed; on `--force` the pin is carried forward onto the new entry. Helper `CheckPinPrecondition` covered by `ExtensionAddPinTests` (5 cases).
41. ✅ **Stack-aware conflict enumeration in `Add.cs`.** Implemented in `ExtensionAddPlanner.PlanContributions`: registry-side `(verb, path)` collisions are now accumulated across the candidate's full leaf set and surfaced as a single `LeafConflict` whose `details.conflicts` is an array (one row per colliding leaf with `leafKey`/`verb`/`path`/`existingPackageId`) and `details.conflictCount` is the total. Human-readable message is "Extension 'X' has N leaf conflicts…" followed by a bullet per row, so plain-text terminals get full diagnosis without parsing JSON. Conflict order is deterministic (candidate `s_cliTable` insertion order). Undeclared-verb and built-in-root checks remain first-error-wins (those are usage errors in the candidate's own code; enumeration adds noise without value). Realizes §5.1 commitments #1 (multi-conflict surface) and lays the groundwork for the vNext `--override` flow (#2 stack model). Also: dropped the dead `IsBuiltIn` flag from `LeafEntry` and `ResolvedExtension` — both were write-only with no live readers; built-in-ness is now implicit via `BuiltInExtensions.IsBuiltInVerb(verb) && path == ""`. Covered by `ShapeBContractTests.PlanContributions_MultipleCollisions_AreEnumeratedInOneThrow` (+ updated single-conflict test) for a total of 5 Shape B cases.
    _Checkpoint at end of Phase 10: Shape B v1, shippable._

### vNext (tracked, not v1)

- Feed-fetch path for `extension-hints.json` (§8.4).
- `CIRRUS_DISABLE_COMPAT_CHECK` env var (Q21).
- **Host core-version source-of-truth.** Today `Add.cs` and any future runtime-side caller pass the literal `"1.0.0"` to `CompatibilityGuard`. Establish a single host version (az-cli's `__version__` is the model) and thread it through. Until then `[CirrusCompatibility]` ranges are checked against the literal — fine for v1 since no breaking host change has shipped, but the literal must move when one does.
- **Runtime warn-and-skip in the dispatch path** (§7). Install-time hard fail catches the common case (operator-driven `extension add`); a runtime check protects against side-loaded packages or post-install host upgrades. Cleanest hook is a registry-side filter applied at `LeafIndex.Build` time so incompatible packages disappear from live routing with a stderr warning. Deferred only because it inherits the host-version source-of-truth question above.
- **`HostExtensionRequirements` (Q19, inverse direction).** Empty `Dictionary<string, VersionRange>` in `Core.Runtime` paralleling az-cli's `EXTENSION_VERSION_REQUIREMENTS`. Checked inside `CompatibilityGuard` after the extension's own range passes. Populated by the host team in the same PR that introduces a breaking change against a specific extension.
- **`CirrusCmdletBase : PSCmdlet`** so the PS surface gets `CompatibilityGuard.CheckAssembly` for free in `BeginProcessing`, instead of duplicating the guard call across the ~12 cmdlet entry points across four modules. Skipped in v1 because the base type doesn't exist yet and rebasing every cmdlet is its own refactor.
- `extension list --available` (Q27).
- **PS team CNF conversation re: embedded hint catalog** (formerly #39). The hint catalog (`extension-hints.json`) is already embedded in the host and works for the CLI dispatcher's verb-miss → extension-name suggestion path. Whether the PowerShell surface should consume the same catalog from a CNF (Command-Not-Found) handler is open: PS modules are loaded by name (`Import-Module Cirrus.Vm`) rather than dispatched by verb, so the unknown-verb shape doesn't map cleanly. Revisit when the PS team has a concrete CNF scenario; until then no v1 wiring.
- Bounded LRU on the ALC cache if memory matters (§5.5).
- Multi-version retention (`versions: [...]` + `activeVersion`, additive schema change — Q6).
- **Latency / cache guardrails (item 40).** (a) TTL/mtime-keyed cache for `RegistryReconciler` so post-cache reconcile is constant-time on network home dirs (SMB/NFS/OneDrive); local-SSD impact today is negligible. (b) BenchmarkDotNet target in `tests/Benchmark/` asserting a leaf-miss hot path (e.g. `azv2 vm foobar` with an extension installed) stays under a p99 budget (~300ms), plus an assembly-load-counter contract test asserting the dispatcher never spins up an ALC on miss. Insurance against drift toward az-cli's load-everything-on-CNF behavior; not needed before v1.
- **Explicit `--override` for `extension add` (informed by az-cli's `overwrite_order_in_cli.md`).** v1 rejects all leaf collisions at install time, no exceptions. vNext relaxes this with an opt-in `--override "verb path[, ...]"` flag that lets a new extension take ownership of specific leaves while preserving the **stack** model committed in §5.1: the displaced contributor is pushed down (not removed), `extension list --by-verb` annotates the override, and `extension remove` of the top-of-stack contributor restores the next one underneath. Built-in displacement uses the same mechanism (per §5.1 commitment #2, no separate "louder" lane). This is a strict improvement over az-cli's silent alphabetical-name-wins overwrite (no audit trail, no restore-on-remove, see [`doc/overwrite_order_in_cli.md`](https://github.com/Azure/azure-cli/blob/main/doc/overwrite_order_in_cli.md)). Schema cost: additive only — a `displaced: [...]` field on the contribution object plus an `overriddenAt` timestamp.

---

## 10. Open / deferred

- **Q11**: how does an extension declare a namespace it owns but ships zero leaves under? Low priority; revisit if a use case appears.
- **Q12 / Q15**: aggregated namespace help rendering. Help branch.
- **Schema-versioning commitment**: how many registry-schema versions back do we read indefinitely? Not discussed; current posture is "v1 + v2."
- **Trust and provenance** (full §1.7 item). Revisit before the first third-party extension ships.
- **Roaming home directories and system-wide installs** (full §1.7 item). Current posture: per-user, unsupported on roaming.
- **PowerShell parity definition** (full §1.7 item). "Same surface" vs. "same shape when both exist."

---

## Appendix A: Historical discussion log

Chronological record of how we arrived at the current decisions. Resolutions live in §1; this appendix exists for context on _why_.

### 2026-05-22 — Initial decision pass

**Q1 (Extension verb declaration).** `[ExtensionVerb("vm")]` attribute on `IExtensionIndex`. Colocated, no manifest, readable in a throwaway ALC.

**Q2 (Registry location).** `~/.cirrus/extensions/registry.json`. Colocated with install dirs, single file, user-scoped.

**Q3 (True core).** `extension`, `login`, `config`. Resources (`group`) and Compute (`vm`) move to installable extensions.

**Q4 (Tree vs. flat command keys).** Flat string-keyed dictionary, hierarchy by spaces. Zero migration cost.

**Q5 (Pinning UX).** Both `extension pin/unpin` commands and `--pin` on `add`. Pinning blocks `update` unless `--force`.

### 2026-05-22 — Second pass

**Q6 (Old-version retention).** Strict single-version. Multi-version is an additive schema change later.

**Q7 (Conflict & scoping).** User-scoped only. No `--force` on conflict. Pinned blocks `update` without `--force`. Built-ins not removable.

**Q8 (ALC cache).** Process-lifetime `ConcurrentDictionary`, no eviction, `--no-cache` test hook.

### 2026-05-22 — Third pass

**§6 seeding = option A.** Hardcoded seed dict merged with on-disk registry. Built-in wins on conflict.

**§4.4 error surface.** Seven exit codes (0–6), symbolic codes for JSON, `--output json` reused.

### 2026-05-22 — Fourth pass

**§4.3 hint table.** Build-time MSBuild target scans `[ExtensionVerb]`, emits embedded JSON. No first-run network calls. Q9 (feed-served index) tracked as non-blocking.

### 2026-05-23 — Shape B revision begins

Re-scoped from Shape A (one verb / one extension) to Shape B (shared namespaces, per-leaf ownership) after the az-cli monorepo precedent (`vm` module contributing to `disk`, `disk-access`, etc.) was raised as a recurring real-world shape. Registry schema bumped to v2; tolerant reader for v1 entries.

### 2026-05-28 — PM meeting

Three decisions landed:

1. **Shape B confirmed as the v1 model.** Per-verb ownership gate removed from `Add.cs`; per-leaf `(verb, path)` collision check is now sole enforcement. Built-in verbs remain reserved.
2. **Compatibility metadata ships in v1.** Extensions declare a `core` min/max version range.
3. **Hint catalog moves to blended embed-plus-feed model.** v1 cut: embed only, schema feed-ready.

Compat posture confirmed: source-compat with az-cli, migration tool acknowledged.

Deferred from the meeting: §1.7 trust, §1.7 hygiene, §1.7 surface polish. PS-CNF integration raised as a follow-up conversation with the PS team.

### 2026-05-28 — Post-meeting Q resolution pass

az-cli investigation (`ext_compat_with_cli`, `check_version_compatibility`, `EXTENSION_VERSION_REQUIREMENTS`) confirmed the model for Q16–Q22 and Q13/Q22 (per-extension warn-and-skip on compat failure under Shape B, not per-verb). Q14 resolved as verb-headed `--by-verb` rendering. Q21 deferred. Q15 to help branch. Q23–Q27 resolved as "embed in v1, schema feed-ready, fetch in vNext" with Q25 silent fallback and Q26 schema reserving optional compat fields.
