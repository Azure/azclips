# Design Spec: AI-Powered Autocomplete

## Overview

This document defines the design for `azv2` autocomplete as a dedicated interactive completion surface.

The chosen architecture is:

- **Code lives under `src`** (single repo, single source tree, shared CI and standards)
- **It builds as its own isolated process** (separate executable, separate startup)
- **It is invoked directly by the shell completion hook** (not by `azv2`)
- **It has its own AI agent** (not `CloudAgent`)
- **Iteration 1: the agent runs locally and calls a remote LLM** (e.g. `gpt-5x-nano`)

Why this shape:

- Tab completion happens **before** `azv2` is running, so any approach that boots the main CLI on every Tab pays full process startup cost
- A small dedicated process gives the best practical latency without requiring a long-lived daemon
- A dedicated agent keeps completion fast, deterministic, list-oriented, and free of confirmation/conversation behavior
- Living under `src` keeps ownership and versioning unified

This separation is intentional. Command execution assistance and tab completion have different requirements:

- Command assistance is conversational, explanatory, and may require confirmation
- Autocomplete must be fast, deterministic, list-oriented, and side-effect free

Because of that, autocomplete owns its own agent instructions, response schema, model selection, and host lifecycle.

---

## Goals

The autocomplete system must:

1. Present a menu under the current command line when completion is requested
2. Intercept Up/Down/Enter only while the menu is visible
3. Return a list of candidate tokens relevant to the current `azv2` input
4. Support a lightweight AI-backed completion path that can suggest real options
5. Remain isolated from the normal command execution path
6. Avoid accidental execution, confirmation prompts, or conversational loops during completion

---

## Non-Goals

The autocomplete system must not:

- Execute commands
- Ask follow-up questions in the terminal during completion
- Emit explanatory prose intended for end users
- Reuse the existing `prompt.md` instruction set from `Core.Agent`
- Reuse the `AgentEngine` JSON contract (`executable_command`, `question`, `confirmation`, etc.)
- Depend on the full CLI startup path if a lighter path can be used

---

## Why a Separate Agent

The existing cloud agent in `Core.Agent` is optimized for command generation and user interaction. Its design assumptions are wrong for completion:

- It supports multi-turn conversation
- It may ask clarifying questions
- It returns a single semantic response object rather than a candidate list
- It is tuned for correctness of commands, not ranking or filtering of completion tokens
- It may carry conversation history, which is undesirable for tab completion

Autocomplete has a tighter contract:

- one request
- one response
- no conversation state
- no command execution intent
- predictable, parseable candidate list

That makes a dedicated agent the correct design.

---

## Core Decision

Autocomplete will define its own AI agent inside the autocomplete process.

This agent will:

- run inside the autocomplete process (not inside `azv2`)
- use a separate instruction file
- use a separate response schema
- use a lightweight model such as `gpt-5x-nano`
- keep no persistent conversation history between completion requests
- be optimized for token suggestion rather than command authoring

This design allows the main command agent and the autocomplete agent to evolve independently.

---

## Runtime Shape

Autocomplete is a separate process, owned by this repo and built from `src`.

### Process Model

- The shell completion hook calls the autocomplete executable directly
- The autocomplete executable does **not** start `azv2`
- The autocomplete executable does **not** load any of the normal CLI startup, telemetry, or agent fallback paths
- It performs its own minimal startup, runs the agent, returns suggestions, exits

### Source Location

- code lives under `src` (target: `src/Autocomplete/...`)
- builds as its own project
- ships its own executable

### Why Not a Hidden `azv2 --autocomplete` Mode

- Tab fires before `azv2` is running
- Each Tab would pay the full `azv2` startup cost
- Even a stripped-down `azv2` mode has more baggage than a dedicated tiny binary

### Why Not a Persistent Daemon (yet)

- A long-lived process would give the best raw latency after warmup
- It is also significantly more complex (IPC, lifecycle, install)
- Iteration 1 explicitly stays with the simpler one-shot helper process
- Daemonization can be added later without changing the agent contract

### Iteration 1 Scope

- one separate executable built from `src`
- one local agent inside that executable
- the agent calls a remote LLM (`gpt-5x-nano`) directly
- no persistent process, no IPC, no shared cache yet
- no reuse of `CloudAgent` or `AgentEngine`
- shell integration via the existing Bash completion hook


---

## AI Model Strategy

Autocomplete should use a lighter and cheaper model than the main command agent.

Recommended default:

- `gpt-5x-nano`

Rationale:

- completion requests are short
- output is narrow and structured
- latency matters more than long-form reasoning quality
- autocomplete can tolerate lighter reasoning as long as the schema is strict

The model should be configurable through dedicated environment variables, for example:

- `AZURE_OPENAI_AUTOCOMPLETE_ENDPOINT`
- `AZURE_OPENAI_AUTOCOMPLETE_DEPLOYMENT`
- `AZURE_OPENAI_AUTOCOMPLETE_API_KEY`
- `AZURE_OPENAI_AUTOCOMPLETE_MODEL`

If these are absent, the system may fall back to shared Azure OpenAI endpoint configuration, but it should still use a dedicated deployment where possible.

---

## Agent Contract

Autocomplete needs a contract different from `Core.Agent`.

### Input Context

Each completion request should provide the agent with:

- shell flavor, initially `bash`
- full current command line
- cursor position
- current token prefix
- tokenized command parts
- whether the user is completing the command name, a subcommand, or an argument value
- optional static command metadata known locally
- optional auth context or Azure context when safe and cheap to retrieve

### Output Schema

The response must be strict JSON only.

Recommended schema:

```json
{
  "items": [
    {
      "value": "group",
      "kind": "command",
      "display": "group",
      "description": "Manage resource groups",
      "score": 0.98
    }
  ]
}
```

Field meanings:

- `value`: exact token to insert
- `kind`: `command`, `subcommand`, `option`, `value`, `resource`, `help`, or `other`
- `display`: text shown in the menu
- `description`: optional short explanation for richer future UI
- `score`: optional ranking signal from 0 to 1

The shell integration may initially use only `value`, but richer fields should be part of the schema from day one.

---

## Prompt Design

Autocomplete requires a dedicated instruction file, for example:

- `autocomplete.prompt.md`

The prompt should instruct the model to:

- behave as an Azure CLI autocomplete engine
- return candidate tokens only
- never ask questions
- never produce markdown
- never produce explanations outside the JSON schema
- prefer local/static commands when obvious
- return short, high-confidence lists
- avoid invented commands when confidence is low
- rank suggestions by likely next token relevance

The prompt should explicitly distinguish these cases:

1. completing top-level commands
2. completing subcommands
3. completing flags/options
4. completing argument values
5. completing Azure resource names when local or cached context is available

---

## Suggested Architecture

### Components

The autocomplete subsystem should contain its own components:

- `AutocompleteAgent`
- `AutocompletePromptProvider`
- `AutocompleteRequestBuilder`
- `AutocompleteResponseParser`
- `AutocompleteOptionRanker`
- `AutocompleteMenuHost`

### Responsibilities

#### `AutocompleteAgent`

- creates and owns the AI client for completion
- loads autocomplete-specific instructions
- sends one-shot completion requests
- returns parsed suggestion objects

#### `AutocompleteRequestBuilder`

- converts shell state into a structured completion request
- identifies the token under completion
- classifies the request type

#### `AutocompleteResponseParser`

- validates strict JSON
- rejects malformed or unsafe responses
- falls back to empty results or local completions on parse failure

#### `AutocompleteOptionRanker`

- merges local/static suggestions and AI suggestions
- deduplicates results
- applies prefix filtering and ordering rules

#### `AutocompleteMenuHost`

- owns terminal rendering and key handling
- is not AI-specific
- consumes a list of suggestions and renders the menu

This keeps AI, shell integration, and terminal UI decoupled.

---

## Request Pipeline

Iteration 1 flow:

```text
Tab pressed
  -> shell completion function runs
  -> autocomplete executable starts
  -> local parser classifies completion context
  -> autocomplete agent calls remote LLM (gpt-5x-nano)
  -> parse structured suggestion list
  -> render menu, capture selection
  -> Enter inserts selected token
  -> autocomplete executable exits
```

Later iterations may add a local pre-filter and merging with static suggestions before invoking the agent. Iteration 1 keeps the pipeline simple to validate the agent and process model first.

---

## Hybrid Completion Strategy

The long-term plan uses three tiers. Iteration 1 implements only Tier 3 to validate the AI path; Tiers 1 and 2 are added in later iterations.

### Tier 1: Static Local Suggestions (later iteration)

Use local data for:

- known top-level commands such as `login`, `group`, `help`
- known subcommands
- known options and flags

This path is fast, deterministic, and should be preferred once introduced.

### Tier 2: Dynamic Non-AI Suggestions (later iteration)

Use local runtime data for:

- cached subscriptions
- cached tenants
- locally available resource group names
- recent commands or previous selections

This path is also preferable to model calls when possible.

### Tier 3: AI Suggestions (iteration 1)

Use the dedicated autocomplete agent for the full completion request.

In iteration 1 the AI path is the primary source of suggestions. Fallbacks are limited to:

- empty list on parse failure
- empty list on timeout
- empty list on transport error

---

## Performance Requirements

Autocomplete is interactive and must prioritize latency.

Recommended targets:

- local-only completion: effectively immediate
- AI-assisted completion: target under 300 ms for a warm path
- hard upper limit before fallback: 800 ms to 1000 ms

If the AI path exceeds the timeout, the system should:

- fall back to local suggestions if available
- avoid blocking terminal interaction excessively
- never leave the terminal in raw mode on timeout/failure

---

## Safety and Reliability Requirements

Autocomplete must be side-effect free.

Requirements:

- no command execution
- no mutation of Azure resources
- no confirmation prompts
- no writes to user files except explicit install/uninstall scripts
- no conversation history persisted across completion requests unless explicitly added as a cache

Response handling must be strict:

- if JSON parsing fails, ignore the AI response
- if the model returns prose, ignore it
- if the model returns tokens that do not match the current prefix, filter them locally
- if the model returns duplicates, deduplicate locally

---

## Authentication and Azure Context

Autocomplete may optionally use Azure context to improve suggestions, but this must be controlled.

Recommended rules:

- do not require a live Azure call for basic top-level completions
- do not fail the entire completion experience because Azure context is unavailable
- when available, use cached auth context and lightweight cached resource state
- treat cloud-derived suggestions as enrichments, not prerequisites

This keeps completion resilient when the user is offline, unauthenticated, or in a partially configured environment.

---

## Configuration

Autocomplete should have its own configuration surface.

Suggested settings:

- `AutocompleteEnabled`
- `AutocompleteUseAI`
- `AutocompleteModel`
- `AutocompleteTimeoutMs`
- `AutocompleteMaxSuggestions`
- `AutocompleteCacheTtlSeconds`
- `AutocompleteUseAzureContext`

These may live in environment variables first, then graduate to formal CLI config later.

---

## Observability

Autocomplete should log and trace separately from the main command agent.

Suggested telemetry dimensions:

- completion request type
- local-only vs AI-assisted
- model name
- latency
- parse success/failure
- number of suggestions returned
- timeout/fallback count

The telemetry must avoid logging sensitive command values unless explicitly allowed.

---

## Packaging Guidance

Autocomplete code lives under `src` but builds as its own isolated executable.

Proposed layout:

- `src/Autocomplete/` (project root)
  - `Autocomplete.csproj` (separate project, separate output)
  - `MenuHost/` (terminal rendering, key handling)
  - `Agent/` (LLM client, prompt loading, request/response models)
  - `Shell/` (Bash completion install/uninstall scripts)
  - `Prompts/autocomplete.prompt.md`

Build:

- a small standalone executable, e.g. `azv2-autocomplete`
- not linked into `azv2`
- not invoked by `azv2`
- distributed alongside `azv2` but starts independently from the shell hook

The constraint is both logical and physical separation: completion must never pull in the main CLI startup path.

---

## Migration Guidance

The current `autocomp-dotnet` prototype validates the menu, key handling, and shell hook design. The migration into `src` happens in phases.

### Phase 1 (iteration 1, in scope)

- create `src/Autocomplete` as a new project
- port the working menu rendering and key interception logic from `autocomp-dotnet`
- add the autocomplete agent calling a remote LLM (`gpt-5x-nano`)
- add the dedicated prompt file and JSON response schema
- add Bash install/uninstall scripts that point at the new executable
- retire `autocomp-dotnet` once parity is reached

### Phase 2 (later)

- add local/static completion sources
- merge local and AI results
- add caching, prefix filtering, and timeout rules

### Phase 3 (later)

- evaluate a long-lived daemon model if latency demands it
- evaluate richer menu UI fields (descriptions, kinds, scores)
- evaluate additional shells beyond Bash

---

## Open Questions

These decisions remain open and should be resolved during implementation:

1. Should autocomplete query live Azure state or only use cached state?
2. Should AI completions be disabled by default when the user is not logged in?
3. Should descriptions be shown in the menu UI, or is the first version value-only?
4. Should ranking combine local deterministic weights and model-provided scores?
5. Should the autocomplete agent support shell-specific formatting in the future beyond Bash?

---

## Final Recommendation

Autocomplete is a dedicated product surface that lives under `src` but ships as its own isolated executable.

Locked-in design:

- separate process, invoked directly by the shell completion hook
- code under `src/Autocomplete`
- own AI agent (no reuse of `CloudAgent` or `AgentEngine`)
- own prompt
- own response schema
- lightweight model (`gpt-5x-nano`) called remotely from inside the autocomplete process
- iteration 1: AI-only path; local tiers added later
- no command execution, no confirmation prompts, no conversation state

This keeps command assistance and autocomplete aligned at the product level while allowing each to optimize for its actual runtime behavior, and keeps Tab latency low by avoiding any path that would require booting `azv2`.