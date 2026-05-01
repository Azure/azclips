# Tab Autocomplete (Experimental, Bash)

## Summary

Adds an experimental, AI-powered Tab autocomplete for `azv2` in Bash. When the
user presses `<Tab>` after typing `azv2`, an interactive menu of suggested next
tokens (subcommands, options, values) is shown. Selecting an item inserts the
chosen token into the current command line.

## Design

- **Standalone helper executable** at [`src/Autocomplete`](../src/Autocomplete)
  → `azv2-autocomplete`. It is invoked **directly by the Bash completion hook**
  and never starts `azv2` or executes commands.
- **Dedicated AI agent**
  ([`AutocompleteAgent`](../src/Autocomplete/Agent/AutocompleteAgent.cs))
  configured separately from the main command agent
  ([`CloudAgent`](../src/Core/Core.Agent/CloudAgent.cs)). Mirrors `CloudAgent`'s
  shape (factory + inner `AIAgent` + `RunAsync`) but reads its own
  `AZURE_OPENAI_AUTOCOMPLETE_*` environment variables — **no fallback** to the
  main agent's variables, so autocomplete cannot accidentally hit the heavier
  command-agent deployment.
- **Separation of concerns:** the agent only calls the model;
  [`SuggestionParser`](../src/Autocomplete/Agent/SuggestionParser.cs) handles
  JSON parsing, dedup, prefix filtering, and trim;
  [`CompletionClassifier`](../src/Autocomplete/CompletionClassifier.cs) maps
  tokens to a completion type hint (`command`, `subcommand`, `option`, `value`);
  [`MenuRenderer`](../src/Autocomplete/MenuHost/MenuRenderer.cs) drives the
  interactive `/dev/tty` menu;
  [`Spinner`](../src/Autocomplete/MenuHost/Spinner.cs) shows a braille spinner
  on the line below the prompt while the model is in flight (to `/dev/tty`,
  so it survives Bash's `COMPREPLY` stdout capture).
- **Strict error surfacing.** The helper prints diagnostics to `stderr` with
  type, message, stack trace, inner exception, and a distinct exit code
  (`1`=error, `2`=no items, `3`=user cancelled, `4`=timeout). The Bash hook
  redirects stderr to `/dev/null`, but running the helper directly produces
  full diagnostics — this is the recommended way to debug Tab failures.

### Why a separate process?

Tab fires before `azv2` runs, so doing completion in-process inside `azv2`
would require starting the CLI on every keystroke. A tiny dedicated executable
is the lowest-latency option, can use a smaller model, and keeps the main CLI
unaffected.

## Activation

> The `<Tab>` activation is **provisional** while the design is validated;
> the trigger key may change in a future iteration.

Currently **Bash only**. PowerShell, Zsh, and Fish are not yet supported.

## Configuration

[`src/Autocomplete/.env`](../src/Autocomplete/.env) (template at
[`.env.example`](../src/Autocomplete/.env.example)):

```
AZURE_OPENAI_AUTOCOMPLETE_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
AZURE_OPENAI_AUTOCOMPLETE_DEPLOYMENT=gpt-4.1-mini
AZURE_OPENAI_AUTOCOMPLETE_API_KEY=YOUR-KEY
```

Recommended deployment: a fast, low-latency model such as `gpt-4.1-mini`.
Reasoning-class models (e.g. the `gpt-5*` family) tend to feel slower for this
workload because they perform an internal reasoning pass before emitting tokens
and reject `max_tokens` (they require `max_completion_tokens`).

| Environment variable                  | Default | Purpose                   |
| ------------------------------------- | ------- | ------------------------- |
| `AZV2_AUTOCOMPLETE_TIMEOUT_MS`        | 10000   | Hard cap on the LLM call. |
| `AZV2_AUTOCOMPLETE_MAX_SUGGESTIONS`   | 12      | Maximum menu items.       |
| `AZV2_AUTOCOMPLETE_MAX_OUTPUT_TOKENS` | 256     | LLM output token cap.     |

## Install / uninstall

```bash
cd src/Autocomplete
dotnet build -c Release
cp .env.example .env   # then edit
bash Shell/install_azv2_autocomp.sh
source ~/.bashrc

# Later:
bash src/Autocomplete/Shell/uninstall_azv2_autocomp.sh
source ~/.bashrc
```

## Tests

New project [`tests/Autocomplete.Tests`](../tests/Autocomplete.Tests) (xUnit)
covering:

- `SuggestionParser`: well-formed JSON, malformed JSON, missing `items`,
  empty/whitespace input, prefix filtering (case-insensitive), dedup
  (case-insensitive), values containing spaces, empty/whitespace values,
  trimming, `Display` defaulting to `Value`, explicit `Display` preserved.
- `CompletionClassifier`: command, subcommand, option (single- and
  double-dash), value (option followed by space), unknown, empty input.

Run:

```bash
dotnet test tests/Autocomplete.Tests/Autocomplete.Tests.csproj
```

All 18 tests pass.

## Repository-wide changes

- **AGENTS.md — new "Error Handling" section.** Codifies the rule that errors
  must be surfaced (logged with context or translated to a typed result/exit
  code), never swallowed silently. The Autocomplete helper has been audited to
  comply.
- **`NU1902` (GHSA-88hf-wf7h-7w4m) suppressed locally** in
  [`src/Core/Core.Telemetry/Core.Telemetry.csproj`](../src/Core/Core.Telemetry/Core.Telemetry.csproj)
  via `<NoWarn>$(NoWarn);NU1902</NoWarn>` so the solution can build. The
  Zipkin exporter is a Debug-only dev dependency; upgrading the OpenTelemetry
  stack is owned by another team and tracked separately. **No package versions
  were changed.**

## Files added

- `src/Autocomplete/` (entire folder: helper executable, agent, menu, shell scripts, prompt)
- `tests/Autocomplete.Tests/` (xUnit project + 2 test files)
- `docs/spec/autocomplete/design.md` (locked spec from earlier in this iteration)
- `changes.md` (this document)

## Files modified

- `cirrus.slnx` — added `src/Autocomplete` and `tests/Autocomplete.Tests`.
- `README.md` — new self-contained "Tab Autocomplete (Experimental)" section
  appended after "Running Tests"; no other areas touched.
- `CHANGELOG.md` — populated with the changes above.
- `AGENTS.md` — new "Error Handling" section.
- `src/Core/Core.Telemetry/Core.Telemetry.csproj` — narrow `NU1902` suppression.

## Out of scope (follow-ups)

- PowerShell, Zsh, Fish completion hosts.
- Replacing Tab with a less-overloaded trigger.
- Per-session result cache.
- Local fallback when offline / endpoint unreachable.
