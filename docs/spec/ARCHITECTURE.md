# Cirrus Architecture: Azure CLI (azv2) and PowerShell with AI Agent Fallback

## Overview

Cirrus is a modular Azure CLI and PowerShell tool with **AI-powered fallback** for natural language commands, typo correction, and "how to" questions. The architecture uses:

1. **Exception-Driven Fallback** - Standard command parsing in try-catch, AI agent invoked on exception
2. **Extension System** - Dynamic module loading via DLL discovery (Group.dll, Login.dll, Resources.dll)
3. **Subprocess PowerShell Integration** - PowerShell calls `azv2` CLI as subprocess, executes commands via `__PSEXEC__` marker
4. **Azure OpenAI Integration** - CloudAgent with GPT-4o-mini for natural language interpretation

**Note:** This document describes the **actual implementation** on the `resilv/merge-cli` branch, not idealized designs.

## Key Architectural Principles

| Aspect | Current Implementation |
|--------|------------------------|
| **Entry Point** | `Program.cs` with try-catch around `ExtensionDispatcher` → AI fallback on exception |
| **Command Routing** | `ExtensionDispatcher` → `CommandGroupIndex` → DLL loading → `ICommand` execution |
| **AI Fallback** | `TryAgentFallbackAsync()` creates `AgentEngine` + `CloudAgent` on demand (max 5 retries) |
| **Agent Integration** | Azure OpenAI Service (GPT-4o-mini) with streaming, configured via .env file |
| **Flavor Detection** | `AZURECLI_FLAVOR` environment variable ("azure cli" or "azure powershell") |
| **PowerShell Integration** | Subprocess pattern - `AgentHelper.ps1` calls `azv2`, captures `__PSEXEC__` marker, executes with `Invoke-Expression` |
| **Module Discovery** | File-based DLL loading from extensions directory at runtime |
| **Response Types** | `sample_command` (placeholders), `executable_command` (user confirms), `question`, `confirmation`, `exit` |
| **Streaming UI** | Spinner characters (`⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏`) for "Thinking...", progress bar in PowerShell |
| **Interactive Prompts** | ✅ CLI can prompt user for confirmation | ❌ PowerShell cannot (subprocess has no stdin) |

## Architecture Comparison: Theoretical vs. Actual

### Theoretical Design (Not Implemented)
- ❌ `OrchestratorEngine` actively routing to `DefaultCommandEngine` → `AgentEngine`
- ❌ Agent setting `ExtractedCommand` property to hand back to command engine
- ❌ Unified Business/ layer shared by Commands and Cmdlets
- ❌ Direct cmdlet execution without CLI subprocess

### Actual Implementation (Current)
- ✅ `Program.Main()` try-catch around extension dispatcher
- ✅ Exception triggers `TryAgentFallbackAsync()` with retry logic
- ✅ Agent outputs JSON responses parsed by `AgentEngine.ExecuteCommandAsync()`
- ✅ PowerShell uses subprocess pattern calling `azv2` CLI
- ✅ Extensions are standalone DLLs with commands, not integrated cmdlets
- ✅ `__PSEXEC__` marker protocol for PowerShell command execution

## Entry Points

### Azure CLI (azv2)

```bash
# Structured commands (extension routing)
$ azv2 group create --name mygroup --location eastus

# Natural language (AI fallback)
$ azv2 create a resource group called mygroup in westus
$ azv2 how do I list virtual machines

# Typo correction (AI fallback)
$ azv2 I would like to logni
```

**What the Bootstrapper does:**
1. Creates `ServiceCollection` and registers all dependencies as Singletons
2. Registers services: `ILogger`, `IOutputAdapter`, `CommandParser`, `CommandBinder`
3. Registers engines: `DefaultCommandEngine`, `AgentEngine`, `OrchestratorEngine`
4. Returns fully configured `ICommandEngine` (which is actually `OrchestratorEngine`)

```powershell
# Direct cmdlet execution
New-ResourceGroup -Name mygroup -Location eastus

# Natural language via Invoke-AzAI (subprocess to azv2)
Invoke-AzAI create a resource group called mygroup in westus

# Convenient aliases (no quotes needed)
ask how do I create a resource group
please show me all resource groups
pls list virtual machines
```

**Key Difference:** PowerShell's `Invoke-AzAI` spawns `azv2` as subprocess with `AZURECLI_FLAVOR=azure powershell`

## CLI Architecture with Exception-Driven Fallback

### Azure CLI (azv2) Flow

```
┌─────────────────────────────────────────────────────────────┐
│              User Input: "azv2 <command>"                   │
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
                   │ Recursive azv2    │  │ Cancelled    │
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
         │  • Find Login.dll         │
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

### PowerShell Subprocess Flow

```
┌─────────────────────────────────────────────────────────────┐
│ PS> ask how do I create a resource group                    │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  Invoke-AzAI Function     │
         │  (AgentHelper.ps1)        │
         │  • Set AZURECLI_FLAVOR=   │
         │    "azure powershell"     │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  & azv2 $Query            │
         │  (subprocess spawn)       │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │  Stream stdout line-by-   │
         │  line via ForEach-Object  │
         │  • Filter: ⠋ Thinking...  │
         │  • Show: Write-Progress   │
         └───────────┬───────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
  Regular Output         __PSEXEC__ marker
        │                         │
        ↓                         ↓
┌───────────────┐    ┌────────────────────────┐
│ Write-Host    │    │ Extract command:       │
│ $line         │    │ $commandToExecute =    │
└───────────────┘    │ $matches[1]            │
                     └────────┬───────────────┘
                              │
                              ↓
                  ┌───────────────────────────┐
                  │ Invoke-Expression         │
                  │ $commandToExecute         │
                  │ (executes cmdlet)         │
                  └───────────────────────────┘
```

## Component Relationships

### File Locations

| Component | File Path | Description |
|-----------|-----------|-------------|
| **CLI Entry Point** | `src/CLI/Program.cs` | Try-catch fallback to agent |
| **Extension Dispatcher** | `src/CLI/ExtensionDispatcher.cs` | Parse args, load DLLs |
| **Command Group Index** | `src/CLI/CommandGroupIndex.cs` | DLL discovery and loading |
| **Agent Engine** | `src/Core/Core.Engine/AgentEngine.cs` | Conversation loop, streaming UI |
| **Cloud Agent** | `src/Core/Core.Agent/CloudAgent.cs` | Azure OpenAI integration |
| **Prompt Template** | `src/Core/Core.Agent/prompt.md` | System instructions with {{flavor}} |
| **PowerShell Helper** | `src/PS/Resources/AgentHelper.ps1` | `Invoke-AzAI` function |
| **Resources Manifest** | `src/PS/Resources/Resources.psd1` | PowerShell module definition |
| **Group Extension** | `artifacts/*/Group/Group.dll` | Resource group commands |
| **Login Extension** | `artifacts/*/Login/Login.dll` | Authentication commands |

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
                                       ├─► Parse JSON responses
                                       └─► __PSEXEC__ marker output

AgentHelper.ps1
    │
    ├─► Set AZURECLI_FLAVOR="azure powershell"
    ├─► & azv2 $Query (subprocess)
    ├─► ForEach-Object streaming
    ├─► Write-Progress (progress bar)
    └─► Invoke-Expression $commandToExecute
```

## PowerShell Integration Details

### AgentHelper.ps1 Architecture

**Location:** `src/PS/Resources/AgentHelper.ps1`

**Purpose:** Provides natural language interface for PowerShell users via subprocess to `azv2` CLI

**Key Functions:**
1. `Invoke-AzAI` - Main function (aliases: `ask`, `please`, `pls`)
2. CLI path discovery - finds `azv2` or `az` executable
3. Streaming pipeline - `ForEach-Object` for line-by-line output
4. Spinner filtering - Regex to hide `⠋ Thinking...` lines
5. Progress bar - `Write-Progress` for better UX
6. Command extraction - `__PSEXEC__` marker detection
7. Execution - `Invoke-Expression` for captured commands

**Example:**
```powershell
# User types:
PS> ask create a resource group called mygroup in westus

# AgentHelper.ps1 executes:
$env:AZURECLI_FLAVOR = "azure powershell"
& $azv2Path create a resource group called mygroup in westus 2>&1 | ForEach-Object {
    # Filter spinner, show progress, capture __PSEXEC__ marker
}

# CLI outputs:
⠋ Thinking...
I'll create a resource group named "mygroup" in the westus region.
Command:
New-ResourceGroup -Name mygroup -Location westus
__PSEXEC__New-ResourceGroup -Name mygroup -Location westus

# AgentHelper.ps1 captures and executes:
Invoke-Expression "New-ResourceGroup -Name mygroup -Location westus"
```

### Behavioral Comparison: CLI vs PowerShell

| Feature | Azure CLI (azv2) | Azure PowerShell |
|---------|------------------|------------------|
| **Process Model** | Single process | Subprocess (PowerShell → azv2) |
| **Interactive Prompts** | ✅ Yes - can ask user for confirmation | ❌ No - stdin not available in subprocess |
| **Streaming Output** | Direct stdout from AgentEngine | Filtered via ForEach-Object pipeline |
| **Progress Indicator** | Spinner characters (⠋⠙⠹...) | Write-Progress bar |
| **Command Execution** | Recursive azv2 call (same process) | `Invoke-Expression` in parent PowerShell |
| **Flavor Detection** | Default or env var | Always sets `AZURECLI_FLAVOR=azure powershell` |
| **Command Marker** | Not needed (direct execution) | `__PSEXEC__<command>` required |
| **User Safety** | Explicit confirmation before execution | Auto-execution after display (no prompts) |
| **Syntax** | `azv2 create resource group X` | `ask create resource group X` (no quotes) |

**PowerShell Limitation:** Due to subprocess architecture, PowerShell cannot prompt for interactive confirmation. Commands are auto-executed after being displayed.

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

**Location:** `src/CLI/ExtensionDispatcher.cs`

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

**Location:** `src/CLI/CommandGroupIndex.cs`

**Responsibilities:**
- Scan extensions directory for DLL files
- Load assemblies dynamically
- Build index of command groups → DLL paths
- Throw `KeyNotFoundException` if command group not found

**Discovery Pattern:**
```
artifacts/Release/Group/Group.dll → "group" command group
artifacts/Release/Login/Login.dll → "login" command group
artifacts/Release/Resources/Resources.dll → "resources" command group
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
        if (flavor == "azure powershell")
        {
            // Output marker for PowerShell
            Console.WriteLine($"__PSEXEC__{response.Command}");
        }
        else
        {
            // Prompt user for confirmation
            Console.Write("Execute this command? (yes/no): ");
            string answer = Console.ReadLine();
            if (answer.ToLower() == "yes")
            {
                // Recursive azv2 call
                await ExecuteExtractedCommandAsync(response.Command);
            }
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
- System instructions for both CLI and PowerShell flavors
- JSON response schema definitions
- Examples for each scenario type
- Placeholder rules (`<angle-brackets>` indicate user-specific values)

### 6. Prompt Template - System Instructions

**Location:** `src/Core/Core.Agent/prompt.md`

**Key Instructions:**
- Detect user intent (typo, how-to question, or execution request)
- Return JSON with `type` field
- For "azure cli" flavor: Generate `az` commands
- For "azure powershell" flavor: Generate `New-ResourceGroup` style cmdlets (no AzV2 prefix)
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

### Login Extension

**Location:** `artifacts/Release/Login/Login.dll`

**Commands:**
- `login` - Authenticate to Azure
- `logout` - Sign out
- `account show` - Display current account

### Resources Extension (PowerShell)

**Location:** `artifacts/Release/Resources/Resources.dll`

**Contains:**
- PowerShell cmdlets: `New-ResourceGroup`, `Get-ResourceGroup`, etc.
- Note: No "AzV2" prefix in current implementation

## Example Execution Flows

### Example 1: Structured CLI Command (Success)

```bash
$ azv2 group create --name mygroup --location eastus
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
$ azv2 grup create --name mygroup --location eastus  # Typo: "grup"
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
$ azv2 create a resource group called mygroup in westus
```

**Flow:**
1. `Program.Main()` → Try block
2. `ExtensionDispatcher.Parse(args)` → Fails (natural language, not structured command)
3. Catch block → `TryAgentFallbackAsync(args)`
4. `AgentEngine` streams to `CloudAgent`
5. Azure OpenAI understands intent
6. Returns `executable_command` JSON:
   ```json
   {
     "type": "executable_command",
     "explanation": "I'll create a resource group named 'mygroup' in westus.",
     "command": "group create --name mygroup --location westus"
   }
   ```
7. `AgentEngine` prompts: "Execute this command? (yes/no): "
8. User types "yes"
9. Recursive call: `azv2 group create --name mygroup --location westus`
10. Extension executes command
11. Return 0

**Agent converted natural language → structured command → execution.**

---

### Example 4: PowerShell Natural Language (Non-Interactive)

```powershell
PS> ask create a resource group called mygroup in westus
```

**Flow:**
1. `Invoke-AzAI` function in AgentHelper.ps1
2. Sets `$env:AZURECLI_FLAVOR = "azure powershell"`
3. Spawns subprocess: `& azv2 create a resource group called mygroup in westus`
4. CLI: `Program.Main()` → Try block fails → `TryAgentFallbackAsync()`
5. `AgentEngine` detects flavor = "azure powershell"
6. `CloudAgent` loads prompt template with "azure powershell" flavor
7. Azure OpenAI returns PowerShell cmdlet:
   ```json
   {
     "type": "executable_command",
     "explanation": "I'll create a resource group named 'mygroup' in westus.",
     "command": "New-ResourceGroup -Name mygroup -Location westus"
   }
   ```
8. `AgentEngine` outputs: `__PSEXEC__New-ResourceGroup -Name mygroup -Location westus`
9. AgentHelper.ps1 captures output via `ForEach-Object`
10. Regex match: `'^__PSEXEC__(.+)$'` → Extracts command
11. `Invoke-Expression "New-ResourceGroup -Name mygroup -Location westus"`
12. Cmdlet executes in PowerShell context
13. Result displayed

**Agent converted natural language → PowerShell cmdlet → auto-execution.**

**Critical Difference:** PowerShell subprocess cannot prompt for confirmation (no stdin), so command executes immediately after display.

## Three Agent Scenarios (Detailed)

### Scenario 1: Typo Correction

**User Input:** Command with spelling errors

**Agent Behavior:** Recognizes intent, provides corrected syntax

**Response Type:** `sample_command` with placeholders

**CLI Example:**
```bash
$ azv2 I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure CLI:
az login
az login --service-principal --username <appId> --password <password> --tenant <tenantId>
az login --use-device-code
```

**PowerShell Example:**
```powershell
PS> ask I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure PowerShell:
Connect-AzAccount
Connect-AzAccount -ServicePrincipal -Credential $credential -Tenant <tenantId>
Connect-AzAccount -UseDeviceAuthentication
```

**Outcome:** User sees help text, no execution.

---

### Scenario 2: "How To" Questions

**User Input:** Queries starting with "how do I", "how can I", "show me how"

**Agent Behavior:** Provides command examples with placeholders

**Response Type:** `sample_command` with placeholders

**CLI Example:**
```bash
$ azv2 how do I create a resource group

⠋ Thinking...

To create a resource group:

Azure CLI:
az group create --name <resource-group-name> --location <location>

# Examples:
az group create --name myResourceGroup --location eastus
az group create --name prodResourceGroup --location westus2
```

**PowerShell Example:**
```powershell
PS> ask how do I create a resource group

⠋ Thinking...

To create a resource group:

Azure PowerShell:
New-ResourceGroup -Name <resource-group-name> -Location <location>

# Example:
New-ResourceGroup -Name myResourceGroup -Location eastus
```

**Outcome:** User manually copies and fills in placeholders.

---

### Scenario 3: Natural Language Execution

**User Input:** Complete intent with specific values

**Agent Behavior:** Generates fully executable command

**Response Type:** `executable_command` (no placeholders)

**CLI Example (Interactive):**
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

**PowerShell Example (Non-Interactive):**
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
ResourceId        : /subscriptions/.../resourceGroups/mygroup
```

**Key Difference:**
- **CLI**: Prompts "Execute? (yes/no)" - user confirms
- **PowerShell**: Auto-executes immediately (no prompt possible)

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
**Decision:** PowerShell calls `azv2` CLI as subprocess with `__PSEXEC__` marker protocol.

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
**Decision:** Load commands from DLL files at runtime (Group.dll, Login.dll, Resources.dll).

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
```

### PowerShell Tests

**Location:** `tests/PowerShell.Tests/`

**Test Class:**
- **AgentCmdletTests** (6 tests)
  - Module existence
  - `Invoke-AzAI` function availability
  - Alias availability (ask, please, pls)
  - Natural language invocation

**Running Tests:**
```bash
dotnet test tests/PowerShell.Tests/PowerShell.Tests.csproj
```

### Manual Verification

For tests requiring Azure OpenAI:
1. Set up `.env` file in `src/Core/Core.Agent/`
2. Run CLI manually: `./bin/azv2 how do I create a resource group`
3. Verify spinner, AI response, command output

## Configuration

### Environment Variables

| Variable | Purpose | Set By |
|----------|---------|--------|
| `AZURECLI_FLAVOR` | Detect CLI vs PowerShell | AgentHelper.ps1 |
| `EXTENSIONS_ROOT_DIRECTORY` | DLL discovery path | Optional (defaults to `artifacts/Release/`) |

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

### ❌ PowerShell Cannot Do Interactive Prompts
**Reason:** Subprocess architecture - stdin not connected

**Impact:** Commands auto-execute after display (no confirmation)

**Workaround:** Use "how to" questions to get examples without execution

---

### ❌ No Unified Business Layer
**Reason:** Extensions are standalone DLLs, not shared library

**Impact:** Potential code duplication between extensions

**Future:** Could create shared Cirrus.Core library for common logic

---

### ❌ Test Coverage Shows 0%
**Reason:** Subprocess execution prevents Coverlet instrumentation

**Impact:** Cannot measure code coverage accurately

**Workaround:** Integration tests verify behavior, not coverage metrics

---

### ❌ Agent Retry Logic Limited to 5 Turns
**Reason:** Prevent infinite loops in multi-turn conversations

**Impact:** Complex queries may hit limit before resolution

**Workaround:** User can re-run command to start new conversation

## Build and Deployment

### Build Process

**Script:** `./scripts/deploy.sh`

**Steps:**
1. Restore NuGet packages
2. Build solution (`cirrus.slnx`)
3. Publish CLI to `artifacts/Release/Core.Cli/` as `azv2`
4. Copy extension DLLs to `artifacts/Release/Group/`, etc.
5. Copy PowerShell module manifests to `artifacts/Release/`

**Manual Build:**
```bash
dotnet build cirrus.slnx
```

**Deploy Build:**
```bash
./scripts/deploy.sh
```

### Directory Structure After Build

```
artifacts/
  Release/
    Core/
      azv2                          ← CLI executable
      azv2.deps.json
      azv2.runtimeconfig.json
      Cirrus.Core.psd1             ← Core PowerShell module
      prompt.md
      runtimes/
    Group/
      Group.dll                     ← Extension
      Group.deps.json
      Cirrus.Group.psd1            ← PowerShell module
    Login/
      Login.dll
      Login.deps.json
      Cirrus.Login.psd1
    Resources/
      Resources.dll
      Resources.deps.json
      Cirrus.Resources.psd1        ← Has AgentHelper.ps1
```

### Running the CLI

```bash
# After deploy
./artifacts/Release/Core/azv2 group create --name test --location eastus

# Or add to PATH
export PATH="$PATH:/workspaces/cirrus/artifacts/Release/Core"
azv2 --help
```

### Importing PowerShell Modules

```powershell
# Import specific module
Import-Module ./artifacts/Release/Resources/Cirrus.Resources.psd1

# Use AI agent
ask how do I create a resource group

# Use cmdlets
New-ResourceGroup -Name test -Location eastus
```

## Future Enhancements

### Potential Improvements

1. **Shared Business Library**
   - Create `Cirrus.Core.Business` NuGet package
   - Extensions reference it instead of duplicating logic
   - Consistent behavior across all extensions

2. **Better Test Coverage**
   - Mock Azure SDK calls for unit testing
   - Integration tests with test Azure subscription
   - Better handling of coverage in subprocess scenarios

3. **Interactive Prompts in PowerShell**
   - Possible alternative: Direct embedding of AgentEngine in PowerShell module
   - Trade-off: Code duplication vs. feature parity
   - Consider hybrid approach: Direct calls for interactive, subprocess for compatibility

4. **Extended Agent Capabilities**
   - Multi-step command sequences ("create RG then storage account")
   - Context awareness (remember previous commands in session)
   - Cost estimation before execution
   - Command explanation ("what does this command do?")

5. **Telemetry and Analytics**
   - Track agent invocation frequency
   - Identify common typos for better corrections
   - Measure user satisfaction (did they execute suggested command?)

6. **Local Model Support**
   - Fallback to local LLM (Phi-3.5) when offline
   - Faster responses for simple queries
   - Privacy-conscious option for sensitive environments

## References

### Documentation

- **Agent Experience:** [agent-experience.md](agent-experience.md) - Three scenarios, CLI vs PowerShell differences
- **Repository Guidelines:** [AGENTS.md](../../AGENTS.md) - Project structure, coding style, commit guidelines

### Code Locations

| Component | Path |
|-----------|------|
| CLI Entry | `src/CLI/Program.cs` |
| Extension Dispatcher | `src/CLI/ExtensionDispatcher.cs` |
| Agent Engine | `src/Core/Core.Engine/AgentEngine.cs` |
| Cloud Agent | `src/Core/Core.Agent/CloudAgent.cs` |
| Prompt Template | `src/Core/Core.Agent/prompt.md` |
| PowerShell Helper | `src/PS/Resources/AgentHelper.ps1` |
| Resources Manifest | `src/PS/Resources/Resources.psd1` |
| CLI Tests | `tests/Cli.Tests/` |
| PowerShell Tests | `tests/PowerShell.Tests/` |

### External Resources

- [Azure OpenAI Service Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure SDK for .NET](https://learn.microsoft.com/dotnet/azure/)
- [PowerShell Module Manifests](https://learn.microsoft.com/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest)


