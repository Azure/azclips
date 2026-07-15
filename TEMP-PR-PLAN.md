# TEMP-PR-PLAN — Issue #511: [Extensions] Command Not Found Error Adjustments

Branch: `clips-511-cnf-error-adjustments`
Issue: <https://github.com/Azure/CLIPS/issues/511>

## 1. Symptom

```
$ az vm --list
error: [ExtensionNotFound] Verb 'vm' is provided by extension 'Microsoft.Azure.Azclips.Extensions.Compute',
which is not installed.
Install it with: az extension add --name Compute
```

Yet on the same shell in the same repo state:

```
$ az vm --help
Group
    az vm : Manage Azure virtual machines.
...
```

`az vm --help` proves the `Compute` extension IS installed and its `vm` namespace is
discoverable. The error text (`ExtensionNotFound`, "not installed", "Install it with…")
is therefore actively misleading — the real user error is a **typo / unknown leaf** inside
an already-installed group (they meant `az vm list`, not `az vm --list`).

The same misclassification fires for non-flag positional typos too — the args shape
doesn't matter, only that no `(verb, path)` leaf matches:

```
$ az group foobar
error: [ExtensionNotFound] Verb 'group' is provided by extension 'Microsoft.Azure.Azclips.Extensions.Resources',
which is not installed.
Install it with: az extension add --name Resources
```

`Resources` is installed (proven by any working `az group list`); `foobar` just isn't a
leaf under `group`. Same bug, different surface.

## 2. Where the message comes from

Thrown as `ExtensionNotInstalledException` (code `ExtensionNotFound`, exit 4):

- Definition: [src/CLI/Exceptions/ExtensionNotInstalledException.cs](src/CLI/Exceptions/ExtensionNotInstalledException.cs#L33-L36)
- Execute-path throw: [src/CLI/Parser/ExtensionDispatcher.cs](src/CLI/Parser/ExtensionDispatcher.cs#L100-L102)
- Help-path throw (mirrored UX): [src/CLI/Help/RuntimeHelpDispatcher.cs](src/CLI/Help/RuntimeHelpDispatcher.cs#L94-L96)
- Rendered by [src/CLI/Program.cs](src/CLI/Program.cs#L146-L153) via `EmitStructuredError`.

## 3. Root cause (trace of `az vm --list`)

`ExtensionDispatcher.Parse(["vm", "--list"])`:

1. `verb = "vm"`.
2. `nonFlagLeadingArgs = []` because `--list` starts with `-` and is filtered by
   [TakeWhile(a => !a.StartsWith("-"))](src/CLI/Parser/ExtensionDispatcher.cs#L73-L76).
3. Longest-prefix loop only tries `len = 0`: `LeafIndex.TryGet("vm", "")`. `Compute`
   contributes `("vm","list")`, `("vm","create")`, `("vm","show")`, `("vm","delete")` — but
   **no `("vm","")` root leaf** — so the lookup misses.
4. Falls through to `_resolver.TryResolve("vm", ...)` — an `ExtensionResolver` wired to
   `FileExtensionRegistryStore` in [src/CLI/CliExtensionServiceRegistration.cs](src/CLI/CliExtensionServiceRegistration.cs#L31-L32).
   This reads `registry.json` only — it does NOT run discovery. If `Compute` is present
   on disk but not written to `registry.json` (bundled install, stale file, hand-edited,
   etc.) this returns `false`.
5. Fallback loop consults `ExtensionHintTable.Default` at
   [ExtensionDispatcher.cs L98-L104](src/CLI/Parser/ExtensionDispatcher.cs#L98-L104).
   Hint table's embedded map has `"vm" → Compute`, so it matches on the bare verb and
   throws `ExtensionNotInstalledException("vm", "…Compute")`.

Trace of `az group foobar` (non-flag variant) — same failure path, exercises the loop
more fully:

1. `verb = "group"`, `nonFlagLeadingArgs = ["foobar"]`.
2. Longest-prefix loop: `LeafIndex.TryGet("group", "foobar")` misses (no such leaf),
   `LeafIndex.TryGet("group", "")` misses (no bare-verb leaf).
3. `_resolver.TryResolve("group", …)` misses in `registry.json`.
4. Hint-table fallback loop: `("group","foobar")` misses, then `("group","")` hits
   `Resources` on the bare-verb key — throw. So even when the leaf-key check runs, the
   final bare-verb probe still lands on the misleading "not installed" message whenever
   the hint table knows the verb.

The asymmetry that produces the bug:

| Consumer                                   | Source of truth for "installed"                                                                                             |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------- |
| `LeafIndex` (dispatcher, help)             | **Discovery** (authoritative, walks assemblies on disk — see `CliExtensionServiceRegistration.BuildLeafIndexWithDiscovery`) |
| `IExtensionResolver` (dispatcher fallback) | `registry.json` only (`FileExtensionRegistryStore.Load`)                                                                    |
| `RuntimeHelpDispatcher`                    | **Discovery** (that's why `az vm --help` works)                                                                             |

So for verbs whose extension is discovered-but-not-in-`registry.json`, the dispatcher's
"is this installed?" check disagrees with reality, and the hint table produces a
confidently-wrong "extension not installed" message.

`az vm --help` avoids the bug because `RuntimeHelpDispatcher.RenderExtensionHelp` runs
`DiscoverInstalledExtensions()` first (see [RuntimeHelpDispatcher.cs L82-L96](src/CLI/Help/RuntimeHelpDispatcher.cs#L82-L96))
and only falls back to the hint table if discovery genuinely can't find the verb.

## 4. Failure modes we need to cover

Two distinct user intents collapse into the same code path today:

- **A — Extension genuinely not installed.** `az vm list` when `Compute` was never
  installed. `ExtensionNotInstalledException` is the _correct_ message.
- **B — Extension installed, typo / unknown leaf.** `az vm --list`, or
  `az vm nope`, or `az vm scale-set` (misspelled). Today we emit A's message. We should
  emit a generic "command not found" that acknowledges the verb but calls out the bad
  leaf, and ideally points to `az vm --help`.

The issue title ("Command Not Found Error Adjustments") explicitly asks for the B branch
to be split out.

## 5. Options considered

### Option 1 — Make `IExtensionResolver` discovery-aware

Give the dispatcher's fallback resolver the same discovery-backed view the LeafIndex
already uses. Concretely: swap `ExtensionResolver` for a resolver that consults the
merged discovered+file registry (same `BuildLeafIndexWithDiscovery` snapshot).

- Pros: single source of truth; removes the asymmetry that spawned the bug.
- Cons: discovery cost repeated in the hot dispatch path; larger change; still doesn't
  distinguish "verb known but this leaf isn't" from "verb known but no subcommand given".
  The hint-table throw would still trigger for `az vm nope` if `Compute` weren't in the
  registry (subset of the bug), but not for the observed `az vm --list` case.

### Option 2 — Gate the hint-table throw on LeafIndex knowledge (RECOMMENDED)

Before `ExtensionDispatcher` throws `ExtensionNotInstalledException`, check whether
`LeafIndex` already knows about the verb (under **any** path). If yes, the extension IS
installed — surface a generic `CommandNotFound` error instead. Only fall through to the
hint-table / `ExtensionNotInstalledException` when the verb is genuinely unknown to
discovery.

Sketch:

```csharp
// In ExtensionDispatcher.Parse, replace the current hint-table loop:
if (_leafIndex.Value.HasVerb(verb))
{
    throw new CommandNotFoundException(verb, nonFlagLeadingArgs);
}

for (int len = nonFlagLeadingArgs.Length; len >= 0; len--)
{
    var path = string.Join(" ", nonFlagLeadingArgs, 0, len);
    if (ExtensionHintTable.Default.TryGetPackageId(verb, path, out var hintedPackage))
    {
        throw new ExtensionNotInstalledException(verb, hintedPackage);
    }
}
```

New API on `LeafIndex`:

```csharp
public bool HasVerb(string verb) =>
    _entries.Keys.Any(k => string.Equals(k.Verb, verb, StringComparison.OrdinalIgnoreCase));
```

(Or precompute a `HashSet<string>` at build time to keep it O(1).)

New exception in [src/CLI/Exceptions](src/CLI/Exceptions/):

```csharp
public sealed class CommandNotFoundException : AzclipsErrorException
{
    public CommandNotFoundException(string verb, IReadOnlyList<string> path)
        : base(
            code: "CommandNotFound",
            exitCode: 4,
            message: BuildMessage(verb, path),
            details: new Dictionary<string, object?>
            {
                ["verb"] = verb,
                ["path"] = string.Join(" ", path),
            }) { }

    private static string BuildMessage(string verb, IReadOnlyList<string> path)
    {
        var full = path.Count == 0 ? verb : $"{verb} {string.Join(' ', path)}";
        return
            $"'az {full}' is not a recognized command. " +
            $"Run 'az {verb} --help' to see available commands.";
    }
}
```

Mirror the same guard inside `RuntimeHelpDispatcher.RenderExtensionHelp` for symmetry —
but the help path already uses discovery, so in practice this only matters when the hint
table ships an entry the discovery didn't corroborate.

- Pros: minimal change, keeps hot path fast, cleanly separates the two failure modes,
  message points the user at the exact recovery command (`az vm --help`).
- Cons: introduces a new `code` (`CommandNotFound`) that downstream callers may want to
  assert on — per [extension-management.md](docs/spec/extension-management.md#L156) the
  `code` field is the contract, so we should list it in the doc.

### Option 3 — Have the extension itself return the not-found error

Let the flow proceed into the extension host; when it can't find a `vm` command with
`--list`, emit its own error. Rejected: the extension host currently surfaces an opaque
`KeyNotFoundException` or crashes with an ArgumentParser failure; would need identical
plumbing across every extension, and defeats the dispatcher's job of catching structural
misuse early.

## 6. Proposed plan (Option 2)

1. **Add `LeafIndex.HasVerb(string verb)`** with a precomputed verb `HashSet<string>`
   built in `LeafIndex.Build`. Unit test in
   [tests/Core.Extensions.Tests/LeafIndexTests.cs](tests/Core.Extensions.Tests/LeafIndexTests.cs).
2. **Add `CommandNotFoundException`** (`code=CommandNotFound`, `exitCode=4`) next to
   [ExtensionNotInstalledException](src/CLI/Exceptions/ExtensionNotInstalledException.cs).
3. **`ExtensionDispatcher.Parse` — verb-only fallback branch**: if the verb-only
   resolver miss AND `LeafIndex.HasVerb(verb)` returns `true`, throw
   `CommandNotFoundException(verb, nonFlagLeadingArgs)` instead of running the hint
   table. Tests in
   [tests/Cli.Tests/Extensions/ExtensionDispatcherTests.cs](tests/Cli.Tests/Extensions/ExtensionDispatcherTests.cs).
4. **`RuntimeHelpDispatcher.RenderExtensionHelp`**: mirror the same guard for
   consistency (discovery already covers most cases, but the hint-table fallback there
   has the same "verb known ≠ package known" hole).
5. **Docs**: add `CommandNotFound` to the error-code table in
   [docs/spec/extension-management.md](docs/spec/extension-management.md#L156) and
   [docs/howto/extension-management.md](docs/howto/extension-management.md#L245); note
   the split in the message-flow example around L84 / L28 respectively.
6. **CHANGELOG**: entry under "Fixed" — misleading `ExtensionNotFound` when an unknown
   leaf is invoked inside an installed group.

## 7. Verification checklist

Repro locally against a build where `Compute` is on disk:

- `az vm --list` → new `CommandNotFound`, message points at `az vm --help`. Exit 4.
- `az vm nope` → new `CommandNotFound`. Exit 4.
- `az group foobar` → new `CommandNotFound` (non-flag positional variant from repro).
- `az vm list` (valid leaf) → runs as today.
- `az vm --help` (valid help) → renders group help as today.
- `az vm list --help` (valid command help) → renders command help as today.
- After `az extension remove Compute` (or a fresh install without Compute):
    - `az vm list` → **still** the old `ExtensionNotFound` message with install hint (no
      regression to option A).
    - `az vm --help` → same, via `RuntimeHelpDispatcher`'s mirrored guard.
- `az completely-unknown-verb foo` → falls through to the generic
  `KeyNotFoundException` → `TryAgentFallbackAsync`, as today.

Automated:

- New `LeafIndex.HasVerb` tests.
- Dispatcher tests: verb-known-but-leaf-missing → `CommandNotFoundException`;
  verb-genuinely-unknown → `ExtensionNotInstalledException` (unchanged).
- Help dispatcher tests: same two branches.

## 8. Open questions

1. **Discovery cost in the resolver path.** Option 1 remains attractive long-term
   because the resolver/leaf-index split is genuinely a design smell. Worth a separate
   change once Option 2 unblocks the user-facing bug. Note in change proposal?
2. **Should `CommandNotFound` echo suggestions?** e.g. Levenshtein-nearest leaf
   (`Did you mean 'az vm list'?`). Nice-to-have; can be layered on later without another
   `code` change.
3. **Exit code.** Keeping `4` for continuity with `ExtensionNotFound` (both are
   "command discovery failed" from a scripting perspective), but arguably a distinct
   integer would help scripts branch. Defer to squad convention.
4. **Hint-table behaviour under registry hand-edits.** If a user hand-edits
   `registry.json` to remove `Compute`, `LeafIndex` (discovery-backed) still sees `vm`,
   and Option 2 would classify their `az vm list` invocation as `CommandNotFound` even
   though the _registry_ claims Compute is uninstalled. Discovery is the truthful
   signal here — the file registry is preference metadata — so treating this as
   `CommandNotFound` is arguably correct. Worth calling out in the doc.
