# Cirrus Architecture: Unified CLI/PowerShell with AI Agent Integration

## Overview

Cirrus implements a **unified architecture** where CLI and PowerShell share the same business logic and leverage an integrated AI agent for natural language command interpretation. This design eliminates code duplication, provides consistent behavior across interfaces, and enables intelligent fallback when commands are not recognized or when users provide natural language input.

## Key Architectural Principles

| Aspect | Design Decision |
|--------|-----------------|
| **Dependency Injection** | Bootstrapper pattern with `Microsoft.Extensions.DependencyInjection` - all services registered as Singletons |
| **CLI & PowerShell Integration** | Unified codebase with shared `Business/` layer, separate interface adapters (Commands for CLI, Cmdlets for PowerShell) |
| **Command Execution** | Orchestrator pattern: tries `DefaultCommandEngine` first, falls back to `AgentEngine` on failure |
| **Agent Integration** | `AgentEngine` wraps `CloudAgent` (Azure OpenAI), integrated seamlessly as `ICommandEngine` implementation |
| **Code Reuse** | Single `Business/` layer shared by both CLI Commands and PowerShell Cmdlets |
| **Module Discovery** | Dynamic assembly loading via `CommandBinder` using reflection |
| **Error Handling** | Graceful fallback to AI agent for unrecognized commands, typos, and natural language |
| **Natural Language** | Built-in via `OrchestratorEngine` + `AgentEngine` with streaming responses |
| **PowerShell Modules** | Separate modules (Cirrus.Login, Cirrus.Group, Cirrus.Agent) following Azure PowerShell pattern |

## Bootstrapper Pattern

The application uses a **Bootstrapper** class (`Core.Engine.Bootstrapper`) that centralizes service registration and configuration:

```csharp
// Core.Cli/Program.cs - Simple 3-line entry point
var commandEngine = await Bootstrapper.StartAsync();
await commandEngine.ExecuteCommandAsync(args, default);
```

**What the Bootstrapper does:**
1. Creates `ServiceCollection` and registers all dependencies as Singletons
2. Registers services: `ILogger`, `IOutputAdapter`, `CommandParser`, `CommandBinder`
3. Registers engines: `DefaultCommandEngine`, `AgentEngine`, `OrchestratorEngine`
4. Returns fully configured `ICommandEngine` (which is actually `OrchestratorEngine`)

**Why Singletons?**
- CLI process is short-lived (single command execution)
- PowerShell module lifetime managed by PowerShell host
- No per-request scoping needed
- Matches original design from initial implementation

## Complete Architecture Flow

### Entry Points

```
┌─────────────────────────────────────────────────────────────┐
│  ENTRY POINT 1: Azure CLI (Structured Commands)             │
│  $ azv2 group create --name test --location eastus          │
└────────────────────┬────────────────────────────────────────┘
                     │
┌─────────────────────────────────────────────────────────────┐
│  ENTRY POINT 2: Azure CLI (Natural Language)                │
│  $ azv2 how do I create a resource group?                   │
│  $ azv2 create a resource group called test                 │
└────────────────────┬────────────────────────────────────────┘
                     │
┌─────────────────────────────────────────────────────────────┐
│  ENTRY POINT 3: PowerShell Cmdlets (Direct)                 │
│  PS> New-AzV2ResourceGroup -Name test -Location eastus      │
│  PS> Connect-AzV2Account -TenantId xxx                      │
└────────────────────┬────────────────────────────────────────┘
                     │
┌─────────────────────────────────────────────────────────────┐
│  ENTRY POINT 4: PowerShell Natural Language                 │
│  PS> Invoke-AzV2 create a resource group called test        │
│  PS> ask how do I list my resource groups?                  │
│  (Spawns azv2 CLI with AZURECLI_FLAVOR=azure powershell)    │
└─────────────────────────────────────────────────────────────┘
```

## CLI Architecture with Agent Fallback

```
                    ┌─────────────────────┐
                    │   Cli/Program.cs    │
                    │  (Entry Point)      │
                    └──────────┬──────────┘
                               │
                               ↓
                    ┌─────────────────────┐
                    │ OrchestratorCommand │
                    │ Engine              │
                    │ (Orchestrator)      │
                    └──────────┬──────────┘
                               │
                   ┌───────────┴───────────┐
                   │                       │
                   ↓                       ↓
        ┌──────────────────┐    ┌─────────────────┐
        │ Default          │    │ Agent           │
        │ CommandEngine    │    │ Engine          │
        │ (Structured)     │    │ (Natural Lang.) │
        └────────┬─────────┘    └────────┬────────┘
                 │                       │
                 ↓                       ↓
        ┌──────────────────┐    ┌─────────────────┐
        │ CommandParser    │    │ CloudAgent      │
        │ (--key value)    │    │ (Azure OpenAI)  │
        └────────┬─────────┘    └────────┬────────┘
                 │                       │
                 ↓                       │
        ┌──────────────────┐             │
        │ CommandBinder    │             │
        │ (Module Loading) │             │
        └────────┬─────────┘             │
                 │                       │
                 ↓                       │
        ┌──────────────────┐             │
        │ IModule          │             │
        │ GetCommands()    │             │
        └────────┬─────────┘             │
                 │                       │
                 ↓                       │
        ┌──────────────────┐             │
        │ ICommand         │ ◄───────────┘
        │ ExecuteAsync()   │  (On Success)
        └────────┬─────────┘
                 │
                 ↓
        ┌──────────────────┐
        │ Business Layer   │
        │ (GroupManager,   │
        │  AuthManager)    │
        └──────────────────┘
```

### Orchestrator Engine Decision Logic

```
User Input: "azv2 <args>"
     │
     ↓
┌────────────────────────────────────────┐
│ OrchestratorEngine                     │
│ Try DefaultCommandEngine first         │
│  • CommandParser: args → command/opts  │
│  • CommandBinder: find ICommand        │
│  • Execute ICommand                    │
└────────┬───────────────────────────────┘
         │
    ┌────┴────┐
    │ Success?│
    └────┬────┘
         │
    ┌────┴─────────────────────────────┐
    │                                  │
   YES                                NO
    │                                  │
    ↓                                  ↓
┌─────────┐               ┌───────────────────────┐
│ Return  │               │ Exception Type?       │
│ Success │               └─────┬─────────────────┘
└─────────┘                     │
                    ┌───────────┴────────────┐
                    │                        │
            NotImplemented              Other Exception
            (Command exists             (Parse error,
             but not done)              unknown command,
                    │                   typo, etc.)
                    ↓                        │
            ┌──────────────┐                 ↓
            │ Show "Not    │      ┌──────────────────┐
            │ implemented" │      │ Fallback to      │
            │ Exit(0)      │      │ AgentEngine      │
            └──────────────┘      └────────┬─────────┘
                                           │
                                           ↓
                              ┌─────────────────────────┐
                              │ AgentEngine.Execute     │
                              │  • Detect CLI flavor    │
                              │  • Stream to CloudAgent │
                              │  • Interactive prompts  │
                              │  • Parse AI response    │
                              └────────┬────────────────┘
                                       │
                                  ┌────┴────┐
                                  │ Command │
                                  │Found in │
                                  │Response?│
                                  └────┬────┘
                                       │
                        ┌──────────────┴──────────────┐
                        │                             │
                      YES                            NO
                        │                             │
                        ↓                             ↓
            ┌────────────────────┐          ┌─────────────┐
            │ Check Flavor       │          │ Show AI     │
            └─────┬──────────────┘          │ Response &  │
                  │                         │ Exit(0)     │
     ┌────────────┴────────────┐            └─────────────┘
     │                         │
  Azure CLI             Azure PowerShell
     │                         │
     ↓                         ↓
┌──────────────┐    ┌────────────────────┐
│ Parse & Retry│    │ Output Marker:     │
│ CommandParser│    │ __PSEXEC__<cmd>    │
│ ExecuteAsync │    │ (PowerShell reads  │
└──────────────┘    │  and executes it)  │
                    └────────────────────┘
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         CLI Entry                           │
│              azv2 group create --name test                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                   CLI Command Adapter                       │
│           GroupCreateCommand (Commands/)                    │
│   • Parses options dict: {"name": "test"}                   │
│   • Creates CreateGroupRequest                              │
│   • Formats Console output                                  │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ↓
┌─────────────────────────────────────────────────────────────┐
│                  BUSINESS LOGIC LAYER                       │
│              GroupManager.CreateGroupAsync()                │
│   • Pure business logic - no CLI/PS knowledge               │
│   • Azure SDK calls                                         │
│   • Returns GroupInfo DTO                                   │
└────────────────────┬────────────────────────────────────────┘
                     ↑
                     │
┌─────────────────────────────────────────────────────────────┐
│              PowerShell Cmdlet Adapter                      │
│        NewAzV2ResourceGroupCmdlet (Cmdlets/)                │
│   • Maps PS parameters to CreateGroupRequest                │
│   • Formats PowerShell output (WriteObject)                 │
└────────────────────┬────────────────────────────────────────┘
                     ↑
                     │
┌─────────────────────────────────────────────────────────────┐
│                    PowerShell Entry                         │
│         New-AzV2ResourceGroup -Name test                    │
└─────────────────────────────────────────────────────────────┘
```

## PowerShell Integration via AgentHelper.ps1

PowerShell modules include `AgentHelper.ps1` which provides the `Invoke-AzV2` function (and aliases: `ask`, `please`, `pls`). This function:

1. Sets `$env:AZURECLI_FLAVOR = "azure powershell"`
2. Spawns the `azv2` CLI process with user input
3. Captures output and looks for `__PSEXEC__<command>` marker
4. If marker found: executes the extracted PowerShell command
5. Shows spinner with progress bar during AI thinking time

**Example Flow:**
```powershell
PS> Import-Module ./bin/Group/Cirrus.Group.psd1
PS> Invoke-AzV2 create a resource group called test in eastus
# → Spawns: azv2 create a resource group called test in eastus
# → CLI detects AZURECLI_FLAVOR=azure powershell
# → Agent returns: New-AzV2ResourceGroup -Name test -Location eastus
# → CLI outputs: __PSEXEC__New-AzV2ResourceGroup -Name test -Location eastus
# → PowerShell extracts and executes the cmdlet
```

## Core Components

### 1. Program.cs (CLI Entry Point)

**Location:** `Cli/Program.cs`

**Responsibilities:**
- Read command-line arguments
- Detect `AZURECLI_FLAVOR` environment variable (set by PowerShell's `Invoke-AzV2`)
- Initialize output adapter, telemetry, and engines
- Create `OrchestratorEngine` with both default and agent engines
- Execute command and handle top-level exceptions

**Key Code:**
```csharp
var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
var orchestratorEngine = new OrchestratorEngine(
    defaultEngine, agentEngine, parser, binder, telemetry, output);
await orchestratorEngine.ExecuteCommandAsync(args);
```

### 2. OrchestratorEngine

**Location:** `Cli/OrchestratorEngine.cs`

**Responsibilities:**
- Try `DefaultCommandEngine` first for structured commands
- On `NotImplementedException`: Show friendly note and exit successfully (feature not implemented)
- On any other exception (parse error, unknown command, typo): Fallback to `AgentEngine`
- If agent extracts a command:
  - **Azure CLI mode**: Parse extracted command and retry execution
  - **PowerShell mode**: Output `__PSEXEC__` marker for PowerShell to execute
- Handle all exit conditions gracefully (no exceptions propagate to user)

**Key Logic:**
```csharp
try {
    await _defaultEngine.ExecuteCommandAsync(args, cancellationToken);
} catch (NotImplementedException ex) {
    await _output.WriteLineAsync($"Not yet implemented: {ex.Message}");
    return; // Exit gracefully
} catch (Exception ex) {
    // Unknown command, typo, parse error → try AI agent
    await _agentEngine.ExecuteAsync(string.Join(" ", args), cancellationToken);
}
```

### 3. DefaultCommandEngine

**Location:** `Cli/DefaultCommandEngine.cs`

**Responsibilities:**
- Parse arguments into command + options dictionary
- Use `CommandBinder` to find matching `ICommand`
- Execute command
- Throw `NotImplementedException` for known but unimplemented features
- Throw other exceptions for parse errors or unknown commands (triggers agent fallback)

**Example:**
```csharp
var parseResult = _parser.Parse(args);
var command = _binder.GetCommand(parseResult.CommandName);
await command.ExecuteAsync(parseResult.Options, cancellationToken);
```

### 4. AgentEngine

**Location:** `Cli/AgentEngine.cs`

**Responsibilities:**
- Stream user input to `CloudAgent` (Azure OpenAI)
- Display thinking spinner and AI responses
- Detect CLI flavor from environment variable
- Parse AI response for command markers (lines starting with `az` or `New-`/`Get-`/`Set-`/`Remove-`)
- For PowerShell flavor: Check for placeholders, prompt user if found
- For Azure CLI: Parse and return command for retry
- For PowerShell: Output `__PSEXEC__` marker for PowerShell to execute

**Key Logic:**
```csharp
if (_flavor == "azure powershell") {
    if (trimmed.StartsWith("New-AzV2") || trimmed.StartsWith("Get-AzV2") || 
        trimmed.StartsWith("Set-AzV2") || trimmed.StartsWith("Remove-AzV2")) {
        // Check for placeholders
        if (HasPlaceholders(trimmed)) continue;
        // Output marker for PowerShell
        await _output.WriteLineAsync($"__PSEXEC__{trimmed}");
        return;
    }
}
```

### 5. CloudAgent

**Location:** `Agent/CloudAgent.cs`

**Responsibilities:**
- Manage Azure OpenAI connection
- Load prompt template from `prompt.txt`
- Stream chat completions
- Inject CLI flavor into system prompt
- Handle errors and retries

**Prompt Engineering:**
- `prompt.txt` contains detailed instructions for both "azure cli" and "azure powershell" flavors
- Agent knows to return `az` commands for CLI, `AzV2` cmdlets for PowerShell
- Agent understands syntax help vs. "do it for me" requests
- `[DONE]` marker used only when command is ready to execute

### 6. CommandParser

**Location:** `Cli/CommandParser.cs`

**Responsibilities:**
- Parse command-line arguments
- Extract command name and options dictionary
- Handle `--key value` and `--key=value` formats
- Handle boolean flags

**Example:**
```bash
azv2 group create --name test --location eastus --tags env=prod
→ Command: "group create"
→ Options: {"name": "test", "location": "eastus", "tags": "env=prod"}
```

### 7. CommandBinder

**Location:** `Cli/CommandBinder.cs`

**Responsibilities:**
- Dynamically load module assemblies from file system
- Discover `IModule` implementations
- Call `GetCommands()` to register commands
- Build command registry (command name → `ICommand` instance)
- Throw exception if command not found (triggers agent fallback)

**Module Discovery:**
```csharp
// Load Group.dll, Login.dll, etc.
var modules = Directory.GetFiles(baseDir, "*.dll")
    .Select(Assembly.LoadFrom)
    .SelectMany(asm => asm.GetTypes())
    .Where(t => typeof(IModule).IsAssignableFrom(t))
    .Select(Activator.CreateInstance)
    .Cast<IModule>();
```

## Business Layer Structure

### Group Module

```
Group/
├── Business/                           ← Pure business logic
│   ├── GroupManager.cs                ← CreateGroupAsync(), DeleteGroupAsync(), etc.
│   └── Models/                        ← DTOs
│       ├── CreateGroupRequest.cs      ← Input parameters
│       ├── DeleteGroupRequest.cs
│       ├── ListGroupsRequest.cs
│       ├── ShowGroupRequest.cs
│       └── GroupInfo.cs               ← Return data
├── Commands/                          ← CLI adapters (thin wrappers)
│   ├── GroupCreateCommand.cs          ← ICommand: options dict → GroupManager
│   ├── GroupListCommand.cs
│   ├── GroupShowCommand.cs
│   └── GroupDeleteCommand.cs
├── Cmdlets/                           ← PowerShell adapters (thin wrappers)
│   ├── NewAzV2ResourceGroupCmdlet.cs  ← PSCmdlet: PS params → GroupManager
│   ├── GetAzV2ResourceGroupCmdlet.cs
│   ├── SetAzV2ResourceGroupCmdlet.cs
│   └── RemoveAzV2ResourceGroupCmdlet.cs
└── Group/                             ← Original location (module registration)
    └── GroupModule.cs                 ← Returns Commands for CLI discovery
```

### Login Module

```
Login/
├── Business/                           ← Pure authentication logic
│   ├── AuthenticationManager.cs        ← LoginAsync(), Logout(), GetCurrentContext()
│   └── Models/
│       ├── LoginRequest.cs
│       └── AuthenticationInfo.cs
├── Commands/
│   └── LoginCommand.cs                 ← Handles login/logout/show actions
├── Cmdlets/
│   ├── ConnectAzV2AccountCmdlet.cs
│   ├── DisconnectAzV2AccountCmdlet.cs
│   └── GetAzV2ContextCmdlet.cs
└── Login/
    ├── LoginModule.cs
    └── AzureAuthenticationContext.cs   ← Global auth state
```

## Example: Creating a Resource Group

### CLI Path

```bash
azv2 group create --name mygroup --location eastus
```

1. **DefaultCommandEngine** → `CommandBinder` → loads `Group` module
2. **GroupModule.GetCommands()** → returns `GroupCreateCommand`
3. **GroupCreateCommand.ExecuteAsync()**:
   ```csharp
   var request = new CreateGroupRequest {
       Name = options["name"],
       Location = options["location"]
   };
   var result = await GroupManager.CreateGroupAsync(request);
   Console.WriteLine($"Created: {result.Name}");
   ```

### PowerShell Path

```powershell
New-AzV2ResourceGroup -Name mygroup -Location eastus
```

1. **PowerShell** → `NewAzV2ResourceGroupCmdlet.ProcessRecord()`
2. **NewAzV2ResourceGroupCmdlet**:
   ```csharp
   var request = new CreateGroupRequest {
       Name = Name,  // PS parameter
       Location = Location
   };
   var result = await GroupManager.CreateGroupAsync(request);  // SAME CALL
   WriteObject(result);
   ```

## Key Benefits

### ✅ Zero Duplication
- Business logic in `GroupManager` is written **once**
- CLI and PowerShell both call the **exact same methods**
- No code copying or synchronization issues

### ✅ Clear Separation of Concerns
- **Business/**: What to do (Azure SDK operations)
- **Commands/**: How CLI invokes it (options dict → request)
- **Cmdlets/**: How PowerShell invokes it (PS params → request)

### ✅ Easy to Test
- Test `GroupManager` methods directly with unit tests
- Test CLI adapters with mocked GroupManager
- Test PowerShell cmdlets with mocked GroupManager

### ✅ Easy to Extend
Adding a new operation requires:
1. Add method to `GroupManager.cs` (business logic)
2. Create `XxxCommand.cs` (CLI adapter)
3. Create `XxxCmdlet.cs` (PowerShell adapter)

### ✅ Consistent Behavior
Since both paths call the same business logic, CLI and PowerShell behavior is **guaranteed to be identical** (except for output formatting).

## Implementation Details

### Business Layer (GroupManager)

**Responsibilities:**
- Execute Azure SDK operations
- Validate authentication state
- Return strongly-typed DTOs
- **No** console output (returns data only)
- **No** knowledge of CLI or PowerShell

**Example:**
```csharp
public static async Task<GroupInfo> CreateGroupAsync(
    CreateGroupRequest request, 
    CancellationToken cancellationToken)
{
    ValidateAuthentication();
    var armClient = new ArmClient(AzureAuthenticationContext.Credential);
    // ... Azure SDK calls ...
    return MapToGroupInfo(resourceGroup);
}
```

### CLI Adapter (Commands/)

**Responsibilities:**
- Parse CLI options dictionary
- Build business request DTOs
- Call `GroupManager` methods
- Format output for console (Console.WriteLine)
- Handle CLI-specific errors

**Example:**
```csharp
public async Task ExecuteAsync(IDictionary<string, string> options, ...)
{
    var request = new CreateGroupRequest {
        Name = options["name"],
        Location = options["location"]
    };
    var result = await GroupManager.CreateGroupAsync(request);
    Console.WriteLine($"Created: {result.Name}");  // CLI formatting
}
```

### PowerShell Adapter (Cmdlets/)

**Responsibilities:**
- Map PowerShell parameters to request DTOs
- Call `GroupManager` methods
- Format output for PowerShell (WriteObject)
- Handle PowerShell-specific features (ShouldContinue, etc.)

**Example:**
```csharp
protected override void ProcessRecord()
{
    var request = new CreateGroupRequest {
        Name = Name,        // PS parameter
        Location = Location
    };
    var result = GroupManager.CreateGroupAsync(request).GetAwaiter().GetResult();
    WriteObject(result);  // PowerShell formatting
}
```

## Authentication Context

**AzureAuthenticationContext** (in Login module) provides global authentication state:
- `Credential`: TokenCredential for Azure SDK
- `SubscriptionId`: Current subscription
- `TenantId`: Current tenant
- `IsAuthenticated`: Auth status

Both CLI and PowerShell share this context, ensuring consistent authentication state.

## Example Scenarios

The following scenarios demonstrate how both Azure CLI (`azv2`) and Azure PowerShell cmdlets work, including natural language support and intelligent fallback.

### Azure CLI Scenarios

#### Scenario 1: Structured CLI Command (Success)

```bash
$ azv2 group create --name test --location eastus
```

**Flow:**
1. `Program.cs` → `OrchestratorEngine`
2. Try `DefaultCommandEngine`
3. `CommandParser` → `{command: "group create", options: {"name": "test", ...}}`
4. `CommandBinder` → finds `GroupCreateCommand`
5. `GroupCreateCommand.ExecuteAsync()` → calls `GroupManager.CreateGroupAsync()`
6. Success → output "Created: test" → Exit(0)

**No agent invoked** - structured command succeeded.

#### Scenario 2: CLI with Typo (Agent Correction)

```bash
$ azv2 grup create --name test --location eastus  # Typo: "grup"
```

**Flow:**
1. `Program.cs` → `OrchestratorEngine`
2. Try `DefaultCommandEngine`
3. `CommandBinder` → throws exception (command "grup create" not found)
4. `OrchestratorEngine` catches exception
5. Fallback to `AgentEngine` with full input
6. `CloudAgent` → Azure OpenAI understands intent
7. AI returns: "Did you mean `azv2 group create`?"
8. `AgentEngine` extracts: `group create --name test --location eastus`
9. **Azure CLI mode**: Parse and retry → Success
10. Exit(0)

**Agent corrected the typo automatically.**

#### Scenario 3: CLI Natural Language

```bash
$ azv2 create a resource group called test in eastus
```

**Flow:**
1. `Program.cs` → `OrchestratorEngine`
2. Try `DefaultCommandEngine`
3. `CommandParser` → treats "create" as command, rest as options
4. `CommandBinder` → no command "create" found → throws exception
5. Fallback to `AgentEngine`
6. `CloudAgent` → understands natural language
7. AI asks: "Would you like me to execute `azv2 group create --name test --location eastus`?"
8. User confirms → AI returns `[DONE]`
9. `AgentEngine` extracts command → Parse and retry
10. `GroupCreateCommand` executes → Success

**Agent converted natural language to structured command.**

#### Scenario 4: CLI Syntax Help (Documentation Request)

```bash
$ azv2 how do I create a resource group?
```

**Flow:**
1. `DefaultCommandEngine` fails (not a command)
2. Fallback to `AgentEngine`
3. `CloudAgent` recognizes "how to" question
4. AI responds with syntax only: `azv2 group create --name <resource-group-name> --location <location>`
5. **No `[DONE]` marker** → No execution
6. User sees syntax help → Exit(0)

**Agent provided documentation, not execution.**

### Azure PowerShell Scenarios

#### Scenario 5: Direct PowerShell Cmdlet

```powershell
PS> Import-Module ./bin/Group/Cirrus.Group.psd1
PS> New-AzV2ResourceGroup -Name test -Location eastus
```

**Flow:**
1. PowerShell → `NewAzV2ResourceGroupCmdlet.ProcessRecord()`
2. Build `CreateGroupRequest` from PS parameters
3. Call `GroupManager.CreateGroupAsync()` (same business logic as CLI!)
4. `WriteObject(result)` → PowerShell output
5. Done

**No CLI spawn**, **no agent** - direct cmdlet execution.

#### Scenario 6: PowerShell Natural Language (via Invoke-AzV2)

```powershell
PS> Import-Module ./bin/Group/Cirrus.Group.psd1
PS> Invoke-AzV2 create a resource group called test in eastus
# Aliases: ask, please, or pls
PS> ask create a resource group called test in eastus
PS> please create a resource group called test in eastus
PS> pls create a resource group called test in eastus
```

**Flow:**
1. `Invoke-AzV2` sets `$env:AZURECLI_FLAVOR = "azure powershell"`
2. Spawns: `azv2 create a resource group called test in eastus`
3. CLI → `OrchestratorEngine` → `DefaultCommandEngine` fails
4. Fallback to `AgentEngine`
5. `AgentEngine` detects flavor = "azure powershell"
6. `CloudAgent` returns PowerShell cmdlet: `New-AzV2ResourceGroup -Name test -Location eastus`
7. `AgentEngine` checks for placeholders (none found)
8. Outputs: `__PSEXEC__New-AzV2ResourceGroup -Name test -Location eastus`
9. `Invoke-AzV2` captures output, extracts command
10. PowerShell executes: `New-AzV2ResourceGroup -Name test -Location eastus`
11. Result displayed

**Agent converted natural language to PowerShell cmdlet, which PowerShell then executed.**

#### Scenario 7: PowerShell Syntax Help

```powershell
PS> Import-Module ./bin/Group/Cirrus.Group.psd1
PS> Invoke-AzV2 how do I list resource groups?
# Aliases: ask, please, or pls
PS> ask how do I list resource groups?
PS> please how do I list resource groups?
PS> pls how do I list resource groups?
```

**Flow:**
1. `Invoke-AzV2` sets `$env:AZURECLI_FLAVOR = "azure powershell"`
2. Spawns: `azv2 how do I list resource groups?`
3. CLI → `OrchestratorEngine` → `DefaultCommandEngine` fails
4. Fallback to `AgentEngine`
5. `AgentEngine` detects flavor = "azure powershell"
6. `CloudAgent` recognizes "how to" question
7. AI responds with syntax only: `Get-AzV2ResourceGroup [-Name <resource-group-name>]`
8. **No `[DONE]` marker** → No execution
9. Output displayed to PowerShell user → Exit(0)

**Agent provided PowerShell cmdlet syntax documentation.**

#### Scenario 8: PowerShell Cmdlet with Typo (Agent Correction via Invoke-AzV2)

```powershell
PS> Import-Module ./bin/Group/Cirrus.Group.psd1
PS> Invoke-AzV2 Get-AzV2ResurceGroup -Name test  # Typo: "Resurce"
# Aliases: ask, please, or pls
PS> ask Get-AzV2ResurceGroup -Name test
PS> please Get-AzV2ResurceGroup -Name test
PS> pls Get-AzV2ResurceGroup -Name test
```

**Flow:**
1. `Invoke-AzV2` spawns CLI with typo
2. CLI → `OrchestratorEngine` → `DefaultCommandEngine` fails
3. Fallback to `AgentEngine`
4. `AgentEngine` detects flavor = "azure powershell"
5. `CloudAgent` recognizes typo and corrects to: `Get-AzV2ResourceGroup -Name test`
6. Outputs: `__PSEXEC__Get-AzV2ResourceGroup -Name test`
7. PowerShell executes corrected cmdlet
8. Results displayed

**Agent corrected PowerShell cmdlet typo automatically.**

### Scenario 9: Mixed - CLI with PowerShell Flavor Context

```bash
$ export AZURECLI_FLAVOR="azure powershell"
$ azv2 create a storage account
```

**Flow:**
1. CLI detects `AZURECLI_FLAVOR = "azure powershell"`
2. `OrchestratorEngine` → `DefaultCommandEngine` fails
3. Fallback to `AgentEngine`
4. `AgentEngine` sees PowerShell flavor → asks `CloudAgent` for PowerShell syntax
5. AI returns: `New-AzV2StorageAccount -Name <name> -ResourceGroupName <rg> -Location <location>`
6. Outputs: `__PSEXEC__New-AzV2StorageAccount ...`
7. (In PowerShell context, this would be executed; in direct CLI, just displayed)

**Agent respects flavor context and returns appropriate syntax.**

## Key Benefits

### ✅ Zero Duplication
- Business logic in `GroupManager` is written **once**
- CLI and PowerShell both call the **exact same methods**
- Agent can invoke either CLI or PowerShell commands

### ✅ Intelligent Fallback
- Typos → Agent corrects automatically
- Unknown commands → Agent suggests correct syntax
- Natural language → Agent converts to structured commands
- Always helpful, never "command not found"

### ✅ Unified Natural Language Support
- CLI supports natural language natively
- PowerShell via `Invoke-AzV2` delegates to CLI agent
- Single AI agent serves both interfaces

### ✅ Flavor-Aware Agent
- Detects CLI vs. PowerShell context
- Returns appropriate syntax for each
- PowerShell gets `AzV2` cmdlets, CLI gets `azv2` commands

### ✅ Clear Separation of Concerns
- **Business/**: What to do (Azure SDK operations)
- **Commands/**: How CLI invokes it (options dict → request)
- **Cmdlets/**: How PowerShell invokes it (PS params → request)
- **AgentEngine**: How natural language converts to commands

### ✅ Easy to Test
- Test `GroupManager` methods with unit tests
- Test `CommandBinder` module loading
- Test `AgentEngine` prompt/response parsing
- Test `OrchestratorEngine` fallback logic
- Test end-to-end scenarios

## Migration from src (v1)

Key differences from v1:
1. ❌ **Removed**: Separate PowerShell process spawning for every cmdlet
2. ✅ **Added**: `OrchestratorEngine` with agent fallback
3. ✅ **Added**: `AgentEngine` for natural language
4. ✅ **Added**: `Business/` folder with shared logic
5. ✅ **Added**: `Invoke-AzV2` PowerShell function for natural language
6. ✅ **Improved**: PowerShell cmdlets call business logic directly (no CLI spawn)
7. ✅ **Improved**: Graceful error handling with helpful AI responses

## Configuration

### Environment Variables
- `AZURECLI_FLAVOR`: Set by PowerShell's `Invoke-AzV2` to `"azure powershell"`, defaults to `"azure cli"`
- Azure OpenAI settings: Configured in `Agent/.env`

### prompt.txt

Located in `Agent/prompt.txt`, this file contains:
- Instructions for "azure cli" syntax
- Instructions for "azure powershell" syntax (AzV2 cmdlets)
- Rules for syntax help vs. execution
- Examples for both flavors
- `[DONE]` marker usage rules

## Next Steps

1. Update `.csproj` files to include new folder structure
2. Test CLI path: `azv2 group create`
3. Test PowerShell path: `New-AzV2ResourceGroup`
4. Verify both paths produce identical results
5. Add unit tests for `GroupManager` methods
6. Add integration tests for end-to-end flows
