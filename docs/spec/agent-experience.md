# Agent Experience Design Specification

## Overview

Core.Agent is an AI-powered natural language interface for Azure CLI (azv2) and Azure PowerShell that interprets user intent and generates executable commands. It operates as an **exception-driven fallback**: when standard command parsing fails, control passes to the agent to interpret the natural language input.

The agent supports three interaction scenarios:
1. **Typo Correction** - Recognizes misspelled commands and suggests correct syntax
2. **"How To" Questions** - Provides command examples with placeholders (not executable)
3. **Natural Language Execution** - Generates complete executable commands from natural language requests

**Key Behavioral Difference:**
- **Azure CLI (azv2)**: Interactive - can prompt user for confirmation before executing commands
- **Azure PowerShell**: Non-interactive - directly outputs commands with `__PSEXEC__` marker for execution (no prompts possible due to subprocess architecture)

**Note:** For comprehensive architecture details including fallback mechanisms and PowerShell subprocess integration, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Architecture

### Integration Architecture

#### Azure CLI (azv2) Flow

The CLI uses an **exception-driven fallback** pattern in `Program.cs`:

```
┌──────────────────────────────────────────────────────────┐
│              User Input: "azv2 <command>"                │
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
                                       │ Recursive azv2 call  │
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
         │  Call: azv2 <user query>      │
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
- **CLI**: Direct execution in same process, can do interactive prompts
- **PowerShell**: Subprocess architecture, no stdin available, uses `__PSEXEC__` marker for command execution

### Deployment Options

The agent supports three AI backend implementations:

1. **Azure OpenAI** (default) - Cloud-based, production-grade responses
2. **Local Model** (Phi-3.5-mini) - Fully offline inference via LlamaSharp
3. **Azure Copilot Service** - Azure's production Copilot infrastructure

### Interface Support

The agent works with both:

1. **Azure CLI (azv2)** - Command-line interface
   ```bash
   azv2 create a resource group called mygroup in westus
   ```

2. **Azure PowerShell** - Natural language support via `Invoke-AzAI` function
   ```powershell
   # Full function name
   Invoke-AzAI how do I create a resource group
   
   # Convenient aliases (no quotes needed)
   ask how do I create a resource group
   please show me all resource groups
   pls list virtual machines
   ```

**Note:** PowerShell uses `ValueFromRemainingArguments`, so arguments are passed **without quotes**.

## Agent Capabilities

The agent supports three interaction scenarios, with behavioral differences between CLI (interactive) and PowerShell (non-interactive):

### Scenario 1: Typo Correction

**Trigger:** User enters a command with spelling errors

**Agent Behavior:** Recognizes the intent and provides correct syntax examples

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Azure CLI Example
```bash
$ azv2 I would like to logni

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

**Behavioral Note:** Both CLI and PowerShell display help text only - no execution occurs.

---

### Scenario 2: "How To" Questions

**Trigger:** User asks how to perform a task ("how do I...", "how can I...")

**Agent Behavior:** Provides command examples with placeholders for user-specific values

**Response Type:** `sample_command` (JSON) - contains placeholders, not executable

#### Azure CLI Example
```bash
$ azv2 how do I create a resource group

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
New-ResourceGroup -Name <resource-group-name> -Location <location>

# Example:
New-ResourceGroup -Name myResourceGroup -Location eastus
```

**Behavioral Note:** Both interfaces display guidance only - user must manually execute commands with their own values.

---

### Scenario 3: Natural Language Execution

**Trigger:** User provides complete intent with specific values ("create a resource group called X in Y")

**Agent Behavior:** Generates fully executable command with user-provided values

**Response Type:** `executable_command` (JSON) - ready to execute

#### Azure CLI Example (Interactive)
```bash
$ azv2 create a resource group called mygroup in westus

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
3. On "yes", recursively calls `azv2 group create --name mygroup --location westus`
4. Displays command output

#### Azure PowerShell Example (Non-Interactive)
```powershell
PS> ask create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
New-ResourceGroup -Name mygroup -Location westus

Name     Location ProvisioningState
----     -------- -----------------
mygroup  westus   Succeeded
```

**PowerShell Behavior:**
1. Agent generates complete command
2. Outputs command with `__PSEXEC__` marker to stdout (no prompt possible)
3. AgentHelper.ps1 captures marker and executes via `Invoke-Expression`
4. Displays cmdlet output

**Critical Difference:** PowerShell cannot prompt for confirmation because subprocess architecture prevents stdin access. Commands are auto-executed after display.

---

### CLI vs PowerShell Behavioral Comparison

| Feature | Azure CLI (azv2) | Azure PowerShell |
|---------|------------------|------------------|
| **Interactive Prompts** | ✅ Yes - can ask "Execute? (yes/no)" | ❌ No - subprocess has no stdin |
| **Confirmation** | User types "yes" or "no" | Auto-executes after displaying command |
| **Command Marker** | Not needed - direct execution | `__PSEXEC__<command>` in stdout |
| **Execution Method** | Recursive azv2 call in same process | `Invoke-Expression` in parent PowerShell |
| **User Safety** | Higher - explicit confirmation required | Lower - immediate execution (by design) |
| **Streaming Output** | Direct stdout from process | Filtered through ForEach-Object pipeline |
| **Flavor Detection** | Uses "azure cli" in prompt template | Uses "azure powershell" via AZURECLI_FLAVOR |
| **Example Syntax** | `azv2 create resource group X` | `ask create resource group X` (no quotes) |

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

The agent is automatically invoked when standard command parsing fails:

**Azure CLI (azv2):**
```bash
# Natural language commands
$ azv2 create a storage account called mystore in westus

# Typo correction
$ azv2 how do I logni

# Help questions
$ azv2 how do I list virtual machines
```

**Azure PowerShell:**
```powershell
# Full function name (no quotes needed)
Invoke-AzAI create a storage account called mystore in westus

# Convenient aliases
ask how do I create a resource group
please show me all storage accounts
pls list virtual machines in eastus
```

**Important:** PowerShell arguments use `ValueFromRemainingArguments`, so **do not use quotes** around queries.

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
$ azv2 I would like to logni

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
$ azv2 how do I list all VMs

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
$ azv2 create a resource group called mygroup in westus

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
$ azv2 delete resource group mygroup

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
New-ResourceGroup -Name mygroup -Location westus

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded
Tags              :
ResourceId        : /subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/mygroup
```

**Outcome:** Agent generates command, displays it, **immediately executes** (no prompt possible), shows output.

---

#### Scenario 3 (Filtering Example)
```powershell
PS> pls show me all resource groups in westus

⠋ Thinking...

I'll retrieve all resource groups in the westus region.

Command:
Get-ResourceGroup | Where-Object Location -eq 'westus'

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded

ResourceGroupName : prod-rg
Location          : westus
ProvisioningState : Succeeded
```

**Outcome:** Agent generates pipeline command, executes immediately, displays filtered results.

---

#### PowerShell Safety Workaround

For destructive operations, use "how to" questions to get syntax without execution:

```powershell
# Safe: Get example syntax
PS> ask how do I delete a resource group

⠋ Thinking...

To delete a resource group:

Azure PowerShell:
Remove-ResourceGroup -Name <resource-group-name> -Force
Remove-ResourceGroup -Name <resource-group-name> -Force -AsJob

# Then user manually executes with confirmation:
PS> Remove-ResourceGroup -Name mygroup -Force

Confirm
Are you sure you want to remove resource group 'mygroup'
[Y] Yes  [N] No  [S] Suspend  [?] Help (default is "Y"): Y
```

**Workaround Pattern:**
1. Use `ask how do I...` to get examples
2. Review the command and parameters
3. Manually execute with PowerShell's built-in confirmation prompts

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
