# Azure CLI Agent Experience

| Status | Platform | Version |
|--------|----------|---------|
| ![Status](https://img.shields.io/badge/status-implemented-green) | Azure CLI | 2.x |

> **See Also:** For general agent concepts and capabilities, see [agent-experience.md](../agent-experience.md)

## Overview

The Azure CLI agent provides natural language command interpretation through an **exception-driven fallback pattern**. When standard command parsing fails, control automatically passes to the AI agent to interpret natural language input.

This document details the Azure CLI-specific implementation, including architecture, execution flow, and comprehensive examples.

## Architecture

### Exception-Driven Fallback Pattern

The CLI uses a try-catch pattern in `Program.cs` that attempts standard command parsing first, falling back to the agent only on failure:

```
┌──────────────────────────────────────────────────────────┐
│     User Input: "azv2 <command>"                         │
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
  ┌────────────────────┐   ┌──────────────────────┐
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
            ┌────────────────────┐      ┌──────────────────────┐
            │ sample_command     │      │ executable_command   │
            │ (with placeholders)│      │ (user confirms)      │
            └────────────────────┘      └──────────┬───────────┘
                                                   │
                                                   ▼
                                       ┌──────────────────────┐
                                       │ Recursive azv2 call  │
                                       └──────────────────────┘
```

### Integration Components

#### Program.cs Integration

The agent integrates at the highest level of the CLI application:

1. **Standard Path**: `ExtensionDispatcher` attempts to parse command
2. **Fallback Path**: On exception, `AgentEngine` receives raw input
3. **Execution**: Agent generates command, prompts for confirmation, executes recursively

#### ExtensionDispatcher

Routes commands to appropriate extension DLLs based on command structure:
- Parses command syntax (verb-noun patterns)
- Validates parameters
- Loads appropriate extension assembly
- On failure, throws exception caught by agent fallback

### Interactive Confirmation Flow

When the agent generates an executable command, it prompts the user for confirmation with AI-interpreted natural language responses:

```bash
Command:
azv2 group create --name mygroup --location westus

Execute this command? (yes/no): 
```

**User can respond with natural language:**
- Affirmative: "yes", "sure", "go ahead", "proceed", "do it", "ok"
- Negative: "no", "cancel", "stop", "quit", "exit"

The agent uses a language model to interpret the user's intent rather than hard-coded string matching. This provides a more natural interaction experience.

### Recursive Execution

When a command is confirmed:
1. Agent outputs the complete command string
2. CLI recursively calls itself: `Process.Start("azv2", "group create --name mygroup --location westus")`
3. ExtensionDispatcher parses and executes via standard path
4. Output streams back to user

This ensures generated commands follow the same validation and execution path as manually typed commands.

## CLI-Specific Examples

All examples demonstrate the **interactive** CLI behavior with AI-interpreted confirmation prompts.

### Scenario 1: Typo Correction

**User Intent:** Misspelled command

**Agent Behavior:** Recognizes typo and provides correct syntax examples (not executable)

```bash
$ azv2 I would like to logni

⠋ Thinking...

It looks like you want to log in. Here are the login commands:

Azure CLI:
azv2 login
azv2 login --service-principal --username <appId> --password <password> --tenant <tenantId>
azv2 login --use-device-code
```

**Outcome:** Agent provides help text with placeholders, no execution occurs.

---

### Scenario 2: "How To" Questions

**User Intent:** Learn how to perform a task

**Agent Behavior:** Provides command examples with placeholders for user-specific values

#### Example: Resource Group Creation

```bash
$ azv2 how do I create a resource group

⠋ Thinking...

To create a resource group in Azure:

Azure CLI:
azv2 group create --name <resource-group-name> --location <location>

# Example with common locations:
azv2 group create --name myResourceGroup --location eastus
azv2 group create --name myResourceGroup --location westus2
```

**Outcome:** Agent provides examples with placeholders, user manually executes with their own values.

#### Example: Virtual Machine Listing

```bash
$ azv2 how do I list all VMs

⠋ Thinking...

To list virtual machines in Azure:

Azure CLI:
# List all VMs in subscription
azv2 vm list

# List VMs in specific resource group
azv2 vm list --resource-group <rg-name>

# List with table output
azv2 vm list --output table

# List with specific properties
azv2 vm list --query "[].{Name:name, RG:resourceGroup, Location:location}" --output table
```

**Outcome:** Agent provides multiple examples demonstrating various options and output formats.

---

### Scenario 3: Natural Language Execution

**User Intent:** Execute a specific command with all required values provided

**Agent Behavior:** Generates complete executable command, prompts for confirmation with AI interpretation

#### Example: Resource Group Creation (Confirmed)

```bash
$ azv2 create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
azv2 group create --name mygroup --location westus

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

**Execution Flow:**
1. Agent generates complete command
2. Prompts user for confirmation (interactive)
3. User responds with "yes" (or any affirmative natural language)
4. Agent interprets response using language model
5. Recursively calls `azv2 group create --name mygroup --location westus`
6. Displays command output (JSON by default)

#### Example: Resource Group Creation (Natural Language Confirmation)

```bash
$ azv2 create a resource group called mygroup in westus

⠋ Thinking...

I'll create a resource group named "mygroup" in the westus region.

Command:
azv2 group create --name mygroup --location westus

Execute this command? (yes/no): sure, go ahead

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

**Note:** User responded with "sure, go ahead" - the agent interprets this as affirmative using the language model, not hard-coded string matching.

#### Example: Resource Group Deletion (Declined)

```bash
$ azv2 delete resource group mygroup

⠋ Thinking...

⚠️  Warning: This will permanently delete the resource group "mygroup" and all resources within it.

Command:
azv2 group delete --name mygroup --yes

Execute this command? (yes/no): no, I changed my mind

Operation cancelled.
```

**Outcome:** Agent respects user's decision, no deletion occurs. Natural language "no, I changed my mind" is interpreted as negative confirmation.

#### Example: Storage Account Creation with Tags

```bash
$ azv2 create a storage account called mystorageacct in resource group mygroup in eastus with tags environment=dev project=demo

⠋ Thinking...

I'll create a storage account named "mystorageacct" in resource group "mygroup" located in eastus with the specified tags.

Command:
azv2 storage account create --name mystorageacct --resource-group mygroup --location eastus --tags environment=dev project=demo

Execute this command? (yes/no): yes

⠋ Executing...

{
  "id": "/subscriptions/.../resourceGroups/mygroup/providers/Microsoft.Storage/storageAccounts/mystorageacct",
  "kind": "StorageV2",
  "location": "eastus",
  "name": "mystorageacct",
  "provisioningState": "Succeeded",
  "tags": {
    "environment": "dev",
    "project": "demo"
  }
}
```

**Outcome:** Agent correctly interprets complex request with multiple parameters and tags.

#### Example: VM Listing with Filters

```bash
$ azv2 show me all running VMs in eastus

⠋ Thinking...

I'll list all virtual machines in the eastus region and filter for running instances.

Command:
azv2 vm list --query "[?location=='eastus' && powerState=='VM running'].{Name:name, ResourceGroup:resourceGroup, Location:location, PowerState:powerState}" --output table

Execute this command? (yes/no): yes

⠋ Executing...

Name        ResourceGroup    Location    PowerState
----------  ---------------  ----------  ------------
webserver1  prod-rg          eastus      VM running
dbserver1   prod-rg          eastus      VM running
```

**Outcome:** Agent generates complex JMESPath query to filter results, demonstrates understanding of Azure VM power states.

---

### Edge Cases and Error Handling

#### Example: Ambiguous Request

```bash
$ azv2 create a resource group

⠋ Thinking...

To create a resource group, I need a bit more information:

What name would you like for the resource group?
```

**Agent Behavior:** Asks clarifying questions when required parameters are missing.

#### Example: Invalid Value Detection

```bash
$ azv2 create a resource group called my-group!@# in westus

⠋ Thinking...

I notice the resource group name "my-group!@#" contains invalid characters. Resource group names can only contain:
- Alphanumeric characters
- Hyphens, underscores, parentheses, and periods
- Cannot end with a period

Would you like to use "my-group" instead? (yes/no): yes

⠋ Executing...

{
  "id": "/subscriptions/.../resourceGroups/my-group",
  "location": "westus",
  "name": "my-group",
  "properties": {
    "provisioningState": "Succeeded"
  }
}
```

**Agent Behavior:** Validates parameters against Azure naming rules, suggests corrections.

## Configuration

The CLI agent shares configuration with other agent approaches. See [agent-approaches.md](agent-approaches.md) for details on:
- Azure OpenAI endpoint and API key configuration
- Local model setup
- Azure Copilot Service configuration

## Running CLI with Agent

The agent is automatically invoked when standard parsing fails:

```bash
# Standard commands work as usual
$ azv2 group list

# Natural language falls back to agent
$ azv2 show me all my resource groups

# Typos trigger agent correction
$ azv2 grup list

# "How to" questions invoke agent
$ azv2 how do I create a VM
```

No special flags or modes required - the agent seamlessly handles cases where traditional parsing fails.

## Implementation Files

Key files implementing the CLI agent experience:

- `src/CLI/Program.cs` - Exception-driven fallback entry point
- `src/Core/Core.Engine/AgentEngine.cs` - ICommandEngine implementation
- `src/Core/Core.Agent/CloudAgent.cs` - Azure OpenAI integration
- `src/Core/Core.Agent/prompt.md` - System prompt with CLI-specific instructions
- `src/CLI/Parser/ExtensionDispatcher.cs` - Standard command parser and router

## References

- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Main Agent Experience Document](../agent-experience.md)
- [Agent Approaches](agent-approaches.md)
