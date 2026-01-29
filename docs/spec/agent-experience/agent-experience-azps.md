# Azure PowerShell Agent Experience

| Status | Platform | Version | Notes |
|--------|----------|---------|-------|
| ![Status](https://img.shields.io/badge/status-design%20only-yellow) | Azure PowerShell | 7.x+ | Initial implementation removed, pending review |

> **See Also:** For general agent concepts and capabilities, see [agent-experience.md](../agent-experience.md)

## Overview

The Azure PowerShell agent provides natural language command interpretation through a **subprocess architecture**. The `Invoke-AzAI` function (with convenient aliases `ask`, `please`, `pls`) calls the Azure CLI agent as a subprocess, streaming output and executing generated PowerShell commands.

**Current Status:** This document describes the design for Azure PowerShell agent integration. The initial implementation has been removed and may be re-added pending architectural review.

## Architecture

### Subprocess Pattern

PowerShell uses a fundamentally different approach than the CLI - it calls `azv2` as a subprocess and interprets the output:

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

### Key Components

#### Invoke-AzAI Function

Primary entry point in `AgentHelper.ps1`:

```powershell
function Invoke-AzAI {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments)]
        [string[]]$Query
    )
    
    # Join query arguments
    $queryString = $Query -join ' '
    
    # Set environment variable for CLI to detect PowerShell context
    $env:AZURECLI_FLAVOR = "azure powershell"
    
    # Call azv2 as subprocess
    $process = Start-Process azv2 -ArgumentList $queryString -NoNewWindow -PassThru
    
    # Stream and process output
    # ... (see Output Processing section)
}

# Convenient aliases
Set-Alias -Name ask -Value Invoke-AzAI
Set-Alias -Name please -Value Invoke-AzAI
Set-Alias -Name pls -Value Invoke-AzAI
```

#### Alias Support

Users can invoke the agent using natural aliases without quotes:

```powershell
# Full function name
Invoke-AzAI how do I create a resource group

# Short aliases (most common)
ask how do I create a resource group
please show me all storage accounts
pls list virtual machines in eastus
```

**Note:** Uses `ValueFromRemainingArguments` so no quotes needed around queries.

#### AZURECLI_FLAVOR Environment Variable

The subprocess sets `AZURECLI_FLAVOR="azure powershell"` before calling `azv2`:

- CLI agent detects this variable
- Modifies prompt template to generate PowerShell cmdlets instead of CLI commands
- Ensures agent outputs PowerShell syntax (e.g., `Get-AzResourceGroup` not `azv2 group list`)

### Output Processing and Command Execution

#### Streaming Output

The PowerShell function streams stdout line-by-line from the subprocess:

```powershell
$process.StandardOutput.BaseStream | ForEach-Object {
    $line = [System.Text.Encoding]::UTF8.GetString($_)
    
    # Filter progress indicators
    if ($line -match '^⠋ Thinking...') {
        Write-Progress -Activity "Agent Processing" -Status "Thinking..."
        return
    }
    
    # Check for execution marker
    if ($line -match '^__PSEXEC__(.+)$') {
        $commandToExecute = $matches[1]
        # Execute PowerShell command
        Invoke-Expression $commandToExecute
        return
    }
    
    # Regular output
    Write-Host $line
}
```

#### __PSEXEC__ Marker Protocol

When the agent generates an executable command for PowerShell:

1. **Agent Output:** `__PSEXEC__<powershell-command>`
2. **Detection:** PowerShell function captures stdout, matches regex
3. **Extraction:** Captures everything after `__PSEXEC__` prefix
4. **Execution:** Passes to `Invoke-Expression`
5. **Output:** Cmdlet results display normally in PowerShell

**Example Flow:**

```
User Input: ask create a resource group called mygroup in westus
           ↓
Subprocess: azv2 "create a resource group called mygroup in westus"
           ↓
CLI Agent:  Generates PowerShell command
           ↓
Stdout:     I'll create a resource group named "mygroup" in the westus region.
            Command:
            New-AzResourceGroup -Name mygroup -Location westus
            Execute this command? (yes/no): 
User Input: yes
           ↓
Stdout:     __PSEXEC__New-AzResourceGroup -Name mygroup -Location westus
           ↓
PS Function: Detects marker, extracts command
           ↓
Invoke-Expression: New-AzResourceGroup -Name mygroup -Location westus
           ↓
Output:     ResourceGroupName : mygroup
            Location          : westus
            ProvisioningState : Succeeded
```

### Interactive Confirmation Flow

PowerShell maintains the same interactive confirmation behavior as CLI:

1. Agent generates complete PowerShell command
2. Agent prompts user for confirmation through subprocess stdout
3. User responds with natural language (subprocess stdin)
4. Agent interprets user intent using language model
5. If confirmed, agent outputs `__PSEXEC__<command>` marker
6. PowerShell function executes via `Invoke-Expression`

**Key Point:** The subprocess can capture user input through the streaming stdin pipeline, enabling interactive prompts even in the subprocess model.

## PowerShell-Specific Examples

All examples demonstrate the **interactive** PowerShell behavior with AI-interpreted confirmation prompts, followed by automatic execution via the `__PSEXEC__` marker protocol.

**Note:** Arguments use `ValueFromRemainingArguments`, so **no quotes needed** around queries.

### Scenario 1: Typo Correction

**User Intent:** Misspelled command

**Agent Behavior:** Recognizes typo and provides correct PowerShell syntax examples (not executable)

```powershell
PS> ask I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure PowerShell:
Connect-AzAccount
Connect-AzAccount -ServicePrincipal -Credential $credential -Tenant <tenantId>
Connect-AzAccount -UseDeviceAuthentication
```

**Outcome:** Agent provides help text with placeholders, no execution occurs.

---

### Scenario 2: "How To" Questions

**User Intent:** Learn how to perform a task

**Agent Behavior:** Provides PowerShell cmdlet examples with placeholders for user-specific values

#### Example: Resource Group Creation

```powershell
PS> ask how do I create a resource group

⠋ Thinking...

To create a resource group in Azure:

Azure PowerShell:
New-AzResourceGroup -Name <resource-group-name> -Location <location>

# Example:
New-AzResourceGroup -Name myResourceGroup -Location eastus
```

**Outcome:** Agent provides examples with placeholders, user manually executes with their own values.

#### Example: Virtual Machine Listing

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

# List with formatted table output
Get-AzVM | Format-Table Name, ResourceGroupName, Location
```

**Outcome:** Agent provides multiple examples demonstrating various PowerShell patterns and pipeline usage.

---

### Scenario 3: Natural Language Execution

**User Intent:** Execute a specific command with all required values provided

**Agent Behavior:** Generates complete executable PowerShell command, prompts for confirmation with AI interpretation, executes via `__PSEXEC__` marker

#### Example: Resource Group Creation (Confirmed)

```powershell
PS> ask create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
New-AzResourceGroup -Name mygroup -Location westus

Execute this command? (yes/no): yes

⠋ Executing...

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded
Tags              :
ResourceId        : /subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/mygroup
```

**Execution Flow:**
1. Agent generates complete PowerShell command
2. Prompts user for confirmation (interactive, through subprocess)
3. User responds with "yes" (or any affirmative natural language)
4. Agent interprets response using language model
5. Agent outputs `__PSEXEC__New-AzResourceGroup -Name mygroup -Location westus` to stdout
6. PowerShell function captures marker and executes via `Invoke-Expression`
7. Displays cmdlet output (PowerShell object formatting)

#### Example: Resource Group Creation (Natural Language Confirmation)

```powershell
PS> ask create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
New-AzResourceGroup -Name mygroup -Location westus

Execute this command? (yes/no): sure, go ahead

⠋ Executing...

ResourceGroupName : mygroup
Location          : westus
ProvisioningState : Succeeded
Tags              :
ResourceId        : /subscriptions/12345678-1234-1234-1234-123456789abc/resourceGroups/mygroup
```

**Note:** User responded with "sure, go ahead" - the agent interprets this as affirmative using the language model, not hard-coded string matching.

#### Example: Resource Group Deletion (Declined)

```powershell
PS> ask delete resource group mygroup

⠋ Thinking...

⚠️  Warning: This will permanently delete the resource group "mygroup" and all resources within it.

Command:
Remove-AzResourceGroup -Name mygroup -Force

Execute this command? (yes/no): no, I changed my mind

Operation cancelled.
```

**Outcome:** Agent respects user's decision, no deletion occurs. Natural language "no, I changed my mind" is interpreted as negative confirmation.

#### Example: Storage Account Listing with Filtering

```powershell
PS> pls show me all storage accounts in eastus

⠋ Thinking...

I'll retrieve all storage accounts in the eastus region.

Command:
Get-AzStorageAccount | Where-Object Location -eq 'eastus'

Execute this command? (yes/no): yes

⠋ Executing...

StorageAccountName ResourceGroupName Location SkuName          Kind
------------------ ----------------- -------- -------          ----
mystorageacct      mygroup           eastus   Standard_LRS     StorageV2
prodstorage        prod-rg           eastus   Premium_LRS      StorageV2
```

**Outcome:** Agent generates PowerShell pipeline with `Where-Object` filtering, demonstrates understanding of PowerShell idioms.

#### Example: VM Listing with Property Selection

```powershell
PS> please show me all VMs with their status

⠋ Thinking...

I'll list all virtual machines with their power state status.

Command:
Get-AzVM -Status | Select-Object Name, ResourceGroupName, Location, PowerState

Execute this command? (yes/no): yes

⠋ Executing...

Name        ResourceGroupName Location PowerState
----        ----------------- -------- ----------
webserver1  prod-rg           eastus   VM running
dbserver1   prod-rg           eastus   VM running
testvm1     dev-rg            westus   VM deallocated
```

**Outcome:** Agent uses `-Status` parameter and `Select-Object` to show relevant properties.

#### Example: Complex Pipeline with Sorting

```powershell
PS> ask list all resource groups sorted by name

⠋ Thinking...

I'll retrieve all resource groups and sort them alphabetically by name.

Command:
Get-AzResourceGroup | Sort-Object ResourceGroupName | Format-Table ResourceGroupName, Location, ProvisioningState

Execute this command? (yes/no): yes

⠋ Executing...

ResourceGroupName Location ProvisioningState
----------------- -------- -----------------
dev-rg            westus   Succeeded
mygroup           eastus   Succeeded
prod-rg           eastus   Succeeded
test-rg           westus2  Succeeded
```

**Outcome:** Agent constructs idiomatic PowerShell pipeline with sorting and formatted output.

---

### Edge Cases and Safety

#### Example: Ambiguous Request

```powershell
PS> ask create a storage account

⠋ Thinking...

To create a storage account, I need a bit more information:

What name would you like for the storage account?
What resource group should it be in?
```

**Agent Behavior:** Asks clarifying questions when required parameters are missing.

#### Example: Invalid Cmdlet Parameter Detection

```powershell
PS> ask create a resource group called my-group!@# in westus

⠋ Thinking...

I notice the resource group name "my-group!@#" contains invalid characters. Resource group names can only contain:
- Alphanumeric characters
- Hyphens, underscores, parentheses, and periods
- Cannot end with a period

Would you like to use "my-group" instead? (yes/no): yes

⠋ Executing...

ResourceGroupName : my-group
Location          : westus
ProvisioningState : Succeeded
```

**Agent Behavior:** Validates parameters against Azure naming rules, suggests corrections.

---

## PowerShell-Specific Features

### Subprocess Architecture

PowerShell uses a fundamentally different approach than CLI:
- **Isolation**: Agent runs in separate process
- **Flexibility**: Can call any CLI version or alternate agent implementation
- **Safety**: Agent errors don't crash PowerShell session
- **Trade-off**: Slightly higher latency due to process spawning

### __PSEXEC__ Marker Protocol

Unique to PowerShell integration:
- Enables command execution across process boundary
- Clean separation: agent generates, PowerShell executes
- Safety: User sees exact command before execution

### PowerShell Object Output

Unlike CLI's JSON, PowerShell cmdlets return structured objects:
- Rich formatting (tables, lists, custom)
- Pipeline compatibility
- Property access and filtering
- Native PowerShell experience

### ValueFromRemainingArguments

The function signature enables natural query syntax:
```powershell
# No quotes needed
ask how do I create a VM

# Not required (but still works)
ask "how do I create a VM"
```

This provides a more natural command-line experience.

### Alias Support

Three convenient aliases for different user preferences:
- `ask` - Brief, conversational
- `please` - Polite variant
- `pls` - Shortest form

All function identically, enabling users to choose their preferred style.

## Configuration

The PowerShell agent uses the same underlying CLI agent, so configuration is identical. See [agent-approaches.md](agent-approaches.md) for details on:
- Azure OpenAI endpoint and API key configuration
- Local model setup
- Azure Copilot Service configuration

The `AZURECLI_FLAVOR` environment variable is automatically set by `Invoke-AzAI` to ensure PowerShell-appropriate responses.

## Running PowerShell with Agent

### Module Import

First, import the Cirrus PowerShell modules:

```powershell
# Import meta-module (includes all Cirrus modules)
Import-Module ./bin/Cirrus.psd1

# Or import specific modules
Import-Module ./bin/Cirrus.Resources/Cirrus.Resources.psd1
```

### Using the Agent

The agent is available through `Invoke-AzAI` and its aliases:

```powershell
# Full function name (no quotes needed)
Invoke-AzAI how do I create a resource group

# Convenient aliases
ask show me all my resource groups
please list virtual machines
pls how do I create a storage account

# Natural language execution
ask create a resource group called mygroup in eastus
```

### Safety Considerations

For destructive operations, users can:

1. **Use "how to" questions** to get examples first:
   ```powershell
   ask how do I delete a resource group
   ```

2. **Review the command** before confirming:
   ```powershell
   ask delete resource group mygroup
   # Agent shows: Remove-AzResourceGroup -Name mygroup -Force
   # User can review before typing "yes"
   ```

3. **Manually execute** after reviewing:
   ```powershell
   # Get example from agent
   ask how do I delete a resource group
   
   # Manually execute with explicit parameters
   Remove-AzResourceGroup -Name mygroup -Force -Confirm:$false
   ```

## Implementation Status

**Current Status:** Design only. Initial implementation has been removed and may be re-added pending architectural review.

**Design Goals:**
- Maintain consistency with CLI agent experience
- Provide natural PowerShell command syntax
- Enable safe interactive confirmation
- Support PowerShell pipeline patterns
- Preserve PowerShell object-oriented output

**Pending Review:**
- Subprocess architecture validation
- `__PSEXEC__` marker security review
- Alternative execution models
- Integration with PowerShell module loading
- Error handling and telemetry

## Implementation Files (Design)

Files that would implement the PowerShell agent experience:

- `src/PS/Resources/AgentHelper.ps1` - `Invoke-AzAI` function and aliases
- `src/Core/Core.Agent/prompt.md` - System prompt (AZURECLI_FLAVOR detection)
- `src/PS/Resources/Cirrus.Resources.psd1` - Module manifest with exported functions

## References

- [Azure PowerShell Documentation](https://learn.microsoft.com/powershell/azure/)
- [Main Agent Experience Document](../agent-experience.md)
- [Agent Approaches](agent-approaches.md)
- [Azure CLI Agent Experience](agent-experience-azcli.md)
