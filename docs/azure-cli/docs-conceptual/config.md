---
title: Configuration management
description: Learn how to manage Azure CLI private preview configuration settings, including keys, environment variables, and file locations.
---

# Configuration management

Azure CLI private preview stores user-scope configuration in a global JSON file.

Configuration follows strict precedence: **command args > environment variables > global config > defaults**.

On Windows, the CLI enables ANSI processing for console stdout and stderr so that
help headings and colored output render in standalone Windows consoles as well as
Windows Terminal. This does not change the color policy or enable styling for
redirected output: `core.output.color`, `NO_COLOR`, and `FORCE_COLOR` still control
whether styling is emitted. If Windows rejects console initialization on either
stream, the CLI writes a warning to stderr and uses plain output for that
invocation, even if `core.output.color=always` or `FORCE_COLOR` requests styling.

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
| `agent.recovery.trigger`                              | string | `off`              | Recovery trigger: `off` disables recovery and clears saved failures; `manual` saves failures for explicit `az troubleshoot recover`; `auto` also starts AI after eligible failures. |
| `agent.recovery.style`                        | string | `oneshot`          | Shared style for manual and automatic recovery: `oneshot` returns one answer; `interactive` opens a conversation. `az troubleshoot recover --style` overrides only that invocation. |
| `agent.recovery.historySize`                  | int    | `10`               | Maximum failure records per local state home; positive integers only. Without OS-protected key storage, the latest failure is kept unencrypted and readable only by the current user. A lower limit takes effect on the next successful history write, not during list/show/completion. |
| `agent.provider`                              | string | `builtin`          | Agent model provider: `builtin` (default, Microsoft's AOAI deployment via APIM, requires `az login`) \| `github` (user's Copilot subscription, requires Copilot login)                                                                                                                                                                                                                                                                                                                                       |
| `agent.reasoningEffort`                       | string | `medium`           | Model reasoning effort: `low`, `medium`, or `high`. Applies to both providers and interaction styles; deleting the override restores `medium`. Model support is required. |
| `agent.interactive.verbose`                   | bool   | `false`            | In interactive mode, also show internal tool details (get*command / find*\* args and output), not just shell commands. Off by default to keep the session clean                                                                                                                                                                                                                                                                                                                                              |
| `agent.trace.enabled`                         | bool   | `false`            | Write a JSONL trace of each agent session (events, tool I/O) for local debugging/evaluation                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `agent.trace.path`                            | string | `~/.azclips/agent/trace` | Directory for agent trace files (used when `agent.trace.enabled` is true; created on first write)                                                                                                                                                                                                                                                                                                                                                                                                            |
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

## AI recovery configuration

Trigger and style are independent. The default `off` trigger saves nothing and
never contacts AI. The `manual` trigger records failed commands without
contacting AI until the user runs `az troubleshoot recover`. The default `oneshot` style returns one answer
when the user runs `az troubleshoot recover`. The same configured style applies when
`auto` starts recovery.

```bash
az config set --key agent.recovery.trigger --value manual
az config set --key agent.recovery.style --value interactive
az config set --key agent.recovery.historySize --value 20

# One answer for this request; the configured style remains interactive.
az troubleshoot recover --style oneshot

# Disable recording and AI, and clear saved failures.
az config set --key agent.recovery.trigger --value off
```

You can set these three keys in any order. Deleting `agent.recovery.trigger` restores the
default `off` trigger and clears saved failures, without deleting the style or history size.
The keys are stored as nested JSON:

```json
{
  "agent": {
    "recovery": {
      "trigger": "manual",
      "style": "oneshot",
      "historySize": 10
    }
  }
}
```

Do not replace the `recovery` object with a scalar such as `"recovery": "manual"`;
that shape is invalid. If the file already contains that shape, edit the file
directly to replace it with the object above or remove the invalid node. Config
commands reject the malformed file rather than overwriting its contents.
The earlier keys `agent.enabled` and `agent.sessionStyle` were removed. Azure
CLI deletes them from the configuration file the next time the Agent starts, without
converting their values, and rejects new writes to them.

Turning recovery off does not unlock secure storage or decrypt old records.
If cleanup fails after `off` was saved, the command still succeeds and prints a
warning; run `az config set --key agent.recovery.trigger --value off` again to retry.
Only `az config set` and `az config delete` for `agent.recovery.trigger` (or a parent
key) perform cleanup. An off value set by editing the file stops new saves, but it
does not clear existing history.

See [AI-based error recovery](./ai-based-error-recovery.md) for terminal-aware
selection, readable IDs, secure-storage requirements, and platform limitations.

## Built-in Agent endpoint

When `agent.provider` is `builtin`, the Agent sends model requests to a regional
gateway. The Agent always chooses the gateway from your tenant's data boundary:

| Tenant data boundary | Gateway |
| -------------------- | ------- |
| `Global` | East US (`azclis-copilot-apim-prod-eus.azure-api.net`) |
| `EU` | West Europe (`azclis-copilot-apim-prod-weu.azure-api.net`) |
| Not known | West Europe (`azclis-copilot-apim-prod-weu.azure-api.net`) |

The boundary is "not known" when the lookup fails or times out. It is also not
known for managed identity sign-in, because that profile has no tenant ID.

The Agent uses the data boundary it looks up at recovery startup. It reads the
per-tenant boundary cache first. On a cache miss, it makes one Azure Resource
Manager call with a three-second time limit.

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

An explicitly enabled automatic AI session has its own output on stdout, just
like `az troubleshoot recover`. Its answer or startup failure is not a JSON command
result. Original command errors and manual recovery hints are written as text to
stderr in every output format; they never appear on stdout.

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
dotnet test --project tests/Cli.Tests/Cli.Tests.csproj -c Release --filter-method "*ConfigurationServiceTests.ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured"
```

Expected result:

- `defaults.*` values are resolved for parameter defaults when configured

### 2) Optional full config regression pass

```bash
dotnet test --project tests/Cli.Tests/Cli.Tests.csproj -c Release
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
