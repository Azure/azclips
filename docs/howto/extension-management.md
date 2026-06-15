# Extension Management

How to install, list, update, pin, and remove Cirrus extensions, plus what to do when something goes wrong.

For extension **authoring**, see [Extension-Development-For-Private-Preview.md](./Extension-Development-For-Private-Preview.md). For the full design, see [docs/spec/extension-management.md](../spec/extension-management.md).

## What is an extension?

An extension is a NuGet package that contributes one or more top-level command **namespaces** to `azv2`. For example, the Compute extension contributes `vm` and `disk`, so installing it makes `azv2 vm list` and `azv2 disk create` work.

Installed extensions live under `~/.cirrus/extensions/<packageId>/<version>/` and are tracked in `~/.cirrus/extensions/registry.json`.

Built-in namespaces (`extension`, `login`, `config`) are part of the host and cannot be removed.

## Quick start

```bash
# Discover what provides a verb you don't have
azv2 vm list
# → Error: 'vm list' is provided by extension 'Microsoft.Azure.Cirrus.Extensions.Compute',
#   which is not installed. Install it with:
#     azv2 extension add --name Compute

# Install
azv2 extension add --name Compute

# Use it
azv2 vm list

# See what you have
azv2 extension list
```

## Installing — `extension add`

```bash
azv2 extension add --name <token> [--version <v>] [--feed-url <url>] [--pin] [--force]
azv2 extension add --path <path-to.nupkg> [--pin] [--force]
```

| Flag         | Purpose                                                                                                                                                                                                                                                                                                                                                   |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--name`     | Package identifier. Accepts the full id (`Microsoft.Azure.Cirrus.Extensions.Compute`) or its short suffix (`Compute`). Verbs and namespaces (e.g. `vm`, `disk`) are **not** accepted — typing one returns a usage error pointing at the owning package id. The unit of install is a package, not a verb (the Compute package ships both `vm` and `disk`). |
| `--version`  | Specific version. Defaults to feed's latest.                                                                                                                                                                                                                                                                                                              |
| `--feed-url` | Override the configured NuGet feed.                                                                                                                                                                                                                                                                                                                       |
| `--path`     | Install from a local `.nupkg` (skips feed lookup entirely).                                                                                                                                                                                                                                                                                               |
| `--pin`      | Lock this version against future `extension update`.                                                                                                                                                                                                                                                                                                      |
| `--force`    | Overwrite an existing pinned installation. The pin carries forward to the new version.                                                                                                                                                                                                                                                                    |

### Resolving `--name`

Both of these install the same package:

```bash
azv2 extension add --name Compute                                      # short suffix
azv2 extension add --name Microsoft.Azure.Cirrus.Extensions.Compute    # full id
```

Third-party packages must use the dotted form: `azv2 extension add --name Contoso.Foo`.

A verb token is rejected with a usage error:

```
> azv2 extension add --name vm
Error: [UsageError] '--name vm' is a verb, not a package identifier.
  Use '--name Compute' to install the package that provides 'vm'.
```

The same rule applies to `remove`, `show`, `update`, `pin`, and `unpin` — only package identifiers are valid `--name` selectors. Misses are enriched with a `Did you mean '--name <Package>'?` hint when the token unambiguously identifies an installed package.

### Local development install

When `--name` misses every configured feed and you're inside the `cirrus` repo, the error message includes a `--path` hint pointing at your most recent local build:

```
Error: [ExtensionNotFound] ...
A local build artifact was found. Try:
  azv2 extension add --path "C:\src\cirrus\artifacts\Debug\Microsoft.Azure.Cirrus.Extensions.Compute\Microsoft.Azure.Cirrus.Extensions.Compute.1.0.0-alpha.nupkg"
```

The probe walks up from cwd looking for `cirrus.slnx`, then globs `artifacts/Debug/<packageId>/`. It's suggestion-only — Cirrus never auto-installs from local builds.

### What `add` validates

1. Built-in verb collision (`extension`, `login`, `config`) — rejected.
2. Compatibility metadata (`[CirrusCompatibility]`) — hard fail if the candidate excludes the current `core` version.
3. Per-leaf `(verb, path)` collisions against the registry — see [Conflicts](#conflicts-on-install).

## Listing — `extension list`

```bash
azv2 extension list                  # default: one row per package
azv2 extension list --by-verb        # regrouped by namespace
azv2 extension list --output json    # structured output
```

Default output:

```
PackageId                                       Version   Namespaces             Pinned  InstalledAt
Microsoft.Azure.Cirrus.Extensions.Compute       1.1.0     vm, disk               no      2026-04-12
Microsoft.Azure.Cirrus.Extensions.Resources     1.0.0     group                  no      2026-04-12
```

`--by-verb` is useful when multiple packages contribute to the same namespace:

```
vm
  Microsoft.Azure.Cirrus.Extensions.Compute       1.1.0
  Contoso.VmPreviewExt                            0.3.0-preview
group
  Microsoft.Azure.Cirrus.Extensions.Resources     1.0.0
```

JSON output carries `live` and `stale` arrays. Stale entries are registry rows whose on-disk DLLs have gone missing (e.g. someone deleted `~/.cirrus/extensions/`). They're never dispatched to; remove them with `azv2 extension remove <name>`.

## Inspecting — `extension show`

```bash
azv2 extension show <name>
```

Shows full path, version, pin state, source feed, install date, declared namespaces, and the full `contributions` list (every `(verb, path)` the extension owns).

## Updating — `extension update`

```bash
azv2 extension update <name> [--version <v>] [--feed-url <url>] [--force]
```

Replaces the installed version in place. Pinned extensions require `--force`.

**Namespace rule:** the new version's `[ExtensionVerb]` set must match the previously installed one exactly. Adding or removing a namespace requires `remove` + `add`. This protects against silent dispatch changes when an extension's surface area shifts.

## Pinning — `extension pin` / `extension unpin`

```bash
azv2 extension pin <name>            # pin the currently installed version
azv2 extension pin <name>@1.2.0      # pin a specific version (installs it if needed)
azv2 extension unpin <name>
```

Pinned extensions are skipped by `update` unless you pass `--force`. Useful for freezing a known-good version while you upgrade other parts of the toolchain.

## Removing — `extension remove`

```bash
azv2 extension remove <name>
```

Deletes `~/.cirrus/extensions/<packageId>/` and the registry entry. Built-in namespaces (`extension`, `login`, `config`) cannot be removed.

## Compatibility metadata

Extensions can declare a supported `core` version range:

```csharp
[assembly: CirrusCompatibility(MinCoreVersion = "1.0.0", MaxCoreVersion = "2.0.0")]
```

- Both bounds optional. Missing attribute = assumed compatible.
- Read via `MetadataLoadContext` — never executes extension code.
- Enforced as a **hard fail** at install (exit code 7).
- Enforced as **warn-and-skip** at dispatch — an incompatible installed extension is dropped from the live index for the current invocation, other extensions keep working.

## Troubleshooting

### `ExtensionNotFound` (exit 4)

You typed a verb whose owning package isn't installed. The error names the package and gives an `extension add` command. If it's a local dev build, see [Local development install](#local-development-install).

### `LeafConflict` (exit 3)

A `(verb, path)` you're trying to add is already owned by another installed extension. The error enumerates **every** colliding leaf in one shot:

```
error: [LeafConflict] Extension 'Contoso.Foo' has 2 leaf conflicts with already-installed extensions.
  - leaf 'vm contoso-magic' is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'
  - leaf 'disk contoso-extra' is already owned by 'Microsoft.Azure.Cirrus.Extensions.Compute'
Remove the conflicting extension(s) first or rename the offending leaves.
```

There's no `--force` for collisions. Either `remove` the conflicting extension first, or have the extension author rename the colliding leaves. Multiple extensions sharing a namespace (same `[ExtensionVerb]`) is fine — only per-leaf collisions fail.

### `PinViolation` (exit 6)

`update` (or `add` over an existing pinned install) without `--force`. Either `extension unpin <name>` first, or rerun with `--force` (which carries the pin forward).

### `CompatibilityViolation` (exit 7)

The extension's `[CirrusCompatibility]` range excludes your `core` version. Either upgrade Cirrus, or install a different version of the extension.

### `RegistryConcurrency` (exit 8)

Two `azv2` invocations modified the registry simultaneously. The mtime guard rejected the second writer. Just retry.

### `RegistryInvalid` (exit 9)

The on-disk `registry.json` violates the leaf-uniqueness invariant — usually from a hand-edit. The error names both offending packages. Restore from a backup or `extension remove` one of them by editing the file directly, then rerun.

### Stale extensions in `extension list`

A row appears under `stale` when its install directory is missing. Run `azv2 extension remove <name>` to clean up the registry.

### Force a fresh assembly load — `--no-cache`

Cirrus caches loaded extension assemblies per `(packageId, version)` for the life of the process. Long-running hosts (TUI) benefit; one-shot CLI invocations are unaffected. To force a fresh load (debugging, benchmarking):

```bash
azv2 --no-cache vm list
```

Or set the config / env var to disable caching globally:

```bash
azv2 config set --key assemblyLoading.bypassExtensionCache --value true
# or
$env:CIRRUS_NO_EXTENSION_CACHE = "true"
```

## Where things live

| Path                                                  | Contents                                                                                         |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `~/.cirrus/extensions/registry.json`                  | Installed-extension manifest (schema v2). Authoritative.                                         |
| `~/.cirrus/extensions/<packageId>/<version>/`         | Extracted DLLs for one installed version.                                                        |
| `azv2.dll` (embedded resource `extension-hints.json`) | Build-time catalog of first-party verb → package mappings. Used for the unknown-verb suggestion. |

## Exit code reference

| Code | Meaning                              |
| ---- | ------------------------------------ |
| 0    | Success                              |
| 1    | Generic error                        |
| 2    | Usage error (bad args, unknown flag) |
| 3    | `LeafConflict`                       |
| 4    | `ExtensionNotFound`                  |
| 5    | `FeedUnavailable`                    |
| 6    | `PinViolation`                       |
| 7    | `CompatibilityViolation`             |
| 8    | `RegistryConcurrency`                |
| 9    | `RegistryInvalid`                    |

Programmatic callers should assert on the JSON `error.code` field, not the human-readable message.
