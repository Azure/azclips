---
title: Configuration management
description: Learn how to manage Azure CLI private preview configuration settings, including keys, environment variables, and file locations.
---

# Configuration management

Azure CLI private preview stores user-scope configuration in a global JSON file.

Configuration follows strict precedence: **command args > environment variables > global config > defaults**.

## Valid Configuration Keys

| Key                                           | Type   | Default            | Notes                                                                                                                                                                                                                                                                                                                                                                                                       |
| --------------------------------------------- | ------ | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `core.logging.level`                          | string | `warn`             | Log level: `debug`, `info`, `warn`, `error`. Diagnostics (`warn`/`info`/`verbose`/`debug`) are written to **stderr**; command results stay on stdout. See [CLI Usage](#cli-usage) below.                                                                                                                                                                                                                    |
| `core.output.color`                           | string | `auto`             | CLI color rendering mode: `auto` (respect env vars and redirection), `always` (force color on), `never` (force color off). `NO_COLOR` and `FORCE_COLOR` env vars still take precedence over this setting. CLI-scoped; PowerShell coloring is controlled by the PS host.                                                                                                                                     |
| `core.http.maxRetries`                        | int    | `3`                | Max HTTP request retries                                                                                                                                                                                                                                                                                                                                                                                    |
| `core.http.maxRetriesFor429`                  | int    | `5`                | Max retries for 429 (rate limit) responses                                                                                                                                                                                                                                                                                                                                                                  |
| `core.engine.extensionIndexCacheSize`         | int    | `512`              | LFU cache capacity for extension index (entry count)                                                                                                                                                                                                                                                                                                                                                        |
| `assemblyLoading.installedExtensionsRoot`     | string | (system)           | Root directory for extensions installed via `az extension add`. Controls where DLLs land, where `registry.json` is written (as `<root>/registry.json`), and every `az extension add/list/show/pin/remove/update` read/write path. Default: `~/.azclips/extensions`. Changing this key does not migrate existing state — move `registry.json` and the `<pkg>/<version>/` payload folders manually if needed. |
| `assemblyLoading.extensionFolderPrefix`       | string | (see notes)        | Extension assembly folder prefix; `Microsoft.Azure.Azclips.Extensions.` (DEBUG), empty (Release)                                                                                                                                                                                                                                                                                                            |
| `telemetry.enabled`                           | bool   | `true`             | Enable telemetry                                                                                                                                                                                                                                                                                                                                                                                            |
| `agent.recovery`                              | string | `manual`           | Recovery trigger: `off` disables recovery and clears saved failures; `manual` saves failures for explicit `az troubleshoot`; `auto` also starts AI after eligible failures. |
| `agent.recovery.style`                        | string | `oneshot`          | Shared style for manual and automatic recovery: `oneshot` returns one answer; `interactive` opens a conversation. `az troubleshoot --style` overrides only that invocation. |
| `agent.recovery.historySize`                  | int    | `10`               | Maximum encrypted failure records per local state home; positive integers only. A lower limit takes effect on the next successful history write, not during list/show/completion. |
| `agent.provider`                              | string | `builtin`          | Agent model provider: `builtin` (default, Microsoft's AOAI deployment via APIM, requires `az login`) \| `github` (user's Copilot subscription, requires Copilot login)                                                                                                                                                                                                                                      |
| `agent.interactive.verbose`                   | bool   | `false`            | In interactive mode, also show internal tool details (get*command / find*\* args and output), not just shell commands. Off by default to keep the session clean                                                                                                                                                                                                                                             |
| `agent.trace.enabled`                         | bool   | `false`            | Write a JSONL trace of each agent session (events, tool I/O) for local debugging/evaluation                                                                                                                                                                                                                                                                                                                 |
| `agent.trace.path`                            | string | `~/.azclips/trace` | Directory for agent trace files (used when `agent.trace.enabled` is true; created on first write)                                                                                                                                                                                                                                                                                                           |
| `completion.enabled`                          | bool   | `true`             | Enable `az` shell tab-completion; set `false` to stop suggestions even while the completion script stays sourced                                                                                                                                                                                                                                                                                            |
| `extensions.resources.arm.apiVersionOverride` | string | (empty)            | Override ARM API version                                                                                                                                                                                                                                                                                                                                                                                    |
| `clients.cli.output.format`                   | string | `json`             | Default `--output` format when the flag isn't passed: `json`, `jsonc`, `table`, `tsv`, `yaml`, `yamlc`. `jsonc`/`yamlc` are the colorized variants, subject to `core.output.color`'s gating. `table`/`tsv` drop nested object/array columns entirely (matches classic `az`). `--output` overrides this per invocation.                                                                                      |
| `clients.cli.output.simpleFormat`             | string | `tsv`              | Default `--output` format for scalar (string/number/bool/null) results, e.g. after a `--query` that narrows to a single field. Same allowed values as `clients.cli.output.format`; that key still governs object/array results. `--output` overrides both.                                                                                                                                                  |
| `defaults.group`                              | string | (empty)            | Default resource group                                                                                                                                                                                                                                                                                                                                                                                      |
| `defaults.location`                           | string | (empty)            | Default location                                                                                                                                                                                                                                                                                                                                                                                            |
| `defaults.web`                                | string | (empty)            | Default web app name                                                                                                                                                                                                                                                                                                                                                                                        |
| `defaults.vm`                                 | string | (empty)            | Default VM name                                                                                                                                                                                                                                                                                                                                                                                             |
| `defaults.vmss`                               | string | (empty)            | Default VMSS name                                                                                                                                                                                                                                                                                                                                                                                           |
| `defaults.acr`                                | string | (empty)            | Default ACR name                                                                                                                                                                                                                                                                                                                                                                                            |

## AI recovery configuration

Trigger and style are independent. The default `manual` trigger records failed
commands without contacting AI. The default `oneshot` style returns one answer
when the user runs `az troubleshoot`. The same configured style applies when
`auto` starts recovery.

```bash
az config set --key agent.recovery --value manual
az config set --key agent.recovery.style --value interactive
az config set --key agent.recovery.historySize --value 20

# One answer for this request; the configured style remains interactive.
az troubleshoot --style oneshot

# Disable recording and AI, and clear saved failures.
az config set --key agent.recovery --value off
```

You can set these three keys in any order. Deleting `agent.recovery` restores the
default `manual` trigger without deleting the style or history size. The public
trigger key maps to the `trigger` leaf in the JSON file:

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
that shape is invalid. Existing values for `agent.enabled`,
`agent.autoRecovery.enabled`, `agent.sessionStyle`, and
`agent.failureHistory.maxEntries` can be inspected and deleted, but new writes
must use the keys above. Legacy values no longer gate recovery or change the new
defaults.

Turning recovery off does not unlock secure storage or decrypt old records.
Cleanup preserves the non-sensitive ID counter and lock so IDs cannot be reused.
If cleanup fails after `off` was saved, recovery remains off and the command
reports that cleanup is incomplete; retry rather than assuming the configuration
write was rolled back. Ordinary CLI invocations also observe off settings made
through file/environment changes. History list, show, help, and completion are
read-only and never perform cleanup.

See [AI-based error recovery](./ai-based-error-recovery.md) for terminal-aware
selection, readable IDs, secure-storage requirements, and platform limitations.

## CLI Usage

`core.logging.level` controls runtime host logging verbosity in CLI sessions.
Use `--debug` to force debug-level output for a single invocation; this
override does not persist to config.

### Stream routing

`az` follows the POSIX/GNU convention shared with every Azure-family and general-purpose CLI (Azure CLI Python, Az PowerShell, AWS CLI, `git`, `kubectl`, `docker`, `terraform`):

- **stdout** — command results only (JSON payloads, table rows, resource IDs). Safe to pipe.
- **stderr** — every diagnostic level: `error`, `warn`, `info`, `verbose`, `debug`. Each line is prefixed with `LEVEL: ` (uppercase word, single colon, single space) — matching Azure CLI Python's `WARNING:` output, the Python `logging` default formatter, and the way the PowerShell host renders `cmdlet.WriteWarning(...)`.

Because diagnostics never touch stdout, `az foo --output json 2>/dev/null` stays parseable even when warnings fire mid-command.

An explicitly enabled automatic AI session has its own output on stdout, just
like `az troubleshoot`. Its answer or startup failure is not a JSON command
result. Original command errors and history metadata remain together in one
stderr document in JSON mode; manual recovery hints never appear on stdout.

The default level is `warn` — matches Azure CLI Python (console default `Warning`) and Az PowerShell (effective `Warning + Error` baseline). Under `warn`, `warn`/`error` surface on stderr and `info`/`verbose`/`debug` stay hidden; `--debug` (or `core.logging.level=debug`) opens the full stream.

Use explicit parameter names for CLI config commands:

- `--key` for configuration key
- `--value` for configuration value

### Get configuration values or sections

```bash
# Retrieve a single key
az config get --key core.logging.level

# Output: core.logging.level = warn [default]

# Retrieve a section (text output)
az config get --key core

# Example output:
#   logging.level = warn
#   engine.extensionIndexCacheSize = 512
#   http.maxRetries = 3
#   http.maxRetriesFor429 = 5

# Retrieve a subsection as JSON
az config get --key core.logging --json

# Example output:
# {
#   "level": "warn"
# }

# Retrieve a section as nested JSON
az config get --key core --json
```

### Set a configuration value

```bash
# Set a key
az config set --key core.logging.level --value debug

# Set with force (allows unknown keys)
az config set --key core.logging.level --value debug --force

# Output: Set core.logging.level = debug
```

### List all configuration

```bash
# List user config only
az config list

# List with defaults (shows all keys with their current values)
az config list --all

# Output as JSON
az config list --all --json
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
| `AZCLIPS_AGENT_RECOVERY` | `agent.recovery` |
| `AZCLIPS_AGENT_RECOVERY_STYLE` | `agent.recovery.style` |
| `AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE` | `agent.recovery.historySize` |

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
az config list | grep -v '\[default\]' | awk '{print $1}' | while read key; do
  az config delete --key "$key" --force
done
```
