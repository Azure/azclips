## Upcoming Release

## v0.1.1 - 2026-08-17 00:12 UTC

#### CLI
- `az config set core.output.color`: Add `auto` | `always` | `never` for persistent color preference (#649)
- Argument parser: Use the flag or alias the user typed in error messages and verbose logs instead of the internal processor property name (#594)

#### Config
- Remove bundledExtensionsRoot from config (#676)
- Rename assemblyLoading extension-root config keys (breaking) (#668)

#### Extension
- `az extension list`: Include bundled extensions and expose on-disk `Location` field (#590)
- `az extension show`: Expose on-disk `Location` field (#590)

#### Extensions
- `extension list` displays all available candidates (#688)

#### PowerShell
- Argument parser: Route cmdlet parameters to their processor property name so parameters whose PS name differs from the backing property no longer fail with a missing-property error (#594)

#### Telemetry
- Honor `telemetry.enabled` config changes without requiring a PowerShell module reload (#592)

#### CLI

- `az extension list` now surfaces every discovered candidate per package under a `candidates` array (each entry has `version`, `tier` — `Bundled` or `Installed` — and `location`), plus a top-level `selectedVersion` field naming the version dispatch will route to. The array is sorted by SemVer descending across tiers, so users can see what versions are available before deciding to pin/unpin. `--by-verb` inherits the same shape. The existing `version` field remains for backward compatibility and equals `selectedVersion`; new consumers should prefer `selectedVersion` (#643).
- Fixed `az extension list` reporting an empty version for extensions in the Bundled tier of packaged installs. The loader now falls back to reading `AssemblyInformationalVersion` off the extension DLL via `MetadataLoadContext` when the `.nupkg` is absent (packaged installs strip it at inject time). Local Debug/Release builds keep the `.nupkg` and are unaffected (#534).
- **Behavioral change:** `az extension add`, `az extension remove`, and `az extension update` now work uniformly for every verb, including the ones that previously shipped as "Core" (`login`, `logout`, `profile`, `config`, `agent`, `extension`). `az extension update login` now upgrades the shipped Login extension in place; `az extension add MyLoginShadow` succeeds if the extension declares the `login` verb (subject to the same `(verb, path)` collision rules as any other extension). Previously the CLI refused all three operations for these verbs with a `VerbConflict` error. If you scripted around those refusals, the error path is gone (#622).
- Fixed `assemblyLoading.installedExtensionsRoot` being honored only by the loader — the config key now also drives the extension registry file location (`<root>/registry.json`) and every `az extension add/list/show/pin/remove/update` read/write path. Setting the key to a custom directory used to install DLLs there while `az extension list` kept reading the default `~/.azclips/extensions/registry.json`; installs, registry, and dispatch now agree. Default behavior (key unset) is unchanged (#629).
- Fixed tab-completion missing verbs from extensions installed via `az extension add`. `az --completion-candidates` (bash/zsh) and `az --completions` (PowerShell) now enumerate through the same loader chain the runtime uses, so installed-tier extensions surface uniformly at every depth. Previously the completion generator scanned only bundled roots (#622).
- **Breaking:** Removed `assemblyLoading.bundledExtensionsRoot` config key. Bundled extensions are now discovered from a fixed candidate root list (`<install>/Extensions` in Release, plus `artifacts/{Debug,Release}` for local dev builds). The key had no user-configurable purpose — packaging always writes bundled extensions to `<install>/Extensions/`. Any config referencing this key will now fail with "unknown key" (#630).
- **Breaking:** Renamed extension-loader configuration key `assemblyLoading.versionedInstallationDir` → `assemblyLoading.installedExtensionsRoot` to match the new Installed vocabulary. Any user config referencing the old key must be renamed manually; the old spelling will now fail with "unknown key" (#533).

## v0.1.0 - 2026-07-20 23:15 UTC

#### Resources

- `az group create`: Make SubscriptionId an optional parameter that defaults to the current subscription (#555)
- `New-AzResourceGroup`: Make SubscriptionId an optional parameter that defaults to the current subscription (#555)

## v0.0.8 - 2026-07-17 03:17 UTC

#### Compute

- `az vm list`: Fix empty output by writing the ARM list envelope value (#463)
- `az disk list`: Fix empty output by writing the ARM list envelope value (#463)

#### Login

- `az login` / `Connect-AzAccount`: Show surface-correct parameter names in validation and post-sign-in error messages (#530)
- `az login`: Select the MSAL cache account matching the active profile during silent token acquisition to avoid signing requests with a stale or unrelated account (#537)

## v0.0.7 - 2026-07-16 10:53 UTC

- No user-facing CLI or PowerShell module changes were listed for this release.

## v0.0.6 - 2026-07-16 01:40 UTC

#### CLI

- `az vm list`: Report CLI flag names instead of internal property names in missing-required-parameter errors (#461)

## v0.0.5 - 2026-07-13 00:22 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.4 - 2026-07-09 23:00 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.3 - 2026-07-09 00:15 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.2 - 2026-07-07 23:44 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.1 - 2026-07-07 00:27 UTC

- No user-facing PowerShell module changes were listed for this release.

## v0.0.0 - 2026-07-01 00:00 UTC

- Initial baseline before the first stable release.
