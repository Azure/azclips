---
title: AI-based error recovery in Azure CLI (Private preview)
description: Learn how AI-based error recovery in Azure CLI analyzes command failures, identifies likely root causes, and provides guided troubleshooting steps.
---

# AI-based error recovery in Azure CLI (Private preview)

AI-based error recovery is a built-in capability in Azure CLI that helps you resolve command
failures more efficiently.

AI-based error recovery is off by default. When you turn it on, Azure CLI saves command failures
in a private encrypted history on your machine. The original error remains visible. Use
`az troubleshoot analyze` when you want an integrated AI model to analyze a saved failure and provide
guided troubleshooting.

> [!IMPORTANT]
> Encrypted capture requires accessible OS-protected storage. Windows uses DPAPI.
> If that storage is unavailable, only the latest failure is saved without encryption
> and the CLI shows a warning. See [Local encrypted failure history](#local-encrypted-failure-history).
> Linux requires an already running Secret Service on the local user-session
> D-Bus and an existing, unlocked default keyring. Recovery never starts that
> service or requests an unlock/authorization prompt. macOS uses a dedicated
> Keychain adapter with user interaction disabled during key access.
> Real desktop/keyring validation is still pending; offline tests are not
> evidence of complete cross-platform recovery support.

The AI model can:

- Analyze the error message
- Identify the likely root cause
- Suggest corrective actions
- Provide a guided plan for resolution

The AI-generated response is designed to be clear and actionable. It typically includes the
following sections:

- **What went wrong:** Explains the error and its likely cause.
- **Plan or next actions:** Recommends steps to resolve the issue.
- **Values to review:** Identifies parameters or inputs that you should review.
- **Disclaimer:** Reminds you that AI-generated content may be inaccurate.

One-shot analysis works in interactive terminals and headless environments. Interactive sessions
require terminal input and output; use `--style oneshot` when redirecting either stream.

The goal is to reduce debugging time, improve command success rates, and provide a more guided
command-line experience.

> [!NOTE]
> AI-based error recovery is currently available only for Azure CLI (`az`) commands. Azure
> PowerShell workflows aren't supported during the private preview.

## Enable or disable AI-based error recovery

The default is **off**: no failures are saved and AI is never called. Set `manual` to save
eligible failures locally and call AI only when you run `az troubleshoot analyze`. Set `auto` to also
start AI automatically after each eligible failure. Recovery trigger and interaction style are
independent settings:

| Setting | Values | Default |
| --- | --- | --- |
| `agent.recovery.trigger` | `off`, `manual`, `auto` | `off` |
| `agent.recovery.style` | `oneshot`, `interactive` | `oneshot` |
| `agent.recovery.historySize` | Positive integer | `10` |

Use manual recovery:

```azurecli
az config set --key agent.recovery.trigger --value manual
```

Disable capture and analysis, and clear the local failure history:

```azurecli
az config set --key agent.recovery.trigger --value off
```

View the current setting:

```azurecli
az config get --key agent.recovery.trigger
```

Manual recovery does **not** automatically call AI. After a failure is securely saved, the CLI
prints a hint containing its record ID:

```text
To troubleshoot this error with AI, run: az troubleshoot analyze --id <failure-id>
Or run az troubleshoot analyze without --id to troubleshoot the latest error in this terminal.
```

If secure persistence fails, the CLI reports that it could not save the record and does not print
an unusable ID. The original error and exit code remain unchanged. In auto mode, a storage
failure does not by itself prevent an otherwise eligible automatic diagnosis.

## Analyze a saved failure

Analyze the latest saved failure in this terminal, falling back to the global latest if this
shell has no matching record:

```azurecli
az troubleshoot analyze
```

List saved failures and choose a specific one:

```azurecli
az troubleshoot list --output table
az troubleshoot show --id <failure-id> --output table
az troubleshoot analyze --id <failure-id>
```

An unknown, invalid, or evicted ID is an error; it never silently selects a different record.

Manual and automatic diagnosis use the same original command/error inputs, prompt, tools,
and approval policy for a given interaction style. Both use `agent.recovery.style`. Override the
style for one manual invocation without changing configuration:

```azurecli
az troubleshoot analyze --id <failure-id> --style interactive
```

`az troubleshoot list` and `az troubleshoot show --id` expose exactly `id`, `command`, and `errorDetail`. The command is the
original `FailedCommand`, not just a command name. Only the list's error detail is normalized to
a single line and limited to 200 characters. `show` preserves full text and line breaks, even
with narrow table output.

`--output` and `--query` apply to these local views, not to a machine-readable AI plan.
`show` requires an exact ID. `--style` applies only to `analyze`. Invalid or duplicate arguments
fail before starting AI.

List, show, and help never start AI or run history cleanup. List/show decrypt existing records but never create a
new key. After a successful off/reset, list is empty and an old exact ID no longer exists.
Selecting a record does not replay the failed command or restore its old profile or working
directory. Diagnosis uses the current Agent configuration and its existing approval policy.

## Enable automatic recovery

Automatic recovery is disabled by default. To also start AI after eligible command failures:

```azurecli
az config set --key agent.recovery.trigger --value auto
```

The original error is shown first, then the AI plan.

Turn it off without disabling explicit troubleshooting:

```azurecli
az config set --key agent.recovery.trigger --value manual
```

> [!IMPORTANT]
> The earlier keys `agent.enabled` and `agent.sessionStyle` were removed. Azure CLI deletes them
> from your configuration file the next time the Agent starts, and does not convert their values. To use
> AI recovery again, set `agent.recovery.trigger` to `manual` or `auto`, and set `agent.recovery.style`
> if you used the interactive style.

Use the public configuration keys shown above. See [Configuration Management][01] for the
configuration-file representation.

## Local encrypted failure history

The history belongs to the current **local operating-system user**, not to a shared service.
It retains complete received command arguments and original error details, which can include
credentials. The data is encrypted with a key protected by Windows DPAPI, macOS Keychain,
or the Linux Secret Service. The recovery-specific Unix adapters do not use the MSAL
helpers that can request Keychain/keyring authorization. Unavailable or locked storage
never displays UI or replaces an existing key. Saving then stores the latest failure
without encryption, and reads that need the key report an error.
No Azure login is required just to save local history.

### When protected storage is unavailable

Some environments have no usable OS key storage. Examples are an SSH session without a
desktop keyring, a locked or missing Linux keyring, or an older operating system.
In these cases the CLI does the following:

- It keeps the earlier encrypted failures and saves **only the latest failure** without
  encryption in the same history file. Each new failure replaces the earlier unencrypted one.
- Only the current user can read and write the file. On Linux and macOS the file mode
  is `0600`. On Windows the file has its own access list that grants only the current user.
- It prints a warning on stderr after the failed command, for every `--output` format.

While protected storage is unavailable, `az troubleshoot list` and `show` report that the key
is unavailable when the history contains encrypted failures. `az troubleshoot analyze` without `--id` uses the unencrypted latest failure
when it is the newest one. The next save with protected storage available deletes the
unencrypted failure. `az config set --key agent.recovery.trigger --value off` also deletes it.

If protected storage is available but the key no longer exists, for example after the
keychain was reset, the CLI creates a new key. Earlier encrypted failures can't be read
with it; they are skipped and removed on the next save.

On Linux, `DBUS_SESSION_BUS_ADDRESS` must identify a local Unix socket. If it is absent,
the CLI uses the `bus` socket beneath `XDG_RUNTIME_DIR`; it does not discover a session
through X11 or connect to a TCP bus. In a headless environment without an accessible
Secret Service, only the latest failure is saved without encryption, as described above.
Configure and unlock your keyring separately when needed; recovery itself does not do so.

On macOS, the adapter uses no-UI Keychain queries and temporarily disables Keychain
interaction for the current process, restoring its prior setting afterward. Capture
runs in the isolated history worker. Local views finish key access before any Agent
startup; they do not unlock or create a keychain.

The history is kept in `.azclips/agent/failure-history` in the local application data folder and respects `AZCLIPS_HOME`. The default
limit is the latest **10** records:

```azurecli
az config set --key agent.recovery.historySize --value 10
```

The limit must be a positive integer. Zero does not mean unlimited. Lowering the limit prunes old
records on the next successful history write; listing and showing details do not
prune. Later successful commands neither remove old failures nor mark them resolved. Switching between manual and auto
preserves records. Setting `agent.recovery.trigger=off`, or deleting the key, clears the history
without decrypting it or unlocking native key stores. On Windows it also deletes the key file. On
macOS and Linux the key stays in the Keychain or Secret Service and is reused when recovery is
turned on again. The setting is saved even if
clearing fails; in that case the command prints a warning, and you can run the same off command again.

Successful commands and normal user cancellation do not add records. Request timeouts still
count as failures unless you also cancel the invocation. Troubleshoot, completion, and
Agent-internal invocations do not create additional failure history.

Record IDs have the form `<PID>-<8 random lowercase hexadecimal digits>`, such as
`42-cfd9ddb4`. Each record also stores its time (UTC) and a per-home sequence. Lists group
by numeric PID ascending, then time and sequence descending; default selection instead uses
the latest successfully persisted sequence.

Terminal association uses the original CLI process's parent-shell PID. It assumes the shell
launches the CLI directly; Unix wrappers should use `exec`. Extra wrapper processes, PID reuse,
and a state directory shared across hosts or PID namespaces can affect association. If the
current parent PID cannot be resolved, no-ID analysis warns and uses the global latest.

A history file that can't be read is treated as empty history and replaced on the next save.
To start over, set `agent.recovery.trigger=off`, then re-enable manual or auto. List, show, and
help never perform that reset themselves.

> [!NOTE]
> Local encryption protects stored data; it does not redact the diagnostic input supplied to AI.
> Explicit analysis sends the selected record's original command and error to your configured
> provider, not the entire history. Only use this feature with input you are permitted to send
> to that provider. Existing opt-in diagnostic agent traces have their own data-handling behavior.

## Configure the AI provider

By default, AI-based error recovery uses Microsoft's built-in AI provider.

Alternatively, you can configure Azure CLI to use your GitHub Copilot subscription.

Use the built-in provider:

```azurecli
# Requires az login
az config set --key agent.provider --value builtin
```

Use GitHub Copilot:

```azurecli
# Requires copilot login
az config set --key agent.provider --value github
```

## Choose the interaction style

The default interaction style is one-shot: generate a recovery response and exit.
`agent.recovery.style` applies to both manual and automatic diagnosis. Changing the trigger
does not change the style, and a manual `--style` override applies only to that invocation.

To use a multi-turn interactive session that remains open until you type `exit`, run:

```azurecli
az config set --key agent.recovery.style --value interactive
```

To return to the default one-shot experience:

```azurecli
az config set --key agent.recovery.style --value oneshot
```

## Configure reasoning effort

`agent.reasoningEffort` controls the reasoning effort requested from the model. The default is
`medium`; supported configuration values are `low`, `medium`, and `high`.

For example, to request low reasoning effort:

```azurecli
az config set --key agent.reasoningEffort --value low
```

To restore the default, remove the override:

```azurecli
az config delete --key agent.reasoningEffort
```

An absent setting or a deleted override resolves to `medium`, which is explicitly sent to the
Copilot SDK. The setting applies to both one-shot and interactive sessions, with either provider.
Invalid values, including an explicit `null`, are rejected without replacing the previous setting.
Use `config delete` to restore the default. If the selected model or provider doesn't support the requested
effort, recovery reports the SDK/provider error rather than silently changing the effort.

## Frequently asked questions

### What does AI-based error recovery do?

AI-based error recovery analyzes command failures and provides:

- A root cause explanation
- Suggested fixes
- Step-by-step recovery guidance

Instead of displaying only raw error output, it helps you understand what went wrong and how to
resolve the issue.

### How is this different from existing error messages?

Traditional CLI error messages often:

- Are cryptic or incomplete
- Point to symptoms instead of root causes
- Offer little or no remediation guidance

AI-based error recovery:

- Translates errors into plain language
- Identifies the likely root cause
- Suggests actionable next steps

### What kinds of issues can it handle?

AI-based error recovery can assist with issues such as:

- Invalid arguments or parameters
- Missing required inputs
- Azure service or API errors, such as unavailable SKUs or policy restrictions

In some cases, the AI can query Azure APIs to suggest valid alternatives, such as available SKUs.

### Is the feature enabled by default?

No. AI-based error recovery is off by default, and no failures are saved.

To save failures and get AI help when you run `az troubleshoot analyze`, run:

```azurecli
az config set --key agent.recovery.trigger --value manual
```

To also start AI diagnosis automatically after each failure, run:

```azurecli
az config set --key agent.recovery.trigger --value auto
```

### Does it require authentication?

AI analysis requires the configured provider's authentication. Local history recording, listing,
and showing details do not require Azure sign-in, but they follow the normal command
startup, which may contact Azure when you are signed in. If analysis cannot authenticate,
it reports an actionable error without deleting the selected failure.

The required sign-in depends on the configured AI provider:

- **builtin** (default) requires `az login`.
- **github** requires `copilot login`.

### Which model powers this feature?

The built-in provider uses a model that Microsoft selects and may update.

You can also configure Azure CLI to use your GitHub Copilot subscription:

```azurecli
az config set --key agent.provider --value github
```

### Who pays for AI usage?

Use of the built-in AI provider is currently included at no additional cost.

If you configure Azure CLI to use your GitHub Copilot subscription, costs depend on your
subscription plan and usage.

### What data is used and collected?

AI-based error recovery emits usage telemetry in the same distributed trace as the
failed command. The command, recovery, and Agent session use parent-child links.
The trace records:

- How long recovery ran, and how long each phase took
- How many conversation turns and tool calls occurred
- The configured AI provider and the effective interaction style for this invocation
- Whether recovery was explicitly requested or automatically triggered
- The model name and the exit code
- Parent-child links between the failed command, recovery, and Agent session

It does **not** record your prompts, the error text, the model's response, or the values
of any command parameters.

Generating a plan is not evidence that you adopted it or that the original problem was repaired.
This feature does not track subsequent commands to infer plan adoption or execution success.

To turn all of it off:

```azurecli
az config set --key telemetry.enabled --value false
```

This setting disables telemetry, not the separately encrypted local history. Use `agent.recovery.trigger=off`
to stop new history records and AI calls and clear local failure history.

Separately, if you want a local record of the model input and output for debugging or auditing, enable tracing:

```azurecli
az config set --key agent.trace.enabled --value true
```

When tracing is enabled, each session writes a JSONL trace file to the location specified by
`agent.trace.path`. The file contains the model input and output and remains on your local machine.

For more information, see [Configuration Management][01].

### Can I rely completely on the AI suggestions?

No. AI-generated content may be inaccurate.

Always review the suggested recovery plan and values before running commands, especially before
deleting resources, resizing resources, or changing permissions.

### How can I provide feedback?

During the private preview, feedback is collected through user interviews and surveys.

<!-- link references -->

[01]: ./config.md
