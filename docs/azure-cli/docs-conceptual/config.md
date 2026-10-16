---
title: Configuration management
description: Learn how to manage Azure CLI private preview configuration settings, including keys, environment variables, and file locations.
---

# Configuration management

Azure CLI private preview stores user-scope configuration in a global JSON file.

Configuration follows strict precedence: **command args > environment variables > global config > defaults**.

Configuration files accept JSON comments and trailing commas. Configuration key lookup and
updates are case-insensitive; nested properties and flat colon-delimited keys refer to the
same logical settings.

## Valid Configuration Keys

| Key                                           | Type   | Default            | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| --------------------------------------------- | ------ | ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `core.logging.level`                          | string | `warn`             | Log level: `debug`, `info`, `warn`, `error`. Diagnostics (`warn`/`info`/`verbose`/`debug`) are written to **stderr**; command results stay on stdout. See [CLI Usage](#cli-usage) below.                                                                                                                                                                                                                                                                                                                     |
| `core.output.color`                           | string | `auto`             | CLI color mode: `auto` (respect env vars and redirection), `always` (force color on), `never` (force color off). `NO_COLOR` and `FORCE_COLOR` env vars still take precedence over this setting. This only permits color and does not choose an output format: `json` and `yaml` are never colored, so use `--output jsonc`/`yamlc` (or set `clients.cli.output.format`) for colored results. CLI-scoped; PowerShell coloring is controlled by the PS host. |
| `core.http.maxRetries`                        | int    | `3`                | Max HTTP request retries                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `core.http.maxRetriesFor429`                  | int    | `5`                | Max retries for 429 (rate limit) responses                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `core.engine.extensionIndexCacheSize`         | int    | `512`              | LFU cache capacity for extension index (entry count)                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `assemblyLoading.installedExtensionsRoot`     | string | (system)           | Root directory for extensions installed via `az extension add`. Controls where DLLs land, where `registry.json` is written (as `<root>/registry.json`), and every `az extension add/list/show/pin/remove/update` read/write path. Default: `~/.azclips/extensions`. Changing this key does not migrate existing state — move `registry.json` and the `<pkg>/<version>/` payload folders manually if needed.                                                                                                  |
| `assemblyLoading.extensionFolderPrefix`       | string | (see notes)        | Extension assembly folder prefix; `Microsoft.Azure.Azclips.Extensions.` (DEBUG), empty (Release)                                                                                                                                                                                                                                                                                                                                                                                                             |
| `telemetry.enabled`                           | bool   | `true`             | Enable telemetry                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `agent.enabled`                               | bool   | `false`            | Enable AI agent fallback (opt-in: `az config set --key agent.enabled --value true`)                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `agent.provider`                              | string | `builtin`          | Agent model provider: `builtin` (default, Microsoft's AOAI deployment via APIM, requires `az login`) \| `github` (user's Copilot subscription, requires Copilot login)                                                                                                                                                                                                                                                                                                                                       |
| `agent.sessionStyle`                          | string | `oneshot`          | Agent interaction mode: `oneshot` (single plan, then stop) or `interactive` (multi-turn)                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `agent.reasoningEffort`                       | string | `medium`           | Model reasoning effort: `low`, `medium`, or `high`. Applies to both providers and interaction styles; deleting the override restores `medium`. Model support is required. |
| `agent.excludedCommands` | string (list) | (empty) | Command paths excluded from AI recovery. Comma/semicolon-separated; group entries include subcommands. Case, extra whitespace, and one leading `az` are ignored. No hidden defaults. |
| `agent.interactive.verbose`                   | bool   | `false`            | In interactive mode, also show internal tool details (get*command / find*\* args and output), not just shell commands. Off by default to keep the session clean                                                                                                                                                                                                                                                                                                                                              |
| `agent.trace.enabled`                         | bool   | `false`            | Write a JSONL trace of each agent session (events, tool I/O) for local debugging/evaluation                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `agent.trace.path`                            | string | `~/.azclips/trace` | Directory for agent trace files (used when `agent.trace.enabled` is true; created on first write)                                                                                                                                                                                                                                                                                                                                                                                                            |
| `completion.enabled`                          | bool   | `true`             | Enable `az` shell tab-completion; set `false` to stop suggestions even while the completion script stays sourced                                                                                                                                                                                                                                                                                                                                                                                             |
| `extensions.resources.arm.apiVersionOverride` | string | (empty)            | Override ARM API version                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `clients.cli.output.format`                   | string | `json`             | Default `--output` format when the flag isn't passed: `json`, `jsonc`, `table`, `tsv`, `yaml`, `yamlc`. `jsonc`/`yamlc` are the colorized variants, subject to `core.output.color`'s gating (`NO_COLOR` makes them render as plain `json`/`yaml`); `json`/`yaml` are never colored, even when color is forced. `table`/`tsv` drop nested object/array columns entirely (matches classic `az`). `--output` overrides this per invocation.                                                                     |
| `clients.cli.output.simpleFormat`             | string | `tsv`              | Default `--output` format for scalar (string/number/bool/null) results, e.g. after a `--query` that narrows to a single field. Same allowed values as `clients.cli.output.format`; that key still governs object/array results. `--output` overrides both.                                                                                                                                                                                                                                                   |
| `defaults.group`                              | string | (empty)            | Default resource group                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `defaults.location`                           | string | (empty)            | Default location                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `defaults.web`                                | string | (empty)            | Default web app name                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| `defaults.vm`                                 | string | (empty)            | Default VM name                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| `defaults.vmss`                               | string | (empty)            | Default VMSS name                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `defaults.acr`                                | string | (empty)            | Default ACR name                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |

## CLI Usage

`core.logging.level` controls runtime host logging verbosity in CLI sessions.
Use `--debug` to force debug-level output for a single invocation; this
override does not persist to config.

The CLI, configuration-aware PowerShell command streams, and no-session fallback
use the same baseline thresholds:

| Configured level | Enabled message severities                  |
| ---------------- | ------------------------------------------- |
| `error`          | Error                                       |
| `warn`           | Error, Warning                              |
| `info`           | Error, Warning, Information                 |
| `debug`          | Error, Warning, Information, Debug, Verbose |

`verbose` is a message severity, not a configured level. Runtime level parsing
ignores casing and surrounding whitespace; missing or unrecognized values use
`warn`. Configuration commands still validate values against the supported levels.

CLI invocations capture their effective level during startup, before early profile
reads. No-session background logging instead resolves the global level for each
non-error message. Errors remain reportable even if configuration cannot be read;
expected configuration-read failures in the fallback use `warn`.

### Stream routing

`az` follows the POSIX/GNU convention shared with every Azure-family and general-purpose CLI (Azure CLI Python, Az PowerShell, AWS CLI, `git`, `kubectl`, `docker`, `terraform`):

- **stdout** — command results only (JSON payloads, table rows, resource IDs). Safe to pipe.
- **stderr** — every diagnostic level: `error`, `warn`, `info`, `verbose`, `debug`. Each line is prefixed with `LEVEL: ` (uppercase word, single colon, single space) — matching Azure CLI Python's `WARNING:` output, the Python `logging` default formatter, and the way the PowerShell host renders `cmdlet.WriteWarning(...)`.

Because diagnostics never touch stdout, `az foo --output json 2>/dev/null` stays parseable even when warnings fire mid-command.
The `--output` option formats successful command results on stdout only. Errors remain
human-readable diagnostics on stderr, including when `--output json` or `--output jsonc` is used.
Use the process exit code to detect failure in automation.

The default level is `warn` — matches Azure CLI Python (console default `Warning`) and Az PowerShell (effective `Warning + Error` baseline). Under `warn`, `warn`/`error` surface on stderr and `info`/`verbose`/`debug` stay hidden; `--debug` (or `core.logging.level=debug`) opens the full stream.

Use explicit parameter names for CLI config commands:

- `--key` for configuration key
- `--value` for configuration value

### Get configuration values or sections

```bash
# Retrieve a single key
az config get --key core.logging.level

# Retrieve a section as flat entries with relative keys
az config get --key core

# Select a standard output format
az config get --key core --output table
az config get --key core --output json

# Query the structured result
az config get --key core.logging.level --query "value"
```

### Set a configuration value

```bash
# Set a key
az config set --key core.logging.level --value debug

# Set with force (allows unknown keys)
az config set --key core.logging.level --value debug --force

# Output: Set core.logging.level = debug
```

For keys with a fixed set of allowed values, invalid-value errors list the accepted
values. Free-form string keys are unaffected.

```text
az config set core.output.color adsf
ERROR: [InvalidInput] Value 'adsf' is not allowed for 'core.output.color'. Allowed values: auto, always, never.
```

Run `az config info` to discover configuration keys and their constraints.

The existing `config set` forms remain supported:

| Form | Support |
| --- | --- |
| `az config set <key> <value>` | Supported |
| `az config set <key> --value <value>` | Supported |
| `az config set --key <key> --value <value>` | Supported |
| `az config set --key <key> <value>` | Already unsupported; use one of the forms above |

Extra positional arguments are rejected. `--force` permits unknown keys for ordinary
`--value` replacement; it doesn't bypass value validation or make a key list-valued.

### Manage list-valued configuration

`agent.excludedCommands` is a string-valued list with an **empty default**. It contains command
paths excluded from AI recovery. There are no hidden default exclusions: after you enable
AI recovery, `az login` still triggers recovery for eligible errors until you add `login`.

Entries are separated by commas or semicolons. Matching uses command-token prefixes:
`profile` excludes both `profile` and `profile show`, while `config` doesn't match
`configuration`. Comparison ignores case, surrounding and extra whitespace between words,
and one optional leading `az` token. Empty entries are ignored; entries that normalize to
zero tokens, such as `az` alone, never match a command. No wildcard syntax is supported,
including `*`. If the command can't be resolved, only one-token entries are compared with
its first token; multi-token entries don't match unresolved commands.

The stored list must be a JSON string, not an array, object, number, or boolean. If only
the exclusion list can't be read or has a non-string value, recovery treats it as empty,
subject to the other recovery guards. Failure to read the agent configuration itself
still skips recovery. `--add` and `--remove` reject a malformed stored list explicitly
without changing it; use `--value` to replace it with valid text first. List inputs that
look like JSON are stored as literal strings, not coerced to other JSON types.

```azurecli
# Replace, add, or remove list entries
az config set --key agent.excludedCommands --add login
az config set --key agent.excludedCommands --value "login;profile"
az config set --key agent.excludedCommands --add "account show,logout"
az config set --key agent.excludedCommands --remove "AZ Login"

# Clear the list, or restore its empty default
az config set --key agent.excludedCommands --value ""
az config delete --key agent.excludedCommands
```

Specify exactly one of `--value`, `--add`, or `--remove`. Only schema-marked list keys accept
`--add` and `--remove`; currently, `agent.excludedCommands` is the only such key. Scalar and
unknown keys reject these operations even with `--force`.

`--value` replaces the whole list and stores the text as supplied. `--add` and `--remove`
use normalized comparisons, retain the first occurrence's spelling and insertion order,
trim entry edges, and write a comma-joined string. Duplicate additions and absent removals
are successful no-ops for those entries: exit 0, the normal `Set ...` confirmation, and no
extra warning. Unknown command entries are retained without command validation or warnings.
Adding to an unset list creates a stored value; clearing stores an empty string, while
deleting the key restores the empty default.

Additions and removals update the stored home configuration document, not merged defaults
or environment overrides. Read-modify-write operations run under the configuration mutation
lock, so concurrent successful updates are retained; concurrent writer order isn't guaranteed.
On Windows, raw and typed configuration reads retry transient sharing/lock violations, and
replacement retries sharing/lock violations or access-denied conflicts, with a ten-second
retry budget **per read or replacement**, not per CLI invocation. Writers retain the mutation
lock during replacement retries. Sequential startup and error-handling reads can make a
persistently locked CLI invocation take longer (about 30 seconds in testing). Persistent
configuration I/O failures are reported explicitly, not as successful mutations.

### Read a list-valued configuration key

A bare `az config get --key agent.excludedCommands` shows a readable list with its actual
source: `[default]` when unset or `[global config]` when stored, including a stored empty
string. An empty list explicitly says recovery runs for all commands, subject to
`agent.enabled`, existing error eligibility, and the other recovery guards.

The readable view is an exception to the built-in JSON output default, used only when
there's no explicit output format, user-configured format, or query:

| Selection | List-key output |
| --- | --- |
| No format setting or query | Readable header and one entry per line |
| Explicit `--output json` or `--output yaml` | Array of entries; `[]` when empty |
| User-set `clients.cli.output.format` | Requested format, rather than the readable default |
| Explicit `--output` with a configured format | Explicit format wins |
| `--query` | Query runs against the entry array, not a `ConfigEntry` object |

```azurecli
az config get --key agent.excludedCommands
az config get --key agent.excludedCommands --output json
az config get --key agent.excludedCommands --output yaml
az config get --key agent.excludedCommands --query "[0]" --output json
```

Arrays preserve entry spelling and stored order. Queries that select a scalar follow the
existing simple-output-format policy unless `--output` overrides it.
`az config list` is unchanged: entries contain the stored string, not the new array view.

### PowerShell list configuration

`Set-AzConfig` has mutually exclusive `Value` (default), `Add`, and `Remove` parameter sets.
Existing `-Key`/`-Value` usage remains supported; the same schema-only list and `-Force`
restrictions apply.

```powershell
Set-AzConfig -Key agent.excludedCommands -Value 'login;profile'
Set-AzConfig -Key agent.excludedCommands -Add 'account show'
Set-AzConfig -Key agent.excludedCommands -Remove 'AZ Login'
Set-AzConfig -Key agent.excludedCommands -Value ''
Get-AzConfig -Key agent.excludedCommands
```

`Get-AzConfig` still returns a structured `ConfigEntry`; the readable list and JSON/YAML
array rendering described above apply only to CLI list-key output.

### List all configuration

```bash
# List user config only
az config list

# List with defaults (shows all keys with their current values)
az config list --all

# Select a standard output format
az config list --all --output json
az config list --all --output table
```

### Delete a configuration value

```bash
# Delete a key
az config delete --key core.logging.level

# Delete with force (allows unknown keys)
az config delete --key core.logging.level --force

# Output: Deleted core.logging.level
```

### View schema information

```bash
# Output schema metadata as JSON
az config info

# Output only key names (JSON array)
az config info --keys-only
```

## Environment Variable Mappings

You can override configuration using environment variables:

| Environment Variable | Maps to              |
| -------------------- | -------------------- |
| `AZCLIPS_LOG_LEVEL`  | `core.logging.level` |

## Configuration File Location

Configuration is stored at:

- **Windows**: `%USERPROFILE%\.azclips\config.json`
- **Linux/macOS**: `$HOME/.azclips/config.json`

## Verification

### 1) Run defaults resolver test

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigurationServiceTests.ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured"
```

Expected result:

- `defaults.*` values are resolved for parameter defaults when configured

### 2) Optional full config regression pass

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Release
```

This confirms defaults handling and command-surface regressions as a bundle.

## Examples

### Workflow: Enable debug logging

```bash
az config set --key core.logging.level --value debug --force
az config get --key core.logging.level
```

### Workflow: Check all settings with defaults

```bash
az config list --all
```

### Workflow: Reset to defaults

```bash
# Delete all user-set config
az config list --query "[].key" --output tsv | while IFS= read -r key; do
  az config delete --key "$key" --force
done
```
