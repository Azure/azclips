# Design Spec: Extension Management

> **Status:** Shape A is implemented on `feature-extension-management-shape-a` (phases 1–5 complete, item #17 deferred). This branch (`feature-extension-management-shape-b`) revises the model to Shape B — shared verb namespaces with per-leaf ownership. See §11 for the Shape B delta; Shape A sections (§3–§9) are preserved as the baseline the delta is read against.
>
> **Branch:** `feature-extension-management-shape-b`
>
> **Related:** [extension-framework.md](./extension-framework.md) — covers extension loading, ALC isolation, and processor contract. This doc covers the layer _above_ that: how extensions are registered, discovered, routed to, and managed over their lifecycle.

---

## 1. Goals

- Make installed extensions first-class: usable without code changes to the host.
- Replace the hardcoded `CommandGroupIndex` with a persistent registry populated at install/remove time.
- Add the missing CRUD lifecycle operations: `list`, `remove`, `update`, `show` (in addition to today's `add`).
- Lift the parser's depth-1 limitation so extensions can ship multi-level command trees (e.g. `azv2 vm boot-diagnostics enable`).
- Cache ALCs across invocations so extension load isn't paid on every command.

## 2. Non-goals (this branch)

- **Shape B (shared verb namespaces).** Multi-extension contribution under a single top-level verb (the az-cli `network` / `private-dns` pattern) is explicitly deferred. Design choices here should not preclude it, but no implementation work is committed.
- **Help system.** `--help` rendering, group descriptions, MAML/online help — tracked separately on the help branch.
- **PowerShell side.** PS module routing has its own load/isolation model (see [extension-framework.md](./extension-framework.md) §4.4). Out of scope.
- **NativeAOT.** Conflicts with dynamic loading regardless of routing topology.
- **Marketplace / catalog UX.** Discovery beyond "user knows the NuGet package id" is out of scope.

## 3. The Shape A model (explicit statement)

> **One top-level verb is owned by exactly one extension.** When a user types `azv2 <verb> ...`, exactly one extension is loaded to handle the invocation. Two extensions cannot both register the same top-level verb. Cross-cutting features (e.g. SQL-on-VM) ship under their own top-level verb, not as leaves under another extension's tree.

Consequences:

- Routing is a flat `{verb → extension}` map. No merging, no per-verb load lists, no conflict resolution beyond "this verb is already taken."
- An extension's command tree is an internal concern of that extension — the host doesn't merge across extensions.
- `azv2 <verb> --help` (when help lands) lists only what the one owning extension provides. Discoverability across extensions happens at the top-level verb list, not within a verb.

### True core of Cirrus

The host ships with exactly three built-in extensions, present in every install:

- **`extension`** — manages other extensions (this doc's CRUD surface).
- **`login`** — authentication and tenant/subscription context.
- **`config`** — user/host configuration.

Everything else (including today's `group` / Resources and `vm` / Compute) becomes an installable extension. The host has no built-in Azure-resource verbs — those are always installed.

## 4. User-facing surface

### 4.1 Extension CRUD commands

All under `azv2 extension`. Naming follows existing kebab-case CLI convention.

| Command                                    | Behavior                                                                                                    |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------- |
| `azv2 extension add <pkg>[@<version>]`     | Install. Overwrites any prior version of the same package. `--pin` flag locks the version against `update`. |
| `azv2 extension list`                      | Show installed extensions: name, version, registered top-level verb, install source, install date.          |
| `azv2 extension remove <name>`             | Uninstall. Remove from registry, delete `~/.cirrus/extensions/<pkg>/`. Errors on built-in extensions.       |
| `azv2 extension update <name>[@<version>]` | Install newer version, replacing the old one in place. Fails on pinned extensions unless `--force`.         |
| `azv2 extension pin <name>[@<version>]`    | Lock active version. With no version, pins the currently installed version.                                 |
| `azv2 extension unpin <name>`              | Remove the pin.                                                                                             |
| `azv2 extension show <name>`               | Detail view: full path, version, registered verb, source feed, install date, dependencies.                  |

**Open items:**

- See §4.4 for the error surface (exit codes and structured output).

### 4.2 Conflict at install time

A user tries to install an extension whose top-level verb is already claimed:

```
> azv2 extension add Contoso.Foo
Error: Extension 'Contoso.Foo' wants to register verb 'vm', but it is already
owned by 'Microsoft.Azure.Cirrus.Extensions.Compute' (installed 2026-04-12).

Options:
  - Remove the existing extension first: azv2 extension remove Compute
  - Contact the extension author to choose a different verb
```

**Decision (for now): no `--force` override.** Shape A's exclusivity is meaningful only if it isn't bypassable. To swap, user runs `extension remove` then `extension add`. See §10 entry for Q7 for the broader scoping discussion.

### 4.3 What changes for end users today

- Built-in verbs `login`, `extension`, `config` continue to work as today.
- Built-in verbs `group` (Resources) and `vm` (Compute) **become installable extensions**, no longer shipped with the host.
- Existing scripts using `azv2 group ...` / `azv2 vm ...` produce an actionable error pointing at the install command (no silent network install on first run — fails badly offline and is surprising).

**Unknown-verb error UX (the discovery problem at scale):**

When the dispatcher sees an unknown top-level verb, it consults a **build-time generated hint table** shipped with the host:

```
> azv2 vm list
Error: 'vm' is provided by extension 'Microsoft.Azure.Cirrus.Extensions.Compute',
which is not installed. Install it with:
  azv2 extension add Microsoft.Azure.Cirrus.Extensions.Compute
```

If the verb is not in the hint table (third-party extension, or a typo):

```
> azv2 foo list
Error: No extension is installed for verb 'foo'.
Search for an extension that provides it:
  azv2 extension search foo
```

(`extension search` itself is out of scope for this branch.)

The AI fallback in `OrchestratorEngine` continues to handle free-form input as today; this error path only fires on the structured-command path when the verb is unknown _and_ the input doesn't look like a natural-language request.

**How the hint table is produced:**

A small MSBuild target in the host project scans `src/Extensions/**` for `[ExtensionVerb("…")]` attributes and emits a JSON file embedded as a resource in the host assembly. The shape is trivial:

```jsonc
{
    "vm": "Microsoft.Azure.Cirrus.Extensions.Compute",
    "group": "Microsoft.Azure.Cirrus.Extensions.Resources",
}
```

**Tradeoffs (acknowledged):**

- **Pro:** Zero runtime cost, no feed dependency, automatically in sync with whatever extensions ship in the monorepo at host-release time. No hand-maintained list.
- **Con:** Hint freshness is bounded by host-release cadence. Users on a stale `azv2` get no hints for newer first-party extensions — they fall through to the generic "search" path.
- **Limit:** Third-party extension publishers aren't covered by this table at all. If/when third-party extensions become a thing, a feed-served verb index (see Q9 in §10) is the answer. For now, `extension search` is the third-party discovery story.

### 4.4 Error surface

**Exit codes:**

| Code | Meaning                                              |
| ---- | ---------------------------------------------------- |
| 0    | success                                              |
| 1    | generic error                                        |
| 2    | usage error (bad args, unknown flag)                 |
| 3    | verb conflict on install                             |
| 4    | extension not found (in feed or registry)            |
| 5    | feed / network error                                 |
| 6    | pin violation (`update` on pinned without `--force`) |

These apply to the `extension` verb's commands. Other verbs keep their own exit-code conventions.

**Structured output:**

`extension` commands respect the host's existing `--output json` flag. On success the payload is the command's domain shape (e.g. `list` returns an array of installed extensions). On failure:

```jsonc
{
    "error": {
        "code": "VerbConflict",
        "message": "Extension 'Contoso.Foo' wants to register verb 'vm', but it is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'.",
        "details": {
            "verb": "vm",
            "requestedPackage": "Contoso.Foo",
            "existingPackage": "Microsoft.Azure.Cirrus.Extensions.Compute",
        },
    },
}
```

- `code` is a stable symbolic identifier intended for programmatic callers (`VerbConflict`, `ExtensionNotFound`, `PinViolation`, `FeedUnavailable`, `UsageError`). Exit codes are for shell scripts; symbolic codes are for tools.
- `message` is human-readable.
- `details` carries operation-specific context.

## 5. Internals

### 5.1 The registry

A single JSON file at `~/.cirrus/extensions/registry.json`, colocated with the extension install directories.

Schema (Shape A):

```jsonc
{
    "schemaVersion": 1,
    "extensions": {
        "Microsoft.Azure.Cirrus.Extensions.Compute": {
            "verb": "vm",
            "version": "1.1.0",
            "pinned": false,
            "source": "https://api.nuget.org/v3/index.json",
            "installedAt": "2026-04-12T10:23:00Z",
        },
    },
}
```

**Design points:**

- One entry per extension package. The `verb` field is the top-level token the extension claims.
- Single version per extension (`version` is a string, not an array). `update` overwrites in place. See §10 / Q6 for the rationale and the path to multi-version later if needed.
- `pinned` controls whether `update` will move `version` forward automatically.
- Built-in extensions (`extension`, `login`, `config`) are **not** in this file — they live in a small static seed in the host so it can boot without any installed extensions. See §6 for the seeding mechanism.

**Forward-compatibility note for Shape B:** The schema is `{extension → {verb, ...}}` rather than `{verb → extension}`. This is intentional: extending to `{extension → {verbs: [...], ...}}` (an extension claims multiple verbs) or building a reverse `{verb → [extensions]}` index from this file is straightforward. Likewise, widening `version` to `versions: [...]` plus an `activeVersion` field is a non-breaking additive change if multi-version is needed later.

### 5.2 Registration on install / remove

`extension add` becomes a multi-step operation:

1. Resolve package from feed (already done).
2. **Load the extension's `IExtensionIndex`** in a throwaway ALC and read its declared top-level verb. The verb is declared via an attribute on the `IExtensionIndex` implementation (proposed: `[ExtensionVerb("vm")]`). The attribute is the single source of truth — no separate manifest file.
3. Check the registry for conflicts. Abort with a clear error if the verb is taken.
4. Extract DLLs to `~/.cirrus/extensions/<pkg>/<version>/` (already done).
5. Update `registry.json` transactionally.
6. On failure at any step: roll back partial state (delete extraction dir, restore registry).

`extension remove`: validate name → delete dir(s) → remove registry entry → done.

### 5.3 Dispatch flow (replacing CommandGroupIndex)

```
azv2 vm boot-diagnostics enable --name foo
       │
       ▼
  args[0] = "vm"
       │
       ▼
  Registry.LookupVerb("vm")
       │  ↓ "Microsoft.Azure.Cirrus.Extensions.Compute" v1.1.0
       ▼
  AssemblyLoader.LoadOrCache(pkg, version)
       │  (existing CLI loader chain; now with ALC caching)
       ▼
  IExtensionIndex.GetCommandMetadata("boot-diagnostics enable", CLI)
       │
       ▼
  ExtensionParser.Parse(args[1..])
       │
       ▼
  IProcessor.Init(ctx) → ProcessAsync()
```

The two changes vs. today:

- Step 2 hits the registry, not a hardcoded dictionary.
- Step 3 caches ALCs across invocations.

### 5.4 Parser depth lift

Today, `ExtensionParser.Parse()` already joins multi-token command names ([src/CLI/Parser/ExtensionParser.cs](../../src/CLI/Parser/ExtensionParser.cs#L31)). The depth-1 limitation is really in the extension's flat `IExtensionIndex` table — extensions just don't ship multi-token keys today.

For Shape A, the change is:

- Allow extensions to register multi-token command keys like `"boot-diagnostics enable"` in `s_cliTable`. Hierarchy is encoded by convention in the key string, not in a tree-of-nodes structure on `IExtensionIndex`. This keeps the migration cost near zero and defers the tree representation until the help branch needs it.
- The dispatcher needs to handle "user typed a prefix that matches no leaf" — today it throws `KeyNotFoundException`. With multi-level trees, this case becomes more common (e.g. `azv2 vm boot-diagnostics` with no further token).
- `[TBD]` What's the right error? Today: exception → AI fallback. With trees: ideally surface "did you mean one of: enable, get-log?" — but that's tied to the help system which is out of scope. Interim: keep the exception → AI fallback path.

### 5.5 ALC caching

Today, `AssemblyLoadingService` creates a new `ExtensionLoadContext` on every invocation (TODO in source). For a long-running scenario (TUI, repeated invocations from a script) this is wasteful.

See §10 entry for Q8 for the full discussion. Working proposal:

- Cache key: `(packageId, version)`.
- Cache lifetime: process lifetime. No eviction.
- One-shot CLI invocations get no benefit (process dies anyway); the cost of the cache is one `ConcurrentDictionary` add per invocation. Negligible.
- Long-running hosts (TUI, test harness) get the full benefit.
- Invalidation on `update`/`remove` is not handled: those operations should be followed by a fresh process. The cache is keyed on version, so the next process picks up the new version automatically.
- Thread safety: `ConcurrentDictionary` with `GetOrAdd` and a `Lazy<ExtensionLoadContext>` value to serialize ALC creation per key.

## 6. Built-in extensions: how they're seeded

The host ships with exactly three built-in extensions: `extension`, `login`, `config` (see §3 "True core"). These need to be in the routing tree from first boot without an install step.

**Decision: option A.** A small hardcoded seed dictionary in the host (replacing today's `CommandGroupIndex`), merged with the on-disk registry at startup.

Merge semantics:

- Built-in entries are never written to `registry.json`. They live only in memory, sourced from the host seed.
- On startup, the dispatcher's verb → extension map is `built-in seed ∪ on-disk registry`.
- On verb conflict between a built-in and an installed extension, **built-in wins.** Defensive: prevents an installed extension from shadowing `extension`/`login`/`config`. The conflict should be impossible because `extension add` refuses to register any verb that collides with a built-in (validation happens in the install path, not at load time).

Alternatives considered and rejected:

- **B.** Build copies built-in metadata into a shipped `registry.json`. Duplicates merge logic for marginal benefit. Also creates a footgun where users might edit the built-in entries.
- **C.** Treat built-ins as installed extensions, install on first boot. Adds first-boot failure modes (network? disk?) for uniformity that doesn't pay back at this scale.

## 7. Open questions

Resolved (see §10 Discussion log):

- ~~Q1: How does an extension declare its verb?~~ → `[ExtensionVerb("…")]` attribute on `IExtensionIndex` implementation.
- ~~Q2: Registry location / schema?~~ → `~/.cirrus/extensions/registry.json`, schema in §5.1.
- ~~Q3 (partial): What's in the true core?~~ → `extension`, `login`, `config`. Seeding mechanism still pending (working rec: option A).
- ~~Q4: Tree vs. flat in `IExtensionIndex`?~~ → Flat, hierarchy by convention.
- ~~Q5: Pinning UX?~~ → Both: `extension pin/unpin` commands + `--pin` flag on `add`.
- ~~Q6: Old-version retention?~~ → Strict single-version. `update` overwrites in place. Multi-version is an additive schema change later if needed.
- ~~Q7: Conflict and scoping edge cases?~~ → No `--force` on verb conflict; user-scoped only; `remove` purges the (single) install; pinned blocks `update` without `--force`; built-ins are not removable.
- ~~Q8: ALC cache strategy?~~ → Process-lifetime `ConcurrentDictionary<(pkg, version), Lazy<ALC>>`, no eviction.

Still open:

- _(none for v1 of this spec)_

## 8. Non-blocking but worth tracking

- **Q9: Feed-served verb index.** When/if the monorepo assumption breaks (extension release cadence decouples from host, or third-party publishers join), the build-time hint table in §4.3 stops being sufficient. The follow-up is a feed-served `verb-index.json` (az-cli `extension-index-v1` model) that the host fetches lazily and caches. Not needed for v1.
- **Forward path to Shape B.** If we adopt Shape A and later want Shape B, what migration looks like — does the registry schema bend, or do we ship a new one alongside?
- **Trust / signing.** Currently anything in `~/.cirrus/extensions/` gets loaded. Not addressed here, but worth not forgetting.
- **Telemetry.** Extension install/remove/update events. Out of scope for this branch but the registry is the natural source of truth.

## 9. Implementation plan

Organized in five phases. Each phase is a checkpoint where the tree compiles, tests pass, and the system is coherent. Cirrus is not yet publicly released, so every phase is free to make breaking changes; no deprecation periods required.

### Phase 1 — Foundation (no user-visible change) ✅

1. ✅ Define `[ExtensionVerb("…")]` attribute in `Core.Abstractions`.
2. ✅ Annotate existing extensions: `Compute → "vm"`, `Resources → "group"`, `Login → "login"`, `Extension → "extension"`, `Config → "config"`.
3. ✅ Registry JSON read/write: schema class (§5.1), atomic write, unit tests.
4. ✅ Built-in seed dictionary in host (initially holds all current verbs to preserve behavior; trimmed to `extension`/`login`/`config` in phase 4).

_Checkpoint: code restructured, runtime behavior unchanged._

### Phase 2 — Routing through the registry ✅

5. ✅ Dispatcher reads `(built-in seed ∪ on-disk registry)` instead of `CommandGroupIndex`.
6. ✅ Delete `CommandGroupIndex`.

_Checkpoint: no installed extensions yet, but the routing layer is the real one._

### Phase 3 — Mutating CRUD ✅

7. ✅ `extension add`: load attribute in throwaway ALC, conflict-check, extract DLLs, transactional registry write, rollback on failure.
8. ✅ `extension list` and `extension show`.
9. ✅ `extension remove` (errors on built-in verbs).
10. ✅ `extension update` (overwrite in place, pin-respecting, `--force` to break a pin).
11. ✅ `extension pin` / `unpin` commands + `--pin` flag on `add`.

Items #7–#11 each depend on #3 and #5 but are independent of each other afterwards — parallelizable across PRs.

_Checkpoint: full Shape A CRUD works against the registry. Built-ins still include `vm` / `group` from phase 1._

### Phase 4 — Migration: `vm` and `group` become real extensions ✅

12. ✅ MSBuild target in the host project scans `src/Extensions/**` for `[ExtensionVerb]` and emits `verb-hints.json` as an embedded resource (§4.3).
13. ✅ Dispatcher consults the hint table on unknown verbs and produces an "install this extension" message; unknown verbs fall back to the generic "try `extension search`" message.
14. ✅ Extract Compute and Resources from the host build into standalone NuGet packages. Drop them from the built-in seed. Hard cut — no deprecation period.
15. ✅ Built-in seed is now exactly `extension`/`login`/`config`.

_Checkpoint: Shape A is real end-to-end. Host ships with three built-ins; `vm` and `group` are installable._

### Phase 5 — Polish

16. ✅ Error surface: exit codes 0–6, symbolic codes in JSON, `--output json` wired (§4.4).
17. ✅ ALC caching: `ConcurrentDictionary<(pkg, version), Lazy<ALC>>`, no eviction, `--no-cache` test hook (§5.5).

_Checkpoint: shippable v1._

### Deferred

17. Multi-token command keys: parser/dispatcher tweak for "prefix matches no leaf"; Compute ships a nested command as the validation case. Includes positional-residual binding so shorthand like `extension remove vm` routes to the `remove` processor with `vm` bound to the first unset required argument.

---

## 10. Discussion log

Use this section to capture decisions and the reasoning behind them as we work through the `[TBD]` markers.

### 2026-05-22 — Initial decision pass

**Q1 (resolved): Extension verb declaration.** Via an `[ExtensionVerb("vm")]` attribute on the `IExtensionIndex` implementation. Rationale: keeps the declaration colocated with the index it gates, avoids a separate manifest file, and is trivially readable by the installer in a throwaway ALC without instantiating the index. Future Shape B extension would add `[ExtensionVerbs(...)]` or repeat the attribute.

**Q2 (resolved): Registry location.** `~/.cirrus/extensions/registry.json`. Colocated with the extension install directories, single file, user-scoped. Schema in §5.1.

**Q3 (partial): True core.** The host ships exactly `extension`, `login`, `config`. Resources (`group`) and Compute (`vm`) are no longer built-in — they become installable extensions like everything else. Seeding mechanism for the three core extensions still open (working recommendation: option A — hardcoded seed merged with on-disk registry at startup).

**Q4 (resolved): Tree vs. flat command keys.** Stick with flat string-keyed `Dictionary<string, CommandMetadata>` and let extensions encode hierarchy by spaces in the key (e.g. `"boot-diagnostics enable"`). Reason: zero migration cost for existing extensions, parser already handles multi-token names. Tree representation can come later when the help branch needs it.

**Q5 (resolved): Pinning UX.** Both. Primary commands: `extension pin <name>[@<version>]` and `extension unpin <name>`. Convenience: `--pin` flag on `extension add`. Pinning blocks `update` unless `--force` is passed.

---

### 2026-05-22 — Second pass: Q6, Q7, Q8 resolved

**Q6 (resolved): Strict single-version.** `update` overwrites in place; only one version of each extension exists on disk at any time. Matches the az-cli model. Rationale: simpler mental model, bounded disk, less surface area to test in v1. Easy to extend later — the schema change is purely additive (`version: string` → `versions: string[]` + `activeVersion: string`). No `--keep-old` flag, no retention config in v1. Dev workflow for swapping between published and WIP versions is deferred; if it becomes painful, the dev-source escape hatch from the earlier discussion is the natural follow-up.

Schema impact: `installedVersions: []` + `activeVersion` collapse into a single `version: string` field (see §5.1).

**Q7 (resolved): Conflict and scoping edge cases.**

- User-scoped only. No machine-wide install path. Built-ins ship with the host.
- No `--force` for verb conflicts on install. To swap owners of a verb: `remove` then `add`.
- `remove <name>` purges the install (single version, per Q6). No `--version` flag needed.
- `update` on a pinned extension fails with a clear error pointing at `unpin` or `--force`.
- Built-in extensions (`extension`, `login`, `config`) cannot be removed. `extension remove <built-in>` errors with a message explaining built-ins are part of the host.

**Q8 (resolved): ALC cache strategy.**

- `ConcurrentDictionary<(string pkg, string version), Lazy<ExtensionLoadContext>>` in `AssemblyLoadingService`.
- Cache lifetime = process lifetime. No eviction. No attempt to unload old ALCs.
- One-shot CLI invocations are unaffected (process dies between invocations anyway).
- Long-running hosts (TUI, test harness) get full benefit.
- After `update` or `remove`, the long-running host should restart to pick up changes cleanly. The cache key includes version, so any new version installed simply gets a new ALC on its first use.
- `--no-cache` test hook for benchmarking.
- Bounded LRU is a follow-up if memory becomes a concern (realistic worst case: ~100MB for 20 extensions, acceptable for TUI).

---

### 2026-05-22 — Third pass: §6 and §4.1 settled

**§6 (resolved): Built-in seeding = option A.** Hardcoded seed dict in the host, merged with on-disk registry at startup. Built-in entries never persisted to disk. Built-in wins on verb conflict (which `extension add` validates against anyway). Options B and C rejected (duplication and first-boot failure modes respectively).

**§4.1 / §4.4 (resolved): Error surface.** Seven exit codes (0–6), symbolic error codes for JSON output (`VerbConflict`, `ExtensionNotFound`, `PinViolation`, `FeedUnavailable`, `UsageError`, etc.), `--output json` reuses the host's existing flag. Full table and JSON shape in §4.4.

---

### 2026-05-22 — Fourth pass: §4.3 settled

**§4.3 (resolved): Unknown-verb discovery via build-time generated hint table.**

MSBuild target in the host project scans `src/Extensions/**` for `[ExtensionVerb("…")]` attributes and emits a `verb-hints.json` embedded as a resource. Dispatcher consults it on unknown-verb errors and produces an actionable "install this extension" message. Third-party extensions and typos fall through to a generic "try `extension search`" message.

No automatic install on first run — errors are loud and explicit, no silent network calls, works offline.

**Acknowledged limit:** hint freshness is bound to host release cadence. For the monorepo (first-party extensions ship with the host), this is a non-issue. Third-party publishers would need either a feed-served verb index or rely on `extension search` — deferred to a follow-up spec.

**New tracked item (Q9, non-blocking):** if/when extension release cadence decouples from host release, or third-party publishers become a thing, design a feed-served verb index (az-cli `extension-index-v1` model). Not needed for v1.

---

## 11. Shape B revision (this branch)

Shape A treats top-level verbs as **exclusive**: one verb is owned by one extension, end of story. Shape B treats them as **shared namespaces**: any extension may contribute leaves under any verb, and ownership is tracked per leaf path rather than per verb. Az-cli's monorepo proves the model — the `vm` module there contributes leaves under `disk`, `disk-access`, `disk-encryption-set`, etc., with no separate "namespace owner" concept.

This section is a **delta**, not a replacement. Sections of §3–§9 it supersedes are called out inline. Everything not mentioned (registry storage location, install/remove transactional flow, ALC caching strategy, exit-code table, structured-error JSON shape, built-in seeding) carries forward unchanged.

### 11.1 Model (supersedes §3)

> **A top-level verb is a shared namespace. Any number of extensions can contribute leaves under it. Each leaf — the full `(verb, path)` tuple — is owned by exactly one extension.**

Consequences:

- Routing is `{(verb, path) → packageId}`, not `{verb → packageId}`. Lookup is still O(1); the key just got wider.
- There is no "namespace owner" role. Every contributor is equal. Namespaces exist by virtue of being contributed to.
- `azv2 vm --help` aggregates leaves across all extensions contributing to `vm`. (Help rendering is still out of scope on the help branch; what changes here is only that the host can answer "who contributes to this namespace" without loading anything.)
- A single extension may contribute to multiple namespaces — e.g. one `Compute` package contributing under `vm`, `disk`, `disk-access`. This is the common case for Azure-style resource families.

The built-in trio (`extension`, `login`, `config`) remains unchanged. They are namespaces in the same sense; the host is the sole contributor for each.

### 11.2 Where contributions are declared (supersedes §5.2 step 2)

**Source of truth: the `s_cliTable` keys themselves, in full-path form.**

A leaf's `s_cliTable` key encodes its full path including the namespace token. Example for Compute under Shape B:

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

Under Shape A, the same Compute extension declared `[ExtensionVerb("vm")]` at the class level and used relative keys like `"create"`, `"show"`. Under Shape B the namespace token moves into the key.

`[ExtensionVerb]` becomes **repeatable** and is purely advisory: it lists the namespaces an extension wants advertised in the build-time hint table (so `azv2 disk list` on a host without Compute installed can produce "install Compute"). It has no runtime role. An extension that contributes under `vm` and `disk` declares:

```csharp
[ExtensionVerb("vm")]
[ExtensionVerb("disk")]
public class ExtensionIndex : IExtensionIndex { ... }
```

Validation: every `s_cliTable` key's first token must match one of the extension's `[ExtensionVerb]` declarations. Mismatches **fail the build** (see §11.10 / Q10). This keeps `[ExtensionVerb]` declarations and table contents in lockstep so the build-time hint table is always complete.

### 11.3 Registry schema (supersedes §5.1)

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
- `namespaces` is derived (set of distinct `verb` values across contributions) and persisted only as a convenience for fast `extension list --by-verb` rendering. Kept in sync on write.
- `version` / `pinned` / `source` / `installedAt` are unchanged from Shape A — they're per-extension, not per-leaf.
- `schemaVersion` bumps to 2. Reader is **tolerant**: schema-1 entries (no `contributions` array, has `verb: "<verb>"`) are interpreted as a single namespace-wide contribution `[{verb, path: ""}]`. No on-disk rewrite; tolerant reads stay v1 until the user runs `extension update` or installs anything new.

### 11.4 Conflict resolution (supersedes §4.2)

Hard error on **duplicate `(verb, path)`**, applied at two layers:

**Build time (monorepo).** The MSBuild target from Shape A §4.3 already scans `src/Extensions/**` for `[ExtensionVerb]` and emits `verb-hints.json`. Extend it to also scan each project's `s_cliTable` literal entries and emit a per-leaf table. If two projects emit the same `(verb, path)` tuple, fail the build with a clear `Verb 'vm', path 'create' is registered by both 'Compute' and 'OtherPkg' (locations: …)` message. Catches monorepo conflicts at PR time.

**Install time (third party).** `extension add` ALC-loads the candidate package, reads its `s_cliTable` keys, and checks every `(verb, path)` against the existing registry. First conflict aborts the install with `Verb 'vm', path 'create' is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'`. No `--force` override (matches the Shape A discipline).

Namespace-level conflicts no longer exist — multiple extensions advertising `[ExtensionVerb("vm")]` is fine and expected.

### 11.5 Dispatch flow (supersedes §5.3)

```
azv2 vm create --name foo
       │
       ▼
  args[0..n] = best longest-prefix match against registry's (verb, path) keys
       │  ↓ ("vm", "create")  →  "Microsoft.Azure.Cirrus.Extensions.Compute" v1.1.0
       ▼
  AssemblyLoader.LoadOrCache(pkg, version)        ← ALC cache from §5.5, unchanged
       │
       ▼
  IExtensionIndex.GetCommandMetadata("vm create", CLI)
       │
       ▼
  IProcessor.Init(ctx) → ProcessAsync()
```

Two changes vs. Shape A's flow:

1. **Two-token (or more) dispatch.** The dispatcher needs longest-prefix matching against the keys of the derived `{(verb, path) → packageId}` index. Same problem deferred under Shape A item #17, now back in scope because Shape B _requires_ multi-token keys to function. Residual tokens (those past the matched prefix) bind to positional arguments — same proposal as the deferred #17 work.
2. **Lazy dispatch, eager help.** Dispatch loads only the extension owning the matched leaf. Help, when implemented, loads every extension whose `namespaces` array contains the requested verb so it can aggregate leaves across contributors. The two paths reuse the ALC cache.

### 11.6 Unknown-verb / unknown-leaf hints (supersedes §4.3)

Today's hint table is `{verb: packageId}`. Shape B widens it to `{(verb, path): packageId}`, emitted by the same MSBuild target. Resolution order on miss:

1. Exact `(verb, path)` match → "install package X".
2. Verb match, no path match → "package X provides verb '<verb>' but no '<path>' leaf — try `azv2 <verb> --help` after installing".
3. No verb match → fall through to the existing generic "try `extension search <verb>`" path.

### 11.7 Migration story

**On-disk registry.** Existing Shape A entries (`schemaVersion: 1`, single `verb` field, no `contributions` array) are read tolerantly as a single full-namespace contribution. No file rewrite forced. Next `extension add`/`update`/`remove` write triggers a transparent schema-2 upgrade for the touched entry.

**Existing extension projects.** Each `s_cliTable` entry's key prepends the extension's `[ExtensionVerb]` value. Mechanical, scriptable. The accompanying refactor is the same change deferred as Shape A's #17 — combining them removes a phase.

**Public surface.** Cirrus is not publicly released; users on the Shape A branch will rebase forward. No deprecation window.

### 11.8 Out of scope (Shape B)

- Help system. Aggregated namespace help (`azv2 vm --help` merging leaves from all contributors) is still on the help branch. Shape B only ensures the host _can_ answer "which extensions contribute to `vm`" cheaply.
- Per-leaf pinning. `pinned` remains per-extension. A user can't pin `vm create` separately from `vm delete`. Not worth the complexity at v1.
- Cross-leaf ordering / overrides. No "this leaf takes priority over that one." Conflicts are hard errors; the user resolves them by uninstalling.
- Feed-served verb index (Q9 from §8). Still tracked, still deferred.

### 11.9 Implementation plan (Shape B phases)

Resumes numbering from Shape A's phase 5. Each phase compiles, tests pass, and the system is coherent.

**Phase 6 — Schema and reader (no user-visible change)**

19. ✅ Bump `ExtensionRegistry` to `schemaVersion: 2`. Add `contributions: [{verb, path}]` and `namespaces: [string]` fields. Tolerant reader for v1 entries.
20. ✅ Build the in-memory `{(verb, path) → packageId}` index at startup from the merged seed + on-disk registry.
21. ✅ Unit tests: v1 tolerance, v2 round-trip, index build from mixed-schema input.

_Checkpoint: registry can express Shape B, but nothing routes through it yet._

**Phase 7 — Dispatcher rewrite**

22. ✅ Replace single-verb lookup with longest-prefix match against the `(verb, path)` index. Surface residual tokens to the parser for positional binding.
23. ✅ Resolve [Shape A deferred item #17](docs/spec/extension-management.md#L361) as part of this — positional-residual binding, multi-token keys.
24. ✅ Compute (and any other in-tree extension) migrates `s_cliTable` keys to full-path form (`"vm create"` instead of `"create"`).

_Checkpoint: Shape B routing is real; Shape A's deferred #17 is closed._

**Phase 8 — Install-time and build-time conflict checks**

25. ✅ `extension add` reads candidate `s_cliTable` keys in throwaway ALC, conflict-checks every `(verb, path)` against the registry. Same error surface as Shape A's verb-level conflict, finer-grained details.
26. ✅ MSBuild `ExtensionHints.targets` collects bare-verb entries from `[ExtensionVerb]` and full-path leaf entries from `s_cliTable`, deduplicates across projects (build error on cross-project collision), and emits a single `extension-hints.json` embedded resource consumed by `ExtensionHintTable`.
27. ✅ Dispatcher walks the longest-prefix path against `extension-hints.json` for the unknown-leaf message (§11.6 case 2).

_Checkpoint: monorepo conflicts caught at PR time; third-party conflicts caught at install time._

**Phase 8.5 — Registry hardening**

27a. ✅ `RegistryReconciler` compares `registry.json` against the install root on every dispatch; entries whose `~/.cirrus/extensions/{packageId}/{version}/` folder is missing are dropped from the live view so the dispatcher never routes to a stale entry. Non-destructive — the file on disk is unchanged.
27b. ✅ `FileExtensionRegistryStore` captures the file's last-write timestamp at `Load()` and checks it at `Save()`; a mismatch throws `CirrusErrorException.RegistryConcurrency` so two interleaved CLI invocations can't silently overwrite each other's installs.
27c. ✅ `FileExtensionRegistryStore.Load()` enforces the §11.4 leaf-uniqueness invariant by scanning every entry's `Contributions`; duplicates across packages throw `CirrusErrorException.RegistryInvalid` with both offending package ids. Catches hand-edited corruption before it reaches the `LeafIndex`.
27d. ✅ `extension list` returns separate `Live` and `Stale` lists; stale entries include the missing path and a `Reason` so the user can decide whether to remove them.

_Checkpoint: the registry file is no longer an unchecked source of truth; missing payloads, concurrent writers, and corrupt invariants all surface as structured errors instead of weird routing._

**Phase 9 — Multi-namespace extension validation**

28. ✅ Compute extension declares `[ExtensionVerb("vm")]` + `[ExtensionVerb("disk")]` and contributes a stub `disk list` leaf via `ListDiskProcessor`. Proves the multi-namespace model end-to-end with the smallest possible payload.
29. ✅ `[ExtensionVerb]` is repeatable (`AllowMultiple = true`). `ExtensionVerbReader.ReadVerbs(...)` returns every declared verb; `Add.cs` / `Update.cs` validate each leaf's first token against the full declared set. `ExtensionHints.targets` emits a build error if any `s_cliTable` key's first token isn't declared via `[ExtensionVerb]` in the same project.
30. ✅ Tests verify: a single registry entry with multiple `Contributions` routes correctly via `LeafIndex` under both namespaces; `ExtensionResolver` resolves both verbs to the same `PackageId`; persistence roundtrip preserves both namespaces; removing the entry purges both namespaces in one operation. `extension list` exposes the full `Namespaces` array per entry.

_Checkpoint: Shape B is real end-to-end with a multi-namespace example proving it._

**Phase 10 — Polish**

31. `extension list --by-verb` rendering (uses the `namespaces` field).
32. Telemetry: add a `contributionCount` dimension to install/remove events.
33. Update [extension-framework.md](./extension-framework.md) to point at this section for the dispatch model.
34. **Symmetry between `extension add --name` and `extension remove --name`.** Today `remove` accepts either a packageId or a verb, but `add` only accepts a short packageId suffix (auto-prefixed with `Microsoft.Azure.Cirrus.Extensions.`). Make `add --name` also accept a verb by consulting the embedded hint file (`extension-hints.json`) for a verb→packageId lookup before falling back to the prefix-strip behavior. Keep `--path` unchanged.
35. **Smarter `ExtensionNotFound` hint.** When the dispatcher emits the "Install it with: `azv2 extension add --name <X>`" hint and the host has no NuGet feed configured (or the package is known to ship as a local artifact in this repo), surface the local nupkg path as the suggestion instead of `--name`. Concretely: if `artifacts/Debug/<packageId>/<packageId>.<version>.nupkg` exists, the message becomes `Install it with: azv2 extension add --path <absolute-path-to-nupkg>`. This closes the dev-loop gap where `--name` silently fails because no feed serves the package.

_Checkpoint: Shape B v1, shippable._

### 11.10 Open questions

- **Q10 (resolved): Build error when an `s_cliTable` key's first token isn't covered by an `[ExtensionVerb]` declaration.** The MSBuild target fails the build with a clear message pointing at the offending key and the extension's declared namespaces. Trade-off: developers must keep `[ExtensionVerb]` declarations in sync with table contents, but in exchange the build-time hint table is guaranteed complete — every leaf has a path that the host can suggest installing.
- **Q11: How does an extension declare a namespace it owns but has no leaves under?** E.g. a future "Networking" extension that wants to claim `network` for help text purposes but ships zero leaves itself. Probably not a real need; `[ExtensionVerb]` declarations without matching leaves currently have no effect. Worth revisiting if a use case appears.
- **Q12: Aggregated namespace help.** Punted to the help branch — Shape B just guarantees the data shape (`namespaces` field on each registry entry) needed to answer it.
