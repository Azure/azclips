# Azclips Architecture: Azure CLI and PowerShell with AI Agent Fallback

> ⚠️ **DEPRECATED (agent sections only) — the AI-fallback description below no
> longer matches the code.** The current agent runs on the **GitHub Copilot SDK**
> (not Azure OpenAI / GPT-4o-mini via `.env`, not a `CloudAgent` / `AgentEngine`,
> not LlamaSharp/Phi-3.5), interacts through the structured **`propose_plan`** and
> **`submit_response`** tools (not the `sample_command` / `executable_command` /
> `question` JSON response types), and runs as a **standalone `azclips-agent`
> process** spawned by `az` via `TryAgentFallbackAsync` (`src/CLI/Program.cs`) only
> when a command **fails** — in `oneshot` mode it proposes a fix and exits without
> running anything, in `interactive` mode it asks for per-command approval. There
> is **no** free-form natural-language command entry (`az "…"`) and no
> `resilv/merge-cli` branch. For the actual implementation see
> `Agent/ChatAgent.cs`, `Agent/Hosting/ProposePlanTool.cs`,
> `Agent/Hosting/SubmitResponseTool.cs`, and `Agent/AgentMode/AgentModeEntry.cs`.
> The **non-agent** sections (extension system, DLL loading, engine) are unaffected
> by this note.

## Overview

Azclips is a modular Azure CLI tool with **AI-powered fallback** for natural language commands, typo correction, and "how to" questions. The architecture uses:

1. **Exception-Driven Fallback** - Standard command parsing in try-catch, AI agent invoked on exception
2. **Extension System** - Dynamic module loading via DLL discovery (Accounts.dll, Resources.dll)
3. **Azure OpenAI Integration** - CloudAgent with GPT-4o-mini for natural language interpretation

**Note:** This document describes the **actual implementation** on the `resilv/merge-cli` branch, not idealized designs.

## CLI command timing

`PerformanceTimeline` provides one monotonic origin for a CLI invocation. Startup
measurements replay into existing telemetry spans; live measured activities share
their start/end offsets with the local debug report. A UTC anchor translates the
offsets into telemetry timestamps without using wall-clock differences as durations.
Replay is once-only, and local-only phases do not create telemetry activities.
PowerShell startup callers retain the existing `Measure`/`Replay` interface.

CLI-owned `CommandTiming` formats the final `[CommandTiming]` table through a
caller-supplied writer, without knowing log levels or output-stream policy.
`Program` retains the invocation's level-gated debug writer once normal logging is
initialized and supplies it after provider disposal. Reporting is independent of
telemetry enablement or listeners. It does not export telemetry tags or command
parameters into the timing table.

Applicable boundaries include init/startup, routing and requested-command resolution,
selected-extension loading, index construction, dispatch preparation, actual awaited
processor invocation, output, failure handling/recovery, and observed finalization.
Engine timing is opt-in via the CLI-registered timeline; it does not depend on
individual extensions publishing processor start/end events. Preparation includes
its nested loading/index intervals. Cached indices do not force a new load.
Help measures full discovery separately from projection, rendering and output.
Completion timing is deferred: both completion modes retain their existing
early-return protocol, do not configure the timing report, and do not start the
ordinary command telemetry root.

Rows are inclusive, ordered by phase creation, and nested by async execution flow.
Uninstrumented time is the managed total minus the union of measured intervals,
not the sum of overlapping durations. Scope completion means the observed interval
ended, not that the operation succeeded; the summary carries the final exit code.

Debug measurements can continue after the telemetry command root ends, including
awaited agent recovery and provider disposal. They do not extend the root across
recovery or create spans after the provider has been disposed. The total stops
before summary emission and excludes pre-entry native/runtime startup, later runtime
teardown, and detached uploader completion. Internal uploader, failure-history worker,
and print-probe modes do not emit user-command summaries. Startup failures before
effective logging policy is available may have no report.

## Key Architectural Principles

| Aspect                  | Current Implementation                                                                                    |
| ----------------------- | --------------------------------------------------------------------------------------------------------- |
| **Entry Point**         | `Program.cs` with try-catch around `ExtensionDispatcher` → AI fallback on exception                       |
| **Command Routing**     | `ExtensionDispatcher` → `CommandGroupIndex` → DLL loading → `ICommand` execution                          |
| **AI Fallback**         | `TryAgentFallbackAsync()` creates `AgentEngine` + `CloudAgent` on demand (max 5 retries)                  |
| **Agent Integration**   | Azure OpenAI Service (GPT-4o-mini) with streaming, configured via .env file                               |
| **Flavor Detection**    | `AZURECLI_FLAVOR` environment variable for potential integration scenarios                                |
| **Module Discovery**    | File-based DLL loading from extensions directory at runtime                                               |
| **Response Types**      | `sample_command` (placeholders), `executable_command` (user confirms), `question`, `confirmation`, `exit` |
| **Streaming UI**        | Spinner characters (`⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏`) for "Thinking..."                                                       |
| **Interactive Prompts** | ✅ CLI prompts user for confirmation with AI-interpreted natural language responses                       |

## Architecture Comparison: Theoretical vs. Actual

### Theoretical Design (Not Implemented)

- ❌ `OrchestratorEngine` actively routing to `DefaultCommandEngine` → `AgentEngine`
- ❌ Agent setting `ExtractedCommand` property to hand back to command engine
- ❌ Unified Business/ layer shared by Commands and Cmdlets

### Actual Implementation (Current)

- ✅ `Program.Main()` try-catch around extension dispatcher
- ✅ Exception triggers `TryAgentFallbackAsync()` with retry logic
- ✅ Agent outputs JSON responses parsed by `AgentEngine.ExecuteCommandAsync()`
- ✅ Extensions are standalone DLLs with commands

## Entry Points

### Azure CLI

```bash
# Structured commands (extension routing)
$ az group create --name mygroup --location eastus

# When a structured command FAILS and the agent is enabled, the failure is
# handed to the standalone azclips-agent, which proposes a fix to review.
# (There is no free-form `az "natural language"` entry point — see the
# DEPRECATED note at the top of this document.)
```

**What the Bootstrapper does:**

1. Creates `ServiceCollection` and registers all dependencies as Singletons
2. Registers services: `ILogger`, `IOutputAdapter`, `CommandParser`, `CommandBinder`
3. Registers engines: `DefaultCommandEngine`, `AgentEngine`, `OrchestratorEngine`
4. Returns fully configured `ICommandEngine` (which is actually `OrchestratorEngine`)

```powershell
# Direct cmdlet and function execution
Connect-AzAccount -SubscriptionId <subscription-id> -TenantId <tenant-id>
New-AzResourceGroup -Name mygroup -Location eastus
```

**Note:** PowerShell modules provide direct cmdlet/function execution without AI agent integration.

## CLI Architecture with Exception-Driven Fallback

### Azure CLI Flow

```
┌─────────────────────────────────────────────────────────────┐
│              User Input: "az <command>"                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │    Program.Main()         │
         │  try {                    │
         │    ExtensionDispatcher    │
         │  } catch {                │
         │    TryAgentFallbackAsync  │
         │  }                        │
         └───────────┬───────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
   ✅ Success                  ❌ Exception
        │                         │
        ↓                         ↓
┌────────────────┐    ┌───────────────────────────┐
│ Execute DLL    │    │ TryAgentFallbackAsync()   │
│ Command        │    │ (max 5 retries)           │
│ Return 0       │    └────────┬──────────────────┘
└────────────────┘             │
                               ↓
                    ┌──────────────────────┐
                    │   AgentEngine        │
                    │   .ExecuteAsync()    │
                    └──────────┬───────────┘
                               │
                               ↓
                    ┌──────────────────────┐
                    │   CloudAgent         │
                    │   (Azure OpenAI)     │
                    │   Stream responses   │
                    └──────────┬───────────┘
                               │
                  ┌────────────┴────────────┐
                  │                         │
        sample_command           executable_command
        (placeholders)           (complete values)
                  │                         │
                  ↓                         ↓
         ┌────────────────┐    ┌────────────────────┐
         │ Display help   │    │ Prompt user:       │
         │ Exit 0         │    │ "Execute? (yes/no)"│
         └────────────────┘    └─────────┬──────────┘
                                         │
                                    ┌────┴────┐
                                    │ User    │
                                    │ Answer? │
                                    └────┬────┘
                               ┌─────────┴─────────┐
                               │                   │
                             "yes"               "no"
                               │                   │
                               ↓                   ↓
                   ┌───────────────────┐  ┌──────────────┐
                   │ Recursive az      │  │ Cancelled    │
                   │ call with command │  │ Exit 0       │
                   └───────────────────┘  └──────────────┘
```

### Extension Routing (Success Path)

```
┌─────────────────────────────────────────────────────────────┐
│           ExtensionDispatcher.Parse(args)                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  CommandGroupIndex        │
         │  LoadFromDirectory()      │
         │  • Find Group.dll         │
         │  • Find Accounts.dll      │
         │  • Find Resources.dll     │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  Load Assembly            │
         │  Find ICommandInvocation  │
         │  implementations          │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  ICommandInvocation       │
         │  .Process(logToConsole)   │
         │  Returns: JSON string     │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  Console output           │
         │  Return 0                 │
         └───────────────────────────┘
```

## Component Relationships

### File Locations

| Component                    | File Path                                                  | Description                                   |
| ---------------------------- | ---------------------------------------------------------- | --------------------------------------------- |
| **CLI Entry Point**          | `src/CLI/Program.cs`                                       | Try-catch fallback to agent                   |
| **Extension Dispatcher**     | `src/CLI/Parser/ExtensionDispatcher.cs`                    | Parse args, load DLLs                         |
| **Command Group Index**      | `src/CLI/Parser/CommandGroupIndex.cs`                      | DLL discovery and loading                     |
| **Agent Engine**             | `src/Core/Core.Engine/AgentEngine.cs`                      | Conversation loop, streaming UI               |
| **Cloud Agent**              | `src/Core/Core.Agent/CloudAgent.cs`                        | Azure OpenAI integration                      |
| **Prompt Template**          | `src/Core/Core.Agent/prompt.md`                            | System instructions with {{flavor}}           |
| **PowerShell Modules**       | `src/PS/`                                                  | PowerShell module source                      |
| **CLI Help Markdown**        | `src/CLI/Help/Docs/<ext>/*.md`                             | Authored CLI help source (frontmatter + body) |
| **PowerShell Help Markdown** | `src/PS/<Module>/help/*.md`                                | Authored PowerShell help (PlatyPS → MAML)     |
| **Help Runtime Provider**    | `src/CLI/Help/MarkdownCommandHelpProvider.cs`              | Runtime markdown parsing for CLI              |
| **Help Tool**                | `tools/Help/Azclips.Help.Tool/`                             | Build-time validators + manifest emitter      |
| **Accounts Extension**          | `artifacts/*/Microsoft.Azure.Azclips.Extensions.Accounts/`     | Authentication commands                       |
| **Resources Extension**      | `artifacts/*/Microsoft.Azure.Azclips.Extensions.Resources/` | Resource management commands                  |

### Dependency Graph

```
Program.cs
    │
    ├─► ExtensionDispatcher ──► CommandGroupIndex ──► DLL Loading
    │                                                      │
    │                                                      ↓
    │                                              ICommandInvocation
    │                                                   .Process()
    │
    └─► TryAgentFallbackAsync ──► AgentEngine ──► CloudAgent
                                       │                  │
                                       │                  ↓
                                       │          Azure OpenAI Service
                                       │          (GPT-4o-mini)
                                       │
                                       ├─► Spinner UI (⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏)
                                       └─► Parse JSON responses
```

## Core Components

### 1. Program.cs - Exception-Driven Entry Point

**Location:** `src/CLI/Program.cs`

**Responsibilities:**

- Try `ExtensionDispatcher` to parse and route commands
- Catch all exceptions and call `TryAgentFallbackAsync()`
- Detect `AZURECLI_FLAVOR` environment variable
- Create `AgentEngine` + `CloudAgent` on demand (not pre-initialized)
- Handle max 5 retry attempts for agent conversations

**Actual Implementation:**

```csharp
static async Task<int> Main(string[] args)
{
    try
    {
        var dispatcher = new ExtensionDispatcher();
        ICommandInvocation invocation = dispatcher.Parse(args);
        string result = Engine.Engine.GetCommand(invocation).Process(true);
        Console.WriteLine(result);
        return 0;
    }
    catch (Exception)
    {
        return await TryAgentFallbackAsync(args);
    }
}
```

**Key Design Decision:** Simple exception-driven fallback, no orchestrator pattern. Any failure in extension routing triggers AI agent.

### 2. ExtensionDispatcher - Command Routing

**Location:** `src/CLI/Parser/ExtensionDispatcher.cs`

**Responsibilities:**

- Parse command-line arguments into command groups
- Use `CommandGroupIndex` to load extension DLLs
- Return `ICommandInvocation` for execution
- Throw exceptions on parse errors or unknown commands

**Key Methods:**

- `Parse(string[] args)` - Returns `ICommandInvocation` or throws
- Uses environment variable `EXTENSIONS_ROOT_DIRECTORY` for DLL discovery

**Example Flow:**

```
args: ["group", "create", "--name", "test"]
→ CommandGroupIndex.LoadFromDirectory()
→ Find Group.dll
→ Load assembly, find matching command
→ Return ICommandInvocation
→ Call .Process(true) to execute
```

### 3. CommandGroupIndex - DLL Discovery

**Location:** `src/CLI/Parser/CommandGroupIndex.cs`

**Responsibilities:**

- Scan extensions directory for DLL files
- Load assemblies dynamically
- Build index of command groups → DLL paths
- Throw `KeyNotFoundException` if command group not found

**Discovery Pattern:**

```
artifacts/Release/Microsoft.Azure.Azclips.Extensions.Accounts/Microsoft.Azure.Azclips.Extensions.Accounts.dll → "login", "logout", "profile" verbs
artifacts/Release/Microsoft.Azure.Azclips.Extensions.Resources/Microsoft.Azure.Azclips.Extensions.Resources.dll → "group", "deployment", "rest" verbs
```

### 4. AgentEngine - Conversation Loop

**Location:** `src/Core/Core.Engine/AgentEngine.cs`

**Responsibilities:**

- Implement `ICommandEngine` interface
- Stream user messages to `CloudAgent`
- Display spinner UI (⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏ rotating characters)
- Parse AI JSON responses (`type` field: `sample_command`, `executable_command`, `question`, `confirmation`, `exit`)
- Handle multi-turn conversations (up to 5 exchanges)
- Detect CLI flavor and output appropriate syntax
- For PowerShell: Output `__PSEXEC__<command>` marker

**Key Response Handling:**

```csharp
// Parse JSON response
var response = JsonSerializer.Deserialize<AgentResponse>(aiMessage);

switch (response.Type)
{
    case "sample_command":
        // Display help, no execution
        Console.WriteLine(response.Command);
        break;

    case "executable_command":
        // Prompt user for confirmation
        Console.Write("Execute this command? (yes/no): ");
        string answer = Console.ReadLine();
        if (answer.ToLower() == "yes")
        {
            // Recursive az call
            await ExecuteExtractedCommandAsync(response.Command);
        }
        break;

    case "question":
        // Multi-turn conversation
        Console.WriteLine(response.Question);
        string userAnswer = Console.ReadLine();
        await SendToAgent(userAnswer);
        break;
}
```

**Three Agent Scenarios:**

1. **Typo Correction**: Returns `sample_command` with corrected syntax
2. **"How To" Questions**: Returns `sample_command` with placeholders
3. **Natural Language Execution**: Returns `executable_command` with complete values

### 5. CloudAgent - Azure OpenAI Integration

**Location:** `src/Core/Core.Agent/CloudAgent.cs`

**Responsibilities:**

- Load .env file from agent directory
- Create Azure OpenAI client with streaming
- Load prompt template from `prompt.md`
- Replace `{{flavor}}` with "azure cli" or "azure powershell"
- Stream completions token-by-token
- Return complete AI messages

**Configuration (.env file):**

```env
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Prompt Template (prompt.md):**

- System instructions for CLI
- JSON response schema definitions
- Examples for each scenario type
- Placeholder rules (`<angle-brackets>` indicate user-specific values)

### 6. Prompt Template - System Instructions

**Location:** `src/Core/Core.Agent/prompt.md`

**Key Instructions:**

- Detect user intent (typo, how-to question, or execution request)
- Return JSON with `type` field
- Generate `az` commands
- Use placeholders for unknown values: `<resource-group-name>`
- Distinguish between help requests and execution requests

**Example Response Types:**

```json
// Typo correction or how-to
{
  "type": "sample_command",
  "explanation": "Here's how to create a resource group:",
  "command": "az group create --name <name> --location <location>"
}

// Natural language execution
{
  "type": "executable_command",
  "explanation": "I'll create a resource group named 'mygroup'.",
  "command": "az group create --name mygroup --location westus"
}

// Need more info
{
  "type": "question",
  "question": "What location would you like for the resource group?"
}
```

### 7. TryAgentFallbackAsync - Retry Logic

**Location:** `src/CLI/Program.cs`

**Responsibilities:**

- Create `AgentEngine` and `CloudAgent` instances
- Detect `AZURECLI_FLAVOR` environment variable
- Load prompt template with flavor replacement
- Execute agent conversation with max 5 retries
- Handle conversation loops until completion or cancellation

**Key Logic:**

```csharp
static async Task<int> TryAgentFallbackAsync(string[] args)
{
    string flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR")
                    ?? "azure cli";

    var cloudAgent = await CloudAgent.CreateAsync(flavor);
    var agentEngine = new AgentEngine(cloudAgent, flavor);

    string userInput = string.Join(" ", args);
    await agentEngine.ExecuteCommandAsync(userInput, maxTurns: 5);

    return 0;
}
```

## Extension Module Structure

### Group Extension (Example)

**Location:** `artifacts/Release/Group/Group.dll`

**Structure:**

- DLL loaded dynamically by `CommandGroupIndex`
- Implements `ICommandInvocation` interface
- Contains command logic for resource group operations
- Returns JSON strings for console output

**Commands:**

- `group create` - Create resource group
- `group list` - List resource groups
- `group show` - Show resource group details
- `group delete` - Delete resource group

### Accounts Extension

**Location:** `artifacts/Release/Microsoft.Azure.Azclips.Extensions.Accounts/Microsoft.Azure.Azclips.Extensions.Accounts.dll`

**Commands:**

- `login` - Authenticate to Azure
- `logout` - Sign out
- `profile list` / `profile show` / `profile set` / `profile remove` - Manage named connection profiles

### Resources Extension (PowerShell)

**Location:** `artifacts/Release/Microsoft.Azure.Azclips.Extensions.Resources/Microsoft.Azure.Azclips.Extensions.Resources.dll`

**Contains:**

- PowerShell cmdlets: `New-AzResourceGroup`, `Get-AzResourceGroup`, etc.
- Note: Cmdlets use the `Az` noun prefix to match Azure PowerShell naming

## Help System

Command help lives in two parallel corpora: CLI help under `src/CLI/Help/Docs/<ext>/*.md` (read by the runtime provider on the cold path), and PowerShell help under `src/PS/<Module>/help/*.md` (compiled to MAML XML via PlatyPS at build time). Build-time tooling validates both corpora against live `ExtensionIndex` / cmdlet metadata and emits `HelpManifest.json` plus per-module `*.dll-Help.xml` artifacts.

Four validators gate every PR: markdown structure, completeness against routed commands, CLI example syntax (command path + flag signature + value type), and PowerShell example syntax (PSL1–PSL7: cmdlet name + parameter signature + required-parameter coverage + duplicate-flag check + variable-assignment check + value-type check + per-fence documented-cmdlet enforcement). Stale help fails CI before merge.

For the design philosophy, validator details, and authoring schema, see [help-system.md](help-system.md). For the author workflow, see [eng-docs/howto/help.md](../howto/help.md).

## Example Execution Flows

### Example 1: Structured CLI Command (Success)

```bash
$ az group create --name mygroup --location eastus
```

**Flow:**

1. `Program.Main()` → Try block
2. `ExtensionDispatcher.Parse(args)` → Returns `ICommandInvocation`
3. `Engine.GetCommand(invocation).Process(true)` → Executes command
4. Group.dll creates resource group via Azure SDK
5. Returns JSON result
6. `Console.WriteLine(result)` → Display to user
7. Return 0 (success)

**Agent NOT invoked** - structured command succeeded.

---

### Example 2: CLI with Typo (AI Correction)

```bash
$ az grup create --name mygroup --location eastus  # Typo: "grup"
```

**Flow:**

1. `Program.Main()` → Try block
2. `ExtensionDispatcher.Parse(args)` → Throws `KeyNotFoundException` ("grup" command group not found)
3. Catch block → `TryAgentFallbackAsync(args)`
4. `AgentEngine` created, sends to `CloudAgent`
5. Azure OpenAI recognizes "grup" → "group" typo
6. Returns `sample_command` JSON: `az group create --name <name> --location <location>`
7. Display corrected syntax to user
8. Return 0

**Agent corrected typo and showed proper syntax.**

---

### Example 3: Natural Language CLI (Interactive)

```bash
$ az create a resource group called mygroup in westus
```

**Flow:**

1. `Program.Main()` → Try block
2. `ExtensionDispatcher.Parse(args)` → Fails (natural language, not structured command)
3. Catch block → `TryAgentFallbackAsync(args)`
4. `AgentEngine` streams to `CloudAgent`
5. Azure OpenAI understands intent
6. Returns `confirmation` JSON requesting user confirmation
7. `AgentEngine` prompts user
8. User responds with natural language (e.g., "yes", "sure", "go ahead")
9. Agent interprets intent and returns `executable_command`
10. Recursive call: `az group create --name mygroup --location westus`
11. Extension executes command
12. Return 0

**Agent converted natural language → structured command → execution.**

## Three Agent Scenarios (Detailed)

### Scenario 1: Typo Correction

**User Input:** Command with spelling errors

**Agent Behavior:** Recognizes intent, provides corrected syntax

**Response Type:** `sample_command` with placeholders

**Example:**

```bash
$ az I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
az login --use-device-code
```

**Outcome:** User sees help text, no execution.

---

### Scenario 2: "How To" Questions

**User Input:** Queries starting with "how do I", "how can I", "show me how"

**Agent Behavior:** Provides command examples with placeholders

**Response Type:** `sample_command` with placeholders

**Example:**

```bash
$ az how do I create a resource group

⠋ Thinking...

To create a resource group:

az group create --name <resource-group-name> --location <location>

# Examples:
az group create --name myResourceGroup --location eastus
az group create --name prodResourceGroup --location westus2
```

**Outcome:** User manually copies and fills in placeholders.

---

### Scenario 3: Natural Language Execution

**User Input:** Complete intent with specific values

**Agent Behavior:** Generates fully executable command

**Response Type:** `executable_command` (no placeholders)

**Example:**

```bash
$ az create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
az group create --name mygroup --location westus

Execute this command? (yes/no): yes

⠋ Executing...

{
  "id": "/subscriptions/.../resourceGroups/mygroup",
  "location": "westus",
  "name": "mygroup",
  "properties": {
    "provisioningState": "Succeeded"
  }
}
```

**Outcome:** CLI prompts for confirmation and executes after user approval.

## Key Architectural Decisions

### ✅ Exception-Driven Fallback

**Decision:** Use try-catch around extension dispatcher, invoke agent on any exception.

**Rationale:**

- Simple implementation (no intent detection logic needed)
- Catches all failure cases: unknown commands, typos, parse errors, natural language
- Clear separation: structured commands vs. AI fallback

**Trade-off:** AI agent invoked even for genuine errors (e.g., bad parameters), but agent can explain the error helpfully.

---

### ✅ Subprocess PowerShell Integration

**Decision:** PowerShell calls `az` CLI as subprocess with `__PSEXEC__` marker protocol.

**Rationale:**

- Reuses CLI's agent logic (no duplication)
- PowerShell gets correct flavor-aware responses
- Simpler than embedding agent directly in PowerShell module

**Trade-off:** Cannot do interactive prompts in PowerShell (stdin not available in subprocess). Commands auto-execute after display.

**Workaround:** Users can ask "how to" questions to get examples without execution.

---

### ✅ Flavor Detection via Environment Variable

**Decision:** Use `AZURECLI_FLAVOR` environment variable set by PowerShell.

**Rationale:**

- Simple, reliable detection mechanism
- PowerShell sets it, CLI reads it
- Allows prompt template to return correct syntax

**Values:**

- `"azure cli"` (default) → Returns `az` commands
- `"azure powershell"` → Returns PowerShell cmdlets

---

### ✅ JSON Response Protocol

**Decision:** Agent returns JSON with `type` field for different response types.

**Rationale:**

- Structured parsing (vs. free-text with markers)
- Clear distinction between help and execution
- Supports multi-turn conversations (`question` type)

**Response Types:**

1. `sample_command` - Help text with placeholders
2. `executable_command` - Ready to execute
3. `question` - Agent needs more info
4. `confirmation` - Dangerous operation warning
5. `exit` - User cancelled

---

### ✅ Streaming UI with Spinner

**Decision:** Display rotating spinner (⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏) during AI thinking.

**Rationale:**

- Immediate feedback (agent is working)
- Better UX than silent wait
- PowerShell uses `Write-Progress` for native progress bar

**Implementation:**

- CLI: Spinner characters to stdout
- PowerShell: Regex filter to hide spinner, show progress bar instead

---

### ✅ Extension DLL Loading

**Decision:** Load commands from DLL files at runtime (Group.dll, Accounts.dll, Resources.dll).

**Rationale:**

- Modular architecture (add new commands without recompiling CLI)
- Clear separation of concerns
- Standard .NET assembly loading

**Discovery:** Scan `artifacts/Release/*/` or `EXTENSIONS_ROOT_DIRECTORY` for DLLs.

## Testing Strategy

### CLI Tests

**Location:** `tests/Cli.Tests/`

**Test Classes:**

1. **ExtensionDispatcherTests** (10 tests)
    - Constructor behavior with environment variables
    - Parse method validation
    - Error handling for unknown command groups

2. **ProgramFallbackTests** (5 tests)
    - Verify fallback NOT invoked for help flags (`--help`, `-h`)
    - Verify fallback IS invoked for natural language
    - Verify fallback IS invoked for typos
    - Note: Some tests skipped (require Azure OpenAI credentials)

**Running Tests:**

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj
# Or run all tests:
dotnet test
```

### Manual Verification

For tests requiring Azure OpenAI:

1. Set up `.env` file in `src/Core/Core.Agent/`
2. Run CLI manually: `./artifacts/Release/Core.Cli/az how do I create a resource group`
3. Verify spinner, AI response, command output

## Configuration

### Environment Variables

| Variable                    | Purpose                      | Default              |
| --------------------------- | ---------------------------- | -------------------- |
| `AZURECLI_FLAVOR`           | Customize execution behavior | `"azure cli"`        |
| `EXTENSIONS_ROOT_DIRECTORY` | DLL discovery path           | `artifacts/Release/` |

### .env File (Azure OpenAI)

**Location:** `src/Core/Core.Agent/.env`

**Required Values:**

```env
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

**Note:** `.env` file is .gitignored. Copy `.env.example` and fill in your credentials.

### Prompt Template

**Location:** `src/Core/Core.Agent/prompt.md`

**Contains:**

- System instructions for AI agent
- `{{flavor}}` placeholder (replaced at runtime)
- JSON response schema
- Examples for each scenario type
- Rules for placeholder usage

## Limitations & Known Issues

### ❌ No Unified Business Layer

**Reason:** Extensions are standalone DLLs, not shared library

**Impact:** Potential code duplication between extensions

**Future:** Could create shared Azclips.Core library for common logic

---

### ❌ Agent Retry Logic Limited to 5 Turns

**Reason:** Prevent infinite loops in multi-turn conversations

**Impact:** Complex queries may hit limit before resolution

**Workaround:** User can re-run command to start new conversation

## Build and Deployment

### Build Process

**Build Command:**

```bash
dotnet build -c Release azclips.slnx
```

**Steps:**

1. Restore NuGet packages
2. Build solution to `artifacts/Release/`
3. Extensions built to their respective directories
4. CLI executable generated as `az`

### Directory Structure After Build

```
artifacts/
  Release/
    Core/
      Core.Abstractions.dll
      Core.Authentication.dll
      Core.Engine.dll
      Core.Implementation.dll
    Core.Cli/
      az                                            ← CLI executable
      az.deps.json
      az.runtimeconfig.json
      prompt.md
      Resources.psd1                                  ← PowerShell Resources module
      Resources.psm1
      runtimes/
    Microsoft.Azure.Azclips.Extensions.Accounts/
      Accounts.psd1                                      ← PowerShell Accounts module
      Accounts.psm1
      Microsoft.Azure.Azclips.Extensions.Accounts.dll
    Microsoft.Azure.Azclips.Extensions.Resources/
      Resources.psd1
      Resources.psm1
      Microsoft.Azure.Azclips.Extensions.Resources.dll
```

### Running the CLI

```bash
# After build
./artifacts/Release/Core.Cli/az group create --name test --location eastus

# Or add to PATH
export PATH="$PATH:/workspaces/azclips/artifacts/Release/Core.Cli"
az --help
```

### Using PowerShell Modules

PowerShell modules are available in `src/PS/` and can be used directly:

```powershell
# Import modules from source
Import-Module ./src/PS/Accounts/Accounts.psd1
Import-Module ./src/PS/Resources/Resources.psd1

# Use cmdlets and functions
Connect-AzAccount -SubscriptionId <subscription-id> -TenantId <tenant-id>
New-AzResourceGroup -Name test -Location eastus
```

**Note:** The Accounts module requires the solution to be built first so that `Accounts.dll` is available in the artifacts directory.

## Future Enhancements

### Potential Improvements

1. **Shared Business Library**
    - Create `Azclips.Core.Business` NuGet package
    - Extensions reference it instead of duplicating logic
    - Consistent behavior across all extensions

2. **Better Test Coverage**
    - Mock Azure SDK calls for unit testing
    - Integration tests with test Azure subscription
    - Better handling of coverage in subprocess scenarios

3. **Extended Agent Capabilities**
    - Multi-step command sequences ("create RG then storage account")
    - Context awareness (remember previous commands in session)
    - Cost estimation before execution
    - Command explanation ("what does this command do?")

4. **Telemetry and Analytics**
    - Track agent invocation frequency
    - Identify common typos for better corrections
    - Measure user satisfaction (did they execute suggested command?)

5. **Local Model Support**
    - Fallback to local LLM (Phi-3.5) when offline
    - Faster responses for simple queries
    - Privacy-conscious option for sensitive environments

## References

### Documentation

- **Agent Experience:** [agent-experience.md](agent-experience.md) - Three scenarios (typo correction, how-to, execution)
- **Help System:** [help-system.md](help-system.md) - Markdown-as-contract design, validator pipeline, authoring schema
- **Help Authoring:** [../howto/help.md](../howto/help.md) - Step-by-step guide for adding help content
- **Repository Guidelines:** [AGENTS.md](../../AGENTS.md) - Project structure, coding style, commit guidelines

### Code Locations

| Component            | Path                                    |
| -------------------- | --------------------------------------- |
| CLI Entry            | `src/CLI/Program.cs`                    |
| Extension Dispatcher | `src/CLI/Parser/ExtensionDispatcher.cs` |
| Agent Engine         | `src/Core/Core.Engine/AgentEngine.cs`   |
| Cloud Agent          | `src/Core/Core.Agent/CloudAgent.cs`     |
| Prompt Template      | `src/Core/Core.Agent/prompt.md`         |
| PowerShell Modules   | `src/PS/`                               |
| CLI Tests            | `tests/Cli.Tests/`                      |

### External Resources

- [Azure OpenAI Service Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure SDK for .NET](https://learn.microsoft.com/dotnet/azure/)
- [PowerShell Module Manifests](https://learn.microsoft.com/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest)
