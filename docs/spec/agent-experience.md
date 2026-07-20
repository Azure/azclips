# Agent Experience Design Specification

> ⚠️ **DEPRECATED — this document describes an earlier agent design that no longer
> matches the code.** The current agent runs on the **GitHub Copilot SDK** (not
> Azure OpenAI / GPT-4o-mini / LlamaSharp), interacts through the structured
> **`propose_plan`** and **`submit_response`** tools (not the `sample_command` /
> `executable_command` / `question` JSON response types described below), and runs
> as a **standalone `azclips-agent` process** spawned by `az` on a failed command —
> in `oneshot` mode it proposes a fix and exits without running anything, in
> `interactive` mode it asks for per-command approval. There is **no** free-form
> natural-language command entry (`az "…"`), and **no** `Invoke-AzAI` / `ask` /
> `pls` PowerShell aliases. For the actual implementation see
> `Agent/Hosting/ProposePlanTool.cs`, `Agent/Hosting/SubmitResponseTool.cs`,
> `Agent/AgentMode/AgentModeEntry.cs`, and the `Agent/prompts/oneshot.md` /
> `Agent/prompts/inline.md` prompts. This file is kept only for historical context.

## Overview

Core.Agent is an AI-powered natural language interface for Azure CLI and Azure PowerShell that interprets user intent and generates executable commands. It operates as an **exception-driven fallback**: when standard command parsing fails, control passes to the agent to interpret the natural language input.

The agent supports three interaction scenarios:
1. **Typo Correction** - Recognizes misspelled commands and suggests correct syntax
2. **"How To" Questions** - Provides command examples with placeholders (not executable)
3. **Natural Language Execution** - Generates complete executable commands from natural language requests

**Key Behavioral Note:**
Both Azure CLI and Azure PowerShell support interactive confirmation. When the agent generates an executable command, it prompts the user for confirmation. The user can respond with natural language (e.g., "yes", "sure", "go ahead", "no", "cancel"), and the agent uses a language model to interpret their intent - not a hard-coded yes/no check.

**Note:** For comprehensive architecture details including fallback mechanisms and PowerShell subprocess integration, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Architecture

### Integration Architecture

#### Azure CLI Flow

The CLI uses an **exception-driven fallback** pattern in `Program.cs`:

```
┌──────────────────────────────────────────────────────────┐
│     User Input: "az <command>"                         │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
         ┌───────────────────────────────┐
         │      Program.Main()           │
         │   try { ... } catch { ... }   │
         └───────────────┬───────────────┘
                         │
            ┌────────────┴────────────┐
            │                         │
            ▼                         ▼
  ┌──────────────────┐    ┌──────────────────────┐
  │ ExtensionDispatcher│   │   AgentEngine        │
  │  (Parse & Route)   │   │ (AI Fallback)        │
  └────────┬───────────┘   └──────────┬───────────┘
           │                          │
           │ Success                  │ Exception → Fallback
           ▼                          │
  ┌──────────────────┐                │
  │ Execute via DLL  │                │
  └──────────────────┘                │
                                      ▼
                          ┌────────────────────────┐
                          │   CloudAgent (Azure    │
                          │   OpenAI GPT-4o-mini)  │
                          └──────────┬─────────────┘
                                     │
                        ┌────────────┴─────────────┐
                        │                          │
                        ▼                          ▼
            ┌───────────────────┐      ┌──────────────────────┐
            │ sample_command    │      │ executable_command   │
            │ (with placeholders)│     │ (user confirms)      │
            └───────────────────┘      └──────────┬───────────┘
                                                   │
                                                   ▼
                                       ┌──────────────────────┐
                                       │ Recursive az call    │
                                       └──────────────────────┘
```

#### Azure PowerShell Flow

PowerShell uses a **subprocess pattern** calling the CLI:

```
┌──────────────────────────────────────────────────────────┐
│     User Input: "ask how do I create a resource group"   │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
         ┌───────────────────────────────┐
         │   Invoke-AzAI Function        │
         │   (AgentHelper.ps1)           │
         └───────────────┬───────────────┘
                         │
                         ▼
         ┌───────────────────────────────┐
         │  Call: az <user query>        │
         │  (as subprocess)              │
         │  Set: AZURECLI_FLAVOR=        │
         │       "azure powershell"      │
         └───────────────┬───────────────┘
                         │
                         ▼
         ┌───────────────────────────────┐
         │   Stream stdout line-by-line  │
         │   Filter: ⠋ Thinking...       │
         │   Show: Write-Progress        │
         └───────────────┬───────────────┘
                         │
            ┌────────────┴────────────┐
            │                         │
            ▼                         ▼
  ┌──────────────────┐    ┌──────────────────────┐
  │  Regular Output  │    │  __PSEXEC__<command> │
  │  (Write-Host)    │    │  marker detected     │
  └──────────────────┘    └──────────┬───────────┘
                                     │
                                     ▼
                          ┌────────────────────────┐
                          │  Invoke-Expression     │
                          │  $commandToExecute     │
                          └────────────────────────┘
```

**Key Differences:**
- **CLI**: Direct execution in same process
- **PowerShell**: Subprocess architecture, uses `__PSEXEC__` marker for command execution
- **Both**: Support interactive prompts with AI-interpreted natural language confirmation

### Deployment Options

The agent supports three AI backend implementations:

1. **Azure OpenAI** (default) - Cloud-based, production-grade responses
2. **Local Model** (Phi-3.5-mini) - Fully offline inference via LlamaSharp
3. **Azure Copilot Service** - Azure's production Copilot infrastructure

### Interface Support

The agent is triggered from the Azure CLI when an `az` command fails; it inspects
the failed command and error and proposes a fix for the user to review. (The
free-form entry points and PowerShell `Invoke-AzAI` / `ask` / `pls` aliases
described in earlier drafts of this document no longer exist — see the DEPRECATED
note at the top.)

## Agent Capabilities

The agent supports three interaction scenarios, with behavioral differences between CLI (interactive) and PowerShell (non-interactive):

### Scenario 1: Typo Correction

**Trigger:** User enters a command with spelling errors

**Agent Behavior:** Recognizes the intent and provides correct syntax examples

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Azure CLI Example
```bash
$ az I would like to logni

⠋ Thinking...

Azure CLI:
az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
```

#### Azure PowerShell Example
```powershell
PS> ask I would like to logni

⠋ Thinking...

Azure PowerShell:
Connect-AzAccount
Connect-AzAccount -ServicePrincipal -Credential $credential -Tenant <tenantId>
```

---

### Scenario 2: "How To" Questions

**Trigger:** User asks how to perform a task ("how do I...", "how can I...")

**Agent Behavior:** Provides command examples with placeholders for user-specific values

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Azure CLI Example
```bash
$ az how do I create a resource group

⠋ Thinking...

Azure CLI:
az group create --name <resource-group-name> --location <location>

# Example with common locations:
az group create --name myResourceGroup --location eastus
az group create --name myResourceGroup --location westus2
```

#### Azure PowerShell Example
```powershell
PS> ask how do I create a resource group

⠋ Thinking...

Azure PowerShell:
New-AzResourceGroup -Name <resource-group-name> -Location <location>

# Example:
New-AzResourceGroup -Name myResourceGroup -Location eastus
```

**Behavioral Note:** Both interfaces display guidance only - user must manually execute commands with their own values.

---

### Scenario 3: Natural Language Execution

**Trigger:** User provides complete intent with specific values ("create a resource group called X in Y")

**Agent Behavior:** Generates fully executable command with user-provided values

**Response Type:** `executable_command` (JSON) - ready to execute

#### Azure CLI Example (Interactive)
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

**CLI Behavior:**
1. Agent generates complete command
2. Prompts user for confirmation (interactive)
3. User responds with natural language (e.g., "yes", "sure", "go ahead", "no", "cancel")
4. Agent interprets user intent using language model
5. If confirmed, recursively calls `az group create --name mygroup --location westus`
6. Displays command output

#### Azure PowerShell Example (Non-Interactive)
```powershell
PS> ask create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
New-AzResourceGroup -Name mygroup -Location westus

Name     Location ProvisioningState
----     -------- -----------------
mygroup  westus   Succeeded
```

**PowerShell Behavior:**
1. Agent generates complete command
2. Prompts user for confirmation (interactive - same as CLI)
3. User responds with natural language (e.g., "yes", "sure", "go ahead", "no", "cancel")
4. Agent interprets user intent using language model
5. If confirmed, outputs command with `__PSEXEC__` marker
6. AgentHelper.ps1 captures marker and executes via `Invoke-Expression`
7. Displays cmdlet output

**Note:** PowerShell uses the same confirmation flow as CLI. The subprocess can capture user input through the streaming stdout/stdin pipeline.

---

### CLI vs PowerShell Behavioral Comparison

| Feature | Azure CLI | Azure PowerShell |
|---------|-----------|------------------|
| **Interactive Prompts** | ✅ Yes - prompts for confirmation | ✅ Yes - prompts for confirmation |
| **Confirmation** | Agent interprets natural language response | Agent interprets natural language response |
| **Command Marker** | Not needed - direct execution | `__PSEXEC__<command>` in stdout |
| **Execution Method** | Recursive az call in same process | `Invoke-Expression` in parent PowerShell |
| **User Safety** | AI-interpreted confirmation with natural language | AI-interpreted confirmation with natural language |
| **Streaming Output** | Direct stdout from process | Filtered through ForEach-Object pipeline |
| **Flavor Detection** | Uses "azure cli" in prompt template | Uses "azure powershell" via AZURECLI_FLAVOR |
| **Example Syntax** | `az create resource group X` | `ask create resource group X` (no quotes) |

**PowerShell Workaround:** For safety-critical operations, users can:
1. Use "how to" questions to get examples: `ask how do I delete a resource group`
2. Review and manually execute the command: `Remove-ResourceGroup -Name mygroup -Force`

---

### Response Protocol

#### JSON Response Types (Internal)

The agent returns JSON with a `type` field:

1. **`sample_command`** - Help text with placeholders
   ```json
   {
     "type": "sample_command",
     "explanation": "Here's how to create a resource group:",
     "command": "az group create --name <name> --location <location>"
   }
   ```

2. **`executable_command`** - Complete command ready to run
   ```json
   {
     "type": "executable_command",
     "explanation": "I'll create a resource group named 'mygroup'.",
     "command": "az group create --name mygroup --location westus"
   }
   ```

3. **`question`** - Agent needs more information
   ```json
   {
     "type": "question",
     "question": "What location would you like for the resource group?"
   }
   ```

4. **`confirmation`** - Asks user to confirm (CLI only)
   ```json
   {
     "type": "confirmation",
     "message": "This will delete resource group 'mygroup'.",
     "command": "az group delete --name mygroup"
   }
   ```

5. **`exit`** - User cancelled
   ```json
   {
     "type": "exit",
     "message": "Operation cancelled."
   }
   ```

#### User-Facing Markers

- **PowerShell Execution Marker:** `__PSEXEC__<command>` in stdout triggers `Invoke-Expression`
- **Cancellation Keywords:** `cancel`, `quit`, `exit`, `stop` terminate conversations

## Running the Agent

### Production Use

The agent is automatically invoked when a standard command **fails** (see the
current implementation in `Agent/AgentMode/AgentModeEntry.cs`). It receives the
failed command and error text and proposes a fix; it is not a free-form
natural-language command entry point.

### Testing & Evaluation (via Program.cs)

The `Program.cs` file provides a **standalone executable for testing and evaluation** independent of the Core CLI:

```bash
# Cloud mode (default)
dotnet run --project src/Agent/Agent.csproj

# Local model
dotnet run --project src/Agent/Agent.csproj -- --local-model

# Azure Copilot
dotnet run --project src/Agent/Agent.csproj -- --use-copilot
```

This standalone mode is used for:
- Rapid prototyping and iteration
- Performance benchmarking
- User experience validation
- Model evaluation without CLI integration overhead

## Implementation Details

## Implementation Details

### Backend Options

#### Option 1: Azure OpenAI (Default)

**Technology:** Azure OpenAI Service with GPT-4o-mini

**Configuration:**
```bash
# .env file in src/Core/Core.Agent/
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

#### Option 2: Local Model

**Technology:** LlamaSharp with Phi-3.5-mini (~2.4GB)

**Features:**
- Automatic model download on first run
- Cached in `~/.cache/llama_models/`
- Fully offline, no external dependencies

#### Option 3: Azure Copilot Service

**Technology:** Azure Copilot infrastructure with CLI/PowerShell handlers

**Features:**
- Production Copilot infrastructure
- Built-in Azure documentation grounding
- Enterprise-grade security
- Requires Azure authentication (`az login`)

### Shared Components

All options share:
- `src/Core/Core.Engine/AgentEngine.cs` - ICommandEngine implementation, conversation loop
- `src/Core/Core.Agent/prompt.md` - System instructions with flavor support (azure cli/azure powershell)
- `src/Core/Core.Agent/CloudAgent.cs` - Azure OpenAI integration layer
- `src/CLI/Program.cs` - Exception-driven fallback (try-catch pattern)
- `src/PS/Resources/AgentHelper.ps1` - PowerShell subprocess wrapper
- Streaming response handling with progress indicators

### Configuration

**Environment Variables:**
```bash
AZURE_OPENAI_ENDPOINT=https://<resource>.openai.azure.com
AZURE_OPENAI_API_KEY=<key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

## Evaluation Criteria

## Evaluation Criteria

### Metrics

#### 1. Factual Accuracy
Correctness of generated commands against Azure CLI/PowerShell standards.

**Measurement:**
- Command syntax correctness
- Parameter accuracy (required vs. optional)
- Azure naming convention compliance

**Measurement:**
- Command syntax correctness
- Parameter accuracy (required vs. optional)
- Azure naming convention compliance

**Test Sources:**
- Azure CLI/PowerShell official documentation
- Automated validation against CLI parsers

#### 2. Latency to First Token

Time from user input to first response character.

**Targets:**
- Excellent: < 500ms
- Good: 500ms - 1500ms
- Acceptable: 1500ms - 3000ms

#### 3. Groundedness

Ability to stay focused on Azure command generation without off-topic responses.

**Measurement:**
- Task completion rate
- Proper use of three interaction patterns
- Correct `[DONE]` marker usage

## Example Interactions

### Azure CLI Examples

All examples show the **interactive** CLI behavior with confirmation prompts.

#### Scenario 1: Typo Correction
```bash
$ az I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure CLI:
az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
az login --use-device-code
```

**Outcome:** Agent provides help text, no execution occurs.

---

#### Scenario 2: "How To" Question
```bash
$ az how do I list all VMs

⠋ Thinking...

To list virtual machines in Azure:

Azure CLI:
# List all VMs in subscription
az vm list

# List VMs in specific resource group
az vm list --resource-group <rg-name>

# List with table output
az vm list --output table
```

**Outcome:** Agent provides examples with placeholders, user manually executes desired command.

---

#### Scenario 3: Natural Language Execution
```bash
$ az create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
az group create --name mygroup --location westus

Execute this command? (yes/no): yes

⠋ Executing...

{
  "id": "/subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/mygroup",
  "location": "westus",
  "managedBy": null,
  "name": "mygroup",
  "properties": {
    "provisioningState": "Succeeded"
  },
  "tags": null,
  "type": "Microsoft.Resources/resourceGroups"
}
```

**Outcome:** Agent generates complete command, prompts for confirmation, executes on "yes".

---

#### Scenario 3 (User Declines)
```bash
$ az delete resource group mygroup

⠋ Thinking...

⚠️  Warning: This will permanently delete the resource group "mygroup" and all resources within it.

Command:
az group delete --name mygroup --yes

Execute this command? (yes/no): no

Operation cancelled.
```

**Outcome:** Agent respects user decision, no deletion occurs.

### Azure PowerShell Examples

All examples show the **non-interactive** PowerShell behavior (auto-execution after display).

**Note:** Arguments passed **without quotes** due to `ValueFromRemainingArguments`.

#### Scenario 1: Typo Correction
```powershell
PS> ask I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure PowerShell:
Connect-AzAccount
Connect-AzAccount -ServicePrincipal -Credential $credential -Tenant <tenantId>
Connect-AzAccount -UseDeviceAuthentication
```

**Outcome:** Agent provides help text, no execution occurs.

---

#### Scenario 2: "How To" Question
```powershell
PS> please how do I list all virtual machines

⠋ Thinking...

To list virtual machines in Azure:

Azure PowerShell:
# List all VMs in subscription
Get-AzVM

# List VMs in specific resource group
Get-AzVM -ResourceGroupName <rg-name>

# List with specific properties
Get-AzVM | Select-Object Name, Location, ResourceGroupName
```

**Outcome:** Agent provides examples with placeholders, user manually executes desired cmdlet.

---

#### Scenario 3: Natural Language Execution
```powershell
PS> ask create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
New-AzResourceGroup -Name mygroup -Location westus

Shall I proceed?

> yes

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded
Tags              :
ResourceId        : /subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/mygroup
```

**Outcome:** Agent generates command, prompts for confirmation, user confirms with natural language, executes, shows output.

---

#### Scenario 3 (Filtering Example)
```powershell
PS> pls show me all resource groups in westus

⠋ Thinking...

I'll retrieve all resource groups in the westus region.

Command:
Get-AzResourceGroup | Where-Object Location -eq 'westus'

Shall I proceed?

> sure

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded

ResourceGroupName : prod-rg
Location          : westus
ProvisioningState : Succeeded
```

**Outcome:** Agent generates pipeline command, prompts for confirmation, user confirms with natural language, executes, displays filtered results.

---

## Future Considerations

## Future Considerations

- **Context Awareness**: Detect current Azure subscription/tenant, infer resource group
- **Learning & Feedback**: User feedback collection, telemetry for improvement
- **Multi-Language Support**: Localization of prompts and responses
- **Advanced Features**: Multi-step command sequences, command explanation, cost estimation

## References

- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Microsoft.Agents.AI Framework](https://www.nuget.org/packages/Microsoft.Agents.AI)
- [LlamaSharp](https://github.com/SciSharp/LLamaSharp)
