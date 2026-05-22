# Design Spec: Extension Management

> **Status:** Draft. Speccing Shape A first (exclusive top-level verb ownership). Shape B (shared verb namespaces) is out of scope for this branch but called out where decisions might constrain it later.
>
> **Branch:** `feature-extension-management-shape-a`
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
18. ✅ ALC caching: `ConcurrentDictionary<(pkg, version), Lazy<ALC>>`, no eviction, `--no-cache` test hook (§5.5).

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
